using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Rings.Models;
using System.Configuration;
using Npgsql;
using Newtonsoft.Json;

namespace Baseingfo.Test.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        [HttpPost]
        public ActionResult UploadPic(HttpPostedFileBase file)
        {
            AppSettingsReader reader = new AppSettingsReader();
            string storageroot = reader.GetValue("storageroot", typeof(string)).ToString();

            if (file.ContentLength == 0)
            {
                return Json(new { message = "请选择文件" });
            }

            string type = file.ContentType.ToLower();
            if (type.StartsWith("image/") == false)
            {
                return Json(new { message = "图片格式不正确" });
            }

            int size = file.ContentLength / 1024;
            if (size > 512)
            {
                //return Json(new { message = "文件大小超出限制，请不要超过500K" });

            }

            string date = DateTime.Now.ToString("yyyyMMdd");
            string applicationid = User.Identity.GetAccount().RootApplicationId;
            string path = Path.Combine(storageroot, applicationid, date);
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            string newfilename = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);

            string fileName = Path.Combine(path, newfilename);

            try
            {
                file.SaveAs(fileName);

                //文件信息存入数据库
                AttachmentInfo info = new AttachmentInfo()
                {
                    Name = newfilename,
                    Path = fileName,
                    Size = file.ContentLength
                };

                SaveAttachmentInfo(info);

                //return Json(new { message = "ok", pic = domain + "/" + applicationid + "/" + date + "/" + newfilename });
                return Json(new { message = "ok", pic = Url.Action("Transmit", "upload", new { id = info.Id }, Request.Url.Scheme) });
            }
            catch (Exception ex)
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                return Json(new { message = "上传出错" });
            }

        }

        [HttpPost]
        public ActionResult UploadFile(HttpPostedFileBase file)
        {
            AppSettingsReader reader = new AppSettingsReader();
            string storageroot = reader.GetValue("storageroot", typeof(string)).ToString();

            if (file.ContentLength == 0)
            {
                return Json(new { message = "请选择文件" });
            }

            int size = file.ContentLength / 1024;
            if (size > 10240)
            {
                return Json(new { message = "文件大小超出限制，请不要超过10M" });
            }

            string date = DateTime.Now.ToString("yyyyMMdd");
            string applicationid = User.Identity.GetAccount().RootApplicationId;
            string path = Path.Combine(storageroot, applicationid, date);
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            string newfilename = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            string fileName = Path.Combine(path, newfilename);

            try
            {
                file.SaveAs(fileName);

                //文件信息存入数据库
                AttachmentInfo info = new AttachmentInfo()
                {
                    Name = newfilename,
                    Path = fileName,
                    Size = file.ContentLength
                };

                SaveAttachmentInfo(info);

                //return Json(new { message = "ok", url = domain + "/" + applicationid + "/" + date + "/" + newfilename, path = storageroot + "/" + applicationid + "/" + date + "/" + newfilename });
                return Json(new
                {
                    message = "ok",
                    url = Url.Action("Transmit", "upload", new { id = info.Id }, Request.Url.Scheme),
                    path = fileName
                });

            }
            catch (Exception ex)
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                return Json(new { message = "上传出错" });
            }

        }

        [HttpPost]
        public ActionResult UploadFiles()
        {
            AppSettingsReader reader = new AppSettingsReader();
            string storageroot = reader.GetValue("storageroot", typeof(string)).ToString();
             
            if (Request.Files == null || Request.Files.Count == 0)
            {
                return Json(new { message = "请选择文件" });
            }

            for (int i = 0; i < Request.Files.Count; i++)
            {
                HttpPostedFileBase file = Request.Files[i];

                if (file.ContentLength == 0)
                {
                    return Json(new { message = "文件不合法" });
                }

                int size = file.ContentLength / 1024;
                if (size > 10240)
                {
                    return Json(new { message = "单个文件大小超出限制，请不要超过10M" });
                }
            }

            string date = DateTime.Now.ToString("yyyyMMdd");
            string applicationid = User.Identity.GetAccount().RootApplicationId;
            string path = Path.Combine(storageroot, applicationid, date);
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            List<object> list = new List<object>();
            for (int i = 0; i < Request.Files.Count; i++)
            {
                HttpPostedFileBase file = Request.Files[i];

                string newfilename = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                string fileName = Path.Combine(path, newfilename);

                try
                {
                    file.SaveAs(fileName);

                    //文件信息存入数据库
                    AttachmentInfo info = new AttachmentInfo()
                    {
                        Name = newfilename,
                        Path = fileName,
                        Size = file.ContentLength
                    };

                    SaveAttachmentInfo(info);

                    list.Add(new
                    {
                        name = file.FileName,
                        url = Url.Action("Transmit", "upload", new { id = info.Id }, Request.Url.Scheme),
                        size = GetFileSizeFriendly(file.ContentLength)
                    });

                }
                catch (Exception ex)
                {
                    Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                    return Json(new { message = "上传出错" });
                }
            }

            return Json(new { message = "ok", urls = list });
        }

        public ActionResult Transmit(int id)
        {
            var info = GetAttachmentInfo(id);
            string mime=MimeTypes.MimeTypeMap.GetMimeType(new FileInfo(info.Path).Extension);
            return File(info.Path, mime);
        }

        public ActionResult Temporary(string filename,string appid,string date)
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temporary", appid, date,filename);
            string mime = MimeTypes.MimeTypeMap.GetMimeType(new FileInfo(path).Extension);
            return File(path, mime);
        }

        private string GetFileSizeFriendly(int size)
        {
            int m1 = 1024 * 1024;
            if (size < m1)
            {
                return (size / 1024) + "KB";
            }
            else
            {
                return Math.Round(Convert.ToDecimal(size) / Convert.ToDecimal(m1), 2) + "MB";
            }
        }

        private void SaveAttachmentInfo(AttachmentInfo info)
        {
            string connectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstr))
            {
                connection.Open();
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;
                command.CommandText = "insert into attachment (content) values (@content) returning id";
                command.Parameters.Add("@content", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = JsonConvert.SerializeObject(info).ToLower();
                info.Id = Convert.ToInt32(command.ExecuteScalar());
                connection.Close();
            }
        }

        private AttachmentInfo GetAttachmentInfo(int id)
        {
            string connectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            AttachmentInfo result = null;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstr))
            {
                connection.Open();
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;
                command.CommandText = "select content from attachment where id="+id;
                string content = command.ExecuteScalar().ToString();
                result = JsonConvert.DeserializeObject<AttachmentInfo>(content);
                connection.Close();
            }

            result.Id = id;
            return result;
        }
    }
    
}
