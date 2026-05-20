using System.Text;

namespace Lite.Shop.AppCodes
{
    public static class CryptHelper
    {
        // Hàm thực thi thực sự (không xung tên với System.Security.Cryptography.MD5)
        public static string MD5Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Gọi kiểu bằng tên đầy đủ để tránh xung tên với phương thức MD5()
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // Wrapper giữ tên cũ để tương thích với các nơi gọi CryptHelper.MD5(...)
        public static string MD5(string input) => MD5Hash(input);
    }
}