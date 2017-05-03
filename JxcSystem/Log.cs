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
    public class Log : MarshalByRefObject
    {
        private string tablename = "log";

        public Object List(string parameters)
        {
            QueryParameter para = ParameterHelper.GetQueryParameters(parameters);

            var mysorting = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.sorting);
            var myfilter = JsonConvert.DeserializeObject<Dictionary<string, string>>(para.filter);

            int pageindex = para.page ?? 1;
            int pagesize = para.count ?? 25;

            StringBuilder sb = new StringBuilder();
             
            foreach (string field in myfilter.Keys)
            {
                if (string.IsNullOrEmpty(myfilter[field]))
                    continue;

                string f = myfilter[field].Trim();
                if (string.IsNullOrEmpty(f))
                    continue;

                if (field.StartsWith("starttime") || field.StartsWith("endtime"))
                {
                    DateTime val = DateTime.Now;
                    bool isdate = DateTime.TryParse(myfilter[field], out val);

                    if (isdate)
                    {
                        string[] ss = field.Split('_');
                        string fieldname = ss[1];
                        if (field.StartsWith("starttime"))
                        {
                            sb.AppendFormat(" and content->>'{0}' >= '{1}'", fieldname, val.ToString("yyyy-MM-dd"));
                        }
                        else
                        {
                            sb.AppendFormat(" and content->>'{0}' < '{1}'", fieldname, val.AddDays(1).ToString("yyyy-MM-dd"));
                        }
                         
                    }
                }
                else {
                    sb.AppendFormat(" and content->>'{0}' ilike '%{1}%'", field.ToLower(), f);
                }

                
            }

            StringBuilder sborder = new StringBuilder();
            if (mysorting.Count > 0) sborder.Append(" order by ");
            int i = 0;
            foreach (string field in mysorting.Keys)
            {
                i++;
                sborder.AppendFormat(" content->>'{0}' {1} {2}", field.ToLower(), mysorting[field], i == mysorting.Count ? "" : ",");
            }

            if (mysorting.Count == 0)
            {
                sborder.Append(" order by id desc ");
            }

            sborder.AppendFormat(" limit {0} offset {1} ", pagesize, pagesize * pageindex - pagesize);

            int recordcount = 0;
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Query(this.tablename, sb.ToString(), sborder.ToString(), out recordcount);
             
            return new { resulttotal = recordcount, data = list };
        }

        public Object Clear(string parameters)
        {
            using (DBHelper db = new DBHelper())
            {
                db.Truncate(tablename);
                db.SaveChanges();
            }
            return new { message = "ok" };

        }

    }
}
