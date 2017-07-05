using Rings.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public static class InitOverHelper
    {
        public static void InitOver(string initoverdate)
        {
            using (DBHelper db = new DBHelper())
            {
                var options = db.First("select * from option");
                var stocks = db.Where("select * from stock");
                 
                //记录开账日期
                options.content["initoverdate"] = initoverdate;
                db.Edit("option", options);

                //初始化库存及明细
                db.ExcuteNoneQuery("update product set content=jsonb_set(content,'{storage}',content->'initstorage',true) where coalesce(content->>'initstorage','')!=''");
                foreach (var stock in stocks)
                {
                    //for example:
                    //insert into storagedetail (content) 
                    //select content->'initstorage'->'1' || ('{"stockid":1,"leftqty":' || (content->'initstorage'->'1'->>'qty') || ',"createtime":"' || now() || '"}')::jsonb as content  
                    //from product where coalesce(content->'initstorage'->>'1','')!=''
                    db.ExcuteNoneQuery("insert into storagedetail (content) select content->'initstorage'->'"
                        + stock.id + "' || ('{\"stockid\":" + stock.id + ",\"leftqty\":' || (content->'initstorage'->'"
                        + stock.id + "'->>'qty') || ',\"lefttotal\":' || (content->'initstorage'->'"
                        + stock.id + "'->>'total') || ',\"productid\":' || (id::text) || ',\"billdate\":\"" + initoverdate
                        + "\",\"createtime\":\"' || now() || '\"}')::jsonb as content  from product where coalesce(content->'initstorage'->>'"
                        + stock.id + "','')!=''");
                }

                //初始化虚拟库存及明细
                db.ExcuteNoneQuery("update product set content=jsonb_set(content,'{virtualstorage}',content->'initstorage',true) where coalesce(content->>'initstorage','')!=''");
                foreach (var stock in stocks)
                {
                    //for example:
                    //insert into virtualstoragedetail (content) 
                    //select content->'initstorage'->'1' || ('{"stockid":1,"qty":' || (content->'initstorage'->'1'->>'qty') || ',"createtime":"' || now() || '"}')::jsonb as content  
                    //from product where coalesce(content->'initstorage'->>'1','')!=''
                    db.ExcuteNoneQuery("insert into virtualstoragedetail (content) select content->'initstorage'->'"
                        + stock.id + "' || ('{\"stockid\":" + stock.id + ",\"productid\":' || (id::text)  || ',\"billdate\":\"" + initoverdate
                        + "\",\"createtime\":\"' || now() || '\"}')::jsonb as content  from product where coalesce(content->'initstorage'->>'"
                        + stock.id + "','')!=''");
                }


                //初始化应收应付及明细
                db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',content->'initreceivable',true) where coalesce(content->>'initreceivable','')!=''");
                db.ExcuteNoneQuery("insert into receivabledetail (content) select jsonb_set('{}'::jsonb,'{total}', content->'initreceivable') || ('{\"customerid\":' || id  || ',\"billdate\":\"" + initoverdate + "\",\"createtime\":\"' || now() || '\"}')::jsonb from customer where coalesce(content->>'initreceivable','')!=''");
                db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',content->'initpayable',true) where coalesce(content->>'initpayable','')!=''");
                db.ExcuteNoneQuery("insert into payabledetail (content) select jsonb_set('{}'::jsonb,'{total}', content->'initpayable') || ('{\"vendorid\":' || id  || ',\"billdate\":\"" + initoverdate + "\",\"createtime\":\"' || now() || '\"}')::jsonb from vendor where coalesce(content->>'initpayable','')!=''");

                //初始化现金银行及明细
                db.ExcuteNoneQuery("update bank set content=jsonb_set(content,'{total}',content->'inittotal',true) where coalesce(content->>'inittotal','')!=''");
                db.ExcuteNoneQuery("insert into bankdetail (content) select jsonb_set('{}'::jsonb,'{total}', content->'inittotal') || ('{\"bankid\":' || id || ',\"billdate\":\"" + initoverdate + "\",\"createtime\":\"' || now() || '\"}')::jsonb from bank where coalesce(content->>'inittotal','')!=''");


                db.SaveChanges();
            }
        }
    }
}
