using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rings.Models;
using Npgsql;
using System.Data;
using Jxc.Utility;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Net;
using System.Configuration;

namespace JxcInit
{
    public class InitOver : MarshalByRefObject
    {
        public Object GetStatus(string parameters)
        {
            DBHelper db = new DBHelper();
            var options = db.First("select * from option");

            bool notinitover = options.content["initoverdate"] == null;
            string initoverdate = "";
            if (!notinitover)
            {
                initoverdate = options.content.Value<string>("initoverdate");
            }

            return new { initoverdate = initoverdate, notinitover = notinitover };
        }

        [MyLog("系统开账")]
        public Object InitOverSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string initoverdate = ph.GetParameterValue<string>("initoverdate");

            using (DBHelper db = new DBHelper())
            {
                var options = db.First("select * from option");
                 
                if (options.content["initoverdate"] != null)
                {
                    return new { message = StringHelper.GetString("系统已经开账，不能重复开账！") };
                } 
            }

            InitOverHelper.InitOver(initoverdate);

            return new { message = "ok" };

        }
    }


}
