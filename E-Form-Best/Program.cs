using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebPush;
using E_Form_Best.Context; // Hãy đảm bảo namespace này đúng với file ITFormContext.cs của bạn
using E_Form_Best.Areas.AdminForm.Controllers; // Để nhận diện PushNotificationService

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ DATABASE CONTEXT (SỬA LỖI BẠN ĐANG GẶP) ---
builder.Services.AddDbContext<ITFormContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. ĐĂNG KÝ CÁC DỊCH VỤ THÔNG BÁO ---
// Đăng ký Service gửi Push để có thể gọi từ bất kỳ Controller nào
builder.Services.AddScoped<PushNotificationService>();

// Lấy cấu hình VAPID từ appsettings.json
builder.Services.Configure<VapidDetails>(builder.Configuration.GetSection("VapidDetails"));

// 3. Thêm dịch vụ MVC (Controllers + Views)
builder.Services.AddControllersWithViews();

// 4. CẤU HÌNH COOKIE AUTHENTICATION
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ITForm_Auth_Cookie";
        options.LoginPath = "/DonXetDuyet/DangNhap";
        options.LogoutPath = "/DonXetDuyet/DangXuat";
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

// 5. Cấu hình Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(3);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Cấu hình Pipeline xử lý Request
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Quan trọng: Để truy cập sw.js và icon thông báo
app.UseRouting();

// 6. THỨ TỰ MIDDLEWARE (QUYẾT ĐỊNH VIỆC ĐĂNG NHẬP CÓ CHẠY KHÔNG)
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// 7. CẤU HÌNH ROUTES
// ƯU TIÊN 1: Map trực tiếp trang chủ vào trang MenuA
app.MapGet("/", context => {
    context.Response.Redirect("/MenuA");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "area",
    pattern: "{area:exists}/{controller=DangNhap}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "homeActions",
    pattern: "{action}/{id?}",
    defaults: new { controller = "Home", action = "Index" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();