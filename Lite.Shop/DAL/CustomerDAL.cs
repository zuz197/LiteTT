using Dapper;
using Microsoft.Extensions.Configuration;
using Lite.Shop.Models;

namespace Lite.Shop.DAL
{
    public class CustomerDAL : BaseDAL
    {
        public CustomerDAL(IConfiguration config) : base(config) { }

        public int Add(Customer data)
        {
            using var conn = OpenConnection();
            string sql = @"
INSERT INTO Customers(CustomerName, ContactName, Province, Address, Phone, Email, Password, IsLocked)
VALUES(@CustomerName, @ContactName, @Province, @Address, @Phone, @Email, @Password, @IsLocked);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return conn.ExecuteScalar<int>(sql, new
            {
                data.CustomerName,
                data.ContactName,
                data.Province,
                data.Address,
                data.Phone,
                data.Email,
                data.Password,
                data.IsLocked
            });
        }

        public Customer? GetByEmail(string email)
        {
            using var conn = OpenConnection();
            return conn.QueryFirstOrDefault<Customer>(
                "SELECT * FROM Customers WHERE Email=@email", new { email });
        }

        public Customer? Get(int id)
        {
            using var conn = OpenConnection();
            return conn.QueryFirstOrDefault<Customer>(
                "SELECT * FROM Customers WHERE CustomerID=@id", new { id });
        }

        public void Update(Customer data)
        {
            using var conn = OpenConnection();
            string sql = @"
UPDATE Customers
SET CustomerName=@CustomerName,
    ContactName=@ContactName,
    Province=@Province,
    Address=@Address,
    Phone=@Phone,
    Email=@Email,
    IsLocked=@IsLocked
WHERE CustomerID=@CustomerID";

            conn.Execute(sql, new
            {
                data.CustomerName,
                data.ContactName,
                data.Province,
                data.Address,
                data.Phone,
                data.Email,
                data.IsLocked,
                data.CustomerID
            });
        }

        public bool ChangePassword(int customerId, string newHashedPassword)
        {
            using var conn = OpenConnection();

            string sql = @"
UPDATE Customers 
SET Password = @Password 
WHERE CustomerID = @CustomerID";

            int rows = conn.Execute(sql, new
            {
                Password = newHashedPassword,
                CustomerID = customerId
            });

            return rows > 0; //biết  có update không
        }
    }
}