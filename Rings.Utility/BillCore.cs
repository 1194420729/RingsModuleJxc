using Newtonsoft.Json.Linq;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public interface IAudit
    {
        /// <summary>
        /// 检查负库存
        /// </summary>
        /// <returns></returns>
        string CheckStorageEnough();

        /// <summary>
        /// 检查零库存
        /// </summary>
        /// <returns></returns>
        string CheckStorageZero();

        /// <summary>
        /// 计算出库成本
        /// </summary>
        void GetCostPrice();

        /// <summary>
        /// 增加虚拟库存
        /// </summary>
        void AddVirtualStorage();

        /// <summary>
        /// 减少虚拟库存
        /// </summary>
        void ReduceVirtualStorage();

        /// <summary>
        /// 增加库存
        /// </summary>
        void AddStorage();

        /// <summary>
        /// 减少库存
        /// </summary>
        void ReduceStorage();

        /// <summary>
        /// 增加应付
        /// </summary>
        void AddPayable();

        /// <summary>
        /// 减少应付
        /// </summary>
        void ReducePayable();

        /// <summary>
        /// 增加应收
        /// </summary>
        void AddReceivable();

        /// <summary>
        /// 减少应收
        /// </summary>
        void ReduceReceivable();

        /// <summary>
        /// 增加资金账户余额
        /// </summary>
        void AddBank();

        /// <summary>
        /// 减少资金账户余额
        /// </summary>
        void ReduceBank();

        /// <summary>
        /// 更新订单到货数量
        /// </summary>
        void ModifyDeliveryQty();

        /// <summary>
        /// 更新采购价格表
        /// </summary>
        void ModifyPurchasePrice();

        /// <summary>
        /// 更新销售价格表
        /// </summary>
        void ModifySalePrice();
    }

    public abstract class BaseProductBillAudit : IAudit
    {
        protected TableModel bill;
        protected DBHelper db;

        public BaseProductBillAudit(TableModel bill, DBHelper db)
        {
            this.bill = bill;
            this.db = db;
        }

        public virtual string CheckStorageEnough()
        {
            int stockid = bill.content.Value<int>("stockid");            
            
            var details=bill.content.Value<JArray>("details").Values<JObject>();
            var products = from c in details
                           group c by c.Value<int>("productid") into g
                           select new
                           {
                               productid=g.Key,
                               qty=g.Sum(d=>Math.Abs(d.Value<decimal>("qty")))
                           };

            foreach (var item in products.ToList())
            {
                var product = db.First("product", item.productid);
                decimal qty = item.qty;
                decimal storage = decimal.Zero;
                if (product.content["storage"] != null)
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    storage = vs[stockid.ToString()] == null ? decimal.Zero : vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty");
                }

                if (qty > storage)
                {
                    return "【" + product.content.Value<string>("code") + "】" + product.content.Value<string>("name") + StringHelper.GetString("库存数量不足！");
                }
            }

            return string.Empty;
        }

        public virtual void GetCostPrice()
        {
            bill.GetCostPrice(db);
        }

        public virtual void AddVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));
                //decimal discount= item.Value<decimal>("discountrate") / 100M;
                decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;
                if (product.content["virtualstorage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("discounttotal") / taxrate }, { "price", item.Value<decimal>("discountprice") / taxrate } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("discounttotal") / taxrate }, { "price", item.Value<decimal>("discountprice") / taxrate } });

                    product.content["virtualstorage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate;
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("discounttotal") / taxrate) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", item.Value<decimal>("qty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("discountprice") / taxrate);
                virtualstoragedetail.content.Add("total", item.Value<decimal>("discounttotal") / taxrate);
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }

        }

        public virtual void ReduceVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));


                JObject vs = product.content.Value<JObject>("virtualstorage");
                if (vs == null)
                {
                    vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", decimal.Zero }, { "total", decimal.Zero }, { "price", decimal.Zero } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", decimal.Zero }, { "total", decimal.Zero }, { "price", decimal.Zero } });
                     
                    product.content["virtualstorage"] = vs;
                }

                decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") - item.Value<decimal>("qty");
                decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") - item.Value<decimal>("costtotal");
                vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") - item.Value<decimal>("qty");
                decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") - item.Value<decimal>("costtotal");
                vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };


                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", -item.Value<decimal>("qty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("costprice"));
                virtualstoragedetail.content.Add("total", -item.Value<decimal>("costtotal"));
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }
        }

        public virtual void AddStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));
                //decimal discount = item.Value<decimal>("discountrate") / 100M;
                decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                if (product.content["storage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("discounttotal") / taxrate }, { "price", item.Value<decimal>("discountprice") / taxrate } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("discounttotal") / taxrate }, { "price", item.Value<decimal>("discountprice") / taxrate } });

                    product.content["storage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate;
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("discounttotal") / taxrate) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", item.Value<decimal>("qty"));
                storagedetail.content.Add("price", item.Value<decimal>("discountprice") / taxrate);
                storagedetail.content.Add("total", item.Value<decimal>("discounttotal") / taxrate);
                storagedetail.content.Add("leftqty", item.Value<decimal>("qty"));
                storagedetail.content.Add("lefttotal", item.Value<decimal>("discounttotal") / taxrate);
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public virtual void ReduceStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));

                JObject vs = product.content.Value<JObject>("storage");
                decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") - item.Value<decimal>("qty");
                decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") - item.Value<decimal>("costtotal");
                vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") - item.Value<decimal>("qty");
                decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") - item.Value<decimal>("costtotal");
                vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };


                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", -item.Value<decimal>("qty"));
                storagedetail.content.Add("price", item.Value<decimal>("costprice"));
                storagedetail.content.Add("total", -item.Value<decimal>("costtotal"));
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public virtual void AddPayable()
        {
            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',(coalesce((content->>'payable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + vendorid);
            TableModel payabledetail = new TableModel() { content = new JObject() };
            payabledetail.content.Add("vendorid", vendorid);
            payabledetail.content.Add("total", total);
            payabledetail.content.Add("createtime", DateTime.Now);
            payabledetail.content.Add("billid", bill.id);
            payabledetail.content.Add("billname", billname);
            payabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("payabledetail", payabledetail);
        }

        public virtual void ReducePayable()
        {
            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',(coalesce((content->>'payable')::decimal,0)-(" + total + "))::text::jsonb,true) where id=" + vendorid);
            TableModel payabledetail = new TableModel() { content = new JObject() };
            payabledetail.content.Add("vendorid", vendorid);
            payabledetail.content.Add("total", -total);
            payabledetail.content.Add("createtime", DateTime.Now);
            payabledetail.content.Add("billid", bill.id);
            payabledetail.content.Add("billname", billname);
            payabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("payabledetail", payabledetail);

        }

        public virtual void AddReceivable()
        {
            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',(coalesce((content->>'receivable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + customerid);
            TableModel receivabledetail = new TableModel() { content = new JObject() };
            receivabledetail.content.Add("customerid", customerid);
            receivabledetail.content.Add("total", total);
            receivabledetail.content.Add("createtime", DateTime.Now);
            receivabledetail.content.Add("billid", bill.id);
            receivabledetail.content.Add("billname", billname);
            receivabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("receivabledetail", receivabledetail);
        }

        public virtual void ReduceReceivable()
        {

            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',(coalesce((content->>'receivable')::decimal,0)-(" + total + "))::text::jsonb,true) where id=" + customerid);
            TableModel receivabledetail = new TableModel() { content = new JObject() };
            receivabledetail.content.Add("customerid", customerid);
            receivabledetail.content.Add("total", -total);
            receivabledetail.content.Add("createtime", DateTime.Now);
            receivabledetail.content.Add("billid", bill.id);
            receivabledetail.content.Add("billname", billname);
            receivabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("receivabledetail", receivabledetail);
        }

        public virtual void ModifyDeliveryQty()
        {
            string billname = bill.content.Value<string>("billname");

            if (billname == "purchasebill")
            {
                int vendorid = bill.content.Value<int>("vendorid");
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    if (item["purchaseorderuuid"] == null) continue;
                    decimal qty = item.Value<decimal>("qty");
                    string uuid = item.Value<string>("purchaseorderuuid");
                    var purchaseorder = db.First("select * from bill where content->>'billname'='purchaseorder' and content@>'{\"details\":[{\"uuid\":\"" + uuid + "\"}]}'::jsonb");
                    foreach (var detail in purchaseorder.content.Value<JArray>("details").Values<JObject>())
                    {
                        if (detail.Value<string>("uuid") != uuid) continue;
                        detail["deliveryqty"] = detail.Value<decimal>("deliveryqty") + item.Value<decimal>("qty");
                        db.Edit("bill", purchaseorder);
                        break;
                    }
                }
            }
            else if (billname == "salebill")
            {
                int customerid = bill.content.Value<int>("customerid");
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    if (item["saleorderuuid"] == null) continue;
                    decimal qty = item.Value<decimal>("qty");
                    string uuid = item.Value<string>("saleorderuuid");
                    var saleorder = db.First("select * from bill where content->>'billname'='saleorder' and content@>'{\"details\":[{\"uuid\":\"" + uuid + "\"}]}'::jsonb");
                    foreach (var detail in saleorder.content.Value<JArray>("details").Values<JObject>())
                    {
                        if (detail.Value<string>("uuid") != uuid) continue;
                        detail["deliveryqty"] = detail.Value<decimal>("deliveryqty") + item.Value<decimal>("qty");
                        db.Edit("bill", saleorder);
                        break;
                    }
                }
            }
        }

        public virtual void ModifyPurchasePrice()
        {
            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                int productid = item.Value<int>("productid");
                decimal price = item.Value<decimal>("discountprice");
                var model = db.FirstOrDefault("select * from vendorproduct where (content->>'vendorid')::int=" + vendorid + " and (content->>'productid')::int=" + productid);
                if (model == null)
                {
                    TableModel vp = new TableModel() { content = new JObject() };
                    vp.content.Add("vendorid", vendorid);
                    vp.content.Add("productid", productid);
                    vp.content.Add("price", price);
                    db.Add("vendorproduct", vp);
                }
                else
                {
                    model.content["price"] = price;
                    db.Edit("vendorproduct", model);
                }
            }
        }

        public virtual void ModifySalePrice()
        {
            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                int productid = item.Value<int>("productid");
                decimal price = item.Value<decimal>("discountprice");
                var model = db.FirstOrDefault("select * from customerproduct where (content->>'customerid')::int=" + customerid + " and (content->>'productid')::int=" + productid);
                if (model == null)
                {
                    TableModel vp = new TableModel() { content = new JObject() };
                    vp.content.Add("customerid", customerid);
                    vp.content.Add("productid", productid);
                    vp.content.Add("price", price);
                    db.Add("customerproduct", vp);
                }
                else
                {
                    model.content["price"] = price;
                    db.Edit("customerproduct", model);
                }
            }
        }


        public virtual string CheckStorageZero()
        {
            int stockid = bill.content.Value<int>("stockid");
            StringBuilder sb = new StringBuilder();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                var product = db.First("product", item.Value<int>("productid"));

                decimal storage = decimal.Zero;
                if (product.content["storage"] != null)
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    storage = vs[stockid.ToString()] == null ? decimal.Zero : vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty");
                }

                if (storage == decimal.Zero)
                {
                    sb.Append(product.id);
                    sb.Append(",");
                }
            }

            return sb.ToString();
        }
         
        public virtual void AddBank()
        {
            throw new NotImplementedException();
        }

        public virtual void ReduceBank()
        {
            throw new NotImplementedException();
        }
    }

    public abstract class BaseMoneyBillAudit : IAudit
    {
        protected TableModel bill;
        protected DBHelper db;

        public BaseMoneyBillAudit(TableModel bill, DBHelper db)
        {
            this.bill = bill;
            this.db = db;
        }

        public virtual string CheckStorageEnough()
        {
            throw new NotImplementedException();
        }

        public virtual string CheckStorageZero()
        {
            throw new NotImplementedException();
        }

        public virtual void GetCostPrice()
        {
            throw new NotImplementedException();
        }

        public virtual void AddVirtualStorage()
        {
            throw new NotImplementedException();
        }

        public virtual void ReduceVirtualStorage()
        {
            throw new NotImplementedException();
        }

        public virtual void AddStorage()
        {
            throw new NotImplementedException();
        }

        public virtual void ReduceStorage()
        {
            throw new NotImplementedException();
        }

        public virtual void AddPayable()
        {
            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',(coalesce((content->>'payable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + vendorid);
            TableModel payabledetail = new TableModel() { content = new JObject() };
            payabledetail.content.Add("vendorid", vendorid);
            payabledetail.content.Add("total", total);
            payabledetail.content.Add("createtime", DateTime.Now);
            payabledetail.content.Add("billid", bill.id);
            payabledetail.content.Add("billname", billname);
            payabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("payabledetail", payabledetail);
        }

        public virtual void ReducePayable()
        {
            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',(coalesce((content->>'payable')::decimal,0)-(" + total + "))::text::jsonb,true) where id=" + vendorid);
            TableModel payabledetail = new TableModel() { content = new JObject() };
            payabledetail.content.Add("vendorid", vendorid);
            payabledetail.content.Add("total", -total);
            payabledetail.content.Add("createtime", DateTime.Now);
            payabledetail.content.Add("billid", bill.id);
            payabledetail.content.Add("billname", billname);
            payabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("payabledetail", payabledetail);

        }

        public virtual void AddReceivable()
        {
            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',(coalesce((content->>'receivable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + customerid);
            TableModel receivabledetail = new TableModel() { content = new JObject() };
            receivabledetail.content.Add("customerid", customerid);
            receivabledetail.content.Add("total", total);
            receivabledetail.content.Add("createtime", DateTime.Now);
            receivabledetail.content.Add("billid", bill.id);
            receivabledetail.content.Add("billname", billname);
            receivabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("receivabledetail", receivabledetail);
        }

        public virtual void ReduceReceivable()
        {

            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            decimal total = bill.content.Value<decimal>("total");

            db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',(coalesce((content->>'receivable')::decimal,0)-(" + total + "))::text::jsonb,true) where id=" + customerid);
            TableModel receivabledetail = new TableModel() { content = new JObject() };
            receivabledetail.content.Add("customerid", customerid);
            receivabledetail.content.Add("total", -total);
            receivabledetail.content.Add("createtime", DateTime.Now);
            receivabledetail.content.Add("billid", bill.id);
            receivabledetail.content.Add("billname", billname);
            receivabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("receivabledetail", receivabledetail);
        }

        public virtual void AddBank()
        {
            string billname = bill.content.Value<string>("billname");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                int bankid = item.Value<int>("bankid");
                var bank = db.First("bank", bankid);

                decimal total = item.Value<decimal>("total");
                bank.content["total"] = bank.content.Value<decimal>("total") + total;
                db.Edit("bank", bank);

                TableModel bankdetail = new TableModel() { content = new JObject() };
                bankdetail.content.Add("bankid", bankid);
                bankdetail.content.Add("total", total);

                bankdetail.content.Add("createtime", DateTime.Now);
                bankdetail.content.Add("billid", bill.id);
                bankdetail.content.Add("billname", billname);
                bankdetail.content.Add("billdate", bill.content.Value<string>("billdate"));

                db.Add("bankdetail", bankdetail);
            }

        }

        public virtual void ReduceBank()
        {
            string billname = bill.content.Value<string>("billname");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                int bankid = item.Value<int>("bankid");
                var bank = db.First("bank", bankid);

                decimal total = item.Value<decimal>("total");
                bank.content["total"] = bank.content.Value<decimal>("total") - total;
                db.Edit("bank", bank);

                TableModel bankdetail = new TableModel() { content = new JObject() };
                bankdetail.content.Add("bankid", bankid);
                bankdetail.content.Add("total", -total);

                bankdetail.content.Add("createtime", DateTime.Now);
                bankdetail.content.Add("billid", bill.id);
                bankdetail.content.Add("billname", billname);
                bankdetail.content.Add("billdate", bill.content.Value<string>("billdate"));

                db.Add("bankdetail", bankdetail);
            }
        }

        public virtual void ModifyDeliveryQty()
        {
            throw new NotImplementedException();
        }

        public virtual void ModifyPurchasePrice()
        {
            throw new NotImplementedException();
        }

        public virtual void ModifySalePrice()
        {
            throw new NotImplementedException();
        }
    }

    public class PurchaseOrderAudit : BaseProductBillAudit
    {
        public PurchaseOrderAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class PurchaseBillAudit : BaseProductBillAudit
    {
        public PurchaseBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class PurchaseBackBillAudit : BaseProductBillAudit
    {
        public PurchaseBackBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class SaleOrderAudit : BaseProductBillAudit
    {
        public SaleOrderAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class SaleBillAudit : BaseProductBillAudit
    {
        public SaleBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class SaleBackBillAudit : BaseProductBillAudit
    {
        public SaleBackBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override void GetCostPrice()
        {
            bill.GetAvgCostPrice(db);
        }

        public override void AddStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["storage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["storage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", item.Value<decimal>("qty"));
                storagedetail.content.Add("price", item.Value<decimal>("costprice"));
                storagedetail.content.Add("total", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("leftqty", item.Value<decimal>("qty"));
                storagedetail.content.Add("lefttotal", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public override void AddVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["virtualstorage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["virtualstorage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", item.Value<decimal>("qty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("costprice"));
                virtualstoragedetail.content.Add("total", item.Value<decimal>("costtotal"));
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }

        }

    }

    public class StockInBillAudit : BaseProductBillAudit
    {
        public StockInBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override void GetCostPrice()
        {
            bill.GetAvgCostPrice(db);
        }

        public override void AddStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["storage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["storage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", item.Value<decimal>("qty"));
                storagedetail.content.Add("price", item.Value<decimal>("costprice"));
                storagedetail.content.Add("total", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("leftqty", item.Value<decimal>("qty"));
                storagedetail.content.Add("lefttotal", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public override void AddVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["virtualstorage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["virtualstorage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", item.Value<decimal>("qty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("costprice"));
                virtualstoragedetail.content.Add("total", item.Value<decimal>("costtotal"));
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }

        }

    }

    public class StockOutBillAudit : BaseProductBillAudit
    {
        public StockOutBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class StockMoveBillAudit : BaseProductBillAudit
    {
        public StockMoveBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override void AddStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid2");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["storage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["storage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", item.Value<decimal>("qty"));
                storagedetail.content.Add("price", item.Value<decimal>("costprice"));
                storagedetail.content.Add("total", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("leftqty", item.Value<decimal>("qty"));
                storagedetail.content.Add("lefttotal", item.Value<decimal>("costtotal"));
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public override void AddVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid2");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["virtualstorage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("qty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["virtualstorage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("qty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", item.Value<decimal>("qty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("costprice"));
                virtualstoragedetail.content.Add("total", item.Value<decimal>("costtotal"));
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }

        }

        public override string CheckStorageEnough()
        {
            int stockid = bill.content.Value<int>("stockid");
            if (bill.content.Value<int>("auditstatus") == 3)
            {
                stockid = bill.content.Value<int>("stockid2");
            }
            var details = bill.content.Value<JArray>("details").Values<JObject>();
            var products = from c in details
                           group c by c.Value<int>("productid") into g
                           select new
                           {
                               productid = g.Key,
                               qty = g.Sum(d => Math.Abs(d.Value<decimal>("qty")))
                           };

            foreach (var item in products.ToList())
            {
                var product = db.First("product", item.productid);
                decimal qty = item.qty;
                decimal storage = decimal.Zero;
                if (product.content["storage"] != null)
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    storage = vs[stockid.ToString()] == null ? decimal.Zero : vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty");
                }

                if (qty > storage)
                {
                    return "【" + product.content.Value<string>("code") + "】" + product.content.Value<string>("name") + StringHelper.GetString("库存数量不足！");
                }
            }

            return string.Empty;
        }

    }

    public class StockInventoryBillAudit : BaseProductBillAudit
    {
        public StockInventoryBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override string CheckStorageZero()
        {
            int stockid = bill.content.Value<int>("stockid");
            StringBuilder sb = new StringBuilder();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (item.Value<decimal>("ykqty") <= decimal.Zero) continue;

                var product = db.First("product", item.Value<int>("productid"));

                decimal storage = decimal.Zero;
                if (product.content["storage"] != null)
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    storage = vs[stockid.ToString()] == null ? decimal.Zero : vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty");
                }

                if (storage == decimal.Zero)
                {
                    sb.Append(product.id);
                    sb.Append(",");
                }
            }

            return sb.ToString();
        }

        public override void GetCostPrice()
        {
            int stockid = bill.content.Value<int>("stockid");
            ICostMethod outcostmethod = new CostMethodFactory().GetCostMethod();
            ICostMethod incostmethod = new AverageMethod();
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (item.Value<decimal>("ykqty") == decimal.Zero) continue;

                if (item.Value<decimal>("ykqty") > decimal.Zero)
                {
                    //报溢入库
                    if (item["costprice"] != null) continue;
                    decimal qty = item.Value<decimal>("ykqty");
                    decimal costprice = incostmethod.GetCostPrice(stockid, item.Value<int>("productid"), qty, db);
                    item["costprice"] = costprice;
                    item["costtotal"] = costprice * qty;
                }
                else
                {
                    //报损出库
                    decimal qty = Math.Abs(item.Value<decimal>("ykqty"));
                    decimal costprice = outcostmethod.GetCostPrice(stockid, item.Value<int>("productid"), qty, db);
                    item["costprice"] = costprice;
                    item["costtotal"] = costprice * qty;
                }
            }

        }

        public override void AddStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (item.Value<decimal>("ykqty") == decimal.Zero) continue;

                #region 更新库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["storage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("ykqty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("ykqty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["storage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("storage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("ykqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("ykqty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("ykqty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新库存明细
                TableModel storagedetail = new TableModel() { content = new JObject() };
                storagedetail.content.Add("stockid", stockid);
                storagedetail.content.Add("productid", product.id);
                storagedetail.content.Add("qty", item.Value<decimal>("ykqty"));
                storagedetail.content.Add("price", item.Value<decimal>("costprice"));
                storagedetail.content.Add("total", item.Value<decimal>("ykqty") > decimal.Zero ? item.Value<decimal>("costtotal") : (-item.Value<decimal>("costtotal")));
                if (item.Value<decimal>("ykqty") > decimal.Zero)
                {
                    storagedetail.content.Add("leftqty", item.Value<decimal>("ykqty"));
                    storagedetail.content.Add("lefttotal", item.Value<decimal>("costtotal"));
                }
                storagedetail.content.Add("createtime", DateTime.Now);
                storagedetail.content.Add("billid", bill.id);
                storagedetail.content.Add("billname", billname);
                storagedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                storagedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("storagedetail", storagedetail);

                #endregion
            }
        }

        public override void AddVirtualStorage()
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");

            foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
            {
                if (item.Value<decimal>("ykqty") == decimal.Zero) continue;

                #region 更新虚拟库存
                var product = db.First("product", item.Value<int>("productid"));

                if (product.content["virtualstorage"] == null)
                {
                    JObject vs = new JObject();
                    vs.Add("0", new JObject() { { "qty", item.Value<decimal>("ykqty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });
                    vs.Add(stockid.ToString(), new JObject() { { "qty", item.Value<decimal>("ykqty") }, { "total", item.Value<decimal>("costtotal") }, { "price", item.Value<decimal>("costprice") } });

                    product.content["virtualstorage"] = vs;
                }
                else
                {
                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("ykqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("costtotal");
                    vs["0"] = new JObject()
                        {
                            {"qty",qty0},
                            {"total",total0},
                            {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                        };

                    decimal qty = vs[stockid.ToString()] == null ? item.Value<decimal>("ykqty") : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("ykqty"));
                    decimal total = vs[stockid.ToString()] == null ? (item.Value<decimal>("costtotal")) : (vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("costtotal"));
                    vs[stockid.ToString()] = new JObject()
                        {
                            {"qty",qty},
                            {"total",total},
                            {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                        };
                }

                db.Edit("product", product);
                #endregion

                #region 更新虚拟库存明细
                TableModel virtualstoragedetail = new TableModel() { content = new JObject() };
                virtualstoragedetail.content.Add("stockid", stockid);
                virtualstoragedetail.content.Add("productid", product.id);
                virtualstoragedetail.content.Add("qty", item.Value<decimal>("ykqty"));
                virtualstoragedetail.content.Add("price", item.Value<decimal>("costprice"));
                virtualstoragedetail.content.Add("total", item.Value<decimal>("ykqty") > decimal.Zero ? item.Value<decimal>("costtotal") : (-item.Value<decimal>("costtotal")));
                virtualstoragedetail.content.Add("createtime", DateTime.Now);
                virtualstoragedetail.content.Add("billid", bill.id);
                virtualstoragedetail.content.Add("billname", billname);
                virtualstoragedetail.content.Add("billdate", bill.content.Value<string>("billdate"));
                virtualstoragedetail.content.Add("billdetailid", item.Value<string>("uuid"));

                db.Add("virtualstoragedetail", virtualstoragedetail);

                #endregion
            }

        }

    }


    public class GatheringBillAudit : BaseMoneyBillAudit
    {
        public GatheringBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class PayBillAudit : BaseMoneyBillAudit
    {
        public PayBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class ReceivableBillAudit : BaseMoneyBillAudit
    {
        public ReceivableBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class PayableBillAudit : BaseMoneyBillAudit
    {
        public PayableBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
    }

    public class FeeBillAudit : BaseMoneyBillAudit
    {
        public FeeBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override void AddPayable()
        {
            if (bill.content.Value<int>("vendorid") == 0) return;
            if (bill.content.Value<decimal>("total") == bill.content.Value<decimal>("paytotal")) return;

            string billname = bill.content.Value<string>("billname");
            int vendorid = bill.content.Value<int>("vendorid");
            decimal total = bill.content.Value<decimal>("total") - bill.content.Value<decimal>("paytotal");

            db.ExcuteNoneQuery("update vendor set content=jsonb_set(content,'{payable}',(coalesce((content->>'payable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + vendorid);
            TableModel payabledetail = new TableModel() { content = new JObject() };
            payabledetail.content.Add("vendorid", vendorid);
            payabledetail.content.Add("total", total);
            payabledetail.content.Add("createtime", DateTime.Now);
            payabledetail.content.Add("billid", bill.id);
            payabledetail.content.Add("billname", billname);
            payabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("payabledetail", payabledetail);
        }

        public override void ReduceBank()
        {
            if (bill.content.Value<int>("bankid") == 0) return;
            if (bill.content.Value<decimal>("paytotal") == decimal.Zero) return;

            string billname = bill.content.Value<string>("billname");

            int bankid = bill.content.Value<int>("bankid");
            var bank = db.First("bank", bankid);

            decimal total = bill.content.Value<decimal>("paytotal");
            bank.content["total"] = bank.content.Value<decimal>("total") - total;
            db.Edit("bank", bank);

            TableModel bankdetail = new TableModel() { content = new JObject() };
            bankdetail.content.Add("bankid", bankid);
            bankdetail.content.Add("total", -total);

            bankdetail.content.Add("createtime", DateTime.Now);
            bankdetail.content.Add("billid", bill.id);
            bankdetail.content.Add("billname", billname);
            bankdetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("bankdetail", bankdetail);
        }
    }

    public class EarningBillAudit : BaseMoneyBillAudit
    {
        public EarningBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }

        public override void AddReceivable()
        {
            if (bill.content.Value<int>("customerid") == 0) return;
            if (bill.content.Value<decimal>("total") == bill.content.Value<decimal>("paytotal")) return;


            string billname = bill.content.Value<string>("billname");
            int customerid = bill.content.Value<int>("customerid");
            decimal total = bill.content.Value<decimal>("total") - bill.content.Value<decimal>("paytotal");

            db.ExcuteNoneQuery("update customer set content=jsonb_set(content,'{receivable}',(coalesce((content->>'receivable')::decimal,0)+(" + total + "))::text::jsonb,true) where id=" + customerid);
            TableModel receivabledetail = new TableModel() { content = new JObject() };
            receivabledetail.content.Add("customerid", customerid);
            receivabledetail.content.Add("total", total);
            receivabledetail.content.Add("createtime", DateTime.Now);
            receivabledetail.content.Add("billid", bill.id);
            receivabledetail.content.Add("billname", billname);
            receivabledetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("receivabledetail", receivabledetail);
        }

        public override void AddBank()
        {
            if (bill.content.Value<int>("bankid") == 0) return;
            if (bill.content.Value<decimal>("paytotal") == decimal.Zero) return;

            string billname = bill.content.Value<string>("billname");

            int bankid = bill.content.Value<int>("bankid");
            var bank = db.First("bank", bankid);

            decimal total = bill.content.Value<decimal>("paytotal");
            bank.content["total"] = bank.content.Value<decimal>("total") + total;
            db.Edit("bank", bank);

            TableModel bankdetail = new TableModel() { content = new JObject() };
            bankdetail.content.Add("bankid", bankid);
            bankdetail.content.Add("total", total);

            bankdetail.content.Add("createtime", DateTime.Now);
            bankdetail.content.Add("billid", bill.id);
            bankdetail.content.Add("billname", billname);
            bankdetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("bankdetail", bankdetail);
        }


         
    }

    public class TransferBillAudit : BaseMoneyBillAudit
    {
        public TransferBillAudit(TableModel bill, DBHelper db)
            : base(bill, db)
        {

        }
 
        public override void AddBank()
        { 
            string billname = bill.content.Value<string>("billname");

            int bankid = bill.content.Value<int>("bankid");
            var bank = db.First("bank", bankid);

            decimal total = bill.content.Value<decimal>("total");
            bank.content["total"] = bank.content.Value<decimal>("total") - total;
            db.Edit("bank", bank);

            TableModel bankdetail = new TableModel() { content = new JObject() };
            bankdetail.content.Add("bankid", bankid);
            bankdetail.content.Add("total", -total);

            bankdetail.content.Add("createtime", DateTime.Now);
            bankdetail.content.Add("billid", bill.id);
            bankdetail.content.Add("billname", billname);
            bankdetail.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("bankdetail", bankdetail);

            int bankid2 = bill.content.Value<int>("bankid2");
            var bank2 = db.First("bank", bankid2);
             
            bank2.content["total"] = bank2.content.Value<decimal>("total") + total;
            db.Edit("bank", bank2);

            TableModel bankdetail2 = new TableModel() { content = new JObject() };
            bankdetail2.content.Add("bankid", bankid2);
            bankdetail2.content.Add("total", total);

            bankdetail2.content.Add("createtime", DateTime.Now);
            bankdetail2.content.Add("billid", bill.id);
            bankdetail2.content.Add("billname", billname);
            bankdetail2.content.Add("billdate", bill.content.Value<string>("billdate"));

            db.Add("bankdetail", bankdetail2);
        }

    }


    public static class BillAuditFactory
    {
        public static IAudit GetBillAudit(this TableModel bill, DBHelper db)
        {
            string billname = bill.content.Value<string>("billname");
            if (billname == "purchaseorder")
            {
                return new PurchaseOrderAudit(bill, db);
            }
            else if (billname == "purchasebill")
            {
                return new PurchaseBillAudit(bill, db);
            }
            else if (billname == "purchasebackbill")
            {
                return new PurchaseBackBillAudit(bill, db);
            }
            else if (billname == "saleorder")
            {
                return new SaleOrderAudit(bill, db);
            }
            else if (billname == "salebill")
            {
                return new SaleBillAudit(bill, db);
            }
            else if (billname == "salebackbill")
            {
                return new SaleBackBillAudit(bill, db);
            }
            else if (billname == "gatheringbill")
            {
                return new GatheringBillAudit(bill, db);
            }
            else if (billname == "paybill")
            {
                return new PayBillAudit(bill, db);
            }
            else if (billname == "receivablebill")
            {
                return new ReceivableBillAudit(bill, db);
            }
            else if (billname == "payablebill")
            {
                return new PayableBillAudit(bill, db);
            }
            else if (billname == "stockinbill")
            {
                return new StockInBillAudit(bill, db);
            }
            else if (billname == "stockoutbill")
            {
                return new StockOutBillAudit(bill, db);
            }
            else if (billname == "stockmovebill")
            {
                return new StockMoveBillAudit(bill, db);
            }
            else if (billname == "stockinventorybill")
            {
                return new StockInventoryBillAudit(bill, db);
            }
            else if (billname == "feebill")
            {
                return new FeeBillAudit(bill, db);
            }
            else if (billname == "earningbill")
            {
                return new EarningBillAudit(bill, db);
            }
            else if (billname == "transferbill")
            {
                return new TransferBillAudit(bill, db);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
