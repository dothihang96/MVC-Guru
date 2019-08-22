﻿using Gurutw.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Gurutw.ViewModels;
using System.Web.Security;
using RegisterViewModel = Gurutw.ViewModels.RegisterViewModel;
using System.Data.SqlClient;
using System.Configuration;
using Dapper;
using Gurutw.Repositorys;

namespace Gurutw.Controllers
{
    public class HomeController : Controller
    {
        private readonly SqlConnection conn;
        private static string connString;
        int nowp = 0;
        private MvcDataBaseEntities db = new MvcDataBaseEntities();
        HomeRepository homeRepository = new HomeRepository();

        //連接資料庫
        public HomeController()
        {
            if (string.IsNullOrEmpty(connString))
            {
                connString = ConfigurationManager.ConnectionStrings["MvcDataBase"].ConnectionString;
            }
            conn = new SqlConnection(connString);
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Registration()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Registration(RegisterViewModel m)
        {
            //gmail的資料
            Guid GmailId = Guid.NewGuid();
            Session["regis-user"] = m.UserName;
            Random rd = new Random();
            
            //random值 發認證信
            var code = rd.Next(100000, 999999);
            if (!ModelState.IsValid)
            {
                return View(m);
            }

            Member user = db.Member.Where(x => x.m_email == m.Email).FirstOrDefault();

            //user 已經存在 
            if (user != null)
            {
                ModelState.AddModelError("", "The email is invalid.");
                return View();
            }
            //新增進資料庫
            else
            {
                Member memb = new Member()
                {
                    m_name = m.UserName,
                    m_email = m.Email,
                    m_password = m.Password,
                    m_status = "0",
                    m_verification = code.ToString(),
                    m_email_id = GmailId
                };

                db.Member.Add(memb);
                db.SaveChanges();
                Session["memb_id"] = memb.m_id;

                //寄信給對方 確認信箱有效
                var email = db.Member.Where(x => x.m_id == memb.m_id).FirstOrDefault().m_email;    //收信人的email            

                System.Net.Mail.MailMessage MyMail = new System.Net.Mail.MailMessage();//建立MAIL   
                MyMail.From = new System.Net.Mail.MailAddress("gurutw201905@gmail.com", "GuruTw");//寄信人   
                MyMail.To.Add(new System.Net.Mail.MailAddress(email));//收信人1     
                MyMail.Subject = "Welcome to Guru. Please verify your account to become Guru's member.";//主題   
                MyMail.Body = "Hello,\n\n Thank you for your registration\n\n This is your account verification code \n\n" + code + "\n\n Best Regards,\n\n GuruTW";//內容   
                System.Net.Mail.SmtpClient Client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);//GMAIL主機   
                                                                                                          //System.Net.Mail.SmtpClient Client = new System.Net.Mail.SmtpClient("msa.hinet.net");//hinet主機   
                Client.Credentials = new System.Net.NetworkCredential("gurutw201905@gmail.com", "wearethe@1");//帳密，Hinet不用但須在它的ADLS(區段)裡面   
                Client.EnableSsl = true;//Gmail需啟動SSL，Hinet不用   
                Client.Send(MyMail);//寄出

                return View("VerifyRegistration");
            }

        }



        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyRegisCode(RegisVerifyCodeViewModel c)
        {
            Session["wrong-code"] = null;
            var membid = int.Parse(Session["memb_id"].ToString());
            var memb = db.Member.Where(x => x.m_id == membid).FirstOrDefault();

            if (!ModelState.IsValid)
            {
                return View("VerifyRegistration");
            }
            //如果認證過 把狀態改為1
            if (memb.m_verification.ToString() == c.Code)
            {
                memb.m_status = "1";
                db.SaveChanges();
                return View("RegisResult");
            }
            //認證碼錯誤
            else
            {
                Session["wrong-code"] = "Verification failed.";
                return View("VerifyRegistration");
            }

        }

        [HttpGet]
        public ActionResult SignIn()
        {
            return View();
        }

