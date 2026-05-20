using Dapper;
using Lite.DataLayers.Interfaces;
using Lite.Models.Common;
using Lite.Models.HR;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Lite.DataLayers.SQLServer
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly string connectionString;

        public EmployeeRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        public async Task<int> AddAsync(Employee data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"INSERT INTO Employees
                            (FullName, BirthDate, Address, Phone, Email, Photo, IsWorking)
                            VALUES
                            (@FullName, @BirthDate, @Address, @Phone, @Email, @Photo, @IsWorking);
                            SELECT CAST(SCOPE_IDENTITY() as int);";

                return await connection.ExecuteScalarAsync<int>(sql, data);
            }
        }

        public async Task<bool> UpdateAsync(Employee data)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"UPDATE Employees
                            SET FullName = @FullName,
                                BirthDate = @BirthDate,
                                Address = @Address,
                                Phone = @Phone,
                                Email = @Email,
                                Photo = @Photo,
                                IsWorking = @IsWorking
                            WHERE EmployeeID = @EmployeeID";

                int result = await connection.ExecuteAsync(sql, data);
                return result > 0;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"DELETE FROM Employees WHERE EmployeeID = @id";
                int result = await connection.ExecuteAsync(sql, new { id });
                return result > 0;
            }
        }

        public async Task<Employee?> GetAsync(int id)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT * FROM Employees WHERE EmployeeID = @id";
                return await connection.QueryFirstOrDefaultAsync<Employee>(sql, new { id });
            }
        }

        public async Task<PagedResult<Employee>> ListAsync(PaginationSearchInput input)
        {
            using (var connection = OpenConnection())
            {
                int rowCount;
                List<Employee> data;

                var sql = @"SELECT COUNT(*) 
                            FROM Employees
                            WHERE FullName LIKE @searchValue;

                            SELECT *
                            FROM Employees
                            WHERE FullName LIKE @searchValue
                            ORDER BY FullName
                            OFFSET (@page - 1) * @pageSize ROWS
                            FETCH NEXT @pageSize ROWS ONLY;";

                using (var multi = await connection.QueryMultipleAsync(sql, new
                {
                    page = input.Page,
                    pageSize = input.PageSize,
                    searchValue = "%" + input.SearchValue + "%"
                }))
                {
                    rowCount = multi.Read<int>().Single();
                    data = multi.Read<Employee>().ToList();
                }

                return new PagedResult<Employee>()
                {
                    Page = input.Page,
                    PageSize = input.PageSize,
                    RowCount = rowCount,
                    DataItems = data
                };
            }
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT COUNT(*) FROM Orders WHERE EmployeeID = @id";
                int count = await connection.ExecuteScalarAsync<int>(sql, new { id });
                return count > 0;
            }
        }

        public Task<bool> ToggleHiddenAsync(int id)
            => throw new NotSupportedException("Không hỗ trợ ẩn/hiện nhân viên.");

        public async Task<bool> ValidateEmailAsync(string email, int id = 0)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT COUNT(*) 
                            FROM Employees
                            WHERE Email = @email AND EmployeeID <> @id";

                int count = await connection.ExecuteScalarAsync<int>(sql, new { email, id });
                return count == 0;
            }
        }

        public async Task<string?> GetRoleNamesAsync(int employeeID)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"SELECT RoleNames FROM Employees WHERE EmployeeID = @employeeID";
                return await connection.ExecuteScalarAsync<string?>(sql, new { employeeID });
            }
        }

        public async Task<bool> UpdateRoleNamesAsync(int employeeID, string roleNames)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"UPDATE Employees SET RoleNames = @roleNames WHERE EmployeeID = @employeeID";
                int rows = await connection.ExecuteAsync(sql, new { employeeID, roleNames });
                return rows > 0;
            }
        }

        public async Task<bool> ChangePasswordAsync(int employeeID, string oldPassword, string newPassword)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"
                    UPDATE Employees
                    SET [Password] = @newPassword
                    WHERE EmployeeID = @employeeID
                      AND [Password] = @oldPassword;
                ";
                int rows = await connection.ExecuteAsync(sql, new { employeeID, oldPassword, newPassword });
                return rows > 0;
            }
        }

        public async Task<bool> SetPasswordAsync(int employeeID, string newPassword)
        {
            using (var connection = OpenConnection())
            {
                var sql = @"UPDATE Employees SET [Password] = @newPassword WHERE EmployeeID = @employeeID";
                int rows = await connection.ExecuteAsync(sql, new { employeeID, newPassword });
                return rows > 0;
            }
        }
    }
}