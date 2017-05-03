using Rings.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace Jxc.Utility
{
    public static class ContextServiceHelper
    {
        public static string MapPath(string path)
        {
            string url = PluginContext.Current.ContextServiceUrl+"/mappath?path="+HttpUtility.UrlEncode(path);
            
            WebClient wc = new WebClient();                  
            wc.Encoding = Encoding.UTF8;
            string filepath = wc.DownloadString(url);

            return filepath;
        }
    }
}