        [HttpPost]
        public ActionResult SignIn(SignInViewModel m)
        {
            //未通過Model驗證
            if (!ModelState.IsValid)
            {
                return View(m);
            }

            //通過Model驗證
            string email = HttpUtility.HtmlEncode(m.Email);
            string password = HttpUtility.HtmlEncode(m.Password);

            //以Name及Password查詢比對Account資料表記錄
            Member user = db.Member.Where(x => x.m_email == email && x.m_password == password).FirstOrDefault();

            if (user == null)
            {
                ModelState.AddModelError("", "The email or password is invalid.");
                return View();
            }

            Session["m_name"] = user.m_name.ToString();
            Session["m_id"] = user.m_id;

            //Create FormsAuthenticationTicket
            var ticket = new FormsAuthenticationTicket(
            version: 1,
            name: user.m_email.ToString(), //可以放使用者Id
            issueDate: DateTime.UtcNow,//現在UTC時間
            expiration: DateTime.UtcNow.AddMinutes(30),//Cookie有效時間=現在時間往後+30分鐘
            isPersistent: true,// 是否要記住我 true or false
            userData: "", //可以放使用者角色名稱
            cookiePath: FormsAuthentication.FormsCookiePath);

            // Encrypt the ticket.
            var encryptedTicket = FormsAuthentication.Encrypt(ticket); //把驗證的表單加密

            // Create the cookie.
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
            Response.Cookies.Add(cookie);

            // Redirect back to original URL.
            var url = FormsAuthentication.GetRedirectUrl(email, true);

            //Response.Redirect(FormsAuthentication.GetRedirectUrl(name, true));

            return RedirectToAction("Index");
        }

