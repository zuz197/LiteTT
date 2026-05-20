namespace Lite.Shop.Models
{
    public class OrderDetail
    {
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; } // mapped to SalePrice column
        // optional for view
        public string? ProductName { get; set; }
    }
}