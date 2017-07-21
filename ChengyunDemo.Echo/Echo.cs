using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using System.Data; 
using System.IO;
using System.Web;
using System.Net;
using System.Configuration;
using Jxc.Utility;

namespace ChengyunDemo.Echo
{
    public class Echo : MarshalByRefObject
    {
        public Object GetModuleContent(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);

            DBHelper db = new DBHelper();

            return new {  };

        }
         

    }


}
