using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lite.Admin;
using Lite.Admin.Models;
using Lite.BusinessLayers;
using Lite.Models.Catalog;
using Lite.Models.Common;
using Lite.Models.Partner;
using Lite.Models.Sales;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Lite.Admin.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến nghiệp vụ bán hàng
    /// </summary>
    /// 
    //[Authorize(Roles = $"{WebUserRoles.Sales}")]
    public class OrderController : Controller
    {
        private const int PAGESIZE = 10;
        private const string ORDER_SEARCH = "OrderSearchInput";

        /// <summary>
        /// Giao diện nhập đầu vào tìm kiếm đơn hàng và hiển thị kết quả tìm kiếm
        /// </summary>
        public IActionResult Index()
        {
            ViewBag.Title = "Quản lý đơn hàng";

            var input = ApplicationContext.GetSessionData<OrderSearchInput>(ORDER_SEARCH);

            if (input == null)
            {
                input = new OrderSearchInput()
                {
                    Page = 1,
                    PageSize = PAGESIZE, // Đã sửa: dùng hằng số khai báo ở đầu class
                    SearchValue = "",
                    Status = 0,
                    DateFrom = null, // Nếu trong Model khai báo là string thì bạn có thể để "" nhé
                    DateTo = null
                };
            }

            return View(input);
        }

        /// <summary>
        /// Tìm kiếm đơn hàng theo từ khóa.
        /// </summary>
        public async Task<IActionResult> Search(OrderSearchInput input)
        {
            // Đã thêm: Đảm bảo PageSize luôn có giá trị để không bị lỗi lúc phân trang
            if (input.PageSize == 0)
                input.PageSize = PAGESIZE;

            // Flatpickr đang gửi ngày theo dạng dd/MM/yyyy, cần parse thủ công
            // để tránh phụ thuộc culture mặc định của server.
            var rawDateFrom = Request.Query["DateFrom"].ToString();
            if (!string.IsNullOrWhiteSpace(rawDateFrom) &&
                DateTime.TryParseExact(rawDateFrom, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
            {
                input.DateFrom = parsedFrom;
            }

            var rawDateTo = Request.Query["DateTo"].ToString();
            if (!string.IsNullOrWhiteSpace(rawDateTo) &&
                DateTime.TryParseExact(rawDateTo, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
            {
                input.DateTo = parsedTo;
            }

            var result = await SalesDataService.ListOrdersAsync(input);

            ApplicationContext.SetSessionData(ORDER_SEARCH, input);

            return View(result);
        }

        /// <summary>
        /// Hiển thị chi tiết một đơn hàng và các chức năng xử lý khác
        /// </summary>
        public async Task<IActionResult> Detail(int id)
        {
            var data = await SalesDataService.GetOrderAsync(id);
            if (data == null)
                return RedirectToAction("Index");

            // Lấy thêm danh sách chi tiết mặt hàng trong đơn để hiển thị trên View
            ViewBag.OrderDetails = await SalesDataService.ListDetailsAsync(id);
            ViewBag.ReturnRequest = await ReturnDataService.GetByOrderIDAsync(id);
            ViewBag.Title = "Chi tiết đơn hàng";

            return View(data);
        }

        private const string SEARCH_PRODUCT = "SearchProductToSale";
        /// <summary>
        /// Giao diện cung cấp các chức năng nghiệp vụ lập đơn hàng mới.
        /// </summary>
        public IActionResult Create()
        {
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(SEARCH_PRODUCT);
            if (input == null)
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = 3,
                    SearchValue = "",
                    CategoryID = 0,
                    SupplierID = 0,
                    MinPrice = 0,
                    MaxPrice = 0
                };
            return View(input);
        }


        /// <summary>
        /// Tim hang de ban
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> SearchProduct(ProductSearchInput input)
        {
            var result = await CatalogDataService.ListProductsAsync(input);
            ApplicationContext.SetSessionData(SEARCH_PRODUCT, input);
            return View(result);
        }
        public IActionResult ShowCart()
        {
            var cart = ShoppingCartHelper.GetShoppingCart();
            return View(cart);
        }
        [HttpPost]
        public async Task<IActionResult> AddCartItem(int productId =0, int quantity =0, decimal price =0)
        {
            //Kiểm tra dữ liệu hợp lệ 
            if (productId <= 0)
                return Json(new ApiResult(0, "Mã mặt hàng không hợp lệ"));
            if (quantity <= 0)
                return Json(new ApiResult(0, "Số lượng không hợp lệ"));
            if (price < 0)
                return Json(new ApiResult(0, "Giá bán không hợp lệ"));

            var product = await CatalogDataService.GetProductAsync(productId);
            if (product == null)
                return Json(new ApiResult(0, "Mặt hàng không tồn tại"));
            if (!product.IsSelling)
                return Json(new ApiResult(0, "Mặt hàng này đã ngưng bán"));

            //Thêm hnagf vào giỏ
            var item = new OrderDetailViewInfo()
            {
                ProductID = productId,
                ProductName = product.ProductName,
                Unit = product.Unit,
                Photo = product.Photo ?? "",
                Quantity = quantity,
                SalePrice = price
            };
            ShoppingCartHelper.AddItemToCart(item);
            return Json(new ApiResult(1, ""));
        }

        /// <summary>
        /// Hiển thị trang xác nhận xóa đơn hàng.
        /// </summary>
        public async Task<IActionResult> Delete(int id)
        {
            var data = await SalesDataService.GetOrderAsync(id);
            if (data == null)
                return RedirectToAction("Index");

            ViewBag.Title = "Xóa đơn hàng";
            return View(data);
        }

        /// <summary>
        /// Thực hiện xóa đơn hàng
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int orderID, string returnUrl = "")
        {
            await SalesDataService.DeleteOrderAsync(orderID);
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Cập nhật thông tin (số lượng, giá bán) của một mặt hàng
        /// trong giỏ hàng hoặc trong một đơn hàng
        /// </summary>
        public IActionResult EditCartItem( int productId)
        {
            var item = ShoppingCartHelper.GetCartItem(productId);
            return PartialView(item);
        }
        [HttpPost]
        public IActionResult UpdateCartItem(int productID, int quantity, decimal salePrice)
        {
            //Kiểm tra dữ liệu
            if (!ModelState.IsValid)
                return Json(new ApiResult(0, "Dữ liệu gửi lên không hợp lệ"));
            if (productID <= 0)
                return Json(new ApiResult(0, "Mã mặt hàng không hợp lệ"));
            if (quantity <= 0)
                return Json(new ApiResult(0, "Số lượng không hợp lệ"));
            if (salePrice < 0)
                return Json(new ApiResult(0, "Giá bán không hợp lệ"));

            var item = ShoppingCartHelper.GetCartItem(productID);
            if (item == null)
                return Json(new ApiResult(0, "Mặt hàng không tồn tại trong giỏ"));

            ShoppingCartHelper.UpdateCartItem(productID, quantity, salePrice);
            return Json(new ApiResult(1, ""));
        }
        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            int? customerID = null,
            string province = "",
            string address = "",
            string deliveryMode = "ship")
        {
            if (customerID.HasValue && customerID.Value < 0)
                return Json(new ApiResult(0, "Khách hàng không hợp lệ"));

            var cart = ShoppingCartHelper.GetShoppingCart();
            if (cart.Count == 0)
            {
                return Json(new ApiResult(0, "Giỏ hàng đang trống"));
            }

            var isPickup = string.Equals(deliveryMode?.Trim(), "pickup", StringComparison.OrdinalIgnoreCase);
            if (isPickup)
            {
                province = "";
                address = "Bán tại cửa hàng — không giao";
            }
            else
            {
                if (!customerID.HasValue || customerID.Value <= 0)
                    return Json(new ApiResult(0, "Giao hàng: vui lòng chọn khách hàng trong danh bạ."));
                if (string.IsNullOrWhiteSpace(province))
                    return Json(new ApiResult(0, "Giao hàng: vui lòng chọn tỉnh/thành."));
                if (string.IsNullOrWhiteSpace(address))
                    return Json(new ApiResult(0, "Giao hàng: vui lòng nhập địa chỉ giao."));
            }

            var lines = cart.Select(item => new OrderDetail
            {
                OrderID = 0,
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                SalePrice = item.SalePrice
            }).ToList();

            int orderID = await SalesDataService.CreateOrderWithDetailsAsync(customerID ?? 0, province, address, lines);
            if (orderID <= 0)
                return Json(new ApiResult(0, "Không lập được đơn hàng (giỏ hàng, khách hàng hoặc mặt hàng không hợp lệ)."));

            //Lập đơn thành công -> xóa giỏ hàng hiện tại
            ShoppingCartHelper.ClearCart();
            return Json(new ApiResult(orderID, ""));
        }


        /// <summary>
        /// Xóa một sản phẩm khỏi giỏ hàng hoặc đơn hàng.
        /// </summary>
        public IActionResult DeleteCartItem(int productId)
        {
            //POST: xóa hàng khỏi giỏ
            if(Request.Method == "POST")
            {
                ShoppingCartHelper.RemoveItemFromCart(productId);
                return Json(new ApiResult(1, ""));
            }
            //GET: Hiển thị giao diện
            ViewBag.ProductID = productId;
            return PartialView();
        }




        /// <summary>
        /// Xóa toàn bộ sản phẩm trong giỏ hàng hiện tại.
        /// </summary>
        public IActionResult ClearCart()
        {
            //POST: Xóa giở hàng
            if(Request.Method == "POST")
            {
                ShoppingCartHelper.ClearCart();
                return Json(new ApiResult(1, ""));
            }
            //GET: Hiển thị giao diện
            return PartialView();
        }




        /// <summary>
        /// Duyệt đơn hàng (cập nhật trạng thái thành "Đã duyệt").
        /// </summary>
        public async Task<IActionResult> Accept(int id)
        {
            if (!TryGetEmployeeId(out int employeeID))
                return RedirectToAction("Login", "Account");

            var ok = await SalesDataService.AcceptOrderAsync(id, employeeID);
            return RedirectToOrderDetail(id, ok ? null : "Không thể duyệt đơn (chỉ đơn chờ duyệt mới được duyệt).");
        }

        /// <summary>
        /// Form chọn người giao hàng (modal).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Shipping(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.Accepted)
                return BadRequest();

            // Đơn tại chỗ (DeliveryAddress chứa "không giao") -> không thể giao hàng
            if (order.DeliveryAddress != null &&
                order.DeliveryAddress.Contains("không giao", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.ErrorMessage = "Không thể áp dụng giao hàng với đơn hàng này.";
                return PartialView(new OrderShippingFormModel { OrderID = id, Shippers = new List<Shipper>() });
            }

            var activeShippers = await PartnerDataService.ListActiveShippersAsync();

            var model = new OrderShippingFormModel
            {
                OrderID = id,
                Shippers = activeShippers
            };
            return PartialView(model);
        }

        /// <summary>
        /// Gán shipper và chuyển đơn sang trạng thái Đang giao.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Shipping(int id, int shipperID)
        {
            var ok = await SalesDataService.ShipOrderAsync(id, shipperID);
            return RedirectToOrderDetail(id, ok ? null : "Không thể giao hàng (đơn phải đã duyệt và shipper hợp lệ).");
        }

        /// <summary>
        /// Sửa thông tin giao hàng của đơn (modal) — chỉ khi New/Accepted.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditInfo(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.New && order.Status != OrderStatusEnum.Accepted)
                return BadRequest();

            // Đơn tại chỗ không được sửa thông tin giao hàng
            bool isInStore = order.DeliveryAddress != null &&
                             order.DeliveryAddress.Contains("không giao", StringComparison.OrdinalIgnoreCase);
            if (isInStore)
            {
                ViewBag.ErrorMessage = "Không thể sửa thông tin giao hàng với đơn hàng bán tại cửa hàng.";
                return PartialView(new OrderEditInfoFormModel
                {
                    OrderID = id,
                    Provinces = new List<Lite.Models.DataDictionary.Province>()
                });
            }

            var provinces = await DictionaryDataService.ListProvincesAsync();
            var model = new OrderEditInfoFormModel
            {
                OrderID = id,
                CustomerID = order.CustomerID,
                DeliveryProvince = order.DeliveryProvince,
                DeliveryAddress = order.DeliveryAddress,
                Provinces = provinces ?? new List<Lite.Models.DataDictionary.Province>()
            };
            return PartialView(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditInfoConfirm(int id, string deliveryProvince, string deliveryAddress)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.New && order.Status != OrderStatusEnum.Accepted)
                return RedirectToOrderDetail(id, "Không thể sửa thông tin khi đơn đã chuyển trạng thái khác.");

            // Chặn đơn tại chỗ
            bool isInStore = order.DeliveryAddress != null &&
                             order.DeliveryAddress.Contains("không giao", StringComparison.OrdinalIgnoreCase);
            if (isInStore)
                return RedirectToOrderDetail(id, "Không thể sửa thông tin giao hàng với đơn hàng bán tại cửa hàng.");

            var updated = new Order
            {
                OrderID = id,
                CustomerID = order.CustomerID,
                DeliveryProvince = deliveryProvince,
                DeliveryAddress = deliveryAddress
            };

            var ok = await SalesDataService.UpdateOrderAsync(updated);
            return RedirectToOrderDetail(id, ok ? null : "Không thể cập nhật thông tin giao hàng (chỉ New/Đã duyệt và dữ liệu hợp lệ).");
        }

        /// <summary>
        /// Xác nhận từ chối đơn (modal).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Reject(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.New)
                return BadRequest();
            return PartialView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectConfirm(int id)
        {
            if (!TryGetEmployeeId(out int employeeID))
                return RedirectToAction("Login", "Account");

            var ok = await SalesDataService.RejectOrderAsync(id, employeeID);
            return RedirectToOrderDetail(id, ok ? null : "Không thể từ chối đơn (chỉ đơn chờ duyệt).");
        }

        /// <summary>
        /// Xác nhận hủy đơn (modal).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.New &&
                order.Status != OrderStatusEnum.Accepted &&
                order.Status != OrderStatusEnum.Shipping)
                return BadRequest();
            return PartialView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirm(int id)
        {
            var ok = await SalesDataService.CancelOrderAsync(id);
            return RedirectToOrderDetail(id, ok ? null : "Không thể hủy đơn với trạng thái hiện tại.");
        }

        /// <summary>
        /// Đang giao nhưng cần giao lại: trả đơn về Đã duyệt (bỏ shipper hiện tại).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RecallFromShipping(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();
            if (order.Status != OrderStatusEnum.Shipping &&
                order.Status != OrderStatusEnum.Accepted)
                return BadRequest();
            return PartialView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecallFromShippingConfirm(int id)
        {
            var ok = await SalesDataService.RevertShippingToAcceptedAsync(id);
            return RedirectToOrderDetail(id, ok ? null : "Không thể thu hồi (chỉ áp dụng đơn đang giao).");
        }

        /// <summary>
        /// Xác nhận hoàn tất giao hàng (modal).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Finish(int id)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
                return NotFound();

            // Đơn ship (DeliveryAddress không phải "tại chỗ") đang Accepted = chưa giao -> chặn
            bool isInStore = order.DeliveryAddress != null &&
                             order.DeliveryAddress.Contains("không giao", StringComparison.OrdinalIgnoreCase);
            if (!isInStore && order.Status == OrderStatusEnum.Accepted)
            {
                ViewBag.ErrorMessage = "Đơn hàng này không thể hoàn tất khi chưa giao hàng.";
                return PartialView(id);
            }

            // Chỉ cho phép: đơn tại chỗ (Accepted, không có ShipperID) hoặc đơn đang giao (Shipping)
            if (order.Status != OrderStatusEnum.Shipping &&
                order.Status != OrderStatusEnum.Accepted)
                return BadRequest();

            return PartialView(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishConfirm(int id)
        {
            var ok = await SalesDataService.CompleteOrderAsync(id);
            return RedirectToOrderDetail(id, ok ? null : "Không thể hoàn tất (chỉ đơn đã duyệt hoặc đang giao).");
        }


        // ============================================================
        // HOÀN HÀNG / ĐỔI HÀNG
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ReturnDetail(int id)
        {
            var rr = await ReturnDataService.GetByOrderIDAsync(id);
            if (rr == null) return NotFound();
            ViewBag.Shippers = await SelectListHelper.Shippers();
            return PartialView(rr);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnApprove(int returnRequestId, int orderId, int shipperId)
        {
            if (!TryGetEmployeeId(out int empId))
                return RedirectToAction("Login", "Account");
            await ReturnDataService.ApproveAsync(returnRequestId, empId, shipperId);
            return RedirectToOrderDetail(orderId, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnReject(int returnRequestId, int orderId, string rejectReason)
        {
            if (!TryGetEmployeeId(out int empId))
                return RedirectToAction("Login", "Account");
            await ReturnDataService.RejectAsync(returnRequestId, empId, rejectReason);
            return RedirectToOrderDetail(orderId, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnSetShipped(int returnRequestId, int orderId)
        {
            await ReturnDataService.SetShippedAsync(returnRequestId);
            return RedirectToOrderDetail(orderId, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnComplete(int returnRequestId, int orderId)
        {
            await ReturnDataService.CompleteAsync(returnRequestId);
            return RedirectToOrderDetail(orderId, null);
        }

        private IActionResult RedirectToOrderDetail(int id, string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                TempData["OrderError"] = errorMessage;
            return RedirectToAction("Detail", new { id });
        }

        private bool TryGetEmployeeId(out int employeeID)
        {
            employeeID = 0;
            var userData = User.GetUserData();
            if (userData == null || string.IsNullOrWhiteSpace(userData.UserId))
                return false;
            return int.TryParse(userData.UserId, out employeeID) && employeeID > 0;
        }
    }
}