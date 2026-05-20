using Lite.Models.Partner;

namespace Lite.Admin.Models
{
    public class OrderShippingFormModel
    {
        public int OrderID { get; set; }
        public List<Shipper> Shippers { get; set; } = new();
    }
}
