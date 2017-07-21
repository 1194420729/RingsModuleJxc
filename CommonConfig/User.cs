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

namespace CommonConfig
{
    public class User : MarshalByRefObject
    {
        private string tablename = "employee";

        public Object List(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();
            sb.Append(" and id>1 and coalesce(content->>'username','')<>''");

            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field.ToLower() == "state")
                {
                    sb.AppendFormat(" and coalesce(content->>'stop','')='{0}'", f == "normal" ? "" : "t");
                }
                else
                {
                    sb.AppendFormat(" and content->>'{0}' ilike '%{1}%'", field.ToLower(), f);
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
            List<TableModel> list = db.Query(this.tablename, sb.ToString(), sborder.ToString(), out recordcount);
            foreach (var item in list)
            {
                if (item.content["departmentid"] == null) continue;

                var department = db.FirstOrDefault("select * from department where id=" + item.content.Value<int>("departmentid"));
                if (department == null) continue;
                item.content.Add("department", department.content);
            }

            return new { resulttotal = recordcount, data = list };
        }

        public Object GetEmployees(string parameters)
        {
            DBHelper db = new DBHelper();

            var list = db.Where("select * from employee where coalesce(content->>'stop','')='' and coalesce(content->>'username','')='' order by content->>'name'");

            return new { data = list };
        }

        public Object GetDepartments(string parameters)
        {
            DBHelper db = new DBHelper();

            var list = db.Where("select * from department where coalesce(content->>'stop','')=''  order by content->>'name'");

            return new { data = list };
        }

        [MyLog("添加操作员")]
        public Object AddSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["employeeid"]);
            string password = Convert.ToString(dic["password"]);

