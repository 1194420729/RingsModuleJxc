using Newtonsoft.Json.Linq;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public static class CategoryHelper
    {
        public static ICollection<TableModel> CategoryList(string categoryclass)
        {
            var account = PluginContext.Current.Account;

            if (account.UserName.ToLower() == "admin" || account.Id == 1
                || (categoryclass != "customer" && categoryclass != "vendor" && categoryclass != "product"))
            {
                DBHelper db = new DBHelper();
                return db.Where("select * from category where content->>'classname'='" + categoryclass + "'");
            }
            else
            {
                return GetCategoryListByScope(categoryclass);
            }
        }

        private static ICollection<TableModel> GetCategoryListByScope(string categoryclass)
        {
            var account = PluginContext.Current.Account;
            DBHelper db = new DBHelper();
            var employee = db.First("employee", account.Id);

            var scope = employee.content.Value<JObject>("scope").Value<JArray>(categoryclass).Values<int>().ToList();

            if (scope.Count == 1 && scope[0] == 0)//全部范围
                return db.Where("select * from category where content->>'classname'='" + categoryclass + "'");
            else
            {
                //部分授权    
                string ids=string.Join(",", scope);
                var categorys= db.Where("select * from category where content->>'classname'='" + categoryclass + "' and id in ("+ids+")");
                bool allparentin = false;
                while (!allparentin)
                {
                    List<TableModel> parents = new List<TableModel>();
                    foreach (var item in categorys)
                    {
                        int parentid = item.content.Value<int>("parentid");
                        if (parentid == 0) continue;
                        if (categorys.Count(c => c.id == parentid) > 0 || parents.Count(c => c.id == parentid) > 0) continue;
                        var parent = db.First("category",parentid);
                        parents.Add(parent);
                    }
                    if (parents.Count > 0)
                        categorys.AddRange(parents);
                    else
                        allparentin = true;
                }

                return categorys;
            }
        }

        public static ICollection<TreeData> GetCategoryTreeData(string categoryclass)
        {
            var items = CategoryList(categoryclass);

            var list = (from c in items
                        select new TreeData
                        {
                            id = c.id.ToString(),
                            parent = c.content["parentid"] == null ? "0" : c.content.Value<string>("parentid"),
                            text = c.content.Value<string>("name"),
                            icon = "fa fa-folder-o fa-fw"
                        }).ToList();

            list.Insert(0, new TreeData()
            {
                icon = "fa fa-folder-o fa-fw",
                id = "0",
                parent = "#",
                text = StringHelper.GetString("全部分类")
            });

            return list;
        }

        public static ICollection<TreeData> GetPartCategoryTreeData(string categoryclass, int rootid)
        {
            var ids = GetChildrenIds(rootid);
            DBHelper db = new DBHelper();
            var root = db.First("category", rootid);

            var items = CategoryList(categoryclass);

            var list = (from c in items
                        where ids.Contains(c.id)
                        select new TreeData
                        {
                            id = c.id.ToString(),
                            parent = c.content["parentid"] == null ? "0" : c.content.Value<string>("parentid"),
                            text = c.content.Value<string>("name"),
                            icon = "fa fa-folder-o fa-fw"
                        }).ToList();

            list.Insert(0, new TreeData()
            {
                icon = "fa fa-folder-o fa-fw",
                id = rootid.ToString(),
                parent = "#",
                text = root.content.Value<string>("name")
            });

            return list;
        }

        public static ICollection<int> GetChildrenIds(int parentid)
        {
            List<int> list = new List<int>();
            DBHelper db = new DBHelper();
            var sons = db.Where("select * from category where  content->>'parentid'='" + parentid + "'");
            list.AddRange(sons.Select(c => c.id));
            if (sons.Count > 0)
            {
                foreach (var item in sons)
                {
                    var children = GetChildrenIds(item.id);
                    list.AddRange(children);
                }
            }

            return list;
        }
    }

    [Serializable]
    public class TreeData
    {
        public string id { get; set; }
        public string parent { get; set; }
        public string text { get; set; }
        public string icon { get; set; }
        public TreeDataState state { get; set; }
    }

    [Serializable]
    public class TreeDataState
    {
        public bool? opened { get; set; }
        public bool? disabled { get; set; }
        public bool? selected { get; set; }

    }
}
