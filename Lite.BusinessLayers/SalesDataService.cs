using System.Collections.Generic;
using Lite.DataLayers.Interfaces;
using Lite.DataLayers.SQLServer;
using Lite.Models.Common;
using Lite.Models.Sales;

namespace Lite.BusinessLayers
{
    /// <summary>
    /// Cung cấp các chức năng xử lý dữ liệu liên quan đến bán hàng
    /// bao gồm: đơn hàng (Order) và chi tiết đơn hàng (OrderDetail).
    /// </summary>
    public static class SalesDataService
    {
        /// <summary>Giới hạn độ dài chuỗi gửi từ client (không tin tưởng input).</summary>
        private const int MaxDeliveryProvinceLength = 255;
        private const int MaxDeliveryAddressLength = 500;
        /// <summary>Số dòng tối đa trong một đơn (chống payload quá lớn / lạm dụng).</summary>
        private const int MaxOrderLines = 500;
        /// <summary>Số lượng tối đa mỗi dòng (int hợp lệ nghiệp vụ).</summary>
        private const int MaxQuantityPerLine = 1_000_000;
        /// <summary>Trần đơn giá một dòng (tránh số quá lớn / sai sót nhập liệu).</summary>
        private const decimal MaxSalePricePerUnit = 999_999_999_999.99m;

        private static readonly IOrderRepository orderDB;

        /// <summary>
        /// Constructor
        /// </summary>
        static SalesDataService()
        {
            orderDB = new OrderRepository(Configuration.ConnectionString);
        }

        #region Order

        /// <summary>
        /// Tìm kiếm và lấy danh sách đơn hàng dưới dạng phân trang
        /// </summary>
        public static async Task<PagedResult<OrderSearchInfo>> ListOrdersAsync(OrderSearchInput input)
        {
            if (input == null)
            {
                input = new OrderSearchInput()
                {
                    Page = 1,
                    PageSize = 10,
                    SearchValue = ""
                };
            }
            else
            {
                var st = (int)input.Status;
                if (st != 0 && !Enum.IsDefined(typeof(OrderStatusEnum), st))
                    input.Status = (OrderStatusEnum)0;
            }

            return await orderDB.ListAsync(input);
        }

