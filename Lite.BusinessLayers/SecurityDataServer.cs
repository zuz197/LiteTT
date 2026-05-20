using Dapper;
using Microsoft.Data.SqlClient;
using Lite.Models.Partner;
using Lite.Models.Security;
using System.Data;

namespace Lite.BusinessLayers
{
    public static class SecurityDataServer
    {
        /// <summary>Vai trò cookie cho khách Shop (không lưu trong Customers; chỉ dùng khi SignIn).</summary>
        public const string ShopCustomerRole = "customer";

        /// <summary>Giải thích lỗi SQL khi đăng ký (trước đây bị gom chung thành một dòng gây khó xử lý).</summary>
        private static string DescribeRegisterSqlError(SqlException ex)
        {
            return ex.Number switch
            {
                208 => "Không tìm thấy bảng trên database (kiểm tra tên bảng Customers và connection string).",
                207 => "Bảng Customers không khớp code (thiếu/sai tên cột Email, Password, …).",
                2627 or 2601 => "Email này đã được dùng cho một khách hàng khác.",
                515 => "Không thể lưu: có cột NOT NULL chưa được gán (ví dụ Customers.Password). Cho phép NULL hoặc cập nhật code để gửi giá trị mặc định.",
                547 => "Vi phạm ràng buộc khóa ngoại hoặc check trên database. Chi tiết: " + ex.Message,
                8152 => "Chuỗi quá dài so với cột SQL (thường gặp: Customers.Email chỉ nvarchar(50)). Rút ngắn email hoặc ALTER COLUMN Email.",
                _ => $"Lỗi SQL ({ex.Number}): {ex.Message}",
            };
        }

        private static IDbConnection OpenConnection()
        {
            var connection = new SqlConnection(Configuration.ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Xác thực nhân viên dựa trên Email (username) và mật khẩu đã hash (MD5).
        /// Lấy RoleNames từ bảng Employees để phục vụ phân quyền.
        /// </summary>
        public static async Task<UserAccount?> EmployeeAuthorizeAsync(string userName, string password)
        {
            using var connection = OpenConnection();

            var sql = @"
                SELECT TOP (1)
                    CAST(EmployeeID AS varchar(20)) AS UserId,
                    Email AS UserName,
                    FullName AS DisplayName,
                    Email,
                    Photo,
                    RoleNames
                FROM Employees
                WHERE Email = @userName
                  AND [Password] = @password
                  AND IsWorking = 1;
            ";

            return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, new { userName, password });
        }

        /// <summary>
        /// Đăng nhập khách: chỉ bảng Customers (Email + Password MD5), không khóa.
        /// </summary>
        public static async Task<UserAccount?> CustomerAuthorizeAsync(string userName, string passwordMd5)
        {
            userName = (userName ?? "").Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(passwordMd5))
                return null;

            await using var connection = new SqlConnection(Configuration.ConnectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT TOP (1)
                    CAST(c.CustomerID AS varchar(20)) AS UserId,
                    LTRIM(RTRIM(c.Email)) AS UserName,
                    LTRIM(RTRIM(c.CustomerName)) AS DisplayName,
                    LTRIM(RTRIM(c.Email)) AS Email,
                    CAST(N'' AS nvarchar(500)) AS Photo,
                    @shopRole AS RoleNames
                FROM Customers c
                WHERE LTRIM(RTRIM(c.Email)) = @userName
                  AND c.[Password] = @password
                  AND ISNULL(c.IsLocked, 0) = 0;
            ";

            var acc = await connection.QueryFirstOrDefaultAsync<UserAccount>(sql,
                new { userName, password = passwordMd5, shopRole = ShopCustomerRole });
            if (acc != null && CustomerProfileHelper.IsPendingDisplayName(acc.DisplayName))
                acc.DisplayName = acc.Email ?? acc.UserName;
            return acc;
        }

        /// <summary>
        /// Đăng ký khách chỉ cần email + mật khẩu; hồ sơ chi tiết bổ sung sau trong Customers.
        /// </summary>
        public static async Task<(bool ok, int customerId, string? error)> RegisterCustomerWithAccountAsync(string email, string plainPassword)
        {
            email = (email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return (false, 0, "Vui lòng nhập email.");
            if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 6)
                return (false, 0, "Mật khẩu tối thiểu 6 ký tự.");

            if (!await PartnerDataService.ValidatelCustomerEmailAsync(email, 0))
                return (false, 0, "Email đã được sử dụng.");

            // Province/Address/Phone: dùng NULL thay vì '' — nhiều DB có FK Province -> Provinces(ProvinceName), chuỗi rỗng không tồn tại trong bảng tỉnh → lỗi 547.
            // ContactName: tránh rỗng nếu có CHECK NOT LIKE '' trên database thực tế.
            var pending = CustomerProfileHelper.PendingCustomerDisplayName;
            var customer = new Customer
            {
                CustomerName = pending,
                ContactName = pending,
                Province = null,
                Address = null,
                Phone = null,
                Email = email,
                IsLocked = false,
            };

            var hash = HashHelper.HashMD5(plainPassword);

            await using var connection = new SqlConnection(Configuration.ConnectionString);
            await connection.OpenAsync();
            await using var tran = await connection.BeginTransactionAsync();

            try
            {
                // Ghi Password vào Customers nếu cột tồn tại (schema LiteCommerce); tránh lỗi NOT NULL / đồng bộ dữ liệu cũ.
                const string insertCustomer = @"
                    INSERT INTO Customers (CustomerName, ContactName, Province, Address, Phone, Email, [Password], IsLocked)
                    VALUES (@CustomerName, @ContactName, @Province, @Address, @Phone, @Email, @Password, @IsLocked);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var customerId = await connection.ExecuteScalarAsync<int>(insertCustomer, new
                {
                    customer.CustomerName,
                    customer.ContactName,
                    customer.Province,
                    customer.Address,
                    customer.Phone,
                    customer.Email,
                    Password = hash,
                    customer.IsLocked
                }, tran);
                if (customerId <= 0)
                {
                    await tran.RollbackAsync();
                    return (false, 0, "Không tạo được khách hàng.");
                }

                await tran.CommitAsync();
                return (true, customerId, null);
            }
            catch (SqlException ex)
            {
                await tran.RollbackAsync();
                return (false, 0, DescribeRegisterSqlError(ex));
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                return (false, 0, $"Đăng ký thất bại: {ex.Message}");
            }
        }

        /// <summary>
        /// Đổi mật khẩu tài khoản khách (xác thực mật khẩu cũ).
        /// </summary>
        public static async Task<bool> ChangeCustomerPasswordAsync(string userName, string oldPasswordMd5, string newPasswordMd5)
        {
            userName = (userName ?? "").Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(oldPasswordMd5) || string.IsNullOrEmpty(newPasswordMd5))
                return false;

            await using var connection = new SqlConnection(Configuration.ConnectionString);
            await connection.OpenAsync();
            var n = await connection.ExecuteAsync(@"
                UPDATE Customers
                SET [Password] = @newPassword
                WHERE LTRIM(RTRIM(Email)) = @userName
                  AND [Password] = @oldPassword
                  AND ISNULL(IsLocked, 0) = 0;",
                new
                {
                    userName,
                    oldPassword = oldPasswordMd5,
                    newPassword = newPasswordMd5
                });
            return n > 0;
        }
    }
}

