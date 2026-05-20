using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Lite.BusinessLayers;
using Lite.Models.Catalog;

namespace Lite.Admin.Controllers
{
    [Authorize(Roles = $"{WebUserRoles.Administrator},{WebUserRoles.DataManager}")]
    public class ProductController : Controller
    {
        private const int PAGESIZE = 10;
        private const string PRODUCT_SEARCH_INPUT = "ProductSearchInput";

        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(PRODUCT_SEARCH_INPUT);
            if (input == null)
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = PAGESIZE,
                    SearchValue = "",
                    CategoryID = 0,
                    SupplierID = 0,
                    MinPrice = 0,
                    MaxPrice = 0
                };

            return View(input);
        }

        public async Task<IActionResult> Search(ProductSearchInput input)
        {
            var result = await CatalogDataService.ListProductsAsync(input);
            ApplicationContext.SetSessionData(PRODUCT_SEARCH_INPUT, input);
            return View(result);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var model = await CatalogDataService.GetProductAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.ProductID = id;
            ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(id);
            ViewBag.Photos = await CatalogDataService.ListPhotosAsync(id);
            return View(model);
        }
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung mặt hàng";
            var model = new Product()
            {
                ProductID = 0,
                IsSelling = true,
                Price = 0
            };
            ViewBag.ProductID = 0;
            ViewBag.ProductAttributes = new List<ProductAttribute>();
            ViewBag.ProductPhotos = new List<ProductPhoto>();
            return View("Edit", model);
        }
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin mặt hàng";
            var model = await CatalogDataService.GetProductAsync(id);
            if (model == null)
                return RedirectToAction("Index");
            ViewBag.ProductID = id;
            ViewBag.ProductAttributes = await CatalogDataService.ListAttributesAsync(id);
            ViewBag.ProductPhotos = await CatalogDataService.ListPhotosAsync(id);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveData(Product data, IFormFile? uploadPhoto)
        {
            ViewBag.Title = data.ProductID == 0 ? "Bổ sung mặt hàng" : "Cập nhật thông tin mặt hàng";
            try
            {
                string? oldPhotoToDelete = null;
                if (data.ProductID > 0)
                {
                    var oldData = await CatalogDataService.GetProductAsync(data.ProductID);
                    oldPhotoToDelete = oldData?.Photo;
                }

                // Giá: bind chuẩn có thể lỗi với dấu . nghìn — chỉ ghi đè khi form có chuỗi giá.
                ModelState.Remove(nameof(data.Price));
                var priceRaw = Request.Form["Price"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(priceRaw))
                {
                    if (!TryParseMoneyInput(priceRaw, out var priceParsed))
                        ModelState.AddModelError(nameof(data.Price), "Giá bán không hợp lệ");
                    else
                        data.Price = priceParsed;
                }

                if (string.IsNullOrWhiteSpace(data.ProductName))
                    ModelState.AddModelError(nameof(data.ProductName), "Vui lòng nhập tên mặt hàng");
                if (data.CategoryID is null or <= 0)
                    ModelState.AddModelError(nameof(data.CategoryID), "Vui lòng chọn loại hàng");
                if (data.SupplierID is null or <= 0)
                    ModelState.AddModelError(nameof(data.SupplierID), "Vui lòng chọn nhà cung cấp");
                if (string.IsNullOrWhiteSpace(data.Unit))
                    ModelState.AddModelError(nameof(data.Unit), "Vui lòng nhập đơn vị tính");
                if (data.Price < 0)
                    ModelState.AddModelError(nameof(data.Price), "Giá bán không hợp lệ");

                if (string.IsNullOrEmpty(data.ProductDescription))
                    data.ProductDescription = "";

                if (uploadPhoto != null && uploadPhoto.Length > 0)
                {
                    var uploadedFileName = await SaveProductImageAsync(uploadPhoto);
                    if (uploadedFileName == null)
                        ModelState.AddModelError(nameof(data.Photo), "File ảnh không hợp lệ hoặc không lưu được.");
                    else
                    {
                        data.Photo = uploadedFileName;
                    }
                }

                if (data.ProductID > 0 && string.IsNullOrWhiteSpace(data.Photo))
                {
                    var oldData = await CatalogDataService.GetProductAsync(data.ProductID);
                    data.Photo = oldData?.Photo;
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.ProductID = data.ProductID;
                    ViewBag.ProductAttributes = data.ProductID > 0 ? await CatalogDataService.ListAttributesAsync(data.ProductID) : new List<ProductAttribute>();
                    ViewBag.ProductPhotos = data.ProductID > 0 ? await CatalogDataService.ListPhotosAsync(data.ProductID) : new List<ProductPhoto>();
                    return View("Edit", data);
                }

                if (data.ProductID == 0)
                    await CatalogDataService.AddProductAsync(data);
                else
                    await CatalogDataService.UpdateProductAsync(data);

                if (!string.IsNullOrWhiteSpace(oldPhotoToDelete) &&
                    !string.Equals(oldPhotoToDelete, data.Photo, StringComparison.OrdinalIgnoreCase))
                {
                    await TryDeleteImageIfUnusedAsync(oldPhotoToDelete);
                }

                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError("Error", "Hệ thống đang bận, Vui lòng thử lại sau!");
                ViewBag.ProductID = data.ProductID;
                ViewBag.ProductAttributes = data.ProductID > 0 ? await CatalogDataService.ListAttributesAsync(data.ProductID) : new List<ProductAttribute>();
                ViewBag.ProductPhotos = data.ProductID > 0 ? await CatalogDataService.ListPhotosAsync(data.ProductID) : new List<ProductPhoto>();
                return View("Edit", data);
            }
        }
        /// <summary>
        /// Hiển thị danh sách các thuộc tính của mặt hàng
        /// </summary>
        /// <param name="id"> Mã mặt hàng cần lấy thuộc tính</param> 
        /// <returns></returns>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                if (!await CatalogDataService.DeleteProductAsync(id))
                    TempData["DeleteError"] = "Không xóa được mặt hàng (đang có trong đơn hàng hoặc dữ liệu liên quan).";
                return RedirectToAction("Index");
            }

            var model = await CatalogDataService.GetProductAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await CatalogDataService.IsUsedProductAsync(id));
            return View(model);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> ListAttributes(int id) 
        { 
            var data = await CatalogDataService.ListAttributesAsync(id);
            ViewBag.ProductID = id;
            return View(data); 
        }

        public IActionResult CreateAttribute(int id)
        {
            if (id <= 0)
            {
                TempData["Message"] = "Vui lòng lưu mặt hàng (có mã sản phẩm) trước khi thêm thuộc tính.";
                return RedirectToAction("Create");
            }
            var model = new ProductAttribute()
            {
                ProductID = id,
                DisplayOrder = 1
            };
            return View("EditAttribute", model);
        }   
        /// <summary>
        /// Cập nhật một thuốc tính cho mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng có thuộc tính cần cập nhật</param>
        /// <param name="attributeId">Mã thuộc tính cần cập nhật</param>
        /// 
        /// 
        /// 
        /// 
        /// <returns></returns>        
        public async Task<IActionResult> EditAttribute(int id, long attributeId)
        {
            var model = await CatalogDataService.GetAttributeAsync(attributeId);
            if (model == null || model.ProductID != id)
                return RedirectToAction("Edit", new { id });

            return View("EditAttribute", model); 
        }    

        [HttpPost]
        public async Task<IActionResult> SaveAttribute(ProductAttribute data)
        {
            if (string.IsNullOrWhiteSpace(data.AttributeName))
                ModelState.AddModelError(nameof(data.AttributeName), "Vui lòng nhập tên thuộc tính");
            if (string.IsNullOrWhiteSpace(data.AttributeValue))
                ModelState.AddModelError(nameof(data.AttributeValue), "Vui lòng nhập giá trị thuộc tính");
            if (data.DisplayOrder < 1)
                ModelState.AddModelError(nameof(data.DisplayOrder), "Thứ tự hiển thị phải lớn hơn 0");
            if (data.ProductID <= 0)
                ModelState.AddModelError(nameof(data.ProductID), "Mã mặt hàng không hợp lệ. Hãy lưu mặt hàng trước khi thêm thuộc tính.");
            else if (await CatalogDataService.GetProductAsync(data.ProductID) == null)
                ModelState.AddModelError(nameof(data.ProductID), "Mặt hàng không tồn tại trong CSDL.");

            if (!ModelState.IsValid)
                return View("EditAttribute", data);

            try
            {
                if (data.AttributeID == 0)
                    await CatalogDataService.AddAttributeAsync(data);
                else
                    await CatalogDataService.UpdateAttributeAsync(data);

                return RedirectToAction("Edit", new { id = data.ProductID });
            }
            catch (SqlException)
            {
                ModelState.AddModelError("Error", "Không lưu được thuộc tính (ràng buộc CSDL hoặc mặt hàng không tồn tại).");
                return View("EditAttribute", data);
            }
        }
        /// <summary>
        /// Xóa một thuộc tính của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng có thuộc tính cần xóa</param>
        /// <param name="attributeId">Mã thuộc tính muốn xóa</param>
        /// <returns></returns>        
        public async Task<IActionResult> DeleteAttribute(int id, long attributeId)
        {
            if (Request.Method == "POST")
            {
                await CatalogDataService.DeleteAttributeAsync(attributeId);
                return RedirectToAction("Edit", new { id });
            }

            var model = await CatalogDataService.GetAttributeAsync(attributeId);
            if (model == null || model.ProductID != id)
                return RedirectToAction("Edit", new { id });

            return View("DeleteListAttributes", model);
        }
           
        public async Task<IActionResult> ListPhotos(int id)
        {
            var data = await CatalogDataService.ListPhotosAsync(id);
            ViewBag.ProductID = id;
            return View(data);
        }
        /// <summary>
        /// Bổ sung ảnh của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng cần bổ sung</param>
        /// <returns></returns>
        public IActionResult CreatePhoto(int id)
        {
            if (id <= 0)
            {
                TempData["Message"] = "Vui lòng lưu mặt hàng (có mã sản phẩm) trước khi thêm ảnh.";
                return RedirectToAction("Create");
            }
            var model = new ProductPhoto()
            {
                ProductID = id,
                DisplayOrder = 1,
                Description = string.Empty
            };
            return View("EditPhoto", model); 
        }
        /// <summary>
        /// Cập nhật ảnh của mặt hàng
        /// </summary>
        /// <param name="id"> Mã mặt hàng có ảnh cần cập nhật</param>
        /// <returns></returns>
        public async Task<IActionResult> EditPhoto(int id, int photoId)
        {
            var model = await CatalogDataService.GetPhotoAsync(photoId);
            if (model == null || model.ProductID != id)
                return RedirectToAction("Edit", new { id });

            return View("EditPhoto", model);
        }

        [HttpPost]
        public async Task<IActionResult> SavePhoto(ProductPhoto data, IFormFile? uploadPhoto)
        {
            // Cột Description trong DB không cho NULL
            data.Description = string.IsNullOrWhiteSpace(data.Description) ? string.Empty : data.Description.Trim();
            string? oldPhotoToDelete = null;
            if (data.PhotoID > 0)
            {
                var oldPhoto = await CatalogDataService.GetPhotoAsync(data.PhotoID);
                oldPhotoToDelete = oldPhoto?.Photo;
            }

            if (uploadPhoto != null && uploadPhoto.Length > 0)
            {
                var uploadedFileName = await SaveProductImageAsync(uploadPhoto);
                if (uploadedFileName == null)
                    ModelState.AddModelError(nameof(data.Photo), "File ảnh không hợp lệ hoặc không lưu được.");
                else
                    data.Photo = uploadedFileName;
            }
            else if (string.IsNullOrWhiteSpace(data.Photo))
            {
                ModelState.AddModelError(nameof(data.Photo), "Vui lòng chọn file ảnh hoặc nhập tên file ảnh");
            }
            if (data.DisplayOrder < 1)
                ModelState.AddModelError(nameof(data.DisplayOrder), "Thứ tự hiển thị phải lớn hơn 0");
            if (data.ProductID <= 0)
                ModelState.AddModelError(nameof(data.ProductID), "Mã mặt hàng không hợp lệ. Hãy lưu mặt hàng trước khi thêm ảnh.");
            else if (await CatalogDataService.GetProductAsync(data.ProductID) == null)
                ModelState.AddModelError(nameof(data.ProductID), "Mặt hàng không tồn tại trong CSDL.");

            if (!ModelState.IsValid)
                return View("EditPhoto", data);

            try
            {
                if (data.PhotoID == 0)
                    await CatalogDataService.AddPhotoAsync(data);
                else
                    await CatalogDataService.UpdatePhotoAsync(data);

                if (!string.IsNullOrWhiteSpace(oldPhotoToDelete) &&
                    !string.Equals(oldPhotoToDelete, data.Photo, StringComparison.OrdinalIgnoreCase))
                {
                    await TryDeleteImageIfUnusedAsync(oldPhotoToDelete);
                }

                return RedirectToAction("Edit", new { id = data.ProductID });
            }
            catch (SqlException)
            {
                ModelState.AddModelError("Error", "Không lưu được ảnh (ràng buộc CSDL hoặc dữ liệu không hợp lệ).");
                return View("EditPhoto", data);
            }
        }
        /// <summary>
        /// Xóa một ảnh của mặt hàng
        /// </summary>
        /// <param name="id"> Mã mặt hàng có ảnh cần xóa</param>
        /// <param name="photoId"> Mã ảnh cần xóa</param>
        /// <returns></returns>
        public async Task<IActionResult> DeletePhoto(int id, int photoId)
        {
            if (Request.Method == "POST")
            {
                var oldPhoto = await CatalogDataService.GetPhotoAsync(photoId);
                await CatalogDataService.DeletePhotoAsync(photoId);
                if (!string.IsNullOrWhiteSpace(oldPhoto?.Photo))
                    await TryDeleteImageIfUnusedAsync(oldPhoto.Photo);
                return RedirectToAction("Edit", new { id });
            }

            var model = await CatalogDataService.GetPhotoAsync(photoId);
            if (model == null || model.ProductID != id)
                return RedirectToAction("Edit", new { id });

            return View(model);
        }

        /// <summary>
        /// Chuẩn hóa chuỗi giá từ form (AutoNumeric dùng . làm phân cách nghìn).
        /// </summary>
        private static bool TryParseMoneyInput(string? raw, out decimal price)
        {
            price = 0m;
            if (string.IsNullOrWhiteSpace(raw))
                return true;

            var s = raw.Trim().Replace(" ", "");
            if (s.Contains('.') && s.Contains(','))
                s = s.Replace(".", "").Replace(",", ".");
            else if (s.Contains('.') && !s.Contains(','))
                s = s.Replace(".", "");
            else if (s.Contains(',') && !s.Contains('.'))
                s = s.Replace(",", ".");

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
        }

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private static readonly Dictionary<string, HashSet<string>> AllowedMimeByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/pjpeg" },
            [".jpeg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/pjpeg" },
            [".png"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/png", "image/x-png" },
            [".gif"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/gif" },
            [".webp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/webp" }
        };

        private async Task<string?> SaveProductImageAsync(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return null;

            var ext = Path.GetExtension(file.FileName)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExtensions.Contains(ext))
                return null;
            if (!IsMimeAllowed(ext, file.ContentType))
                return null;
            if (!await HasValidImageSignatureAsync(file, ext))
                return null;

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var adminDir = Path.Combine(ApplicationContext.WWWRootPath, "images", "products");
            Directory.CreateDirectory(adminDir);

            var adminPath = Path.Combine(adminDir, fileName);
            await using (var stream = new FileStream(adminPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Dong bo qua Shop de customer site hien thi duoc ngay.
            try
            {
                var appRoot = ApplicationContext.ApplicationRootPath;
                var solutionRoot = Directory.GetParent(appRoot)?.FullName;
                if (!string.IsNullOrWhiteSpace(solutionRoot))
                {
                    var shopDir = Path.Combine(solutionRoot, "Lite.Shop", "wwwroot", "images", "products");
                    if (Directory.Exists(shopDir))
                    {
                        var shopPath = Path.Combine(shopDir, fileName);
                        System.IO.File.Copy(adminPath, shopPath, overwrite: true);
                    }
                }
            }
            catch
            {
                // Khong chan luu anh tai admin neu dong bo sang Shop that bai.
            }

            return fileName;
        }

        private static bool IsMimeAllowed(string ext, string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return false;
            if (!AllowedMimeByExtension.TryGetValue(ext, out var allowed))
                return false;
            return allowed.Contains(contentType.Trim());
        }

        private static async Task<bool> HasValidImageSignatureAsync(IFormFile file, string ext)
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[16];
            var read = await stream.ReadAsync(header, 0, header.Length);
            if (read < 12)
                return false;

            return ext switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                          header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
                ".gif" => header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
                          (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61,
                ".webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                           header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
                _ => false
            };
        }

        private async Task TryDeleteImageIfUnusedAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            if (string.Equals(fileName, "nophoto.png", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(fileName, "default-thumbnail-400.jpg", StringComparison.OrdinalIgnoreCase))
                return;

            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM Products WHERE LTRIM(RTRIM(ISNULL(Photo,''))) = @name)
  + (SELECT COUNT(*) FROM ProductPhotos WHERE LTRIM(RTRIM(ISNULL(Photo,''))) = @name);";

            await using var con = new SqlConnection(Configuration.ConnectionString);
            var refs = await con.ExecuteScalarAsync<int>(sql, new { name = fileName.Trim() });
            if (refs > 0)
                return;

            DeleteImageInBothSites(fileName);
        }

        private static void DeleteImageInBothSites(string fileName)
        {
            var adminPath = Path.Combine(ApplicationContext.WWWRootPath, "images", "products", fileName);
            if (System.IO.File.Exists(adminPath))
                System.IO.File.Delete(adminPath);

            var appRoot = ApplicationContext.ApplicationRootPath;
            var solutionRoot = Directory.GetParent(appRoot)?.FullName;
            if (string.IsNullOrWhiteSpace(solutionRoot))
                return;

            var shopPath = Path.Combine(solutionRoot, "Lite.Shop", "wwwroot", "images", "products", fileName);
            if (System.IO.File.Exists(shopPath))
                System.IO.File.Delete(shopPath);
        }
    }
}