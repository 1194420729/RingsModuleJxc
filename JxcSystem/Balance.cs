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
using System.Configuration;
using Npgsql;

namespace JxcSystem
{
    public class Balance : MarshalByRefObject
    {
        public Object MonthBalanceList(string parameters)
        {             
            DBHelper db = new DBHelper();
            List<TableModel> list = db.Where("select * from monthbalance order by id desc");             

            return new { history = list };
        }

        public Object MonthBalanceDelete(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            int id = ph.GetParameterValue<int>("id");

            using (DBHelper db = new DBHelper())
            {
                int cnt=db.Count("select count(0) as cnt from monthbalance where id>"+id);
                if (cnt>0)
                {
                    return new { message = StringHelper.GetString("请先删除最后一次的月结！") };
                }
                db.Remove("monthbalance", id);
                db.SaveChanges();
            }

            return new { message = "ok" };
        }


        public Object MonthBalanceSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            DateTime balancedate = ph.GetParameterValue<DateTime>("balancedate");

            using (DBHelper db = new DBHelper(true))
            {
                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能月结存！") };
                }

                //月结日期不能早于开账日期
                if (balancedate.CompareTo(options.content.Value<DateTime>("initoverdate")) < 0)
                {
                    return new { message = StringHelper.GetString("月结日期不能早于开账日期！") };
                }

                //月结日期不能早于上次月结日期
                var lastbalance = db.FirstOrDefault("select * from monthbalance order by id desc");
                if (lastbalance != null && balancedate.CompareTo(lastbalance.content.Value<DateTime>("balancedate")) <= 0)
                {
                    return new { message = StringHelper.GetString("月结日期不能早于或等于上次月结日期！") };
                }

                TableModel balance = new TableModel() { content=new JObject()};
                balance.content["balancedate"] = balancedate.ToString("yyyy-MM-dd");
                balance.content["accountname"] = PluginContext.Current.Account.Name;
                balance.content["createtime"] = DateTime.Now;

                db.Add("monthbalance",balance);

                db.SaveChanges();
            }

            return new { message = "ok" };

        }

        public Object YearBalanceSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            DateTime? balancedate = null;
            string ztname = ph.GetParameterValue<string>("ztname");
            bool specialdate = ph.GetParameterValue<bool>("specialdate");
            if (specialdate)
            {
                balancedate = ph.GetParameterValue<DateTime>("balancedate");
            }

            using (DBHelper db = new DBHelper(true))
            {
                //检查是否已经开账
                var options = db.First("select * from option");
                if (options.content["initoverdate"] == null)
                {
                    return new { message = StringHelper.GetString("系统还没有开账，不能年结存！") };
                }

                //检查结存日期是否早于开账日期
                if (balancedate.HasValue && balancedate.Value.Date <= options.content.Value<DateTime>("initoverdate").Date)
                {
                    return new { message = StringHelper.GetString("年结存日期不能早于开账日期！") };
                }
            }

            //检查账套名称是否已经存在
            if (new ZtHelper().ZtNameExist(ztname))
            {
                return new { message = StringHelper.GetString("账套名称已存在！") };
            }

            try
            {
                IYearBalance helper = new LocalYearBalance();
                helper.Exec(ztname, balancedate);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                DeleteErrorDatabase(ztname);
                return new { message = "年结存出错：" + ex.Message };
            }
            
            return new { message = "ok" };

        }

        private void DeleteErrorDatabase(string ztname)
        {
            string centralconnectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
        }

        
         
    }
}
