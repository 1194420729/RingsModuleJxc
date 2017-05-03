using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rings.Models;
using Jxc.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web.Security;

namespace JxcSystem
{
    public class Option : MarshalByRefObject
    {
        private string tablename = "option";

        public Object List(string parameters)
        {
            DBHelper db = new DBHelper();
            var item = db.First("select * from option");

            return new { data = item.content, initovered = item.content["initoverdate"] != null };
        }

        public Object Save(string parameters)
        {
            var content = JsonConvert.DeserializeObject<JObject>(parameters);

            using (DBHelper db = new DBHelper())
            {
                var item = db.First("select * from option");
                item.content = content;
                db.Edit(tablename, item);
                db.SaveChanges();
            }
            return new { message = "ok" };
        }
         
    }
}
