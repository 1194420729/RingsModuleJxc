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

namespace JxcPay
{
    public class PayReport : MarshalByRefObject
    {
        public Object RefreshMVWReceivableDetail(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_receivabledetail");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object RefreshMVWReceivableCheckout(string parameters)
        {
            using (DBHelper db = new DBHelper())
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_receivablecheckout");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object RefreshMVWPayableDetail(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_payabledetail");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object ReceivableQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'receivable','0')::decimal<>0");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "customername")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                } 

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'code' ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from customer "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from customer "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'receivable')::decimal),0) as total from customer "+sb.ToString();

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

        public Object ReceivableHistoryQuery(string parameters)
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

                if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by customercode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select customerid,customername,customercode,coalesce(sum(total),0) as total from mvw_receivabledetail "
                        + sb.ToString()
                        + " group by customerid,customername,customercode "
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from (select customerid,customername,customercode from mvw_receivabledetail "
                        + sb.ToString()
                        + " group by customerid,customername,customercode) as t";


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_receivabledetail " + sb.ToString();

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

        public Object ReceivableEmployeeQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

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

                if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by employeecode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select employeeid,employeename,employeecode,coalesce(sum(total),0) as total from mvw_receivabledetail "
                        + sb.ToString()
                        + " group by employeeid,employeename,employeecode "
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from (select employeeid,employeename,employeecode from mvw_receivabledetail "
                        + sb.ToString()
                        + " group by employeeid,employeename,employeecode) as t";


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_receivabledetail " + sb.ToString();

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
         
 
        public Object ReceivableQueryDetail(string parameters)
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

                if (field == "customerid")
                {
                    sb.AppendFormat(" and customerid={0}",f);
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

            string sql = "select *  from mvw_receivabledetail where 1=1 "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from mvw_receivabledetail where 1=1 "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_receivabledetail where 1=1 " + sb.ToString();
            string sqlTotalBefore = "select coalesce(sum(total),0) as total from mvw_receivabledetail where billdate<'"
                + myfilter["startdate"] + "' and customerid=" + myfilter["customerid"];

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal totalbefore = Convert.ToDecimal(db.Scalar(sqlTotalBefore));

            DataTable list = db.QueryTable(sql);
             
            var customer = db.First("customer",Convert.ToInt32(myfilter["customerid"]));

            return new
            {
                customer=customer,
                totalbefore=totalbefore,
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }

        public Object ReceivableEmployeeQueryDetail(string parameters)
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

                if (field == "employeeid")
                {
                    sb.AppendFormat(" and employeeid={0}", f);
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

            string sql = "select *  from mvw_receivabledetail where 1=1 "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from mvw_receivabledetail where 1=1 "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_receivabledetail where 1=1 " + sb.ToString();
            string sqlTotalBefore = "select coalesce(sum(total),0) as total from mvw_receivabledetail where billdate<'"
                + myfilter["startdate"] + "' and employeeid=" + myfilter["employeeid"];

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal totalbefore = Convert.ToDecimal(db.Scalar(sqlTotalBefore));

            DataTable list = db.QueryTable(sql);
            list.Columns.Add("billcode", typeof(string));
            list.Columns.Add("billname", typeof(string));
            list.Columns.Add("comment", typeof(string));
            foreach (DataRow item in list.Rows)
            {
                var bill = db.First("bill", Convert.ToInt32(item["billid"]));
                item["billcode"] = bill.content.Value<string>("billcode");
                item["billname"] = bill.content.Value<string>("billname");
                item["comment"] = bill.content.Value<string>("comment");
            }

            var employee = db.First("employee", Convert.ToInt32(myfilter["employeeid"]));

            return new
            {
                employee = employee,
                totalbefore = totalbefore,
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }
 
        public Object PayableQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'payable','0')::decimal<>0");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "vendorname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'code' ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from vendor "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from vendor "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'payable')::decimal),0) as total from vendor " + sb.ToString();

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

        public Object PayableHistoryQuery(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where billdate<='" + myfilter["enddate"] + "'");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
                }

            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by vendorcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select vendorid,vendorname,vendorcode,coalesce(sum(total),0) as total from mvw_payabledetail "
                        + sb.ToString()
                        + " group by vendorid,vendorname,vendorcode "
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from (select vendorid,vendorname,vendorcode from mvw_payabledetail "
                        + sb.ToString()
                        + " group by vendorid,vendorname,vendorcode) as t";


            string sqlTotalSum = "select coalesce(sum(total),0) as total from mvw_payabledetail " + sb.ToString();

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

        public Object PayableQueryDetail(string parameters)
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

                if (field == "vendorid")
                {
                    sb.AppendFormat(" and (content->>'vendorid')::int={0}", f);
                }
                else if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate' ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from payabledetail where 1=1 "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from payabledetail where 1=1 "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total from payabledetail where 1=1 " + sb.ToString();
            string sqlTotalBefore = "select coalesce(sum((content->>'total')::decimal),0) as total from payabledetail where content->>'billdate'<'" + myfilter["startdate"] + "'";

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal totalbefore = Convert.ToDecimal(db.Scalar(sqlTotalBefore));

            List<TableModel> list = db.Where(sql);
            foreach (var item in list)
            {
                var bill = db.First("bill", item.content.Value<int>("billid"));
                var employee = db.First("employee", bill.content.Value<int>("employeeid"));
                item.content.Add("billcode", bill.content["billcode"]);
                item.content.Add("employeename", employee.content["name"]);
                item.content.Add("comment", bill.content["comment"]);
            }

            var vendor = db.First("vendor", Convert.ToInt32(myfilter["vendorid"]));

            return new
            {
                vendor = vendor,
                totalbefore = totalbefore,
                resulttotal = recordcount,
                totalsum = totalsum,
                data = list,
            };
        }

        public Object ReturnedMoneyReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where content->>'billname'='salebill' and (content->>'auditstatus')::int=1 ");


            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'customerid')::int in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'employeeid')::int in ({0})", ids);
                }
                else if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'billdate',id ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select *  from bill "
                        + sb.ToString()
                        + sborder.ToString();

            string sqlCount = "select count(*) as cnt from bill "
                        + sb.ToString();


            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total from bill " + sb.ToString();
            string sqlCheckoutTotalSum = "select coalesce(sum((content->>'checkouttotal')::decimal),0) as total from bill " + sb.ToString();
            DataTable dtBill = db.QueryTable("select id from bill "+sb.ToString());
            string billids = dtBill.GetIds();
            string sqlReturnedTotalSum = string.Format("select coalesce(sum(checkouttotal),0) as total from mvw_receivablecheckout "+
                "where billname='gatheringbill' and salebillid in ({0})",billids) ;
            string sqlAdjustedTotalSum = string.Format("select coalesce(sum(checkouttotal),0) as total from mvw_receivablecheckout " +
                "where billname='receivablebill' and salebillid in ({0})", billids);

            int recordcount = db.Count(sqlCount);
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal checkouttotalsum = Convert.ToDecimal(db.Scalar(sqlCheckoutTotalSum));
            decimal receivabletotalsum = totalsum-checkouttotalsum;
            decimal returnedtotalsum = Convert.ToDecimal(db.Scalar(sqlReturnedTotalSum));
            decimal adjustedtotalsum = Convert.ToDecimal(db.Scalar(sqlAdjustedTotalSum));

            var list = db.Where(sql);
            foreach (var item in list)
            {
                var customer = db.First("customer",item.content.Value<int>("customerid"));
                var employee = db.First("employee", item.content.Value<int>("employeeid"));
                item.content["customername"] = customer.content.Value<string>("name");
                item.content["employeename"] = employee.content.Value<string>("name");
                item.content["receivabletotal"] = item.content.Value<decimal>("total") - item.content.Value<decimal>("checkouttotal");
                string sqlreturned = "select  coalesce(sum(checkouttotal),0) as total from mvw_receivablecheckout where billname='gatheringbill' and salebillid="+item.id;
                item.content["returnedtotal"] = Convert.ToDecimal(db.Scalar(sqlreturned));
                string sqladjusted = "select  coalesce(sum(checkouttotal),0) as total from mvw_receivablecheckout where billname='receivablebill' and salebillid=" + item.id;
                item.content["adjustedtotal"] = Convert.ToDecimal(db.Scalar(sqladjusted));
            }
              
            return new
            { 
                resulttotal = recordcount,
                totalsum = totalsum,
                checkouttotalsum = checkouttotalsum,
                receivabletotalsum = receivabletotalsum,
                returnedtotalsum = returnedtotalsum,
                adjustedtotalsum = adjustedtotalsum,
                data = list,
            };
        }

        public Object ReceivableComponentQuery(string parameters)
        {
  

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'receivable','0')::decimal<>0");
             
            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by (coalesce(content->>'receivable','0')::decimal) desc ");
             
            sborder.AppendFormat(" limit {0} ", 5);

            string sql = "select *  from customer "
                        + sb.ToString()
                        + sborder.ToString();
              
            List<TableModel> list = db.Where(sql);

            var result = (from c in list
                         select new
                         {
                             name=c.content.Value<string>("name"),
                             total = c.content.Value<decimal>("receivable").ToString("c2")
                         }).ToList();

            return new
            { 
                data = result,
            };
        }

        public Object PayableComponentQuery(string parameters)
        { 
            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'payable','0')::decimal<>0");
             
            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by (coalesce(content->>'payable','0')::decimal) desc ");


            sborder.AppendFormat(" limit {0} ", 5);

            string sql = "select *  from vendor "
                        + sb.ToString()
                        + sborder.ToString();

            
            List<TableModel> list = db.Where(sql);

            var result = (from c in list
                          select new
                          {
                              name = c.content.Value<string>("name"),
                              total = c.content.Value<decimal>("payable").ToString("c2")
                          }).ToList();

            return new
            { 
                data = result,
            };
        }

    }
}