        public ActionResult Logout()
        {
            Session["m_id"] = null;
            Session["m_name"] = null;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Search(string keywords)
        {
            if (string.IsNullOrEmpty(keywords))
            {
                return RedirectToAction("Index");
            }
            using (conn)
            {
                string sql = 
                    "SELECT distinct dbo.Product.p_id, " +
                    "dbo.Product.p_name, " +
                    "dbo.Product.p_lauchdate, " +
                    "dbo.Product.p_unitprice, " +
                       "( " +
                           "SELECT c.c_name " +
                           "FROM Category c " +
                           "WHERE c.c_id = dbo.Product.c_id " +
                       ")AS c_name, " +
                    "pic_path = STUFF(( " +
                        "SELECT ',' + dbo.Product_Picture.pp_path " +
                        "FROM dbo.Product_Picture " +
                        "where dbo.Product.p_id = dbo.Product_Picture.p_id " +
                        "FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''), " +
                        " ( SELECT TOP 1 dbo.Discount.d_discount " +
                        " FROM dbo.Discount " +
                        " WHERE dbo.Product.c_id = dbo.Discount.c_id " +
                        " ORDER BY dbo.Discount.d_discount " +
                        " )AS d_discount, " +
                        "( SELECT TOP 1 dbo.Discount.d_startdate " +
                        "FROM dbo.Discount " +
                        "WHERE dbo.Product.c_id = dbo.Discount.c_id " +
                        "AND DATEADD(HH,+8, GETDATE() ) BETWEEN dbo.Discount.d_startdate AND dbo.Discount.d_enddate " +
                        "ORDER BY dbo.Discount.d_discount " +
                        ")AS d_startdate, " +
                        "( SELECT TOP 1 dbo.Discount.d_enddate " +
                        "FROM dbo.Discount " +
                        "WHERE dbo.Product.c_id = dbo.Discount.c_id " +
                        "AND DATEADD(HH,+8, GETDATE() ) BETWEEN dbo.Discount.d_startdate AND dbo.Discount.d_enddate " +
                        "ORDER BY dbo.Discount.d_discount " +
                        ")AS d_enddate " +
                    "FROM dbo.Product " +
                    "INNER JOIN dbo.Product_Picture ON dbo.Product.p_id = dbo.Product_Picture.p_id " +
                    "where dbo.Product.p_name like" + "'%" + keywords + "%' ";

                var search_product_list = conn.Query(sql).ToList();
                ViewBag.searchlist = search_product_list;
            }
            return View();
        }

        /*鍵盤分類頁*/
        public ActionResult Keyboard_Category()
        {
            using (conn)
            {
                string sql = homeRepository.GetCategorySqlString(1);
                var product = conn.Query(sql).ToList();
                ViewBag.p = product;
            }

            return View();
        }

        /*滑鼠分類頁*/
        public ActionResult Mouse_Category()
        {
            using (conn)
            {     
                string sql = homeRepository.GetCategorySqlString(2);
                var product = conn.Query(sql).ToList();
                ViewBag.p = product;
            }

            return View();
        }

        /*耳機分類頁*/
        public ActionResult Headset_Category()
        {
            using (conn)
            {
                string sql = homeRepository.GetCategorySqlString(3);
                var product = conn.Query(sql).ToList();
                ViewBag.p = product;
            }

            return View();
        }

        /*鍵盤產品頁*/
        public ActionResult Keyboard_item(int id=0)
        {
            if (id == 0)
            {
                nowp = Convert.ToInt32(Session["nowproduct"].ToString());
                id = nowp;
            }

            //避免之後會下 別的linq語法 改成在這裡ToList 
            ViewBag.p_List = homeRepository.GetProductData(id).ToList();
            ViewBag.pd_List = homeRepository.GetProductDetailData(id).ToList();
            ViewBag.pp_List = homeRepository.GetProductPicturelData(id).ToList();
            ViewBag.pf_List = homeRepository.GetProductFeaturelData(id).ToList();
            ViewBag.classift_List = homeRepository.GetClassifylData(id).ToList();

            Session["nowproduct"] = id;
            Session["Nowitem"] = "Keyboard_item";
            return View();
        }

        /*滑鼠產品頁*/
        public ActionResult Mouse_item(int id = 0)
        {
            if (id == 0)
            {
                nowp = Convert.ToInt32(Session["nowproduct"].ToString());
                id = nowp;
            }

            //避免之後會下 別的linq語法 改成在這裡ToList 
            ViewBag.p_List = homeRepository.GetProductData(id).ToList();
            ViewBag.pd_List = homeRepository.GetProductDetailData(id).ToList();
            ViewBag.pp_List = homeRepository.GetProductPicturelData(id).ToList();
            ViewBag.pf_List = homeRepository.GetProductFeaturelData(id).ToList();
            ViewBag.classift_List = homeRepository.GetClassifylData(id).ToList();

            Session["nowproduct"] = id;
            Session["Nowitem"] = "Mouse_item";
            return View();
        }

        /*耳機產品頁*/
        public ActionResult Headset_item(int id = 0)
        {
            if (id == 0)
            {
                nowp = Convert.ToInt32(Session["nowproduct"].ToString());
                id = nowp;
            }

            //避免之後會下 別的linq語法 改成在這裡ToList 
            ViewBag.p_List = homeRepository.GetProductData(id).ToList();
            ViewBag.pd_List = homeRepository.GetProductDetailData(id).ToList();
            ViewBag.pp_List = homeRepository.GetProductPicturelData(id).ToList();
            ViewBag.pf_List = homeRepository.GetProductFeaturelData(id).ToList();
            ViewBag.classift_List = homeRepository.GetClassifylData(id).ToList();

            Session["nowproduct"] = id;
            Session["Nowitem"] = "Headset_item";
            return View();
        }

        [HttpPost]
        public ActionResult PassData(int id, int count)
        {
            //temp id count
            return View();
        }

        public ActionResult UserCart(int? num,int? id)
        {
            //確認登入狀態
            if(Session["m_id"] == null)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                var userr = int.Parse(Session["m_id"].ToString());
                using (conn)
                {
                    //將該筆商品 加入購物車
                    if (num != null)
                    {
                        string sql = "SELECT *FROM dbo.Shopping_Cart WHERE dbo.Shopping_Cart.m_id =@mid  AND dbo.Shopping_Cart.pd_id =@pdid";
                        var Cart = conn.QuerySingleOrDefault(sql, new { mid = userr, pdid = id });


                        //已經有產品了 更新數量
                        if (Cart != null)
                        {
                            num += Cart.cart_quantity;
                            string sqlUpdata = "Update dbo.Shopping_Cart SET cart_quantity=@Num WHERE dbo.Shopping_Cart.m_id=" + userr +
                                "AND dbo.Shopping_Cart.pd_id=" + Cart.pd_id;
                            conn.Execute(sqlUpdata, new { Num = num });
                        }
                        //沒有就新增進去
                        else
                        {
                            Shopping_Cart sc = new Shopping_Cart();
                            sc.m_id = userr;
                            sc.pd_id = id;
                            sc.cart_quantity = num;
                            db.Shopping_Cart.Add(sc);
                            db.SaveChanges();
                        }
                    }
                }

                //加入購物車後 返回商品頁面
                string temp = Session["Nowitem"].ToString();
                int tempid = Convert.ToInt32(Session["nowproduct"].ToString());

                return RedirectToAction(Session["Nowitem"].ToString(), new { id = tempid });
            }

        }

    }
}