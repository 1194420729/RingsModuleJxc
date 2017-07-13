using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rings.Models;
using Npgsql;
using System.Data;
using Jxc.Utility;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Net;
using System.Configuration;

namespace JxcBillCenter
{
    public class BillCenter : MarshalByRefObject
    {
        public Object BillDetail(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int billid = ph.GetParameterValue<int>("billid");

            DBHelper db = new DBHelper();
            var bill = db.First("bill", billid);

            return new { billid = billid, billname = bill.content.Value<string>("billname") };

        }

        public Object DraftListQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where (content->>'auditstatus')::int=0");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
                else if (field == "billname")
                {
                    sb.AppendFormat(" and content->>'billname' = '{0}'", f);
                }
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and content->>'billcode' ilike '%{0}%'", f);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'productid')::int in ({0}))", ids);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'vendorid')::int in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'customerid')::int in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'employeeid')::int in ({0})", ids);
                }
                else if (field == "makername")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'makerid')::int in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'stockid')::int in ({0})", ids);
                }
                else if (field == "comment")
                {
                    sb.AppendFormat(" and content->>'comment' ilike '%{0}%'", f);
                }
                else if (field == "detailcomment")
                {
                    sb.AppendFormat(" and exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'comment') ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate' desc,id desc ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bill "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from bill "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total " +
                                "from  bill " + sb.ToString();


            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            List<TableModel> list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                string wldw = "";
                if (item.content["vendorid"] != null)
                    wldw = db.First("vendor", item.content.Value<int>("vendorid")).content.Value<string>("name");
                else if (item.content["customerid"] != null)
                    wldw = db.First("customer", item.content.Value<int>("customerid")).content.Value<string>("name");

                item.content.Add("wldw", wldw);
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
            }

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
                showcost = showcost
            };
        }

        [MyLog("复制业务草稿")]
        public Object Copy(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");

            using (DBHelper db = new DBHelper())
            {
                var bill = db.First("bill", id);
                bill.id = 0;
                bill.content["billcode"] = BillHelper.GetBillCode(bill.content.Value<string>("billname"), db);
                bill.content["billdate"] = DateTime.Now.ToString("yyyy-MM-dd");
                bill.content["makerid"] = PluginContext.Current.Account.Id;
                bill.content["createtime"] = DateTime.Now;
                bill.content["auditstatus"] = 0;
                bill.content.Remove("auditorid");
                bill.content.Remove("audittime");
                bill.content.Remove("abort");
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    item.Remove("deliveryqty");
                    item["uuid"] = Guid.NewGuid().ToString("N");
                }
                db.Add("bill", bill);

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("删除业务草稿")]
        public Object Delete(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                db.RemoveRange("bill", ids);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("批量过账")]
        public Object Audit(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                List<TableModel> bills = new List<TableModel>();

                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能批量过账！") };
                }

                foreach (string s in ss)
                {
                    var bill = db.First("bill", int.Parse(s));
                    bills.Add(bill);

                    //检查是否草稿
                    if (bill.content["auditorid"] != null)
                    {
                        return new { message = StringHelper.GetString("已审核的单据不能再次审核！") };
                    }
                    //单据日期不能早于开账日期
                    if (bill.content.Value<DateTime>("billdate").CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                    {
                        return new { message = StringHelper.GetString("单据日期不能早于开账日期！") };
                    }

                    //单据日期不能早于月结存日期
                    var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                    if (lastbalance != null
                        && bill.content.Value<DateTime>("billdate").CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                    {
                        return new { message = StringHelper.GetString("单据日期不能早于月结存日期！") };
                    }

                    //检查编号重复
                    int cnt = db.Count("select count(0) as cnt from bill where id<>" + bill.id
                        + " and content->>'billname'='" + bill.content.Value<string>("billname")
                        + "' and content->>'billcode'='" + bill.content.Value<string>("billcode") + "'");
                    if (cnt > 0)
                    {
                        return new { message = StringHelper.GetString("编号有重复") };
                    }

                }


                //重新排序
                bills.OrderBy(c => c.content.Value<string>("billdate")).ThenBy(c => c.id).ToList();

                //审核 
                foreach (var item in bills)
                {
                    string result = AuditBill(item);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return new { message = "【" + item.content.Value<string>("billcode") + "】" + result };
                    }
                }

            }

            return new { message = "ok" };

        }

        private string AuditBill(TableModel bill)
        {
            string billname = bill.content.Value<string>("billname");
            bill.content["auditorid"] = PluginContext.Current.Account.Id;
            bill.content["audittime"] = DateTime.Now;
            bill.content["auditstatus"] = 1;

            using (DBHelper db = new DBHelper())
            {
                db.Edit("bill", bill);
                string msg = "";
                bill.Audit(db, out msg);

                if (!string.IsNullOrEmpty(msg))
                    return msg;

                db.SaveChanges();
            }

            return string.Empty;
        }

        public Object BizHistoryQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where (content->>'billname')<>'purchaseorder' and (content->>'billname')<>'saleorder'");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
                else if (field == "auditstatus")
                {
                    if (f == "nored")
                        sb.Append(" and (content->>'auditstatus')::int=1");
                    else if (f == "all")
                        sb.Append(" and (content->>'auditstatus')::int>=1");
                    else if (f == "justred")
                        sb.Append(" and (content->>'auditstatus')::int>1");
                }
                else if (field == "billname")
                {
                    sb.AppendFormat(" and content->>'billname' = '{0}'", f);
                }
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and content->>'billcode' ilike '%{0}%'", f);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'productid')::int in ({0}))", ids);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'vendorid')::int in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'customerid')::int in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'employeeid')::int in ({0})", ids);
                }
                else if (field == "makername")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'makerid')::int in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'stockid')::int in ({0})", ids);
                }
                else if (field == "comment")
                {
                    sb.AppendFormat(" and content->>'comment' ilike '%{0}%'", f);
                }
                else if (field == "detailcomment")
                {
                    sb.AppendFormat(" and exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'comment') ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate' desc,id desc ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bill "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from bill "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total " +
                                "from  bill " + sb.ToString();


            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            List<TableModel> list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                string wldw = "";
                if (item.content["vendorid"] != null)
                    wldw = db.First("vendor", item.content.Value<int>("vendorid")).content.Value<string>("name");
                else if (item.content["customerid"] != null)
                    wldw = db.First("customer", item.content.Value<int>("customerid")).content.Value<string>("name");

                item.content.Add("wldw", wldw);
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
            }

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
                showcost = showcost
            };
        }


        [MyLog("修改单据备注")]
        public Object BillComment(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");
            string comment = ph.GetParameterValue<string>("comment");


            using (DBHelper db = new DBHelper())
            {
                var bill = db.First("bill", id);
                bill.content["comment"] = comment;
                db.Edit("bill", bill);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("红字反冲")]
        public Object RedWord(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");

            using (DBHelper db = new DBHelper(true))
            {
                var bill = db.First("bill", id);

                //原单记蓝字
                bill.content["auditstatus"] = 2;
                db.Edit("bill", bill);

                //复制一张红字草稿
                bill.id = 0;
                bill.content["billcode"] = bill.content.Value<string>("billcode") + "_1";
                bill.content["billdate"] = DateTime.Now.ToString("yyyy-MM-dd");
                bill.content["makerid"] = PluginContext.Current.Account.Id;
                bill.content["createtime"] = DateTime.Now;
                bill.content["auditstatus"] = 3;
                bill.content["auditorid"] = PluginContext.Current.Account.Id;
                bill.content["audittime"] = DateTime.Now;
                if (bill.content["total"] != null)
                {
                    bill.content["total"] = -bill.content.Value<decimal>("total");
                }
                if (bill.content["details"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                    {
                        if (item["qty"] != null)
                            item["qty"] = -item.Value<decimal>("qty");
                        if (item["total"] != null)
                            item["total"] = -item.Value<decimal>("total");
                        if (item["taxtotal"] != null)
                            item["taxtotal"] = -item.Value<decimal>("taxtotal");
                        if (item["discounttotal"] != null)
                            item["discounttotal"] = -item.Value<decimal>("discounttotal");
                        if (item["costtotal"] != null)
                            item["costtotal"] = -item.Value<decimal>("costtotal");

                        item["uuid"] = Guid.NewGuid().ToString("N");
                    }
                }
                db.Add("bill", bill);

                //红冲
                string msg = "";
                bill.RedWordAudit(db, out msg);

                if (!string.IsNullOrEmpty(msg))
                    return new { message = msg };

                db.SaveChanges();
            }

            return new { message = "ok" };

        }

    }


}
