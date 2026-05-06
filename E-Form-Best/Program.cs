using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebPush;
using E_Form_Best.Context; // Đảm bảo namespace này đúng
using E_Form_Best.Areas.AdminForm.Controllers; // Nhận diện PushNotificationService
using E_Form_Best.Areas.ITForm.Services; // Thêm namespace của Worker mới
using Microsoft.AspNetCore.Authentication; // Thêm để dùng SignOutAsync
using System.Security.Claims; // Thêm để làm việc với Claims

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ DATABASE CONTEXT ---
builder.Services.AddDbContext<ITFormContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. ĐĂNG KÝ BACKGROUND SERVICE (CHẠY NGẦM LÚC 12H ĐÊM) ---
builder.Services.AddHostedService<AutoRatingWorker>();

// 3. Thêm dịch vụ MVC (Controllers + Views)
builder.Services.AddControllersWithViews();

// 4. CẤU HÌNH COOKIE AUTHENTICATION (Đã thêm logic kiểm tra SecurityStamp)
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

        // --- MỚI: LOGIC KIỂM TRA ĐĂNG XUẤT TOÀN BỘ ---
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // 1. Lấy UserId và SecurityStamp từ Cookie hiện tại
                var userId = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var stampInCookie = context.Principal.FindFirst("SecurityStamp")?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stampInCookie))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                // 2. Lấy Service Database để truy vấn
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<ITFormContext>();

                // 3. Kiểm tra SecurityStamp trong Database
                // Giả sử bảng User của bạn có khóa chính là id_nguoi_dung (int)
                var currentStampInDb = await dbContext.Users
                    .Where(u => u.IdNguoiDung.ToString() == userId)
                    .Select(u => u.SecurityStamp)
                    .FirstOrDefaultAsync();

                // 4. So sánh: Nếu Stamp thay đổi (do Admin reset) -> Đuổi người dùng ra
                if (currentStampInDb == null || currentStampInDb != stampInCookie)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
        };
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

// 6. THỨ TỰ MIDDLEWARE
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