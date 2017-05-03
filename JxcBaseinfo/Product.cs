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

namespace JxcBaseinfo
{
    public class Product : MarshalByRefObject
    {
        private string tablename = "product";

        public Object Categorys(string parameters)
        {
            return CategoryHelper.GetCategoryTreeData(tablename);
        }

        public Object List(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

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

                if (field.ToLower() == "state")
                {
                    sb.AppendFormat(" and coalesce(content->>'stop','')='{0}'", f == "normal" ? "" : "t");
                }
                else if (field.ToLower() == "categoryid")
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
            List<TableModel> list = db.Query(this.tablename, sb.ToString(), sborder.ToString(), out recordcount);
            foreach (var item in list)
            {
                if (item.content["categoryid"] == null) continue;
                var category = db.FirstOrDefault("select * from category where id=" + item.content.Value<int>("categoryid"));
                if (category == null) continue;
                item.content.Add("category", category.content);
            }

            return new { resulttotal = recordcount, data = list };
        }

        [MyLog("批量导入产品资料")]
        public Object ImportData(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            string path = dic["path"].ToString();
            path = ContextServiceHelper.MapPath(path);

            bool cover = Convert.ToBoolean(dic["cover"]);

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
                    List<string> codes = new List<string>();
                    for (int i = 1; i < rowcount; i++)
                    {
                        #region 检查编号重复
                        rowno = i + 1;

                        IRow row = sheet.GetRow(i);
                        string code = row.GetCell(0).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(code))
                            continue;

                        if (codes.Contains(code))
                        {
                            sb.AppendFormat("第{0}行出现错误：编号重复！<br/>", rowno);
                        }
                        else
                        {
                            codes.Add(code);
                        }
                        #endregion
                    }

                    if (sb.Length > 0)
                    {
                        return new { message = sb.ToString() };
                    }

                    if (cover)
                    {
                        db.Truncate(tablename);
                    }

                    for (int i = 1; i < rowcount; i++)
                    {
                        #region 逐行导入
                        rowno = i + 1;

                        IRow row = sheet.GetRow(i);
                        string code = row.GetCell(0).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(code))
                            continue;

                        //检查编号重复
                        if (!cover)
                        {
                            int cnt = db.Count("select count(id) as cnt from \"" + tablename + "\" where content->>'code'='" + code + "'");
                            if (cnt > 0)
                            {
                                sb.AppendFormat("第{0}行出现错误：编号重复！<br/>", rowno);
                                continue;
                            }
                        }

                        string name = row.GetCell(1).GetCellValue().ToString();
                        string categoryname = row.GetCell(2).GetCellValue().ToString();
                        string standard = row.GetCell(3).GetCellValue().ToString();
                        string type = row.GetCell(4).GetCellValue().ToString();
                        string unit = row.GetCell(5).GetCellValue().ToString();
                        string barcode = row.GetCell(6).GetCellValue().ToString();
                        string area = row.GetCell(7).GetCellValue().ToString();
                        string comment = row.GetCell(8).GetCellValue().ToString();
                        var category = db.FirstOrDefault("select * from category where content->>'name'='" + categoryname
                            + "' and content->>'classname'='" + tablename + "'");
                        Dictionary<string, object> product = new Dictionary<string, object>();
                        product.Add("code", code);
                        product.Add("name", name);
                        if (category != null)
                        {
                            product.Add("categoryid", category.id);
                        }
                        product.Add("standard", standard);
                        product.Add("type", type);
                        product.Add("unit", unit);
                        product.Add("barcode", barcode);
                        product.Add("area", area);
                        product.Add("comment", comment);
                        product.Add("pycode", PyConverter.IndexCode(name));

                        TableModel model = new TableModel()
                        {
                            content = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(product))
                        };
                        db.Add(tablename, model);

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

