using Dapper;
using Microsoft.Extensions.Configuration;
using Lite.Shop.Models;

namespace Lite.Shop.DAL
{
    public class ReturnRequestDAL : BaseDAL
    {
        public ReturnRequestDAL(IConfiguration config) : base(config) { }

        /// <summary>
        /// Lấy yêu cầu hoàn/đổi theo OrderID (nếu có)
        /// </summary>
        public ReturnRequest? GetByOrderID(int orderId)
        {
            using var conn = OpenConnection();

            var rr = conn.QueryFirstOrDefault<ReturnRequest>(@"
SELECT
    rr.ReturnRequestID, rr.OrderID, rr.Type, rr.Reason,
    rr.Status, rr.RequestTime, rr.ApprovedTime,
    rr.ShippedTime, rr.CompletedTime, rr.RejectReason,
    s.ShipperName, s.Phone AS ShipperPhone
FROM ReturnRequests rr
LEFT JOIN Shippers s ON rr.ShipperID = s.ShipperID
WHERE rr.OrderID = @orderId", new { orderId });

            if (rr == null) return null;

            rr.Photos = conn.Query<string>(@"
SELECT PhotoPath FROM ReturnPhotos
WHERE ReturnRequestID = @id", new { id = rr.ReturnRequestID }).ToList();

            return rr;
        }

        /// <summary>
        /// Tạo yêu cầu hoàn/đổi hàng mới
        /// </summary>
        public int Create(int orderId, string type, string reason, List<string> photoPaths)
        {
            using var conn = OpenConnection();
            using var tran = conn.BeginTransaction();

            try
            {
                int id = conn.ExecuteScalar<int>(@"
INSERT INTO ReturnRequests (OrderID, Type, Reason, Status, RequestTime)
VALUES (@orderId, @type, @reason, 1, GETUTCDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { orderId, type, reason }, tran);

                foreach (var path in photoPaths)
                {
                    conn.Execute(@"
INSERT INTO ReturnPhotos (ReturnRequestID, PhotoPath)
VALUES (@id, @path)", new { id, path }, tran);
                }

                tran.Commit();
                return id;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }
    }
}