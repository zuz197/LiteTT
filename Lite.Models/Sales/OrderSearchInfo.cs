namespace Lite.Models.Sales
{
    /// <summary>
    /// Thông tin đơn hàng khi hiển thị trong danh sách tìm kiếm (DTO)
    /// </summary>
    public class OrderSearchInfo : Order
    {
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public decimal SumOfPrice { get; set; }

        /// <summary>
        /// Trạng thái yêu cầu hoàn/đổi hàng (NULL nếu không có)
        /// </summary>
        public int? ReturnRequestStatus { get; set; }

        /// <summary>
        /// Loại yêu cầu hoàn/đổi: "Refund" | "Exchange" | NULL
        /// </summary>
        public string? ReturnRequestType { get; set; }

        /// <summary>
        /// Thời gian gửi yêu cầu hoàn/đổi (dùng để sắp xếp)
        /// </summary>
        public DateTime? ReturnRequestTime { get; set; }
    }
}