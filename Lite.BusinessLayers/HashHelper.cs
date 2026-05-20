using System.Security.Cryptography;
using System.Text;

namespace Lite.BusinessLayers
{
    public static class HashHelper
    {
        public static string HashMD5(string input)
        {
            if (input == null) return "";
            using var md5 = MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            var sb = new StringBuilder(hashBytes.Length * 2);
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
