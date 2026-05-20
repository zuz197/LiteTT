using Microsoft.AspNetCore.Authentication.Cookies; 
using Lite.Admin;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("LiteCommerceDB")
    ?? throw new InvalidOperationException("ConnectionString 'LiteCommerceDB' not found.");

Lite.BusinessLayers.Configuration.Initialize(connectionString);

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews()
                .AddMvcOptions(option =>
                {
                    option.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(option =>
                {
                    option.Cookie.Name = "LiteCommerce.Admin";
                    option.LoginPath = "/Account/Login";
                    option.AccessDeniedPath = "/Account/AccessDenied";
                    option.ExpireTimeSpan = TimeSpan.FromDays(7);
                    option.SlidingExpiration = true;
                    option.Cookie.HttpOnly = true;
                    option.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                });

builder.Services.AddSession(option =>
{
    option.IdleTimeout = TimeSpan.FromHours(2);
    option.Cookie.HttpOnly = true;
    option.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

var shopImagesPath = Path.Combine(builder.Environment.ContentRootPath, 
    "..", "Lite.Shop", "wwwroot", "images", "returns");
if (Directory.Exists(shopImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(shopImagesPath),
        RequestPath = "/images/returns"
    });
}

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

var cultureInfo = new CultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

ApplicationContext.Configure(
    httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>(),
    webHostEnvironment: app.Services.GetRequiredService<IWebHostEnvironment>(),
    configuration: app.Configuration
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();