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
    public class InitBank : MarshalByRefObject
    {
        private string tablename = "bank";

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
            List<TableModel> list = db.Query(this.tablename, sb.ToString(), sborder.ToString(), out recordcount);

            decimal totalsum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->>'inittotal','0')::decimal),0) as total from bank where 1=1 " + sb.ToString()));

            var options = db.First("select * from option");
            bool initover = options.content["initoverdate"] != null;

            return new { resulttotal = recordcount, data = list, totalsum = totalsum, initover = initover };
        }

        [MyLog("修改期初现金银行")]
        public Object EditSave(string parameters)
        {
            var bank = JsonConvert.DeserializeObject<TableModel>(parameters);

            using (DBHelper db = new DBHelper())
            {
                var options = db.First("select * from option");
                if (options.content["initoverdate"] != null)
                {
                    return new { message = StringHelper.GetString("系统已经开账，不能再修改期初数据！") };
                }

                var p = db.First("select * from bank where id=" + bank.id);
                p.content["inittotal"] = bank.content["inittotal"];
                db.Edit(tablename, p);
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object PrepareExcel(string parameters)
        {
            DBHelper db = new DBHelper();

            var banks = db.Where("select * from bank where coalesce(content->>'stop','')='' order by content->>'code'");

            DataTable dt = new DataTable();
            dt.Columns.Add("账户编号", typeof(string));
            dt.Columns.Add("账户名称", typeof(string));
            dt.Columns.Add("期初余额", typeof(decimal));


            foreach (var bank in banks)
            {
                DataRow row = dt.NewRow();
                row["账户编号"] = bank.content.Value<string>("code");
                row["账户名称"] = bank.content.Value<string>("code");
                if (bank.content["inittotal"] != null)
                {
                    row["期初余额"] = bank.content.Value<decimal>("inittotal");
                }

                dt.Rows.Add(row);
            }

            byte[] bytes = dt.GetExcelStream();

            string date = DateTime.Now.ToString("yyyyMMdd");
            string path = Path.Combine(ContextServiceHelper.MapPath("~/temporary"), PluginContext.Current.Account.ApplicationId, date);
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            string filename = Guid.NewGuid().ToString("N") + ".xls";
            string filepath = Path.Combine(path, filename);
            FileStream fs = File.Create(filepath);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();

            return new { url = "/temporary/" + PluginContext.Current.Account.ApplicationId + "/" + date + "/" + filename };
        }

        [MyLog("批量导入期初现金银行")]
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

                using (DBHelper db = new DBHelper())
                {
                    for (int i = 1; i < rowcount; i++)
                    {
                        #region 逐行导入
                        rowno = i + 1;

                        IRow row = sheet.GetRow(i);
                        string code = row.GetCell(0).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(code))
                            continue;

                        var bank = db.First("select * from bank where content->>'code'='" + code + "'");
                        if (row.GetCell(2).GetCellValue().ToString() != "")
                        {
                            bank.content["inittotal"] = Convert.ToDecimal(row.GetCell(2).GetCellValue());
                        }
                        else
                        {
                            bank.content.Remove("inittotal");
                        }

                        db.Edit(tablename, bank);
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


}
