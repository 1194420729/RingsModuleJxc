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

namespace JxcInit
{
    public class InitStorage : MarshalByRefObject
    {
        public Object List(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int stockid = Convert.ToInt32(dic["stockid"]);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field.ToLower() == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        var idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
                    }
                }
                else
                {
                    sb.AppendFormat(" and content->>'{0}' ilike '%{1}%'", field.ToLower(), f);
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
                sborder.Append(" order by content->>'code' ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query("product", sb.ToString(), sborder.ToString(), out recordcount);
            foreach (var item in list)
            {
                if (item.content["categoryid"] == null) continue;
                var category = db.FirstOrDefault("select * from category where id=" + item.content.Value<int>("categoryid"));
                if (category == null) continue;
                item.content.Add("category", category.content);
            }

            decimal qtysum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stockid + "'->>'qty','0')::decimal),0) as qty from product where 1=1 " + sb.ToString()));
            decimal totalsum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stockid + "'->>'total','0')::decimal),0) as total from product where 1=1 " + sb.ToString()));

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            var options = db.First("select * from option");
            bool initover = options.content["initoverdate"] != null;

            return new { resulttotal = recordcount, data = list, qtysum = qtysum, totalsum = totalsum, showcost = showcost, initover = initover };
        }

        public Object DistList(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int stockid = 0;

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field.ToLower() == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        var idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
                    }
                }
                else
                {
                    sb.AppendFormat(" and content->>'{0}' ilike '%{1}%'", field.ToLower(), f);
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
                sborder.Append(" order by content->>'code' ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query("product", sb.ToString(), sborder.ToString(), out recordcount);
            foreach (var item in list)
            {
                if (item.content["categoryid"] == null) continue;
                var category = db.FirstOrDefault("select * from category where id=" + item.content.Value<int>("categoryid"));
                if (category == null) continue;
                item.content.Add("category", category.content);
            }

            decimal qtysum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stockid + "'->>'qty','0')::decimal),0) as qty from product where 1=1 " + sb.ToString()));
            decimal totalsum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stockid + "'->>'total','0')::decimal),0) as total from product where 1=1 " + sb.ToString()));

            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            Dictionary<int, decimal> qtysums = new Dictionary<int, decimal>();
            Dictionary<int, decimal> totalsums = new Dictionary<int, decimal>();
            foreach (var stock in stocks)
            {
                decimal stockqty = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stock.id + "'->>'qty','0')::decimal),0) as qty from product where 1=1 " + sb.ToString()));
                qtysums.Add(stock.id, stockqty);

                decimal stocktotal = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'initstorage'->'" + stock.id + "'->>'total','0')::decimal),0) as total from product where 1=1 " + sb.ToString()));
                totalsums.Add(stock.id, stocktotal);
            }

            bool initstorageedit = PluginContext.Current.Account.IsAllowed("initstorageedit");

            var options = db.First("select * from option");
            bool initover = options.content["initoverdate"] != null;

            return new
            {
                resulttotal = recordcount,
                data = list,
                qtysum = qtysum,
                totalsum = totalsum,
                stocks = stocks,
                showcost = showcost,
                qtysums = qtysums,
                totalsums = totalsums,
                initstorageedit = initstorageedit,
                initover=initover
            };
        }

        public Object ProductCategorys(string parameters)
        {
            return CategoryHelper.GetCategoryTreeData("product");
        }

        public Object Stocks(string parameters)
        {
            DBHelper db = new DBHelper();

            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");
            stocks.Insert(0, new TableModel()
            {
                id = 0,
                content = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(new { name = StringHelper.GetString("全部仓库") }))
            });

            return new { data = stocks };
        }

        public Object StocksTree(string parameters)
        {
            DBHelper db = new DBHelper();
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')=''  order by content->>'code'");
            List<TreeData> list = new List<TreeData>();
            list.Add(new TreeData()
            {
                icon = "fa fa-folder-o fa-fw",
                id = "0",
                parent = "#",
                text = StringHelper.GetString("全部仓库")
            });
            foreach (var item in stocks)
            {
                TreeData td = new TreeData()
                {
                    id = item.id.ToString(),
                    parent = "0",
                    text = item.content.Value<string>("name"),
                    icon = "fa fa-folder-o fa-fw"
                };

                list.Add(td);
            }

            return list;
        }

        [MyLog("修改期初库存")]
        public Object EditSave(string parameters)
        {
            using (DBHelper db = new DBHelper())
            {
                var options = db.First("select * from option");
                if (options.content["initoverdate"] != null)
                {
                    return new { message = StringHelper.GetString("系统已经开账，不能再修改期初数据！") };
                }
                
                var product = JsonConvert.DeserializeObject<TableModel>(parameters);
                if (product.content["initstorage"] != null)
                {
                    var items = JsonConvert.DeserializeObject<Dictionary<string, InitStorageModel>>(JsonConvert.SerializeObject(product.content["initstorage"]));
                    Dictionary<string, InitStorageModel> list = new Dictionary<string, InitStorageModel>();
                    foreach (string key in items.Keys)
                    {
                        if (key != "0" && items[key].qty.HasValue)
                        {
                            items[key].price = items[key].price ?? decimal.Zero;
                            items[key].total = items[key].total ?? decimal.Zero;
                            list.Add(key, items[key]);
                        }
                    }

                    InitStorageModel stock0 = new InitStorageModel()
                    {
                        qty = list.Values.Sum(c => c.qty.Value),
                        total = list.Values.Sum(c => c.total.Value)
                    };
                    stock0.price = stock0.qty.Value == decimal.Zero ? decimal.Zero : Math.Round(stock0.total.Value / stock0.qty.Value, 4);
                    list.Add("0", stock0);

                    product.content["initstorage"] = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(list));

                }

                var p = db.First("select * from product where id=" + product.id);
                p.content["initstorage"] = product.content["initstorage"];
                db.Edit("product", p);
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object PrepareExcel(string parameters)
        {
            DBHelper db = new DBHelper();

            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");
            var products = db.Where("select * from product where coalesce(content->>'stop','')='' order by content->>'code'");

            DataTable dt = new DataTable();
            dt.Columns.Add("产品编号", typeof(string));
            dt.Columns.Add("产品名称", typeof(string));
            dt.Columns.Add("产品规格", typeof(string));
            dt.Columns.Add("产品型号", typeof(string));
            dt.Columns.Add("计量单位", typeof(string));
            foreach (var stock in stocks)
            {
                dt.Columns.Add(stock.content.Value<string>("name") + "数量", typeof(decimal));
                dt.Columns.Add(stock.content.Value<string>("name") + "成本均价", typeof(decimal));
            }

            foreach (var product in products)
            {
                DataRow row = dt.NewRow();
                row["产品编号"] = product.content.Value<string>("code");
                row["产品名称"] = product.content.Value<string>("name");
                row["产品规格"] = product.content.Value<string>("standard");
                row["产品型号"] = product.content.Value<string>("type");
                row["计量单位"] = product.content.Value<string>("unit");
                foreach (var stock in stocks)
                {
                    if (product.content["initstorage"] == null) continue;
                    if (product.content.Value<JObject>("initstorage")[stock.id.ToString()] == null) continue;
                    row[stock.content.Value<string>("name") + "数量"] = product.content.Value<JObject>("initstorage").Value<JObject>(stock.id.ToString()).Value<decimal>("qty");
                    row[stock.content.Value<string>("name") + "成本均价"] = product.content.Value<JObject>("initstorage").Value<JObject>(stock.id.ToString()).Value<decimal>("price");
                }

                dt.Rows.Add(row);
            }

            byte[] bytes = dt.GetExcelStream();

            string date = DateTime.Now.ToString("yyyyMMdd");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temporary", PluginContext.Current.Account.ApplicationId, date); 
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            string filename = Guid.NewGuid().ToString("N") + ".xls";
            string filepath = Path.Combine(path, filename);
            FileStream fs = File.Create(filepath);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();

            return new { url = "/upload/temporary?filename=" + filename + "&appid=" + PluginContext.Current.Account.ApplicationId + "&date=" + date };
        }

        [MyLog("批量导入期初库存")]
        public Object ImportData(string parameters)
        {
            using (DBHelper db = new DBHelper())
            {
                var options = db.First("select * from option");
                if (options.content["initoverdate"] != null)
                {
                    return new { message = StringHelper.GetString("系统已经开账，不能再修改期初数据！") };
                }
            }

            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            string path = dic["path"].ToString();
             
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
                using (DBHelper db = new DBHelper())
                {
                    var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

                    for (int i = 1; i < rowcount; i++)
                    {
                        #region 逐行导入
                        rowno = i + 1;

                        IRow row = sheet.GetRow(i);
                        string code = row.GetCell(0).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(code))
                            continue;

                        int col = 5;
                        var product = db.First("select * from product where content->>'code'='" + code + "'");
                        Dictionary<string, InitStorageModel> initstorage = new Dictionary<string, InitStorageModel>();
                        InitStorageModel storage0 = new InitStorageModel() { qty = decimal.Zero, total = decimal.Zero };
                        foreach (var stock in stocks)
                        {
                            decimal qty = row.GetCell(col).GetCellValue().ToString() == "" ? decimal.Zero : Convert.ToDecimal(row.GetCell(col).GetCellValue());
                            decimal price = row.GetCell(col + 1).GetCellValue().ToString() == "" ? decimal.Zero : Convert.ToDecimal(row.GetCell(col + 1).GetCellValue());
                            decimal total = qty * price;
                            if (qty != decimal.Zero)
                            {
                                initstorage.Add(stock.id.ToString(), new InitStorageModel()
                                {
                                    price = price,
                                    qty = qty,
                                    total = total
                                });
                            }
                            storage0.qty += qty;
                            storage0.total += total;
                            col += 2;
                        }

                        storage0.price = storage0.qty.Value == decimal.Zero ? decimal.Zero : Math.Round(storage0.total.Value / storage0.qty.Value, 4);
                        initstorage.Add("0", storage0);
                        product.content["initstorage"] = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(initstorage));
                        db.Edit("product", product);
                        #endregion

                    }

                    if (sb.Length > 0)
                    {
                        db.Discard();
                        return new { message = sb.ToString() };
                    }

                    db.SaveChanges();
                }
                return new { message = "ok" };
            }
            catch (Exception ex)
            {
                //Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return new { message = "导入出错(" + rowno + ")" + ex.Message };
            }

        }


    }

    public class InitStorageModel
    {
        public decimal? qty { get; set; }
        public decimal? price { get; set; }
        public decimal? total { get; set; }
    }

}
