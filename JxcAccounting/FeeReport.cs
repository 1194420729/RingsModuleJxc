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
                        DataTable dt = db.QueryTable("select id from fee where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and feeid in ({0})", idsfilter);
                    }
                }                 
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
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

        public Object InitFeeBillVendorReport(string parameters)
        {
            var categorys = CategoryHelper.GetCategoryTreeData("vendor");

            return new
            {
                categorys = categorys
            };
        }

        public Object FeeBillVendorReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where vendorid is not null ");

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
                        DataTable dt = db.QueryTable("select id from vendor where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and vendorid in ({0})", idsfilter);
                    }
                }
                else if (field == "feename")
                {
                    sb.AppendFormat(" and (feename ilike '%{0}%' or feecode ilike '%{0}%')", f);
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
                sborder.Append(" order by vendorcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            
            string sql = "select vendorid,vendorcode,vendorname,sum(total) as total " +
                            "from mvw_feebill " +
                            sb.ToString() +
                            "group by vendorid,vendorcode,vendorname " +
                            sborder.ToString();

            string sqlCount = "select count(vendorid) as cnt from (select vendorid from mvw_feebill "
                                + sb.ToString()
                                + " group by vendorid) as t";

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

        public Object FeeBillEmployeeReport(string parameters)
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
                else if (field == "feename")
                {
                    sb.AppendFormat(" and (feename ilike '%{0}%' or feecode ilike '%{0}%')", f);
                }
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
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
                            "from mvw_feebill " +
                            sb.ToString() +
                            "group by employeeid,employeecode,employeename " +
                            sborder.ToString();

            string sqlCount = "select count(employeeid) as cnt from (select employeeid from mvw_feebill "
                                + sb.ToString()
                                + " group by employeeid) as t";

            string sqlSum =  "select coalesce(sum(total),0) as total " +
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

        public Object InitDetailReport(string parameters)
        {
            DBHelper db = new DBHelper();
            ParameterHelper ph = new ParameterHelper(parameters);
            string reporttype = ph.GetParameterValue<string>("type");
            string tip = "";
            if (reporttype == "byvendor")
            {
                int vendorid = ph.GetParameterValue<int>("vendorid");
                var vendor = db.First("vendor", vendorid);
                tip = vendor.content.Value<string>("code") + " " + vendor.content.Value<string>("name");
            }
            else if (reporttype == "byemployee")
            {
                int employeeid = ph.GetParameterValue<int>("employeeid");
                var employee = db.First("employee", employeeid);
                tip = employee.content.Value<string>("code") + " " + employee.content.Value<string>("name");
            }
            else if (reporttype == "byfee")
            {
                int feeid = ph.GetParameterValue<int>("feeid");
                var fee = db.First("fee", feeid);
                tip = fee.content.Value<string>("code") + " " + fee.content.Value<string>("name");
            }
             
            //var option = db.First("select * from option");
            int digit = 2;//option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            { 
                digit = digit,
                tip = tip
            };
        }

        public Object FeeBillDetailReport(string parameters)
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
                else if (field == "feeid")
                {
                    sb.AppendFormat(" and feeid={0}", f);
                }
                else if (field == "vendorid")
                {
                    sb.AppendFormat(" and vendorid={0}", f);
                }
                else if (field == "employeeid")
                {
                    sb.AppendFormat(" and employeeid={0}", f);
                }
                else if (field == "feename")
                {
                    sb.AppendFormat(" and (feename ilike '%{0}%' or feecode ilike '%{0}%')", f);
                }
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }  
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,feecode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);
             
            string sql =  "select * " +
                            "from mvw_feebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_feebill "
                            + sb.ToString();


            string sqlSum =  "select coalesce(sum(total),0) as totalsum " +
                            "from mvw_feebill " +
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

        public Object FeeFlow(string parameters)
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
                else if (field == "feename")
                {
                    sb.AppendFormat(" and (feename ilike '%{0}%' or feecode ilike '%{0}%')", f);
                }
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
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
            sborder.Append(" order by id,feecode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);
 
            string sql =  "select * " +
                            "from mvw_feebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_feebill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(total),0) as totalsum " +
                            "from mvw_feebill " +
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
