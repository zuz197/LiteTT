using Lite.BusinessLayers;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lite.Shop.AppCodes
{
    /// <summary>
    /// Lớp cung cấp các hàm tiện ích dùng cho SelectList (DropDownList)
    /// </summary>
    public static class SelectListHelper
    {
        /// <summary>
        /// Tỉnh thành
        /// </summary>
        /// <returns></returns>
        public static async Task<List<SelectListItem>> Provinces()
        {
            var list = new List<SelectListItem>()
            {
                new SelectListItem() { Value = "", Text = "-- Tỉnh/Thành phố --"}
            };
            var result = await DictionaryDataService.ListProvincesAsync();
            foreach (var item in result)
            {
                list.Add(new SelectListItem()
                {
                    Value = item.ProvinceName,
                    Text = item.ProvinceName
                });
            }
            return list;
        }
    }
}