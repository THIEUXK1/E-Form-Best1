using System.Globalization;
using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền ghi lịch sử camera đổi trạng thái (Hoạt động ⇄ Mất kết nối) cho tab "Lịch sử" /QLCamera.
    ///
    /// Hệ thống ISAPI chỉ trả trạng thái HIỆN TẠI (kèm status_changed_at), không có nhật ký. Nên cứ mỗi
    /// chu kỳ (CameraIsapi:ChuKyLichSuGiay, mặc định 120 giây) đọc toàn bộ camera, so với trạng thái đã
    /// lưu ở KK_CameraTrangThai, khác thì ghi một dòng KK_CameraLichSu. Thời điểm sự kiện lấy từ
    /// status_changed_at nên vẫn đúng dù job chạy lệch nhịp.
    ///
    /// Giới hạn: camera chập chờn đổi rồi đổi lại trong cùng một chu kỳ poll của ISAPI (~5 phút) thì
    /// không thấy được. Lần chạy đầu chỉ ghi trạng thái nền, không sinh sự kiện.
    /// Idempotent: unique (nvr_ip, kenh, thoi_gian, sang_trang_thai) — chạy lại hay chạy song song
    /// trên 2 máy chủ cũng không nhân đôi sự kiện.
    /// </summary>
    public class CameraLichSuWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public CameraLichSuWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("CameraIsapi:GhiLichSu") ?? true)) return;

            var chuKyGiay = Math.Clamp(_configuration.GetValue<int?>("CameraIsapi:ChuKyLichSuGiay") ?? 120, 60, 3600);

            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await DoiChieuAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[CameraLichSu] Ghi {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Hệ thống ISAPI sập / mất mạng: bỏ lượt này, lượt sau chạy tiếp
                    Console.WriteLine($"[CameraLichSu Error]: {ex.Message}");
                }

                // PFVN/MEGA đọc file trạng thái riêng — lỗi bên này không ảnh hưởng BPVN và ngược lại
                foreach (var congTy in CameraXemTrucTiepService.DsCongTyAnhLuu)
                {
                    try
                    {
                        // Đầu ghi máy chủ gọi thẳng được (MEGA): tự làm mới file trạng thái trước khi đối chiếu
                        if (CameraXemTrucTiepService.CoDauGhiTrucTiepCongTy(_configuration, congTy))
                        {
                            using var scope = _scopeFactory.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<CameraXemTrucTiepService>()
                                .CapNhatTrangThaiTrucTiepAsync(congTy, stoppingToken);
                        }

                        var soSuKien = await DoiChieuCongTyAsync(congTy, stoppingToken);
                        if (soSuKien > 0)
                            Console.WriteLine($"[CameraLichSu {congTy}] Ghi {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                    }
                    catch (TaskCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CameraLichSu {congTy} Error]: {ex.Message}");
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(chuKyGiay), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Trạng thái hiện tại của 1 camera, gom từ hệ thống giám sát BPVN hoặc file trạng thái PFVN/MEGA.</summary>
        private record CameraHienTai(string NvrIp, int Kenh, string TrangThai, string? Ten, string? Ip,
            string? TenDauGhi, string? KhuVuc, DateTime? DoiLuc);

        private async Task<int> DoiChieuAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
            var giamSat = scope.ServiceProvider.GetRequiredService<CameraGiamSatService>();

            // Gồm cả camera đã loại trừ: loại trừ chỉ là ẩn cảnh báo, lịch sử vẫn nên có
            var duLieu = await giamSat.DanhSachCameraAsync(null, null, null, true, false, ct);
            if (!duLieu.TryGetProperty("cameras", out var cameras) || cameras.ValueKind != JsonValueKind.Array)
                return 0;

            var ds = new List<CameraHienTai>();
            foreach (var c in cameras.EnumerateArray())
            {
                var nvrIp = Chuoi(c, "nvr_ip");
                var trangThai = Chuoi(c, "status");
                if (nvrIp == null || trangThai == null || !int.TryParse(Chuoi(c, "cam_id"), out var kenh)) continue;
                ds.Add(new CameraHienTai(nvrIp, kenh, trangThai, Chuoi(c, "name"), Chuoi(c, "ip"),
                    Chuoi(c, "nvr_name"), Chuoi(c, "zone"), Ngay(Chuoi(c, "status_changed_at"))));
            }
            return await GhiThayDoiAsync(db, ds, ct);
        }

        /// <summary>
        /// PFVN/MEGA: không có hệ thống giám sát, trạng thái là file "_trang-thai.json" máy dev đẩy lên 5 phút/lần
        /// (tools/dong-bo-trang-thai-pfvn.ps1). Thời điểm đổi = giờ của file đó, giống nhau trên 2 máy chủ nên
        /// unique (nvr_ip, kenh, thoi_gian, sang_trang_thai) vẫn chặn được ghi đôi.
        /// </summary>
        private async Task<int> DoiChieuCongTyAsync(string congTy, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
            var xem = scope.ServiceProvider.GetRequiredService<CameraXemTrucTiepService>();

            var (capNhat, dsKenh) = await xem.TrangThaiCongTyAsync(congTy, ct);
            if (capNhat == null || dsKenh.Count == 0) return 0;

            var ds = dsKenh.Select(k => new CameraHienTai(k.Nvr, k.Kenh, k.Online ? "UP" : "DOWN", k.Ten, k.IpCamera,
                CameraXemTrucTiepService.TenDauGhiCongTy(congTy, k.Nvr, k.TenDauGhi), congTy, capNhat)).ToList();
            return await GhiThayDoiAsync(db, ds, ct);
        }

        /// <summary>So trạng thái hiện tại với KK_CameraTrangThai, khác thì ghi 1 dòng KK_CameraLichSu.</summary>
        private static async Task<int> GhiThayDoiAsync(ITFormContext db, List<CameraHienTai> ds, CancellationToken ct)
        {
            var daLuu = (await db.KkCameraTrangThais.ToListAsync(ct))
                .ToDictionary(x => (x.NvrIp, x.Kenh));

            var suKienMoi = new List<KkCameraLichSu>();
            var bayGio = DateTime.Now;
            // Khoá của ISAPI là nvr|kênh|IP nên một kênh có thể xuất hiện 2 lần (đổi IP camera);
            // chỉ lấy lần đầu, không thì 2 bản ghi khác trạng thái sẽ sinh sự kiện đảo qua đảo lại mỗi lượt
            var daXet = new HashSet<(string, int)>();

            foreach (var c in ds)
            {
                var nvrIp = c.NvrIp;
                var kenh = c.Kenh;
                var trangThai = c.TrangThai;
                if (!daXet.Add((nvrIp, kenh))) continue;

                var ten = Cat(c.Ten, 255);
                var ip = Cat(c.Ip, 50);
                var doiLuc = c.DoiLuc;

                if (!daLuu.TryGetValue((nvrIp, kenh), out var cu))
                {
                    // Lần đầu thấy kênh này: chỉ ghi trạng thái nền, không có "đổi" nào để báo
                    var moi = new KkCameraTrangThai
                    {
                        NvrIp = nvrIp, Kenh = kenh, TrangThai = Cat(trangThai, 20)!, TenCamera = ten,
                        IpCamera = ip, DoiLuc = doiLuc, CapNhatLuc = bayGio
                    };
                    db.KkCameraTrangThais.Add(moi);
                    daLuu[(nvrIp, kenh)] = moi;
                    continue;
                }

                if (!string.Equals(cu.TrangThai, trangThai, StringComparison.OrdinalIgnoreCase))
                {
                    var thoiGian = doiLuc ?? bayGio;
                    int? thoiLuong = null;
                    // Hoạt động lại: tính đã mất kết nối bao lâu từ lúc chuyển sang DOWN đã lưu
                    if (cu.TrangThai == "DOWN" && cu.DoiLuc.HasValue && thoiGian > cu.DoiLuc.Value)
                        thoiLuong = (int)Math.Min(int.MaxValue, (thoiGian - cu.DoiLuc.Value).TotalSeconds);

                    suKienMoi.Add(new KkCameraLichSu
                    {
                        ThoiGian = thoiGian,
                        NvrIp = nvrIp,
                        TenDauGhi = Cat(c.TenDauGhi, 255),
                        KhuVuc = Cat(c.KhuVuc, 100),
                        Kenh = kenh,
                        TenCamera = ten,
                        IpCamera = ip,
                        TuTrangThai = cu.TrangThai,
                        SangTrangThai = Cat(trangThai, 20)!,
                        ThoiLuongGiay = thoiLuong,
                        GhiNhanLuc = bayGio
                    });

                    cu.TrangThai = Cat(trangThai, 20)!;
                    cu.DoiLuc = thoiGian;
                }

                cu.TenCamera = ten;
                cu.IpCamera = ip;
                cu.CapNhatLuc = bayGio;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (LaTrungKhoa(ex))
            {
                // Máy chủ kia vừa ghi nền trước: lượt sau đọc lại là khớp
                return 0;
            }

            // Ghi từng sự kiện: trùng (máy chủ kia đã ghi) thì bỏ qua đúng dòng đó, không mất cả lô
            var daGhi = 0;
            foreach (var sk in suKienMoi)
            {
                db.KkCameraLichSus.Add(sk);
                try
                {
                    await db.SaveChangesAsync(ct);
                    daGhi++;
                }
                catch (DbUpdateException ex) when (LaTrungKhoa(ex))
                {
                    db.Entry(sk).State = EntityState.Detached;
                }
            }
            return daGhi;
        }

        private static bool LaTrungKhoa(DbUpdateException ex)
            => ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

        private static string? Chuoi(JsonElement e, string ten)
        {
            if (!e.TryGetProperty(ten, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };
        }

        // ISAPI trả giờ địa phương không múi giờ ("2026-10-06T13:59:11")
        private static DateTime? Ngay(string? s)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static string? Cat(string? s, int toiDa)
            => s == null ? null : (s.Length > toiDa ? s[..toiDa] : s);
    }
}
