using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Rings.Models;
using System.Reflection;
using System.Text;
using System.Runtime.Remoting.Lifetime;
using System.Configuration;

namespace Baseingfo.Test.Controllers
{
    [Authorize]
    public class ServiceController : Controller
    {
        [ValidateInput(false)]
        [HttpPost]
        public ActionResult ServiceFactory()
        {
            string parameters = "";
            if (Request.InputStream.Length > 0)
            {
                StreamReader sr = new StreamReader(Request.InputStream);
                parameters = sr.ReadToEnd();
                sr.Close();
            }
            else if (Request.QueryString.Count > 0)
            {
                parameters = JsonConvert.SerializeObject(Request.QueryString);
            }

            var virtualPath = Request.Path;
            string[] ss = virtualPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var account = User.Identity.GetAccount();
            List<string> paths = new List<string>();
            string componentname = ss[ss.Length - 4];
            string assemblyname = ss[ss.Length - 3];
            string classname = ss[ss.Length - 2];
            string methodname = ss[ss.Length - 1];

            paths.Add(Server.MapPath("~/Views/" + account.ApplicationId + "/"+account.Language+"/"+  componentname + "/" + assemblyname + ".dll"));
            paths.Add(Server.MapPath("~/Views/" + account.ApplicationId + "/" + componentname + "/" + assemblyname + ".dll"));
            paths.Add(Server.MapPath("~/Views/Default/" + account.Language + "/" + componentname + "/" + assemblyname + ".dll"));
            paths.Add(Server.MapPath("~/Views/Default/" + componentname + "/" + assemblyname + ".dll"));

            string dllpath = "";
            foreach (string path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    dllpath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(dllpath))
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(new FileNotFoundException("请求："+virtualPath+"，对应的程序集不存在"));
                return Json(new { message="服务不存在！"});
            }

            AppSettingsReader reader = new AppSettingsReader();
            string contextserviceurl = reader.GetValue("contextservice",typeof(string)).ToString();

            //通过反射调用服务 
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            setup.PrivateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
            setup.LoaderOptimization = LoaderOptimization.MultiDomain;
            setup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            setup.ApplicationTrust = AppDomain.CurrentDomain.SetupInformation.ApplicationTrust;
            
            AppDomain domain=AppDomain.CreateDomain(Guid.NewGuid().ToString(),null,setup);
            PluginLoader loader= domain.CreateInstanceAndUnwrap(typeof(PluginLoader).Assembly.FullName, typeof(PluginLoader).FullName) as PluginLoader;
            string resultjson = "";
            try
            {
                resultjson = loader.Run(dllpath, classname, methodname, parameters, account, contextserviceurl);
            }
            catch (Exception ex)
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                resultjson = JsonConvert.SerializeObject(new { message=ex.InnerException==null? ex.Message:ex.InnerException.Message});
            }
             
            AppDomain.Unload(domain);

            return Content(resultjson,"application/json",Encoding.UTF8);
        }
    }
     
}
