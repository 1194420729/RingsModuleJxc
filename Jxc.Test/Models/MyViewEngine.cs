using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Rings.Models
{
    public class MyViewEngine : RazorViewEngine
    {
        private string viewPath = null;
        private string partialPath = null;

        public MyViewEngine()
        {

        }

        public override ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
        {
            if (HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated)
            {
                var account = HttpContext.Current.User.Identity.GetAccount();

                if (partialViewName.StartsWith("/"))
                {
                    partialViewName = partialViewName.Substring(1);
                }

                PartialViewLocationFormats = new[]
                {
                    "~/Views/"+account.ApplicationId+"/"+account.Language+"/{0}.cshtml",
                    "~/Views/"+account.ApplicationId+"/{0}.cshtml", 
                    "~/Views/Default/"+account.Language+"/{0}.cshtml" ,
                    "~/Views/Default/{0}.cshtml" ,
                    "~/Views/{1}/"+account.Language+"/{0}.cshtml",
                    "~/Views/{1}/{0}.cshtml"
                };
            }
            else
            {
                PartialViewLocationFormats = new[]
                {
                    "~/Views/{1}/{0}.cshtml",
                    "~/Views/{1}/{0}.vbhtml",
                    "~/Views/Shared/{0}.cshtml",
                    "~/Views/Shared/{0}.vbhtml"
                };
            }

            return base.FindPartialView(controllerContext, partialViewName, useCache);
        }

        public override ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {
            if (HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated)
            {
                var account = HttpContext.Current.User.Identity.GetAccount();
                 
                ViewLocationFormats = new[]
                {
                    "~/Views/"+account.ApplicationId+"/"+account.Language+"/{0}.cshtml",
                    "~/Views/"+account.ApplicationId+"/{0}.cshtml", 
                    "~/Views/Default/"+account.Language+"/{0}.cshtml" ,
                    "~/Views/Default/{0}.cshtml" ,
                    "~/Views/{1}/"+account.Language+"/{0}.cshtml",
                    "~/Views/{1}/{0}.cshtml"
                };
            }
            else
            {
                ViewLocationFormats = new[]
                {
                    "~/Views/{1}/{0}.cshtml",
                    "~/Views/{1}/{0}.vbhtml",
                    "~/Views/Shared/{0}.cshtml",
                    "~/Views/Shared/{0}.vbhtml"
                };
            }

            return base.FindView(controllerContext, viewName, masterName, useCache);
        }

        protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
        {
            this.partialPath = partialPath;

            return base.CreatePartialView(controllerContext, partialPath);
        }

        protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
        {
            this.viewPath = viewPath;

            return base.CreateView(controllerContext,viewPath,masterPath);
        }

        public string ViewPath { get { return this.viewPath; } }
        public string PartialPath { get { return this.partialPath; } }
    }
}