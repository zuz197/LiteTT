namespace Lite.Shop.Models
{
    public class ReturnRequest
    {
        public int ReturnRequestID { get; set; }
        public int OrderID { get; set; }
        public string Type { get; set; } = ""; // "Refund" | "Exchange"
        public string? Reason { get; set; }
        public int Status { get; set; } = 1;
        public DateTime RequestTime { get; set; }
        public DateTime? ApprovedTime { get; set; }
        public DateTime? ShippedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public string? RejectReason { get; set; }
        public string? ShipperName { get; set; }
        public string? ShipperPhone { get; set; }
        public List<string> Photos { get; set; } = new();
    }
}