using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc; 
using System.Data;
using System.Web.Security; 
using System.Drawing;
using System.IO;
using Npgsql;
using System.Configuration;
using System.Globalization;
using System.Threading; 
using System.Resources;
using Rings.Models;

namespace Baseingfo.Test.Controllers
{
    
    public class AccountController : Controller
    {
        public ActionResult Login(string lan)
        {
            ViewBag.Lan = lan;

            if (string.IsNullOrEmpty(lan) == false && lan.ToLower() != "zh-cn")
            {
                return View(lan + "/login");
            }

            return View();
        }

        [HttpPost]
        public ActionResult Login(string company, string username, string password, string validcode, string lan, int ztid)
        {
            if (Session["yanzhengma"] == null || Session["yanzhengma"].ToString() != validcode)
            {
                return Json(new { message = StringHelper.GetString("验证码不正确", lan) });
            }

            DataTable dtCompany = new DataTable();
            DataTable dtParent = null;
            string connectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstr))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select id,applicationid,name,connectionstring,content->>'rootid' as rootid,(content->>'default')::bool as isdefault from corporation where id=" + ztid;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dtCompany);

                if (dtCompany.Rows.Count > 0 && Convert.ToBoolean(dtCompany.Rows[0]["isdefault"]) == false)
                {
                    dtParent = new DataTable();
                    command.CommandText = "select * from corporation where id=" + dtCompany.Rows[0]["rootid"];
                    da.Fill(dtParent);
                }

