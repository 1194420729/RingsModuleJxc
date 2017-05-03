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

namespace JxcAccounting
{
    public class FeeReport : MarshalByRefObject
    {
        public Object RefreshMVWFeeBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_feebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object InitTimespanFeeReport(string parameters)
        { 
            var categorys = CategoryHelper.GetCategoryTreeData("fee");

            return new
            { 
                categorys = categorys
            };
        }

        public Object TimespanFeeReport(string parameters)
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
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from fee where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and feeid in ({0})", idsfilter);
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
                sborder.Append(" order by feecode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select feeid,feecode,feename,sum(total) as total " +
                            "from mvw_feebill " +
                            sb.ToString() +
                            "group by feeid,feecode,feename " +
                            sborder.ToString();

            string sqlCount = "with cte as (select feeid from mvw_feebill "
                                + sb.ToString()
                                + " group by feeid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(total),0) as total " +
                            "from mvw_feebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);            
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlSum)); 

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount, 
                totalsum = totalsum, 
                data = list
            };
        }
         
    }
}
