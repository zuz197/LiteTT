using Dapper;
using Microsoft.Data.SqlClient;
using Lite.DataLayers.Interfaces;
using Lite.Models.Catalog;
using Lite.Models.Common;

namespace Lite.DataLayers.SQLServer
{
    public class ProductRepository : IProductRepository
    {
        private readonly string connectionString;

        public ProductRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(connectionString);
        }

        #region PRODUCT

        public async Task<PagedResult<Product>> ListAsync(ProductSearchInput input)
        {
            using var connection = GetConnection();

            string sql = @"
                        SELECT COUNT(*)
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                        LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
                        WHERE p.ProductName LIKE @search
                          AND (@categoryId = 0 OR p.CategoryID = @categoryId)
                          AND (@supplierId = 0 OR p.SupplierID = @supplierId)
                          AND (@minPrice = 0 OR p.Price >= @minPrice)
                          AND (@maxPrice = 0 OR p.Price <= @maxPrice)
                          AND (@onlySelling = 0 OR p.IsSelling = 1)
                          AND (c.IsHidden IS NULL OR c.IsHidden = 0)
                          AND (s.IsHidden IS NULL OR s.IsHidden = 0);

                        SELECT p.*
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                        LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
                        WHERE p.ProductName LIKE @search
                          AND (@categoryId = 0 OR p.CategoryID = @categoryId)
                          AND (@supplierId = 0 OR p.SupplierID = @supplierId)
                          AND (@minPrice = 0 OR p.Price >= @minPrice)
                          AND (@maxPrice = 0 OR p.Price <= @maxPrice)
                          AND (@onlySelling = 0 OR p.IsSelling = 1)
                          AND (c.IsHidden IS NULL OR c.IsHidden = 0)
                          AND (s.IsHidden IS NULL OR s.IsHidden = 0)
                        ORDER BY p.ProductName
                        OFFSET @offset ROWS FETCH NEXT @pagesize ROWS ONLY";

            var param = new
            {
                search = $"%{input.SearchValue}%",
                offset = (input.Page - 1) * input.PageSize,
                pagesize = input.PageSize,
                categoryId = input.CategoryID,
                supplierId = input.SupplierID,
                minPrice = input.MinPrice,
                maxPrice = input.MaxPrice,
                onlySelling = input.OnlySelling ? 1 : 0
            };

            using var multi = await connection.QueryMultipleAsync(sql, param);

            int count = multi.Read<int>().Single();
            var data = multi.Read<Product>().ToList();

            return new PagedResult<Product>()
            {
                Page = input.Page,
                PageSize = input.PageSize,
                RowCount = count,
                DataItems = data
            };
        }

        public async Task<Product?> GetAsync(int productID)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM Products WHERE ProductID=@productID";

