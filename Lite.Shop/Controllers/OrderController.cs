using Microsoft.AspNetCore.Mvc;
using Lite.Shop.DAL;

namespace Lite.Shop.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderDAL orderDAL;
        private readonly CartDAL cartDAL;
        private readonly ReturnRequestDAL returnDAL;
        private readonly CustomerDAL customerDAL;

        public OrderController(OrderDAL oDal, CartDAL cDal, ReturnRequestDAL rDal, CustomerDAL csDal)
        {
            orderDAL = oDal;
            cartDAL = cDal;
            returnDAL = rDal;
            customerDAL = csDal;
        }

        public IActionResult Index()
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);
            var orders = orderDAL.List(customerId);

            return View(orders);
        }

        public IActionResult Details(int id)
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            var detail = orderDAL.GetOrderViewDetail(id);

            if (detail == null)
                return Content("Đơn hàng không tồn tại");

            detail.ReturnRequest = returnDAL.GetByOrderID(id);

            return View(detail);
        }

        public IActionResult ConfirmOrder()
        {
            var idStr = HttpContext.Session.GetString("CustomerID");
            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            var customer = customerDAL.Get(customerId);
            var cartItems = cartDAL.Get(customerId);

            if (cartItems == null || cartItems.Count == 0)
                return RedirectToAction("Index", "Cart");

            ViewBag.Customer = customer;
            ViewBag.CartItems = cartItems;
            return View();
        }

        [HttpPost]
        public IActionResult Checkout()
        {
            var idStr = HttpContext.Session.GetString("CustomerID");

            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            int customerId = int.Parse(idStr);

            try
            {
                int orderId = cartDAL.Checkout(customerId);
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (Exception ex)
            {
                return Content("Lỗi khi thanh toán: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult Cancel(int id)
        {
            orderDAL.CancelOrder(id);
            return RedirectToAction("Index");
        }
    }
}