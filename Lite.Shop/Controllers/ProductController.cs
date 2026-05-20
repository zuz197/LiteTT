using Microsoft.AspNetCore.Mvc;
using Lite.Shop.DAL;
using Lite.Shop.Models;

namespace Lite.Shop.Controllers
{
    public class ProductController : Controller
    {
        private readonly ProductDAL productDAL;

        public ProductController(ProductDAL dal)
        {
            productDAL = dal;
        }

        public IActionResult Index(string search = "", int? categoryId = 0, string priceRange = "", int page = 1)
        {
            decimal? min = null;
            decimal? max = null;

            if (!string.IsNullOrEmpty(priceRange))
            {
                if (priceRange == "1") { min = 0; max = 99999; }
                else if (priceRange == "2") { min = 100000; max = 500000; }
                else if (priceRange == "3") { min = 500001; }
            }

            var all = productDAL.List(search, categoryId, min, max) ?? new List<Product>();

            int pageSize = 12; // keep 12 products per page
            int total = all.Count;
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Search = search ?? "";
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.PriceRange = priceRange ?? "";

            return View(items);
        }

        // DETAILS: use ProductDetailsViewModel
        public IActionResult Details(int id)
        {
            var model = productDAL.GetDetails(id);

            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }
    }
}