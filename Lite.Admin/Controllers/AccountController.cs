using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Admin.Models;
using Lite.Models.Security;

namespace Lite.Admin.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new AccountChangePasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(AccountChangePasswordViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.OldPassword))
                ModelState.AddModelError(nameof(model.OldPassword), "Vui lòng nhập mật khẩu cũ");
            if (string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới");
            if (model.NewPassword != model.ConfirmPassword)
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Xác nhận mật khẩu không khớp");

            var userData = User.GetUserData();
            if (userData == null || string.IsNullOrWhiteSpace(userData.UserId) || !int.TryParse(userData.UserId, out int employeeId))
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
                return View(model);

            var oldHash = CryptHelper.HashMD5(model.OldPassword);
            var newHash = CryptHelper.HashMD5(model.NewPassword);
            bool ok = await HRDataService.ChangeEmployeePasswordAsync(employeeId, oldHash, newHash);
            if (!ok)
            {
                ModelState.AddModelError("Error", "Mật khẩu cũ không đúng hoặc tài khoản không tồn tại");
                return View(model);
            }

            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync();
            TempData["Message"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string username, string password, string captcha)
        {
            ViewBag.Username = username;

            int failCount = HttpContext.Session.GetInt32("LoginFail") ?? 0;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("Error", "Nhập đủ tên và mật khẩu");
                return View();
            }

            if (failCount >= 3)
            {
                var sessionCaptcha = HttpContext.Session.GetString("Captcha");
                if (string.IsNullOrWhiteSpace(captcha) || captcha != sessionCaptcha)
                {
                    failCount++;
                    HttpContext.Session.SetInt32("LoginFail", failCount);
                    ModelState.AddModelError("Error", "Sai mã captcha");
                    return View();
                }
            }

            var hashed = CryptHelper.HashMD5(password);
            var userAccount = await SecurityDataServer.EmployeeAuthorizeAsync(username, hashed);

            if (userAccount == null)
            {
                failCount++;
                HttpContext.Session.SetInt32("LoginFail", failCount);
                ModelState.AddModelError("Error", "Đăng nhập thất bại");
                return View();
            }

            HttpContext.Session.Remove("LoginFail");
            HttpContext.Session.Remove("Captcha");

            string otp = OtpEmailService.GenerateOtp();
            HttpContext.Session.SetString("Admin_OTP", otp);
            HttpContext.Session.SetString("Admin_OTP_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));

            HttpContext.Session.SetString("Admin_Pending_UserId", userAccount.UserId ?? "");
            HttpContext.Session.SetString("Admin_Pending_UserName", userAccount.UserName ?? "");
            HttpContext.Session.SetString("Admin_Pending_DisplayName", userAccount.DisplayName ?? "");
            HttpContext.Session.SetString("Admin_Pending_Email", userAccount.Email ?? "");
            HttpContext.Session.SetString("Admin_Pending_Photo", userAccount.Photo ?? "");
            HttpContext.Session.SetString("Admin_Pending_Roles", userAccount.RoleNames ?? "");

            var gmailAddr = _config["Gmail:Address"] ?? "";
            var gmailPass = _config["Gmail:AppPassword"] ?? "";
            var appName   = _config["Gmail:AppName"] ?? "LiteCommerce Admin";
            var (sent, sendError) = await OtpEmailService.SendOtpAsync(
                gmailAddr, gmailPass, userAccount.Email ?? "", userAccount.DisplayName ?? username, otp, appName);

            if (!sent)
            {
                ModelState.AddModelError("Error", $"Khong the gui OTP: {sendError}");
                return View();
            }

            return RedirectToAction("VerifyOtp");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyOtp()
        {
            // Nếu không có OTP pending thì về login
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Admin_OTP")))
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp(string otp)
        {
            var savedOtp = HttpContext.Session.GetString("Admin_OTP");
            var expiryStr = HttpContext.Session.GetString("Admin_OTP_Expiry");

            if (string.IsNullOrEmpty(savedOtp) || string.IsNullOrEmpty(expiryStr))
            {
                ViewBag.Error = "Phiên xác thực đã hết hạn. Vui lòng đăng nhập lại.";
                return View();
            }

            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
            {
                HttpContext.Session.Remove("Admin_OTP");
                HttpContext.Session.Remove("Admin_OTP_Expiry");
                ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng đăng nhập lại.";
                return View();
            }

            if (otp?.Trim() != savedOtp)
            {
                ViewBag.Error = "Mã OTP không đúng. Vui lòng thử lại.";
                return View();
            }

            HttpContext.Session.Remove("Admin_OTP");
            HttpContext.Session.Remove("Admin_OTP_Expiry");

            var userData = new WebUserData()
            {
                UserId = HttpContext.Session.GetString("Admin_Pending_UserId"),
                UserName = HttpContext.Session.GetString("Admin_Pending_UserName"),
                DisplayName = HttpContext.Session.GetString("Admin_Pending_DisplayName"),
                Email = HttpContext.Session.GetString("Admin_Pending_Email"),
                Photo = HttpContext.Session.GetString("Admin_Pending_Photo"),
                Roles = (HttpContext.Session.GetString("Admin_Pending_Roles") ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            };

            HttpContext.Session.Remove("Admin_Pending_UserId");
            HttpContext.Session.Remove("Admin_Pending_UserName");
            HttpContext.Session.Remove("Admin_Pending_DisplayName");
            HttpContext.Session.Remove("Admin_Pending_Email");
            HttpContext.Session.Remove("Admin_Pending_Photo");
            HttpContext.Session.Remove("Admin_Pending_Roles");

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                userData.CreatePrincipal());

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResendOtp()
        {
            var email = HttpContext.Session.GetString("Admin_Pending_Email");
            var displayName = HttpContext.Session.GetString("Admin_Pending_DisplayName");

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            string otp = OtpEmailService.GenerateOtp();
            HttpContext.Session.SetString("Admin_OTP", otp);
            HttpContext.Session.SetString("Admin_OTP_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));

            var gmailAddr = _config["Gmail:Address"] ?? "";
            var gmailPass = _config["Gmail:AppPassword"] ?? "";
            var appName   = _config["Gmail:AppName"] ?? "LiteCommerce Admin";
            await OtpEmailService.SendOtpAsync(gmailAddr, gmailPass, email, displayName ?? "", otp, appName);

            TempData["Message"] = "Đã gửi lại mã OTP. Vui lòng kiểm tra email.";
            return RedirectToAction("VerifyOtp");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GenerateCaptcha()
        {
            var code = new Random().Next(1000, 9999).ToString();
            HttpContext.Session.SetString("Captcha", code);
            return Json(new { captcha = code });
        }

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}