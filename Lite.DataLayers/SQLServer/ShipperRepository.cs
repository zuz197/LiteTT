using Dapper;
using Microsoft.Data.SqlClient;
using Lite.DataLayers.Interfaces;
using Lite.Models.Common;
using Lite.Models.Partner;

namespace Lite.DataLayers.SQLServer
{
    public class ShipperRepository : IGenericRepository<Shipper>
    {
        private readonly string _connectionString;

        public ShipperRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> AddAsync(Shipper data)
        {
            using var connection = GetConnection();

            string sql = @"
                INSERT INTO Shippers(ShipperName, Phone)
                VALUES(@ShipperName, @Phone);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> UpdateAsync(Shipper data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE Shippers
                SET ShipperName = @ShipperName,
                    Phone = @Phone
                WHERE ShipperID = @ShipperID
            ";

            int rows = await connection.ExecuteAsync(sql, data);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();

            string sql = "DELETE FROM Shippers WHERE ShipperID = @id";

            int rows = await connection.ExecuteAsync(sql, new { id });
            return rows > 0;
        }

        public async Task<bool> ToggleHiddenAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "UPDATE Shippers SET IsHidden = CASE WHEN IsHidden = 1 THEN 0 ELSE 1 END WHERE ShipperID = @id";
            return await connection.ExecuteAsync(sql, new { id }) > 0;
        }

        /// <summary>Chỉ lấy shipper đang hiển thị (dùng khi chọn shipper cho đơn hàng)</summary>
        public async Task<List<Shipper>> ListActiveAsync()
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Shippers WHERE IsHidden = 0 ORDER BY ShipperName";
            var data = await connection.QueryAsync<Shipper>(sql);
            return data.ToList();
        }

        public async Task<Shipper?> GetAsync(int id)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM Shippers WHERE ShipperID = @id";

            return await connection.QueryFirstOrDefaultAsync<Shipper>(sql, new { id });
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT COUNT(*)
                FROM Orders
                WHERE ShipperID = @id
            ";

            int count = await connection.ExecuteScalarAsync<int>(sql, new { id });
            return count > 0;
        }

        public async Task<PagedResult<Shipper>> ListAsync(PaginationSearchInput input)
        {
            using var connection = GetConnection();

            var result = new PagedResult<Shipper>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string countSql = @"
                SELECT COUNT(*)
                FROM Shippers
                WHERE ShipperName LIKE @SearchValue
            ";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new
            {
                SearchValue = $"%{input.SearchValue}%"
            });

            string dataSql = @"
                SELECT *
                FROM Shippers
                WHERE ShipperName LIKE @SearchValue
                ORDER BY ShipperName
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY
            ";

            var data = await connection.QueryAsync<Shipper>(dataSql, new
            {
                SearchValue = $"%{input.SearchValue}%",
                Offset = input.Offset,
                PageSize = input.PageSize
            });

            result.DataItems = data.ToList();

            return result;
        }
    }
}