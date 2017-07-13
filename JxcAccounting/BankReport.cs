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
using System.Net;
using System.Text;

namespace JxcAccounting
{
    public class BankReport : MarshalByRefObject
    {
        public Object RefreshMVWBankDetail(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_bankdetail");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object BankQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'stop','')=''");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "bankname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                } 

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'code' ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bank "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from bank "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total from bank "+sb.ToString();

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            List<TableModel> list = db.Where(sql);
            

            return new
            {
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }

        public Object BankHistoryQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where billdate<='"+myfilter["enddate"]+"'");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "bankname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                } 

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by bankcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select bankid,bankname,bankcode,coalesce(sum(total),0) as total from mvw_bankdetail "
                        + sb.ToString()
                        + " group by bankid,bankname,bankcode "
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from (select bankid from mvw_bankdetail "
                        + sb.ToString()
                        + " group by bankid) as t";


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_bankdetail " + sb.ToString();

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            DataTable list = db.QueryTable(sql);
 
            return new
            {
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }
 
        public Object BankQueryDetail(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
             
            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "bankid")
                {
                    sb.AppendFormat(" and bankid={0}",f);
                }
                else if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by billdate,id ");
             
            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from mvw_bankdetail where 1=1 "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from mvw_bankdetail where 1=1 "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_bankdetail where 1=1 " + sb.ToString();
            string sqlTotalBefore = "select coalesce(sum(total),0) as total from mvw_bankdetail where billdate<'"
                + myfilter["startdate"] + "' and bankid=" + myfilter["bankid"];

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal totalbefore = Convert.ToDecimal(db.Scalar(sqlTotalBefore));

            DataTable list = db.QueryTable(sql);
            
            var bank = db.First("bank",Convert.ToInt32(myfilter["bankid"]));

            return new
            {
                bank=bank,
                totalbefore=totalbefore,
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }

        public Object BankFlow(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();

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
                else if (field == "bankname")
                {
                    sb.AppendFormat(" and (bankname ilike '%{0}%' or bankcode ilike '%{0}%')", f);
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
            sborder.Append(" order by id,bankcode ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from mvw_bankdetail where 1=1 "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(0) as cnt from mvw_bankdetail where 1=1 "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_bankdetail where 1=1 " + sb.ToString();
             
            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
             
            DataTable list = db.QueryTable(sql);
             
            return new
            { 
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }
    }
}