        /// <summary>
        /// Danh sách đơn hàng của khách (site bán lẻ — luôn lọc theo CustomerID).
        /// </summary>
        public static async Task<PagedResult<OrderSearchInfo>> ListCustomerOrdersAsync(int customerID, OrderSearchInput input)
        {
            if (customerID <= 0)
            {
                return new PagedResult<OrderSearchInfo>
                {
                    Page = 1,
                    PageSize = input?.PageSize > 0 ? input.PageSize : 10,
                    RowCount = 0,
                    DataItems = new List<OrderSearchInfo>()
                };
            }

            if (input == null)
            {
                input = new OrderSearchInput { Page = 1, PageSize = 10, SearchValue = "" };
            }
            else
            {
                var st = (int)input.Status;
                if (st != 0 && !Enum.IsDefined(typeof(OrderStatusEnum), st))
                    input.Status = (OrderStatusEnum)0;
            }

            input.CustomerID = customerID;
            input.SearchValue = "";
            return await orderDB.ListAsync(input);
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một đơn hàng
        /// </summary>
        public static async Task<OrderViewInfo?> GetOrderAsync(int orderID)
        {
            if (orderID <= 0)
                return null;
            return await orderDB.GetAsync(orderID);
        }

        /// <summary>
        /// Chi tiết đơn chỉ khi đơn thuộc đúng khách (tránh lộ dữ liệu).
        /// </summary>
        public static async Task<OrderViewInfo?> GetOrderForCustomerAsync(int orderID, int customerID)
        {
            if (orderID <= 0 || customerID <= 0)
                return null;
            var order = await orderDB.GetAsync(orderID);
            if (order == null || order.CustomerID != customerID)
                return null;
            return order;
        }

        /// <summary>
        /// Tạo đơn hàng mới (viết ngu)
        /// </summary>
        //public static async Task<int> AddOrderAsync(Order data)
        //{
        //   data.Status = OrderStatusEnum.New;
        //   data.OrderTime = DateTime.Now;
        //
        //     return await orderDB.AddAsync(data);
        // }
        /// <summary>
        /// Viết đúng
        /// </summary>
        /// <param name="customerID"></param>
        /// <param name="deliveryProcince"></param>
        /// <param name="deliverAddress"></param>
        /// <returns></returns>
        public static async Task<int> AddOrderAsync(int customerID, string deliveryProcince, string deliverAddress)
        {
            if (customerID < 0)
                return 0;

            if (customerID > 0)
            {
                var customer = await PartnerDataService.GetCustomerAsync(customerID);
                if (customer == null)
                    return 0;
            }

            var province = (deliveryProcince ?? "").Trim();
            var address = (deliverAddress ?? "").Trim();
            if (province.Length > MaxDeliveryProvinceLength || address.Length > MaxDeliveryAddressLength)
                return 0;

            var order = new Order()
            {
                CustomerID = customerID == 0 ? null : customerID,
                DeliveryProvince = province,
                DeliveryAddress = address,
                Status = OrderStatusEnum.New,
                OrderTime = DateTime.UtcNow
            };
            return await orderDB.AddAsync(order);
        }

        /// <summary>
        /// Tạo đơn và ghi toàn bộ chi tiết trong một transaction (lập đơn từ giỏ).
        /// </summary>
        /// <param name="deliveryContactPhone">SĐT nhận hàng cho đơn này (không cập nhật hồ sơ khách); lưu kèm trong DeliveryAddress.</param>
        public static async Task<int> CreateOrderWithDetailsAsync(int customerID, string deliveryProvince,
            string deliveryAddress, IReadOnlyList<OrderDetail> lines, string? deliveryContactPhone = null)
        {
            if (lines == null || lines.Count == 0 || lines.Count > MaxOrderLines)
                return 0;

            if (customerID < 0)
                return 0;

            if (customerID > 0)
            {
                var customer = await PartnerDataService.GetCustomerAsync(customerID);
                if (customer == null)
                    return 0;
            }

            var province = (deliveryProvince ?? "").Trim();
            var addr = (deliveryAddress ?? "").Trim();
            var phone = (deliveryContactPhone ?? "").Trim();
            if (phone.Length > 0)
                addr = "SĐT nhận hàng: " + phone + (addr.Length > 0 ? "\n" : "") + addr;
            if (province.Length > MaxDeliveryProvinceLength || addr.Length > MaxDeliveryAddressLength)
                return 0;

            var seen = new HashSet<int>();
            foreach (var line in lines)
            {
                if (line == null)
                    return 0;
                if (line.ProductID <= 0 || line.Quantity <= 0 || line.Quantity > MaxQuantityPerLine)
                    return 0;
                if (line.SalePrice < 0 || line.SalePrice > MaxSalePricePerUnit)
                    return 0;
                if (!seen.Add(line.ProductID))
                    return 0;

                var product = await CatalogDataService.GetProductAsync(line.ProductID);
                if (product == null || !product.IsSelling)
                    return 0;
            }

            var order = new Order()
            {
                CustomerID = customerID == 0 ? null : customerID,
                DeliveryProvince = province,
                DeliveryAddress = addr,
                Status = OrderStatusEnum.New,
                OrderTime = DateTime.UtcNow
            };

            var details = lines.Select(l => new OrderDetail
            {
                OrderID = 0,
                ProductID = l.ProductID,
                Quantity = l.Quantity,
                SalePrice = l.SalePrice
            }).ToList();

            return await orderDB.AddOrderWithDetailsAsync(order, details);
        }

        /// <summary>
        /// Cập nhật thông tin đơn hàng
        /// </summary>
        public static async Task<bool> UpdateOrderAsync(Order data)
        {
            if (data == null || data.OrderID <= 0)
                return false;

            var order = await orderDB.GetAsync(data.OrderID);
            if (order == null)
                return false;

            // Chỉ cho phép cập nhật thông tin đơn hàng khi đơn chưa giao.
            if (order.Status != OrderStatusEnum.New &&
                order.Status != OrderStatusEnum.Accepted)
                return false;

            if (data.CustomerID.HasValue && data.CustomerID.Value < 0)
                return false;
            if (data.CustomerID == 0)
                data.CustomerID = null;
            if (data.CustomerID is > 0)
            {
                var cust = await PartnerDataService.GetCustomerAsync(data.CustomerID.Value);
                if (cust == null)
                    return false;
            }

            var p = (data.DeliveryProvince ?? "").Trim();
            var a = (data.DeliveryAddress ?? "").Trim();
            if (p.Length > MaxDeliveryProvinceLength || a.Length > MaxDeliveryAddressLength)
                return false;
            data.DeliveryProvince = p;
            data.DeliveryAddress = a;

            // Không cho phép caller tự ý đổi các mốc thời gian/ trạng thái ở hàm update chung
            data.Status = order.Status;
            data.OrderTime = order.OrderTime;
            data.EmployeeID = order.EmployeeID;
            data.AcceptTime = order.AcceptTime;
            data.ShipperID = order.ShipperID;
            data.ShippedTime = order.ShippedTime;
            data.FinishedTime = order.FinishedTime;

            return await orderDB.UpdateAsync(data);
        }

        /// <summary>
        /// Xóa đơn hàng
        /// </summary>
        public static async Task<bool> DeleteOrderAsync(int orderID)
        {
            if (orderID <= 0)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null)
                return false;

            // 👉 CHỈ CHO HUỶ nếu đang xử lý
            if (order.Status != OrderStatusEnum.New &&
                order.Status != OrderStatusEnum.Accepted)
                return false;

            // 🔥 KHÔNG XOÁ DB NỮA → CHUYỂN SANG HUỶ
            order.Status = OrderStatusEnum.Cancelled;
            order.FinishedTime = DateTime.UtcNow;

            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Thống kê cho dashboard trang chủ quản trị.
        /// </summary>
        public static async Task<SalesDashboardData> GetDashboardDataAsync()
        {
            return await orderDB.GetDashboardDataAsync();
        }

        #endregion

        #region Order Status Processing

        /// <summary>
        /// Duyệt đơn hàng
        /// </summary>
        public static async Task<bool> AcceptOrderAsync(int orderID, int employeeID)
        {
            if (orderID <= 0 || employeeID <= 0)
                return false;

            var employee = await HRDataService.GetEmployeeAsync(employeeID);
            if (employee == null)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null) 
                return false;

            if (order.Status != OrderStatusEnum.New)
                return false;

            order.EmployeeID = employeeID;
            order.AcceptTime = DateTime.UtcNow;
            order.Status = OrderStatusEnum.Accepted;

            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Từ chối đơn hàng
        /// </summary>
        public static async Task<bool> RejectOrderAsync(int orderID, int employeeID)
        {
            if (orderID <= 0 || employeeID <= 0)
                return false;

            var employee = await HRDataService.GetEmployeeAsync(employeeID);
            if (employee == null)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null) 
                return false;

            if (order.Status != OrderStatusEnum.New)
                return false;

            order.EmployeeID = employeeID;
            order.FinishedTime = DateTime.UtcNow;
            order.Status = OrderStatusEnum.Rejected;
            
            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Hủy đơn hàng
        /// </summary>
        public static async Task<bool> CancelOrderAsync(int orderID)
        {
            if (orderID <= 0)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null) 
                return false;

            if (order.Status != OrderStatusEnum.New &&
                order.Status != OrderStatusEnum.Accepted &&
                order.Status != OrderStatusEnum.Shipping)
                return false;

            order.FinishedTime = DateTime.UtcNow;
            order.Status = OrderStatusEnum.Cancelled;
            
            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Đơn đang giao nhưng giao không thành / cần đổi shipper: trả về trạng thái Đã duyệt (xóa shipper và thời điểm bàn giao).
        /// </summary>
        public static async Task<bool> RevertShippingToAcceptedAsync(int orderID)
        {
            if (orderID <= 0)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.Shipping)
                return false;

            order.Status = OrderStatusEnum.Accepted;
            order.ShipperID = null;
            order.ShippedTime = null;

            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Giao đơn hàng cho người giao hàng
        /// </summary>
        public static async Task<bool> ShipOrderAsync(int orderID, int shipperID)
        {
            if (orderID <= 0 || shipperID <= 0)
                return false;

            var shipper = await PartnerDataService.GetShipperAsync(shipperID);
            if (shipper == null)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null) 
                return false;

            if (order.Status != OrderStatusEnum.Accepted)
                return false;

            order.ShipperID = shipperID;
            order.ShippedTime = DateTime.UtcNow;
            order.Status = OrderStatusEnum.Shipping;
            
            return await orderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Hoàn tất đơn hàng
        /// </summary>
        public static async Task<bool> CompleteOrderAsync(int orderID)
        {
            if (orderID <= 0)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null) 
                return false;

            if (order.Status != OrderStatusEnum.Shipping &&
                order.Status != OrderStatusEnum.Accepted)
                return false;

            order.FinishedTime = DateTime.UtcNow;
            order.Status = OrderStatusEnum.Completed;
            
            return await orderDB.UpdateAsync(order);
        }

