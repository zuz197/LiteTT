using Microsoft.AspNetCore.Authentication.Cookies;
using Lite.Shop.DAL;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// Khởi tạo cấu hình cho tầng Business (cần thiết để dùng DictionaryDataService, v.v.)
string connectionString = builder.Configuration.GetConnectionString("LiteCommerceDB")
    ?? throw new InvalidOperationException("ConnectionString 'LiteCommerceDB' not found.");
Lite.BusinessLayers.Configuration.Initialize(connectionString);

// DAL
builder.Services.AddScoped<CustomerDAL>();
builder.Services.AddScoped<ProductDAL>();
builder.Services.AddScoped<CartDAL>();
builder.Services.AddScoped<OrderDAL>();
builder.Services.AddScoped<ReturnRequestDAL>();

// cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(option =>
    {
        option.Cookie.Name = "LiteCommerce.Shop";
        option.LoginPath = "/Account/Login";
        option.AccessDeniedPath = "/Account/AccessDenied";
        option.ExpireTimeSpan = TimeSpan.FromDays(7);
        option.SlidingExpiration = true;
        option.Cookie.HttpOnly = true;
    });

// Session (giữ lại nếu cần captcha)
builder.Services.AddSession();

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseRouting();

app.UseSession(); // dùng cho captcha

// chú ý
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Product}/{action=Index}/{id?}");

app.Run();