        public Object GetCode(string parameters)
        {
            DBHelper db = new DBHelper();

            string code = string.Empty;
            var model = db.FirstOrDefault("select * from \"" + tablename + "\" order by content->>'code' desc");
            if (model != null)
            {
                code = MyHelper.TryParseCode(model.content.Value<string>("code"));
            }

            return new { code = code };

        }

        public Object Edit(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper();

            var model = db.First("select * from \"" + tablename + "\" where id=" + id);
            if (model.content.Value<int>("vendorid") > 0)
            {
                var vendor = db.First("vendor", model.content.Value<int>("vendorid"));
                model.content.Add("vendor", vendor.content.Value<string>("name"));
            }

            var category = db.FirstOrDefault("select * from category where id="
                + (model.content["categoryid"] == null ? 0 : model.content.Value<int>("categoryid")));

            if (category == null)
            {
                return new { data = model };
            }

            return new { data = model, category = category.content };

        }

        [MyLog("新增产品资料")]
        public Object AddSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from \"" + tablename + "\" where content->>'code'='" + model.content.Value<string>("code") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }
                if (!string.IsNullOrEmpty(model.content.Value<string>("barcode")))
                {
                    cnt = db.Count("select count(*) as cnt from \"" + tablename + "\" where content->>'barcode'='" + model.content.Value<string>("barcode") + "' and coalesce(content->>'stop','')=''");
                    if (cnt > 0)
                    {
                        return new { message = StringHelper.GetString("条码有重复") };
                    }
                }

                model.content["pycode"] = PyConverter.IndexCode(model.content.Value<string>("name"));
                db.Add(this.tablename, model);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("编辑产品资料")]
        public Object EditSave(string parameters)
        {
            TableModel model = JsonConvert.DeserializeObject<TableModel>(parameters);
            using (DBHelper db = new DBHelper())
            {
                //检查编号重复
                int cnt = db.Count("select count(*) as cnt from \"" + tablename + "\" where id<>" + model.id + " and content->>'code'='" + model.content.Value<string>("code") + "'");
                if (cnt > 0)
                {
                    return new { message = StringHelper.GetString("编号有重复") };
                }
                if (!string.IsNullOrEmpty(model.content.Value<string>("barcode")))
                {
                    cnt = db.Count("select count(*) as cnt from \"" + tablename + "\" where id<>" + model.id + " and content->>'barcode'='" + model.content.Value<string>("barcode") + "' and coalesce(content->>'stop','')=''");
                    if (cnt > 0)
                    {
                        return new { message = StringHelper.GetString("条码有重复") };
                    }
                }
                model.content["pycode"] = PyConverter.IndexCode(model.content.Value<string>("name"));
                db.Edit(this.tablename, model);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("删除产品资料")]
        public Object Delete(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);
             
            using (DBHelper db = new DBHelper())
            {
                var model = db.First(this.tablename, id);
                if (model.content["storage"] != null 
                    && model.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty")!=decimal.Zero)
                {
                    return new { message = "库存不为零，不能删除！" };
                }
                var detail = db.FirstOrDefault("select * from bill from bill "
                    + "where exists(select 1 from jsonb_array_elements(content->'details')  t(x) where (x->>'productid')::int=" + id + ") ");
                if (detail != null)
                {
                    return new { message = "该产品有关联的业务单据，不能删除！" };
                }

                db.Remove(this.tablename, id);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("停用产品")]
        public Object Stop(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);
 
            using (DBHelper db = new DBHelper())
            {
                var model = db.First(this.tablename, id);
                if (model.content["storage"] != null
                    && model.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty") != decimal.Zero)
                {
                    return new { message = "库存不为零，不能停用！" };
                }
                 
                model.content["stop"] = "t";
                db.Edit(this.tablename, model);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("启用产品")]
        public Object UnStop(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from \"" + tablename + "\" where id=" + id);
                model.content.Remove("stop");
                db.Edit(this.tablename, model);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }
    }
}