            return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { productID });
        }

        public async Task<int> AddAsync(Product data)
        {
            using var connection = GetConnection();

            string sql = @"
                        INSERT INTO Products(ProductName,ProductDescription,SupplierID,CategoryID,Unit,Price,Photo,IsSelling)
                        VALUES(@ProductName,@ProductDescription,@SupplierID,@CategoryID,@Unit,@Price,@Photo,@IsSelling);
                        SELECT SCOPE_IDENTITY();";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> UpdateAsync(Product data)
        {
            using var connection = GetConnection();

            string sql = @"
                        UPDATE Products
                        SET ProductName=@ProductName,
                            ProductDescription=@ProductDescription,
                            SupplierID=@SupplierID,
                            CategoryID=@CategoryID,
                            Unit=@Unit,
                            Price=@Price,
                            Photo=@Photo,
                            IsSelling=@IsSelling
                        WHERE ProductID=@ProductID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteAsync(int productID)
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Xóa các bản ghi liên quan trước để tránh vi phạm khóa ngoài
                await connection.ExecuteAsync(
                    "DELETE FROM ProductAttributes WHERE ProductID=@productID",
                    new { productID }, transaction);

                await connection.ExecuteAsync(
                    "DELETE FROM ProductPhotos WHERE ProductID=@productID",
                    new { productID }, transaction);

                int rows = await connection.ExecuteAsync(
                    "DELETE FROM Products WHERE ProductID=@productID",
                    new { productID }, transaction);

                transaction.Commit();
                return rows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> IsUsedAsync(int productID)
        {
            using var connection = GetConnection();

            // Chỉ kiểm tra đơn hàng — attributes và photos sẽ được xóa tự động khi xóa sản phẩm
            string sql = "SELECT COUNT(*) FROM OrderDetails WHERE ProductID = @productID;";

            int inOrders = await connection.ExecuteScalarAsync<int>(sql, new { productID });

            return inOrders > 0;
        }

        /// <summary>Toggle IsSelling (dùng chung interface ToggleHiddenAsync)</summary>
        public async Task<bool> ToggleHiddenAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = "UPDATE Products SET IsSelling = CASE WHEN IsSelling = 1 THEN 0 ELSE 1 END WHERE ProductID = @productID";
            return await connection.ExecuteAsync(sql, new { productID }) > 0;
        }

        #endregion


        #region ATTRIBUTE

        public async Task<List<ProductAttribute>> ListAttributesAsync(int productID)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM ProductAttributes WHERE ProductID=@productID";

            var data = await connection.QueryAsync<ProductAttribute>(sql, new { productID });

            return data.ToList();
        }

        public async Task<ProductAttribute?> GetAttributeAsync(long attributeID)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM ProductAttributes WHERE AttributeID=@attributeID";

            return await connection.QueryFirstOrDefaultAsync<ProductAttribute>(sql, new { attributeID });
        }

        public async Task<long> AddAttributeAsync(ProductAttribute data)
        {
            using var connection = GetConnection();

            string sql = @"
                        INSERT INTO ProductAttributes(ProductID,AttributeName,AttributeValue,DisplayOrder)
                        VALUES(@ProductID,@AttributeName,@AttributeValue,@DisplayOrder);
                        SELECT SCOPE_IDENTITY();";

            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> UpdateAttributeAsync(ProductAttribute data)
        {
            using var connection = GetConnection();

            string sql = @"
                        UPDATE ProductAttributes
                        SET AttributeName=@AttributeName,
                            AttributeValue=@AttributeValue,
                            DisplayOrder=@DisplayOrder
                        WHERE AttributeID=@AttributeID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteAttributeAsync(long attributeID)
        {
            using var connection = GetConnection();

            string sql = "DELETE FROM ProductAttributes WHERE AttributeID=@attributeID";

            return await connection.ExecuteAsync(sql, new { attributeID }) > 0;
        }

        #endregion


        #region PHOTO

        public async Task<List<ProductPhoto>> ListPhotosAsync(int productID)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM ProductPhotos WHERE ProductID=@productID";

            var data = await connection.QueryAsync<ProductPhoto>(sql, new { productID });

            return data.ToList();
        }

        public async Task<ProductPhoto?> GetPhotoAsync(long photoID)
        {
            using var connection = GetConnection();

            string sql = "SELECT * FROM ProductPhotos WHERE PhotoID=@photoID";

            return await connection.QueryFirstOrDefaultAsync<ProductPhoto>(sql, new { photoID });
        }

        public async Task<long> AddPhotoAsync(ProductPhoto data)
        {
            data.Description ??= string.Empty;

            using var connection = GetConnection();

            string sql = @"
                        INSERT INTO ProductPhotos(ProductID,Photo,Description,DisplayOrder,IsHidden)
                        VALUES(@ProductID,@Photo,@Description,@DisplayOrder,@IsHidden);
                        SELECT SCOPE_IDENTITY();";

            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> UpdatePhotoAsync(ProductPhoto data)
        {
            data.Description ??= string.Empty;

            using var connection = GetConnection();

            string sql = @"
                        UPDATE ProductPhotos
                        SET Photo=@Photo,
                            Description=@Description,
                            DisplayOrder=@DisplayOrder,
                            IsHidden=@IsHidden
                        WHERE PhotoID=@PhotoID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeletePhotoAsync(long photoID)
        {
            using var connection = GetConnection();

            string sql = "DELETE FROM ProductPhotos WHERE PhotoID=@photoID";

            return await connection.ExecuteAsync(sql, new { photoID }) > 0;
        }

        #endregion
    }
}