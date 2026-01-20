using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm dịch vụ MVC (Controllers + Views)
builder.Services.AddControllersWithViews();

// 2. CẤU HÌNH COOKIE AUTHENTICATION (QUAN TRỌNG ĐỂ SỬ DỤNG CK)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ITForm_Auth_Cookie";    // Tên file Cookie lưu trên Laptop
        options.LoginPath = "/DonXetDuyet/DangNhap";    // Đường dẫn nếu chưa đăng nhập
        options.LogoutPath = "/DonXetDuyet/DangXuat";
        options.ExpireTimeSpan = TimeSpan.FromDays(365); // Hạn dùng 1 năm cho đăng nhập vĩnh viễn
        options.SlidingExpiration = true;               // Tự động gia hạn khi user còn dùng
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

// 3. Cấu hình Session (Giữ lại nếu bạn vẫn dùng cho các biến tạm khác)
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
app.UseStaticFiles();
app.UseRouting();

// 4. THỨ TỰ MIDDLEWARE (RẤT QUAN TRỌNG)
app.UseAuthentication(); // Bước 1: Xác thực xem bạn là ai (Phải đứng trước Authorization)
app.UseAuthorization();  // Bước 2: Kiểm tra bạn có quyền vào trang đó không
app.UseSession();        // Bước 3: Sử dụng Session

// 5. CẤU HÌNH ROUTES
// ƯU TIÊN 1: Map trực tiếp trang chủ vào Action [HttpGet("/MenuA")]
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

// Route mặc định để hỗ trợ các Controller/Action khác
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();