            using (DBHelper db = new DBHelper(true))
            {
                var model = db.First("select * from employee where id=" + id);
                model.content["username"] = model.content["code"];
                model.content["password"] = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "MD5");
                if (model.content["scope"] == null)
                {
                    Dictionary<string, object> scope = new Dictionary<string, object>();
                    scope.Add("product", new List<int>() { 0 });
                    scope.Add("customer", new List<int>() { 0 });
                    scope.Add("vendor", new List<int>() { 0 });
                    scope.Add("stock", new List<int>() { 0 });
                    model.content["scope"] = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(scope));
                }
                db.Edit(tablename, model);

                //创建角色
                string rolename = "myuid" + db.CurrentDataContext.CorporationId + "_" + id;
                int cnt = db.Count("select count(0) as cnt from pg_user where usename='" + rolename + "'");
                if (cnt == 0)
                {
                    db.ExcuteNoneQuery(string.Format("create role {0} login password '{1}'", rolename, "mypassword" + id));
                    db.ExcuteNoneQuery("grant all on all tables  in schema public to " + rolename);
                    db.ExcuteNoneQuery("grant all on all sequences in schema public to " + rolename);
                }

                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        public Object Reset(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper();

            var model = db.First("select * from \"" + tablename + "\" where id=" + id);

            return new { data = model };

        }

        [MyLog("重置密码")]
        public Object ResetSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["employeeid"]);
            string password = Convert.ToString(dic["password"]);

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from employee where id=" + id);
                model.content["password"] = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "MD5");
                db.Edit(tablename, model);

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        [MyLog("删除操作员")]
        public Object Delete(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from employee where id=" + id);
                model.content.Remove("username");
                model.content.Remove("password");
                db.Edit(tablename, model);

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        public Object Copy(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper();

            var model = db.First("select * from \"" + tablename + "\" where id=" + id);
            var employees = db.Where("select * from \"" + tablename + "\" where id<>" + id + " and id>1 and coalesce(content->>'username','')<>'' ");

            return new { data = model, employees = employees };

        }

        [MyLog("复制权限")]
        public Object CopySave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);
            string ids = Convert.ToString(dic["ids"]);
            bool copyscope = Convert.ToBoolean(dic["copyscope"]);

            string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            using (DBHelper db = new DBHelper(true))
            {
                var model = db.First("select * from employee where id=" + id);
                foreach (string s in ss)
                {
                    var employee = db.First("select * from employee where id=" + s);
                    employee.content["limit"] = model.content["limit"];
                    employee.content["scope"] = model.content["scope"];

                    db.Edit(tablename, employee);
                }

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        private Object Edit_1(string parameters)
        {
            //刷新权限的定义
            ILimitLoader loader = LimitLoaderFactory.GetInstance();
            loader.Load();

            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper();

            var model = db.First("select * from \"" + tablename + "\" where id=" + id);

            DataTable dt = db.QueryTable("select content->>'groupname' as groupname,jsonb_agg(jsonb_set(content,'{id}',id::text::jsonb)) as items from \"limit\" group by content->>'groupname'");

            Account account = new Account()
            {
                Id = model.id,
                UserName = model.content.Value<string>("username"),
                Name = model.content.Value<string>("name"),
                ApplicationId = PluginContext.Current.Account.ApplicationId,
                Limit = model.content["limit"] == null ? "" : model.content.Value<string>("limit")
            };

            var list = db.Where("select * from \"limit\"");
            Dictionary<int, bool> userlimits = new Dictionary<int, bool>();
            foreach (var item in list)
            {
                userlimits.Add(item.id, account.IsAllowed(item.id));
            }

            return new { data = model, limits = dt, userlimit = userlimits };

        }

        public Object Edit(string parameters)
        {
            //刷新权限的定义
            ILimitLoader loader = LimitLoaderFactory.GetInstance();
            loader.Load();

            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper(true);
             
            var model = db.First("select * from \"" + tablename + "\" where id=" + id);             
            Account account = new Account()
            {
                Id = model.id,
                UserName = model.content.Value<string>("username"),
                Name = model.content.Value<string>("name"),
                ApplicationId = PluginContext.Current.Account.ApplicationId,
                Limit = model.content["limit"] == null ? "" : model.content.Value<string>("limit")
            };

            var list = db.Where("select * from \"limit\" order by id");
            Dictionary<int, bool> userlimits = new Dictionary<int, bool>();
            foreach (var item in list)
            {
                userlimits.Add(item.id, account.IsAllowed(item.id));
            }
              
            var modules = (from c in list
                           group c by new { modulename = c.content.Value<string>("modulename"), modulesort = c.content.Value<int>("modulesort") } into g
                          select g.Key).OrderByDescending(c=>c.modulesort).Select(c=>c.modulename).ToList();
            Dictionary<string, List<string>> modulegroups = new Dictionary<string, List<string>>();
            foreach (string modulename in modules)
            {
                var sublist = list.Where(c => c.content.Value<string>("modulename") == modulename);
                var groups = (from c in sublist
                             group c by new { groupname = c.content.Value<string>("groupname"), groupsort = c.content.Value<int>("groupsort") } into g
                             select g.Key).OrderByDescending(c=>c.groupsort).Select(c=>c.groupname).ToList();
                modulegroups.Add(modulename,groups);
                 
            }

            list.ForEach(c => { c.content.Add("id", c.id); });
            List<JObject> limits = list.Select(c => c.content).ToList();

            return new { data = model,modules=modules,modulegroups=modulegroups, limits = limits, userlimit = userlimits };

        }

        [MyLog("设置权限")]
        public Object EditSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);
            string userlimit = Convert.ToString(dic["userlimit"]);

            Dictionary<int, bool> limitdic = JsonConvert.DeserializeObject<Dictionary<int, bool>>(userlimit);
            int max = limitdic.Keys.Max();
            char[] chars = new char[max + 1];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = '0';
            }

            foreach (int pos in limitdic.Keys)
            {
                chars[pos] = limitdic[pos] ? '1' : '0';
            }

            string limit = new String(chars);

            using (DBHelper db = new DBHelper(true))
            {
                var model = db.First("select * from employee where id=" + id);
                model.content["limit"] = limit;
                db.Edit(tablename, model);

                string rolename = "myuid" + db.CurrentDataContext.CorporationId + "_" + id;
                db.ExcuteNoneQuery("grant all on all tables in schema public to " + rolename);
                db.ExcuteNoneQuery("grant all on all sequences in schema public to " + rolename);

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

        public Object Scope(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper();

            var model = db.First("select * from \"" + tablename + "\" where id=" + id);

            return new { data = model };

        }

        private ICollection<TableModel> CategoryList(string categoryclass)
        {
            DBHelper db = new DBHelper();
            return db.Where("select * from category where content->>'classname'='" + categoryclass + "'");
        }


        private ICollection<TreeData> GetCategoryTreeData(string categoryclass)
        {
            var items = CategoryList(categoryclass);

            var list = (from c in items
                        select new TreeData
                        {
                            id = c.id.ToString(),
                            parent = c.content["parentid"] == null ? "0" : c.content.Value<string>("parentid"),
                            text = c.content.Value<string>("name"),
                            icon = "fa fa-folder-o fa-fw"
                        }).ToList();

            list.Insert(0, new TreeData()
            {
                icon = "fa fa-folder-o fa-fw",
                id = "0",
                parent = "#",
                text = StringHelper.GetString("全部分类")
            });

            return list;
        }

        public Object Categorys(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            string classname = dic["classname"].ToString();
            int id = Convert.ToInt32(dic["id"]);


            var list = GetCategoryTreeData(classname);

            DBHelper db = new DBHelper(true);
            var employee = db.First("employee", id);
            if (employee.content["scope"] != null && employee.content.Value<JObject>("scope")[classname] != null)
            {
                List<int> ids = employee.content.Value<JObject>("scope").Value<JArray>(classname).Values<int>().ToList();
                //string[] ss = ids.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in list)
                {
                    item.state = new TreeDataState() { selected = ids.Contains(Convert.ToInt32(item.id)) };

                }
            }

            return list;
        }

        public Object Stocks(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            string classname = dic["classname"].ToString();
            int id = Convert.ToInt32(dic["id"]);

            DBHelper db = new DBHelper(true);
            var stocks = db.Where("select * from stock where coalesce(content->>'stop','')=''  order by content->>'code'");
            List<TreeData> list = new List<TreeData>();
            list.Add(new TreeData()
            {
                icon = "fa fa-folder-o fa-fw",
                id = "0",
                parent = "#",
                text = StringHelper.GetString("全部仓库")
            });
            foreach (var item in stocks)
            {
                TreeData td = new TreeData()
                {
                    id = item.id.ToString(),
                    parent = "0",
                    text = item.content.Value<string>("name"),
                    icon = "fa fa-folder-o fa-fw"
                };

                list.Add(td);
            }

            var employee = db.First("employee", id);
            if (employee.content["scope"] != null && employee.content.Value<JObject>("scope")[classname] != null)
            {
                List<int> ids = employee.content.Value<JObject>("scope").Value<JArray>(classname).Values<int>().ToList();
                foreach (var item in list)
                {
                    item.state = new TreeDataState() { selected = ids.Contains(Convert.ToInt32(item.id)) };

                }
            }

            return list;
        }

        [MyLog("数据授权")]
        public Object ScopeSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
            int id = Convert.ToInt32(dic["id"]);
            var product = ScopeToArray(Convert.ToString(dic["product"]));
            var customer = ScopeToArray(Convert.ToString(dic["customer"]));
            var vendor = ScopeToArray(Convert.ToString(dic["vendor"]));
            var stock = ScopeToArray(Convert.ToString(dic["stock"]));


            Dictionary<string, object> scope = new Dictionary<string, object>();
            scope.Add("product", product);
            scope.Add("customer", customer);
            scope.Add("vendor", vendor);
            scope.Add("stock", stock);

            using (DBHelper db = new DBHelper(true))
            {
                var model = db.First("select * from employee where id=" + id);
                model.content["scope"] = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(scope));
                db.Edit(tablename, model);

                SetScopePolicyByCategory(id, "product", product, db);
                SetScopePolicyByCategory(id, "customer", customer, db);
                SetScopePolicyByCategory(id, "vendor", vendor, db);
                SetScopePolicyById(id, "stock", stock, db);

                db.SaveChanges();
            }
            return new { message = "ok" };

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
        
        private List<int> ScopeToArray(string scope)
        {
            string[] ss = scope.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (ss.Contains("0"))
            {
                return new List<int>() { 0 };
            }
            else
            {
                return ss.Select(c => Convert.ToInt32(c)).ToList();
            }
        }

    }
}
