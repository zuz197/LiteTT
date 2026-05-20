using Microsoft.AspNetCore.Mvc;
using Lite.Shop.DAL;

namespace Lite.Shop.Controllers
{
    public class ReturnController : Controller
    {
        private readonly ReturnRequestDAL returnDAL;
        private readonly IWebHostEnvironment env;

        public ReturnController(ReturnRequestDAL returnDAL, IWebHostEnvironment env)
        {
            this.returnDAL = returnDAL;
            this.env = env;
        }

        [HttpPost]
        public async Task<IActionResult> Submit(
            int orderId,
            string type,
            string reason,
            List<IFormFile> photos)
        {
            var idStr = HttpContext.Session.GetString("CustomerID");
            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Login", "Account");

            // Lưu ảnh vào wwwroot/images/returns/
            var savedPaths = new List<string>();
            if (photos != null && photos.Count > 0)
            {
                var folder = Path.Combine(env.WebRootPath, "images", "returns");
                Directory.CreateDirectory(folder);

                foreach (var photo in photos)
                {
                    if (photo.Length > 0)
                    {
                        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
                        var filePath = Path.Combine(folder, fileName);
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await photo.CopyToAsync(stream);
                        savedPaths.Add(fileName);
                    }
                }
            }

            returnDAL.Create(orderId, type, reason, savedPaths);

            return RedirectToAction("Details", "Order", new { id = orderId });
        }
    }
}