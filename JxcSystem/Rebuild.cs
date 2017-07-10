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
    public class Rebuild : MarshalByRefObject
    { 
        public Object RebuildSave(string parameters)
        {
            ParameterHelper ph = new ParameterHelper(parameters);
            bool clearinit=ph.GetParameterValue<bool>("clearinit");
            bool cleardraft = ph.GetParameterValue<bool>("cleardraft");
            bool clearstop = ph.GetParameterValue<bool>("clearstop");

            using (DBHelper db = new DBHelper(true))
            {
                //删除开账日期
                db.ExcuteNoneQuery("update option set content=content-'initoverdate'");

                //删除库存及明细
                db.ExcuteNoneQuery("update product set content=content-'storage'");
                db.Truncate("storagedetail");
                //删除虚拟库存及明细
                db.ExcuteNoneQuery("update product set content=content-'virtualstorage'");
                db.Truncate("virtualstoragedetail");
                //删除应收应付及明细
                db.ExcuteNoneQuery("update customer set content=content-'receivable'");
                db.Truncate("receivabledetail");
                db.ExcuteNoneQuery("update vendor set content=content-'payable'");
                db.Truncate("payabledetail");
                //删除现金银行及明细
                db.ExcuteNoneQuery("update bank set content=content-'total'");
                db.Truncate("bankdetail");

                //删除期初
                if (clearinit)
                {
                    db.ExcuteNoneQuery("update product set content=content-'initstorage'");
                    db.ExcuteNoneQuery("update customer set content=content-'initreceivable'");
                    db.ExcuteNoneQuery("update vendor set content=content-'initpayable'");
                    db.ExcuteNoneQuery("update bank set content=content-'inittotal'");
                }
                //删除停用基础资料
                if (clearstop)
                {
                    db.ExcuteNoneQuery("delete from product where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from customer where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from vendor where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from department where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from employee where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from stock where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from bank where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from fee where coalesce(content->>'stop','')!=''");
                    db.ExcuteNoneQuery("delete from earning where coalesce(content->>'stop','')!=''");
                }

                //删除业务单据及相关数据
                db.Truncate("bill");

                //todo:删除产品价格表

                db.SaveChanges();
            }
            return new { message = "ok" };

        }

    }
}
