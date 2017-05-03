using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Baseingfo.Test
{
    public class ServiceRoute : RouteBase
    {
        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            var virtualPath = httpContext.Request.AppRelativeCurrentExecutionFilePath + httpContext.Request.PathInfo;//获取相对路径

            virtualPath = virtualPath.Substring(2);
            string[] ss = virtualPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            //登录或者注销，不处理
            if (ss.Length > 0 && ss[0].ToLower() == "account")
            {
                return null;
            }

            //打印，不处理
            if (ss.Length > 0 && ss[0].ToLower() == "print")
            {
                return null;
            }
            
            //上传文件，不处理
            if (ss.Length > 0 && ss[0].ToLower() == "upload")
            {
                return null;
            }
            
            //httpcontext服务，不处理
            if (ss.Length > 0 && ss[0].ToLower() == "contextservice")
            {
                return null;
            }

            //数据接口，全部转到ServiceFactory
            if (ss.Length > 0 && ss[0].ToLower() == "service")
            { 
                var data = new RouteData(this, new MvcRouteHandler());//声明一个RouteData，添加相应的路由值
                data.Values.Add("controller", "Service");
                data.Values.Add("action", "ServiceFactory");

                return data;
            }

            //html模板，全部转到:CommonService\RenderTemplate
            var routedata = new RouteData(this, new MvcRouteHandler());//声明一个RouteData，添加相应的路由值
            routedata.Values.Add("controller", "CommonService");
            routedata.Values.Add("action", "RenderTemplate");

            return routedata;
        }

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
        {
            return null;
        }
    }
}