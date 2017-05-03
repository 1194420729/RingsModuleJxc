using Newtonsoft.Json;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;

namespace Jxc.Utility
{
    public class ParameterHelper
    {
        public static QueryParameter GetQueryParameters(string parameter)
        {
            return JsonConvert.DeserializeObject<QueryParameter>(parameter);
        }

        public static IDictionary<string, object> ParseParameters(string parameters)
        {
            parameters = HttpUtility.UrlDecode(parameters);

            if (parameters.Trim().StartsWith("{") || parameters.Trim().StartsWith("["))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(parameters);
            }
            else
            {
                Dictionary<string, object> dic = new Dictionary<string, object>();
                string[] ss = parameters.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in ss)
                {
                    string[] ss2 = s.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (dic.ContainsKey(ss2[0])) continue;
                    if (ss2.Length > 1)
                    {
                        dic.Add(ss2[0], ss2[1]);
                    }
                    else
                    {
                        dic.Add(ss2[0], string.Empty);
                    }
                }
                return dic;
            }
        }

        private IDictionary<string, object> dic = null;
        public ParameterHelper(string parameter)
        {
            dic = ParseParameters(parameter);
        }

        public T GetParameterValue<T>(string name)
        {
            if (dic.ContainsKey(name) == false || dic[name]==null)
            {
                throw new System.Collections.Generic.KeyNotFoundException();
            }
            
            return (T)Convert.ChangeType(dic[name].ToString(),typeof(T));
        }
         
    }

    public class QueryParameter
    {
        public int? page { get; set; }
        public int? count { get; set; }
        public string sorting { get; set; }
        public string filter { get; set; }
    }
}
