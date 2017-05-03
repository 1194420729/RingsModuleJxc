using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public static class CostHelper
    {
        public static void GetCostPrice(this TableModel bill, DBHelper db, string qtyfield = "qty")
        {
            int stockid = bill.content.Value<int>("stockid");
            ICostMethod costmethod = new CostMethodFactory().GetCostMethod();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                decimal qty=item.Value<decimal>(qtyfield);
                decimal costprice = costmethod.GetCostPrice(stockid, item.Value<int>("productid"),qty,db);
                item["costprice"]=costprice;
                item["costtotal"]=costprice*qty;
            }
        }

        public static void GetAvgCostPrice(this TableModel bill, DBHelper db,string qtyfield="qty")
        {
            int stockid = bill.content.Value<int>("stockid");
            ICostMethod costmethod = new AverageMethod();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (item["costprice"] != null) continue;
                decimal qty = item.Value<decimal>(qtyfield);
                decimal costprice = costmethod.GetCostPrice(stockid, item.Value<int>("productid"), qty, db);
                item["costprice"] = costprice;
                item["costtotal"] = costprice * qty;
            }
        }

        public static void SetCostPriceInput(this TableModel bill,string costpriceinput,string qtyfield="qty")
        {
            if (string.IsNullOrEmpty(costpriceinput)) throw new Exception("无效的成本输入");

            Dictionary<int, decimal> dic = new Dictionary<int, decimal>();
            string[] ss1 = costpriceinput.Split(new char[]{','},StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in ss1)
            {
                string[] ss2 = s.Split(new char[]{':'},StringSplitOptions.RemoveEmptyEntries);
                if (ss2.Length != 2) throw new Exception("无效的成本输入");

                dic.Add(Convert.ToInt32(ss2[0]),Convert.ToDecimal(ss2[1]));
            }

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (dic.ContainsKey(item.Value<int>("productid")) == false) continue;

                decimal qty = item.Value<decimal>(qtyfield);
                decimal costprice = dic[item.Value<int>("productid")];
                item["costprice"] = costprice;
                item["costtotal"] = costprice * qty;
            }
        }
    }

    internal interface ICostMethod
    {
        decimal GetCostPrice(int stockid,int productid,decimal qty,DBHelper db);
    }

    internal class CostMethodFactory
    {
        internal ICostMethod GetCostMethod()
        {
            DBHelper db = new DBHelper();
            var option = db.First("select * from option");
            string costmethod= option.content.Value<string>("costmethod");
            if (costmethod == "ydjqpj")
            {
                return new AverageMethod();
            }
            else if (costmethod == "xjxc")
            {
                return new FIFOMethod();
            }
            else if (costmethod == "gbzd")
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException();
            }            
        }
    }

    internal class AverageMethod:ICostMethod
    {

        public decimal GetCostPrice(int stockid, int productid, decimal qty,DBHelper db)
        {
            var product = db.First("product",productid);
            JObject vs = product.content.Value<JObject>("storage");
            decimal price = vs.Value<JObject>(stockid.ToString()).Value<decimal>("price");

            return price;
        }
    }

    internal class FIFOMethod : ICostMethod
    {

        public decimal GetCostPrice(int stockid, int productid, decimal qty, DBHelper db)
        {
            var notauditedbillids = db.QueryTable("select id from bill where (content->>'auditstatus')::int<>1").GetIds();
            var storagedetails = db.Where("select * from storagedetail where (content->>'stockid')::int=" + stockid 
                + " and (content->>'productid')::int="+productid
                +" and (content->>'leftqty')::decimal>0 and (content->>'billid')::int not in ("+notauditedbillids
                +") order by content->>'billdate'");
            decimal costtotal = decimal.Zero;
            decimal myqty = qty;
            while (myqty > decimal.Zero)
            {
                var item=storagedetails.First();
                storagedetails.RemoveAt(0);
                decimal leftqty = item.content.Value<decimal>("leftqty");
                if (myqty <= leftqty)
                {
                    costtotal += item.content.Value<decimal>("price") * myqty;
                    item.content["leftqty"] = leftqty - myqty;
                    item.content["lefttotal"] = item.content.Value<decimal>("price") * (leftqty - myqty);
                    db.Edit("storagedetail", item);
                    myqty = decimal.Zero;
                }
                else
                {
                    costtotal += item.content.Value<decimal>("price") * leftqty;
                    item.content["leftqty"] = decimal.Zero;
                    item.content["lefttotal"] = decimal.Zero;
                    db.Edit("storagedetail", item);
                    myqty = myqty - leftqty ;
                }
            }
             
            decimal price = costtotal/qty;

            return price;
        }
    }
}
