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
using System.Xml;
using System.Text.RegularExpressions;
using CsQuery;
using System.Web.Security;

namespace HomePage
{
    public class Portal : MarshalByRefObject
    {
        public Object Init(string parameters)
        {
            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);
            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var names = homepagecomponents.Select(c => c.Value<string>("name"));

            var components = LoadComponents();
            var dependency = LoadDependency(components.Where(c => names.Contains(c.name)).ToList());

            return new { mycomponents = homepagecomponents, dependency = dependency };
        }

        public Object InitConfig(string parameters)
        {
            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);
            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }
            var components = LoadComponents();

            return new { components = components, mycomponents = homepagecomponents, dependency = LoadDependency(components) };
        }

        public Object SaveConfig(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string config = ph.GetParameterValue<string>("config");
            var list = JsonConvert.DeserializeObject<JArray>(config);

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            if (employeeconfig == null)
            {
                employeeconfig = new TableModel() { content = new JObject() };
                employeeconfig.content["employeeid"] = PluginContext.Current.Account.Id;
                employeeconfig.content["homepagecomponents"] = list;
                db.Add("employeeconfig", employeeconfig);
            }
            else
            {
                #region 保留组件实例自定义存储项
                var homepagecomponents = new JArray();
                if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
                {
                    homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
                }

                foreach (var item in list)
                {
                    var component = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == item.Value<string>("guid"));
                    if (component != null && component["storage"] != null)
                    {
                        item["storage"] = component["storage"];
                    }
                }
                #endregion

                employeeconfig.content["homepagecomponents"] = list;
                db.Edit("employeeconfig", employeeconfig);
            }

            db.SaveChanges();

            return new { message = "ok" };
        }

        private List<Component> LoadComponents()
        {
            List<Component> list = new List<Component>();

            //查找定制目录下的组件配置 
            string applicationpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\" + PluginContext.Current.Account.RootApplicationId);
            string[] subdirs = new string[] { };
            if (Directory.Exists(applicationpath))
            {
                subdirs = Directory.GetDirectories(applicationpath, "*", SearchOption.TopDirectoryOnly);
            }

            foreach (string dir in subdirs)
            {
                string path = Path.Combine(dir, "component.config.xml");
                if (File.Exists(path))
                {
                    list.AddRange(ReadConfigFile(path));
                }
            }

            //查找默认目录下的组件配置
            string defaultpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\Default");
            string[] defaultsubdirs = Directory.GetDirectories(defaultpath, "*", SearchOption.TopDirectoryOnly);
            foreach (string subdir in defaultsubdirs)
            {
                if (subdirs.Contains(subdir)) continue;
                string path = Path.Combine(subdir, "component.config.xml");
                if (File.Exists(path))
                {
                    list.AddRange(ReadConfigFile(path));
                }

            }

            return list;
        }

        private List<Component> ReadConfigFile(string path)
        {
            List<Component> list = new List<Component>();

            XmlDocument doc = new XmlDocument();
            doc.Load(path);

            XmlNodeList nodes = doc.DocumentElement.SelectNodes("/components/component");

            foreach (XmlNode node in nodes)
            {
                Component component = new Component()
                {
                    name = node.Attributes["name"].Value,
                    title = node.Attributes["title"].Value,
                    path = node.Attributes["path"].Value.StartsWith("/component") ? node.Attributes["path"].Value : ("/component" + node.Attributes["path"].Value),
                    limit = node.Attributes["limit"] == null ? "" : node.Attributes["limit"].Value,
                    width = node.Attributes["width"].Value,
                    height = node.Attributes["height"].Value
                };

                if (string.IsNullOrEmpty(component.limit) || PluginContext.Current.Account.IsAllowed(component.limit))
                    list.Add(component);
            }

            return list;
        }

        private List<Dependency> GetDependency(Component component)
        {
            List<Dependency> list = new List<Dependency>();

            var account = PluginContext.Current.Account;

            string[] paths = new[]
                {
                    "Views\\"+account.RootApplicationId+"\\"+account.Language+"\\{0}.cshtml",
                    "Views\\"+account.RootApplicationId+"\\{0}.cshtml", 
                    "Views\\Default\\"+account.Language+"\\{0}.cshtml" ,
                    "Views\\Default\\{0}.cshtml" 
                };

            foreach (string path in paths)
            {
                string componentpath = component.path.Substring(10).Replace("/", "\\");
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(path, componentpath));
                if (File.Exists(p))
                {
                    string html = File.ReadAllText(p);
                    CQ cq = CQ.CreateFragment(html);

                    var js = cq.Select("script[data-define]");
                    foreach (var link in js)
                    {
                        string defined = link["data-define"];
                        var dd = list.FirstOrDefault(c => c.defined == defined);
                        if (dd == null)
                        {
                            dd = new Dependency() { defined = defined };
                            list.Add(dd);
                        }


                        if (string.IsNullOrEmpty(link["src"]))
                        {
                            #region 替换service的短链接
                            string viewfilepath = p;
                            string viewdirectorypath = new FileInfo(viewfilepath).Directory.FullName;
                            string shortconfigpath = Path.Combine(viewdirectorypath, "short.config.xml");
                            if (File.Exists(shortconfigpath))
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(shortconfigpath);

                                XmlNodeList nodes = doc.DocumentElement.SelectNodes("/shorts/short");
                                string outhtml = link.OuterHTML;
                                foreach (XmlNode node in nodes)
                                {
                                    string shortname = node.Attributes["name"].Value;
                                    string shortpath = node.Attributes["path"].Value;
                                    outhtml = outhtml.Replace(shortname, shortpath);
                                }
                                dd.js.Add(outhtml);
                            }
                            #endregion
                        }
                        else
                        {
                            dd.js.Add(link.OuterHTML);
                        }


                    }

                    var css = cq.Select("link[data-define]");
                    foreach (var link in css)
                    {
                        string defined = link["data-define"];
                        var dd = list.FirstOrDefault(c => c.defined == defined);
                        if (dd == null)
                        {
                            dd = new Dependency() { defined = defined };
                            list.Add(dd);
                        }
                        dd.css.Add(link.OuterHTML);

                    }
                    break;
                };
            }

            return list;
        }

        private List<Dependency> LoadDependency(List<Component> components)
        {
            List<Dependency> list = new List<Dependency>();

            foreach (var component in components)
            {
                var dds = GetDependency(component);
                foreach (var item in dds)
                {
                    if (list.FirstOrDefault(c => c.defined == item.defined) == null) list.Add(item);
                }
            }

            return list;
        }

        public Object QuickLinkInit(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myquicklinks = new JArray();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myquicklinks = JsonConvert.DeserializeObject<JArray>(instance.Value<string>("storage"));
            }

            return new { myquicklinks = myquicklinks };
        }

        public Object GetAllQuickLinks(string parameters)
        {
            return new { quicklinks = GetAllLinks() };
        }

        public Object QuickLinkAddSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            string link = ph.GetParameterValue<string>("link");
            string title = ph.GetParameterValue<string>("title");
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);

            var homepagecomponents = new JArray();
            if (employeeconfig != null && employeeconfig.content["homepagecomponents"] != null)
            {
                homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");
            }

            var myquicklinks = new JArray();
            var instance = homepagecomponents.FirstOrDefault(c => c.Value<string>("guid") == id);
            if (instance != null && instance["storage"] != null)
            {
                myquicklinks = JsonConvert.DeserializeObject<JArray>(instance.Value<string>("storage"));
            }

            JObject linkobj = new JObject();
            linkobj.Add("title", title);
            linkobj.Add("link", link);
            myquicklinks.Add(linkobj);

            instance["storage"] = JsonConvert.SerializeObject(myquicklinks);
            employeeconfig.content["homepagecomponents"] = homepagecomponents;

            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

        public Object QuickLinkRemoveSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int index = ph.GetParameterValue<int>("index");
            string id = ph.GetParameterValue<string>("id");

            DBHelper db = new DBHelper();

            var employeeconfig = db.FirstOrDefault("select * from employeeconfig where (content->>'employeeid')::int=" + PluginContext.Current.Account.Id);
            var homepagecomponents = employeeconfig.content.Value<JArray>("homepagecomponents");

            var instance = homepagecomponents.First(c => c.Value<string>("guid") == id);
            var myquicklinks = JsonConvert.DeserializeObject<JArray>(instance.Value<string>("storage"));

            myquicklinks.RemoveAt(index);

            instance["storage"] = JsonConvert.SerializeObject(myquicklinks);
            employeeconfig.content["homepagecomponents"] = homepagecomponents;

            db.Edit("employeeconfig", employeeconfig);

            db.SaveChanges();

            return new { message = "ok" };
        }

        public Object EditPasswordSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);

            string password = ph.GetParameterValue<string>("password");
            string md5 = FormsAuthentication.HashPasswordForStoringInConfigFile(password, "MD5");

            DBHelper db = new DBHelper();

            var employee = db.First("employee", PluginContext.Current.Account.Id);
            employee.content["password"] = md5;
            db.Edit("employee", employee);
            db.SaveChanges();

            return new { message = "ok" };
        }

        private List<QuickLink> GetAllLinks()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            #region 查找menu文件
            //当前用户自定义目录下的指定语言目录
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\" + PluginContext.Current.Account.RootApplicationId + "\\" + PluginContext.Current.Account.Language);

            string[] files = new string[] { };
            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path, "menu.cshtml", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    FileInfo fi = new FileInfo(file);
                    string modulename = fi.DirectoryName.ToLower().Replace(path.ToLower(), "");

                    dic.Add(modulename, file);
                }
            }

            //当前用户自定义目录下的默认语言目录
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\" + PluginContext.Current.Account.RootApplicationId);

            if (Directory.Exists(path))
            {
                string[] files2 = Directory.GetFiles(path, "menu.cshtml", SearchOption.AllDirectories);
                foreach (string file in files2)
                {
                    if (files.Contains(file)) continue;

                    FileInfo fi = new FileInfo(file);
                    string modulename = fi.DirectoryName.ToLower().Replace(path.ToLower(), "");

                    if (dic.ContainsKey(modulename) == false)
                        dic.Add(modulename, file);
                }
            }

            //默认目录下的指定语言目录
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\Default\\" + PluginContext.Current.Account.Language);

            string[] files3 = new string[] { };
            if (Directory.Exists(path))
            {
                files3 = Directory.GetFiles(path, "menu.cshtml", SearchOption.AllDirectories);
                foreach (string file in files3)
                {
                    FileInfo fi = new FileInfo(file);
                    string modulename = fi.DirectoryName.ToLower().Replace(path.ToLower(), "");

                    if (dic.ContainsKey(modulename) == false)
                        dic.Add(modulename, file);
                }
            }

            //默认目录下的默认语言目录
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views\\Default");

            if (Directory.Exists(path))
            {
                string[] files4 = Directory.GetFiles(path, "menu.cshtml", SearchOption.AllDirectories);
                foreach (string file in files4)
                {
                    if (files3.Contains(file)) continue;

                    FileInfo fi = new FileInfo(file);
                    string modulename = fi.DirectoryName.ToLower().Replace(path.ToLower(), "");

                    if (dic.ContainsKey(modulename) == false)
                        dic.Add(modulename, file);
                }
            }
            #endregion

            List<QuickLink> list = new List<QuickLink>();

            #region 读取menu文件内容

            foreach (string file in dic.Values)
            {
                string html = File.ReadAllText(file, Encoding.UTF8);
                CQ frag = CQ.CreateFragment(html);

                #region 去除没有权限的
                //去除没有权限的菜单项
                CQ lis = frag.Select("li[ng-limit]");
                for (int i = 0; i < lis.Length; i++)
                {
                    string limitname = lis[i].Attributes["ng-limit"];
                    bool isallowed = PluginContext.Current.Account.IsAllowed(limitname);
                    if (isallowed)
                    {
                        var li = lis[i].Cq();
                        list.Add(new QuickLink()
                        {
                            title = li.Find(".menutitle").Text(),
                            link = li.Find("a").Attr<string>("href")
                        });
                    }

                }

                #endregion

            }

            #endregion

            return list;

        }
    }

    public class Component
    {
        public string name { get; set; }
        public string title { get; set; }
        public string path { get; set; }
        public string limit { get; set; }
        public string width { get; set; }
        public string height { get; set; }
    }

    public class ComponentInstance
    {
        public string guid { get; set; }
        public string name { get; set; }
        public string title { get; set; }
        public string path { get; set; }
        public string limit { get; set; }
        public string style { get; set; }
    }

    public class Dependency
    {
        public List<string> css { get; set; }
        public List<string> js { get; set; }

        public string defined { get; set; }

        public Dependency()
        {
            css = new List<string>();
            js = new List<string>();
        }
    }

    public class QuickLink
    {
        public string title { get; set; }
        public string link { get; set; }
    }
}
