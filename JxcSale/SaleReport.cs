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

namespace JxcSale
{
    public class SaleReport : MarshalByRefObject
    {
        public Object RefreshMVWSaleBill(string parameters)
        {
            using (DBHelper db = new DBHelper(true))
            {
                db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object InitDailySaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object DailySaleReport(string parameters)
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
                    sb.AppendFormat(" and billdate='{0}'", f);
                }
                else if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
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
                sborder.Append(" order by productcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
                            "sum(case when billname='salebill' then qty else -qty end)  as qty," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) / sum(case when billname='salebill' then qty else -qty end) as costprice," +
                            "sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end) as profit," +
                            "(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end)) / sum(case when billname='salebill' then total else -total end)*100 as profitrate " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid from mvw_salebill "
                                + sb.ToString()
                                + " group by productid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from mvw_salebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);
            decimal costtotalsum = Convert.ToDecimal(dtSum.Rows[0]["costtotal"]);
            decimal profittotalsum = Convert.ToDecimal(dtSum.Rows[0]["profittotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                costtotalsum = costtotalsum,
                profittotalsum = profittotalsum,
                data = list
            };
        }

        public Object InitMonthSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object MonthSaleReport(string parameters)
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
                    sb.AppendFormat(" and billdate>='{0}' and billdate<='{1}'", f + "-01", f + "-31");
                }
                else if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
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
                sborder.Append(" order by productcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
                            "sum(case when billname='salebill' then qty else -qty end)  as qty," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) / sum(case when billname='salebill' then qty else -qty end) as costprice," +
                            "sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end) as profit," +
                            "(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end)) / sum(case when billname='salebill' then total else -total end)*100 as profitrate " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid from mvw_salebill "
                                + sb.ToString()
                                + " group by productid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from mvw_salebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);
            decimal costtotalsum = Convert.ToDecimal(dtSum.Rows[0]["costtotal"]);
            decimal profittotalsum = Convert.ToDecimal(dtSum.Rows[0]["profittotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                costtotalsum = costtotalsum,
                profittotalsum = profittotalsum,
                data = list
            };
        }

        public Object InitTimespanSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object TimespanSaleReport(string parameters)
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
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
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
                sborder.Append(" order by productcode ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
                            "sum(case when billname='salebill' then qty else -qty end)  as qty," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) / sum(case when billname='salebill' then qty else -qty end) as costprice," +
                            "sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end) as profit," +
                            "(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end)) / sum(case when billname='salebill' then total else -total end)*100 as profitrate " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid from mvw_salebill "
                                + sb.ToString()
                                + " group by productid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from mvw_salebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);
            decimal costtotalsum = Convert.ToDecimal(dtSum.Rows[0]["costtotal"]);
            decimal profittotalsum = Convert.ToDecimal(dtSum.Rows[0]["profittotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                costtotalsum = costtotalsum,
                profittotalsum = profittotalsum,
                data = list
            };
        }

        public Object InitProductSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object ProductSaleReport(string parameters)
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
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
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
                sborder.Append(" order by discounttotal desc");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
                            "sum(case when billname='salebill' then qty else -qty end)  as qty," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal," +
                            "sum(case when billname='salebill' then costtotal else -costtotal end) / sum(case when billname='salebill' then qty else -qty end) as costprice," +
                            "sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end) as profit," +
                            "(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end)) / sum(case when billname='salebill' then total else -total end)*100 as profitrate " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = "with cte as (select productid from mvw_salebill "
                                + sb.ToString()
                                + " group by productid) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from mvw_salebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);
            decimal costtotalsum = Convert.ToDecimal(dtSum.Rows[0]["costtotal"]);
            decimal profittotalsum = Convert.ToDecimal(dtSum.Rows[0]["profittotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                costtotalsum = costtotalsum,
                profittotalsum = profittotalsum,
                data = list
            };
        }

