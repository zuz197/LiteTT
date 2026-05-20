using Microsoft.AspNetCore.Mvc;
using Lite.Shop.DAL;

namespace Lite.Shop.Controllers
{
    public class CartController : Controller
    {
        private readonly CartDAL cartDAL;
        private readonly ProductDAL productDAL;
        private readonly CustomerDAL customerDAL;

        public CartController(CartDAL cDal, ProductDAL pDal, CustomerDAL csDal)
        {
            cartDAL = cDal;
            productDAL = pDal;
            customerDAL = csDal;
        }

        public IActionResult Index()
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            var cart = cartDAL.Get(customerId);

            ViewBag.Customer = customerDAL.Get(customerId);

            return View(cart);
        }

        public IActionResult Add(int productId)
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            var product = productDAL.Get(productId);

            if (product == null)
                return Content("Sản phẩm không tồn tại");

            if (product.IsSelling != true)
                return Content("Sản phẩm này đã ngừng bán và không thể thêm vào giỏ hàng.");

            cartDAL.Add(customerId, productId, 1);

            return RedirectToAction("Index");
        }

        public IActionResult Remove(int productId)
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            cartDAL.Remove(customerId, productId);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Update(int productId, int quantity)
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            cartDAL.Update(customerId, productId, quantity);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            cartDAL.Clear(customerId);

            return RedirectToAction("Index");
        }
    }
}