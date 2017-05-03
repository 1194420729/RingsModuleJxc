using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rings.Models;
using Jxc.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace JxcStorage
{
    public class Common : MarshalByRefObject
    { 
        public Object ProductChoice(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string filter = ph.GetParameterValue<string>("filter");

            DBHelper db = new DBHelper();
            var list = db.Where("select * from product where coalesce(content->>'stop','')='' and (content->>'name' ilike '%" + filter + "%' or content->>'code' ilike '%" + filter + "%' or content->>'pycode' ilike '%" + filter + "%')");

            return new { data = list };
        }

        public Object StockChoice(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string filter = ph.GetParameterValue<string>("filter");

            DBHelper db = new DBHelper();
            var list = db.Where("select * from stock where coalesce(content->>'stop','')='' and (content->>'name' ilike '%" + filter + "%' or content->>'code' ilike '%" + filter + "%' or content->>'pycode' ilike '%" + filter + "%')");

            return new { data = list };
        }

        public Object StockList(string parameters)
        {

            DBHelper db = new DBHelper();
            var list = db.Where("select * from stock where coalesce(content->>'stop','')=''  order by content->>'code'");

            return new { data = list };
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


        public Object ProductCategorys(string parameters)
        {
            return CategoryHelper.GetCategoryTreeData("product");
        }

        public Object ProductList(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();
            sb.Append(" and coalesce(content->>'stop','')=''");

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
            sborder.Append(" order by content->>'code' ");
            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query("product", sb.ToString(), sborder.ToString(), out recordcount);

            return new { resulttotal = recordcount, data = list };
        }

        public Object EmployeeChoice(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string filter = ph.GetParameterValue<string>("filter");

            DBHelper db = new DBHelper();
            var list = db.Where("select * from employee where id>1 and coalesce(content->>'stop','')='' and (content->>'name' ilike '%" + filter + "%' or content->>'code' ilike '%" + filter + "%' or content->>'pycode' ilike '%" + filter + "%')");

            return new { data = list };
        }

        public Object EmployeeList(string parameters)
        {

            DBHelper db = new DBHelper();
            var list = db.Where("select * from employee where id>1 and coalesce(content->>'stop','')=''  order by content->>'code'");

            return new { data = list };
        }
         
        public Object GetBillCodeTemplate(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string billname = ph.GetParameterValue<string>("billname");

            return new { template = BillHelper.GetBillCodeTemplate(billname) };
        }

        public Object GetEmployeeAndStock(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int employeeid = ph.GetParameterValue<int>("employeeid");
            int stockid = ph.GetParameterValue<int>("stockid");

            DBHelper db = new DBHelper();
            var employee = db.First("employee", employeeid);
            var stock = db.First("stock", stockid);

            return new { employee = employee, stock = stock };
        }

        public Object GetProductsById(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string productids = ph.GetParameterValue<string>("productids");
            if (productids.EndsWith(","))
            {
                productids = productids.Substring(0, productids.Length - 1);
            } 

            DBHelper db = new DBHelper();
            var products = db.Where("select * from product where id in ("+productids+")");

            return new { data = products };
        }


    }
}
