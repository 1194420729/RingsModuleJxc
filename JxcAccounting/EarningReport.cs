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
    public class EarningReport : MarshalByRefObject
    {
        public Object RefreshMVWEarningBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_earningbill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object InitTimespanEarningReport(string parameters)
        { 
            var categorys = CategoryHelper.GetCategoryTreeData("earning");

            return new
            { 
                categorys = categorys
            };
        }

        public Object TimespanEarningReport(string parameters)
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
                        DataTable dt = db.QueryTable("select id from earning where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and earningid in ({0})", idsfilter);
                    }
                }                 
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
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
                sborder.Append(" order by earningcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select earningid,earningcode,earningname,sum(total) as total " +
                            "from mvw_earningbill " +
                            sb.ToString() +
                            "group by earningid,earningcode,earningname " +
                            sborder.ToString();

            string sqlCount = "with cte as (select earningid from mvw_earningbill "
                                + sb.ToString()
                                + " group by earningid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(total),0) as total " +
                            "from mvw_earningbill " +
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

        public Object InitEarningBillCustomerReport(string parameters)
        {
            var categorys = CategoryHelper.GetCategoryTreeData("customer");

            return new
            {
                categorys = categorys
            };
        }

        public Object EarningBillCustomerReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where customerid is not null ");

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
                        DataTable dt = db.QueryTable("select id from customer where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and customerid in ({0})", idsfilter);
                    }
                }
                else if (field == "earningname")
                {
                    sb.AppendFormat(" and (earningname ilike '%{0}%' or earningcode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
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
                sborder.Append(" order by customercode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            
            string sql = "select customerid,customercode,customername,sum(total) as total " +
                            "from mvw_earningbill " +
                            sb.ToString() +
                            "group by customerid,customercode,customername " +
                            sborder.ToString();

            string sqlCount = "select count(customerid) as cnt from (select customerid from mvw_earningbill "
                                + sb.ToString()
                                + " group by customerid) as t";

            string sqlSum = "select coalesce(sum(total),0) as total " +
                            "from mvw_earningbill " +
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

        public Object EarningBillEmployeeReport(string parameters)
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
                else if (field == "earningname")
                {
                    sb.AppendFormat(" and (earningname ilike '%{0}%' or earningcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
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
             
            string sql = "select employeeid,employeecode,employeename,sum(total) as total " +
                            "from mvw_earningbill " +
                            sb.ToString() +
                            "group by employeeid,employeecode,employeename " +
                            sborder.ToString();

            string sqlCount = "select count(employeeid) as cnt from (select employeeid from mvw_earningbill "
                                + sb.ToString()
                                + " group by employeeid) as t";

            string sqlSum =  "select coalesce(sum(total),0) as total " +
                            "from mvw_earningbill " +
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

        public Object InitDetailReport(string parameters)
        {
            DBHelper db = new DBHelper();
            ParameterHelper ph = new ParameterHelper(parameters);
            string reporttype = ph.GetParameterValue<string>("type");
            string tip = "";
            if (reporttype == "bycustomer")
            {
                int customerid = ph.GetParameterValue<int>("customerid");
                var customer = db.First("customer", customerid);
                tip = customer.content.Value<string>("code") + " " + customer.content.Value<string>("name");
            }
            else if (reporttype == "byemployee")
            {
                int employeeid = ph.GetParameterValue<int>("employeeid");
                var employee = db.First("employee", employeeid);
                tip = employee.content.Value<string>("code") + " " + employee.content.Value<string>("name");
            }
            else if (reporttype == "byearning")
            {
                int earningid = ph.GetParameterValue<int>("earningid");
                var earning = db.First("earning", earningid);
                tip = earning.content.Value<string>("code") + " " + earning.content.Value<string>("name");
            }
             
            //var option = db.First("select * from option");
            int digit = 2;//option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            { 
                digit = digit,
                tip = tip
            };
        }

        public Object EarningBillDetailReport(string parameters)
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
                else if (field == "earningid")
                {
                    sb.AppendFormat(" and earningid={0}", f);
                }
                else if (field == "customerid")
                {
                    sb.AppendFormat(" and customerid={0}", f);
                }
                else if (field == "employeeid")
                {
                    sb.AppendFormat(" and employeeid={0}", f);
                }
                else if (field == "earningname")
                {
                    sb.AppendFormat(" and (earningname ilike '%{0}%' or earningcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }  
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,earningcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);
             
            string sql =  "select * " +
                            "from mvw_earningbill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_earningbill "
                            + sb.ToString();


            string sqlSum =  "select coalesce(sum(total),0) as totalsum " +
                            "from mvw_earningbill " +
                            sb.ToString();


            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["totalsum"]);  

            DataTable list = db.QueryTable(sql);
             
            return new
            { 
                resulttotal = recordcount, 
                totalsum = totalsum, 
                data = list
            };
        }

        public Object EarningFlow(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            //var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
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
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and billcode ilike '%{0}%'", f);
                }                
                else if (field == "earningname")
                {
                    sb.AppendFormat(" and (earningname ilike '%{0}%' or earningcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "makername")
                {
                    sb.AppendFormat(" and (makername ilike '%{0}%' or makercode ilike '%{0}%')", f);
                }
                else if (field == "comment")
                {
                    sb.AppendFormat(" and comment ilike '%{0}%'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,earningcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);
 
            string sql =  "select * " +
                            "from mvw_earningbill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_earningbill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(total),0) as totalsum " +
                            "from mvw_earningbill " +
                            sb.ToString();


            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["totalsum"]);

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
