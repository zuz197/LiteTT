using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Models.Common;
using Lite.Models.Partner;

namespace Lite.Admin.Controllers
{
    [Authorize(Roles = $"{WebUserRoles.Administrator},{WebUserRoles.DataManager}")]
    public class SupplierController : Controller
    {
        private const int PAGESIZE = 10;
        private const string SUPPLIER_SEARCH_INPUT = "SupplierSearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(SUPPLIER_SEARCH_INPUT);
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
            var result = await PartnerDataService.ListSuppliersAsync(input);
            ApplicationContext.SetSessionData(SUPPLIER_SEARCH_INPUT, input);
            return View(result);
        }

        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung Nhà cung cấp";
            var model = new Supplier()
            {
                SupplierID = 0
            };
            return View("Edit", model);
        }
        public async Task<IActionResult> Edit(int id )
        {
            ViewBag.Title = "Cập nhật thông tin Nhà cung cấp";
            var model = await PartnerDataService.GetSupplierAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SaveData(Supplier data)
        {
            ViewBag.Title = data.SupplierID == 0 ? "Bổ sung Nhà cung cấp" : "Cập nhật thông tin Nhà cung cấp";

            try
            {
                if (string.IsNullOrWhiteSpace(data.SupplierName))
                    ModelState.AddModelError(nameof(data.SupplierName), "Vui lòng nhập tên của khách hàng");
                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng cho biết Email của khách hàng");
                else if (!(await PartnerDataService.ValidateSupplierEmailAsync(data.Email, data.SupplierID)))
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
                if (data.SupplierID == 0)
                {
                    await PartnerDataService.AddSupplierAsync(data);
                }
                else
                {
                    await PartnerDataService.UpdateSupplierAsync(data);
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
            if (Request.Method == "POST")
            {
                if (!await PartnerDataService.DeleteSupplierAsync(id))
                    TempData["DeleteError"] = "Không xóa được nhà cung cấp (đang được sử dụng).";
                return RedirectToAction("Index");
            }
            var model = await PartnerDataService.GetSupplierAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedSupplierAsync(id));

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHidden(int id)
        {
            await PartnerDataService.ToggleHiddenSupplierAsync(id);
            return RedirectToAction("Index");
        }

    }
}