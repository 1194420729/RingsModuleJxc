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
using System.Web.Security;

namespace JxcStorage
{
    public class StorageReport : MarshalByRefObject
    {
        public Object InitStatusReport(string parameters)
        {
            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys,
                stocks = stocks
            };
        }

        public Object StorageStatusReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where 1=1 ");

            int stockid = Convert.ToInt32(myfilter["stockid"]);

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                }
                else if (field == "showzero")
                {
                    if (f.ToLower() == "false")
                    {
                        sb.AppendFormat(" and coalesce((content->'storage'->'{0}'->>'qty')::decimal,0)<>0", stockid);
                    }
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
                sborder.Append(" order by content->>'code' ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * from product " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) as cnt from product " +
                            sb.ToString();

            string sqlSum = "select coalesce(sum((content->'storage'->'" + stockid + "'->>'qty')::decimal),0) as qty,coalesce(sum((content->'storage'->'" + stockid + "'->>'total')::decimal),0) as total " +
                            "from product " +
                            sb.ToString();

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);

            var list = db.Where(sql);

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

        public Object InitStatusReportDetail(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int productid = ph.GetParameterValue<int>("productid");
            int stockid = ph.GetParameterValue<int>("stockid");

            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            string stockname = StringHelper.GetString("全部仓库");
            if (stockid > 0)
                stockname = db.First("stock", stockid).content.Value<string>("name");

            var product = db.First("product", productid);

            return new
            {
                showcost = showcost,
                digit = digit,
                stockname = stockname,
                product = product
            };
        }

        public Object StorageStatusReportDetail(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;
            int productid = Convert.ToInt32(myfilter["productid"]);

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'billname','')<>'' ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
                else if (field == "stockid")
                {
                    if (f != "0")
                        sb.AppendFormat(" and content->>'stockid'='{0}'", f);
                }
                else if (field == "productid")
                {
                    sb.AppendFormat(" and content->>'productid'='{0}'", f);
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
                sborder.Append(" order by content->>'billdate',id ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from storagedetail " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) as cnt " +
                            "from storagedetail " +
                            sb.ToString();
            string sqlQtySum = "select coalesce(sum((content->>'qty')::decimal),0) as qty from storagedetail " + sb.ToString();
            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total from storagedetail " + sb.ToString();
            string sqlLastQty = "select coalesce(sum((content->>'qty')::decimal),0) as qty from storagedetail where content->>'productid'='" + productid + "'" +
                (myfilter["stockid"] == "0" ? "" : (" and content->>'stockid'='" + myfilter["stockid"] + "'")) +
                " and (coalesce(content->>'billname','')='' or content->>'billdate'<'" + myfilter["startdate"] + "')";

            int recordcount = Convert.ToInt32(db.Scalar(sqlCount));
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal lastqty = Convert.ToDecimal(db.Scalar(sqlLastQty));

            var list1 = db.Where(sql);
            List<TableModel> list = new List<TableModel>();
            decimal storageqty = lastqty;
            foreach (var item in list1)
            {
                var bill = db.First("bill", item.content.Value<int>("billid"));
                string wldwname = "";
                if (bill.content.Value<int>("vendorid") > 0) wldwname = db.First("vendor", bill.content.Value<int>("vendorid")).content.Value<string>("name");
                else if (bill.content.Value<int>("customerid") > 0) wldwname = db.First("customer", bill.content.Value<int>("customerid")).content.Value<string>("name");

                decimal total = item.content.Value<decimal>("total");
                decimal qty = item.content.Value<decimal>("qty");
                storageqty += qty;

                bill.content.Add("makername", db.First("employee", bill.content.Value<int>("makerid")).content.Value<string>("name"));
                bill.content.Add("wldwname", wldwname);
                bill.content.Add("employeename", db.First("employee", bill.content.Value<int>("employeeid")).content.Value<string>("name"));
                bill.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                bill.content.Add("qty", qty);
                bill.content.Add("storageqty", storageqty);
                bill.content.Add("costtotal", total);
                bill.content.Add("costprice", total / qty);

                list.Add(bill);

            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                lastqty = lastqty,
                data = list
            };
        }

        public Object InitDistributeReport(string parameters)
        {
            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys
            };
        }

        public Object StorageDistributeReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int stockid = 0;

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();

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
                else if (field == "showzero")
                {
                    if (f.ToLower() == "false")
                    {
                        sb.AppendFormat(" and coalesce((content->'storage'->'{0}'->>'qty')::decimal,0)<>0", stockid);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
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
                sborder.Append(" order by content->>'code' ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query("product", sb.ToString(), sborder.ToString(), out recordcount);
            foreach (var item in list)
            {
                if (item.content["categoryid"] == null) continue;
                var category = db.FirstOrDefault("select * from category where id=" + item.content.Value<int>("categoryid"));
                if (category == null) continue;
                item.content.Add("category", category.content);
            }

            decimal qtysum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'storage'->'" + stockid + "'->>'qty','0')::decimal),0) as qty from product where 1=1 " + sb.ToString()));
            decimal totalsum = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'storage'->'" + stockid + "'->>'total','0')::decimal),0) as total from product where 1=1 " + sb.ToString()));

            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

            Dictionary<int, decimal> qtysums = new Dictionary<int, decimal>();
            Dictionary<int, decimal> totalsums = new Dictionary<int, decimal>();
            foreach (var stock in stocks)
            {
                decimal stockqty = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'storage'->'" + stock.id + "'->>'qty','0')::decimal),0) as qty from product where 1=1 " + sb.ToString()));
                qtysums.Add(stock.id, stockqty);

                decimal stocktotal = Convert.ToDecimal(db.Scalar("select coalesce(sum(coalesce(content->'storage'->'" + stock.id + "'->>'total','0')::decimal),0) as total from product where 1=1 " + sb.ToString()));
                totalsums.Add(stock.id, stocktotal);
            }


            return new
            {
                resulttotal = recordcount,
                data = list,
                qtysum = qtysum,
                totalsum = totalsum,
                stocks = stocks,
                qtysums = qtysums,
                totalsums = totalsums
            };
        }

        public Object InitVirtualStatusReport(string parameters)
        {
            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

            bool strictordermanage = option.content.Value<bool>("strictordermanage");

            return new
            {
                strictordermanage = strictordermanage,
                showcost = showcost,
                digit = digit,
                categorys = categorys,
                stocks = stocks
            };
        }

        public Object VirtualStorageStatusReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where 1=1 ");

            int stockid = Convert.ToInt32(myfilter["stockid"]);

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                }
                //else if (field == "showzero")
                //{
                //    if (f.ToLower() == "false")
                //    {
                //        sb.AppendFormat(" and coalesce((content->'storage'->'{0}'->>'qty')::decimal,0)<>0", stockid);
                //    }
                //}
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
                sborder.Append(" order by content->>'code' ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * from product " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) as cnt from product " +
                            sb.ToString();

            string sqlSum = "select coalesce(sum((content->'virtualstorage'->'" + stockid
                + "'->>'qty')::decimal),0) as virtualqty,coalesce(sum((content->'storage'->'" + stockid
                + "'->>'qty')::decimal),0) as qty " +
                            "from product " +
                            sb.ToString();
            string sqlPurchaseUndelivery = "WITH cte AS (" +
                         "SELECT jsonb_array_elements(bill.content -> 'details'::text) AS detail,(bill.content->>'stockid')::int as stockid " +
                         "FROM bill " +
                         "WHERE (bill.content ->> 'billname'::text) = 'purchaseorder'::text AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                        ") " +
                        "select coalesce(sum((cte.detail->>'qty')::decimal),0)-coalesce(sum((cte.detail->>'deliveryqty')::decimal),0) as undeliveryqty " +
                        "from cte inner join (select id from product "+sb.ToString()+") as p on (cte.detail->>'productid')::int=p.id " + (stockid == 0 ? "" : ("where cte.stockid=" + stockid));
            string sqlSaleUndelivery = "WITH cte AS (" +
                         "SELECT jsonb_array_elements(bill.content -> 'details'::text) AS detail,(bill.content->>'stockid')::int as stockid " +
                         "FROM bill " +
                         "WHERE (bill.content ->> 'billname'::text) = 'saleorder'::text AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                        ") " +
                        "select coalesce(sum((cte.detail->>'qty')::decimal),0)-coalesce(sum((cte.detail->>'deliveryqty')::decimal),0) as undeliveryqty " +
                        "from cte inner join (select id from product " + sb.ToString() + ") as p on (cte.detail->>'productid')::int=p.id  " + (stockid == 0 ? "" : ("where cte.stockid=" + stockid));

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal virtualqtysum = Convert.ToDecimal(dtSum.Rows[0]["virtualqty"]);
            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal purchaseundeliverysum = Convert.ToDecimal(db.Scalar(sqlPurchaseUndelivery));
            decimal saleundeliverysum = Convert.ToDecimal(db.Scalar(sqlSaleUndelivery));

            var list = db.Where(sql);
            foreach (var item in list)
            {
                sql = "WITH cte AS (" +
                         "SELECT jsonb_array_elements(bill.content -> 'details'::text) AS detail,(bill.content->>'stockid')::int as stockid " +
                         "FROM bill " +
                         "WHERE (bill.content ->> 'billname'::text) = 'purchaseorder'::text AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                        ") " +
                        "select coalesce(sum((cte.detail->>'qty')::decimal),0)-coalesce(sum((cte.detail->>'deliveryqty')::decimal),0) as undeliveryqty " +
                        "from cte " +
                        "where (cte.detail->>'productid')::int=" + item.id
                        + (stockid == 0 ? "" : (" and cte.stockid=" + stockid));
                item.content["purchaseundelivery"] = Convert.ToDecimal(db.Scalar(sql));

                sql = "WITH cte AS (" +
                         "SELECT jsonb_array_elements(bill.content -> 'details'::text) AS detail,(bill.content->>'stockid')::int as stockid " +
                         "FROM bill " +
                         "WHERE (bill.content ->> 'billname'::text) = 'saleorder'::text AND ((bill.content ->> 'auditstatus'::text)::integer) = 1 " +
                        ") " +
                        "select coalesce(sum((cte.detail->>'qty')::decimal),0)-coalesce(sum((cte.detail->>'deliveryqty')::decimal),0) as undeliveryqty " +
                        "from cte " +
                        "where (cte.detail->>'productid')::int=" + item.id
                        + (stockid == 0 ? "" : (" and cte.stockid=" + stockid));
                item.content["saleundelivery"] = Convert.ToDecimal(db.Scalar(sql));
            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                virtualqtysum = virtualqtysum,
                purchaseundeliverysum = purchaseundeliverysum,
                saleundeliverysum = saleundeliverysum,
                data = list
            };
        }

        public Object InitVirtualStatusReportDetail(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int productid = ph.GetParameterValue<int>("productid");
            int stockid = ph.GetParameterValue<int>("stockid");

            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            string stockname = StringHelper.GetString("全部仓库");
            if (stockid > 0)
                stockname = db.First("stock", stockid).content.Value<string>("name");

            var product = db.First("product", productid);

            return new
            {
                showcost = showcost,
                digit = digit,
                stockname = stockname,
                product = product
            };
        }

        public Object VirtualStorageStatusReportDetail(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;
            int productid = Convert.ToInt32(myfilter["productid"]);

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'billname','')<>'' ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
                else if (field == "stockid")
                {
                    if (f != "0")
                        sb.AppendFormat(" and content->>'stockid'='{0}'", f);
                }
                else if (field == "productid")
                {
                    sb.AppendFormat(" and content->>'productid'='{0}'", f);
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
                sborder.Append(" order by content->>'billdate',id ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * " +
                            "from virtualstoragedetail " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = "select count(*) as cnt " +
                            "from virtualstoragedetail " +
                            sb.ToString();
            string sqlQtySum = "select coalesce(sum((content->>'qty')::decimal),0) as qty from virtualstoragedetail " + sb.ToString();
            string sqlTotalSum = "select coalesce(sum((content->>'total')::decimal),0) as total from virtualstoragedetail " + sb.ToString();
            string sqlLastQty = "select coalesce(sum((content->>'qty')::decimal),0) as qty from virtualstoragedetail where content->>'productid'='" + productid + "'" +
                (myfilter["stockid"] == "0" ? "" : (" and content->>'stockid'='" + myfilter["stockid"] + "'")) +
                " and (coalesce(content->>'billname','')='' or content->>'billdate'<'" + myfilter["startdate"] + "')";

            int recordcount = Convert.ToInt32(db.Scalar(sqlCount));
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));
            decimal lastqty = Convert.ToDecimal(db.Scalar(sqlLastQty));

            var list1 = db.Where(sql);
            List<TableModel> list = new List<TableModel>();
            decimal storageqty = lastqty;
            foreach (var item in list1)
            {
                var bill = db.First("bill", item.content.Value<int>("billid"));
                string wldwname = "";
                if (bill.content.Value<int>("vendorid") > 0) wldwname = db.First("vendor", bill.content.Value<int>("vendorid")).content.Value<string>("name");
                else if (bill.content.Value<int>("customerid") > 0) wldwname = db.First("customer", bill.content.Value<int>("customerid")).content.Value<string>("name");

                decimal total = item.content.Value<decimal>("total");
                decimal qty = item.content.Value<decimal>("qty");
                storageqty += qty;

                bill.content.Add("makername", db.First("employee", bill.content.Value<int>("makerid")).content.Value<string>("name"));
                bill.content.Add("wldwname", wldwname);
                bill.content.Add("employeename", db.First("employee", bill.content.Value<int>("employeeid")).content.Value<string>("name"));
                bill.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                bill.content.Add("qty", qty);
                bill.content.Add("storageqty", storageqty);
                bill.content.Add("costtotal", total);
                bill.content.Add("costprice", total / qty);

                list.Add(bill);

            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                lastqty = lastqty,
                data = list
            };
        }

        public Object InitStorageInOutReport(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);

            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");
            var employees = db.Where("select * from employee where id>1 and coalesce(content->>'stop','')='' order by content->>'code'");

            return new
            {
                showcost = showcost,
                digit = digit,
                stocks = stocks,
                employees = employees
            };
        }

        public Object StorageInOutReport(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            DBHelper db = new DBHelper();
            StringBuilder sb = new StringBuilder();
            sb.Append(" where coalesce(content->>'billname','')<>'' ");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field == "startdate")
                {
                    sb.AppendFormat(" and content->>'billdate'>='{0}'", f);
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'productid')::int in ({0})", ids);
                }
                else if (field == "employeeid")
                {
                    if (f != "0")
                    {
                        DataTable dt = db.QueryTable("select id from bill where (content->>'employeeid')::int = " + f);
                        string ids = dt.GetIds();
                        sb.AppendFormat(" and (content->>'billid')::int in ({0})", ids);
                    }
                }
                else if (field == "stockid")
                {
                    if (f != "0")
                        sb.AppendFormat(" and content->>'stockid'='{0}'", f);
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
                sborder.Append(" order by content->>'billdate',id ");
            }


            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as (select storagedetail.* from storagedetail "+
                            "inner join product on (storagedetail.content->>'productid')::int=product.id "+
                            "inner join stock on (storagedetail.content->>'stockid')::int=stock.id) ";

            string sql = cte+"select * " +
                            "from cte " +
                            sb.ToString() +
                            sborder.ToString();

            string sqlCount = cte+"select count(*) as cnt " +
                            "from cte " +
                            sb.ToString();
            string sqlQtySum = cte+"select coalesce(sum((content->>'qty')::decimal),0) as qty from cte " + sb.ToString();
            string sqlTotalSum = cte+"select coalesce(sum((content->>'total')::decimal),0) as total from cte " + sb.ToString();

            int recordcount = Convert.ToInt32(db.Scalar(sqlCount));
            decimal qtysum = Convert.ToDecimal(db.Scalar(sqlQtySum));
            decimal totalsum = Convert.ToDecimal(db.Scalar(sqlTotalSum));

            var list1 = db.Where(sql);
            List<TableModel> list = new List<TableModel>();
            foreach (var item in list1)
            {
                var bill = db.First("bill", item.content.Value<int>("billid"));
                string wldwname = "";
                if (bill.content.Value<int>("vendorid") > 0) wldwname = db.First("vendor", bill.content.Value<int>("vendorid")).content.Value<string>("name");
                else if (bill.content.Value<int>("customerid") > 0) wldwname = db.First("customer", bill.content.Value<int>("customerid")).content.Value<string>("name");

                decimal total = item.content.Value<decimal>("total");
                decimal qty = item.content.Value<decimal>("qty");

                bill.content.Add("makername", db.First("employee", bill.content.Value<int>("makerid")).content.Value<string>("name"));
                bill.content.Add("wldwname", wldwname);
                bill.content.Add("productname", db.First("product", item.content.Value<int>("productid")).content.Value<string>("name"));
                bill.content.Add("employeename", db.First("employee", bill.content.Value<int>("employeeid")).content.Value<string>("name"));
                bill.content.Add("stockname", db.First("stock", item.content.Value<int>("stockid")).content.Value<string>("name"));
                bill.content.Add("qty", qty);
                bill.content.Add("costtotal", total);
                bill.content.Add("costprice", total / qty);

                list.Add(bill);

            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

        public Object InitStatusHistoryReport(string parameters)
        {
            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys,
                stocks = stocks
            };
        }

        public Object StorageStatusHistoryReport(string parameters)
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

                if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids1 = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids1.Add(Convert.ToInt32(f));
                        string idsfilter = ids1.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);

                        DataTable dt = db.QueryTable(string.Format("select id from product where (content->>'categoryid')::int in ({0})", idsfilter));
                        string ids = dt.GetIds();
                        sb.AppendFormat(" and (content->>'productid')::int in ({0})", ids);
                    }
                }
                else if (field == "productname")
                {
                    DataTable dt = db.QueryTable("select id from product where content->>'name' ilike '%" + f + "%' or content->>'code' ilike '%" + f + "%'");
                    string ids = dt.GetIds();
                    sb.AppendFormat(" and (content->>'productid')::int in ({0})", ids);
                }
                else if (field == "stockid")
                {
                    if (f != "0")
                    {
                        sb.AppendFormat(" and (content->>'stockid')::int={0}", f);
                    }
                }
                else if (field == "enddate")
                {
                    sb.AppendFormat(" and content->>'billdate'<='{0}'", f);
                }
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by product.content->>'code' ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string cte = "with cte as " +
                        "( " +
                        "select (content->>'productid')::int as productid,(content->>'stockid')::int as stockid,(content->>'qty')::decimal as qty, " +
                        "	(content->>'total')::decimal as total " +
                        "from storagedetail " + sb.ToString() +
                        ") ";

            string sql = cte +
                        "select cte.productid,product.content->>'code' as code,product.content->>'name' as name, " +
                        "product.content->>'standard' as standard,product.content->>'type' as type, " +
                        "product.content->>'unit' as unit,product.content->>'barcode' as barcode,product.content->>'area' as area, " +
                        "sum(qty) as qty,sum(total) as total  " +
                        "from cte inner join product on cte.productid=product.id " +
                        "group by cte.productid,product.content->>'code',product.content->>'name', " +
                        "	product.content->>'standard',product.content->>'type', " +
                        "	product.content->>'unit',product.content->>'barcode',product.content->>'area' " +
                        sborder.ToString();

            string sqlCount = "select count(*) as cnt from (" +
                            cte +
                        "select cte.productid " +
                        "from cte inner join product on cte.productid=product.id " +
                        "group by cte.productid " +
                        ") as t";


            string sqlSum = cte+"select coalesce(sum(cte.qty),0) as qty, " +
                        "	coalesce(sum(cte.total),0) as total " +
                        "from cte inner join product on cte.productid=product.id ";

            int recordcount = db.Count(sqlCount);
            DataTable dtSum = db.QueryTable(sqlSum);

            decimal qtysum = Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal totalsum = Convert.ToDecimal(dtSum.Rows[0]["total"]);

            DataTable list = db.QueryTable(sql);
            list.Columns.Add("price", typeof(decimal));
            foreach (DataRow item in list.Rows)
            {
                if (Convert.ToDecimal(item["qty"]) != decimal.Zero)
                {
                    item["price"] = Convert.ToDecimal(item["total"]) / Convert.ToDecimal(item["qty"]);
                }
            }

            return new
            {
                resulttotal = recordcount,
                qtysum = qtysum,
                totalsum = totalsum,
                data = list
            };
        }

        public Object InitSummaryReport(string parameters)
        {
            DBHelper db = new DBHelper();
            bool showcost = PluginContext.Current.Account.IsAllowed("showcost");
            var option = db.First("select * from option");
            int digit = option.content["digit"] == null ? 2 : option.content.Value<int>("digit");

            var categorys = CategoryHelper.GetCategoryTreeData("product");
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')='' order by content->>'code'");

            var inouttypes = db.Where("select * from inouttype where coalesce(content->>'stop','')=''");

            List<object> changeitems = new List<object>();
            changeitems.Add(new { id = 1, name = StringHelper.GetString("采购入库"), visible = true, inout = "入库" });
            changeitems.Add(new { id = 2, name = StringHelper.GetString("销售退货"), visible = true, inout = "入库" });
            changeitems.Add(new { id = 3, name = StringHelper.GetString("调拨入库"), visible = true, inout = "入库" });

            foreach (var item in inouttypes.Where(c => c.content.Value<string>("direction") == "入库").ToList())
            {
                changeitems.Add(new { id = 100 + item.id, name = item.content.Value<string>("name"), visible = true, inout = "入库" });
            }

            changeitems.Add(new { id = 4, name = StringHelper.GetString("销售出库"), visible = true, inout = "出库" });
            changeitems.Add(new { id = 5, name = StringHelper.GetString("采购退货"), visible = true, inout = "出库" });
            changeitems.Add(new { id = 6, name = StringHelper.GetString("调拨出库"), visible = true, inout = "出库" });

            foreach (var item in inouttypes.Where(c => c.content.Value<string>("direction") == "出库").ToList())
            {
                changeitems.Add(new { id = 100 + item.id, name = item.content.Value<string>("name"), visible = true, inout = "出库" });
            }

            return new
            {
                showcost = showcost,
                digit = digit,
                categorys = categorys,
                stocks = stocks,
                changeitems = changeitems
            };
        }

        public Object StorageSummaryReport(string parameters)
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

                if (field == "categoryid")
                {
                    if (f != "0")
                    {
                        var ids = CategoryHelper.GetChildrenIds(Convert.ToInt32(f));
                        ids.Add(Convert.ToInt32(f));
                        string idsfilter = ids.Select(c => c.ToString()).Aggregate((a, b) => a + "," + b);

                        sb.AppendFormat(" and (content->>'categoryid')::int in ({0})", idsfilter);
                    }
                }
                else if (field == "productname")
                {
                    sb.AppendFormat(" and (content->>'name' ilike '%{0}%' or content->>'code' ilike '%{0}%')", f);
                }
            }

            int stockid = Convert.ToInt32(myfilter["stockid"]);
            string startdate = myfilter["startdate"];
            string enddate = myfilter["enddate"];

            if (myfilter["shownochange"] == "false")
            {
                DataTable dt = db.QueryTable(string.Format("select (content->>'productid')::int as id from storagedetail where content->>'billdate'>='{0}' and content->>'billdate'<='{1}' " +
                    (stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid)) +
                    " group by content->>'productid'", startdate, enddate));
                string ids = dt.GetIds();
                sb.AppendFormat(" and id in ({0})", ids);
            }

            StringBuilder sborder = new StringBuilder();
            sborder.Append(" order by content->>'code' ");

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            string sql = "select * from product " +
                        sb.ToString() +
                        sborder.ToString();

            string sqlCount = "select count(*) as cnt from product " + sb.ToString();
            int recordcount = db.Count(sqlCount);

            var list = db.Where(sql);
            string productids = list.GetIds();

            string sqlSum = string.Format("select coalesce(sum((content->>'qty')::decimal),0) as qty,"+
                "coalesce(sum((content->>'total')::decimal),0) as total "+
                "from storagedetail where (content->>'productid')::int in ({0}) " +
                (stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid)) +
                " and content->>'billdate'<'{1}'",
                productids, startdate);

            DataTable dtSum = db.QueryTable(sqlSum);
             
            decimal lastqtysum = dtSum.Rows.Count == 0 ? decimal.Zero : Convert.ToDecimal(dtSum.Rows[0]["qty"]);
            decimal lasttotalsum = dtSum.Rows.Count == 0 ? decimal.Zero : Convert.ToDecimal(dtSum.Rows[0]["total"]);

            string sqlSum2 = string.Format("select coalesce(sum((content->>'qty')::decimal),0) as qty," +
                "coalesce(sum((content->>'total')::decimal),0) as total " +
                "from storagedetail where (content->>'productid')::int in ({0}) " +
                (stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid)) +
                " and content->>'billdate'<='{1}'",
                productids, enddate);

            DataTable dtSum2 = db.QueryTable(sqlSum2);

            decimal nowqtysum = dtSum2.Rows.Count == 0 ? decimal.Zero : Convert.ToDecimal(dtSum2.Rows[0]["qty"]);
            decimal nowtotalsum = dtSum2.Rows.Count == 0 ? decimal.Zero : Convert.ToDecimal(dtSum2.Rows[0]["total"]);
            
            string sqlDetail = string.Format("select * from storagedetail where (content->>'productid')::int in ({0}) " +
                (stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid)) +
                " and content->>'billdate'>='{1}' and content->>'billdate'<='{2}'",
                productids, startdate, enddate);
            var storagedetails = db.Where(sqlDetail);

            foreach (var item in list)
            {
                //上期数量
                sql = string.Format("select " +
                        "coalesce(sum((content->>'qty')::decimal),0) as qty," +
                        "coalesce(sum((content->>'total')::decimal),0) as total " +
                        "from storagedetail " +
                        "where (content->>'productid')::int={0} and content->>'billdate'<='{1}' {2} " +
                        "group by content->>'productid'", item.id, Convert.ToDateTime(startdate).AddDays(-1).ToString("yyyy-MM-dd"),
                        stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid));
                DataTable dtLast = db.QueryTable(sql);
                if (dtLast.Rows.Count > 0)
                {
                    item.content["lastqty"] = Convert.ToDecimal(dtLast.Rows[0]["qty"]);
                    item.content["lasttotal"] = Convert.ToDecimal(dtLast.Rows[0]["total"]);
                }

                //本期数量
                sql = string.Format("select " +
                        "coalesce(sum((content->>'qty')::decimal),0) as qty," +
                        "coalesce(sum((content->>'total')::decimal),0) as total " +
                        "from storagedetail " +
                        "where (content->>'productid')::int={0} and content->>'billdate'<='{1}' {2} " +
                        "group by content->>'productid'", item.id, enddate,
                        stockid == 0 ? "" : (" and (content->>'stockid')::int=" + stockid));
                DataTable dtNow = db.QueryTable(sql);
                if (dtNow.Rows.Count > 0)
                {
                    item.content["nowqty"] = Convert.ToDecimal(dtNow.Rows[0]["qty"]);
                    item.content["nowtotal"] = Convert.ToDecimal(dtNow.Rows[0]["total"]);
                }

                foreach (var detail in storagedetails)
                {
                    if (detail.content.Value<int>("productid") != item.id) continue;

                    string billname = detail.content.Value<string>("billname");
                    if (billname == "purchasebill")
                    {
                        item.content["qty1"] = item.content.Value<decimal>("qty1") + detail.content.Value<decimal>("qty");
                        item.content["total1"] = item.content.Value<decimal>("total1") + detail.content.Value<decimal>("total");
                    }
                    else if (billname == "salebackbill")
                    {
                        item.content["qty2"] = item.content.Value<decimal>("qty2") + detail.content.Value<decimal>("qty");
                        item.content["total2"] = item.content.Value<decimal>("total2") + detail.content.Value<decimal>("total");
                    }
                    else if (billname == "stockmovebill" && detail.content.Value<decimal>("qty") > decimal.Zero)
                    {
                        item.content["qty3"] = item.content.Value<decimal>("qty3") + detail.content.Value<decimal>("qty");
                        item.content["total3"] = item.content.Value<decimal>("total3") + detail.content.Value<decimal>("total");
                    }
                    else if (billname == "stockinbill")
                    {
                        var bill = db.First("bill", detail.content.Value<int>("billid"));
                        int ls = bill.content.Value<int>("inouttypeid") + 100;
                        item.content["qty" + ls] = item.content.Value<decimal>("qty" + ls) + detail.content.Value<decimal>("qty");
                        item.content["total" + ls] = item.content.Value<decimal>("total" + ls) + detail.content.Value<decimal>("total");
                    }
                    else if (billname == "salebill")
                    {
                        item.content["qty4"] = item.content.Value<decimal>("qty4") - detail.content.Value<decimal>("qty");
                        item.content["total4"] = item.content.Value<decimal>("total4") - detail.content.Value<decimal>("total");
                    }
                    else if (billname == "purchasebackbill")
                    {
                        item.content["qty5"] = item.content.Value<decimal>("qty5") - detail.content.Value<decimal>("qty");
                        item.content["total5"] = item.content.Value<decimal>("total5") - detail.content.Value<decimal>("total");
                    }
                    else if (billname == "stockmovebill" && detail.content.Value<decimal>("qty") < decimal.Zero)
                    {
                        item.content["qty6"] = item.content.Value<decimal>("qty6") - detail.content.Value<decimal>("qty");
                        item.content["total6"] = item.content.Value<decimal>("total6") - detail.content.Value<decimal>("total");
                    }
                    else if (billname == "stockoutbill")
                    {
                        var bill = db.First("bill", detail.content.Value<int>("billid"));
                        int ls = bill.content.Value<int>("inouttypeid") + 100;
                        item.content["qty" + ls] = item.content.Value<decimal>("qty" + ls) - detail.content.Value<decimal>("qty");
                        item.content["total" + ls] = item.content.Value<decimal>("total" + ls) - detail.content.Value<decimal>("total");
                    }
                }
            }

            return new
            {
                resulttotal = recordcount,
                data = list,
                lastqtysum = lastqtysum,
                lasttotalsum = lasttotalsum,
                nowqtysum=nowqtysum,
                nowtotalsum=nowtotalsum
            };
        }

    }
}
