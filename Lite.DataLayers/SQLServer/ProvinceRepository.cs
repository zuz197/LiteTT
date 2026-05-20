using Dapper;
using Microsoft.Data.SqlClient;
using Lite.DataLayers.Interfaces;
using Lite.Models.DataDictionary;

namespace Lite.DataLayers.SQLServer
{
    public class ProvinceRepository : IDataDictionaryRepository<Province>
    {
        private readonly string _connectionString;

        public ProvinceRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<List<Province>> ListAsync()
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT ProvinceName
                FROM Provinces
                ORDER BY ProvinceName
            ";

            var data = await connection.QueryAsync<Province>(sql);

            return data.ToList();
        }
    }
}