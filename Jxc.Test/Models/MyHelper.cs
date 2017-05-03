using Npgsql;
using System;
using System.Collections.Generic;
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
        public static Account GetAccount(this IIdentity identity)
        {
            string[] ss = identity.Name.Split(new char[] { '`' }, StringSplitOptions.RemoveEmptyEntries);
            string applicationid = ss[0];

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

            Account account = new Account()
            {
                ApplicationId=applicationid,
                Id=Convert.ToInt32(dt.Rows[0]["id"]),
                Name=dt.Rows[0]["name"].ToString(),
                UserName=ss[2],
                CompanyName=ss[1],
                Language=ss[3],
                Limit = dt.Rows[0]["limits"].ToString()

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