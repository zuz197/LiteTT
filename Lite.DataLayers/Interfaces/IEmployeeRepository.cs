using Lite.Models.HR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lite.DataLayers.Interfaces
{
    /// <summary>
    /// Định nghĩa các phép xử lý dữ liệu trên Employee
    /// </summary>
    public interface IEmployeeRepository : IGenericRepository<Employee>
    {
        /// <summary>
        /// Kiểm tra xem email của nhân viên có hợp lệ không
        /// </summary>
        /// <param name="email">Email cần kiểm tra</param>
        /// <param name="id">
        /// Nếu id = 0: Kiểm tra email của nhân viên mới
        /// Nếu id <> 0: Kiểm tra email của nhân viên có mã là id
        /// </param>
        /// <returns></returns>
        Task<bool> ValidateEmailAsync(string email, int id = 0);

        /// <summary>
        /// Lấy danh sách role của nhân viên (dạng chuỗi, ví dụ: "admin,sales")
        /// </summary>
        Task<string?> GetRoleNamesAsync(int employeeID);

        /// <summary>
        /// Cập nhật danh sách role của nhân viên (dạng chuỗi, ví dụ: "admin,sales")
        /// </summary>
        Task<bool> UpdateRoleNamesAsync(int employeeID, string roleNames);

        /// <summary>
        /// Đổi mật khẩu nhân viên (yêu cầu đúng mật khẩu cũ - đã hash).
        /// </summary>
        Task<bool> ChangePasswordAsync(int employeeID, string oldPassword, string newPassword);

        /// <summary>
        /// Thiết lập lại mật khẩu nhân viên (không cần mật khẩu cũ - đã hash).
        /// </summary>
        Task<bool> SetPasswordAsync(int employeeID, string newPassword);
    }
}
