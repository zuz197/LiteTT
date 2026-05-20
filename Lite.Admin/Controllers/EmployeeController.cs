using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Admin.Models;
using Lite.Models.Common;
using Lite.Models.HR;

namespace Lite.Admin.Controllers
{
    [Authorize(Roles = $"{WebUserRoles.Administrator}")]
    public class EmployeeController : Controller
    {
        private const int PAGESIZE = 10;
        private const string EMPLOYEE_SEARCH_INPUT = "EmployeeSearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(EMPLOYEE_SEARCH_INPUT);
            if (input == null)
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = PAGESIZE,
                    SearchValue = ""
                };

            return View(input);
        }

        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            var result = await HRDataService.ListEmployeesAsync(input);
            ApplicationContext.SetSessionData(EMPLOYEE_SEARCH_INPUT, input);
            return View(result);
        }

        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung nhân viên";
            var model = new Employee()
            {
                EmployeeID = 0,
                IsWorking = true
            };
            return View("Edit", model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin nhân viên";
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveData(Employee data, IFormFile? uploadPhoto)
        {
            ViewBag.Title = data.EmployeeID == 0 ? "Bổ sung nhân viên" : "Cập nhật thông tin nhân viên";

            try
            {
                if (string.IsNullOrWhiteSpace(data.FullName))
                    ModelState.AddModelError(nameof(data.FullName), "Vui lòng nhập họ tên nhân viên");

                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng nhập email");
                else if (!(await HRDataService.ValidateEmployeeEmailAsync(data.Email, data.EmployeeID)))
                    ModelState.AddModelError(nameof(data.Email), "Email này đã có người sử dụng");

                var oldData = data.EmployeeID > 0
                    ? await HRDataService.GetEmployeeAsync(data.EmployeeID)
                    : null;
                data.IsWorking = Request.Form["IsWorking"].ToString().Contains("true");
                if (oldData != null)
                {
                    if (string.IsNullOrWhiteSpace(data.Address))
                        data.Address = oldData.Address;

                    if (string.IsNullOrWhiteSpace(data.Phone))
                        data.Phone = oldData.Phone;

                    if (string.IsNullOrWhiteSpace(data.Photo))
                        data.Photo = oldData.Photo;
                }

                // ==============================
                // UPLOAD ẢNH
                // ==============================
                if (uploadPhoto != null && uploadPhoto.Length > 0)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";

                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/employees");

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    var filePath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }

                    data.Photo = fileName;
                }

                // ==============================

                if (!ModelState.IsValid)
                    return View("Edit", data);

                if (data.EmployeeID == 0)
                    await HRDataService.AddEmployeeAsync(data);
                else
                    await HRDataService.UpdateEmployeeAsync(data);

                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError("Error", "Hệ thống đang bận, Vui lòng thử lại sau!");
                return View("Edit", data);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                if (!await HRDataService.DeleteEmployeeAsync(id))
                    TempData["DeleteError"] = "Không xóa được nhân viên (đang được tham chiếu, ví dụ đơn hàng).";
                return RedirectToAction("Index");
            }

            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await HRDataService.IsUsedEmployeeAsync(id));
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null)
                return RedirectToAction("Index");

            var model = new EmployeeChangePasswordViewModel
            {
                Employee = employee
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(int id, EmployeeChangePasswordViewModel model)
        {
            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null)
                return RedirectToAction("Index");

            model.Employee = employee;

            if (string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới");

            if (model.NewPassword != model.ConfirmPassword)
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Xác nhận mật khẩu không khớp");

            if (!ModelState.IsValid)
                return View(model);

            var newHash = CryptHelper.HashMD5(model.NewPassword);
            bool ok = await HRDataService.SetEmployeePasswordAsync(id, newHash);

            if (!ok)
            {
                ModelState.AddModelError("Error", "Không đổi được mật khẩu. Vui lòng thử lại.");
                return View(model);
            }

            TempData["Message"] = "Đổi mật khẩu nhân viên thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ChangeRole(int id)
        {
            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null)
                return RedirectToAction("Index");

            var roleNames = await HRDataService.GetEmployeeRoleNamesAsync(id) ?? string.Empty;

            var selectedRoles = roleNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var model = new EmployeeRoleViewModel
            {
                Employee = employee,
                SelectedRoles = selectedRoles
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(int id, EmployeeRoleViewModel model)
        {
            if (model.Employee.EmployeeID != id)
                model.Employee.EmployeeID = id;

            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null)
                return RedirectToAction("Index");

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                WebUserRoles.Administrator,
                WebUserRoles.DataManager,
                WebUserRoles.Sales
            };

            var roles = (model.SelectedRoles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Where(r => allowed.Contains(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var roleNames = string.Join(",", roles);
            await HRDataService.UpdateEmployeeRoleNamesAsync(id, roleNames);

            return RedirectToAction(nameof(ChangeRole), new { id });
        }
    }
}