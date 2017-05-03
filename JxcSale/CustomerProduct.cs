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

namespace JxcSale
{
    public class CustomerProduct : MarshalByRefObject
    {
        private string tablename = "customerproduct";

        public Object List(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(" where coalesce(customer.content->>'stop','')='' and coalesce(product.content->>'stop','')=''");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "customerid")
                {
                    sb.AppendFormat(" and (customerproduct.content->>'customerid')::int = {0}", f);
                }
                else if (field == "productid")
                {
                    sb.AppendFormat(" and (customerproduct.content->>'productid')::int = {0}", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            if (mysorting.Count > 0) sborder.Append(" order by ");
            int i = 0;
            foreach (string field in mysorting.Keys)
            {
                i++;
                if (field.ToLower() == "customer.code")
                {
                    sborder.AppendFormat(" customer.content->'customer'->>'code' {0} {1}", mysorting[field], i == mysorting.Count ? "" : ",");
                }
                else if (field.ToLower() == "customer.name")
                {
                    sborder.AppendFormat(" customer.content->'customer'->>'name' {0} {1}", mysorting[field], i == mysorting.Count ? "" : ",");
                }
                else if (field.ToLower() == "product.code")
                {
                    sborder.AppendFormat(" product.content->'product'->>'code' {0} {1}", mysorting[field], i == mysorting.Count ? "" : ",");
                }
                else if (field.ToLower() == "product.name")
                {
                    sborder.AppendFormat(" product.content->'product'->>'name' {0} {1}", mysorting[field], i == mysorting.Count ? "" : ",");
                }
                else
                {
                    sborder.AppendFormat(" customerproduct.content->>'{0}' {1} {2}", field.ToLower(), mysorting[field], i == mysorting.Count ? "" : ",");
                }
            }

            if (mysorting.Count == 0)
            {
                sborder.Append(" order by customerproduct.id desc ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);


            DBHelper db = new DBHelper();
            string sql = "select customerproduct.id,jsonb_set(jsonb_set(customerproduct.content,'{customer}',customer.content),'{product}',product.content) as content"
                        + " from customerproduct "
                        + " inner join customer on (customerproduct.content->>'customerid')::int=customer.id "
                        + " inner join product on (customerproduct.content->>'productid')::int=product.id "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt"
                        + " from customerproduct "
                        + " inner join customer on (customerproduct.content->>'customerid')::int=customer.id "
                        + " inner join product on (customerproduct.content->>'productid')::int=product.id "
                        + sb.ToString();

            int recordcount = db.Count(sqlCount);
            List<TableModel> list = db.Where(sql);

            return new { resulttotal = recordcount, data = list };
        }

        [MyLog("新增产品价格")]
        public Object AddSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int customerid = ph.GetParameterValue<Int32>("customerid");
            int productid = ph.GetParameterValue<Int32>("productid");
            decimal price = ph.GetParameterValue<Decimal>("price");

            using (DBHelper db = new DBHelper())
            {
                var model = db.FirstOrDefault("select * from customerproduct where (content->>'customerid')::int=" + customerid + " and (content->>'productid')::int=" + productid);
                if (model == null)
                {
                    db.Add("customerproduct", new TableModel()
                    {
                        content = JsonConvert.DeserializeObject<JObject>(parameters)
                    });
                }
                else
                {
                    model.content["price"] = price;
                    db.Edit("customerproduct", model);
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("批量导入产品价格")]
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
                    if (cover)
                    {
                        db.Truncate(tablename);
                    }

                    for (int i = 1; i < rowcount; i++)
                    {
                        #region 逐行导入
                        rowno = i + 1;

                        IRow row = sheet.GetRow(i);
                        string customercode = row.GetCell(0).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(customercode))
                            continue;
                        string productcode = row.GetCell(2).GetCellValue().ToString();
                        if (string.IsNullOrEmpty(customercode))
                            continue;

                        var customer = db.FirstOrDefault("select * from customer where content->>'code'='" + customercode + "'");
                        if (customer == null)
                        {
                            sb.AppendFormat("第{0}行出现错误：客户编号不存在！<br/>", rowno);
                            continue;
                        }
                        var product = db.FirstOrDefault("select * from product where content->>'code'='" + productcode + "'");
                        if (product == null)
                        {
                            sb.AppendFormat("第{0}行出现错误：产品编号不存在！<br/>", rowno);
                            continue;
                        }
                        if (row.GetCell(4).GetCellValue().ToString() == "")
                        {
                            sb.AppendFormat("第{0}行出现错误：没有填写价格！<br/>", rowno);
                            continue;
                        }

                        decimal price = Convert.ToDecimal(row.GetCell(4).GetCellValue());
                        JObject vp = new JObject();
                        vp.Add("customerid", customer.id);
                        vp.Add("productid", product.id);
                        vp.Add("price", price);

                        TableModel model = new TableModel()
                        {
                            content = vp
                        };

                        if (!cover)
                        {
                            var dbmodel = db.FirstOrDefault("select * from customerproduct where (content->>'customerid')::int=" + customer.id + " and (content->>'productid')::int=" + product.id);
                            if (dbmodel == null)
                            {
                                db.Add(tablename, model);
                            }
                            else
                            {
                                dbmodel.content = model.content;
                                db.Edit(tablename, dbmodel);
                            }
                        }
                        else
                        {
                            db.Add(tablename, model);

                        }


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
