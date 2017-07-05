using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Rings.Models
{
    public static class MyHelper
    {
        public static IDictionary<string, object> ToDictionary(this System.Collections.Specialized.NameValueCollection nv)
        {
            var result = new Dictionary<string, object>();
            foreach (string key in nv.Keys)
            {
                string[] values = nv.GetValues(key);
                if (values.Length == 1)
                {
                    result.Add(key, values[0]);
                }
                else
                {
                    result.Add(key, values);
                }
            }

            return result;
        }
    

        public static Account GetAccount(this IIdentity identity)
        {
            string[] ss = identity.Name.Split(new char[] { '`' }, StringSplitOptions.RemoveEmptyEntries);
            string applicationid = ss[0];
            string rootapplicationid = ss[4];

            DataContext db = new DataContext(applicationid);
            DataTable dt = new DataTable(); 
            using (NpgsqlConnection connection = new NpgsqlConnection(db.ConnectionString))
            {
                connection.Open();
                
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select id,content->>'username' as username,content->>'password' as password,content->>'name' as name,coalesce(content->>'limit','') as limits from \"employee\" where content->>'username'=@username";
                command.Parameters.Add("username", NpgsqlTypes.NpgsqlDbType.Text).Value = ss[2];
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

                connection.Close();
            }

            DataTable dtCompany = new DataTable();
            string connectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstr))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select content->>'name' as name,(content->>'default')::bool as isdefault from corporation where id=" + db.CorporationId;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dtCompany);

                connection.Close();
            }

            Account account = new Account()
            {
                ApplicationId=applicationid,
                RootApplicationId = rootapplicationid,
                Id=Convert.ToInt32(dt.Rows[0]["id"]),
                Name=dt.Rows[0]["name"].ToString(),
                UserName=ss[2],
                CompanyName=ss[1],
                Language=ss[3],
                Limit = dt.Rows[0]["limits"].ToString(),
                ZtName=dtCompany.Rows[0]["name"].ToString(),
                IsDefaultZt=Convert.ToBoolean(dtCompany.Rows[0]["isdefault"])
            };

            return account;
             
        }

        public static DataContext GetDataContext(this IIdentity identity)
        {
            string[] ss=identity.Name.Split(new char[] { '`' },StringSplitOptions.RemoveEmptyEntries);
            string applicationid = ss[0];

            DataContext db = new DataContext(applicationid);

            return db;
        }

        public static string GetLan(this IIdentity identity)
        {
            string[] ss = identity.Name.Split(new char[] { '`' }, StringSplitOptions.RemoveEmptyEntries);
             
            return ss[3];
        }
         
        public static MyJsonResult MyJson(this Controller controller, object data,
                string contentType = "application/json", Encoding encoding = null, JsonRequestBehavior behavior = JsonRequestBehavior.DenyGet)
        {
            var result = new MyJsonResult()
            {
                Data = data,
                ContentType = contentType,
                ContentEncoding = encoding == null ? Encoding.UTF8 : encoding,
                JsonRequestBehavior = behavior
            };

            return result;
        }

        public static MyPartialViewResult MyPartialView(this Controller controller, string viewName = null, object model = null)
        {
            if (model != null)
            {
                controller.ViewData.Model = model;
            }

            return new MyPartialViewResult
            {
                ViewName = viewName,
                ViewData = controller.ViewData,
                TempData = controller.TempData,
                ViewEngineCollection = controller.ViewEngineCollection
            };
        }

        public static MyViewResult MyView(this Controller controller, string viewName = null, object model = null)
        {
            if (model != null)
            {
                controller.ViewData.Model = model;
            }

            return new MyViewResult
            {
                ViewName = viewName,
                ViewData = controller.ViewData,
                TempData = controller.TempData,
                ViewEngineCollection = controller.ViewEngineCollection
            };
        }

    }
}