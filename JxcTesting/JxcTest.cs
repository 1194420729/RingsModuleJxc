using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Web.Security;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;

namespace JxcTesting
{
    [TestClass]
    public class JxcTest
    {
        private TestContext testContextInstance;
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        [TestInitialize]
        public void MyTestInitialize()
        {
            ClearAllData();
            InitBasicData();
        }
         
        [TestMethod]
        public void PurchaseOrderSaveTest()
        {
            InitOver();

            TableModel bill = CreatePurchaseOrder();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchaseorder/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchaseorder");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_purchaseorder");
                    Assert.AreEqual<decimal>(10000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(11300M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                }
            }

        }

        [TestMethod]
        public void PurchaseOrderEditTest()
        {
            InitOver();

            TableModel bill = CreatePurchaseOrder();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchaseorder/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchaseorder");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_purchaseorder");
                    Assert.AreEqual<decimal>(decimal.Zero, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchaseorder/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchaseorder");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_purchaseorder");
                    Assert.AreEqual<decimal>(10000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(11300M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                }
            }

        }

        [TestMethod]
        public void PurchaseBillSaveTest()
        {
            InitOver();

            TableModel bill = CreatePurchaseBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_purchasebill");
                    var product1 = db.First("product", 1);
                    var vendor = db.First("vendor", 1);
                    Assert.AreEqual<decimal>(11300M, vendor.content.Value<decimal>("payable"));

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(10000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(11300M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));

                }
            }

        }

        [TestMethod]
        public void PurchaseBillEditTest()
        {
            InitOver();

            TableModel bill = CreatePurchaseBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_purchasebill");
                    Assert.AreEqual<decimal>(decimal.Zero, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_purchasebill");
                    var product1 = db.First("product", 1);
                    var vendor = db.First("vendor", 1);
                    Assert.AreEqual<decimal>(11300M, vendor.content.Value<decimal>("payable"));

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(10000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(11300M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void PurchaseBackBillSaveTest()
        { 
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();            
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
 
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreatePurchaseBackBill(qty:15);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebackbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(case when billname='purchasebill' then total else -total end) as total," +
                        "sum(case when billname='purchasebill' then qty else -qty end) as qty," +
                        "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) as discounttotal from mvw_purchasebill");
                    var product1 = db.First("product", 1);
                    var vendor = db.First("vendor", 1);
                    Assert.AreEqual<decimal>(7910M, vendor.content.Value<decimal>("payable"));

                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(50M, Convert.ToDecimal(dt.Rows[0]["qty"]));

                    Assert.AreEqual<decimal>(7000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(7910M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));

                    Assert.AreEqual<decimal>(costmethod=="ydjqpj"?550M:600M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void PurchaseBackBillEditTest()
        {
            
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);
            //Console.WriteLine("costmethod:"+costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreatePurchaseBackBill(qty:15M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebackbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_purchasebill");
                    Assert.AreEqual<decimal>(22000M, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebackbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_purchasebill");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(case when billname='purchasebill' then total else -total end) as total," +
                        "sum(case when billname='purchasebill' then qty else -qty end) as qty," +
                        "sum(case when billname='purchasebill' then discounttotal else -discounttotal end) as discounttotal from mvw_purchasebill");
                    var product1 = db.First("product", 1);
                    var vendor = db.First("vendor", 1);
                    Assert.AreEqual<decimal>(7910M, vendor.content.Value<decimal>("payable"));

                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(50M, Convert.ToDecimal(dt.Rows[0]["qty"]));

                    Assert.AreEqual<decimal>(7000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(7910M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));

                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 550M : 600M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [TestMethod]
        public void SaleOrderSaveTest()
        {
            InitOver();

            TableModel bill = CreateSaleOrder(price:200M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/saleorder/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_saleorder");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_saleorder");
                    Assert.AreEqual<decimal>(20000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(22600M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                }
            }

        }

        [TestMethod]
        public void SaleOrderEditTest()
        {
            InitOver();

            TableModel bill = CreateSaleOrder(price:200M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/saleorder/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_saleorder");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_saleorder");
                    Assert.AreEqual<decimal>(decimal.Zero, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/saleorder/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }


                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_saleorder");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,sum(discounttotal) as discounttotal from mvw_saleorder");
                    Assert.AreEqual<decimal>(20000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(22600M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void SaleBillSaveTest()
        { 
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateSaleBill(price:200M,qty:15M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,"
                        + "sum(discounttotal) as discounttotal,sum(costtotal) as costtotal from mvw_salebill");
                    var product1 = db.First("product", 1);
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    Assert.AreEqual<decimal>(33900M, customer.content.Value<decimal>("receivable"));

                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(30000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(150M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(33900M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 550M : 600M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void SaleBillEditTest()
        { 
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateSaleBill(price: 200M, qty: 15M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_salebill");
                    Assert.AreEqual<decimal>(decimal.Zero, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }


                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(total) as total,sum(qty) as qty,"
                        + "sum(discounttotal) as discounttotal,sum(costtotal) as costtotal from mvw_salebill");
                    var product1 = db.First("product", 1);
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    Assert.AreEqual<decimal>(33900M, customer.content.Value<decimal>("receivable"));

                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(5M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(30000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(150M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(33900M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 550M : 600M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }
         
        [TestMethod]
        public void SaleBackBillSaveTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel bill = CreateSaleBackBill(price:200M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebackbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();

                    DataTable dt = db.QueryTable("select sum(case when billname='salebill' then total else -total end) as total," +
                        "sum(case when billname='salebill' then qty else -qty end) as qty,"
                        + "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                        "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal from mvw_salebill");
                    var product1 = db.First("product", 1);
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    Assert.AreEqual<decimal>(-22600M, customer.content.Value<decimal>("receivable"));

                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(-20000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(-100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(-22600M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                    Assert.AreEqual<decimal>(-10000M, Convert.ToDecimal(dt.Rows[0]["costtotal"]));
                }
            }

        }

        [TestMethod]
        public void SaleBackBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel bill = CreateSaleBackBill(price:200M);

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebackbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    DBHelper db = new DBHelper();
                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();
                    DataTable dt = db.QueryTable("select coalesce(sum(total),0) as total from mvw_salebill");
                    Assert.AreEqual<decimal>(decimal.Zero, Convert.ToDecimal(dt.Rows[0]["total"]));

                    response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebackbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    db.ExcuteNoneQuery("refresh materialized view concurrently mvw_salebill");
                    db.SaveChanges();

                    dt = db.QueryTable("select sum(case when billname='salebill' then total else -total end) as total," +
                        "sum(case when billname='salebill' then qty else -qty end) as qty,"
                        + "sum(case when billname='salebill' then discounttotal else -discounttotal end) as discounttotal," +
                        "sum(case when billname='salebill' then costtotal else -costtotal end) as costtotal from mvw_salebill");
                    var product1 = db.First("product", 1);
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    Assert.AreEqual<decimal>(-22600M, customer.content.Value<decimal>("receivable"));

                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(-20000M, Convert.ToDecimal(dt.Rows[0]["total"]));
                    Assert.AreEqual<decimal>(-100M, Convert.ToDecimal(dt.Rows[0]["qty"]));
                    Assert.AreEqual<decimal>(-22600M, Convert.ToDecimal(dt.Rows[0]["discounttotal"]));
                    Assert.AreEqual<decimal>(-10000M, Convert.ToDecimal(dt.Rows[0]["costtotal"]));
                }
            }

        }

        [TestMethod]
        public void GatheringBillAddTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel salebill = CreateSaleBill(price: 200M);//receivable:22600

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(salebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                 
            }

            TableModel bill = CreateGatheringBill(20000M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/gatheringbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    var bank = db.First("bank", 1);
                    Assert.AreEqual<decimal>(2600M, customer.content.Value<decimal>("receivable"));
                    Assert.AreEqual<decimal>(20000M, bank.content.Value<decimal>("total"));

                }

            }

        }

        [TestMethod]
        public void GatheringBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel salebill = CreateSaleBill(price: 200M);//receivable:22600

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(salebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                } 
            }

            TableModel bill = CreateGatheringBill(20000M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/gatheringbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/gatheringbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var customer = db.First("customer", bill.content.Value<int>("customerid"));
                    var bank = db.First("bank",1);
                    Assert.AreEqual<decimal>(2600M, customer.content.Value<decimal>("receivable"));
                    Assert.AreEqual<decimal>(20000M, bank.content.Value<decimal>("total"));

                }

            }

        }
 

        [TestMethod]
        public void PayBillAddTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();//payable:11300
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
             
            TableModel bill = CreatePayBill(10000M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/paybill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));
                    var bank = db.First("bank", 1);
                    Assert.AreEqual<decimal>(1300M, vendor.content.Value<decimal>("payable"));
                    Assert.AreEqual<decimal>(-10000M, bank.content.Value<decimal>("total"));

                }

            }

        }

        [TestMethod]
        public void PayBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();//11300
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
             
            TableModel bill = CreatePayBill(10000M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/paybill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/paybill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));
                    var bank = db.First("bank", 1);
                    Assert.AreEqual<decimal>(1300M, vendor.content.Value<decimal>("payable"));
                    Assert.AreEqual<decimal>(-10000M, bank.content.Value<decimal>("total"));

                }

            }

        }

        [TestMethod]
        public void ReceivableBillAddTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel salebill = CreateSaleBill(price: 200M);//receivable:22600

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(salebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }

            }

            TableModel bill = CreateReceivableBill(-2600M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/receivablebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var customer = db.First("customer", bill.content.Value<int>("customerid")); 
                    Assert.AreEqual<decimal>(20000M, customer.content.Value<decimal>("receivable")); 

                }

            }

        }

        [TestMethod]
        public void ReceivableBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel salebill = CreateSaleBill(price: 200M);//receivable:22600

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcsale/jxcsale/salebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(salebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
            }

            TableModel bill = CreateReceivableBill(-2600M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/receivablebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/receivablebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var customer = db.First("customer", bill.content.Value<int>("customerid")); 
                    Assert.AreEqual<decimal>(20000M, customer.content.Value<decimal>("receivable")); 

                }

            }

        }

        [TestMethod]
        public void PayableBillAddTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();//payable:11300
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel bill = CreatePayableBill(-1300M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/payablebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid")); 
                    Assert.AreEqual<decimal>(10000M, vendor.content.Value<decimal>("payable")); 

                }

            }

        }

        [TestMethod]
        public void PayableBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();//11300
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }

            TableModel bill = CreatePayableBill(-1300M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/payablebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcpay/jxcpay/payablebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid")); 
                    Assert.AreEqual<decimal>(10000M, vendor.content.Value<decimal>("payable")); 

                }

            }

        }


        [TestMethod]
        public void StockInBillAddTest()
        { 
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockInBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockinbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    
                    var product1 = db.First("product", 1);  

                    Assert.AreEqual<decimal>(30M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(30M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));
                     
                    Assert.AreEqual<decimal>( 3300M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [TestMethod]
        public void StockInBillEditTest()
        {
            InitOver();

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockInBill();
 
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockinbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");
                     
                    response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockinbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    
                    var product1 = db.First("product", 1);

                    Assert.AreEqual<decimal>(30M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(30M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(3300M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }


        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void StockOutBillAddTest()
        {
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockOutBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockoutbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();

                    var product1 = db.First("product", 1);

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1200M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void StockOutBillEditTest()
        {
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockOutBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockoutbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockoutbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();

                    var product1 = db.First("product", 1);

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1200M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void StockMoveBillAddTest()
        {
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockMoveBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockmovebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();

                    var product1 = db.First("product", 1);

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("2").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1200M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));
                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1000M, product1.content.Value<JObject>("storage").Value<JObject>("2").Value<decimal>("total"));

                }
            }

        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", @"testdata.csv", "testdata#csv", DataAccessMethod.Sequential)]
        [TestMethod]
        public void StockMoveBillEditTest()
        {
            string costmethod = TestContext.DataRow[0].ToString();
            InitOver(costmethod);

            TableModel purchasebill = CreatePurchaseBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }
            }
            TableModel purchasebill2 = CreatePurchaseBill(price: 120M);
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcpurchase/jxcpurchase/purchasebill/addauditsave",
                     "POST", JsonConvert.SerializeObject(purchasebill2));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存采购单据失败:" + message);
                }

            }

            TableModel bill = CreateStockMoveBill();

            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockmovebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcstorage/jxcstorage/stockmovebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();

                    var product1 = db.First("product", 1);

                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(10M, product1.content.Value<JObject>("storage").Value<JObject>("2").Value<decimal>("qty"));
                    Assert.AreEqual<decimal>(20M, product1.content.Value<JObject>("storage").Value<JObject>("0").Value<decimal>("qty"));

                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1200M, product1.content.Value<JObject>("storage").Value<JObject>("1").Value<decimal>("total"));
                    Assert.AreEqual<decimal>(costmethod == "ydjqpj" ? 1100M : 1000M, product1.content.Value<JObject>("storage").Value<JObject>("2").Value<decimal>("total"));

                }
            }

        }

        [TestMethod]
        public void FeeBillAddTest()
        {
            InitOver();

            
            TableModel bill = CreateFeeBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/feebill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));
                    
                    Assert.AreEqual<decimal>(1000M, vendor.content.Value<decimal>("payable"));
                     
                }

            }

        }

        [TestMethod]
        public void FeeBillEditTest()
        {
            InitOver(); 

            TableModel bill = CreateFeeBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/feebill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/feebill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var vendor = db.First("vendor", bill.content.Value<int>("vendorid"));

                    Assert.AreEqual<decimal>(1000M, vendor.content.Value<decimal>("payable"));

                }

            }

        }


        [TestMethod]
        public void EarningBillAddTest()
        {
            InitOver();


            TableModel bill = CreateEarningBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/earningbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var vendor = db.First("customer", bill.content.Value<int>("customerid"));

                    Assert.AreEqual<decimal>(1000M, vendor.content.Value<decimal>("receivable"));

                }

            }

        }

        [TestMethod]
        public void EarningBillEditTest()
        {
            InitOver();


            TableModel bill = CreateEarningBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/earningbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/earningbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var vendor = db.First("customer", bill.content.Value<int>("customerid"));

                    Assert.AreEqual<decimal>(1000M, vendor.content.Value<decimal>("receivable"));
                }
            }

        }


        [TestMethod]
        public void TransferBillAddTest()
        {
            InitOver(); 

            TableModel bill = CreateTransferBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/transferbill/addauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    DBHelper db = new DBHelper();
                    var bank1 = db.First("bank", bill.content.Value<int>("bankid"));
                    var bank2 = db.First("bank", bill.content.Value<int>("bankid2"));

                    Assert.AreEqual<decimal>(-100M, bank1.content.Value<decimal>("total"));
                    Assert.AreEqual<decimal>(100M, bank2.content.Value<decimal>("total"));
                }

            }

        }

        [TestMethod]
        public void TransferBillEditTest()
        {
            InitOver();

            TableModel bill = CreateTransferBill();
            using (var client = new CookieWebClient())
            {
                string response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/transferbill/addsave",
                    "POST", JsonConvert.SerializeObject(bill));
                string message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                if (message != "ok")
                {
                    Assert.Fail("保存单据失败:" + message);
                }
                else
                {
                    bill.id = JsonConvert.DeserializeObject<JObject>(response).Value<int>("id");

                    response = client.UploadString("http://localhost:59422/service/jxcaccounting/jxcaccounting/transferbill/editauditsave",
                    "POST", JsonConvert.SerializeObject(bill));
                    message = JsonConvert.DeserializeObject<JObject>(response).Value<string>("message");
                    if (message != "ok")
                    {
                        Assert.Fail("保存单据失败:" + message);
                        return;
                    }

                    DBHelper db = new DBHelper();
                    var bank1 = db.First("bank", bill.content.Value<int>("bankid"));
                    var bank2 = db.First("bank", bill.content.Value<int>("bankid2"));

                    Assert.AreEqual<decimal>(-100M, bank1.content.Value<decimal>("total"));
                    Assert.AreEqual<decimal>(100M, bank2.content.Value<decimal>("total"));
                }

            }

        }

        private TableModel CreatePurchaseOrder(decimal taxrate = 13M, decimal qty = 10M, decimal price = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "PO-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "purchaseorder");
            bill.content.Add("vendorid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty * price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price * tr);
                detail.Add("taxtotal", qty * price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreatePurchaseBill(decimal taxrate=13M, decimal qty=10M,decimal price=100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "PB-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "purchasebill");
            bill.content.Add("vendorid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty*price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price*tr);
                detail.Add("taxtotal", qty*price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreatePurchaseBackBill(decimal taxrate = 13M, decimal qty = 10M, decimal price = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "TH-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "purchasebackbill");
            bill.content.Add("vendorid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty * price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price * tr);
                detail.Add("taxtotal", qty * price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateSaleOrder(decimal taxrate = 13M, decimal qty = 10M, decimal price = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "SO-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "saleorder");
            bill.content.Add("customerid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty * price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price * tr);
                detail.Add("taxtotal", qty * price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateSaleBill(decimal taxrate = 13M, decimal qty = 10M, decimal price = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "XS-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "salebill");
            bill.content.Add("customerid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty * price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price * tr);
                detail.Add("taxtotal", qty * price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateSaleBackBill(decimal taxrate = 13M, decimal qty = 10M, decimal price = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "XT-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "salebackbill");
            bill.content.Add("customerid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            decimal tr = 1M + taxrate / 100M;
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("price", price);
                detail.Add("qty", qty);
                detail.Add("total", qty * price);
                detail.Add("taxrate", taxrate);
                detail.Add("discountrate", 100);
                detail.Add("taxprice", price * tr);
                detail.Add("taxtotal", qty * price * tr);
                detail.Add("discountprice", price * tr);
                detail.Add("discounttotal", qty * price * tr);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateGatheringBill(decimal total)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "GB-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());            
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "gatheringbill");
            bill.content.Add("customerid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            JObject detail = new JObject();
            detail.Add("uuid", Guid.NewGuid().ToString("N"));
            detail.Add("bankid", 1);           
            detail.Add("total", total); 
            details.Add(detail);
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreatePayBill(decimal total)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "PB-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "paybill");
            bill.content.Add("vendorid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            JObject detail = new JObject();
            detail.Add("uuid", Guid.NewGuid().ToString("N"));
            detail.Add("bankid", 1);
            detail.Add("total", total);
            details.Add(detail);
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateReceivableBill(decimal total)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "ST-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "receivablebill");
            bill.content.Add("customerid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());            
            bill.content.Add("total", total);
            #endregion

            return bill;
        }

        private TableModel CreatePayableBill(decimal total)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "FT-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "payablebill");
            bill.content.Add("vendorid", 1);
            bill.content.Add("employeeid", 1);
            bill.content.Add("attachments", new JArray());
            bill.content.Add("total", total);
            #endregion

            return bill;
        }

        private TableModel CreateStockInBill(decimal qty = 10M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "RK-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "stockinbill");            
            bill.content.Add("employeeid", 1);
            bill.content.Add("inouttypeid", 6);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();            
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);                
                detail.Add("qty", qty);  
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateStockOutBill(decimal qty = 10M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "CK-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "stockoutbill");
            bill.content.Add("employeeid", 1);
            bill.content.Add("inouttypeid", 1);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("qty", qty);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateStockMoveBill(decimal qty = 10M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "DB-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("stockid", 1);
            bill.content.Add("stockid2", 2);
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "stockmovebill");
            bill.content.Add("employeeid", 1); 
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("productid", i);
                detail.Add("qty", qty);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateFeeBill(decimal total = 100M,int? vendorid=1)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "FY-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());          
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "feebill");
            bill.content.Add("employeeid", 1);
            bill.content.Add("vendorid", vendorid);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("feeid", i);
                detail.Add("total", total);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateEarningBill(decimal total = 100M, int? customerid = 1)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "SR-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "earningbill");
            bill.content.Add("employeeid", 1);
            bill.content.Add("customerid", customerid);
            bill.content.Add("attachments", new JArray());
            JArray details = new JArray();
            for (int i = 1; i <= 10; i++)
            {
                JObject detail = new JObject();
                detail.Add("uuid", Guid.NewGuid().ToString("N"));
                detail.Add("earningid", i);
                detail.Add("total", total);
                details.Add(detail);
            }
            bill.content.Add("details", details);
            #endregion

            return bill;
        }

        private TableModel CreateTransferBill(decimal total = 100M)
        {
            #region create bill
            TableModel bill = new TableModel();
            bill.content = new JObject();
            bill.content.Add("billcode", "ZK-" + DateTime.Now.Ticks + "-" + new Random().Next(100, 999).ToString());
            bill.content.Add("billdate", DateTime.Now.ToString("yyyy-MM-dd"));
            bill.content.Add("billname", "transferbill");
            bill.content.Add("employeeid", 1);
            bill.content.Add("bankid", 1);
            bill.content.Add("bankid2", 2);
            bill.content.Add("total", total);
            bill.content.Add("attachments", new JArray());
             
            #endregion

            return bill;
        }
         

        private void InitBasicData()
        {
            using (DBHelper db = new DBHelper())
            {
                #region 产品资料
                for (int i = 1; i <= 10; i++)
                {
                    TableModel product = new TableModel();
                    product.content = new JObject();
                    product.content.Add("code", i.ToString().PadLeft(3, '0'));
                    product.content.Add("name", "产品" + i.ToString().PadLeft(3, '0'));
                    db.Add("product", product);
                }
                #endregion
                #region 客户资料
                for (int i = 1; i <= 10; i++)
                {
                    TableModel customer = new TableModel();
                    customer.content = new JObject();
                    customer.content.Add("code", i.ToString().PadLeft(3, '0'));
                    customer.content.Add("name", "客户" + i.ToString().PadLeft(3, '0'));
                    db.Add("customer", customer);
                }
                #endregion
                #region 供应商资料
                for (int i = 1; i <= 10; i++)
                {
                    TableModel vendor = new TableModel();
                    vendor.content = new JObject();
                    vendor.content.Add("code", i.ToString().PadLeft(3, '0'));
                    vendor.content.Add("name", "产品" + i.ToString().PadLeft(3, '0'));
                    db.Add("vendor", vendor);
                }
                #endregion
                #region 仓库资料
                for (int i = 1; i <= 2; i++)
                {
                    TableModel stock = new TableModel();
                    stock.content = new JObject();
                    stock.content.Add("code", i.ToString().PadLeft(3, '0'));
                    stock.content.Add("name", "产品" + i.ToString().PadLeft(3, '0'));
                    db.Add("stock", stock);
                }
                #endregion
                #region 员工资料
                for (int i = 1; i <= 10; i++)
                {
                    TableModel employee = new TableModel();
                    employee.content = new JObject();
                    employee.content.Add("code", i.ToString().PadLeft(3, '0'));
                    employee.content.Add("name", "员工" + i.ToString().PadLeft(3, '0'));
                    employee.content.Add("username", i.ToString().PadLeft(3, '0'));
                    employee.content.Add("password", FormsAuthentication.HashPasswordForStoringInConfigFile("000000", "MD5"));
                    db.Add("employee", employee);
                }
                #endregion
                #region 资金账户
                for (int i = 1; i <= 10; i++)
                {
                    TableModel bank = new TableModel();
                    bank.content = new JObject();
                    bank.content.Add("code", i.ToString().PadLeft(3, '0'));
                    bank.content.Add("name", "银行账户" + i.ToString().PadLeft(3, '0'));
                    
                    db.Add("bank", bank);
                }
                #endregion

                #region 出入库类型
                for (int i = 1; i <= 10; i++)
                {
                    string name =i<=5? "出库":"入库";
                    TableModel inouttype = new TableModel();
                    inouttype.content = new JObject();
                    inouttype.content.Add("code", i.ToString().PadLeft(3, '0'));
                    inouttype.content.Add("name", name + i.ToString().PadLeft(3, '0'));
                    inouttype.content.Add("direction", name);

                    db.Add("inouttype", inouttype);
                }
                #endregion

                #region 费用类型
                for (int i = 1; i <= 10; i++)
                {
                    TableModel fee = new TableModel();
                    fee.content = new JObject();
                    fee.content.Add("code", i.ToString().PadLeft(3, '0'));
                    fee.content.Add("name", "费用类型" + i.ToString().PadLeft(3, '0'));

                    db.Add("fee", fee);
                }
                #endregion

                #region 收入类型
                for (int i = 1; i <= 10; i++)
                {
                    TableModel earning = new TableModel();
                    earning.content = new JObject();
                    earning.content.Add("code", i.ToString().PadLeft(3, '0'));
                    earning.content.Add("name", "收入类型" + i.ToString().PadLeft(3, '0'));

                    db.Add("earning", earning);
                }
                #endregion

                db.SaveChanges();
            }
        }

        private void InitOver(string costmethod="ydjqpj")
        {
            using (DBHelper db = new DBHelper())
            {
                TableModel option = new TableModel();
                option.content = new JObject();
                option.content.Add("digit", 2);
                option.content.Add("costmethod", costmethod);
                option.content.Add("initoverdate", "2017-01-01");

                db.Add("option", option);
                db.SaveChanges();
            }
        }

        private void ClearAllData()
        {
            using (DBHelper db = new DBHelper())
            {
                db.Truncate("bank");
                db.Truncate("bankdetail");
                db.Truncate("bill");
                db.Truncate("billconfig");
                db.Truncate("category");
                db.Truncate("customer");
                db.Truncate("customerproduct");
                db.Truncate("department");
                db.Truncate("earning");
                db.Truncate("employee");
                db.Truncate("fee");
                db.Truncate("log");
                db.Truncate("option");
                db.Truncate("payabledetail");
                db.Truncate("product");
                db.Truncate("receivabledetail");
                db.Truncate("stock");
                db.Truncate("storagedetail");
                db.Truncate("vendor");
                db.Truncate("vendorproduct");
                db.Truncate("virtualstoragedetail");
                db.Truncate("inouttype");

                db.ExcuteNoneQuery("ALTER SEQUENCE bank_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE bankdetail_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE bill_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE billconfig_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE category_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE customer_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE customerproduct_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE department_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE earning_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE employee_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE fee_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE log_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE option_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE payabledetail_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE product_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE receivabledetail_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE stock_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE storagedetail_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE vendor_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE vendorproduct_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE virtualstoragedetail_id_seq RESTART WITH 1");
                db.ExcuteNoneQuery("ALTER SEQUENCE inouttype_id_seq RESTART WITH 1");

                db.SaveChanges();
            }
        }
    }
}
