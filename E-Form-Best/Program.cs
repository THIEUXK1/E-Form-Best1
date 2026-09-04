using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebPush;
using E_Form_Best.Context; // Đảm bảo namespace này đúng
using E_Form_Best.Areas.AdminForm.Controllers; // Nhận diện PushNotificationService
using E_Form_Best.Areas.ITForm.Services; // Thêm namespace của Worker mới
using Microsoft.AspNetCore.Authentication; // Thêm để dùng SignOutAsync
using System.Security.Claims; // Thêm để làm việc với Claims
using Microsoft.AspNetCore.HttpOverrides; // Đọc header X-Forwarded-* do nginx gửi sang
using Microsoft.AspNetCore.RateLimiting; // Giới hạn tần suất request cho trang đăng nhập
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- 0. CẤU HÌNH ĐỌC HEADER TỪ REVERSE PROXY (nginx) ---
// Không có phần này, ứng dụng tưởng request đến bằng http nên sinh redirect về http://,
// khiến trình duyệt phải đi thêm một vòng 301 nữa mới quay lại https. Đồng thời client IP
// ghi trong log sẽ là IP của nginx thay vì IP thật của người dùng.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    // nginx nằm trên máy khác nên không thuộc danh sách proxy tin cậy mặc định (loopback).
    // Xoá danh sách này để chấp nhận header từ nginx; an toàn vì Kestrel/IIS chỉ nhận
    // kết nối từ nginx, không mở trực tiếp ra Internet.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- 1. ĐĂNG KÝ DATABASE CONTEXT ---
builder.Services.AddDbContext<ITFormContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. ĐĂNG KÝ BACKGROUND SERVICE (CHẠY NGẦM LÚC 12H ĐÊM) ---
builder.Services.AddHostedService<AutoRatingWorker>();

// Cache trong bộ nhớ cho dữ liệu tra cứu ít thay đổi (Công ty, Bộ phận...) để giảm truy vấn DB lặp lại
builder.Services.AddMemoryCache();

// 3. Thêm dịch vụ MVC (Controllers + Views)
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    // Cho phép sửa file .cshtml và thấy thay đổi ngay khi F5 lại trang (không cần build lại project)
    mvcBuilder.AddRazorRuntimeCompilation();
}

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
                // Kiểm tra an toàn xem Principal có tồn tại không
                if (context.Principal == null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                // 1. Lấy UserId và SecurityStamp từ Cookie hiện tại (Dùng toán tử ?. để dập tắt cảnh báo null)
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

                // 4. So sánh: Nếu Stamp thay đổi (do Admin reset hoặc đổi mật khẩu) -> Đuổi người dùng ra
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

// 5b. GIỚI HẠN TẦN SUẤT ĐĂNG NHẬP THEO IP
// Chốt khoá tài khoản trong ITFormController đếm theo từng user nên không cản được kiểu quét
// "một mật khẩu thử cho hàng nghìn tài khoản" (password spraying) — mỗi tài khoản chỉ sai 1 lần,
// không tài khoản nào chạm ngưỡng 5. Điểm chung của kiểu quét đó là cùng một IP, nên chặn theo IP.
// Bộ đếm phải PHÂN VÙNG theo IP; nếu dùng limiter chung, một kẻ quét sẽ khoá đăng nhập cả công ty.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("dang-nhap", httpContext =>
    {
        // UseForwardedHeaders chạy trước nên đây là IP thật của người dùng, không phải IP nginx.
        // Không lấy được IP (trường hợp hiếm) thì gom chung vào một vùng "khong-ro" để vẫn bị giới hạn.
        string khoaIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "khong-ro";

        return RateLimitPartition.GetFixedWindowLimiter(khoaIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,                   // 10 lần POST đăng nhập
            Window = TimeSpan.FromMinutes(1),   // trong mỗi 1 phút
            QueueLimit = 0                      // vượt là từ chối ngay, không xếp hàng chờ
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Trả kèm Retry-After để client biết chờ bao lâu, và một câu tiếng Việt cho người dùng thật
    // lỡ bị dính (thay vì trang lỗi trắng khó hiểu).
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng chờ khoảng 1 phút rồi thử lại.",
            cancellationToken);
    };
});

var app = builder.Build();

// Phải đứng trước mọi middleware khác để các middleware sau đó (nhất là HttpsRedirection
// và Cookie Authentication) nhìn thấy đúng scheme https và đúng IP của người dùng.
app.UseForwardedHeaders();

// Cấu hình Pipeline xử lý Request
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    // Cho phép trình duyệt cache lib/css/js 7 ngày, giảm tải lại các file tĩnh không đổi mỗi lần chuyển trang.
    // Ngoại trừ sw.js: service worker cần luôn được trình duyệt kiểm tra lại để nhận bản cập nhật kịp thời.
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        ctx.Context.Response.Headers.CacheControl = path.Equals("sw.js", StringComparison.OrdinalIgnoreCase)
            ? "no-cache"
            : "public,max-age=604800";
    }
}); // Quan trọng: Để truy cập sw.js và icon thông báo

app.UseRouting();

// Phải đứng sau UseRouting thì middleware mới biết request rơi vào endpoint nào để áp đúng
// policy [EnableRateLimiting]. Đặt trước Authentication để chặn ngay, không tốn truy vấn DB.
app.UseRateLimiter();

// 6. THỨ TỰ MIDDLEWARE
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// 7. CẤU HÌNH ROUTES

// ƯU TIÊN 0: Endpoint cho công cụ giám sát uptime (không cần đăng nhập, không redirect)
// /health       : ứng dụng còn sống hay không - trả lời ngay, không chạm database.
// /health/ready : kiểm tra luôn kết nối SQL Server, dùng cho uptime check để biết
//                 hệ thống thật sự dùng được, thay vì chỉ biết web server còn chạy.
app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapGet("/health/ready", async (ITFormContext db) =>
{
    try
    {
        return await db.Database.CanConnectAsync()
            ? Results.Ok("Healthy")
            : Results.Text("Unhealthy: khong ket noi duoc database", statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Text($"Unhealthy: {ex.Message}", statusCode: 503);
    }
});

// ƯU TIÊN 1: Trang chủ "/" được xử lý trực tiếp bởi action MenuA (xem [HttpGet("/")]
// trong AdminFormController) thay vì trả 302 sang /MenuA, bớt được một vòng round-trip
// cho mọi người dùng khi mở trang.

// ƯU TIÊN 2: Khai báo Route của Area (Phải đặt lên trước các route thông thường)
app.MapControllerRoute(
    name: "area",
    pattern: "{area:exists}/{controller=DangNhap}/{action=Index}/{id?}"
);

// ƯU TIÊN 3: Route mặc định của ứng dụng
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// ƯU TIÊN KÈM THEO: Route tùy chỉnh của bạn (đặt xuống dưới cùng để tránh bắt nhầm các route chuẩn của Area)
app.MapControllerRoute(
    name: "homeActions",
    pattern: "{action}/{id?}",
    defaults: new { controller = "Home", action = "Index" }
);

app.Run();