using System.Security.Claims;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Ghi nhật ký bảo mật vào LichSuBaoMat (trang /NhatKyBaoMat, chỉ quyền All).
    /// - ChuaDangNhap / KhongDuQuyen: gọi từ sự kiện cookie OnRedirectToLogin / OnRedirectToAccessDenied (Program.cs),
    ///   nên bắt được mọi lần FallbackPolicy, [Authorize(Roles)] hay Forbid() chặn — không cần sửa từng action.
    /// - DuLieuNhayCam: attribute [GhiNhatKyBaoMat] trên action trả dữ liệu nhạy cảm, chỉ ghi khi trả thành công.
    /// Ghi bằng DbContext riêng (scope mới) để không lưu nhầm thay đổi đang dở của request; lỗi ghi log
    /// (bảng chưa tạo, mất kết nối) chỉ cảnh báo vào log app, không làm hỏng request.
    /// </summary>
    public static class NhatKyBaoMat
    {
        public const string ChuaDangNhap = "ChuaDangNhap";
        public const string KhongDuQuyen = "KhongDuQuyen";
        public const string DuLieuNhayCam = "DuLieuNhayCam";

        // Cùng sự kiện + người/IP + đường dẫn trong 30 giây chỉ ghi 1 dòng (tải lại trang, AJAX lặp)
        private static readonly TimeSpan KhoangGopTrung = TimeSpan.FromSeconds(30);
        // Máy quét từ Internet có thể bắn hàng nghìn URL: mỗi IP chưa đăng nhập tối đa 60 dòng / 10 phút
        private const int ToiDaMoiIpChuaDangNhap = 60;
        private static readonly TimeSpan CuaSoMoiIp = TimeSpan.FromMinutes(10);

        // Mở trang chủ khi chưa đăng nhập là chuyện bình thường hằng ngày, không phải sự kiện bảo mật
        private static readonly HashSet<string> DuongDanBoQua = new(StringComparer.OrdinalIgnoreCase)
        {
            "/", "/menuA", "/favicon.ico"
        };

        public static async Task GhiAsync(HttpContext http, string loai, string? moTa = null)
        {
            try
            {
                if (loai == ChuaDangNhap && DuongDanBoQua.Contains(http.Request.Path.Value ?? "/")) return;

                var ipObj = http.Connection.RemoteIpAddress;
                if (ipObj != null && ipObj.IsIPv4MappedToIPv6) ipObj = ipObj.MapToIPv4();
                var ip = ipObj?.ToString();
                int? idNguoiDung = int.TryParse(http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
                var duongDan = Cat(http.Request.Path.Value + http.Request.QueryString.Value, 500) ?? "/";

                var cache = http.RequestServices.GetService<IMemoryCache>();
                if (cache != null)
                {
                    var khoa = $"nkbm|{loai}|{idNguoiDung?.ToString() ?? ip}|{duongDan}";
                    if (cache.TryGetValue(khoa, out _)) return;
                    cache.Set(khoa, true, KhoangGopTrung);

                    if (loai == ChuaDangNhap)
                    {
                        var khoaIp = $"nkbm-ip|{ip}";
                        var dem = cache.GetOrCreate(khoaIp, e => { e.AbsoluteExpirationRelativeToNow = CuaSoMoiIp; return new int[1]; })!;
                        if (Interlocked.Increment(ref dem[0]) > ToiDaMoiIpChuaDangNhap) return;
                    }
                }

                using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                db.LichSuBaoMats.Add(new LichSuBaoMat
                {
                    ThoiGian = DateTime.Now,
                    Loai = loai,
                    IdNguoiDung = idNguoiDung,
                    TaiKhoan = Cat(http.User.FindFirst("MaNv")?.Value, 100),
                    HoTen = Cat(http.User.Identity?.IsAuthenticated == true ? http.User.Identity.Name : null, 150),
                    PhuongThuc = Cat(http.Request.Method, 10),
                    DuongDan = duongDan,
                    DiaChiIp = ip,
                    TrinhDuyet = Cat(http.Request.Headers.UserAgent.ToString(), 400),
                    MoTa = Cat(moTa, 200)
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                http.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger(typeof(NhatKyBaoMat))
                    .LogWarning(ex, "Không ghi được LichSuBaoMat ({Loai} {Path})", loai, http.Request.Path.Value);
            }
        }

        private static string? Cat(string? s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// Đánh dấu action trả dữ liệu nhạy cảm: ghi LichSuBaoMat loại DuLieuNhayCam khi trả thành công (2xx).
    /// Bị Forbid/Challenge thì không ghi ở đây — đã có dòng KhongDuQuyen/ChuaDangNhap từ sự kiện cookie.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class GhiNhatKyBaoMatAttribute : ActionFilterAttribute
    {
        public string MoTa { get; }

        public GhiNhatKyBaoMatAttribute(string moTa) => MoTa = moTa;

        public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            await next();
            if (context.Result is ForbidResult or ChallengeResult or UnauthorizedResult) return;
            var ma = context.HttpContext.Response.StatusCode;
            if (ma >= 200 && ma < 300)
                await NhatKyBaoMat.GhiAsync(context.HttpContext, NhatKyBaoMat.DuLieuNhayCam, MoTa);
        }
    }
}
