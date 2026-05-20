using System.ComponentModel.DataAnnotations;

namespace Lite.Models.Partner
{
    /// <summary>
    /// Khách hàng
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Mã khách hàng
        /// </summary>
        public int CustomerID { get; set; }
        /// <summary>
        /// Tên khách hàng
        /// </summary>
        [Display(Name = "Họ và tên")]
        public string CustomerName { get; set; } = string.Empty;
        /// <summary>
        /// Tên giao dịch
        /// </summary>
        [Display(Name = "Tên giao dịch")]
        public string ContactName { get; set; } = string.Empty;
        /// <summary>
        /// Tỉnh/thành
        /// </summary>
        [Display(Name = "Tỉnh / thành phố")]
        public string? Province { get; set; }
        /// <summary>
        /// Địa chỉ
        /// </summary>
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
        /// <summary>
        /// Điện thoại
        /// </summary>
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }
        /// <summary>
        /// Email
        /// </summary>
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// Khách hàng hiện có bị khóa hay không?
        /// </summary>
        public bool IsLocked { get; set; }
    }
}
