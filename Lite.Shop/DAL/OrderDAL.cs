using Dapper;
using Microsoft.Extensions.Configuration;
using Lite.Shop.Models;

namespace Lite.Shop.DAL
{
    public class OrderDAL : BaseDAL
    {
        public OrderDAL(IConfiguration config) : base(config) { }

        // Lấy danh sách đơn hàng
        public List<Order> List(int customerId)
        {
            using var conn = OpenConnection();

            string sql = @"
SELECT o.OrderID, o.CustomerID, o.OrderTime, o.DeliveryAddress, o.Status,
       rr.Status AS ReturnStatus,
       rr.Type AS ReturnType
FROM Orders o
LEFT JOIN ReturnRequests rr ON rr.OrderID = o.OrderID
WHERE o.CustomerID = @cid
ORDER BY o.OrderTime DESC";

            return conn.Query<Order>(sql, new { cid = customerId }).ToList();
        }

        // Chi tiết đơn hàng (cũ — giữ lại)
        public List<OrderDetail> GetDetails(int orderId)
        {
            using var conn = OpenConnection();

            string sql = @"
SELECT 
    od.OrderID,
    od.ProductID,
    od.Quantity,
    od.SalePrice,
    p.ProductName
FROM OrderDetails od
JOIN Products p ON od.ProductID = p.ProductID
WHERE od.OrderID = @oid";

            return conn.Query<OrderDetail>(sql, new { oid = orderId }).ToList();
        }

        // Chi tiết đơn hàng đầy đủ (mới)
        public OrderViewDetail? GetOrderViewDetail(int orderId)
        {
            using var conn = OpenConnection();

            // Lấy thông tin đơn hàng + khách hàng + shipper
            string orderSql = @"
SELECT
    o.OrderID,
    o.Status,
    o.OrderTime,
    o.AcceptTime,
    o.ShippedTime,
    o.FinishedTime,
    o.CancelledTime,
    o.DeliveryAddress,
    o.DeliveryProvince,
    c.CustomerName,
    c.Phone,
    s.ShipperName,
    s.Phone AS ShipperPhone
FROM Orders o
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
LEFT JOIN Shippers  s ON o.ShipperID  = s.ShipperID
WHERE o.OrderID = @oid";

            var order = conn.QueryFirstOrDefault<OrderViewDetail>(orderSql, new { oid = orderId });
            if (order == null) return null;

            // Lấy danh sách sản phẩm kèm ảnh
            string itemSql = @"
SELECT
    od.ProductID,
    p.ProductName,
    p.Photo,
    od.Quantity,
    od.SalePrice
FROM OrderDetails od
JOIN Products p ON od.ProductID = p.ProductID
WHERE od.OrderID = @oid";

            order.Items = conn.Query<OrderDetailItem>(itemSql, new { oid = orderId }).ToList();

            return order;
        }

        // Lấy ReturnRequest theo OrderID (dùng bởi ReturnRequestDAL — để tránh circular dep)
        public bool HasReturnRequest(int orderId)
        {
            using var conn = OpenConnection();
            return conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM ReturnRequests WHERE OrderID = @orderId",
                new { orderId }) > 0;
        }

        // XÓA ĐƠN HÀNG
        public void Delete(int orderId)
        {
            using var conn = OpenConnection();
            using var tran = conn.BeginTransaction();

            try
            {
                conn.Execute("DELETE FROM OrderDetails WHERE OrderID = @id",
                    new { id = orderId }, tran);
                conn.Execute("DELETE FROM Orders WHERE OrderID = @id",
                    new { id = orderId }, tran);
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // HUỶ ĐƠN
        public bool CancelOrder(int orderId)
        {
            using var conn = OpenConnection();

            string sql = @"
UPDATE Orders
SET Status = -1,
    FinishedTime = GETUTCDATE(),
    CancelledTime = GETUTCDATE()
WHERE OrderID = @id";

            return conn.Execute(sql, new { id = orderId }) > 0;
        }
    }
}