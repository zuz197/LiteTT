using Dapper;
using Microsoft.Data.SqlClient;
using Lite.DataLayers.Interfaces;
using Lite.Models.Security;
using System.Data;

namespace Lite.DataLayers.SQLServer
{
    public class UserAccountRepository : IUserAccountRepository
    {
        private readonly string connectionString;

        public UserAccountRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        public async Task<UserAccount?> AuthorizeAsync(string userName, string password)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"
                    SELECT TOP (1)
                        UserId,
                        UserName,
                        DisplayName,
                        Email,
                        Photo,
                        RoleNames
                    FROM UserAccounts
                    WHERE UserName = @userName
                      AND [Password] = @password;
                ";

                return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, new { userName, password });
            }
        }

        public async Task<bool> ChangePasswordAsync(string userName, string password)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"
                    UPDATE UserAccounts
                    SET [Password] = @password
                    WHERE UserName = @userName;
                ";

                int rows = await connection.ExecuteAsync(sql, new { userName, password });
                return rows > 0;
            }
        }
    }
}

