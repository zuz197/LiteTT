using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Lite.BusinessLayers
{
    public static class OtpEmailService
    {
        public static async Task<(bool Success, string Error)> SendOtpAsync(
            string gmailAddress, string appPassword,
            string toEmail, string toName,
            string otp, string appName = "LiteCommerce")
        {
            if (string.IsNullOrWhiteSpace(gmailAddress) || string.IsNullOrWhiteSpace(appPassword))
                return (false, "Chưa cấu hình Gmail SMTP.");

            if (string.IsNullOrWhiteSpace(toEmail))
                return (false, "Email người nhận trong.");

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(appName, gmailAddress));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = $"[{appName}] Mã xác thực đăng nhập";

                message.Body = new TextPart("html")
                {
                    Text = $@"
<div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;
            border:1px solid #e5e7eb;border-radius:8px;'>
    <h2 style='color:#1d4ed8;margin-bottom:8px;'>Xác thực đăng nhập</h2>
    <p>xin chào <strong>{toName}</strong>,</p>
    <p>Mã OTP của bạn là:</p>
    <div style='font-size:36px;font-weight:bold;letter-spacing:8px;color:#1d4ed8;
                text-align:center;padding:16px;background:#eff6ff;
                border-radius:8px;margin:16px 0;'>
        {otp}
    </div>
    <p style='color:#6b7280;font-size:14px;'>
        Mã có hiệu lực trong <strong>5 phút</strong>.
        Không chia sẽ mã này cho bất kỳ ai.
    </p>
    <hr style='border:none;border-top:1px solid #e5e7eb;margin:16px 0;'/>
    <p style='color:#9ca3af;font-size:12px;'>
        Nếu bạn không yêu cầu, vui lòng bỏ qua email này.
    </p>
</div>"
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(gmailAddress, appPassword);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }
    }
}