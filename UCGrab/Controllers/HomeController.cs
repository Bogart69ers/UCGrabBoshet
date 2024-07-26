﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using UCGrab.Utils;
using UCGrab.Models;
using UCGrab.Database;
using System.IO;
using UCGrab.Repository;

namespace UCGrab.Controllers
{
    [Authorize(Roles = "Customer")]
    public class HomeController : BaseController
    {
        // GET: Home
        [Authorize]
        public ActionResult Index()
        {
            IsUserLoggedSession();

            return View();
        }

        [AllowAnonymous]
        public ActionResult Login(String ReturnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }

            ViewBag.Error = String.Empty;
            ViewBag.ReturnUrl = ReturnUrl;

            return View();
        }
        [AllowAnonymous]
        [HttpPost]
        public ActionResult Login(String username, String password, String ReturnUrl)
        {

            if (_userManager.SignIn(username, password, ref ErrorMessage) == ErrorCode.Success)
            {
                var user = _userManager.GetUserByUsername(username);

                if (user.status != (int)Status.Active)
                {
                    TempData["username"] = username;
                    return RedirectToAction("Verify");
                }

                FormsAuthentication.SetAuthCookie(username, false);
                //
                if (!String.IsNullOrEmpty(ReturnUrl))
                    return Redirect(ReturnUrl);

                switch (user.User_Role.rolename)
                {
                    case Constant.Role_Customer:
                        return RedirectToAction("Index");
                    case Constant.Role_Provider:
                        return RedirectToAction("Index", "Shop");
                    default:
                        return RedirectToAction("Index", "Admin");
                }
            }
            ViewBag.Error = ErrorMessage;

            return View();
        }

