using Dapper;
using Microsoft.Data.SqlClient;
using Lite.DataLayers.Interfaces;
using Lite.Models.Common;
using Lite.Models.Partner;

namespace Lite.DataLayers.SQLServer
{
    public class SupplierRepository : IGenericRepository<Supplier>
    {
        private readonly string _connectionString;

        public SupplierRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> AddAsync(Supplier data)
        {
            using var connection = GetConnection();

            string sql = @"
                INSERT INTO Suppliers
                (SupplierName, ContactName, Province, Address, Phone, Email)
                VALUES
                (@SupplierName, @ContactName, @Province, @Address, @Phone, @Email);
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";

            int id = await connection.ExecuteScalarAsync<int>(sql, data);
            return id;
        }

        public async Task<bool> UpdateAsync(Supplier data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE Suppliers
                SET SupplierName = @SupplierName,
                    ContactName = @ContactName,
                    Province = @Province,
                    Address = @Address,
                    Phone = @Phone,
                    Email = @Email
                WHERE SupplierID = @SupplierID
            ";

            int rows = await connection.ExecuteAsync(sql, data);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();

            string sql = "DELETE FROM Suppliers WHERE SupplierID = @id";

            int rows = await connection.ExecuteAsync(sql, new { id });
            return rows > 0;
        }

        public async Task<bool> ToggleHiddenAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "UPDATE Suppliers SET IsHidden = CASE WHEN IsHidden = 1 THEN 0 ELSE 1 END WHERE SupplierID = @id";
            return await connection.ExecuteAsync(sql, new { id }) > 0;
        }

        public async Task<Supplier?> GetAsync(int id)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM Suppliers WHERE SupplierID = @id";

            var supplier = await connection.QueryFirstOrDefaultAsync<Supplier>(sql, new { id });
            return supplier;
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT COUNT(*) 
                FROM Products
                WHERE SupplierID = @id
            ";

            int count = await connection.ExecuteScalarAsync<int>(sql, new { id });
            return count > 0;
        }

        public async Task<PagedResult<Supplier>> ListAsync(PaginationSearchInput input)
        {
            using var connection = GetConnection();

            var result = new PagedResult<Supplier>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string countSql = @"
                SELECT COUNT(*)
                FROM Suppliers
                WHERE SupplierName LIKE @SearchValue
                   OR ContactName LIKE @SearchValue
            ";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new
            {
                SearchValue = $"%{input.SearchValue}%"
            });

            string dataSql = @"
                SELECT *
                FROM Suppliers
                WHERE SupplierName LIKE @SearchValue
                   OR ContactName LIKE @SearchValue
                ORDER BY SupplierName
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY
            ";

            var data = await connection.QueryAsync<Supplier>(dataSql, new
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