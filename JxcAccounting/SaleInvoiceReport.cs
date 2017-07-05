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
    public class SaleInvoiceReport : MarshalByRefObject
    {
        public Object RefreshMVWSaleInvoiceBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_saleinvoicebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object RefreshMVWSaleBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object SaleInvoiceBillReport(string parameters)
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
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "invoicetype")
                {
                    sb.AppendFormat(" and invoicetype='{0}'", f);
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

            string cte = "with cte as (select mvw_saleinvoicebill.* "+
                            "from mvw_saleinvoicebill "+
                            "inner join customer on mvw_saleinvoicebill.customerid=customer.id) ";

            string sql = cte + "select customerid,customercode,customername,"+
                                "sum(invoicetotal) as invoicetotal," +
                                "sum(saletotal) as saletotal," +
                                "sum(saletotal)-sum(invoicetotal) as uninvoicetotal " +
                                "from cte " +
                                sb.ToString() +
                                "group by customerid,customercode,customername " +
                                sborder.ToString();

            string sqlCount = cte+"select count(*) as cnt from (select customerid from cte "+
                                sb.ToString()+
                                " group by customerid) as t";

            string sqlSum = cte + "select  coalesce(sum(invoicetotal),0) as invoicetotal " +
                                    "from cte " +
                                    sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum= db.QueryTable(sqlSum);
            
            decimal invoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["invoicetotal"]); 

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount, 
                invoicetotalsum=invoicetotalsum, 
                data = list
            };
        }

        public Object SaleInvoiceBillReportDetail(string parameters)
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

                if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                } 
                else if (field == "customerid")
                {
                    sb.AppendFormat(" and customerid={0}", f);
                }                 
                else if (field == "invoicetype")
                {
                    sb.AppendFormat(" and invoicetype='{0}'", f);
                }
                else if (field == "invoicecode")
                {
                    sb.AppendFormat(" and invoicecode ilike '%{0}%'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,salebillid ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from mvw_saleinvoicebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_saleinvoicebill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(invoicetotal),0) as totalsum " +
                            "from mvw_saleinvoicebill " +
                            sb.ToString();


            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal invoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["totalsum"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                invoicetotalsum = invoicetotalsum,
                data = list
            };
        }

        public Object SaleInvoiceQuery(string parameters)
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

                if (field == "startdate")
                {
                    sb.AppendFormat(" and billdate>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and billdate<='{0}'", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "invoicetype")
                {
                    sb.AppendFormat(" and invoicetype='{0}'", f);
                }
                else if (field == "invoicecode")
                {
                    sb.AppendFormat(" and invoicecode ilike '%{0}%'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,salebillid ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from mvw_saleinvoicebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) from mvw_saleinvoicebill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(invoicetotal),0) as totalsum " +
                            "from mvw_saleinvoicebill " +
                            sb.ToString();


            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal invoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["totalsum"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                invoicetotalsum = invoicetotalsum,
                data = list
            };
        }

        public Object SaleUnInvoiceBillReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where ((bill.content ->> 'billname'::text) = 'salebill'::text OR (bill.content ->> 'billname'::text) = 'salebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and bill.content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and bill.content->>'billdate'<='{0}'", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customer.content->>'name' ilike '%{0}%' or customer.content->>'code' ilike '%{0}%')", f);
                } 
            }

            StringBuilder sborder = new StringBuilder();
            
            sborder.Append(" order by customer.content->>'code' ");
             
            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);


            string sql =  "select customer.id as customerid,customer.content->>'code' as customercode,customer.content->>'name' as customername," +
                                "sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as invoicetotal," +
                                "sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end) as saletotal," +
                                "sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as uninvoicetotal " +
                                "from bill inner join customer on (bill.content->>'customerid')::int=customer.id " +
                                sb.ToString() +
                                "group by customer.id,customer.content->>'code',customer.content->>'name' " +
                                sborder.ToString();

            string sqlCount = "select count(*) as cnt from (select customer.id from bill inner join customer on (bill.content->>'customerid')::int=customer.id " +
                                sb.ToString() +
                                " group by customer.id) as t";

            string sqlSum = "select coalesce(sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal),0) as uninvoicetotal " +
                                    "from bill inner join customer on (bill.content->>'customerid')::int=customer.id " +
                                    sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal uninvoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["uninvoicetotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                uninvoicetotalsum = uninvoicetotalsum,
                data = list
            };
        }

        public Object SaleUnInvoiceComponentQuery(string parameters)
        { 
            DBHelper db = new DBHelper();
            string sql = "select customer.id as customerid,customer.content->>'code' as customercode,customer.content->>'name' as customername," +
                                "sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as invoicetotal," +
                                "sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end) as saletotal," +
                                "sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as uninvoicetotal " +
                                "from bill inner join customer on (bill.content->>'customerid')::int=customer.id " +
                                "where ((bill.content ->> 'billname'::text) = 'salebill'::text OR (bill.content ->> 'billname'::text) = 'salebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                                "group by customer.id,customer.content->>'code',customer.content->>'name' " +
                                "order by uninvoicetotal desc limit 5";

              
            DataTable list = db.QueryTable(sql);
             
            return new
            { 
                data = list
            };
        }


        public Object SaleUnInvoiceReportDetail(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);
             
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where ((bill.content ->> 'billname'::text) = 'salebill'::text OR (bill.content ->> 'billname'::text) = 'salebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and bill.content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and bill.content->>'billdate'<='{0}'", f);
                }
                else if (field == "customerid")
                {
                    sb.AppendFormat(" and (bill.content->>'customerid')::int={0}", f);
                }
            }

            StringBuilder sborder = new StringBuilder();

            sborder.Append(" order by bill.content->>'billdate' desc,bill.id desc ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);


            string sql = "select * from bill " +
                                sb.ToString() +
                                sborder.ToString();

            string sqlCount = "select count(*) as cnt from bill " +
                                sb.ToString() ;

            string sqlSum = "select coalesce(sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end),0) as total,"+
                            "coalesce(sum(coalesce(content->>'invoicetotal','0')::decimal),0) as invoicetotal," +
                            "coalesce(sum(case bill.content->>'billname' when 'salebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(content->>'invoicetotal','0')::decimal),0) as uninvoicetotal " +
                            "from bill  " +
                                    sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal invoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["invoicetotal"]);
            decimal uninvoicetotalsum = Convert.ToDecimal(dtSum.Rows[0]["uninvoicetotal"]);

            var list = db.Where(sql);
            foreach (var item in list)
            {
                string billname = item.content.Value<string>("billname");
                item.content["total"] = (billname == "salebill" ? item.content.Value<decimal>("total") : (-item.content.Value<decimal>("total")));

                item.content["uninvoicetotal"] = item.content.Value<decimal>("total") - item.content.Value<decimal>("invoicetotal");

                item.content["employeename"] = db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name");
                item.content["makername"] = db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name");
            }

            var customername = db.First("customer", Convert.ToInt32(myfilter["customerid"])).content.Value<string>("name");
            return new
            {
                customername=customername,
                resulttotal = recordcount,
                totalsum = totalsum,
                invoicetotalsum = invoicetotalsum,
                uninvoicetotalsum = uninvoicetotalsum,
                data = list
            };
        }

    }
}
