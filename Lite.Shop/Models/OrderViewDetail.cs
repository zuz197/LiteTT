namespace Lite.Shop.Models
{
    /// <summary>
    /// Model tổng hợp cho trang chi tiết đơn hàng phía Shop
    /// </summary>
    public class OrderViewDetail
    {
        // Thông tin đơn hàng
        public int OrderID { get; set; }
        public int Status { get; set; }
        public DateTime? OrderTime { get; set; }
        public DateTime? AcceptTime { get; set; }
        public DateTime? ShippedTime { get; set; }
        public DateTime? FinishedTime { get; set; }
        public DateTime? CancelledTime { get; set; }

        // Địa chỉ giao hàng
        public string? DeliveryAddress { get; set; }
        public string? DeliveryProvince { get; set; }

        // Thông tin khách hàng đặt đơn
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }

        // Thông tin shipper (hiện sau khi đơn được giao)
        public string? ShipperName { get; set; }
        public string? ShipperPhone { get; set; }

        // Danh sách sản phẩm
        public List<OrderDetailItem> Items { get; set; } = new();

        // Yêu cầu hoàn/đổi hàng (nếu có)
        public ReturnRequest? ReturnRequest { get; set; }
    }

    public class OrderDetailItem
    {
        public int ProductID { get; set; }
        public string? ProductName { get; set; }
        public string? Photo { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
    }
}