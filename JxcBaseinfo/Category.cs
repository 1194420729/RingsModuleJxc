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

namespace JxcBaseinfo
{
    public class Category : MarshalByRefObject
    {
        private string tablename = "category";

        public Object Categorys(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);
        
            return CategoryHelper.GetCategoryTreeData(dic["classname"].ToString());
        }

        [MyLog("编辑类别")]
        public Object EditSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);

            int id = Convert.ToInt32(dic["id"]);
            string name = dic["name"].ToString();

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from category where id=" + id);
                model.content["name"] = name;

                db.Edit(tablename, model);
                db.SaveChanges();
            }
            return new { message="ok"};
        }

        [MyLog("添加类别")]
        public Object AddSave(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);

            int parentid = Convert.ToInt32(dic["parentid"]);
            string name = dic["name"].ToString();
            string classname = dic["classname"].ToString();

            using (DBHelper db = new DBHelper())
            {
                TableModel model = new TableModel()
                {
                    id = 0,
                    content = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(new { name = name, classname = classname, parentid = parentid }))
                };

                db.Add(tablename, model);

                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        [MyLog("删除类别")]
        public Object Delete(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);

            int id = Convert.ToInt32(dic["id"]);
            var ids = CategoryHelper.GetChildrenIds(id);

            using (DBHelper db = new DBHelper())
            {
                foreach (int cid in ids)
                {
                    db.Remove(tablename, cid);
                }
                db.Remove(tablename, id);

                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        [MyLog("搬移类别")]
        public Object Move(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);

            int id = Convert.ToInt32(dic["id"]);
            int parentid = Convert.ToInt32(dic["parentid"]);

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from category where id=" + id);
                if (parentid == 0)
                {
                    model.content.Remove("parentid");
                }
                else
                {
                    model.content["parentid"] = parentid;
                }
                db.Edit(tablename, model);
                db.SaveChanges();
            }
            return new { message = "ok" };
        }

        [MyLog("搬移数据")]
        public Object MoveData(string parameters)
        {
            IDictionary<string, object> dic = ParameterHelper.ParseParameters(parameters);

            int id = Convert.ToInt32(dic["id"]);
            int parentid = Convert.ToInt32(dic["parentid"]);

            using (DBHelper db = new DBHelper())
            {
                var model = db.First("select * from category where id=" + id);
                string classname = model.content.Value<String>("classname");

                db.ExcuteNoneQuery("update \"" + classname + "\" set content=jsonb_set(content,'{categoryid}','" + parentid + "'::jsonb,false) where content->>'categoryid'='" + id + "'");

                db.SaveChanges();
            }
            return new { message = "ok" };
        }
    }
}
