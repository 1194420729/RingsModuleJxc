using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public interface IYearBalance
    {
        void Exec(string ztname, DateTime? balancedate);
    }

    public class LocalYearBalance : IYearBalance
    {
        private DateTime? balancedate;

        public void Exec(string ztname, DateTime? balancedate)
        {
            this.balancedate = balancedate;

            #region 获取当前数据库名，以及指向postgres数据库的连接字符串
            DataContext dc = new DataContext(PluginContext.Current.Account.ApplicationId);
            string postgresconnstr = dc.PostgresConnectionString;
            string database = "";
            //for example:"Server=127.0.0.1;Port=5432;uid=postgres;pwd=password;Database=dbname;Encoding=UNICODE"
            string[] ss = postgresconnstr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in ss)
            {
                if (s.ToLower().StartsWith("database"))
                {
                    postgresconnstr = postgresconnstr.Replace(s, "Database=postgres");
                    database = s.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                    break;
                }
            }
            #endregion

            #region 杀死主账套数据库所有会话
            this.ExecNoneQuery(postgresconnstr,
                "SELECT pg_terminate_backend(pid) " +
                "FROM pg_stat_activity " +
                "WHERE  pid <> pg_backend_pid() " +//don't kill my own connection!
                    "AND datname = '" + database + "'");//don't kill the connections to other databases

            #endregion

            #region 复制数据库
            string newdatabase = database + DateTime.Now.ToString("yyMMddHHmmss");
            this.ExecNoneQuery(postgresconnstr, "create database " + newdatabase + " template " + database);
            #endregion

            #region 结转
            Carry(dc.ApplicationId);
            #endregion

            #region 新增账套信息
            string centralconnectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            string appid = Guid.NewGuid().ToString().ToUpper();
            var rootcorporation =new ZtHelper().GetRootCorporation();
            string name = rootcorporation.content.Value<string>("corporationname") + "_" + ztname;
            JObject content = new JObject();
            content.Add("name", ztname);
            content.Add("default", false);
            content.Add("parentid", dc.CorporationId);
            content.Add("rootid", rootcorporation.id);
            string sql = string.Format("INSERT INTO corporation(applicationid, name, content, connectionstring) "
                + "VALUES ('{0}','{1}','{2}','{3}')",
                appid, name, JsonConvert.SerializeObject(content), postgresconnstr.Replace("Database=postgres", "Database=" + newdatabase));
            this.ExecNoneQuery(centralconnectionstr, sql);
            #endregion

            #region 新增的账套，添加数据库角色
            AddRole(appid);
            #endregion

        }

        private void Carry(string applicationid)
        {
            DataContext dc = new DataContext(applicationid);

            using (DBHelper db = new DBHelper(dc, true))
            {
                #region 删除业务单据
                if (this.balancedate.HasValue)
                {
                    db.ExcuteNoneQuery("delete from bill where content->>'billdate'<='"
                        + balancedate.Value.ToString("yyyy-MM-dd")
                        + "' and (content->>'auditstatus')::int>0");
                }
                else
                {
                    db.ExcuteNoneQuery("delete from bill where (content->>'auditstatus')::int>0");
                }
                #endregion

                #region 结余转为期初
                if (balancedate.HasValue)
                {
                    //库存 
                    db.ExcuteNoneQuery("update product set content=content-'initstorage'");


                    //计算历史库存
                    string sql = "with cte as " +
                        "( " +
                        "select (content->>'productid')::int as productid,(content->>'stockid')::int as stockid,(content->>'qty')::decimal as qty, " +
                        "	(content->>'total')::decimal as total " +
                        "from storagedetail where content->>'billdate'<='" + balancedate.Value.ToString("yyyy-MM-dd") + "' " +
                        ") " +
                        "select cte.productid,cte.stockid, " +
                        "sum(qty) as qty,sum(total) as total " +
                        "from cte " +
                        "group by cte.productid,cte.stockid ";
                    DataTable historystorage = db.QueryTable(sql);
                    Dictionary<int, JObject> cache = new Dictionary<int, JObject>();
                    Dictionary<int,decimal> qtycache=new Dictionary<int,decimal>();
                    Dictionary<int,decimal> totalcache=new Dictionary<int,decimal>();
                    foreach (DataRow row in historystorage.Rows)
                    {
                        //"storage": {"0": {"qty": 100.0, "price": 100.0, "total": 10000.0}, "1": {"qty": 100.0, "price": 100.0, "total": 10000.0}}
                        int productid = Convert.ToInt32(row["productid"]);
                        int stockid = Convert.ToInt32(row["stockid"]);
                        decimal qty = Convert.ToDecimal(row["qty"]);
                        decimal total = Convert.ToDecimal(row["total"]);
                        decimal price = qty == decimal.Zero ? decimal.Zero : (total / qty);
                        if (!cache.ContainsKey(productid))
                        { 
                            cache.Add(productid, new JObject());
                        }
                        if (!qtycache.ContainsKey(productid))
                        { 
                            qtycache.Add(productid, decimal.Zero);
                        }
                        if (!totalcache.ContainsKey(productid))
                        { 
                            totalcache.Add(productid, decimal.Zero);
                        }

                        JObject detail = new JObject();
                        detail.Add("qty", qty);
                        detail.Add("total", total);
                        detail.Add("price", price);
                        cache[productid].Add(stockid.ToString(), detail);
                        qtycache[productid]=qtycache[productid]+qty;
                        totalcache[productid]=totalcache[productid]+qty;
                    }

                    foreach (int key in cache.Keys)
                    {
                        JObject detail = new JObject();
                        detail.Add("qty", qtycache[key]);
                        detail.Add("total", totalcache[key]);
                        decimal price = qtycache[key] == decimal.Zero ? decimal.Zero : (totalcache[key] / qtycache[key]);
                        detail.Add("price", price);
                        cache[key].Add("0", detail);

                        db.ExcuteNoneQuery("update product set content=jsonb_set(content,'{initstorage}','"
                            +JsonConvert.SerializeObject(cache[key])+"'::jsonb,true) where id="+key);
                    }

                    //重新排列库存明细
                    var storagedetails= db.Where("select * from storagedetail where content->>'billdate'>'"
                        + balancedate.Value.ToString("yyyy-MM-dd")+"'");
                    var virtualstoragedetails = db.Where("select * from virtualstoragedetail where content->>'billdate'>'"
                        + balancedate.Value.ToString("yyyy-MM-dd") + "'");
                    db.Truncate("storagedetail");
                    db.Truncate("virtualstoragedetail");

                    foreach (DataRow row in historystorage.Rows)
                    {
                        int productid = Convert.ToInt32(row["productid"]);
                        int stockid = Convert.ToInt32(row["stockid"]);
                        decimal qty = Convert.ToDecimal(row["qty"]);
                        decimal total = Convert.ToDecimal(row["total"]);
                        decimal price = qty == decimal.Zero ? decimal.Zero : (total / qty);
                        JObject storagedetail = new JObject();
                        storagedetail.Add("productid",productid);
                        storagedetail.Add("stockid", stockid);
                        storagedetail.Add("leftqty", qty);
                        storagedetail.Add("lefttotal", total);
                        storagedetail.Add("qty", qty);
                        storagedetail.Add("total", total);
                        storagedetail.Add("price", price);
                        storagedetail.Add("billdate", balancedate.Value.AddDays(1).ToString("yyyy-MM-dd"));
                        storagedetail.Add("createtime", DateTime.Now);

                        db.ExcuteNoneQuery("insert into storagedetail (content) values ('"+JsonConvert.SerializeObject(storagedetail)+"')");
                        db.ExcuteNoneQuery("insert into virtualstoragedetail (content) values ('" + JsonConvert.SerializeObject(storagedetail) + "')");
                    }

                    foreach (var storagedetail in storagedetails)
                    {
                        db.ExcuteNoneQuery("insert into storagedetail (content) values ('" + JsonConvert.SerializeObject(storagedetail) + "')");
                    }
                    foreach (var storagedetail in virtualstoragedetails)
                    {
                        db.ExcuteNoneQuery("insert into virtualstoragedetail (content) values ('" + JsonConvert.SerializeObject(storagedetail) + "')");
                    }

                    
                    //应收
                    db.ExcuteNoneQuery("update customer set content=content-'initreceivable'");                     
                    sql = "with cte as " +
                       "( " +
                       "select (content->>'customerid')::int as customerid,(content->>'total')::decimal as total " +
                       "from receivabledetail where content->>'billdate'<='" + balancedate.Value.ToString("yyyy-MM-dd") + "' " +
                       ") " +
                       "select cte.customerid,sum(total) as total " +
                       "from cte " +
                       "group by cte.customerid ";
                    DataTable historyreceivable = db.QueryTable(sql);
                    var receivabledetails = db.Where("select * from receivabledetail where content->>'billdate'>'"
                       + balancedate.Value.ToString("yyyy-MM-dd") + "'");
                    db.Truncate("receivabledetail");

                    foreach (DataRow row in historyreceivable.Rows)
                    {
                        int customerid = Convert.ToInt32(row["customerid"]);
                        decimal total = Convert.ToDecimal(row["total"]);
                        JObject detail = new JObject();
                        detail.Add("customerid",customerid);
                        detail.Add("total", total);
                        detail.Add("billdate", balancedate.Value.AddDays(1).ToString("yyyy-MM-dd"));
                        detail.Add("createtime", DateTime.Now);
                        db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{initreceivable}','"
                            + total+ "'::jsonb,true) where id=" + customerid);

                        db.ExcuteNoneQuery("insert into receivabledetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");
                    
                    }

                    foreach (var detail in receivabledetails)
                    {
                        db.ExcuteNoneQuery("insert into receivabledetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");
                    }

                    //应付
                    db.ExcuteNoneQuery("update vendor set content=content-'initpayable'");
                    sql = "with cte as " +
                       "( " +
                       "select (content->>'vendorid')::int as vendorid,(content->>'total')::decimal as total " +
                       "from payabledetail where content->>'billdate'<='" + balancedate.Value.ToString("yyyy-MM-dd") + "' " +
                       ") " +
                       "select cte.vendorid,sum(total) as total " +
                       "from cte " +
                       "group by cte.vendorid ";
                    DataTable historypayable = db.QueryTable(sql);
                    var payabledetails = db.Where("select * from payabledetail where content->>'billdate'>'"
                       + balancedate.Value.ToString("yyyy-MM-dd") + "'");
                    db.Truncate("payabledetail");

                    foreach (DataRow row in historypayable.Rows)
                    {
                        int vendorid = Convert.ToInt32(row["vendorid"]);
                        decimal total = Convert.ToDecimal(row["total"]);
                        JObject detail = new JObject();
                        detail.Add("vendorid", vendorid);
                        detail.Add("total", total);
                        detail.Add("billdate", balancedate.Value.AddDays(1).ToString("yyyy-MM-dd"));
                        detail.Add("createtime", DateTime.Now);
                        db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{initpayable}','"
                            + total + "'::jsonb,true) where id=" + vendorid);

                        db.ExcuteNoneQuery("insert into payabledetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");

                    }

                    foreach (var detail in payabledetails)
                    {
                        db.ExcuteNoneQuery("insert into payabledetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");
                    }

                    //现金银行
                    db.ExcuteNoneQuery("update bank set content=content-'inittotal'");
                    sql = "with cte as " +
                       "( " +
                       "select (content->>'bankid')::int as bankid,(content->>'total')::decimal as total " +
                       "from bankdetail where content->>'billdate'<='" + balancedate.Value.ToString("yyyy-MM-dd") + "' " +
                       ") " +
                       "select cte.bankid,sum(total) as total " +
                       "from cte " +
                       "group by cte.bankid ";
                    DataTable historybank = db.QueryTable(sql);
                    var bankdetails = db.Where("select * from bankdetail where content->>'billdate'>'"
                       + balancedate.Value.ToString("yyyy-MM-dd") + "'");
                    db.Truncate("bankdetail");

                    foreach (DataRow row in historybank.Rows)
                    {
                        int bankid = Convert.ToInt32(row["bankid"]);
                        decimal total = Convert.ToDecimal(row["total"]);
                        JObject detail = new JObject();
                        detail.Add("bankid", bankid);
                        detail.Add("total", total);
                        detail.Add("billdate", balancedate.Value.AddDays(1).ToString("yyyy-MM-dd"));
                        detail.Add("createtime", DateTime.Now);
                        db.ExcuteNoneQuery("update bank set content=jsonb_set(content,'{inittotal}','"
                            + total + "'::jsonb,true) where id=" + bankid);

                        db.ExcuteNoneQuery("insert into bankdetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");

                    }

                    foreach (var detail in bankdetails)
                    {
                        db.ExcuteNoneQuery("insert into bankdetail (content) values ('" + JsonConvert.SerializeObject(detail) + "')");
                    }
                }
                else
                {
                    //库存
                    db.ExcuteNoneQuery("update product set content=jsonb_set(content,'{initstorage}',content->'storage',true) where coalesce(content->>'storage','')!=''");
                    db.ExcuteNoneQuery("update product set content=content-'storage' where coalesce(content->>'storage','')!=''");
                    db.Truncate("storagedetail");
                    db.Truncate("virtualstoragedetail");

                    //应收应付
                    db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{initreceivable}',content->'receivable',true) where coalesce(content->>'receivable','')!=''");
                    db.ExcuteNoneQuery("update customer set content=content-'receivable' where coalesce(content->>'receivable','')!=''");
                    db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{initpayable}',content->'payable',true) where coalesce(content->>'payable','')!=''");
                    db.ExcuteNoneQuery("update vendor set content=content-'payable' where coalesce(content->>'payable','')!=''");
                    db.Truncate("receivabledetail");
                    db.Truncate("payabledetail");

                    //现金银行
                    db.ExcuteNoneQuery("update bank set content=jsonb_set(content,'{inittotal}',content->'total',true) where coalesce(content->>'total','')!=''");
                    db.ExcuteNoneQuery("update bank set content=content-'total' where coalesce(content->>'total','')!=''");
                    db.Truncate("bankdetail");
                }
                #endregion

                #region 清空月结
                db.Truncate("monthbalance");
                #endregion

                #region 如果是指定日期，自动开账。否则不开账
                var options = db.First("select * from option");
                if (balancedate.HasValue)
                {
                    //记录开账日期
                    options.content["initoverdate"] = balancedate.Value.AddDays(1).ToString("yyyy-MM-dd");
                    db.Edit("option", options);
                }
                else
                {
                    options.content.Remove("initoverdate");
                    db.Edit("option", options);
                }
                #endregion
                 
                db.SaveChanges();
            }
        }

        private void ExecNoneQuery(string connectionstring, string sql)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstring))
            {
                connection.Open();
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = sql;

                command.ExecuteScalar();

                connection.Close();
            }
        }

        
        private void AddRole(string applicationid)
        {
            DataContext dc = new DataContext(applicationid);

            using (DBHelper db = new DBHelper(dc, true))
            {
                var employees = db.Where("select * from employee where id>1 and content->>'username'<>''");
                //创建角色
                foreach (var employee in employees)
                {
                    string rolename = "myuid" + db.CurrentDataContext.CorporationId + "_" + employee.id;
                    int cnt = db.Count("select count(*) as cnt from pg_user where usename='" + rolename + "'");
                    if (cnt == 0)
                    {
                        db.ExcuteNoneQuery(string.Format("create role {0} login password '{1}'", rolename, "mypassword" + employee.id));
                    }

                    db.ExcuteNoneQuery("grant all on all tables in schema public to " + rolename);
                    db.ExcuteNoneQuery("grant all on all sequences in schema public to " + rolename);

                    var product = employee.content.Value<JObject>("scope").Value<JArray>("product").Values<int>().ToList();
                    if (!(product.Count == 1 && product[0] == 0))
                    {
                        SetScopePolicyByCategory(employee.id, "product", product, db);
                    }

                    var customer = employee.content.Value<JObject>("scope").Value<JArray>("customer").Values<int>().ToList();
                    if (!(customer.Count == 1 && customer[0] == 0))
                    {
                        SetScopePolicyByCategory(employee.id, "customer", customer, db);
                    }

                    var vendor = employee.content.Value<JObject>("scope").Value<JArray>("vendor").Values<int>().ToList();
                    if (!(vendor.Count == 1 && vendor[0] == 0))
                    {
                        SetScopePolicyByCategory(employee.id, "vendor", vendor, db);
                    }

                    var stock = employee.content.Value<JObject>("scope").Value<JArray>("stock").Values<int>().ToList();
                    if (!(stock.Count == 1 && stock[0] == 0))
                    {
                        SetScopePolicyById(employee.id, "stock", stock, db);
                    }
                }

                db.SaveChanges();
            }
        }
        
        private void SetScopePolicyByCategory(int employeeid, string tablename, List<int> scope, DBHelper db)
        {
            string policyname = "myscopepolicy" + db.CurrentDataContext.CorporationId + "_" + employeeid;
            string rolename = "myuid" + db.CurrentDataContext.CorporationId + "_" + employeeid;

            db.ExcuteNoneQuery("ALTER TABLE " + tablename + " ENABLE ROW LEVEL SECURITY");
            db.ExcuteNoneQuery("DROP POLICY IF EXISTS " + policyname + " ON " + tablename);

            if (scope.Count == 1 && scope[0] == 0)
            {
                db.ExcuteNoneQuery("Create POLICY " + policyname
                       + " ON " + tablename + " to " + rolename
                       + " using(true)");
            }
            else
            {

                db.ExcuteNoneQuery("Create POLICY " + policyname
                    + " ON " + tablename + " to " + rolename
                    + " using(coalesce(content->>'categoryid','-1')::int in  (select jsonb_array_elements_text( content->'scope'->'" + tablename + "')::int from employee where id=" + employeeid + "))");
            }
        }

        private void SetScopePolicyById(int employeeid, string tablename, List<int> scope, DBHelper db)
        {
            string policyname = "myscopepolicy" + db.CurrentDataContext.CorporationId + "_" + employeeid;
            string rolename = "myuid" + db.CurrentDataContext.CorporationId + "_" + employeeid;

            db.ExcuteNoneQuery("ALTER TABLE " + tablename + " ENABLE ROW LEVEL SECURITY");
            db.ExcuteNoneQuery("DROP POLICY IF EXISTS " + policyname + " ON " + tablename);

            if (scope.Count == 1 && scope[0] == 0)
            {
                db.ExcuteNoneQuery("Create POLICY " + policyname
                    + " ON " + tablename + " to " + rolename
                    + " using(true)");
            }
            else
            {
                db.ExcuteNoneQuery("Create POLICY " + policyname
                    + " ON " + tablename + " to " + rolename
                    + " using(id in  (select jsonb_array_elements_text( content->'scope'->'" + tablename + "')::int from employee where id=" + employeeid + "))");
            }
        }

    }
}
