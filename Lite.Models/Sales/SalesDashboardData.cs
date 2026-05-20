namespace Lite.Models.Sales
{
    /// <summary>
    /// Dữ liệu tổng hợp cho trang chủ quản trị (dashboard).
    /// </summary>
    public class SalesDashboardData
    {
        /// <summary>Doanh thu hoàn tất trong ngày (đơn Completed, theo ngày FinishedTime).</summary>
        public decimal TodayRevenue { get; set; }

        public int TotalOrderCount { get; set; }

        public int CustomerCount { get; set; }

        public int ProductCount { get; set; }

        public List<DashboardPendingOrderRow> PendingOrders { get; set; } = new();

        public List<DashboardTopProductRow> TopProducts { get; set; } = new();

        /// <summary>Nhãn trục X biểu đồ doanh thu (6 tháng gần nhất).</summary>
        public List<string> MonthlyChartLabels { get; set; } = new();

        /// <summary>Doanh thu hoàn tất từng tháng (theo FinishedTime), đơn vị: triệu đồng.</summary>
        public List<decimal> MonthlyChartValues { get; set; } = new();
    }

    public class DashboardPendingOrderRow
    {
        public int OrderID { get; set; }
        public string CustomerName { get; set; } = "";
        public DateTime OrderTime { get; set; }
        public decimal SumOfPrice { get; set; }
        public OrderStatusEnum Status { get; set; }

        // Thông tin ReturnRequest (nếu có)
        public int? ReturnRequestStatus { get; set; }
        public string? ReturnRequestType { get; set; }
        public DateTime? ReturnRequestTime { get; set; }
    }

    public class DashboardTopProductRow
    {
        public string ProductName { get; set; } = "";
        public long TotalQuantity { get; set; }
    }
}