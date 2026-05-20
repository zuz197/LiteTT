using Microsoft.AspNetCore.Mvc;
using Lite.Shop.DAL;
using Lite.Shop.Models;
using Lite.Shop.AppCodes;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Lite.Shop.Controllers
{
    public class AccountController : Controller
    {
        private readonly CustomerDAL db;
        private readonly ProductDAL productDAL;
        private readonly IConfiguration _config;

        public AccountController(CustomerDAL dal, ProductDAL pDal, IConfiguration config)
        {
            db = dal;
            productDAL = pDal;
            _config = config;
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(Customer model, string PasswordConfirm)
        {
            if (!ModelState.IsValid) return View(model);

            var exist = db.GetByEmail(model.Email ?? "");
            if (exist != null)
            {
                ViewBag.Error = "Email đã tồn tại";
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.Password) || model.Password != PasswordConfirm)
            {
                ViewBag.Error = "Mật khẩu và xác nhận không khớp";
                return View(model);
            }

            model.Password = CryptHelper.MD5(model.Password);
            model.IsLocked = false;

            int id = db.Add(model);
            if (id > 0)
            {
                HttpContext.Session.SetString("CustomerID", id.ToString());
                HttpContext.Session.SetString("CustomerName", model.CustomerName ?? "");

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.CustomerName ?? ""),
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Role, "Shop")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return RedirectToAction("Index", "Product");
            }

            ViewBag.Error = "Đăng ký thất bại";
            return View(model);
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string captcha)
        {
            int failCount = HttpContext.Session.GetInt32("LoginFail_Shop") ?? 0;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Nhập email và mật khẩu";
                return View();
            }

            if (failCount >= 3)
            {
                var sessionCaptcha = HttpContext.Session.GetString("Captcha_Shop");

                if (string.IsNullOrWhiteSpace(captcha) || captcha != sessionCaptcha)
                {
                    failCount++;
                    HttpContext.Session.SetInt32("LoginFail_Shop", failCount);
                    ViewBag.Error = "Sai mã captcha";
                    return View();
                }
            }

            var user = db.GetByEmail(email);
            string hash = CryptHelper.MD5(password);

            if (user != null && user.Password != null && user.Password.Trim() == hash.Trim())
            {
                if (user.IsLocked == true)
                {
                    ViewBag.Error = "Tài khoản bị khoá";
                    return View();
                }

                HttpContext.Session.Remove("LoginFail_Shop");
                HttpContext.Session.Remove("Captcha_Shop");

                string otp = Lite.BusinessLayers.OtpEmailService.GenerateOtp();
                HttpContext.Session.SetString("Shop_OTP", otp);
                HttpContext.Session.SetString("Shop_OTP_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));
                HttpContext.Session.SetString("Shop_Pending_CustomerID", user.CustomerID.ToString());
                HttpContext.Session.SetString("Shop_Pending_CustomerName", user.CustomerName ?? "");
                HttpContext.Session.SetString("Shop_Pending_Email", user.Email ?? "");

                var gmailAddr = _config["Gmail:Address"] ?? "";
                var gmailPass = _config["Gmail:AppPassword"] ?? "";
                var appName   = _config["Gmail:AppName"] ?? "NPT Shop";
                var (sent, sendError) = await Lite.BusinessLayers.OtpEmailService.SendOtpAsync(
                    gmailAddr, gmailPass, user.Email ?? "", user.CustomerName ?? "", otp, appName);

                if (!sent)
                {
                    ViewBag.Error = $"Khong the gui OTP: {sendError}";
                    return View();
                }

                return RedirectToAction("VerifyOtp");
            }

            failCount++;
            HttpContext.Session.SetInt32("LoginFail_Shop", failCount);

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
            return View();
        }

        [HttpGet]
        public IActionResult GenerateCaptcha()
        {
            var code = new Random().Next(1000, 9999).ToString();
            HttpContext.Session.SetString("Captcha_Shop", code);
            return Json(new { captcha = code });
        }


        [HttpGet]
        public IActionResult VerifyOtp()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Shop_OTP")))
                return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string otp)
        {
            var savedOtp = HttpContext.Session.GetString("Shop_OTP");
            var expiryStr = HttpContext.Session.GetString("Shop_OTP_Expiry");

            if (string.IsNullOrEmpty(savedOtp) || string.IsNullOrEmpty(expiryStr))
            {
                ViewBag.Error = "Phien xac thuc da het han. Vui long dang nhap lai.";
                return View();
            }

            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
            {
                HttpContext.Session.Remove("Shop_OTP");
                HttpContext.Session.Remove("Shop_OTP_Expiry");
                ViewBag.Error = "Ma OTP da het han. Vui long dang nhap lai.";
                return View();
            }

            if (otp?.Trim() != savedOtp)
            {
                ViewBag.Error = "Ma OTP khong dung. Vui long thu lai.";
                return View();
            }

            HttpContext.Session.Remove("Shop_OTP");
            HttpContext.Session.Remove("Shop_OTP_Expiry");

            var customerId = HttpContext.Session.GetString("Shop_Pending_CustomerID") ?? "";
            var customerName = HttpContext.Session.GetString("Shop_Pending_CustomerName") ?? "";

            HttpContext.Session.SetString("CustomerID", customerId);
            HttpContext.Session.SetString("CustomerName", customerName);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, customerName),
                new Claim(ClaimTypes.NameIdentifier, customerId),
                new Claim(ClaimTypes.Role, "Shop")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            HttpContext.Session.Remove("Shop_Pending_CustomerID");
            HttpContext.Session.Remove("Shop_Pending_CustomerName");
            HttpContext.Session.Remove("Shop_Pending_Email");

            return RedirectToAction("Index", "Product");
        }

        [HttpPost]
        public async Task<IActionResult> ResendOtp()
        {
            var email = HttpContext.Session.GetString("Shop_Pending_Email");
            var name = HttpContext.Session.GetString("Shop_Pending_CustomerName");

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            string otp = Lite.BusinessLayers.OtpEmailService.GenerateOtp();
            HttpContext.Session.SetString("Shop_OTP", otp);
            HttpContext.Session.SetString("Shop_OTP_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));

            var gmailAddr = _config["Gmail:Address"] ?? "";
            var gmailPass = _config["Gmail:AppPassword"] ?? "";
            var appName   = _config["Gmail:AppName"] ?? "NPT Shop";
            await Lite.BusinessLayers.OtpEmailService.SendOtpAsync(gmailAddr, gmailPass, email, name ?? "", otp, appName);

            TempData["Message"] = "Da gui lai ma OTP. Vui long kiem tra email.";
            return RedirectToAction("VerifyOtp");
        }


        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Vui lòng nhập email.";
                return View();
            }

            var user = db.GetByEmail(email.Trim());
            if (user == null)
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống.";
                return View();
            }

            string otp = Lite.BusinessLayers.OtpEmailService.GenerateOtp();
            HttpContext.Session.SetString("FP_OTP", otp);
            HttpContext.Session.SetString("FP_OTP_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));
            HttpContext.Session.SetString("FP_Email", email.Trim());

            var gmailAddr = _config["Gmail:Address"] ?? "";
            var gmailPass = _config["Gmail:AppPassword"] ?? "";
            var appName   = _config["Gmail:AppName"] ?? "NPT Shop";
            var (sent, sendError) = await Lite.BusinessLayers.OtpEmailService.SendOtpAsync(
                gmailAddr, gmailPass, email.Trim(), user.CustomerName ?? "", otp, appName);

            if (!sent)
            {
                ViewBag.Error = $"Không thể gửi OTP: {sendError}";
                return View();
            }

            return RedirectToAction("ForgotPasswordVerify");
        }

        [HttpGet]
        public IActionResult ForgotPasswordVerify()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("FP_OTP")))
                return RedirectToAction("ForgotPassword");
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPasswordVerify(string otp)
        {
            var savedOtp  = HttpContext.Session.GetString("FP_OTP");
            var expiryStr = HttpContext.Session.GetString("FP_OTP_Expiry");

            if (string.IsNullOrEmpty(savedOtp))
            {
                ViewBag.Error = "Phiên xác thực đã hết hạn. Vui lòng thử lại.";
                return View();
            }

            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
            {
                HttpContext.Session.Remove("FP_OTP");
                ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng thử lại.";
                return View();
            }

            if (otp?.Trim() != savedOtp)
            {
                ViewBag.Error = "Mã OTP không đúng.";
                return View();
            }

            HttpContext.Session.Remove("FP_OTP");
            HttpContext.Session.Remove("FP_OTP_Expiry");
            HttpContext.Session.SetString("FP_Verified", "true");

            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            if (HttpContext.Session.GetString("FP_Verified") != "true")
                return RedirectToAction("ForgotPassword");
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (HttpContext.Session.GetString("FP_Verified") != "true")
                return RedirectToAction("ForgotPassword");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu không khớp hoặc để trống.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View();
            }

            var email = HttpContext.Session.GetString("FP_Email");
            var user  = db.GetByEmail(email ?? "");
            if (user == null)
                return RedirectToAction("ForgotPassword");

            string newHash = CryptHelper.MD5(newPassword);
            db.ChangePassword(user.CustomerID, newHash);

            HttpContext.Session.Remove("FP_Verified");
            HttpContext.Session.Remove("FP_Email");

            TempData["Message"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Profile()
        {
            int? id = GetCurrentUserId();
            if (id == null) return RedirectToAction("Login");

            var user = db.Get(id.Value);
            if (user == null) return RedirectToAction("Login");

            ViewBag.ProvinceList = await SelectListHelper.Provinces();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(Customer model)
        {
            int? id = GetCurrentUserId();
            if (id == null) return RedirectToAction("Login");

            var user = db.Get(id.Value);
            if (user == null) return RedirectToAction("Login");

            user.CustomerName = model.CustomerName;
            user.ContactName = model.ContactName;
            user.Address = model.Address;
            user.Province = model.Province;
            user.Phone = model.Phone;

            db.Update(user);

            ViewBag.Message = "Cập nhật thành công";
            ViewBag.ProvinceList = await SelectListHelper.Provinces();

            return View(user);
        }

        public IActionResult ChangePassword()
        {
            int? id = GetCurrentUserId();
            if (id == null) return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            int? id = GetCurrentUserId();
            if (id == null) return RedirectToAction("Login");

            var user = db.Get(id.Value);
            if (user == null) return RedirectToAction("Login");

            string currentHash = CryptHelper.MD5(currentPassword);

            if (user.Password == null || user.Password.Trim() != currentHash.Trim())
            {
                ViewBag.Error = "Mật khẩu hiện tại không đúng";
                return View();
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu mới và xác nhận không khớp";
                return View();
            }

            string newHash = CryptHelper.MD5(newPassword);
            var success = db.ChangePassword(id.Value, newHash);

            if (success)
            {
                // Gửi email thông báo đổi mật khẩu thành công
                try
                {
                    var gmailAddr = _config["Gmail:Address"] ?? "";
                    var gmailPass = _config["Gmail:AppPassword"] ?? "";
                    var appName   = _config["Gmail:AppName"] ?? "LiteShop";
                    if (!string.IsNullOrWhiteSpace(gmailAddr) && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        await SendPasswordChangedEmailAsync(gmailAddr, gmailPass, user.Email, user.CustomerName ?? "", appName);
                    }
                }
                catch
                {
                    // Không chặn flow nếu gửi email thất bại
                }

                ViewBag.Message = "Đổi mật khẩu thành công";
            }
            else
                ViewBag.Error = "Đổi mật khẩu thất bại";

            return View();
        }

        private static async Task SendPasswordChangedEmailAsync(string gmailAddress, string appPassword, string toEmail, string toName, string appName)
        {
            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(appName, gmailAddress));
            message.To.Add(new MimeKit.MailboxAddress(toName, toEmail));
            message.Subject = $"[{appName}] Mật khẩu của bạn đã được thay đổi";

            message.Body = new MimeKit.TextPart("html")
            {
                Text = $@"
<div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;
            border:1px solid #e5e7eb;border-radius:8px;'>
    <h2 style='color:#16a34a;margin-bottom:8px;'>Mật khẩu đã được thay đổi</h2>
    <p>Xin chào <strong>{toName}</strong>,</p>
    <p>Mật khẩu tài khoản của bạn trên <strong>{appName}</strong> vừa được thay đổi thành công.</p>
    <p style='color:#6b7280;font-size:14px;'>
        Nếu bạn không thực hiện thao tác này, vui lòng liên hệ với chúng tôi ngay lập tức.
    </p>
    <hr style='border:none;border-top:1px solid #e5e7eb;margin:16px 0;'/>
    <p style='color:#9ca3af;font-size:12px;'>
        Email này được gửi tự động, vui lòng không trả lời.
    </p>
</div>"
            };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(gmailAddress, appPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && !string.IsNullOrEmpty(claim.Value))
            {
                if (int.TryParse(claim.Value, out int id))
                    return id;
            }

            var idStr = HttpContext.Session.GetString("CustomerID");
            if (!string.IsNullOrEmpty(idStr))
            {
                if (int.TryParse(idStr, out int id))
                    return id;
            }

            return null;
        }
    }
}