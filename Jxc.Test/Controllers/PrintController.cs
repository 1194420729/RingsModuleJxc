using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Newtonsoft.Json;
using Rings.Models;
using Npgsql;
using System.Data;


namespace Baseingfo.Test.Controllers
{
    [Authorize]
    public class PrintController : Controller
    {
        public ActionResult PrintPage(string category, int modelid)
        {
            ViewBag.Category = category;
            ViewBag.ModelId = modelid;

            return View();
        }

        public ActionResult PrintDialog(string category, int modelid)
        {
            var account = User.Identity.GetAccount();
            DataContext db = new DataContext(account.ApplicationId);

            Dictionary<int, PrintTemplate> templates = new Dictionary<int, PrintTemplate>();
            using (NpgsqlConnection connection = new NpgsqlConnection(db.ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select id,content from printtemplate where content->>'category'=@category";
                command.Parameters.Add("category", NpgsqlTypes.NpgsqlDbType.Varchar, 50).Value = category;

                DataTable dt = new DataTable();
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);
                connection.Close();

                foreach (DataRow row in dt.Rows)
                {
                    templates.Add(Convert.ToInt32(row["id"]), JsonConvert.DeserializeObject<PrintTemplate>(row["content"].ToString()));
                }

            }

            ViewBag.Category = category;
            ViewBag.ModelId = modelid;

            return PartialView(templates);
        }


        public ActionResult PrintPreview(int templateid, int modelid)
        { 
            var template = GetPrintTemplateById(templateid);

            var account = User.Identity.GetAccount();
            PrintManager pm = new PrintManager(account.ApplicationId);
            ViewBag.PrintData = pm.GetPrintModel(modelid);

            ViewBag.Width = Convert.ToInt32((template.Width - template.Padding - template.Padding) / 25.41M * 96M);
            ViewBag.TemplateId = templateid;

            return View(template);
        }


        public ActionResult PrintDesigner(string category, int? templateid, int modelid)
        { 

            ViewBag.Width = 210;
            ViewBag.Height = 297;
            ViewBag.Padding = 5;
            ViewBag.RepeatHeader = false;
            
            if (templateid.HasValue)
            {
                var template = GetPrintTemplateById(templateid.Value);
                ViewBag.Width = template.Width;
                ViewBag.Height = template.Height;
                ViewBag.Padding = template.Padding;
                ViewBag.Template = template;
                ViewBag.RepeatHeader = template.RepeatHeader ?? false;
                ViewBag.FixedLines = template.FixedLines;
                ViewBag.MaxLines = template.MaxLines;

            }

            ViewBag.TemplateId = templateid;
            ViewBag.Category = category;
            ViewBag.ModelId = modelid;
            

            var account = User.Identity.GetAccount();
            PrintManager pm = new PrintManager(account.ApplicationId);
            ViewBag.PrintData = pm.GetPrintModel(modelid);

            return PartialView();
        }


        [HttpPost]
        [ValidateInput(false)]
        public ActionResult PrintDesignerSave(int? id, PrintTemplate item)
        {
            var account = User.Identity.GetAccount();
            DataContext db = new DataContext(account.ApplicationId);
             
            string json = JsonConvert.SerializeObject(item);

            using (NpgsqlConnection connection = new NpgsqlConnection(db.ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                if (id.HasValue)
                {
                    command.CommandText = "update printtemplate set content=@content  where id=" + id.Value;
                }
                else
                {
                    command.CommandText = "insert into printtemplate (content) values (@content)";
                }

                command.Parameters.Add("content", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = json.ToLower();

                command.ExecuteNonQuery();
                connection.Close();
            }

            return Json(new { message = "ok" }, "text/plain");
        }
         
        public ActionResult QueryFieldNameByType(string fieldtype, int modelid)
        {
            var account = User.Identity.GetAccount();
            PrintManager pm = new PrintManager(account.ApplicationId);
            PrintData pd = pm.GetPrintModel(modelid);
            if (fieldtype == "header")
            {
                return Json(pd.HeaderField, "text/plain", JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(pd.DetailField, "text/plain", JsonRequestBehavior.AllowGet);
            }
        }

        private PrintTemplate GetPrintTemplateById(int templateid)
        {
            var account = User.Identity.GetAccount();
            DataContext db = new DataContext(account.ApplicationId);

            PrintTemplate template = null;
            using (NpgsqlConnection connection = new NpgsqlConnection(db.ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select content from printtemplate where id=" + templateid;
                string json = command.ExecuteScalar().ToString();

                template = JsonConvert.DeserializeObject<PrintTemplate>(json);
                connection.Close();
            }

            return template;
        }
    }
}
