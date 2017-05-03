using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Jxc.Utility
{
    public static class NPOIHelper
    {
        public static object GetCellValue(this ICell cell)
        {
            if (cell == null)
                return "";

            if (cell.CellType == CellType.Blank)
                return "";
            else if (cell.CellType == CellType.Boolean)
                return cell.BooleanCellValue;
            else if (cell.CellType == CellType.Error)
                return "";
            else if (cell.CellType == CellType.Formula)
                return "";
            else if (cell.CellType == CellType.Numeric)
                return cell.NumericCellValue;
            else if (cell.CellType == CellType.String)
                return cell.StringCellValue;
            else
                return "";
        }

        public static byte[] GetExcelStream(this IList list)
        {
            return GetExcelStream(list, new List<string>());
        }

        public static byte[] GetExcelStream(this IList list, string[] fields)
        {
            return GetExcelStream(list, new List<string>(fields));
        }

        public static byte[] GetExcelStream(this IList list, List<string> fields)
        {

            object template = list[0];
            Dictionary<string, PropertyInfo> dic = new Dictionary<string, PropertyInfo>();

            HSSFWorkbook workbook = new HSSFWorkbook();

            PropertyInfo[] ps = template.GetType().GetProperties();

            foreach (PropertyInfo pi in ps)
            {
                object[] typeAttributes = pi.GetCustomAttributes(false);
                foreach (Attribute attr in typeAttributes)
                {
                   if (attr is ExcelDisplayAttribute && (fields.Count == 0 || fields.Contains(pi.Name)))
                    {
                        string displayname = ((ExcelDisplayAttribute)attr).GetName();
                        dic.Add(displayname, pi);

                        break;
                    }

                }
            }

            ISheet sheet = workbook.CreateSheet("Sheet1");

            IRow r1 = sheet.CreateRow(0);
            int colnum = 0;
            foreach (string key in dic.Keys)
            {
                r1.CreateCell(colnum).SetCellValue(key);
                colnum++;
            }

            int i = 1;
            foreach (Object obj in list)
            {
                IRow r = sheet.CreateRow(i);
                int j = 0;
                foreach (string key in dic.Keys)
                {
                    r.CreateCell(j).SetCellValue(dic[key].GetValue(obj, null) == null ? "" : dic[key].GetValue(obj, null).ToString());
                    j++;
                }

                i++;
            }

            //添加合计
            if (list.Count > 0)
            {
                IRow r = sheet.CreateRow(i);
                int j = 0;
                object obj = list[0];
                foreach (string key in dic.Keys)
                {
                    if (dic[key].PropertyType.FullName == typeof(decimal).FullName || dic[key].PropertyType.FullName == typeof(decimal?).FullName)
                    {
                        decimal sum = decimal.Zero;
                        foreach (Object item in list)
                        {
                            sum += dic[key].GetValue(item, null) == null ? decimal.Zero : Convert.ToDecimal(dic[key].GetValue(item, null));
                        }
                        r.CreateCell(j).SetCellValue(sum.ToString());

                    }
                    j++;
                }
            }
            
            MemoryStream ms = new MemoryStream();
            workbook.Write(ms);
            ms.Flush();
            byte[] bytes = ms.ToArray();
            ms.Close();
            return bytes;
        }

        public static byte[] GetExcelStream(this DataTable list)
        { 
            HSSFWorkbook workbook = new HSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            IRow r1 = sheet.CreateRow(0);
            int colnum = 0;
            foreach (DataColumn col in list.Columns)
            {
                r1.CreateCell(colnum).SetCellValue(col.ColumnName);
                colnum++;
            }
             
            int i = 1;
            foreach (DataRow row in list.Rows)
            {
                IRow r = sheet.CreateRow(i);
                int j = 0;
                foreach (DataColumn col in list.Columns)
                {
                    if (col.DataType.FullName == typeof(decimal).FullName || col.DataType.FullName == typeof(int).FullName)
                    {
                        if (row[col] != DBNull.Value)
                        {
                            r.CreateCell(j).SetCellValue(Convert.ToDouble(row[col]));
                        }                        
                    }
                    else
                    {
                        r.CreateCell(j).SetCellValue(row[col] == DBNull.Value ? "" : row[col].ToString());
                    }
                    j++;
                }

                i++;
            }
             
            MemoryStream ms = new MemoryStream();
            workbook.Write(ms);
            ms.Flush();
            byte[] bytes = ms.ToArray();
            ms.Close();
            return bytes;
        }
    }
}
