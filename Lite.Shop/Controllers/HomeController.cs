using Microsoft.AspNetCore.Mvc;

namespace Lite.Shop.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Trả về file giao diện Views/Home/Index.cshtml
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}