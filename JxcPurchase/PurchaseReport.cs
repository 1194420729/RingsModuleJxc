using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Rings.Models;
using Jxc.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JxcPurchase
{
    public class PurchaseReport : MarshalByRefObject
    {
        public Object RefreshMVWPurchaseBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object InitTimespanPurchaseReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object TimespanPurchaseReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where 1=1 ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                }
                else if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and vendorid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
                }
            }

            StringBuilder sborder = new StringBuilder();
            if (mysorting.Count > 0) sborder.Append(" order by ");
            int i = 0;
            foreach (string field in mysorting.Keys)
            {
                i++;
                sborder.AppendFormat(" {0} {1} {2}", field.ToLower(), mysorting[field], i == mysorting.Count ? "" : ",");
            }

            if (mysorting.Count == 0)
            {
                sborder.Append(" order by productcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
                            "sum(case when billname='purchasebill' then qty else -qty end)  as qty," +
                            "sum(case when billname='purchasebill' then total else -total end) as total," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end)-sum(case when billname='purchasebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='purchasebill' then total else -total end) /sum(case when billname='purchasebill' then qty else -qty end) as price," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) /sum(case when billname='purchasebill' then qty else -qty end) as discountprice " +
                            "from mvw_purchasebill " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid from mvw_purchasebill "
                                + sb.ToString()
                                + " group by productid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='purchasebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='purchasebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='purchasebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='purchasebill' then discounttotal else -discounttotal end)-sum(case when billname='purchasebill' then total else -total end),0) as taxtotal " +
                            "from mvw_purchasebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,

                data = list
            };
        }

        public Object InitVendorPurchaseReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("vendor");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object VendorPurchaseReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where 1=1 ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                }
                else if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from vendor where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and vendorid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and vendorid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
                }

            }

            StringBuilder sborder = new StringBuilder();
            if (mysorting.Count > 0) sborder.Append(" order by ");
            int i = 0;
            foreach (string field in mysorting.Keys)
            {
                i++;
                sborder.AppendFormat(" {0} {1} {2}", field.ToLower(), mysorting[field], i == mysorting.Count ? "" : ",");
            }

            if (mysorting.Count == 0)
            {
                sborder.Append(" order by discounttotal desc ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select vendorid,vendorcode,vendorname," +
                            "sum(case when billname='purchasebill' then total else -total end) as total," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end)-sum(case when billname='purchasebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='purchasebill' then total else -total end) /sum(case when billname='purchasebill' then qty else -qty end) as price," +
                            "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) /sum(case when billname='purchasebill' then qty else -qty end) as discountprice " +
                            "from mvw_purchasebill " +
                            sb.ToString() +
                            "group by vendorid,vendorcode,vendorname " +
                            sborder.ToString();

            string sqlCount = "with cte as (select vendorid,vendorcode,vendorname from mvw_purchasebill "
                                + sb.ToString()
                                + " group by vendorid,vendorcode,vendorname) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='purchasebill' then qty else -qty end),0) as qty," +
                           "coalesce(sum(case when billname='purchasebill' then total else -total end),0) as total," +
                           "coalesce(sum(case when billname='purchasebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                           "coalesce(sum(case when billname='purchasebill' then discounttotal else -discounttotal end)-sum(case when billname='purchasebill' then total else -total end),0) as taxtotal " +
                           "from mvw_purchasebill " +
                           sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }


        public Object InitDetailReport(string parameters)
        {
            DBHelper db = new DBHelper();
            ParameterHelper ph = new ParameterHelper(parameters);
            string reporttype = ph.GetParameterValue<string>("type");
            string tip = "";
            if (reporttype == "byproduct")
            {
                int productid = ph.GetParameterValue<int>("productid");
                var product = db.First("product", productid);
                tip = StringHelper.GetString("按产品") + ":" + product.content.Value<string>("code") + " " + product.content.Value<string>("name");
            }
            else if (reporttype == "byvendor")
            {
                int vendorid = ph.GetParameterValue<int>("vendorid");
                var vendor = db.First("vendor", vendorid);
                tip = StringHelper.GetString("按供应商") + ":" + vendor.content.Value<string>("code") + " " + vendor.content.Value<string>("name");
            }
            else if (reporttype == "byemployee")
            {
                int employeeid = ph.GetParameterValue<int>("employeeid");
                var employee = db.First("employee", employeeid);
                tip = StringHelper.GetString("按经手人") + ":" + employee.content.Value<string>("code") + " " + employee.content.Value<string>("name");
            }
            else if (reporttype == "bystock")
            {
                int stockid = ph.GetParameterValue<int>("stockid");
                var stock = db.First("stock", stockid);
                tip = StringHelper.GetString("按仓库") + ":" + stock.content.Value<string>("code") + " " + stock.content.Value<string>("name");
            }

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit,
                tip = tip
            };
        }

        public Object PurchaseBillDetailReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where 1=1 ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and vendorid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
                }
                else if (field == "productid")
                {
                    sb.AppendFormat(" and productid={0}", f);
                }
                else if (field == "vendorid")
                {
                    sb.AppendFormat(" and vendorid={0}", f);
                }
                else if (field == "employeeid")
                {
                    sb.AppendFormat(" and employeeid={0}", f);
                }
                else if (field == "stockid")
                {
                    sb.AppendFormat(" and stockid={0}", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            if (mysorting.Count > 0) sborder.Append(" order by ");
            int i = 0;
            foreach (string field in mysorting.Keys)
            {
                i++;
                sborder.AppendFormat(" content->>'{0}' {1} {2}", field.ToLower(), mysorting[field], i == mysorting.Count ? "" : ",");
            }

            if (mysorting.Count == 0)
            {
                sborder.Append(" order by id ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select id " +
                            "from mvw_purchasebill " +
                            sb.ToString() +
                            "group by id ";
            DataTable dtIds = db.QueryTable(sql);
            string filterids = dtIds.GetIds();
            sql = string.Format("select * from bill where id in ({0}) " + sborder, filterids);

            string sqlQtySum = "select coalesce(sum(case when billname='purchasebill' then qty else -qty end),0) as qty " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'qty')::decimal) as qty,content->>'billname' as billname from bill where id in (" + filterids + ")) as t";
            string sqlTotalSum = "select coalesce(sum(case when billname='purchasebill' then total else -total end),0) as total " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'discounttotal')::decimal) as total,content->>'billname' as billname from bill where id in (" + filterids + ")) as t";

            int recordcount = dtIds.Rows.Count;
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            var list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                item.content.Add("vendorname", db.First("vendor", item.content.Value<int>("vendorid")).content.Value<string>("name"));
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                decimal qty = item.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("qty"));
                decimal total = item.content.Value<decimal>("total");
                string billname = item.content.Value<string>("billname");
                item.content.Add("qty", billname == "purchasebill" ? qty : -qty);
                item.content["total"]= billname == "purchasebill" ? total : -total;
            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

        public Object RefreshMVWSaleBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object PurchaseSupplement(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);
             
            StringBuilder sb = new StringBuilder();
            sb.Append(" and coalesce(content->>'stop','')=''");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field.ToLower() == "vendorid")
                {
                    sb.AppendFormat(" and (content->>'vendorid')::int = {0}", f);
                }
               
            }

            StringBuilder sborder = new StringBuilder();

            sborder.Append(" order by content->>'code' ");


            //sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query("product", sb.ToString(), sborder.ToString(), out recordcount);

            decimal storageqtysum = decimal.Zero;
            decimal saleqtysum = decimal.Zero;
            foreach (var item in list)
            {
                string startdate=DateTime.Now.AddDays(-Convert.ToDouble(myfilter["saledate"])).ToString("yyyy-MM-dd");
                var saleqty = db.Scalar("select coalesce(sum(qty),0) as qty from mvw_salebill  where productid=" + item.id + " and billdate>='" + startdate+"'");
                item.content.Add("saleqty", Convert.ToDecimal(saleqty));
                
                saleqtysum += Convert.ToDecimal(saleqty);
                if (item.content["storage"] != null)
                {
                    storageqtysum += Convert.ToDecimal(item.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));
                }

                var vp=db.FirstOrDefault("select * from vendorproduct where (content->>'vendorid')::int="+myfilter["vendorid"]+" and (content->>'productid')::int="+item.id);
                if (vp != null)
                {
                    item.content.Add("lastbuyprice",vp.content["price"]);
                }
            }

            return new { resulttotal = recordcount, data = list, storageqtysum = storageqtysum, saleqtysum = saleqtysum };
        }

        public Object PurchaseSupplementSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int vendorid= ph.GetParameterValue<int>("vendorid");
            string products = ph.GetParameterValue<string>("products");
             
            TableModel purchaseorder = new TableModel()
            {
                id=0,
                content=new JObject()
            };

            using (DBHelper db = new DBHelper())
            {
                var stock = db.FirstOrDefault("select * from stock where coalesce(content->>'stop','')=''");
                if (stock == null)
                {
                    return new { message = StringHelper.GetString("请在基础资料中至少添加一个仓库！") };
                }

                purchaseorder.content.Add("makerid", PluginContext.Current.Account.Id);
                purchaseorder.content.Add("employeeid", PluginContext.Current.Account.Id);
                purchaseorder.content.Add("billname", "purchaseorder");
                purchaseorder.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
                purchaseorder.content.Add("billcode", BillHelper.GetBillCode("purchaseorder", db));
                purchaseorder.content.Add("stockid", stock.id);
                purchaseorder.content.Add("createtime", DateTime.Now);
                purchaseorder.content.Add("attachments", new JArray());
                purchaseorder.content.Add("vendorid", vendorid);

                JArray details = new JArray();
                var billconfig = db.FirstOrDefault("select * from billconfig where content->>'billname'='purchaseorder'");
                decimal taxrate = 0;
                if (billconfig != null && billconfig.content.Value<bool>("taxformat"))
                {
                    taxrate = billconfig.content.Value<decimal>("taxrate");
                }

                string[] ss = products.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in ss)
                {
                    string[] ss2 = s.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    JObject detail = new JObject();
                    decimal qty = Convert.ToDecimal(ss2[1]);
                    decimal taxprice = Convert.ToDecimal(ss2[2]);

                    detail.Add("uuid", Guid.NewGuid().ToString("N"));
                    detail.Add("productid", Convert.ToInt32(ss2[0]));
                    detail.Add("qty", qty);
                    detail.Add("taxprice", taxprice);
                    detail.Add("taxtotal", taxprice * qty);
                    detail.Add("taxrate", taxrate);
                    detail.Add("discountrate", 100);
                    detail.Add("discountprice", taxprice);
                    detail.Add("discounttotal", taxprice * qty);
                    detail.Add("price", taxprice / (100M + taxrate) * 100M);
                    detail.Add("total", taxprice * qty / (100M + taxrate) * 100M);

                    details.Add(detail);
                }
                purchaseorder.content.Add("details", details);

                db.Add("bill", purchaseorder);
                db.SaveChanges();
            }
            return new { message = "ok" };
        }
    }
}
