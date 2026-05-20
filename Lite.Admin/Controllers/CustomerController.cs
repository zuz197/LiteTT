using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Models.Common;
using Lite.Models.Partner;
using System.Linq.Expressions;

namespace Lite.Admin.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến khác hàng
    /// </summary>
    [Authorize(Roles = $"{WebUserRoles.Administrator},{WebUserRoles.DataManager}")]
    public class CustomerController : Controller
    {
        //private const int PAGESIZE = 10; //Hard Code
        /// <summary>
        /// tên biến session lưu lại điều kiện tìm kiếm khách hàng
        /// </summary>
        private const string CUSTOMER_SEARCH_INPUT = "CustomerSearchInput";
        /// <summary>
        /// Giao diên để nhập đầu vào tìm kiếm và hiển thị kết quả tìm kiếm 
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(CUSTOMER_SEARCH_INPUT);
            if (input == null)
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };

            return View(input);
        }

        /// <summary>
        /// Tìm kiếm khách hàng và trả về kết quả dưới dạng phân trang
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            var result = await PartnerDataService.ListCustomersAsync(input);
            ApplicationContext.SetSessionData(CUSTOMER_SEARCH_INPUT, input);
            return View(result);
        }
        /// <summary>
        /// Bổ sung khách hàng mới
        /// </summary>
        /// <returns></returns>
        public IActionResult Create() {
            ViewBag.Title = "Bổ sung khách hàng";
            var model = new Customer()
            {
                CustomerID = 0
            };
            return View("Edit", model);
        }
        /// <summary>
        /// Cập nhật thông tin khách hàng
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin khách hàng";
            var model = await PartnerDataService.GetCustomerAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SaveData(Customer data)
        {
            ViewBag.Title = data.CustomerID == 0 ? "Bổ sung khách hàng" : "Cập nhật thông tin khách hàng";

            //Sử dụng ModelState để kiểm soát thông báo lỗi và gửi thông báo lỗi cho view
            try {
                if (string.IsNullOrWhiteSpace(data.CustomerName))
                    ModelState.AddModelError(nameof(data.CustomerName), "Vui lòng nhập tên của khách hàng");
                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng cho biết Email của khách hàng");
                else if (!(await PartnerDataService.ValidatelCustomerEmailAsync(data.Email, data.CustomerID)))
                    ModelState.AddModelError(nameof(data.Email), "Email này đã có người sử dụng");
                if (string.IsNullOrWhiteSpace(data.Province))
                    ModelState.AddModelError(nameof(data.Province), "Vui lòng chọn Tỉnh/Thành");

                //Điều chỉnh lại các giá trị dữ liệu khác theo qui định/qui ước của App
                if (string.IsNullOrEmpty(data.ContactName)) data.ContactName = "";
                if (string.IsNullOrEmpty(data.Phone)) data.Phone = "";
                if (string.IsNullOrEmpty(data.Address)) data.Address = "";

                if (!ModelState.IsValid)
                {
                    return View("Edit", data);
                }


                //Yêu cầu lưu dữ liệu vào CSDL
                if (data.CustomerID == 0)
                {
                    await PartnerDataService.AddCustomerAsync(data);
                }
                else
                {
                    await PartnerDataService.UpdateCustomerAsync(data);
                }
                return RedirectToAction("Index");

            }
            catch (Exception)
            {
                //Lưu log lỗi (ghi log tại đây nếu có ILogger)
                ModelState.AddModelError("Error", "Hệ thống đang bận, Vui lòng thủ lại sau!");
                return View("Edit", data);
            }
        }
        
        public async Task<IActionResult> Delete(int id)
        {
            if(Request.Method == "POST")
            {
                if (!await PartnerDataService.DeleteCustomerAsync(id))
                    TempData["DeleteError"] = "Không xóa được khách hàng (đang có đơn hàng tham chiếu).";
                return RedirectToAction("Index");
            }
            var model = await PartnerDataService.GetCustomerAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedCustomerAsync(id));

            return View(model);
        }
        public async Task<IActionResult> ChangePassword(int id)
        {
            var customer = await PartnerDataService.GetCustomerAsync(id);
            if (customer == null)
                return RedirectToAction("Index");

            return View(customer);
        }
            [HttpPost]
            public async Task<IActionResult> ChangePassword(int id, string NewPassword, string ConfirmPassword)
            {
                var customer = await PartnerDataService.GetCustomerAsync(id);
                if (customer == null)
                    return RedirectToAction("Index");

                if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
                {
                    ViewBag.Error = "Mật khẩu không hợp lệ hoặc không khớp";
                    return View(customer);
                }

                string hashedPassword = CryptHelper.HashMD5(NewPassword);

                bool result = await PartnerDataService.ChangeCustomerPasswordAsync(id, hashedPassword);

                if (result)
                    ViewBag.Message = "Đổi mật khẩu thành công";
                else
                    ViewBag.Error = "Đổi mật khẩu thất bại";

                return View(customer);
            }
    }
}
