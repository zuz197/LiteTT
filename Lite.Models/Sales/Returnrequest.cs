namespace Lite.Models.Sales
{
    public class ReturnRequest
    {
        public int ReturnRequestID { get; set; }
        public int OrderID { get; set; }
        public string Type { get; set; } = ""; // "Refund" | "Exchange"
        public string? Reason { get; set; }
        public int Status { get; set; }
        // 1=Chờ duyệt, 2=Đã duyệt, 3=Đang xử lý, 4=Hoàn tất, -1=Từ chối

        public DateTime RequestTime { get; set; }
        public DateTime? ApprovedTime { get; set; }
        public DateTime? ShippedTime { get; set; }
        public DateTime? CompletedTime { get; set; }

        public int? ApprovedByEmployeeID { get; set; }
        public int? ShipperID { get; set; }
        public string? RejectReason { get; set; }

        // JOIN fields
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ShipperName { get; set; }
        public string? ShipperPhone { get; set; }
        public string? EmployeeName { get; set; }

        public List<string> Photos { get; set; } = new();
    }
}