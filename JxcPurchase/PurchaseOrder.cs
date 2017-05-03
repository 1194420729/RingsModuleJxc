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
using System.Net;
using System.Text;

namespace JxcPurchase
{
    public class PurchaseOrder : MarshalByRefObject
    {
        private string billname = "purchaseorder";
        public Object Init(string parameters)
        {
            string billcode = BillHelper.GetBillCode(billname);
            JObject content = new JObject();
            content.Add("billcode", billcode);
            content.Add("billname", billname);
            content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));

            DBHelper db = new DBHelper();
            var billconfig = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
            if (billconfig == null)
            {
                billconfig = new TableModel()
                {
                    id = 0,
                    content = new JObject()
                };
                billconfig.content.Add("billname", billname);
                billconfig.content.Add("taxformat", false);
                billconfig.content.Add("discountformat", false);
                billconfig.content.Add("taxrate", 0);
                billconfig.content.Add("showstandard", false);
                billconfig.content.Add("showtype", false);
                billconfig.content.Add("showunit", false);
                billconfig.content.Add("showarea", false);
                billconfig.content.Add("showbarcode", false);
                billconfig.content.Add("showstorage", false);
                billconfig.content.Add("keepemployeeandstock", true);
            }

            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
            bool noeditbillcode = option.content["noeditbillcode"] == null ? false : option.content.Value<bool>("noeditbillcode");
            bool noeditbilldate = option.content["noeditbilldate"] == null ? false : option.content.Value<bool>("noeditbilldate");

