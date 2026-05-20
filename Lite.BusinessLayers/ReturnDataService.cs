using Dapper;
using Lite.Models.Sales;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Lite.BusinessLayers
{
    public static class ReturnDataService
    {
        private static IDbConnection OpenConnection()
        {
            var conn = new SqlConnection(Configuration.ConnectionString);
            conn.Open();
            return conn;
        }

        public static async Task<ReturnRequest?> GetByOrderIDAsync(int orderId)
        {
            using var conn = OpenConnection();

            var rr = await conn.QueryFirstOrDefaultAsync<ReturnRequest>(@"
SELECT
    rr.ReturnRequestID, rr.OrderID, rr.Type, rr.Reason,
    rr.Status, rr.RequestTime, rr.ApprovedTime,
    rr.ShippedTime, rr.CompletedTime, rr.RejectReason,
    rr.ShipperID, rr.ApprovedByEmployeeID,
    c.CustomerName, c.Phone AS CustomerPhone,
    s.ShipperName, s.Phone AS ShipperPhone,
    e.FullName AS EmployeeName
FROM ReturnRequests rr
LEFT JOIN Orders    o ON rr.OrderID   = o.OrderID
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
LEFT JOIN Shippers  s ON rr.ShipperID = s.ShipperID
LEFT JOIN Employees e ON rr.ApprovedByEmployeeID = e.EmployeeID
WHERE rr.OrderID = @orderId", new { orderId });

            if (rr == null) return null;

            rr.Photos = (await conn.QueryAsync<string>(@"
SELECT PhotoPath FROM ReturnPhotos
WHERE ReturnRequestID = @id",
                new { id = rr.ReturnRequestID })).ToList();

            return rr;
        }

        public static async Task<List<ReturnRequest>> ListPendingAsync()
        {
            using var conn = OpenConnection();
            return (await conn.QueryAsync<ReturnRequest>(@"
SELECT
    rr.ReturnRequestID, rr.OrderID, rr.Type, rr.Reason,
    rr.Status, rr.RequestTime,
    c.CustomerName, c.Phone AS CustomerPhone
FROM ReturnRequests rr
LEFT JOIN Orders    o ON rr.OrderID   = o.OrderID
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
WHERE rr.Status = 1
ORDER BY rr.RequestTime DESC")).ToList();
        }

        public static async Task<bool> ApproveAsync(int returnRequestId, int employeeId, int shipperId)
        {
            using var conn = OpenConnection();
            return await conn.ExecuteAsync(@"
UPDATE ReturnRequests
SET Status = 2,
    ApprovedTime = GETUTCDATE(),
    ApprovedByEmployeeID = @employeeId,
    ShipperID = @shipperId
WHERE ReturnRequestID = @id",
                new { id = returnRequestId, employeeId, shipperId }) > 0;
        }

        public static async Task<bool> RejectAsync(int returnRequestId, int employeeId, string rejectReason)
        {
            using var conn = OpenConnection();
            return await conn.ExecuteAsync(@"
UPDATE ReturnRequests
SET Status = -1,
    ApprovedTime = GETUTCDATE(),
    ApprovedByEmployeeID = @employeeId,
    RejectReason = @rejectReason
WHERE ReturnRequestID = @id",
                new { id = returnRequestId, employeeId, rejectReason }) > 0;
        }

        public static async Task<bool> SetShippedAsync(int returnRequestId)
        {
            using var conn = OpenConnection();
            return await conn.ExecuteAsync(@"
UPDATE ReturnRequests
SET Status = 3, ShippedTime = GETUTCDATE()
WHERE ReturnRequestID = @id",
                new { id = returnRequestId }) > 0;
        }

        public static async Task<bool> CompleteAsync(int returnRequestId)
        {
            using var conn = OpenConnection();
            return await conn.ExecuteAsync(@"
UPDATE ReturnRequests
SET Status = 4, CompletedTime = GETUTCDATE()
WHERE ReturnRequestID = @id",
                new { id = returnRequestId }) > 0;
        }
    }
}