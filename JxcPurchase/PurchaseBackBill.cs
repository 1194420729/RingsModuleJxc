﻿using Newtonsoft.Json;
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
    public class PurchaseBackBill : MarshalByRefObject
    {
        private string billname = "purchasebackbill";
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
                billconfig.content.Add("showstorage", false);
                billconfig.content.Add("showarea", false);
                billconfig.content.Add("showbarcode", false);
                billconfig.content.Add("keepemployeeandstock", true);
            }

            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");
            bool noeditbillcode = option.content["noeditbillcode"] == null ? false : option.content.Value<bool>("noeditbillcode");
            bool noeditbilldate = option.content["noeditbilldate"] == null ? false : option.content.Value<bool>("noeditbilldate");
            bool strictordermanage = option.content.Value<bool>("strictordermanage");
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                content = content,
                makername = PluginContext.Current.Account.Name,
                billconfig = billconfig,
                digit = digit,
                noeditbillcode = noeditbillcode,
                noeditbilldate = noeditbilldate,
                strictordermanage = strictordermanage,
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

            string sqlCount = "select count(0) as cnt from bill "
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
            bool strictordermanage = option.content.Value<bool>("strictordermanage");
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

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
                noeditbilldate = noeditbilldate,
                strictordermanage = strictordermanage,
                showcost = showcost
            };
        }

        [MyLog("配置采购退货单格式")]
        public Object BillConfigSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string bf = ph.GetParameterValue<string>("billconfig");
            bool applyall = ph.GetParameterValue<bool>("applyall");

            TableModel billconfig = JsonConvert.DeserializeObject<TableModel>(bf);
            billconfig.SaveBillConfig(applyall);             

            return new { message = "ok", id = billconfig.id };

        }

        [MyLog("Excel导入")]
        public Object ImportData(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string path = ph.GetParameterValue<string>("path");
            decimal taxrate = ph.GetParameterValue<decimal>("taxrate");
            
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

        [MyLog("采购退货单编号设置")]
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

        [MyLog("新增采购退货单")]
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
                int cnt = db.Count("select count(0) as cnt from bill where content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
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
                model.content["auditstatus"] = 0;
                model.content["createtime"] = DateTime.Now;
                model.content["total"] = model.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("discounttotal"));

                db.Add("bill", model);
                db.SaveChanges();

                bool nodraftprint = options.content["nodraftprint"] != null && options.content.Value<bool>("nodraftprint");

                return new { message = "ok", id = model.id, nodraftprint = nodraftprint };
            }
        }

        [MyLog("保存并审核采购退货单")]
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
                int cnt = db.Count("select count(0) as cnt from bill where content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
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
                 
                string result=model.Audit(db);
                if(!string.IsNullOrEmpty(result))
                    return new { message = result};

                db.SaveChanges();
            }
            return new { message = "ok", id = model.id };

        }

        [MyLog("编辑采购退货单")]
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
                int cnt = db.Count("select count(0) as cnt from bill where id<>" + model.id + " and content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
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

        [MyLog("编辑并审核采购退货单")]
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
                int cnt = db.Count("select count(0) as cnt from bill where id<>" + model.id + " and content->>'billname'='" + billname + "' and content->>'billcode'='" + model.content.Value<string>("billcode") + "'");
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

                string result = model.Audit(db);
                if (!string.IsNullOrEmpty(result))
                    return new { message = result };

                db.SaveChanges();
            }
            return new { message = "ok", id = model.id };

        }


        public Object RegisterPrintModel(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int billid = ph.GetParameterValue<int>("billid");

            DBHelper db = new DBHelper();

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

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
                "公司名称",
                "单据编号",
                "单据日期",
                "经手人",
                "制单人",
                "仓库名称",
                "供应商名称",
                "供应商联系人",
                "供应商电话",
                "供应商地址", 
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
                {"公司名称",PluginContext.Current.Account.CompanyName},
                {"单据编号",bill.content.Value<string>("billcode")},
                {"单据日期",bill.content.Value<string>("billdate")},
                {"经手人",employee.content.Value<string>("name")},
                {"制单人",maker.content.Value<string>("name")},
                {"仓库名称",stock.content.Value<string>("name")},
                {"供应商名称",vendor.content.Value<string>("name")},
                {"供应商联系人",vendor.content.Value<string>("linkman")},
                {"供应商电话",vendor.content.Value<string>("linkmobile")},
                {"供应商地址",vendor.content.Value<string>("address")}, 
                {"备注",bill.content.Value<string>("comment")},
                {"系统日期",DateTime.Now.ToString("yyyy-MM-dd")},
                {"系统时间",DateTime.Now.ToString("yyyy-MM-dd HH:mm")},
                {"未税金额",showcost?total.ToString("n2"):"****"},
                {"未税金额大写",showcost?MoneyHelper.ConvertSum(total):"****"},
                {"含税金额",showcost?taxtotal.ToString("n2"):"****"},
                {"含税金额大写",showcost?MoneyHelper.ConvertSum(taxtotal):"****"},
                {"折后金额",showcost?discounttotal.ToString("n2"):"****"},
                {"折后金额大写",showcost?MoneyHelper.ConvertSum(discounttotal):"****"},
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
                detail.Add("单价", showcost ? item.Value<decimal>("price").ToString("n2") : "****");
                detail.Add("金额", showcost ? item.Value<decimal>("total").ToString("n2") : "****");
                detail.Add("含税单价", showcost ? item.Value<decimal>("taxprice").ToString("n2") : "****");
                detail.Add("含税金额", showcost ? item.Value<decimal>("taxtotal").ToString("n2") : "****");
                detail.Add("税率", item.Value<decimal>("taxrate").ToString());
                detail.Add("折后单价", showcost ? item.Value<decimal>("discountprice").ToString("n2") : "****");
                detail.Add("折后金额", showcost ? item.Value<decimal>("discounttotal").ToString("n2") : "****");
                detail.Add("折扣率", item.Value<decimal>("discountrate").ToString());
                detail.Add("备注", item.Value<string>("comment"));
                pd.DetailValue.Add(detail);

            }

            PrintManager pm = new PrintManager(PluginContext.Current.Account.ApplicationId);
            int modelid = pm.RegisterPrintModel(pd);

            return new { modelid = modelid };
        }

    }
}