        public Object InitEmployeeSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit
            };
        }

        public Object EmployeeSaleReport(string parameters)
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
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
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
                sborder.Append(" order by discounttotal desc ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select employeeid,employeecode,employeename," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by employeeid,employeecode,employeename " +
                            sborder.ToString();

            string sqlCount = "with cte as (select employeeid,employeecode,employeename from mvw_salebill "
                                + sb.ToString()
                                + " group by employeeid,employeecode,employeename) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal " +
                            "from mvw_salebill " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }

        public Object InitCustomerSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("customer");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object CustomerSaleReport(string parameters)
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
                        DataTable dt = db.QueryTable("select id,'{}'::jsonb as content from customer where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and customerid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
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
                sborder.Append(" order by discounttotal desc ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select customerid,customercode,customername," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by customerid,customercode,customername " +
                            sborder.ToString();

            string sqlCount = "with cte as (select customerid,customercode,customername from mvw_salebill "
                                + sb.ToString()
                                + " group by customerid,customercode,customername) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                           "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal " +
                           "from mvw_salebill " +
                           sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }

        public Object InitStockSaleReport(string parameters)
        {
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = new DBHelper().First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit
            };
        }

        public Object StockSaleReport(string parameters)
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
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
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
                sborder.Append(" order by discounttotal desc ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select stockid,stockcode,stockname," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by stockid,stockcode,stockname " +
                            sborder.ToString();

            string sqlCount = "with cte as (select stockid,stockcode,stockname from mvw_salebill "
                                + sb.ToString()
                                + " group by stockid,stockcode,stockname) select count(*) from cte";

            string sqlSum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                           "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal " +
                           "from mvw_salebill " +
                           sb.ToString();


            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }


        public Object InitDetailReport(string parameters)
        {
            DBHelper db = new DBHelper();
            ParameterHelper ph = new ParameterHelper(parameters);
            string reporttype = ph.GetParameterValue<string>("type");
            string tip = "";
            if (reporttype == "byproduct")
            {
                int productid = ph.GetParameterValue<int>("productid");
                var product = db.First("product", productid);
                tip = StringHelper.GetString("按产品") + ":" + product.content.Value<string>("code") + " " + product.content.Value<string>("name");
            }
            else if (reporttype == "bycustomer")
            {
                int customerid = ph.GetParameterValue<int>("customerid");
                var customer = db.First("customer", customerid);
                tip = StringHelper.GetString("按客户") + ":" + customer.content.Value<string>("code") + " " + customer.content.Value<string>("name");
            }
            else if (reporttype == "byemployee")
            {
                int employeeid = ph.GetParameterValue<int>("employeeid");
                var employee = db.First("employee", employeeid);
                tip = StringHelper.GetString("按经手人") + ":" + employee.content.Value<string>("code") + " " + employee.content.Value<string>("name");
            }
            else if (reporttype == "bystock")
            {
                int stockid = ph.GetParameterValue<int>("stockid");
                var stock = db.First("stock", stockid);
                tip = StringHelper.GetString("按仓库") + ":" + stock.content.Value<string>("code") + " " + stock.content.Value<string>("name");
            }

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            return new
            {
                showcost = showcost,
                digit = digit,
                tip = tip
            };
        }

        public Object SaleBillDetailReport(string parameters)
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
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and productid in ({0})", ids);
                }
                else if (field == "customername")
                {
                    DataTable dt = db.QueryTable("select id from customer where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and customerid in ({0})", ids);
                }
                else if (field == "employeename")
                {
                    DataTable dt = db.QueryTable("select id from employee where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and employeeid in ({0})", ids);
                }
                else if (field == "stockname")
                {
                    DataTable dt = db.QueryTable("select id from stock where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and stockid in ({0})", ids);
                }
                else if (field == "productid")
                {
                    sb.AppendFormat(" and productid={0}", f);
                }
                else if (field == "customerid")
                {
                    sb.AppendFormat(" and customerid={0}", f);
                }
                else if (field == "employeeid")
                {
                    sb.AppendFormat(" and employeeid={0}", f);
                }
                else if (field == "stockid")
                {
                    sb.AppendFormat(" and stockid={0}", f);
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
                sborder.Append(" order by id ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select id " +
                            "from mvw_salebill " +
                            sb.ToString() +
                            "group by id ";
            DataTable dtIds = db.QueryTable(sql);
            string filterids = dtIds.GetIds();
            sql = string.Format("select * from bill where id in ({0}) " + sborder, filterids);

            string sqlQtySum = "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'qty')::decimal) as qty,content->>'billname' as billname from bill where id in (" + filterids + ")) as t";
            string sqlTotalSum = "select coalesce(sum(case when billname='salebill' then total else -total end),0) as total " +
                                "from  " +
                                "( " +
                                "select  ((jsonb_array_elements(content->'details')->>'discounttotal')::decimal) as total,content->>'billname' as billname from bill where id in (" + filterids + ")) as t";

            int recordcount = dtIds.Rows.Count;
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            var list = db.Where(sql);
            foreach (var item in list)
            {
                item.content.Add("makername", db.First("employee", item.content.Value<int>("makerid")).content.Value<string>("name"));
                item.content.Add("customername", db.First("customer", item.content.Value<int>("customerid")).content.Value<string>("name"));
                item.content.Add("employeename", db.First("employee", item.content.Value<int>("employeeid")).content.Value<string>("name"));
                item.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                decimal qty = item.content.Value<JArray>("details").Values<JObject>().Sum(c => c.Value<decimal>("qty"));
                decimal total = item.content.Value<decimal>("total");
                string billname = item.content.Value<string>("billname");
                item.content.Add("qty", billname == "salebill" ? qty : -qty);
                item.content["total"] = billname == "salebill" ? total : -total;
            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

    }
}
