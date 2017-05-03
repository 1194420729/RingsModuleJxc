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

namespace JxcStorage
{
    public class StockInventoryBill : MarshalByRefObject
    {
        private string billname = "stockinventorybill";
        public Object Init(string parameters)
        {
            string billcode = BillHelper.GetBillCode(billname);
            JObject content = new JObject();
            content.Add("billcode", billcode);
            content.Add("billname", billname);
            content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            content.Add("inventorydate", DateTime.Now.ToString("yyyy-MM-dd"));

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
                billconfig.content.Add("showstandard", false);
                billconfig.content.Add("showtype", false);
                billconfig.content.Add("showunit", false);
                billconfig.content.Add("showarea", false);
                billconfig.content.Add("showbarcode", false);
                billconfig.content.Add("keepemployeeandstock", true);
            }

            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
            bool noeditbillcode = option.content["noeditbillcode"] == null ? false : option.content.Value<bool>("noeditbillcode");
            bool noeditbilldate = option.content["noeditbilldate"] == null ? false : option.content.Value<bool>("noeditbilldate");
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                content = content,
                makername = PluginContext.Current.Account.Name,
                billconfig = billconfig,
                digit = digit,
                noeditbillcode = noeditbillcode,
                noeditbilldate = noeditbilldate,
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
            var employee = db.First("employee", bill.content.Value<int>("employeeid"));
            var stock = db.First("stock", bill.content.Value<int>("stockid"));
            var productcategory = db.First("category",bill.content.Value<int>("categoryid"));

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
                billconfig.content.Add("showstandard", false);
                billconfig.content.Add("showtype", false);
                billconfig.content.Add("showunit", false);
                billconfig.content.Add("showarea", false);
                billconfig.content.Add("showbarcode", false);
                billconfig.content.Add("keepemployeeandstock", true);
            }

            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
            bool noeditbillcode = option.content["noeditbillcode"] == null ? false : option.content.Value<bool>("noeditbillcode");
            bool noeditbilldate = option.content["noeditbilldate"] == null ? false : option.content.Value<bool>("noeditbilldate");
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                content = bill.content,
                makername = maker.content.Value<string>("name"),
                categoryname=productcategory.content.Value<string>("name"),
                billconfig = billconfig,
                digit = digit,
                employeename = employee.content.Value<string>("name"),
                stockname = stock.content.Value<string>("name"),
                noeditbillcode = noeditbillcode,
                noeditbilldate = noeditbilldate,
                showcost = showcost
            };
        }

        [MyLog("配置盘点单格式")]
        public Object BillConfigSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string bf = ph.GetParameterValue<string>("billconfig");

            TableModel billconfig = JsonConvert.DeserializeObject<TableModel>(bf);
            billconfig.SaveBillConfig(false);

            return new { message = "ok", id = billconfig.id };

        }

        [MyLog("Excel导入")]
        public Object ImportData(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string path = ph.GetParameterValue<string>("path");
            int stockid = ph.GetParameterValue<int>("stockid");
            int categoryid = ph.GetParameterValue<int>("categoryid");
            DateTime inventorydate = ph.GetParameterValue<DateTime>("inventorydate");

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

                    //检查产品类别
                    if (categoryid > 0)
                    {
                        var ids = CategoryHelper.GetChildrenIds(categoryid);
                        ids.Add(categoryid);
                        if (ids.Contains(product.content.Value<int>("categoryid")) == false)
                        { 
                            continue;
                        }
                    }

                    if (product == null)
                    {
                        sb.AppendFormat("第{0}行出现错误：产品编号不存在！<br/>", rowno);
                        continue;
                    }

                    decimal qty = Convert.ToDecimal(row.GetCell(2).GetCellValue());
                    string comment = row.GetCell(3).GetCellValue().ToString();
                    JObject detail = new JObject();
                    detail.Add("productid", product.id);
                    detail.Add("product", product.content);
                    detail.Add("qty", qty);
                    if (inventorydate.Date.CompareTo(DateTime.Now.Date) < 0)
                    {
                        decimal storageqty=GetHistoryStorage(product.id,stockid,inventorydate);
                        detail.Add("storageqty", storageqty);
                        detail.Add("ykqty", qty - storageqty);
                    }
                    else
                    {
                        decimal storageqty = decimal.Zero;
                        if (product.content["storage"] != null && product.content.Value<JObject>("storage")[stockid.ToString()] != null)
                            storageqty=product.content.Value<JObject>("storage").Value<JObject>(stockid.ToString()).Value<decimal>("qty");
                        detail.Add("storageqty", storageqty);
                        detail.Add("ykqty", qty-storageqty);
                    }
                    
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

        [MyLog("盘点单编号设置")]
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

        [MyLog("新增盘点单")]
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

                db.Add("bill", model);
                db.SaveChanges();

                bool nodraftprint = options.content["nodraftprint"] != null && options.content.Value<bool>("nodraftprint");

                return new { message = "ok", id = model.id, nodraftprint = nodraftprint };
            }
        }

        [MyLog("保存并审核盘点单")]
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

                string result = model.Audit(db);
                if (!string.IsNullOrEmpty(result))
                    return new { message = "needcost", productids = result };
                 

                db.SaveChanges();
            }

            return new { message = "ok", id = model.id };
        }

        [MyLog("编辑盘点单")]
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

                db.Edit("bill", model);
                db.SaveChanges();

                bool nodraftprint = options.content["nodraftprint"] != null && options.content.Value<bool>("nodraftprint");

                return new { message = "ok", id = model.id, nodraftprint = nodraftprint };
            }
        }

        [MyLog("编辑并审核盘点单")]
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

                string result = model.Audit(db);
                if (!string.IsNullOrEmpty(result))
                    return new { message = "needcost", productids = result };
                 

                db.SaveChanges();
            }
            return new { message = "ok", id = model.id };

        }

        public Object RegisterPrintModel(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int billid = ph.GetParameterValue<int>("billid");

            DBHelper db = new DBHelper();
            var bill = db.First("bill", billid);

            PrintData pd = new PrintData();
            pd.HeaderField = new List<string>() 
            {
                "单据编号"
            };

            pd.HeaderValue = new Dictionary<string, string>()
            {
                {"单据编号",bill.content.Value<string>("billcode")}

            };

            pd.DetailField = new List<string>()
            {
                "产品编号"
            };

            pd.DetailValue = new List<Dictionary<string, string>>();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                var product = db.First("product", item.Value<int>("productid"));
                Dictionary<string, string> detail = new Dictionary<string, string>();
                detail.Add("产品编号", product.content.Value<string>("code"));
                pd.DetailValue.Add(detail);

            }

            PrintManager pm = new PrintManager(PluginContext.Current.Account.ApplicationId);
            int modelid = pm.RegisterPrintModel(pd);

            return new { modelid = modelid };
        }

        public Object GetProductsByCategoryId(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int categoryid = ph.GetParameterValue<int>("categoryid");
            int stockid = ph.GetParameterValue<int>("stockid");
            DateTime inventorydate = ph.GetParameterValue<DateTime>("inventorydate");

            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'stop','')=''");
            if (categoryid > 0)
            {
                var ids = CategoryHelper.GetChildrenIds(categoryid);
                ids.Add(categoryid);
                var idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'code' ");

            string sql = "select * from product " + sb.ToString() + sborder.ToString();
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Where(sql);

            if (inventorydate.Date.CompareTo(DateTime.Now.Date) < 0)
            {
                //获取历史库存
                foreach (var item in list)
                {
                    decimal qty = GetHistoryStorage(item.id, stockid, inventorydate);
                    JObject storage = new JObject();
                    storage.Add("0", new JObject() { { "qty", qty } });
                    storage.Add(stockid.ToString(), new JObject() { { "qty", qty } });
                    item.content["storage"] = storage;
                }
            }

            return new { resulttotal = list.Count, data = list };
        }

        public Object GetHistoryStorageByProductId(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int productid = ph.GetParameterValue<int>("productid");
            int stockid = ph.GetParameterValue<int>("stockid");
            DateTime enddate = ph.GetParameterValue<DateTime>("enddate");

            DBHelper db = new DBHelper();
            var product = db.First("product", productid);

            decimal qty = GetHistoryStorage(product.id, stockid, enddate);
            JObject storage = new JObject();
            storage.Add("0", new JObject() { { "qty", qty } });
            storage.Add(stockid.ToString(), new JObject() { { "qty", qty } });

            return new { data = storage };
        }

        public Object GetHistoryStorageByProductIds(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string productids = ph.GetParameterValue<string>("productids");
            int stockid = ph.GetParameterValue<int>("stockid");
            DateTime enddate = ph.GetParameterValue<DateTime>("enddate");
            string[] ss = productids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            DBHelper db = new DBHelper();

            JObject storages = new JObject();
            foreach (string productid in ss)
            {
                var product = db.First("product", Convert.ToInt32(productid));

                decimal qty = GetHistoryStorage(product.id, stockid, enddate);
                JObject storage = new JObject();
                storage.Add("0", new JObject() { { "qty", qty } });
                storage.Add(stockid.ToString(), new JObject() { { "qty", qty } });

                storages.Add(productid, storage);
            }

            return new { data = storages };
        }

        private decimal GetHistoryStorage(int produtid, int stockid, DateTime enddate)
        {
            DBHelper db = new DBHelper();

            string sql = "select coalesce(sum((content->>'qty')::decimal),0) as qty " +
                        "from storagedetail where (content->>'productid')::int=" + produtid +
                            " and (content->>'stockid')::int=" + stockid +
                            " and content->>'billdate'<='" + enddate.ToString("yyyy-MM-dd") + "'";
            return Convert.ToDecimal(db.Scalar(sql));
        }

        public Object GetLegalCategoryIds(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int categoryid = ph.GetParameterValue<int>("categoryid");

            if (categoryid > 0)
            {
                var ids = CategoryHelper.GetChildrenIds(categoryid);
                ids.Add(categoryid);

                return new { data = ids };
            }
            else
            {
                return new { data = new List<int>() };
            }
        }

        public Object LegalProductCategorys(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int categoryid = ph.GetParameterValue<int>("categoryid");

            if (categoryid == 0)
            {
                return CategoryHelper.GetCategoryTreeData("product");
            }
            else
            {
                return CategoryHelper.GetPartCategoryTreeData("product", categoryid);
            }
        }


    }
}
