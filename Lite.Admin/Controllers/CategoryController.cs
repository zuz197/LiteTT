using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.BusinessLayers;
using Lite.Models.Common;
using Lite.Models.Catalog;

namespace Lite.Admin.Controllers
{
    [Authorize(Roles = $"{WebUserRoles.Administrator},{WebUserRoles.DataManager}")]
    public class CategoryController : Controller
    {
        private const int PAGESIZE = 10;
        private const string CATEGORY_SEARCH_INPUT = "CategorySearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(CATEGORY_SEARCH_INPUT);
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
            var result = await CatalogDataService.ListCategoriesAsync(input);
            ApplicationContext.SetSessionData(CATEGORY_SEARCH_INPUT, input);
            return View(result);
        }

        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung loại hàng";
            var model = new Category()
            {
                CategoryID = 0
            };
            return View("Edit", model);
        }
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin loại hàng";
            var model = await CatalogDataService.GetCategoryAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SaveData(Category data)
        {
            ViewBag.Title = data.CategoryID == 0 ? "Bổ sung loại hàng" : "Cập nhật thông tin loại hàng";
            try
            {
                if (string.IsNullOrWhiteSpace(data.CategoryName))
                    ModelState.AddModelError(nameof(data.CategoryName), "Vui lòng nhập tên loại hàng");

                if (string.IsNullOrEmpty(data.Description))
                    data.Description = "";

                if (!ModelState.IsValid)
                    return View("Edit", data);

                if (data.CategoryID == 0)
                    await CatalogDataService.AddCategoryAsync(data);
                else
                    await CatalogDataService.UpdateCategoryAsync(data);

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
                if (!await CatalogDataService.DeleteCategoryAsync(id))
                    TempData["DeleteError"] = "Không xóa được loại hàng (đang có mặt hàng thuộc loại này).";
                return RedirectToAction("Index");
            }

            var model = await CatalogDataService.GetCategoryAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await CatalogDataService.IsUsedCategoryAsync(id));
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHidden(int id)
        {
            await CatalogDataService.ToggleHiddenCategoryAsync(id);
            return RedirectToAction("Index");
        }
    }
}