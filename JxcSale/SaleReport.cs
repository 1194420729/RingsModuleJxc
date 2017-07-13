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
                        DataTable dt = db.QueryTable("select id from product where (content->>'categoryid')::int in (" + idsfilter + ")");
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                            + "inner join stock on mvw_salebill.stockid=stock.id "
                            + "inner join customer on mvw_salebill.customerid=customer.id "
                            + "inner join product on mvw_salebill.productid=product.id) ";

            string sql = cte +
                            "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
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
                            "from cte " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = cte
                            + "select count(0) from (select productid from cte "
                            + sb.ToString()
                            + " group by productid) as t";

            string sqlSum = cte +
                            "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from cte " +
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
                        DataTable dt = db.QueryTable("select id from product where (content->>'categoryid')::int in (" + idsfilter + ")");
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                            + "inner join stock on mvw_salebill.stockid=stock.id "
                            + "inner join customer on mvw_salebill.customerid=customer.id "
                            + "inner join product on mvw_salebill.productid=product.id) ";

            string sql = cte + "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
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
                            "from cte " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = cte
                           + "select count(0) from (select productid from cte "
                           + sb.ToString()
                           + " group by productid) as t";


            string sqlSum = cte + "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from cte " +
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
                        DataTable dt = db.QueryTable("select id from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                            + "inner join stock on mvw_salebill.stockid=stock.id "
                            + "inner join customer on mvw_salebill.customerid=customer.id "
                            + "inner join product on mvw_salebill.productid=product.id) ";


            string sql = cte + "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
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
                            "from cte " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = cte
                           + "select count(0) from (select productid from cte "
                           + sb.ToString()
                           + " group by productid) as t";

            string sqlSum = cte + "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from cte " +
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
                        DataTable dt = db.QueryTable("select id from product where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and productid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                           + "inner join stock on mvw_salebill.stockid=stock.id "
                           + "inner join customer on mvw_salebill.customerid=customer.id "
                           + "inner join product on mvw_salebill.productid=product.id) ";


            string sql = cte + "select productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea," +
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
                            "from cte " +
                            sb.ToString() +
                            "group by productid,productcode,productname,producttype,productstandard,productbarcode,productunit,productarea " +
                            sborder.ToString();

            string sqlCount = cte
                           + "select count(0) from (select productid from cte "
                           + sb.ToString()
                           + " group by productid) as t";

            string sqlSum = cte + "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                            "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                            "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal," +
                            "coalesce(sum(case when billname='salebill' then costtotal else -costtotal end),0) as costtotal," +
                            "coalesce(sum(case when billname='salebill' then total else -total end)-sum(case when billname='salebill' then costtotal else -costtotal end),0) as profittotal " +
                            "from cte " +
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
                                + " group by employeeid,employeecode,employeename) select count(0) from cte";

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
                        DataTable dt = db.QueryTable("select id from customer where (content->>'categoryid')::int in (" + idsfilter + ")");
                        idsfilter = dt.GetIds();
                        sb.AppendFormat(" and customerid in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                           + "inner join stock on mvw_salebill.stockid=stock.id "
                           + "inner join customer on mvw_salebill.customerid=customer.id "
                           + "inner join product on mvw_salebill.productid=product.id) ";


            string sql = cte + "select customerid,customercode,customername," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice " +
                            "from cte " +
                            sb.ToString() +
                            "group by customerid,customercode,customername " +
                            sborder.ToString();

            string sqlCount = cte
                           + "select count(0) from (select customerid from cte "
                           + sb.ToString()
                           + " group by customerid) as t";


            string sqlSum = cte + "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                           "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal " +
                           "from cte " +
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
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
                }
                else if (field == "customername")
                {
                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
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

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                           + "inner join stock on mvw_salebill.stockid=stock.id "
                           + "inner join customer on mvw_salebill.customerid=customer.id "
                           + "inner join product on mvw_salebill.productid=product.id) ";

            string sql = cte + "select stockid,stockcode,stockname," +
                            "sum(case when billname='salebill' then total else -total end) as total," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end) as taxtotal," +
                            "sum(case when billname='salebill' then total else -total end) /sum(case when billname='salebill' then qty else -qty end) as price," +
                            "sum(case when billname='salebill' then discounttotal else -discounttotal end) /sum(case when billname='salebill' then qty else -qty end) as discountprice " +
                            "from cte " +
                            sb.ToString() +
                            "group by stockid,stockcode,stockname " +
                            sborder.ToString();

            string sqlCount = cte
                           + "select count(0) from (select stockid from cte "
                           + sb.ToString()
                           + " group by stockid) as t";

            string sqlSum = cte + "select coalesce(sum(case when billname='salebill' then qty else -qty end),0) as qty," +
                           "coalesce(sum(case when billname='salebill' then total else -total end),0) as total," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end),0) as discounttotal," +
                           "coalesce(sum(case when billname='salebill' then discounttotal else -discounttotal end)-sum(case when billname='salebill' then total else -total end),0) as taxtotal " +
                           "from cte " +
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
                tip = product.content.Value<string>("code") + " " + product.content.Value<string>("name");
            }
            else if (reporttype == "bycustomer")
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
            else if (reporttype == "bystock")
            {
                int stockid = ph.GetParameterValue<int>("stockid");
                var stock = db.First("stock", stockid);
                tip = stock.content.Value<string>("code") + " " + stock.content.Value<string>("name");
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
                else if (field == "customername")
                {

                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
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
            sborder.Append(" order by id,productcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                            + "inner join stock on mvw_salebill.stockid=stock.id "
                            + "inner join customer on mvw_salebill.customerid=customer.id "
                            + "inner join product on mvw_salebill.productid=product.id) ";

            string sql = cte
                            + "select * " +
                            "from cte " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = cte
                            + "select count(0) from cte "
                            + sb.ToString();

            string sqlSum = cte
                            + "select coalesce(sum(qty) filter (where billname='salebill'),0)-coalesce(sum(qty) filter (where billname='salebackbill'),0) as qty," +
                            "coalesce(sum(total) filter (where billname='salebill'),0)-coalesce(sum(total) filter (where billname='salebackbill'),0) as total," +
                            "coalesce(sum(discounttotal) filter (where billname='salebill'),0)-coalesce(sum(discounttotal) filter (where billname='salebackbill'),0) as discounttotal " +
                            "from cte " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = discounttotalsum - totalsum;

            DataTable list = db.QueryTable(sql);

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                showcost = showcost,
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }

        public Object SaleOrderFlow(string parameters)
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
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and billcode ilike '%{0}%'", f);
                }
                else if (field == "customername")
                {

                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
                }
                else if (field == "makername")
                {
                    sb.AppendFormat(" and (makername ilike '%{0}%' or makercode ilike '%{0}%')", f);
                }
                else if (field == "comment")
                {
                    sb.AppendFormat(" and comment ilike '%{0}%'", f);
                }
                else if (field == "finish")
                {
                    if (f == "aborted")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=true");
                    }
                    else if (f == "finished")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=false and deliveryqty>=qty");
                    }
                    else if (f == "unfinish")
                    {
                        sb.Append(" and coalesce((content->>'abort')::boolean,false)=false  and deliveryqty<qty");
                    }
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by id,productcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as (select mvw_saleorder.* from mvw_saleorder "
                            + "inner join stock on mvw_saleorder.stockid=stock.id "
                            + "inner join customer on mvw_saleorder.customerid=customer.id "
                            + "inner join product on mvw_saleorder.productid=product.id) ";

            string sql = cte
                            + "select * " +
                            "from cte " +
                            sb.ToString() +
                            sborder.ToString();

            //string sqlCount = "with cte as (select productid,productcode,productname,producttype,productstandard from mvw_saleorder "
            //                    + sb.ToString()
            //                    + " group by productid,productcode,productname,producttype,productstandard) select count(0) from cte";

            string sqlCount = cte
                            + "select count(0) from cte "
                            + sb.ToString();

            string sqlSum = cte
                            + "select coalesce(sum(qty),0) as qty,coalesce(sum(deliveryqty),0) as deliveryqty," +
                            "coalesce(sum(total),0) as total," +
                            "coalesce(sum(discounttotal),0) as discounttotal," +
                            "coalesce(sum(discounttotal)-sum(total),0) as taxtotal " +
                            "from cte " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal deliveryqtysum = Convert.ToDecimal(dtSum.Rows[0]["deliveryqty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = Convert.ToDecimal(dtSum.Rows[0]["taxtotal"]);

            DataTable list = db.QueryTable(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                deliveryqtysum = deliveryqtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }

        public Object SaleBillFlow(string parameters)
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
                else if (field == "billcode")
                {
                    sb.AppendFormat(" and billcode ilike '%{0}%'", f);
                }
                else if (field == "customername")
                {

                    sb.AppendFormat(" and (customername ilike '%{0}%' or customercode ilike '%{0}%')", f);
                }
                else if (field == "employeename")
                {
                    sb.AppendFormat(" and (employeename ilike '%{0}%' or employeecode ilike '%{0}%')", f);
                }
                else if (field == "stockname")
                {
                    sb.AppendFormat(" and (stockname ilike '%{0}%' or stockcode ilike '%{0}%')", f);
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (productname ilike '%{0}%' or productcode ilike '%{0}%')", f);
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
            sborder.Append(" order by id,productcode ");


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as (select mvw_salebill.* from mvw_salebill "
                            + "inner join stock on mvw_salebill.stockid=stock.id "
                            + "inner join customer on mvw_salebill.customerid=customer.id "
                            + "inner join product on mvw_salebill.productid=product.id) ";

            string sql = cte
                            + "select * " +
                            "from cte " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = cte
                            + "select count(0) from cte "
                            + sb.ToString();

            string sqlSum = cte
                            + "select coalesce(sum(qty) filter (where billname='salebill'),0)-coalesce(sum(qty) filter (where billname='salebackbill'),0) as qty," +
                            "coalesce(sum(total) filter (where billname='salebill'),0)-coalesce(sum(total) filter (where billname='salebackbill'),0) as total," +
                            "coalesce(sum(discounttotal) filter (where billname='salebill'),0)-coalesce(sum(discounttotal) filter (where billname='salebackbill'),0) as discounttotal " +
                            "from cte " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);
            decimal discounttotalsum = Convert.ToDecimal(dtSum.Rows[0]["discounttotal"]);
            decimal taxtotalsum = discounttotalsum - totalsum;

            DataTable list = db.QueryTable(sql);

            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");

            return new
            {
                showcost = showcost,
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                discounttotalsum = discounttotalsum,
                taxtotalsum = taxtotalsum,
                data = list
            };
        }

        public Object InitMyselfSaleReport(string parameters)
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

        public Object MyselfSaleReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where employeeid = " + PluginContext.Current.Account.Id);

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
                                + " group by employeeid,employeecode,employeename) select count(0) from cte";

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

        public Object MyselfSaleReportComponent(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myselfsalereportconfig = new JObject();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myselfsalereportconfig = JsonConvert.DeserializeObject<JObject>(instance.Value<string>("storage"));
            }

            int period = myselfsalereportconfig["period"] == null ? 6 : myselfsalereportconfig.Value<int>("period");
            string charttype = myselfsalereportconfig["charttype"] == null ? "bar" : myselfsalereportconfig.Value<string>("charttype");

            List<string> x = new List<string>();
            List<decimal> serie = new List<decimal>();

            for (int i = 0; i < period; i++)
            {
                string startdate = DateTime.Now.AddMonths(-(period - i)).ToString("yyyy-MM") + "-01";
                string enddate = DateTime.Now.AddMonths(-(period - i - 1)).ToString("yyyy-MM") + "-01";

                string sql = string.Format("select coalesce(sum(case content->>'billname' when 'salebill' then (content->>'total')::decimal else -((content->>'total')::decimal) end),0) as total from bill " +
                                "where content->>'billname' in ('salebill','salebackbill') " +
                                "and (content->>'employeeid')::int={0} " +
                                "and (content->>'auditstatus')::int=1 " +
                                "and content->>'billdate'>='{1}' and content->>'billdate'<'{2}'",
                                PluginContext.Current.Account.Id, startdate, enddate);

                x.Add(Convert.ToDateTime(startdate).Month + "月");
                serie.Add(Convert.ToDecimal(db.Scalar(sql)));
            }

            //有超过1万的数字，单位就采用万元
            string unit = "y";
            if (serie.Count > 0 && serie.Max() >= 10000M)
            {
                unit = "wy";
                for (int i = 0; i < serie.Count; i++) serie[i] = serie[i] / 10000M;
            }
            return new { charttype = charttype, x = x, serie = serie, unit = unit, period = period.ToString() };
        }

        public Object MyselfSaleReportComponentSaveConfig(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");
            int period = ph.GetParameterValue<int>("period");
            string charttype = ph.GetParameterValue<string>("charttype");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            var instance = homepagecomponents.First(c => c.Value<string>("guid") == id);

            var myselfsalereportconfig = new JObject();
            myselfsalereportconfig.Add("period", period);
            myselfsalereportconfig.Add("charttype", charttype);
            instance["storage"] = JsonConvert.SerializeObject(myselfsalereportconfig);
            //employeeconfig.content["homepagecomponents"] = homepagecomponents;
            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

        public Object TimeSaleReportComponent(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myselfsalereportconfig = new JObject();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myselfsalereportconfig = JsonConvert.DeserializeObject<JObject>(instance.Value<string>("storage"));
            }

            int period = myselfsalereportconfig["period"] == null ? 6 : myselfsalereportconfig.Value<int>("period");
            string charttype = myselfsalereportconfig["charttype"] == null ? "line" : myselfsalereportconfig.Value<string>("charttype");

            List<string> x = new List<string>();
            List<decimal> serie = new List<decimal>();

            for (int i = 0; i < period; i++)
            {
                string startdate = DateTime.Now.AddMonths(-(period - i)).ToString("yyyy-MM") + "-01";
                string enddate = DateTime.Now.AddMonths(-(period - i - 1)).ToString("yyyy-MM") + "-01";

                string sql = string.Format("select coalesce(sum(case content->>'billname' when 'salebill' then (content->>'total')::decimal else -((content->>'total')::decimal) end),0) as total from bill " +
                                "where content->>'billname' in ('salebill','salebackbill') " +
                                "and (content->>'auditstatus')::int=1 " +
                                "and content->>'billdate'>='{0}' and content->>'billdate'<'{1}'",
                                startdate, enddate);

                x.Add(Convert.ToDateTime(startdate).Month + "月");
                serie.Add(Convert.ToDecimal(db.Scalar(sql)));
            }

            //有超过1万的数字，单位就采用万元
            string unit = "y";
            if (serie.Count > 0 && serie.Max() >= 10000M)
            {
                unit = "wy";
                for (int i = 0; i < serie.Count; i++) serie[i] = serie[i] / 10000M;
            }

            return new { charttype = charttype, x = x, serie = serie, unit = unit, period = period.ToString() };
        }

        public Object TimeSaleReportComponentSaveConfig(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");
            int period = ph.GetParameterValue<int>("period");
            string charttype = ph.GetParameterValue<string>("charttype");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            var instance = homepagecomponents.First(c => c.Value<string>("guid") == id);

            var myselfsalereportconfig = new JObject();
            myselfsalereportconfig.Add("period", period);
            myselfsalereportconfig.Add("charttype", charttype);
            instance["storage"] = JsonConvert.SerializeObject(myselfsalereportconfig);
            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

        public Object EmployeeSaleReportComponent(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myselfsalereportconfig = new JObject();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myselfsalereportconfig = JsonConvert.DeserializeObject<JObject>(instance.Value<string>("storage"));
            }

            int period = myselfsalereportconfig["period"] == null ? 6 : myselfsalereportconfig.Value<int>("period");
            string charttype = myselfsalereportconfig["charttype"] == null ? "bar" : myselfsalereportconfig.Value<string>("charttype");

            string startdate = DateTime.Now.AddMonths(-period).ToString("yyyy-MM") + "-01";
            string enddate = DateTime.Now.ToString("yyyy-MM") + "-01";

            string sql = string.Format("select content->>'employeeid' as employeeid, coalesce(sum(case content->>'billname' when 'salebill' then (content->>'total')::decimal else -((content->>'total')::decimal) end),0) as total from bill " +
                            "where content->>'billname' in ('salebill','salebackbill') " +
                            "and (content->>'auditstatus')::int=1 " +
                            "and content->>'billdate'>='{0}' and content->>'billdate'<'{1}' " +
                            "group by content->>'employeeid' order by total desc limit 10",
                            startdate, enddate);

            List<string> x = new List<string>();
            List<decimal> serie = new List<decimal>();
            List<JObject> pieserie = new List<JObject>();

            DataTable dt = db.QueryTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                string name = db.First("employee", Convert.ToInt32(row["employeeid"])).content.Value<string>("name");
                decimal value = Convert.ToDecimal(row["total"]);
                x.Add(name);
                serie.Add(value);

                var obj = new JObject();
                obj.Add("name", name);
                obj.Add("value", value);
                pieserie.Add(obj);
            }

            //有超过1万的数字，单位就采用万元
            string unit = "y";
            if (serie.Count>0 && serie.Max() >= 10000M)
            {
                unit = "wy";
                for (int i = 0; i < serie.Count; i++) serie[i] = serie[i] / 10000M;
                pieserie.ForEach(c => c["value"] = c.Value<decimal>("value") / 10000M);
            }

            return new { charttype = charttype, x = x, serie = serie, pieserie = pieserie, unit = unit, period = period.ToString() };
        }

        public Object EmployeeSaleReportComponentSaveConfig(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");
            int period = ph.GetParameterValue<int>("period");
            string charttype = ph.GetParameterValue<string>("charttype");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            var instance = homepagecomponents.First(c => c.Value<string>("guid") == id);

            var myselfsalereportconfig = new JObject();
            myselfsalereportconfig.Add("period", period);
            myselfsalereportconfig.Add("charttype", charttype);
            instance["storage"] = JsonConvert.SerializeObject(myselfsalereportconfig);
            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

        public Object ProductSaleReportComponent(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myselfsalereportconfig = new JObject();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myselfsalereportconfig = JsonConvert.DeserializeObject<JObject>(instance.Value<string>("storage"));
            }

            int period = myselfsalereportconfig["period"] == null ? 6 : myselfsalereportconfig.Value<int>("period");
            string charttype = myselfsalereportconfig["charttype"] == null ? "bar" : myselfsalereportconfig.Value<string>("charttype");

            string startdate = DateTime.Now.AddMonths(-period).ToString("yyyy-MM") + "-01";
            string enddate = DateTime.Now.ToString("yyyy-MM") + "-01";

            string sql = string.Format(" WITH cte AS (" +
                             "SELECT bill.id," +
                                "jsonb_array_elements(bill.content -> 'details'::text) AS detail," +
                                "bill.content " +
                               "FROM bill " +
                              "WHERE ((bill.content ->> 'billname'::text) = 'salebill'::text " +
                                "OR (bill.content ->> 'billname'::text) = 'salebackbill'::text) " +
                                "AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                                "and content->>'billdate'>='{0}' and content->>'billdate'<'{1}' " +
                            ") " +
                            "select detail->>'productid' as productid," +
                            "coalesce(sum(case content->>'billname' when 'salebill' then (detail->>'discounttotal')::decimal else -((detail->>'discounttotal')::decimal) end),0) as total " +
                            "from cte group by detail->>'productid' order by total desc limit 5",
                            startdate, enddate);


            List<string> x = new List<string>();
            List<decimal> serie = new List<decimal>();
            List<JObject> pieserie = new List<JObject>();

            DataTable dt = db.QueryTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                string name = db.First("product", Convert.ToInt32(row["productid"])).content.Value<string>("name");
                decimal value = Convert.ToDecimal(row["total"]);
                x.Add(name);
                serie.Add(value);

                var obj = new JObject();
                obj.Add("name", name);
                obj.Add("value", value);
                pieserie.Add(obj);
            }

            //有超过1万的数字，单位就采用万元
            string unit = "y";
            if (serie.Count > 0 && serie.Max() >= 10000M)
            {
                unit = "wy";
                for (int i = 0; i < serie.Count; i++) serie[i] = serie[i] / 10000M;
                pieserie.ForEach(c => c["value"] = c.Value<decimal>("value") / 10000M);
            }

            return new { charttype = charttype, x = x, serie = serie, pieserie = pieserie, unit = unit, period = period.ToString() };
        }

        public Object ProductSaleReportComponentSaveConfig(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");
            int period = ph.GetParameterValue<int>("period");
            string charttype = ph.GetParameterValue<string>("charttype");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            var instance = homepagecomponents.First(c => c.Value<string>("guid") == id);

            var myselfsalereportconfig = new JObject();
            myselfsalereportconfig.Add("period", period);
            myselfsalereportconfig.Add("charttype", charttype);
            instance["storage"] = JsonConvert.SerializeObject(myselfsalereportconfig);
            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

    }

}