        #endregion

        #region Order Detail

        /// <summary>
        /// Lấy danh sách mặt hàng của đơn hàng
        /// </summary>
        public static async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            if (orderID <= 0)
                return new List<OrderDetailViewInfo>();
            return await orderDB.ListDetailsAsync(orderID);
        }

        /// <summary>
        /// Lấy thông tin một mặt hàng trong đơn hàng
        /// </summary>
        public static async Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            if (orderID <= 0 || productID <= 0)
                return null;
            return await orderDB.GetDetailAsync(orderID, productID);
        }

        /// <summary>
        /// Thêm mặt hàng vào đơn hàng
        /// </summary>
        public static async Task<bool> AddDetailAsync(OrderDetail data)
        {
            if (data == null || data.OrderID <= 0 || data.ProductID <= 0)
                return false;
            if (data.Quantity <= 0 || data.Quantity > MaxQuantityPerLine)
                return false;
            if (data.SalePrice < 0 || data.SalePrice > MaxSalePricePerUnit)
                return false;

            var product = await CatalogDataService.GetProductAsync(data.ProductID);
            if (product == null || !product.IsSelling)
                return false;

            var order = await orderDB.GetAsync(data.OrderID);
            if (order == null || order.Status != OrderStatusEnum.New)
                return false;

            // Tránh thêm trùng một mặt hàng đã tồn tại trong đơn.
            var existedDetail = await orderDB.GetDetailAsync(data.OrderID, data.ProductID);
            if (existedDetail != null)
                return false;

            var lineCount = (await orderDB.ListDetailsAsync(data.OrderID)).Count;
            if (lineCount >= MaxOrderLines)
                return false;

            return await orderDB.AddDetailAsync(data);
        }

        /// <summary>
        /// Cập nhật mặt hàng trong đơn hàng
        /// </summary>
        public static async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            if (data == null || data.OrderID <= 0 || data.ProductID <= 0)
                return false;
            if (data.Quantity <= 0 || data.Quantity > MaxQuantityPerLine)
                return false;
            if (data.SalePrice < 0 || data.SalePrice > MaxSalePricePerUnit)
                return false;

            var order = await orderDB.GetAsync(data.OrderID);
            if (order == null || order.Status != OrderStatusEnum.New)
                return false;

            return await orderDB.UpdateDetailAsync(data);
        }

        /// <summary>
        /// Xóa mặt hàng khỏi đơn hàng
        /// </summary>
        public static async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            if (orderID <= 0 || productID <= 0)
                return false;

            var order = await orderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.New)
                return false;

            return await orderDB.DeleteDetailAsync(orderID, productID);
        }

        #endregion
    }
}