                connection.Close();
            }

            if (dtCompany.Rows.Count == 0)
            {
                return Json(new { message = StringHelper.GetString("请选择账套", lan) });
            }

            if (Convert.ToBoolean(dtCompany.Rows[0]["isdefault"]) == false && dtParent.Rows[0]["name"].ToString() != company)
            {
                return Json(new { message = StringHelper.GetString("公司名称不正确", lan) });
            }
            else if (Convert.ToBoolean(dtCompany.Rows[0]["isdefault"]) && dtCompany.Rows[0]["name"].ToString() != company)
            {
                return Json(new { message = StringHelper.GetString("公司名称不正确", lan) });
            }

            if (string.IsNullOrEmpty(username))
            {
                return Json(new { message = StringHelper.GetString("用户名不存在", lan) });
            }

            DataTable dt = new DataTable();
            string userdbconnstr = dtCompany.Rows[0]["connectionstring"].ToString();
            using (NpgsqlConnection connection = new NpgsqlConnection(userdbconnstr))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select id,content->>'username' as username,content->>'password' as password,content->>'name' as name from \"employee\" where content->>'username'=@username";
                command.Parameters.Add("username", NpgsqlTypes.NpgsqlDbType.Text).Value = username;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

                connection.Close();
            }

            if (dt.Rows.Count == 0)
            {
                return Json(new { message = StringHelper.GetString("用户名不存在", lan) }, "text/plain");
            }
            else
            {
                string md5 = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "MD5");
                if (string.IsNullOrEmpty(password) || md5 != dt.Rows[0]["password"].ToString())
                {
                    return Json(new { message = StringHelper.GetString("密码错误", lan) }, "text/plain");
                }

                FormsAuthentication.SetAuthCookie(dtCompany.Rows[0]["applicationid"] + "`" + company + "`" + username + "`" + lan + "`"
                    + (dtParent == null ? dtCompany.Rows[0]["applicationid"].ToString() : dtParent.Rows[0]["applicationid"].ToString()), false);
                return Json(new { message = "ok" }, "text/plain");
            }

        }

        [HttpPost]
        public ActionResult GetZtList(string company)
        {
            List<object> list = new List<object>();
            DataTable dtCompany = new DataTable();
            string connectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionstr))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select id,content->>'name' as name from corporation where name=@name and (content->>'default')::bool=true";
                command.Parameters.Add("name", NpgsqlTypes.NpgsqlDbType.Text).Value = company;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dtCompany);

                if (dtCompany.Rows.Count == 0)
                {
                    connection.Close();
                    return Json(new { message = "empty" });
                }

                list.Add(new
                {
                    id = Convert.ToInt32(dtCompany.Rows[0]["id"]),
                    name = dtCompany.Rows[0]["name"].ToString()
                });

                command.Parameters.Clear();
                command.CommandText = "select id,content->>'name' as name from corporation where (content->>'rootid')::int=" + Convert.ToInt32(dtCompany.Rows[0]["id"]);
                dtCompany = new DataTable();
                da.Fill(dtCompany);

                connection.Close();
            }


            foreach (DataRow row in dtCompany.Rows)
            {
                list.Add(new
                {
                    id = Convert.ToInt32(row["id"]),
                    name = row["name"].ToString()
                });
            }

            return this.MyJson(new { message = "ok", list = list });
        }

        [Authorize]
        public ActionResult Logout(string username, string password)
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("Login");
        }

        [HttpPost]
        public ActionResult UnitTestLogin()
        {
#if DEBUG
            string applicationid="D36F64D9-4646-4531-990B-B7A3FA5FAEF2";
            string company="测试公司";
            string username="001";
            string lan="zh-CN";
            FormsAuthentication.SetAuthCookie(applicationid + "`" + company + "`" + username + "`" + lan, false);
            return Content("ok","text/plain",System.Text.Encoding.UTF8);
#else

            return Content("error", "text/plain", System.Text.Encoding.UTF8);
#endif
        }

        public ActionResult Yanzhengma()
        {
            string str = getRandomValidate(4);
            Session.Remove("yanzhengma");
            Session.Add("yanzhengma", str);

            return File(getImageValidate(str), "image/gif");
        }


        //得到随机字符串,长度自己定义
        private string getRandomValidate(int len)
        {
            Random ran = new Random();

            int num;
            int tem;
            string rtuStr = "";
            for (int i = 0; i < len; i++)
            {
                num = ran.Next();
                /*
                 * 这里可以选择生成字符和数字组合的验证码
                 */
                tem = num % 10 + '0';//生成数字
                //tem = num % 26 + 'A';//生成字符
                rtuStr += Convert.ToChar(tem).ToString();
            }
            return rtuStr;
        }
        //生成图像
        private byte[] getImageValidate(string strValue)
        {
            //string str = "OO00"; //前两个为字母O，后两个为数字0
            int width = Convert.ToInt32(strValue.Length * 12);    //计算图像宽度
            Bitmap img = new Bitmap(width, 23);
            Graphics gfc = Graphics.FromImage(img);           //产生Graphics对象，进行画图
            gfc.Clear(Color.White);
            drawLine(gfc, img);
            //写验证码，需要定义Font
            Font font = new Font("arial", 12, FontStyle.Bold);
            System.Drawing.Drawing2D.LinearGradientBrush brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, img.Width, img.Height), Color.DarkOrchid, Color.Blue, 1.5f, true);
            gfc.DrawString(strValue, font, brush, 3, 2);
            drawPoint(img);
            gfc.DrawRectangle(new Pen(Color.DarkBlue), 0, 0, img.Width - 1, img.Height - 1);
            //将图像添加到页面
            MemoryStream ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);

            byte[] bytes = ms.ToArray();
            ms.Close();
            img.Dispose();

            return bytes;

        }
        private void drawLine(Graphics gfc, Bitmap img)
        {
            Random ran = new Random();

            //选择画10条线,也可以增加，也可以不要线，只要随机杂点即可
            for (int i = 0; i < 10; i++)
            {
                int x1 = ran.Next(img.Width);
                int y1 = ran.Next(img.Height);
                int x2 = ran.Next(img.Width);
                int y2 = ran.Next(img.Height);
                gfc.DrawLine(new Pen(Color.Silver), x1, y1, x2, y2);      //注意画笔一定要浅颜色，否则验证码看不清楚
            }
        }
        private void drawPoint(Bitmap img)
        {
            Random ran = new Random();

            /*
            //选择画100个点,可以根据实际情况改变
            for (int i = 0; i < 100; i++)
            {
                int x = ran.Next(img.Width);
                int y = ran.Next(img.Height);
                img.SetPixel(x,y,Color.FromArgb(ran.Next()));//杂点颜色随机
            }
             */
            int col = ran.Next();//在一次的图片中杂店颜色相同
            for (int i = 0; i < 100; i++)
            {
                int x = ran.Next(img.Width);
                int y = ran.Next(img.Height);
                img.SetPixel(x, y, Color.FromArgb(col));
            }
        }

    } 
    
}
