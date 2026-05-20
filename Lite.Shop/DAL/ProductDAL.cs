using Dapper;
using Microsoft.Extensions.Configuration;
using Lite.Shop.Models;
using System.Data;

namespace Lite.Shop.DAL
{
    public class ProductDAL : BaseDAL
    {
        public ProductDAL(IConfiguration config) : base(config) { }

        public List<Product> List(string search, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
            using var conn = OpenConnection();

            string sql = @"
SELECT p.* FROM Products p
LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
WHERE p.IsSelling = 1
AND (c.IsHidden IS NULL OR c.IsHidden = 0)
AND (s.IsHidden IS NULL OR s.IsHidden = 0)
AND (@search = '' OR p.ProductName LIKE '%' + @search + '%')
AND (@categoryId = 0 OR p.CategoryID = @categoryId)
AND (@minPrice IS NULL OR p.Price >= @minPrice)
AND (@maxPrice IS NULL OR p.Price <= @maxPrice)
";

            return conn.Query<Product>(sql, new
            {
                search = search ?? "",
                categoryId = categoryId ?? 0,
                minPrice,
                maxPrice
            }).ToList();
        }

        // existing Get (keeps returning Product)
        public Product? Get(int id)
        {
            using var conn = OpenConnection();
            string sql = "SELECT * FROM Products WHERE ProductID = @id";
            return conn.QueryFirstOrDefault<Product>(sql, new { id });
        }

        // NEW: GetDetails returns product info + supplier name + category name
        public ProductDetailsViewModel? GetDetails(int id)
        {
            using var conn = OpenConnection();

            string sql = @"
SELECT 
    p.ProductID,
    p.ProductName,
    p.ProductDescription,
    p.SupplierID,
    p.CategoryID,
    p.Unit,
    p.Price,
    p.Photo,
    p.IsSelling,
    s.SupplierName,
    c.CategoryName
FROM Products p
LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
WHERE p.ProductID = @id
";

            return conn.QueryFirstOrDefault<ProductDetailsViewModel>(sql, new { id });
        }
    }
}