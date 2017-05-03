using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Rings.Models;

namespace Baseingfo.Test.Controllers
{
    [Authorize]
    public class CommonServiceController : Controller
    { 
        public ActionResult RenderTemplate()
        {
            var account = User.Identity.GetAccount();

            var virtualPath = Request.Path;//获取虚拟路径
             
            ViewBag.QueryString = Request.QueryString;
            ViewBag.QueryStringDictionary = ToDictionary(Request.QueryString);

            string[] ss = virtualPath.Split(new char[]{'/'},StringSplitOptions.RemoveEmptyEntries);
            if (ss.Length==0)
            {
                return this.MyView("home/index");
            }
            else
            {
                string path = "";
                foreach (var s in ss)
                {
                    path += s+"/";
                }
                path = path.Substring(0,path.Length-1);
                 
                return this.MyView(path);
            }

            
        }

        private IDictionary<string, object> ToDictionary(System.Collections.Specialized.NameValueCollection nv)
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
    }
}
