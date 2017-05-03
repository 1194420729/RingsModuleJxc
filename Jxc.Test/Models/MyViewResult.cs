using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Text;
using System.Web.UI;
using System.Text.RegularExpressions;
using CsQuery;
using System.Xml;

namespace Rings.Models
{
    public class MyViewResult : ViewResult
    {
        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (String.IsNullOrEmpty(ViewName))
            {
                ViewName = context.RouteData.GetRequiredString("action");
            }

            ViewEngineResult result = null;

            if (View == null)
            {
                result = FindView(context);
                View = result.View;
            }
            
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            HtmlTextWriter tw = new HtmlTextWriter(sw);

            TextWriter writer = context.HttpContext.Response.Output;

            ViewContext viewContext = new ViewContext(context, View, ViewData, TempData, writer);
            View.Render(viewContext, tw);

            string s = sb.ToString();

            bool isallowed = true;

            CQ limitcq = CQ.CreateDocument(s);
            CQ limitelment=limitcq.Select("div[data-pagelimit]");
             
            if (limitelment.Length>0)
            {
                string limitname = limitelment[0]["data-pagelimit"];
                isallowed = context.HttpContext.User.Identity.GetAccount().IsAllowed(limitname);
            }

            if (!isallowed)
            {
                #region 权限不符   
                CQ limitcq2 = limitelment.First().Html("<div class=\"alert alert-danger\" role=\"alert\">" 
                    + StringHelper.GetString("您没有相关权限，请和系统管理员联系！",context.HttpContext.User.Identity.GetAccount().ApplicationId) + "</div>");

                s = (limitcq2.Render());
                #endregion
            }

            #region 移除没有权限的元素
            string pattern = "ng-limit=\".*?\"";
            this.context = context;
            s = Regex.Replace(s, pattern, new MatchEvaluator(OutPutMatch));

            CQ cq = CQ.CreateDocument(s);
            CQ cq2 = cq.Select("[ng-limit='false']").Remove();
            string newhtml = cq2.Render();
            #endregion

            #region 替换service的短链接
            string viewpath=(result.ViewEngine as MyViewEngine).ViewPath;
            string viewfilepath=context.HttpContext.Server.MapPath(viewpath);
            string viewdirectorypath = new FileInfo(viewfilepath).Directory.FullName;
            string shortconfigpath = Path.Combine(viewdirectorypath,"short.config.xml");
            if (File.Exists(shortconfigpath))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(shortconfigpath);

                XmlNodeList nodes= doc.DocumentElement.SelectNodes("/shorts/short");
                foreach (XmlNode node in nodes)
                {
                    string shortname = node.Attributes["name"].Value;
                    string shortpath = node.Attributes["path"].Value;
                    newhtml = newhtml.Replace(shortname,shortpath);
                }
            }

            #endregion
             
            writer.Write(newhtml);
             
            if (result != null)
            {
                result.ViewEngine.ReleaseView(context, View);
            }


        }

        private ControllerContext context;

        private string OutPutMatch(Match match)
        {
            string limitname = match.Value.Substring(10, match.Value.Length - 11);
            return "ng-limit=\"" + context.HttpContext.User.Identity.GetAccount().IsAllowed(limitname).ToString().ToLower() + "\"";
        }
    }
}