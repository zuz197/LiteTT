using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.Admin.Models;
using Lite.BusinessLayers;
using System.Diagnostics;

namespace Lite.Admin.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Title = "Trang chủ";
            var data = await SalesDataService.GetDashboardDataAsync();
            return View(data);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