            return new
            {
                content = content,
                makername = PluginContext.Current.Account.Name,
                billconfig = billconfig,
                digit = digit,
                noeditbillcode = noeditbillcode,
                noeditbilldate = noeditbilldate
            };
        }

        public Object PurchaseOrderQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(" where content->>'billname'='{0}'", billname);

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
                else if (field == "status")
                {
                    if (f == "draft") sb.Append(" and (content->>'auditstatus')::int=0");
                    else if (f == "audited") sb.Append(" and (content->>'auditstatus')::int=1");
                }
                else if (field == "finish")
                {
                    if (f == "aborted")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=true");
                    }
                    else if (f == "finished")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=false and not exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'qty')::decimal>coalesce(x->>'deliveryqty','0')::decimal)");
                    }
                    else if (f == "unfinish")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=false and exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'qty')::decimal>coalesce(x->>'deliveryqty','0')::decimal)");
                    }
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate' desc,id desc ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bill "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from bill "
                        + sb.ToString();

            string sqlQtySum = "select coalesce(sum(qty),0) as qty " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'qty')::decimal) as qty from bill " + sb.ToString() +
                                ") as t";
            string sqlTotalSum = "select coalesce(sum(total),0) as total " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'discounttotal')::decimal) as total from bill " + sb.ToString() +
                                ") as t";
            string sqlDeliveryQtySum = "select coalesce(sum(deliveryqty),0) as deliveryqty " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'deliveryqty')::decimal) as deliveryqty from bill " + sb.ToString() +
                                ") as t";

            int recordcount = db.Count(sqlCount);
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal deliveryqtysum = Convert.ToDecimal(db.Scalar(sqlDeliveryQtySum));

            List<TableModel> list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                item.content.Add("vendorname", db.First("vendor", item.content.Value<int>("vendorid")).content.Value<string>("name"));
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                item.content.Add("qty", item.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("qty")));
                item.content.Add("deliveryqty", item.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("deliveryqty")));
            }

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                deliveryqtysum = deliveryqtysum,
                data = list,
                showcost = showcost
            };
        }
        
        public Object LoadBillQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(" where content->>'billname'='{0}'", billname);

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
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and content->>'billcode' ilike '%{0}%'", f);
                }
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'vendorid')::int in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'employeeid')::int in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'stockid')::int in ({0})", ids);
                }
                else if (field == "comment")
                {
                    sb.AppendFormat(" and content->>'comment' ilike '%{0}%'", f);
                }
                else if (field == "status")
                {
                    if (f == "draft") sb.Append(" and (content->>'auditstatus')::int=0");
                    else if (f == "audited") sb.Append(" and (content->>'auditstatus')::int=1");
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate' desc,id desc ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bill "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from bill "
                        + sb.ToString();

            int recordcount = db.Count(sqlCount);
            List<TableModel> list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                item.content.Add("vendorname", db.First("vendor", item.content.Value<int>("vendorid")).content.Value<string>("name"));
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
            }

            return new { resulttotal = recordcount, data = list };
        }

        public Object LoadBill(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");

            DBHelper db = new DBHelper();
            var bill = db.First("bill", id);
            var maker = db.First("employee", bill.content.Value<int>("makerid"));
            var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));
            var employee = db.First("employee", bill.content.Value<int>("employeeid"));
            var stock = db.First("stock", bill.content.Value<int>("stockid"));

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                item.Add("product", db.First("product", item.Value<int>("productid")).content);
            }

            var billconfig = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
            if (billconfig == null)
            {
                billconfig = new TableModel()
                {
                    id = 0,
                    content = new JObject()
                };
                billconfig.content.Add("billname", billname);
                billconfig.content.Add("taxformat", false);
                billconfig.content.Add("discountformat", false);
                billconfig.content.Add("taxrate", 0);
                billconfig.content.Add("showstandard", false);
                billconfig.content.Add("showtype", false);
                billconfig.content.Add("showunit", false);
                billconfig.content.Add("showarea", false);
                billconfig.content.Add("showbarcode", false);
                billconfig.content.Add("showstorage", false);
                billconfig.content.Add("keepemployeeandstock", true);
            }

            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
            bool noeditbillcode = option.content["noeditbillcode"] == null ? false : option.content.Value<bool>("noeditbillcode");
            bool noeditbilldate = option.content["noeditbilldate"] == null ? false : option.content.Value<bool>("noeditbilldate");

            return new
            {
                content = bill.content,
                makername = maker.content.Value<string>("name"),
                billconfig = billconfig,
                digit = digit,
                vendorname = vendor.content.Value<string>("name"),
                employeename = employee.content.Value<string>("name"),
                stockname = stock.content.Value<string>("name"),
                noeditbillcode = noeditbillcode,
                noeditbilldate = noeditbilldate
            };
        }

        [MyLog("配置采购订单格式")]
        public Object BillConfigSave(string parameters)
        {
            TableModel billconfig = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                if (billconfig.id > 0)
                {
                    db.Edit("billconfig", billconfig);
                }
                else
                {
                    db.Add("billconfig", billconfig);
                }

                db.SaveChanges();
            }
            return new { message = "ok", id = billconfig.id };

        }

        [MyLog("Excel导入")]
        public Object ImportData(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string path = ph.GetParameterValue<string>("path");
            decimal taxrate = ph.GetParameterValue<decimal>("taxrate");
            path = ContextServiceHelper.MapPath(path);

            int rowno = 0;

            try
            {
                string ext = Path.GetExtension(path).ToLower();

                //导入数据
                IWorkbook workbook = null;
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                if (ext == ".xls")
                {
                    workbook = new HSSFWorkbook(fs);
                }
                else
                {
                    workbook = new XSSFWorkbook(fs);
                }
                var sheet = workbook.GetSheetAt(0);
                int rowcount = sheet.LastRowNum + 1;

                StringBuilder sb = new StringBuilder();
                DBHelper db = new DBHelper();
                List<JObject> list = new List<JObject>();

                for (int i = 1; i < rowcount; i++)
                {
                    #region 逐行导入
                    rowno = i + 1;

                    IRow row = sheet.GetRow(i);
                    string code = row.GetCell(0).GetCellValue().ToString();
                    if (string.IsNullOrEmpty(code))
                        continue;

                    //检查编号
                    var product = db.FirstOrDefault("select * from product where content->>'code'='" + code + "' and coalesce(content->>'stop','')=''");

                    if (product == null)
                    {
                        sb.AppendFormat("第{0}行出现错误：产品编号不存在！<br/>", rowno);
                        continue;
                    }

                    decimal qty = Convert.ToDecimal(row.GetCell(2).GetCellValue());
                    decimal price = Convert.ToDecimal(row.GetCell(3).GetCellValue());
                    decimal total = Convert.ToDecimal(row.GetCell(4).GetCellValue());
                    string comment = row.GetCell(5).GetCellValue().ToString();
                    JObject detail = new JObject();
                    detail.Add("productid", product.id);
                    detail.Add("product", product.content);
                    detail.Add("qty", qty);
                    detail.Add("taxprice", price);
                    detail.Add("taxtotal", total);
                    detail.Add("discountprice", price);
                    detail.Add("discounttotal", total);
                    detail.Add("discountrate", 100M);
                    detail.Add("taxrate", taxrate);

                    var option = db.First("select * from option");
                    int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
                    //var billconfig = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
                    //decimal taxrate = decimal.Zero;
                    //if (billconfig != null && billconfig.content.Value<bool>("taxformat") && billconfig.content["taxrate"] != null)
                    //{
                    //    taxrate = billconfig.content.Value<decimal>("taxrate");
                    //}

                    detail.Add("price", Math.Round(price / (1m + taxrate / 100m), digit));
                    detail.Add("total", Math.Round(total / (1m + taxrate / 100m), digit));
                    detail.Add("comment", comment);

                    list.Add(detail);
                    #endregion

                }

                if (sb.Length > 0)
                {
                    return new { message = sb.ToString() };
                }

                return new { message = "ok", data = list };
            }
            catch (Exception ex)
            {
                //Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return new { message = "导入出错(" + rowno + ")" + ex.Message };
            }

        }

        [MyLog("采购订单编号设置")]
        public Object BillCodeConfigSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string template = ph.GetParameterValue<string>("template");

            using (DBHelper db = new DBHelper())
            {
                //获取编号配置
                var config = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
                if (config == null)
                {
                    TableModel billconfig = new TableModel();
                    billconfig.content = new JObject();
                    billconfig.content.Add("billcodetemplate", template);

                    db.Add("billconfig", billconfig);
                }
                else
                {
                    config.content["billcodetemplate"] = template;

                    db.Edit("billconfig", config);
                }

                db.SaveChanges();
            }
            return new { message = "ok", newcode = BillHelper.GetBillCode(billname) };

        }

        [MyLog("新增采购订单")]
        public Object AddSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能保存单据！") };
                }

                //单据日期不能早于开账日期
                if (model.content.Value<DateTime>("billdate").CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于开账日期！") };
                }

                //单据日期不能早于月结存日期
                var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                if (lastbalance != null 
                    && model.content.Value<DateTime>("billdate").CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于月结存日期！") };
                }

                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from bill where content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }

                foreach (var item in model.content.Value<JArray>("details").Values<JObject>())
                {
                    item.Remove("product");
                }

                model.content["billname"] = billname;
                model.content["makerid"] = PluginContext.Current.Account.Id;
                model.content["createtime"] = DateTime.Now;
                model.content["auditstatus"] = 0;
                model.content["total"] = model.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));

                db.Add("bill", model);
                db.SaveChanges();

                bool nodraftprint = options.content["nodraftprint"] != null && options.content.Value<bool>("nodraftprint");

                return new { message = "ok", id = model.id, nodraftprint = nodraftprint };
            }
        }

        [MyLog("保存并审核采购订单")]
        public Object AddAuditSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能保存单据！") };
                }

                //单据日期不能早于开账日期
                if (model.content.Value<DateTime>("billdate").CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于开账日期！") };
                }

                //单据日期不能早于月结存日期
                var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                if (lastbalance != null
                    && model.content.Value<DateTime>("billdate").CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于月结存日期！") };
                }

                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from bill where content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }

                foreach (var item in model.content.Value<JArray>("details").Values<JObject>())
                {
                    item.Remove("product");
                }

                model.content["billname"] = billname;
                model.content["makerid"] = PluginContext.Current.Account.Id;
                model.content["createtime"] = DateTime.Now;
                model.content["auditorid"] = PluginContext.Current.Account.Id;
                model.content["audittime"] = DateTime.Now;
                model.content["auditstatus"] = 1;
                model.content["total"] = model.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));

                db.Add("bill", model);

                model.Audit(db);

                db.SaveChanges();
            }
            return new { message = "ok", id = model.id };

        }

        [MyLog("编辑采购订单")]
        public Object EditSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能保存单据！") };
                }

                //单据日期不能早于开账日期
                if (model.content.Value<DateTime>("billdate").CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于开账日期！") };
                }

                //单据日期不能早于月结存日期
                var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                if (lastbalance != null
                    && model.content.Value<DateTime>("billdate").CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于月结存日期！") };
                }


                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from bill where id<>" + model.id + " and content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }

                foreach (var item in model.content.Value<JArray>("details").Values<JObject>())
                {
                    item.Remove("product");
                }

                //model.content["billname"] = billname;
                model.content["makerid"] = PluginContext.Current.Account.Id;
                //model.content["createtime"] = DateTime.Now;
                model.content["total"] = model.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));

                db.Edit("bill", model);
                db.SaveChanges();

                bool nodraftprint = options.content["nodraftprint"] != null && options.content.Value<bool>("nodraftprint");

                return new { message = "ok", id = model.id, nodraftprint = nodraftprint };
            }
        }

        [MyLog("编辑并审核采购订单")]
        public Object EditAuditSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                //检查是否已经审核过
                if (model.content["auditorid"] != null)
                {
                    return new { message = StringHelper.GetString("单据已经审核过，不能修改！") };
                }

                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能保存单据！") };
                }

                //单据日期不能早于开账日期
                if (model.content.Value<DateTime>("billdate").CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于开账日期！") };
                }

                //单据日期不能早于月结存日期
                var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                if (lastbalance != null
                    && model.content.Value<DateTime>("billdate").CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                {
                    return new { message = StringHelper.GetString("单据日期不能早于月结存日期！") };
                }

                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from bill where id<>" + model.id + " and content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }

                foreach (var item in model.content.Value<JArray>("details").Values<JObject>())
                {
                    item.Remove("product");
                }

                //model.content["billname"] = billname;
                model.content["makerid"] = PluginContext.Current.Account.Id;
                //model.content["createtime"] = DateTime.Now;
                model.content["auditorid"] = PluginContext.Current.Account.Id;
                model.content["audittime"] = DateTime.Now;
                model.content["auditstatus"] = 1;
                model.content["total"] = model.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));

                db.Edit("bill", model);

                model.Audit(db);

                db.SaveChanges();
            }
            return new { message = "ok", id = model.id };

        }

        [MyLog("删除采购订单")]
        public Object Delete(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                //检查是否已经被入库单引用
                foreach (string s in ss)
                {
                    var purchasebill = db.FirstOrDefault("select * from bill where content->'details' @> '[{\"purchaseorderid\":" + s + "}]'");
                    if (purchasebill != null)
                    {
                        return new { message = StringHelper.GetString("订单已被引用，不能删除") };
                    }

                }

                //反冲虚拟库存
                var options = db.First("select * from option");
                if (options.content.Value<bool>("strictordermanage"))
                {
                    foreach (string s in ss)
                    {
                        var purchaseorder = db.First("bill", Convert.ToInt32(s));
                        if (purchaseorder.content["auditorid"] != null)
                        {
                            purchaseorder.UnModifyVirtualStorage(db);
                        }

                    }
                }

                db.RemoveRange("bill", ids);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("中止采购订单")]
        public Object Abort(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                //检查是否草稿 
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));
                    if (purchaseorder.content["auditorid"] == null)
                    {
                        return new { message = StringHelper.GetString("草稿无需中止，可以直接删除") };
                    }

                    if (purchaseorder.content.Value<bool>("abort") == true)
                    {
                        return new { message = StringHelper.GetString("已经中止的订单，不能再次中止") };
                    }
                }

                //中止并反冲虚拟库存
                var options = db.First("select * from option");
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", Convert.ToInt32(s));
                    purchaseorder.content["abort"] = true;
                    db.Edit("bill", purchaseorder);

                    if (options.content.Value<bool>("strictordermanage"))
                    {
                        purchaseorder.ModifyVirtualStorageForAbort(db);
                    }
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("取消中止采购订单")]
        public Object UnAbort(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                //检查是否已经中止 
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));

                    if (purchaseorder.content.Value<bool>("abort") == false)
                    {
                        return new { message = StringHelper.GetString("未中止的订单，不能取消中止") };
                    }
                }

                //取消中止并更新虚拟库存
                var options = db.First("select * from option");
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));

                    purchaseorder.content.Remove("abort");
                    db.Edit("bill", purchaseorder);
                    if (options.content.Value<bool>("strictordermanage"))
                    {
                        purchaseorder.ModifyVirtualStorageForUnAbort(db);
                    }
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("批量审核采购订单")]
        public Object Audit(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                //检查是否草稿
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));
                    if (purchaseorder.content["auditorid"] != null)
                    {
                        return new { message = StringHelper.GetString("已审核的单据不能再次审核！") };
                    }
                }

                //审核并更新虚拟库存和价格表
                var options = db.First("select * from option");
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));

                    purchaseorder.content["auditorid"] = PluginContext.Current.Account.Id;
                    purchaseorder.content["audittime"] = DateTime.Now;

                    db.Edit("bill", purchaseorder);

                    var billaudit = purchaseorder.GetBillAudit(db);

                    if (options.content.Value<bool>("strictordermanage"))
                    {
                        billaudit.AddVirtualStorage();
                    }

                    //写入价格表
                    if (options.content.Value<bool>("purchasepricekeep"))
                    {
                        billaudit.ModifyPurchasePrice();
                    }
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("修改采购订单备注")]
        public Object BillComment(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");
            string comment = ph.GetParameterValue<string>("comment");


            using (DBHelper db = new DBHelper())
            {
                var purchaseorder = db.First("bill", id);
                purchaseorder.content["comment"] = comment;
                db.Edit("bill", purchaseorder);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("复制采购订单")]
        public Object Copy(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string ids = ph.GetParameterValue<string>("ids");

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            using (DBHelper db = new DBHelper())
            {
                foreach (string s in ss)
                {
                    var purchaseorder = db.First("bill", int.Parse(s));
                    purchaseorder.id = 0;
                    purchaseorder.content["billcode"] = BillHelper.GetBillCode("purchaseorder", db);
                    purchaseorder.content["billdate"] = DateTime.Now.ToString("yyyy-MM-dd");
                    purchaseorder.content["makerid"] = PluginContext.Current.Account.Id;
                    purchaseorder.content["createtime"] = DateTime.Now;
                    purchaseorder.content["auditstatus"] = 0;
                    purchaseorder.content.Remove("auditorid");
                    purchaseorder.content.Remove("audittime");
                    purchaseorder.content.Remove("abort");
                    foreach (var item in purchaseorder.content.Value<JArray>("details").Values<JObject>())
                    {
                        item.Remove("deliveryqty");
                        item["uuid"] = Guid.NewGuid().ToString("N");
                    }
                    db.Add("bill", purchaseorder);
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        public Object RegisterPrintModel(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int billid = ph.GetParameterValue<int>("billid");

            DBHelper db = new DBHelper();
            var bill = db.First("bill", billid);
            var employee = db.First("employee", bill.content.Value<int>("employeeid"));
            var maker = db.First("employee", bill.content.Value<int>("makerid"));
            var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));
            var stock = db.First("stock", bill.content.Value<int>("stockid"));

            decimal total = bill.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("total"));
            decimal taxtotal = bill.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("taxtotal"));
            decimal discounttotal = bill.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));
            decimal qty = bill.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("qty"));

            PrintData pd = new PrintData();
            pd.HeaderField = new List<string>() 
            {
                "单据编号",
                "单据日期",
                "经手人",
                "仓库名称",
                "供应商名称",
                "供应商联系人",
                "供应商电话",
                "供应商地址",
                "交货日期",
                "备注",
                "系统日期",
                "系统时间",
                "未税金额",
                "未税金额大写",
                "含税金额",
                "含税金额大写",
                "折后金额",
                "折后金额大写",
                "数量"
            };

            pd.HeaderValue = new Dictionary<string, string>()
            {
                {"单据编号",bill.content.Value<string>("billcode")},
                {"单据日期",bill.content.Value<string>("billdate")},
                {"经手人",employee.content.Value<string>("name")},
                {"仓库名称",stock.content.Value<string>("name")},
                {"供应商名称",vendor.content.Value<string>("name")},
                {"供应商联系人",vendor.content.Value<string>("linkman")},
                {"供应商电话",vendor.content.Value<string>("linkmobile")},
                {"供应商地址",vendor.content.Value<string>("address")},
                {"交货日期",bill.content.Value<string>("deliverydate")},
                {"备注",bill.content.Value<string>("comment")},
                {"系统日期",DateTime.Now.ToString("yyyy-MM-dd")},
                {"系统时间",DateTime.Now.ToString("yyyy-MM-dd HH:mm")},
                {"未税金额",total.ToString("n2")},
                {"未税金额大写",MoneyHelper.ConvertSum(total)},
                {"含税金额",taxtotal.ToString("n2")},
                {"含税金额大写",MoneyHelper.ConvertSum(taxtotal)},
                {"折后金额",discounttotal.ToString("n2")},
                {"折后金额大写",MoneyHelper.ConvertSum(discounttotal)},
                {"数量",qty.ToString()}
            };

            pd.DetailField = new List<string>()
            {
                "行号",
                "产品编号",
                "产品名称",
                "规格",
                "型号",
                "产地",
                "计量单位",
                "条码",
                "数量",
                "库存数量",
                "已到数量",
                "单价",
                "金额",
                "含税单价",
                "含税金额",
                "税率",
                "折后单价",
                "折后金额",
                "折扣率",
                "备注"
            };

            int stockid = stock.content.Value<int>("id");
            pd.DetailValue = new List<Dictionary<string, string>>();
            int i = 0;
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                var product = db.First("product", item.Value<int>("productid"));
                decimal storageqty = decimal.Zero;
                var storage = product.content.Value<JObject>("storage");
                if (storage != null)
                {
                    var s = storage.Value<JObject>(stockid.ToString());
                    if (s != null) storageqty = s.Value<decimal>("qty");
                }
                Dictionary<string, string> detail = new Dictionary<string, string>();
                i++;
                detail.Add("行号", i.ToString());
                detail.Add("产品编号", product.content.Value<string>("code"));
                detail.Add("产品名称", product.content.Value<string>("name"));
                detail.Add("规格", product.content.Value<string>("standard"));
                detail.Add("型号", product.content.Value<string>("type"));
                detail.Add("产地", product.content.Value<string>("area"));
                detail.Add("计量单位", product.content.Value<string>("unit"));
                detail.Add("条码", product.content.Value<string>("barcode"));
                detail.Add("数量", item.Value<decimal>("qty").ToString());
                detail.Add("库存数量", storageqty.ToString());
                detail.Add("已到数量", item.Value<decimal>("deliveryqty").ToString());
                detail.Add("单价", item.Value<decimal>("price").ToString("n2"));
                detail.Add("金额", item.Value<decimal>("total").ToString("n2"));
                detail.Add("含税单价", item.Value<decimal>("taxprice").ToString("n2"));
                detail.Add("含税金额", item.Value<decimal>("taxtotal").ToString("n2"));
                detail.Add("税率", item.Value<decimal>("taxrate").ToString());
                detail.Add("折后单价", item.Value<decimal>("discountprice").ToString("n2"));
                detail.Add("折后金额", item.Value<decimal>("discounttotal").ToString("n2"));
                detail.Add("折扣率", item.Value<decimal>("discountrate").ToString());
                detail.Add("备注", item.Value<string>("comment"));
                pd.DetailValue.Add(detail);

            }

            PrintManager pm = new PrintManager(PluginContext.Current.Account.ApplicationId);
            int modelid = pm.RegisterPrintModel(pd);

            return new { modelid = modelid };
        }

        public Object RefreshMVWPurchaseOrder(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchaseorder");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object InitProductReport(string parameters)
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

        public Object PurchaseOrderProductReport(string parameters)
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

            string sql = "select productid,productcode,productname,producttype,productstandard," +
                            "sum(qty) as qty,sum(total) as total,sum(discounttotal) as discounttotal," +
                            "sum(discounttotal)-sum(total) as taxtotal," +
                            "sum(total) /sum(qty) as price,sum(discounttotal) /sum(qty) as discountprice " +
                            "from mvw_purchaseorder " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid,productcode,productname,producttype,productstandard from mvw_purchaseorder "
                                + sb.ToString()
                                + " group by productid,productcode,productname,producttype,productstandard) select count(*) from cte";

            string sqlSum = "select coalesce(sum(qty),0) as qty,coalesce(sum(total),0) as total," +
                            "coalesce(sum(discounttotal),0) as discounttotal," +
                            "coalesce(sum(discounttotal)-sum(total),0) as taxtotal " +
                            "from mvw_purchaseorder " +
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

        public Object InitVendorReport(string parameters)
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

        public Object PurchaseOrderVendorReport(string parameters)
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
                sborder.Append(" order by vendorcode ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select vendorid,vendorcode,vendorname," +
                            "sum(qty) as qty,sum(total) as total,sum(discounttotal) as discounttotal," +
                            "sum(discounttotal)-sum(total) as taxtotal," +
                            "sum(total) /sum(qty) as price,sum(discounttotal) /sum(qty) as discountprice " +
                            "from mvw_purchaseorder " +
                            sb.ToString() +
                            "group by vendorid,vendorcode,vendorname " +
                            sborder.ToString();

            string sqlCount = "with cte as (select vendorid,vendorcode,vendorname from mvw_purchaseorder "
                                + sb.ToString()
                                + " group by vendorid,vendorcode,vendorname) select count(*) from cte";

            string sqlSum = "select coalesce(sum(qty),0) as qty,coalesce(sum(total),0) as total," +
                            "coalesce(sum(discounttotal),0) as discounttotal," +
                            "coalesce(sum(discounttotal)-sum(total),0) as taxtotal " +
                            "from mvw_purchaseorder " +
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

        public Object InitEmployeeReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit
            };
        }

        public Object PurchaseOrderEmployeeReport(string parameters)
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
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and vendorid in ({0})", ids);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
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
                sborder.Append(" order by employeecode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select employeeid,employeecode,employeename," +
                            "sum(qty) as qty,sum(total) as total,sum(discounttotal) as discounttotal," +
                            "sum(discounttotal)-sum(total) as taxtotal," +
                            "sum(total) /sum(qty) as price,sum(discounttotal) /sum(qty) as discountprice " +
                            "from mvw_purchaseorder " +
                            sb.ToString() +
                            "group by employeeid,employeecode,employeename " +
                            sborder.ToString();

            string sqlCount = "with cte as (select employeeid,employeecode,employeename from mvw_purchaseorder "
                                + sb.ToString()
                                + " group by employeeid,employeecode,employeename) select count(*) from cte";

            string sqlSum = "select coalesce(sum(qty),0) as qty,coalesce(sum(total),0) as total," +
                            "coalesce(sum(discounttotal),0) as discounttotal," +
                            "coalesce(sum(discounttotal)-sum(total),0) as taxtotal " +
                            "from mvw_purchaseorder " +
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

        public Object InitStockReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit
            };
        }

        public Object PurchaseOrderStockReport(string parameters)
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
                else if (field == "vendorname")
                {
                    DataTable dt = db.QueryTable("select id from vendor where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and vendorid in ({0})", ids);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
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
                sborder.Append(" order by stockcode ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select stockid,stockcode,stockname," +
                            "sum(qty) as qty,sum(total) as total,sum(discounttotal) as discounttotal," +
                            "sum(discounttotal)-sum(total) as taxtotal," +
                            "sum(total) /sum(qty) as price,sum(discounttotal) /sum(qty) as discountprice " +
                            "from mvw_purchaseorder " +
                            sb.ToString() +
                            "group by stockid,stockcode,stockname " +
                            sborder.ToString();

            string sqlCount = "with cte as (select stockid,stockcode,stockname from mvw_purchaseorder "
                                + sb.ToString()
                                + " group by stockid,stockcode,stockname) select count(*) from cte";

            string sqlSum = "select coalesce(sum(qty),0) as qty,coalesce(sum(total),0) as total," +
                            "coalesce(sum(discounttotal),0) as discounttotal," +
                            "coalesce(sum(discounttotal)-sum(total),0) as taxtotal " +
                            "from mvw_purchaseorder " +
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

        public Object PurchaseOrderDetailReport(string parameters)
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
                            "from mvw_purchaseorder " +
                            sb.ToString() +
                            "group by id ";
            DataTable dtIds = db.QueryTable(sql);
            string filterids = dtIds.GetIds();
            sql = string.Format("select * from bill where id in ({0}) " + sborder, filterids);

            string sqlQtySum = "select coalesce(sum(qty),0) as qty " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'qty')::decimal) as qty from bill where id in (" + filterids + ")) as t";
            string sqlTotalSum = "select coalesce(sum(total),0) as total " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'discounttotal')::decimal) as total from bill where id in (" + filterids + ")) as t";

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
                item.content.Add("qty", item.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("qty")));
               
            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

    }
}