        [AllowAnonymous]
        public ActionResult Verify()
        {
            if (String.IsNullOrEmpty(TempData["username"] as String))
                return RedirectToAction("Login");

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult Verify(string verify_code, string username)
        {
            if (String.IsNullOrEmpty(username))
                return RedirectToAction("Login");

            TempData["username"] = username;

            var user = _userManager.GetUserByUsername(username);
            if (user == null)
            {
                TempData["error"] = "User not found.";
            }
            if (!user.verify_code.Equals(verify_code))
            {
                TempData["error"] = "Incorrect Code";
                return View();
            }

            user.status = (Int32)Status.Active;
            _userManager.UpdateUser(user, ref ErrorMessage);

            SendActivationNotificationEmail(user.email);
            
            return RedirectToAction("Login");
        }

        private void SendActivationNotificationEmail(string userEmail)
        {
            string emailBody = "Your account has been activated successfully.";
            string errorMessage = "";

            var mailManager = new MailManager();
            bool emailSent = mailManager.SendEmail(userEmail, "Account Activation Notification", emailBody, ref errorMessage);

            if (!emailSent)
            {
                // Handle email sending failure
            }
        }
        [AllowAnonymous]
        [HttpPost]
        public ActionResult ResendCode(User_Accounts ua, string username)
        {
            if (String.IsNullOrEmpty(username))
                return RedirectToAction("Login");

            var user = _userManager.GetUserByUsername(username);

            string verificationCode = ua.verify_code;
            string emailBody = $"Your verification code is: {verificationCode}";
            string errorMessage = "";

            var mailManager = new MailManager();
            bool emailSent = mailManager.SendEmail(ua.email, "Verification Code", emailBody, ref errorMessage);

            if (!emailSent)
            {
                ModelState.AddModelError(String.Empty, errorMessage);
                return View(ua);
            }

            TempData["username"] = username;
            return RedirectToAction("Verify");
        }

        [AllowAnonymous]
        public ActionResult SignUp()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index");

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult SignUp(User_Accounts ua, string ConfirmPass)
        {
            if (!ua.password.Equals(ConfirmPass))
            {
                ModelState.AddModelError(String.Empty, "Password does not match");
                return View(ua);
            }

            if (_userManager.SignUp(ua, ref ErrorMessage) != ErrorCode.Success)
            {
                ModelState.AddModelError(String.Empty, ErrorMessage);
                return View(ua);
            }

            var user = _userManager.GetUserByEmail(ua.email);
            string verificationCode = ua.verify_code;

            string emailBody = $"Your verification code is: {verificationCode}";
            string errorMessage = "";

            var mailManager = new MailManager();
            bool emailSent = mailManager.SendEmail(ua.email, "Verification Code", emailBody, ref errorMessage);

            if (!emailSent)
            {
                ModelState.AddModelError(String.Empty, errorMessage);
                return View(ua);
            }

            TempData["username"] = ua.username;
            return RedirectToAction("Verify");
        }
        [Authorize]
        public ActionResult MyProfile()
        {
            IsUserLoggedSession();
            var user = User.Identity.Name;
            var userinfo = _userManager.GetUserInfoByUsername(user);

            if(userinfo == null)
            {
                TempData["ErrorMessage"] = "Failed retreiving user information.";
                return RedirectToAction("MyProfile");

            }
            return View(userinfo);
        }
        [HttpPost]
        public ActionResult MyProfile(User_Information userInf, HttpPostedFileBase profilePicture)
        {
            IsUserLoggedSession();

            if (ModelState.IsValid)
            {
                var user = _userManager.GetUserByUserId(userInf.user_id);
                if(user == null)
                {
                    TempData["ErrorMessage"] = "User Not Found,";
                    return View(userInf);
                }
            }
            if (profilePicture != null && profilePicture.ContentLength > 0)
            {
                var uploadsFolderPath = Server.MapPath("~/UploadedFiles/");
                if (!Directory.Exists(uploadsFolderPath))
                    Directory.CreateDirectory(uploadsFolderPath);

                var profileFileName = Path.GetFileName(profilePicture.FileName);
                var profileSavePath = Path.Combine(uploadsFolderPath, profileFileName);
                profilePicture.SaveAs(profileSavePath);

                var existingImage = _imageManager.ListImgAttachByImageId(userInf.id).FirstOrDefault();
                if (existingImage != null)
                {
                    existingImage.image_file = profileFileName;
                    if (_imageManager.UpdateImg(existingImage, ref ErrorMessage) == ErrorCode.Error)
                    {
                        ModelState.AddModelError(String.Empty, ErrorMessage);
                        return View(userInf);
                    }
                }
                
            }
            return View(userInf);
        }
        [Authorize]
        public ActionResult EditProfile()
        {
            IsUserLoggedSession();

            var user = User.Identity.Name;
            var usrinfo = _userManager.GetUserInfoByUsername(user);

            if (usrinfo == null)
            {
                TempData["ErrorMessage"] = "Failed retrieving user information.";
                return RedirectToAction("MyProfile");
            }

            return View(usrinfo);
        }

        [HttpPost]
        public ActionResult EditProfile(User_Information userInf, HttpPostedFileBase profilePicture)
        {
            IsUserLoggedSession();

            if (ModelState.IsValid)
            {
                var user = _userManager.GetUserByUserId(userInf.user_id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Error updating profile: User not found.";
                    return View(userInf);
                }

                // Handle profile picture upload
                if (profilePicture != null && profilePicture.ContentLength > 0)
                {
                    var uploadsFolderPath = Server.MapPath("~/UploadedFiles/");
                    if (!Directory.Exists(uploadsFolderPath))
                        Directory.CreateDirectory(uploadsFolderPath);

                    var profileFileName = Path.GetFileName(profilePicture.FileName);
                    var profileSavePath = Path.Combine(uploadsFolderPath, profileFileName);
                    profilePicture.SaveAs(profileSavePath);

                    // Log the save path
                    System.Diagnostics.Debug.WriteLine("Profile picture saved at: " + profileSavePath);

                    var existingImage = _imageManager.ListImgAttachByImageId(userInf.id).FirstOrDefault();
                    if (existingImage != null)
                    {
                        existingImage.image_file = profileFileName;
                        if(_imageManager.UpdateImg(existingImage, ref ErrorMessage) == ErrorCode.Error)
                        {
                            ModelState.AddModelError(String.Empty, ErrorMessage);
                            return View(userInf);
                        }
                    }
                    else
                    {
                        Image img = new Image
                        {
                            image_file = profileFileName,
                            image_id = userInf.id
                        };

                       if (_imageManager.CreateImg(img, ref ErrorMessage) == ErrorCode.Error)
                        {
                            ModelState.AddModelError(String.Empty, ErrorMessage);
                            return View(userInf);
                        }
                    }
                }

           if (_userManager.UpdateUserInformation(userInf, ref ErrorMessage) == ErrorCode.Error)
                {
                    ModelState.AddModelError(String.Empty, ErrorMessage);
                    return View(userInf);

                }
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("MyProfile");
            }

            return View(userInf);
        }



        [AllowAnonymous]
        public ActionResult ForgotPass()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            Session.Clear();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult Shop(String storeId)
        {
            var store = _storeManager.GetStoreByUserId(storeId);

            if (store != null)
            {
                var products = _productManager.ListActiveProduct(store.user_id) ?? new List<Product>();
                return View(products);
            }

            // Optionally, handle the case where the store is not found
            return HttpNotFound("Store not found");
        }

        [AllowAnonymous]
        public ActionResult ShopList()
        {
            var stores = _storeManager.ListStore();

            return View(stores);
        }

        [AllowAnonymous]
        public ActionResult Detail(int? id)
        {
            if (id == null || id == 0)
                return RedirectToAction("PageNotFound");

            var product = _productManager.GetProductById(id);

            return View(product);
            
        }
        [AllowAnonymous]
        [HttpPost]
        public JsonResult AddCart(int prodId, int qty)
        {
            var res = new Response();

            try
            {
                if (_orderManager.AddCart(UserId, prodId, qty, ref ErrorMessage) == ErrorCode.Error)
                {
                    throw new Exception(ErrorMessage);
                }

                res.code = (int)ErrorCode.Success;
                res.message = "Item Added!";
            }
            catch (Exception ex)
            {
                res.code = (int)ErrorCode.Error;
                res.message = "An error occurred while adding the item to the cart: " + ex.Message;

                // Log the inner exception if it exists
                if (ex.InnerException != null)
                {
                    res.message += " Inner exception: " + ex.InnerException.Message;
                }

                LogError(ex);
                // Optionally log the exception to a file or monitoring system
                // Example: System.IO.File.WriteAllText("path/to/logfile.txt", ex.ToString());
            }

            return Json(res, JsonRequestBehavior.AllowGet);
        }
        private void LogError(Exception ex)
        {
            string logFilePath = Server.MapPath("~/Logs/errorlog.txt");
            string errorMessage = DateTime.Now.ToString() + " - " + ex.ToString();

            if (!System.IO.File.Exists(logFilePath))
            {
                System.IO.File.Create(logFilePath).Dispose();
            }

            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine(errorMessage);
            }
        }
        [AllowAnonymous]
        public ActionResult Cart()
        {
            return View(_orderManager.GetOrderByUserId(UserId));
        }
        [HttpPost]
        [AllowAnonymous]
        public ActionResult Cart(int qty, int orderDtId, String action)
        {
            switch (action)
            {
                case "&plus;":
                    qty++;
                    break;
                case "&minus;":
                    qty--;
                    break;
                case "X":
                    _orderManager.DeleteOrderDetail(orderDtId, ref ErrorMessage);
                    return View(_orderManager.GetOrderByUserId(UserId));
            }

            if (qty <= 0)
            {
                _orderManager.DeleteOrderDetail(orderDtId, ref ErrorMessage);
                return View(_orderManager.GetOrderByUserId(UserId));
            }

            var orderDt = _orderManager.GetOrderDetailById(orderDtId);
            orderDt.quatity = qty;

            _orderManager.UpdateOrderDetail(orderDt.id, orderDt, ref ErrorMessage);

            return View(_orderManager.GetOrderByUserId(UserId));
        }

        public JsonResult GetCartCount()
        {
            var res = new { count = _orderManager.GetCartCountByUserId(UserId) };

            return Json(res, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult FavoritedProduct()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult CheckOut()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Contact()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Ewallet()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult MyOrders()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult PageNotFound()
        {
            return View();
        }
    }
}