using Newtonsoft.Json.Linq;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public static class BillHelper
    {
        public static string RedWordAudit(this TableModel bill, DBHelper db, out string friendlymessage)
        {
            string billname = bill.content.Value<string>("billname");
            var options = db.First("select * from option");
            var billaudit = bill.GetBillAudit(db);
            friendlymessage = "";

            if (billname == "purchasebill")
            {
                #region 采购入库单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "红冲失败！" + result;
                    return result;
                }

                //增加账面库存
                billaudit.AddStorage();

                //增加应付账款
                billaudit.AddPayable();

                //增加订单到货数量
                billaudit.ModifyDeliveryQty();
                 
                #endregion
            }
            else if (billname == "purchasebackbill")
            {
                #region 采购退货单                 
                //减少账面库存
                billaudit.ReduceStorage();

                //减少虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.ReduceVirtualStorage();
                }

                //减少应付账款
                billaudit.ReducePayable();
                #endregion
            } 
            else if (billname == "salebill")
            {
                #region 销售出库单 
                 
                //减少账面库存
                billaudit.ReduceStorage();

                //增加应收账款
                billaudit.AddReceivable();

                //增加订单发货数量
                billaudit.ModifyDeliveryQty();
 
                #endregion
            }
            else if (billname == "salebackbill")
            {
                #region 销售退货单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！" + result;
                    return result;
                }
                 
                //增加账面库存
                billaudit.AddStorage();

                //增加虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }

                //减少应收账款
                billaudit.ReduceReceivable();
                #endregion
            }
            else if (billname == "stockinbill")
            {
                #region 入库单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！" + result;
                    return result;
                } 

                //增加账面库存
                billaudit.AddStorage();

                //增加虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }
                #endregion
            }
            else if (billname == "stockoutbill")
            {
                #region 出库单
                 
                //减少账面库存
                billaudit.ReduceStorage();

                //减少虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.ReduceVirtualStorage();
                }
                #endregion
            }
            else if (billname == "stockmovebill")
            {
                #region 调拨单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！" + result;
                    return result;
                } 

                //减少出库仓库账面库存
                billaudit.ReduceStorage();

                //增加入库仓库账面库存
                billaudit.AddStorage();

                if (options.content.Value<bool>("strictordermanage"))
                {
                    //减少出库仓库虚拟库存
                    billaudit.ReduceVirtualStorage();
                    //增加入库仓库虚拟库存
                    billaudit.AddVirtualStorage();
                }

                #endregion
            } 
            else if (billname == "paybill")
            {
                #region 付款单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            - item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.ReducePayable();
                billaudit.ReduceBank();
                #endregion
            }
            else if (billname == "gatheringbill")
            {
                #region 收款单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            - item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.ReduceReceivable();
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "payablebill")
            {
                #region 应付调整单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            + item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.AddPayable();
                #endregion
            }
            else if (billname == "receivablebill")
            {
                #region 应收调整单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            + item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }
                billaudit.AddReceivable();
                #endregion
            }
            else if (billname == "feebill")
            {
                #region 费用单
                billaudit.AddPayable();
                billaudit.ReduceBank();
                #endregion
            }
            else if (billname == "earningbill")
            {
                #region 收入单
                billaudit.AddReceivable();
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "transferbill")
            {
                #region 转款单
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "saleinvoicebill")
            {
                #region 销售发票
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    var invoicebill = db.First("bill", item.Value<int>("billid"));
                    invoicebill.content["invoicetotal"] = invoicebill.content.Value<decimal>("invoicetotal")
                        - item.Value<decimal>("invoicetotal");
                    db.Edit("bill", invoicebill);
                }
                #endregion
            }
            else if (billname == "purchaseinvoicebill")
            {
                #region 采购发票
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    var invoicebill = db.First("bill", item.Value<int>("billid"));
                    invoicebill.content["invoicetotal"] = invoicebill.content.Value<decimal>("invoicetotal")
                        - item.Value<decimal>("invoicetotal");
                    db.Edit("bill", invoicebill);
                }
                #endregion
            }

            return string.Empty;
        }


        public static string Audit(this TableModel bill, DBHelper db)
        {
            string msg = "";
            return Audit(bill,db,out msg);
        }

        public static string Audit(this TableModel bill, DBHelper db,out string friendlymessage)
        {
            string billname = bill.content.Value<string>("billname");
            var options = db.First("select * from option");
            var billaudit = bill.GetBillAudit(db);
            friendlymessage = "";

            if (billname == "purchaseorder")
            {
                #region 采购订单
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }

                //写入价格表
                if (options.content["purchasepricekeep"] != null
                    && options.content.Value<bool>("purchasepricekeep"))
                {
                    billaudit.ModifyPurchasePrice();
                }
                #endregion
            }
            else if (billname == "purchasebill")
            {
                #region 采购入库单
                //增加账面库存
                billaudit.AddStorage();

                //增加应付账款
                billaudit.AddPayable();

                //增加订单到货数量
                billaudit.ModifyDeliveryQty();

                //写入价格表
                if (options.content.Value<bool>("purchasepricekeep"))
                {
                    billaudit.ModifyPurchasePrice();
                }
                #endregion
            }
            else if (billname == "purchasebackbill")
            {
                #region 采购退货单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！"+result;
                    return result;
                }

                //计算出库成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //减少账面库存
                billaudit.ReduceStorage();

                //减少虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.ReduceVirtualStorage();
                }

                //减少应付账款
                billaudit.ReducePayable();
                #endregion
            }
            else if (billname == "saleorder")
            {
                #region 销售订单
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.ReduceVirtualStorage();
                }

                //写入价格表
                if (options.content.Value<bool>("salepricekeep"))
                {
                    billaudit.ModifySalePrice();
                }
                #endregion
            }
            else if (billname == "salebill")
            {
                #region 销售出库单

                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！" + result;
                    return result;
                }

                //计算出库成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //减少账面库存
                billaudit.ReduceStorage();

                //增加应收账款
                billaudit.AddReceivable();

                //增加订单发货数量
                billaudit.ModifyDeliveryQty();

                //写入价格表
                if (options.content.Value<bool>("salepricekeep"))
                {
                    billaudit.ModifySalePrice();
                }
                #endregion
            }
            else if (billname == "salebackbill")
            {
                #region 销售退货单

                //检查零库存，零库存需要指定成本价
                if (bill.content["costpriceinput"] != null)
                {
                    //已经手工指定成本
                    bill.SetCostPriceInput(bill.content.Value<string>("costpriceinput"));
                }
                else
                {
                    string result = billaudit.CheckStorageZero();
                    if (!string.IsNullOrEmpty(result))
                    {
                        db.Discard();
                        friendlymessage = "过账失败！需要手工指定入库成本，请打开本张单据的编辑页面进行过账。";
                        return result;
                    }
                }

                //计算成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //增加账面库存
                billaudit.AddStorage();

                //增加虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }

                //减少应收账款
                billaudit.ReduceReceivable();
                #endregion
            }
            else if (billname == "stockinbill")
            {
                #region 入库单

                //检查零库存，零库存需要指定成本价
                if (bill.content["costpriceinput"] != null)
                {
                    //已经手工指定成本
                    bill.SetCostPriceInput(bill.content.Value<string>("costpriceinput"));
                }
                else
                {
                    string result = billaudit.CheckStorageZero();
                    if (!string.IsNullOrEmpty(result))
                    {
                        db.Discard();
                        friendlymessage = "过账失败！需要手工指定入库成本，请打开本张单据的编辑页面进行过账。";
                        return result;
                    }
                }
                //计算成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //增加账面库存
                billaudit.AddStorage();

                //增加虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }
                #endregion
            }
            else if (billname == "stockoutbill")
            {
                #region 出库单

                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！"+result;
                    return result;
                }

                //计算出库成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //减少账面库存
                billaudit.ReduceStorage();

                //减少虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.ReduceVirtualStorage();
                }
                #endregion
            }
            else if (billname == "stockmovebill")
            {
                #region 调拨单
                //检查负库存
                string result = billaudit.CheckStorageEnough();
                if (!string.IsNullOrEmpty(result))
                {
                    db.Discard();
                    friendlymessage = "过账失败！"+result;
                    return result;
                }

                //计算出库成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //减少出库仓库账面库存
                billaudit.ReduceStorage();

                //增加入库仓库账面库存
                billaudit.AddStorage();

                if (options.content.Value<bool>("strictordermanage"))
                {
                    //减少出库仓库虚拟库存
                    billaudit.ReduceVirtualStorage();
                    //增加入库仓库虚拟库存
                    billaudit.AddVirtualStorage();
                }

                #endregion
            }
            else if (billname == "stockinventorybill")
            {
                #region 盘点单

                //检查零库存，零库存需要指定成本价
                if (bill.content["costpriceinput"] != null)
                {
                    //已经手工指定成本
                    bill.SetCostPriceInput(bill.content.Value<string>("costpriceinput"));
                }
                else
                {
                    string result = billaudit.CheckStorageZero();
                    if (!string.IsNullOrEmpty(result))
                    {
                        db.Discard();
                        friendlymessage = "过账失败！需要手工指定入库成本，请打开本张单据的编辑页面进行过账。";
                        return result;
                    }
                }
                //计算成本
                billaudit.GetCostPrice();
                if (bill.id == 0)
                    db.Add("bill", bill);
                else
                    db.Edit("bill", bill);

                //增加账面库存
                billaudit.AddStorage();

                //增加虚拟库存
                if (options.content.Value<bool>("strictordermanage"))
                {
                    billaudit.AddVirtualStorage();
                }
                #endregion
            }
            else if (billname == "paybill")
            {
                #region 付款单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            + item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.ReducePayable();
                billaudit.ReduceBank();
                #endregion
            }
            else if (billname == "gatheringbill")
            {
                #region 收款单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            + item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.ReduceReceivable();
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "payablebill")
            {
                #region 应付调整单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            - item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }

                billaudit.AddPayable();
                #endregion
            }
            else if (billname == "receivablebill")
            {
                #region 应收调整单
                if (bill.content["checkoutbills"] != null)
                {
                    foreach (var item in bill.content.Value<JArray>("checkoutbills").Values<JObject>())
                    {
                        var checkoutbill = db.First("bill", item.Value<int>("billid"));
                        checkoutbill.content["checkouttotal"] = checkoutbill.content.Value<decimal>("checkouttotal")
                            - item.Value<decimal>("checkouttotal");
                        db.Edit("bill", checkoutbill);
                    }
                }
                billaudit.AddReceivable();
                #endregion
            }
            else if (billname == "feebill")
            {
                #region 费用单
                billaudit.AddPayable();
                billaudit.ReduceBank();
                #endregion
            }
            else if (billname == "earningbill")
            {
                #region 收入单
                billaudit.AddReceivable();
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "transferbill")
            {
                #region 转款单
                billaudit.AddBank();
                #endregion
            }
            else if (billname == "saleinvoicebill")
            {
                #region 销售发票
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    var invoicebill = db.First("bill", item.Value<int>("billid"));
                    invoicebill.content["invoicetotal"] = invoicebill.content.Value<decimal>("invoicetotal")
                        + item.Value<decimal>("invoicetotal");
                    db.Edit("bill", invoicebill);
                }
                #endregion
            }
            else if (billname == "purchaseinvoicebill")
            {
                #region 采购发票
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    var invoicebill = db.First("bill", item.Value<int>("billid"));
                    invoicebill.content["invoicetotal"] = invoicebill.content.Value<decimal>("invoicetotal")
                        + item.Value<decimal>("invoicetotal");
                    db.Edit("bill", invoicebill);
                }
                #endregion
            }

            return string.Empty;
        }

        public static void UnModifyVirtualStorage(this TableModel bill, DBHelper db)
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            if (billname == "purchaseorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") - item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate;
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") - item.Value<decimal>("qty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate;
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 删除虚拟库存明细

                    db.ExcuteNoneQuery("delete from virtualstoragedetail where (content->>'billid')::int=" + bill.id);
                    #endregion
                }
            }
            else if (billname == "saleorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate;
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate;
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 删除虚拟库存明细

                    db.ExcuteNoneQuery("delete from virtualstoragedetail where (content->>'billid')::int=" + bill.id);
                    #endregion
                }
            }
        }

        public static void ModifyVirtualStorageForAbort(this TableModel bill, DBHelper db)
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            if (billname == "purchaseorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") - item.Value<decimal>("qty") + item.Value<decimal>("deliveryqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate + (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") - item.Value<decimal>("qty") + item.Value<decimal>("deliveryqty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate + (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 更新虚拟库存明细
                    TableModel virtualstoragedetail = db.First("select * from virtualstoragedetail where content->>'billdetailid'='" + item.Value<string>("uuid") + "'");

                    virtualstoragedetail.content["qty"] = item.Value<decimal>("deliveryqty");
                    virtualstoragedetail.content["total"] = item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate;
                    virtualstoragedetail.content["price"] = item.Value<decimal>("discountprice") / taxrate;

                    db.Edit("virtualstoragedetail", virtualstoragedetail);
                    #endregion
                }
            }
            else if (billname == "saleorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty") - item.Value<decimal>("deliveryqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate - (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty") - item.Value<decimal>("deliveryqty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate - (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 更新虚拟库存明细
                    TableModel virtualstoragedetail = db.First("select * from virtualstoragedetail where content->>'billdetailid'='" + item.Value<string>("uuid") + "'");

                    virtualstoragedetail.content["qty"] = -item.Value<decimal>("deliveryqty");
                    virtualstoragedetail.content["total"] = -item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate;
                    virtualstoragedetail.content["price"] = item.Value<decimal>("discountprice") / taxrate;

                    db.Edit("virtualstoragedetail", virtualstoragedetail);
                    #endregion
                }
            }
        }

        public static void ModifyVirtualStorageForUnAbort(this TableModel bill, DBHelper db)
        {
            string billname = bill.content.Value<string>("billname");
            int stockid = bill.content.Value<int>("stockid");
            if (billname == "purchaseorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") + item.Value<decimal>("qty") - item.Value<decimal>("deliveryqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate - (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") + item.Value<decimal>("qty") - item.Value<decimal>("deliveryqty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") + item.Value<decimal>("discounttotal") / taxrate - (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 更新虚拟库存明细
                    TableModel virtualstoragedetail = db.First("select * from virtualstoragedetail where content->>'billdetailid'='" + item.Value<string>("uuid") + "'");

                    virtualstoragedetail.content["qty"] = item.Value<decimal>("qty");
                    virtualstoragedetail.content["total"] = item.Value<decimal>("discounttotal") / taxrate;
                    virtualstoragedetail.content["price"] = item.Value<decimal>("discountprice") / taxrate;

                    db.Edit("virtualstoragedetail", virtualstoragedetail);
                    #endregion
                }
            }
            else if (billname == "saleorder")
            {
                foreach (var item in bill.content.Value<JArray>("details").Values<JObject>())
                {
                    #region 更新虚拟库存
                    var product = db.First("product", item.Value<int>("productid"));
                    //decimal discount = item.Value<decimal>("discountrate") / 100M;
                    decimal taxrate = 1 + item.Value<decimal>("taxrate") / 100M;

                    JObject vs = product.content.Value<JObject>("virtualstorage");
                    decimal qty0 = vs.Value<JObject>("0").Value<decimal>("qty") - item.Value<decimal>("qty") + item.Value<decimal>("deliveryqty");
                    decimal total0 = vs.Value<JObject>("0").Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate + (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs["0"] = new JObject()
                    {
                        {"qty",qty0},
                        {"total",total0},
                        {"price",qty0==decimal.Zero?decimal.Zero:Math.Round(total0/qty0,4)}
                    };

                    decimal qty = vs.Value<JObject>(stockid.ToString()).Value<decimal>("qty") - item.Value<decimal>("qty") + item.Value<decimal>("deliveryqty");
                    decimal total = vs.Value<JObject>(stockid.ToString()).Value<decimal>("total") - item.Value<decimal>("discounttotal") / taxrate + (item.Value<decimal>("deliveryqty") * item.Value<decimal>("discountprice") / taxrate);
                    vs[stockid.ToString()] = new JObject()
                    {
                        {"qty",qty},
                        {"total",total},
                        {"price",qty==decimal.Zero?decimal.Zero:Math.Round(total/qty,4)}
                    };


                    db.Edit("product", product);
                    #endregion

                    #region 更新虚拟库存明细
                    TableModel virtualstoragedetail = db.First("select * from virtualstoragedetail where content->>'billdetailid'='" + item.Value<string>("uuid") + "'");

                    virtualstoragedetail.content["qty"] = -item.Value<decimal>("qty");
                    virtualstoragedetail.content["total"] = -item.Value<decimal>("discounttotal") / taxrate;
                    virtualstoragedetail.content["price"] = item.Value<decimal>("discountprice") / taxrate;

                    db.Edit("virtualstoragedetail", virtualstoragedetail);
                    #endregion
                }
            }
        }


        public static void SaveBillConfig(this TableModel billconfig, bool applyall)
        {
            using (DBHelper db = new DBHelper())
            {
                if (billconfig.id > 0)
                {
                    db.Edit("billconfig", billconfig);
                }
                else
                {
                    db.Add("billconfig", billconfig);
                }
                db.SaveChanges();
            }
            if (applyall)
            {
                List<string> billnames = new List<string>()
                {
                    "purchaseorder",
                    "purchasebill",
                    "purchasebackbill",
                    "saleorder",
                    "salebill",
                    "salebackbill"
                };

                foreach (string billname in billnames)
                {
                    SaveBillConfig(billname, billconfig);
                }
            }
        }

        private static void SaveBillConfig(string billname, TableModel billconfigtemplate)
        {
            using (DBHelper db = new DBHelper())
            {
                var billconfig = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
                if (billconfig == null)
                {
                    billconfig = new TableModel()
                    {
                        id = 0,
                        content = billconfigtemplate.content
                    };

                    billconfig.content["billname"] = billname;
                    db.Add("billconfig", billconfig);
                }
                else
                {
                    billconfig.content["taxformat"] = billconfigtemplate.content["taxformat"];
                    billconfig.content["discountformat"] = billconfigtemplate.content["discountformat"];
                    billconfig.content["taxrate"] = billconfigtemplate.content["taxrate"];
                    billconfig.content["showstandard"] = billconfigtemplate.content["showstandard"];
                    billconfig.content["showtype"] = billconfigtemplate.content["showtype"];
                    billconfig.content["showunit"] = billconfigtemplate.content["showunit"];
                    billconfig.content["showstorage"] = billconfigtemplate.content["showstorage"];
                    billconfig.content["showarea"] = billconfigtemplate.content["showarea"];
                    billconfig.content["showbarcode"] = billconfigtemplate.content["showbarcode"];
                    billconfig.content["keepemployeeandstock"] = billconfigtemplate.content["keepemployeeandstock"];

                    db.Edit("billconfig", billconfig);
                }

                db.SaveChanges();
            }
        }

        public static string GetBillCodeTemplate(string billname)
        {
            DBHelper db = new DBHelper();

            //获取编号配置
            var config = db.FirstOrDefault("select * from billconfig where content->>'billname'='" + billname + "'");
            string template = (config == null || config.content["billcodetemplate"] == null) ?
                GetBillCodeDefaultTemplate(billname) : config.content.Value<string>("billcodetemplate");

            //解析模板
            return template;
        }

        public static string GetBillCode(string billname, DBHelper db = null)
        {
            //解析模板
            return ParseBillCodeTemplate(GetBillCodeTemplate(billname), billname, db);
        }

        private static string ParseBillCodeTemplate(string template, string billname, DBHelper db = null)
        {
            if (db == null) db = new DBHelper();

            int ls = 1;
            if (template.Contains("MM") && template.Contains("dd"))
            {
                var last = db.FirstOrDefault("select * from bill where content->>'billname'='" + billname
                    + "' and substring(content->>'createtime' from 1 for 10)='" + DateTime.Now.ToString("yyyy-MM-dd") + "' order by char_length(content->>'billcode') desc,content->>'billcode' desc");
                if (last != null)
                {
                    string lastcode = last.content.Value<string>("billcode");
                    string[] ss1 = lastcode.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    ls = Convert.ToInt32(ss1[ss1.Length - 1]) + 1;
                }
            }

            Dictionary<string, Func<string>> dic = new Dictionary<string, Func<string>>()
            { 
                {"{yyyyMMdd}",()=>{return DateTime.Now.ToString("yyyyMMdd");}},
                {"{yyyy-MM-dd}",()=>{return DateTime.Now.ToString("yyyy-MM-dd");}},
                {"{yyMMdd}",()=>{return DateTime.Now.ToString("yyMMdd");}},
                {"{制单人编号}",()=>{return db.First("select * from employee where id="+PluginContext.Current.Account.Id).content.Value<string>("code");}},
                {"{0}",()=>{return ls.ToString().PadLeft(1,'0');}},
                {"{00}",()=>{ return ls.ToString().PadLeft(2,'0');}},
                {"{000}",()=>{ return ls.ToString().PadLeft(3,'0');}},
                {"{0000}",()=>{ return ls.ToString().PadLeft(4,'0');}},
                {"{00000}",()=>{ return ls.ToString().PadLeft(5,'0');}}
            };

            StringBuilder sb = new StringBuilder();

            string[] ss = template.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in ss)
            {
                if (s.StartsWith("{") && s.EndsWith("}") && dic.ContainsKey(s))
                {
                    sb.Append(dic[s]());
                }
                else
                {
                    sb.Append(s);
                }
            }

            return sb.ToString();
        }

        private static string GetBillCodeDefaultTemplate(string billname)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>() 
            { 
                {"purchaseorder","PO,-,{yyyyMMdd},-,{000}"},
                {"purchasebill","PB,-,{yyyyMMdd},-,{000}"},
                {"purchasebackbill","TH,-,{yyyyMMdd},-,{000}"},
                {"saleorder","SO,-,{yyyyMMdd},-,{000}"},
                {"salebill","XS,-,{yyyyMMdd},-,{000}"},
                {"salebackbill","XT,-,{yyyyMMdd},-,{000}"},
                {"gatheringbill","GB,-,{yyyyMMdd},-,{000}"},
                {"paybill","PB,-,{yyyyMMdd},-,{000}"},
                {"receivablebill","ST,-,{yyyyMMdd},-,{000}"},
                {"payablebill","FT,-,{yyyyMMdd},-,{000}"},
                {"stockinbill","RK,-,{yyyyMMdd},-,{000}"},
                {"stockoutbill","CK,-,{yyyyMMdd},-,{000}"},
                {"stockmovebill","DB,-,{yyyyMMdd},-,{000}"},
                {"stockinventorybill","PD,-,{yyyyMMdd},-,{000}"},
                {"feebill","FY,-,{yyyyMMdd},-,{000}"},
                {"earningbill","SR,-,{yyyyMMdd},-,{000}"},
                {"transferbill","ZK,-,{yyyyMMdd},-,{000}"},
                {"saleinvoicebill","XP,-,{yyyyMMdd},-,{000}"},
                {"purchaseinvoicebill","JP,-,{yyyyMMdd},-,{000}"}
            };

            if (!dic.ContainsKey(billname)) throw new KeyNotFoundException();

            return dic[billname];
        }
    }
}
