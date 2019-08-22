﻿using Dapper;
using Gurutw.Models;
using Gurutw.Repositorys;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Gurutw.Controllers
{
    public class MemberController : Controller
    {
        private readonly SqlConnection conn;
        private static string connString;
        public readonly MemberRepository _repo;
        private MvcDataBaseEntities db;

        public MemberController()
        {
            _repo = new MemberRepository();
            if (string.IsNullOrEmpty(connString))
            {
                connString = ConfigurationManager.ConnectionStrings["MvcDataBase"].ConnectionString;
            }
            conn = new SqlConnection(connString);
            db = new MvcDataBaseEntities();
        }

        //根據按+或- 來改變數量  
        public ActionResult CountCart(int? addpdid, int? lesspdid)
        {
            var user = int.Parse(Session["m_id"].ToString());
            int add = Convert.ToInt32(addpdid);

            int less = Convert.ToInt32(lesspdid);

            if (add != 0)
            {
                _repo.CountCartQuantity(add, user, 1);
            }
            else
            {
                _repo.CountCartQuantity(less, user, -1);
            }
            return RedirectToAction("Cart");
        }
        
        //刪除
        public ActionResult DelCart(int? pdid = 0)
        {
            if (pdid != 0)
            {
                string delsql = "DELETE from Shopping_Cart Where pd_id=@pdid";
                conn.Execute(delsql, new { pdid = pdid });
            }
            return RedirectToAction("Cart");
        }

        //根據會員id 把所有購物車商品找出來 
        public ActionResult Cart()
        {
            Response.Cache.SetNoStore();
            using (conn)
            {
                var nowtime = DateTime.Now;
                var user = int.Parse(Session["m_id"].ToString());
          
                string sql = 
                    "SELECT  dbo.Member.m_id, " +
                            "dbo.Shopping_Cart.pd_id, " +
                            "dbo.Product_Detail.pd_color, " +
                            "dbo.Product.p_unitprice,  " +
                            "dbo.Product.p_name,  " +
                            "( select top 1 dbo.Discount.d_discount " +
                            "from dbo.Discount " +
                            "where CONVERT(VARCHAR(10), GETDATE(), 120) " +
                            "between dbo.Discount.d_startdate " +
                            "And dbo.Discount.d_enddate And dbo.Discount.c_id = dbo.Category.c_id " +
                            "order by dbo.Discount.d_discount ) d_discount, " +
                            "( select top 1 dbo.Discount.d_activity " +
                            "from dbo.Discount where CONVERT(VARCHAR(10), GETDATE(), 120) " +
                            "between dbo.Discount.d_startdate And dbo.Discount.d_enddate " +
                            "And dbo.Discount.c_id = dbo.Category.c_id order by dbo.Discount.d_discount) d_name, " +
                            "( SELECT TOP 1 dbo.Product_Picture.pp_path " +
                            "FROM dbo.Product_Picture WHERE dbo.Product_Picture.p_id = dbo.Product.p_id) AS pp_path, " +
                            "dbo.Category.c_id, " +
                            "dbo.Shopping_Cart.cart_quantity " +
                    "FROM dbo.Product_Detail " +
                    "INNER JOIN dbo.Product ON dbo.Product_Detail.p_id = dbo.Product.p_id " +
                    "INNER JOIN dbo.Shopping_Cart ON dbo.Product_Detail.pd_id = dbo.Shopping_Cart.pd_id  " +
                    "INNER JOIN dbo.Member ON dbo.Shopping_Cart.m_id = dbo.Member.m_id " +
                    "INNER JOIN dbo.Category ON dbo.Product.c_id = dbo.Category.c_id  "  +
                    "WHERE dbo.Shopping_Cart.m_id = " + user; //依照會員id查詢

                var CartTotal = conn.Query(sql).ToList();
                ViewBag.od = CartTotal;
            }

            return View();
        }

        //輸入訂單資訊頁面
        public ActionResult OrderDeliver()
        {
            ViewBag.dw_id = new SelectList(db.Delive_Way, "dw_id", "dw_name");
            ViewBag.pay_id = new SelectList(db.Payment, "pay_id", "pay_name");

            return View();
        }

        //將訂單資訊 結合購物車 生成訂單 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OrderDeliver([Bind(Include = "o_receiver,o_address,pay_id,dw_id")]Order or)
        {
            var user = int.Parse(Session["m_id"].ToString());
            //Order or = new Order();
            if (ModelState.IsValid)
            {
                or.m_id = user;
                or.o_status = "0";
                or.o_date = DateTime.Now;
                db.Order.Add(or);
                db.SaveChanges();
                return RedirectToAction("CreateOrderDetail", new { id = or.o_id });
            }
            return View(or);
        }

        //將訂單的詳細資料 找出來
        public ActionResult CreateOrderDetail(int? id)
        {
            var user = int.Parse(Session["m_id"].ToString());
            var cart = db.Shopping_Cart.Where(x => x.m_id == user).ToList();

            //撈出每一筆資料
            foreach (var u in cart)
            {
                var pdetail = db.Product_Detail.Where(x => x.pd_id == u.pd_id).FirstOrDefault();
                var product = db.Product.Where(x => x.p_id == pdetail.p_id).FirstOrDefault();
                var date = DateTime.Now;
                var discountList = db.Discount.Where(x => x.c_id == product.c_id).ToList();
                float discount = 1;
                foreach (var t in discountList)
                {
                    if (t.d_startdate <= date && t.d_enddate >= date)
                    {
                        discount = (float)t.d_discount;
                    }
                    else
                    {
                        discount = 1;
                    }
                }
                Order_Detail od = new Order_Detail();
                od.pd_id = u.pd_id;
                od.o_id = id;
                od.od_quantity = u.cart_quantity;
                od.od_discount = discount;
                od.od_price = (float)product.p_unitprice;

                pdetail.pd_stock = pdetail.pd_stock - u.cart_quantity;
                pdetail.pd_onorder = pdetail.pd_onorder - u.cart_quantity;

                db.Order_Detail.Add(od);
                db.SaveChanges();
                //return RedirectToAction("result");
            }

            var uid = db.Member.Where(x => x.m_id == user).FirstOrDefault().m_email_id;
            var email = db.Member.Where(x => x.m_id == user).FirstOrDefault().m_email;
            string a = Convert.ToString(uid);
            //寄信給對方
            string cont;
            cont = "http://" + Request.Url.Authority + "/Member/result?uid=" + a;
            System.Net.Mail.MailMessage MyMail = new System.Net.Mail.MailMessage();//建立MAIL   
            MyMail.From = new System.Net.Mail.MailAddress("gurutw201905@gmail.com", "Guru");//寄信人   
            MyMail.To.Add(new System.Net.Mail.MailAddress(email));//收信人1   
                                                                  //MyMail.To.Add(new System.Net.Mail.MailAddress("123@yahoo.com.tw"));//收信人2   
            MyMail.Subject = "訂單成立成功！";//主題   
            MyMail.Body = "您好\n\n您的訂單於剛剛成立，謝謝你的支持\n\n在以下網址能夠返回網站看到剛剛的訂單\n\n" + cont + "\n\n 如果這不是您剛剛做的動作，請直接刪除這封電子郵件！\n\n Guru 團隊 敬上";//內容   
            System.Net.Mail.SmtpClient Client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);//GMAIL主機   
                                                                                                      //System.Net.Mail.SmtpClient Client = new System.Net.Mail.SmtpClient("msa.hinet.net");//hinet主機   
            Client.Credentials = new System.Net.NetworkCredential("gurutw201905@gmail.com", "wearethe@1");//帳密，Hinet不用但須在它的ADLS(區段)裡面   
            Client.EnableSsl = true;//Gmail需啟動SSL，Hinet不用   
            Client.Send(MyMail);//寄出  
         
            return RedirectToAction("result");       
        }
  
        //訂單結果
        public ActionResult result(string uid)
        {
            var user = int.Parse(Session["m_id"].ToString());
            using (conn)
            {
                string sql2 = "SELECT dbo.[Order].o_id, dbo.[Order].o_status, dbo.[Order].o_date, dbo.[Order].o_delivedate, dbo.[Order].o_receiver, dbo.[Order].o_address, " +
                            "dbo.Payment.pay_name, dbo.Order_Detail.od_quantity, dbo.Order_Detail.od_price, dbo.Order_Detail.od_discount, " +
                            "dbo.Product_Detail.pd_color, dbo.Product.p_name, dbo.Product.p_unitprice, " +
                                "(SELECT TOP(1) pp_path FROM dbo.Product_Picture WHERE(dbo.Product.p_id = p_id)) AS pp_path, " +
                            "Payment_1.pay_name AS Expr1, dbo.Delive_Way.dw_name FROM dbo.[Order] INNER JOIN " +
                            "dbo.Order_Detail ON dbo.[Order].o_id = dbo.Order_Detail.o_id INNER JOIN " +
                            "dbo.Payment ON dbo.[Order].pay_id = dbo.Payment.pay_id INNER JOIN " +
                            "dbo.Product_Detail ON dbo.Order_Detail.pd_id = dbo.Product_Detail.pd_id INNER JOIN " +
                            "dbo.Product ON dbo.Product_Detail.p_id = dbo.Product.p_id INNER JOIN " +
                            "dbo.Delive_Way ON dbo.[Order].dw_id = dbo.Delive_Way.dw_id INNER JOIN " +
                            "dbo.Payment AS Payment_1 ON dbo.[Order].pay_id = Payment_1.pay_id " +
                            "WHERE(dbo.[Order].m_id = " + user + " AND dbo.[Order].o_status != 4 AND dbo.[Order].o_status != 8 and dbo.[Order].o_id = dbo.[Order_Detail].o_id );";

                var o = conn.Query(sql2, new { mid = user });

                ViewBag.t = o;
            }


            return View();
        }


        public ActionResult Outcome(int? Id)
        {
            var user = int.Parse(Session["m_id"].ToString());
            var check = db.Order.Where(x => x.o_id == Id).FirstOrDefault().m_id;

            if (user != check)
            {
                return RedirectToAction("result");
            }

            using (conn)
            {
                string sql1 = "SELECT dbo.Member.m_name, dbo.Order_Detail.od_price, dbo.Order_Detail.od_discount, dbo.Order_Detail.od_quantity, " +
                            "dbo.Product_Detail.pd_color, dbo.[Order].o_date, dbo.[Order].o_receiver, dbo.[Order].o_address, dbo.Product.p_name, " +
                            "dbo.Product.p_unitprice, (SELECT TOP(1) pp_path FROM dbo.Product_Picture WHERE(dbo.Product.p_id = p_id)) AS pp_path, dbo.Delive_Way.dw_name, dbo.Payment.pay_name FROM dbo.[Order] INNER JOIN " +
                            "dbo.Member ON dbo.[Order].m_id = dbo.Member.m_id INNER JOIN " +
                            "dbo.Order_Detail ON dbo.[Order].o_id = dbo.Order_Detail.o_id INNER JOIN " +
                            "dbo.Product_Detail ON dbo.Order_Detail.pd_id = dbo.Product_Detail.pd_id INNER JOIN " +
                            "dbo.Product ON dbo.Product_Detail.p_id = dbo.Product.p_id INNER JOIN " +
                            "dbo.Delive_Way ON dbo.[Order].dw_id = dbo.Delive_Way.dw_id INNER JOIN " +
                            "dbo.Payment ON dbo.[Order].pay_id = dbo.Payment.pay_id " +
                            "WHERE dbo.[Order].m_id = " + user + " and dbo.[Order_Detail].o_id = " + Id; 

                var o = conn.Query(sql1, new { mid = user }).ToList();
                ViewBag.t = o;
            }

            return View();
        }

        //會員管理 查看會員資料
        public ActionResult Member_Manager()
        {
            var user = int.Parse(Session["m_id"].ToString());
            Member memb = db.Member.Where(m => m.m_id == user).FirstOrDefault();
            ViewBag.memb = memb;
            return View();
        }

        //用partial view 改變會員管理頁面
        public ActionResult Account_DashBoard()
        {
            TempData["view"] = "Account_DashBoard";
            return RedirectToAction("Member_Manager");
        }

        public ActionResult Account_Information()
        {
            TempData["view"] = "Account_Information";
            return RedirectToAction("Member_Manager");
        }


        [HttpGet]
        public ActionResult Update_Profile()
        {
            return View();
        }
        //更新資料
        [HttpPost]
        public ActionResult Update_Profile(UpdateProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var userid = int.Parse(Session["m_id"].ToString());
            Member memb = db.Member.Where(x => x.m_id == userid).FirstOrDefault();
            memb.m_name = model.UserName;
            memb.m_email = model.Email;
            Session["m_name"] = memb.m_name;
            Session["m_email"] = memb.m_email;
            db.SaveChanges();
            return RedirectToAction("Member_Manager");
        }
        [HttpGet]
        public ActionResult Change_Pw()
        {
            return View();
        }
        //更新資料
        [HttpPost]
        public ActionResult Change_Pw(ChangePassword model)
        {
            var userid = int.Parse(Session["m_id"].ToString());
            Member memb = db.Member.Where(x => x.m_id == userid).FirstOrDefault();
            if (!ModelState.IsValid || model.CurrentPassword != memb.m_password)
            {
                return View(model);
            }

            if (model.CurrentPassword == memb.m_password)
            {
                memb.m_password = model.NewPassword;
                db.SaveChanges();
            }
            return View("ChangePwResult");
        }
    }
}