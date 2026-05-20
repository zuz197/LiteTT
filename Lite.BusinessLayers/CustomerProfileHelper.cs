using Lite.Models.Partner;

namespace Lite.BusinessLayers
{
    /// <summary>
    /// Hồ sơ khách đăng ký tối giản: họ tên giữ chỗ cho đến khi người dùng cập nhật.
    /// </summary>
    public static class CustomerProfileHelper
    {
        public const string PendingCustomerDisplayName = "(Cập nhật sau đăng ký)";

        public static bool IsPendingDisplayName(string? customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return true;
            return string.Equals(customerName.Trim(), PendingCustomerDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Giá trị đưa lên form chỉnh sửa: chuỗi giữ chỗ trong DB hiển thị thành ô trống.</summary>
        public static string ForEditableDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return string.Equals(value.Trim(), PendingCustomerDisplayName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
        }

        /// <summary>
        /// Đủ thông tin để đặt hàng: họ tên thật, điện thoại, tỉnh/thành, địa chỉ.
        /// </summary>
        public static bool IsCompleteForCheckout(Customer? c)
        {
            if (c == null)
                return false;
            if (IsPendingDisplayName(c.CustomerName))
                return false;
            if (string.IsNullOrWhiteSpace(c.Phone))
                return false;
            if (string.IsNullOrWhiteSpace(c.Province))
                return false;
            if (string.IsNullOrWhiteSpace(c.Address))
                return false;
            return true;
        }
    }
}
