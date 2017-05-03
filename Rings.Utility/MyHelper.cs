using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public static class MyHelper
    {
        public static string TryParseCode(string lastcode)
        {
            if(string.IsNullOrEmpty(lastcode)) return string.Empty;

            int i = 0;
            bool b = int.TryParse(lastcode, out i);
            if (b)
            {
                return (i + 1).ToString().PadLeft(lastcode.Length, '0');
            }
            else
            {
                List<char> numbers = new List<char>() { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
                Stack<char> stack = new Stack<char>();
                var chars = lastcode.ToArray();
                for (int j = chars.Length - 1; j >= 0; j--)
                {
                    if (numbers.Contains(chars[j]))
                    {
                        stack.Push(chars[j]);
                    }
                    else
                    {
                        break;
                    }
                }

                if (stack.Count > 0)
                {
                    string s = "";

                    while (stack.Count > 0)
                    {
                        s += stack.Pop();
                    }

                    int ls = int.Parse(s) + 1;

                    return lastcode.Substring(0, lastcode.Length - s.Length) + ls.ToString().PadLeft(s.Length, '0');
                }
            }

            return string.Empty;
        }

        public static string GetIds(this DataTable dt,string emptyresult="0",string columnname="id")
        {
            if (dt.Rows.Count == 0) return emptyresult;

            StringBuilder sb = new StringBuilder();
            foreach (DataRow row in dt.Rows)
            {
                sb.Append(row[columnname] + ",");
            }

            return sb.ToString().Substring(0, sb.Length - 1);
        }

        public static string GetIds(this List<TableModel> list, string emptyresult = "0")
        {
            if (list.Count == 0) return emptyresult;

            StringBuilder sb = new StringBuilder();
            foreach (var item in list)
            {
                sb.Append(item.id + ",");
            }

            return sb.ToString().Substring(0, sb.Length - 1);
        }
    }
}
