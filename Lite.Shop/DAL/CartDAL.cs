using Dapper;
using Microsoft.Extensions.Configuration;
using Lite.Shop.Models;

namespace Lite.Shop.DAL
{
    public class CartDAL : BaseDAL
    {
        public CartDAL(IConfiguration config) : base(config) { }

        public List<CartItem> Get(int customerId)
        {
            using var conn = OpenConnection();

            string sql = @"
SELECT 
    c.CartItemID,
    c.ProductID,
    c.Quantity,
    p.ProductName,
    p.Price
FROM CartItems c
JOIN Products p ON c.ProductID = p.ProductID
WHERE c.CustomerID = @cid
";

            return conn.Query<CartItem>(sql, new { cid = customerId }).ToList();
        }

        public void Add(int customerId, int productId, int quantity)
        {
            using var conn = OpenConnection();

            var count = conn.ExecuteScalar<int>(@"
SELECT COUNT(*) FROM CartItems 
WHERE CustomerID = @cid AND ProductID = @pid",
            new { cid = customerId, pid = productId });

            if (count > 0)
            {
                conn.Execute(@"
UPDATE CartItems 
SET Quantity = Quantity + @qty
WHERE CustomerID = @cid AND ProductID = @pid",
                new { cid = customerId, pid = productId, qty = quantity });
            }
            else
            {
                conn.Execute(@"
INSERT INTO CartItems(CustomerID, ProductID, Quantity)
VALUES(@cid, @pid, @qty)",
                new { cid = customerId, pid = productId, qty = quantity });
            }
        }

        public void Remove(int customerId, int productId)
        {
            using var conn = OpenConnection();

            conn.Execute(@"
DELETE FROM CartItems 
WHERE CustomerID = @cid AND ProductID = @pid",
            new { cid = customerId, pid = productId });
        }

        public void Update(int customerId, int productId, int quantity)
        {
            using var conn = OpenConnection();

            if (quantity <= 0)
            {
                Remove(customerId, productId);
                return;
            }

            conn.Execute(@"
UPDATE CartItems 
SET Quantity = @qty
WHERE CustomerID = @cid AND ProductID = @pid",
            new { cid = customerId, pid = productId, qty = quantity });
        }

        //  CHECKOUT
        public int Checkout(int customerId)
        {
            using var conn = OpenConnection();
            using var tran = conn.BeginTransaction();

            try
            {
                var customer = conn.QueryFirstOrDefault<dynamic>(@"
SELECT Address, Province
FROM Customers 
WHERE CustomerID = @cid",
                new { cid = customerId }, tran);

                if (customer == null || string.IsNullOrWhiteSpace(customer.Address))
                    throw new Exception("Chưa có địa chỉ");

                int orderId = conn.ExecuteScalar<int>(@"
INSERT INTO Orders
(
    CustomerID,
    OrderTime,
    DeliveryAddress,
    DeliveryProvince,
    Status
)
VALUES
(
    @cid,
    GETDATE(),
    @addr,
    @province,
    1
);
SELECT CAST(SCOPE_IDENTITY() as int);",
                new
                {
                    cid = customerId,
                    addr = customer.Address,
                    province = customer.Province
                }, tran);

                var cartItems = conn.Query<CartItem>(@"
SELECT c.ProductID, c.Quantity, p.Price
FROM CartItems c
JOIN Products p ON c.ProductID = p.ProductID
WHERE c.CustomerID = @cid",
                new { cid = customerId }, tran).ToList();

                foreach (var item in cartItems)
                {
                    conn.Execute(@"
INSERT INTO OrderDetails(OrderID, ProductID, Quantity, SalePrice)
VALUES(@oid, @pid, @qty, @price)",
                    new
                    {
                        oid = orderId,
                        pid = item.ProductID,
                        qty = item.Quantity,
                        price = item.Price
                    }, tran);
                }

                conn.Execute("DELETE FROM CartItems WHERE CustomerID = @cid",
                    new { cid = customerId }, tran);

                tran.Commit();
                return orderId;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // NEW: XOÁ TOÀN BỘ GIỎ HÀNG 
        public void Clear(int customerId)
        {
            using var conn = OpenConnection();

            conn.Execute(@"
DELETE FROM CartItems 
WHERE CustomerID = @cid",
            new { cid = customerId });
        }
    }
}