using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Rings.Models
{
    public class AllowedIPOnlyAttribute : AuthorizeAttribute
    {
        private string iplist = string.Empty;

        public AllowedIPOnlyAttribute(string iplist)
        {
            this.iplist = string.IsNullOrEmpty(iplist)?"":iplist;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            string[] ss = iplist.Split(new char[]{','},StringSplitOptions.RemoveEmptyEntries);
            string clientIp = filterContext.HttpContext.Request.UserHostAddress;
            if (!ss.Contains(clientIp.ToLower()))
            {
                filterContext.Result = new HttpUnauthorizedResult();
            }
        }

    }

    public class LocalhostOnlyAttribute : AllowedIPOnlyAttribute
    {
        public LocalhostOnlyAttribute():base("127.0.0.1,localhost,::1")
        {

        }
    }
}