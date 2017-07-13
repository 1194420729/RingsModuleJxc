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
    public class PurchaseInvoiceReport : MarshalByRefObject
    {
        public Object RefreshMVWPurchaseInvoiceBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchaseinvoicebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object PurchaseInvoiceBillReport(string parameters)
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
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
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
                sborder.Append(" order by vendorcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as (select mvw_purchaseinvoicebill.* "+
                            "from mvw_purchaseinvoicebill "+
                            "inner join vendor on mvw_purchaseinvoicebill.vendorid=vendor.id) ";

            string sql = cte + "select vendorid,vendorcode,vendorname,"+
                                "sum(invoicetotal) as invoicetotal," +
                                "sum(purchasetotal) as purchasetotal," +
                                "sum(purchasetotal)-sum(invoicetotal) as uninvoicetotal " +
                                "from cte " +
                                sb.ToString() +
                                "group by vendorid,vendorcode,vendorname " +
                                sborder.ToString();

            string sqlCount = cte+"select count(0) as cnt from (select vendorid from cte "+
                                sb.ToString()+
                                " group by vendorid) as t";

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

        public Object PurchaseInvoiceBillReportDetail(string parameters)
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
                else if (field == "vendorid")
                {
                    sb.AppendFormat(" and vendorid={0}", f);
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
            sborder.Append(" order by id,purchasebillid ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from mvw_purchaseinvoicebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(0) from mvw_purchaseinvoicebill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(invoicetotal),0) as totalsum " +
                            "from mvw_purchaseinvoicebill " +
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

        public Object PurchaseInvoiceQuery(string parameters)
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
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendorname ilike '%{0}%' or vendorcode ilike '%{0}%')", f);
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
            sborder.Append(" order by id,purchasebillid ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from mvw_purchaseinvoicebill " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(0) from mvw_purchaseinvoicebill "
                            + sb.ToString();


            string sqlSum = "select coalesce(sum(invoicetotal),0) as totalsum " +
                            "from mvw_purchaseinvoicebill " +
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

        public Object PurchaseUnInvoiceBillReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where ((bill.content ->> 'billname'::text) = 'purchasebill'::text OR (bill.content ->> 'billname'::text) = 'purchasebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 ");

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
                else if (field == "vendorname")
                {
                    sb.AppendFormat(" and (vendor.content->>'name' ilike '%{0}%' or vendor.content->>'code' ilike '%{0}%')", f);
                }
            }

            StringBuilder sborder = new StringBuilder();

            sborder.Append(" order by vendor.content->>'code' ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);


            string sql = "select vendor.id as vendorid,vendor.content->>'code' as vendorcode,vendor.content->>'name' as vendorname," +
                                "sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as invoicetotal," +
                                "sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end) as purchasetotal," +
                                "sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as uninvoicetotal " +
                                "from bill inner join vendor on (bill.content->>'vendorid')::int=vendor.id " +
                                sb.ToString() +
                                "group by vendor.id,vendor.content->>'code',vendor.content->>'name' " +
                                sborder.ToString();

            string sqlCount = "select count(0) as cnt from (select vendor.id from bill inner join vendor on (bill.content->>'vendorid')::int=vendor.id " +
                                sb.ToString() +
                                " group by vendor.id) as t";

            string sqlSum = "select coalesce(sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal),0) as uninvoicetotal " +
                                    "from bill inner join vendor on (bill.content->>'vendorid')::int=vendor.id " +
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

        public Object PurchaseUnInvoiceComponentQuery(string parameters)
        {
            DBHelper db = new DBHelper();
            string sql = "select vendor.id as vendorid,vendor.content->>'code' as vendorcode,vendor.content->>'name' as vendorname," +
                                "sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as invoicetotal," +
                                "sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end) as purchasetotal," +
                                "sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(bill.content->>'invoicetotal','0')::decimal) as uninvoicetotal " +
                                "from bill inner join vendor on (bill.content->>'vendorid')::int=vendor.id " +
                                "where ((bill.content ->> 'billname'::text) = 'purchasebill'::text OR (bill.content ->> 'billname'::text) = 'purchasebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                                "group by vendor.id,vendor.content->>'code',vendor.content->>'name' " +
                                "order by uninvoicetotal desc limit 5";


            DataTable list = db.QueryTable(sql);

            return new
            {
                data = list
            };
        }

        public Object PurchaseUnInvoiceReportDetail(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where ((bill.content ->> 'billname'::text) = 'purchasebill'::text OR (bill.content ->> 'billname'::text) = 'purchasebackbill'::text) AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 ");

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
                else if (field == "vendorid")
                {
                    sb.AppendFormat(" and (bill.content->>'vendorid')::int={0}", f);
                }
            }

            StringBuilder sborder = new StringBuilder();

            sborder.Append(" order by bill.content->>'billdate' desc,bill.id desc ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);


            string sql = "select * from bill " +
                                sb.ToString() +
                                sborder.ToString();

            string sqlCount = "select count(0) as cnt from bill " +
                                sb.ToString();

            string sqlSum = "select coalesce(sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end),0) as total," +
                            "coalesce(sum(coalesce(content->>'invoicetotal','0')::decimal),0) as invoicetotal," +
                            "coalesce(sum(case bill.content->>'billname' when 'purchasebill' then (bill.content->>'total')::decimal else -((bill.content->>'total')::decimal) end)-sum(coalesce(content->>'invoicetotal','0')::decimal),0) as uninvoicetotal " +
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
                item.content["total"] = (billname == "purchasebill" ? item.content.Value<decimal>("total") : (-item.content.Value<decimal>("total")));

                item.content["uninvoicetotal"] = item.content.Value<decimal>("total") - item.content.Value<decimal>("invoicetotal");

                item.content["employeename"] = db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name");
                item.content["makername"] = db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name");
            }

            var vendorname = db.First("vendor", Convert.ToInt32(myfilter["vendorid"])).content.Value<string>("name");
            return new
            {
                vendorname = vendorname,
                resulttotal = recordcount,
                totalsum = totalsum,
                invoicetotalsum = invoicetotalsum,
                uninvoicetotalsum = uninvoicetotalsum,
                data = list
            };
        }

    }
}
