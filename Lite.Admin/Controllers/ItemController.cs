using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lite.Admin.Controllers
{
    [Authorize]
    public class ItemController : Controller
    {
        public IActionResult Index() => RedirectToAction("Index", "Product");

        public IActionResult Edit(int id) => RedirectToAction("Edit", "Product", new { id });

        public IActionResult Delete(int id) => RedirectToAction("Delete", "Product", new { id });
    }
}
