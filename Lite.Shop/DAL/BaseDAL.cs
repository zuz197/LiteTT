using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Lite.Shop.DAL
{
    public class BaseDAL
    {
        private readonly string connectionString;

        public BaseDAL(IConfiguration config)
        {
            // lấy connection string chính xác theo appsettings của bạn
            connectionString = config.GetConnectionString("LiteCommerceDB")
                ?? throw new Exception("Missing ConnectionString 'LiteCommerceDB' in appsettings.json");
        }

        protected IDbConnection OpenConnection()
        {
            var conn = new SqlConnection(connectionString);
            conn.Open();
            return conn;
        }
    }
}