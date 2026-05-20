using Dapper;
using Lite.DataLayers.Interfaces;
using Lite.Models.Common;
using Lite.Models.Sales;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Lite.DataLayers.SQLServer
{
    public class OrderRepository : IOrderRepository
    {
        private readonly string connectionString;

        public OrderRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        public async Task<PagedResult<OrderSearchInfo>> ListAsync(OrderSearchInput input)
        {
            using (var connection = OpenConnection())
            {
                int rowCount;
                List<OrderSearchInfo> data;

                var sql = @"SELECT COUNT(*)
                            FROM Orders o
                            LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                            WHERE COALESCE(c.CustomerName, '') LIKE @searchValue
                              AND (@status = 0 OR o.Status = @status)
                              AND (@dateFrom IS NULL OR o.OrderTime >= @dateFrom)
                              AND (@dateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @dateTo))
                              AND (@customerId = 0 OR o.CustomerID = @customerId);

                            SELECT o.OrderID, o.CustomerID, o.OrderTime,
                                   o.DeliveryProvince, o.DeliveryAddress, o.EmployeeID, o.AcceptTime,
                                   o.ShipperID, o.ShippedTime, o.FinishedTime, o.Status,
                                   c.CustomerName, c.Phone AS CustomerPhone,
                                   e.FullName AS EmployeeName,
                                   ISNULL(od.SumOfPrice, 0) AS SumOfPrice,
                                   rr.Status AS ReturnRequestStatus,
                                   rr.Type AS ReturnRequestType,
                                   rr.RequestTime AS ReturnRequestTime
                            FROM Orders o
                            LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                            LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                            LEFT JOIN ReturnRequests rr ON rr.OrderID = o.OrderID
                            OUTER APPLY
                            (
                                SELECT SUM(d.Quantity * d.SalePrice) AS SumOfPrice
                                FROM OrderDetails d
                                WHERE d.OrderID = o.OrderID
                            ) od
                            WHERE COALESCE(c.CustomerName, '') LIKE @searchValue
                              AND (@status = 0 OR o.Status = @status)
                              AND (@dateFrom IS NULL OR o.OrderTime >= @dateFrom)
                              AND (@dateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @dateTo))
                              AND (@customerId = 0 OR o.CustomerID = @customerId)
                            ORDER BY
                                CASE WHEN rr.Status = 1 THEN 0 ELSE 1 END,
                                ISNULL(rr.RequestTime, o.OrderTime) DESC
                            OFFSET (@page - 1) * @pageSize ROWS
                            FETCH NEXT @pageSize ROWS ONLY";

                using (var multi = await connection.QueryMultipleAsync(sql, new
                {
                    page = input.Page,
                    pageSize = input.PageSize,
                    searchValue = "%" + (input.SearchValue ?? "") + "%",
                    status = (int)input.Status,
                    dateFrom = input.DateFrom,
                    dateTo = input.DateTo,
                    customerId = input.CustomerID
                }))
                {
                    rowCount = multi.Read<int>().Single();
                    data = multi.Read<OrderSearchInfo>().ToList();
                }

                return new PagedResult<OrderSearchInfo>()
                {
                    Page = input.Page,
                    PageSize = input.PageSize,
                    RowCount = rowCount,
                    DataItems = data
                };
            }
        }

        public async Task<OrderViewInfo?> GetAsync(int orderID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT o.OrderID, o.CustomerID, o.OrderTime,
                                   o.DeliveryProvince, o.DeliveryAddress, o.EmployeeID, o.AcceptTime,
                                   o.ShipperID, o.ShippedTime, o.FinishedTime, o.Status,
                                   ISNULL(c.CustomerName, '') AS CustomerName,
                                   ISNULL(c.ContactName, '') AS CustomerContactName,
                                   ISNULL(c.Email, '') AS CustomerEmail,
                                   ISNULL(c.Phone, '') AS CustomerPhone,
                                   ISNULL(c.Address, '') AS CustomerAddress,
                                   ISNULL(e.FullName, '') AS EmployeeName,
                                   ISNULL(s.ShipperName, '') AS ShipperName,
                                   ISNULL(s.Phone, '') AS ShipperPhone
                            FROM Orders o
                            LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                            LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                            LEFT JOIN Shippers s ON o.ShipperID = s.ShipperID
                            WHERE o.OrderID = @orderID";
                return await connection.QueryFirstOrDefaultAsync<OrderViewInfo>(sql, new { orderID });
            }
        }

        public async Task<int> AddAsync(Order data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"INSERT INTO Orders
                            (CustomerID, OrderTime, DeliveryProvince, DeliveryAddress, Status)
                            VALUES
                            (@CustomerID, @OrderTime, @DeliveryProvince, @DeliveryAddress, @Status);
                            SELECT CAST(SCOPE_IDENTITY() as int);";

                return await connection.ExecuteScalarAsync<int>(sql, data);
            }
        }

        public async Task<int> AddOrderWithDetailsAsync(Order order, IReadOnlyList<OrderDetail> details)
        {
            if (details == null || details.Count == 0)
                return 0;

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string insertOrderSql = @"INSERT INTO Orders
                            (CustomerID, OrderTime, DeliveryProvince, DeliveryAddress, Status)
                            VALUES
                            (@CustomerID, @OrderTime, @DeliveryProvince, @DeliveryAddress, @Status);
                            SELECT CAST(SCOPE_IDENTITY() as int);";

                var orderId = await connection.ExecuteScalarAsync<int>(insertOrderSql, order, transaction);
                if (orderId <= 0)
                {
                    transaction.Rollback();
                    return 0;
                }

                const string insertDetailSql = @"INSERT INTO OrderDetails
                            (OrderID, ProductID, Quantity, SalePrice)
                            VALUES
                            (@OrderID, @ProductID, @Quantity, @SalePrice)";

                foreach (var d in details)
                {
                    var row = new OrderDetail
                    {
                        OrderID = orderId,
                        ProductID = d.ProductID,
                        Quantity = d.Quantity,
                        SalePrice = d.SalePrice
                    };
                    var n = await connection.ExecuteAsync(insertDetailSql, row, transaction);
                    if (n <= 0)
                    {
                        transaction.Rollback();
                        return 0;
                    }
                }

                transaction.Commit();
                return orderId;
            }
            catch
            {
                transaction.Rollback();
                return 0;
            }
        }

        public async Task<bool> UpdateAsync(Order data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"UPDATE Orders
                            SET CustomerID = @CustomerID,
                                DeliveryProvince = @DeliveryProvince,
                                DeliveryAddress = @DeliveryAddress,
                                EmployeeID = @EmployeeID,
                                AcceptTime = @AcceptTime,
                                ShipperID = @ShipperID,
                                ShippedTime = @ShippedTime,
                                FinishedTime = @FinishedTime,
                                Status = @Status
                            WHERE OrderID = @OrderID";

                int result = await connection.ExecuteAsync(sql, data);
                return result > 0;
            }
        }

        public async Task<bool> DeleteAsync(int orderID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"DELETE FROM Orders WHERE OrderID = @orderID";
                int result = await connection.ExecuteAsync(sql, new { orderID });
                return result > 0;
            }
        }

        public async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT d.OrderID, d.ProductID, p.ProductName,
                                   d.Quantity, d.SalePrice
                            FROM OrderDetails d
                            JOIN Products p ON d.ProductID = p.ProductID
                            WHERE d.OrderID = @orderID";

                return (await connection.QueryAsync<OrderDetailViewInfo>(sql, new { orderID })).ToList();
            }
        }

        public async Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT d.OrderID, d.ProductID, p.ProductName,
                                   d.Quantity, d.SalePrice
                            FROM OrderDetails d
                            JOIN Products p ON d.ProductID = p.ProductID
                            WHERE d.OrderID = @orderID AND d.ProductID = @productID";

                return await connection.QueryFirstOrDefaultAsync<OrderDetailViewInfo>(sql,
                    new { orderID, productID });
            }
        }

        public async Task<bool> AddDetailAsync(OrderDetail data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"INSERT INTO OrderDetails
                            (OrderID, ProductID, Quantity, SalePrice)
                            VALUES
                            (@OrderID, @ProductID, @Quantity, @SalePrice)";

                int result = await connection.ExecuteAsync(sql, data);
                return result > 0;
            }
        }

        public async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"UPDATE OrderDetails
                            SET Quantity = @Quantity,
                                SalePrice = @SalePrice
                            WHERE OrderID = @OrderID AND ProductID = @ProductID";

                int result = await connection.ExecuteAsync(sql, data);
                return result > 0;
            }
        }

        public async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"DELETE FROM OrderDetails
                            WHERE OrderID = @orderID AND ProductID = @productID";

                int result = await connection.ExecuteAsync(sql, new { orderID, productID });
                return result > 0;
            }
        }

        public async Task<SalesDashboardData> GetDashboardDataAsync()
        {
            using var connection = OpenConnection();
            var today = DateTime.Today;
            var completed = (int)OrderStatusEnum.Completed;

            var todayRevenue = await connection.ExecuteScalarAsync<decimal?>(@"
                SELECT ISNULL(SUM(CAST(d.Quantity AS DECIMAL(18,4)) * d.SalePrice), 0)
                FROM OrderDetails d
                INNER JOIN Orders o ON o.OrderID = d.OrderID
                WHERE o.Status = @completed
                  AND o.FinishedTime IS NOT NULL
                  AND CAST(o.FinishedTime AS DATE) = CAST(@today AS DATE)", new { today, completed }) ?? 0m;

            var totalOrders = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders");
            var customerCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Customers");
            var productCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Products");

            const string pendingSql = @"
                SELECT TOP 10
                       o.OrderID,
                       ISNULL(c.CustomerName, '') AS CustomerName,
                       o.OrderTime,
                       ISNULL(od.SumOfPrice, 0) AS SumOfPrice,
                       o.Status,
                       rr.Status AS ReturnRequestStatus,
                       rr.Type AS ReturnRequestType,
                       rr.RequestTime AS ReturnRequestTime
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                LEFT JOIN ReturnRequests rr ON rr.OrderID = o.OrderID
                OUTER APPLY (
                    SELECT SUM(CAST(d2.Quantity AS DECIMAL(18,4)) * d2.SalePrice) AS SumOfPrice
                    FROM OrderDetails d2
                    WHERE d2.OrderID = o.OrderID
                ) od
                WHERE o.Status IN (@newStatus, @acceptedStatus, @shippingStatus)
                   OR (o.Status = @completedStatus AND rr.Status IN (1, 2, 3))
                ORDER BY
                    CASE WHEN rr.Status IN (1, 2, 3) THEN 0 ELSE 1 END,
                    ISNULL(rr.RequestTime, o.OrderTime) DESC";

            var pending = (await connection.QueryAsync<DashboardPendingOrderRow>(pendingSql, new
            {
                newStatus = (int)OrderStatusEnum.New,
                acceptedStatus = (int)OrderStatusEnum.Accepted,
                shippingStatus = (int)OrderStatusEnum.Shipping,
                completedStatus = (int)OrderStatusEnum.Completed
            })).ToList();

            const string topSql = @"
                SELECT TOP 5 p.ProductName AS ProductName, CAST(SUM(d.Quantity) AS BIGINT) AS TotalQuantity
                FROM OrderDetails d
                INNER JOIN Products p ON p.ProductID = d.ProductID
                INNER JOIN Orders o ON o.OrderID = d.OrderID AND o.Status = @completed
                GROUP BY p.ProductName
                ORDER BY SUM(d.Quantity) DESC";

            var topProducts = (await connection.QueryAsync<DashboardTopProductRow>(topSql, new { completed })).ToList();

            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
            const string monthlySql = @"
                SELECT YEAR(o.FinishedTime) AS Year,
                       MONTH(o.FinishedTime) AS Month,
                       ISNULL(SUM(CAST(d.Quantity AS DECIMAL(18,4)) * d.SalePrice), 0) AS Revenue
                FROM Orders o
                INNER JOIN OrderDetails d ON d.OrderID = o.OrderID
                WHERE o.Status = @completed
                  AND o.FinishedTime IS NOT NULL
                  AND o.FinishedTime >= @monthStart
                GROUP BY YEAR(o.FinishedTime), MONTH(o.FinishedTime)
                ORDER BY YEAR(o.FinishedTime), MONTH(o.FinishedTime)";

            var monthlyRows = (await connection.QueryAsync<(int Year, int Month, decimal Revenue)>(
                monthlySql, new { monthStart, completed })).ToList();
            var monthlyDict = monthlyRows.ToDictionary(t => (t.Year, t.Month), t => t.Revenue);

            var chartLabels = new List<string>();
            var chartValues = new List<decimal>();
            for (var d = monthStart; d <= new DateTime(today.Year, today.Month, 1); d = d.AddMonths(1))
            {
                chartLabels.Add($"Tháng {d.Month}/{d.Year}");
                var rev = monthlyDict.GetValueOrDefault((d.Year, d.Month));
                chartValues.Add(Math.Round(rev / 1_000_000m, 2));
            }

            return new SalesDashboardData
            {
                TodayRevenue = todayRevenue,
                TotalOrderCount = totalOrders,
                CustomerCount = customerCount,
                ProductCount = productCount,
                PendingOrders = pending,
                TopProducts = topProducts,
                MonthlyChartLabels = chartLabels,
                MonthlyChartValues = chartValues
            };
        }
    }
}