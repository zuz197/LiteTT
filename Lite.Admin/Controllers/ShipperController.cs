using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Models.Common;
using Lite.Models.Partner;

namespace Lite.Admin.Controllers
{
    [Authorize(Roles = $"{WebUserRoles.Administrator},{WebUserRoles.DataManager}")]
    public class ShipperController : Controller
    {
        private const int PAGESIZE = 10;
        private const string SHIPPER_SEARCH_INPUT = "ShipperSearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(SHIPPER_SEARCH_INPUT);
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
            var result = await PartnerDataService.ListShippersAsync(input);
            ApplicationContext.SetSessionData(SHIPPER_SEARCH_INPUT, input);
            return View(result);
        }

        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung người giao hàng";
            var model = new Shipper()
            {
                ShipperID = 0
            };
            return View("Edit", model);
        }
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin người giao hàng";
            var model = await PartnerDataService.GetShipperAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SaveData(Shipper data)
        {
            ViewBag.Title = data.ShipperID == 0 ? "Bổ sung người giao hàng" : "Cập nhật thông tin người giao hàng";
            try
            {
                if (string.IsNullOrWhiteSpace(data.ShipperName))
                    ModelState.AddModelError(nameof(data.ShipperName), "Vui lòng nhập tên người giao hàng");
                if (string.IsNullOrEmpty(data.Phone))
                    data.Phone = "";

                if (!ModelState.IsValid)
                    return View("Edit", data);

                if (data.ShipperID == 0)
                    await PartnerDataService.AddShipperAsync(data);
                else
                    await PartnerDataService.UpdateShipperAsync(data);

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
                if (!await PartnerDataService.DeleteShipperAsync(id))
                    TempData["DeleteError"] = "Không xóa được người giao hàng (đang được gán cho đơn).";
                return RedirectToAction("Index");
            }

            var model = await PartnerDataService.GetShipperAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedShipperAsync(id));
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHidden(int id)
        {
            await PartnerDataService.ToggleHiddenShipperAsync(id);
            return RedirectToAction("Index");
        }
    }
}