using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Bản sao dữ liệu đóng gói NH-MES trong DB (QA_MesDongGoi): tra cứu đọc DB cho nhanh và không phụ thuộc MES/OA;
    /// MesDongBoWorker đổ dữ liệu theo từng ngày đóng gói, nút "Cập nhật từ MES" kéo lại riêng 1 mã phối màu.
    /// Khoá dòng = dấu vân tay (SHA-256 JSON gốc) giống QA_ThungDong, nên cột "Thùng" vẫn khớp dù dữ liệu lấy từ DB.
    /// </summary>
    public class MesKhoService
    {
        public record KetQuaKho(MesApiService.KetQuaTraCuu Kq, string Nguon, DateTime? CapNhatLuc);

        // Bộ lọc trang tra cứu: các ô để trống = không lọc; lọc chứa chuỗi, các ô kết hợp AND
        public record LocTraCuu(string Ma, string Kh, string Hang)
        {
            public bool CoLocPhu => Kh.Length > 0 || Hang.Length > 0;
            public bool KhopPhu(MesApiService.DongDongGoi d)
                => (Kh.Length == 0 || (d.CustCode ?? "").Contains(Kh, StringComparison.OrdinalIgnoreCase))
                && (Hang.Length == 0 || (d.ArtCode ?? "").Contains(Hang, StringComparison.OrdinalIgnoreCase));
        }

        private readonly ITFormContext _db;
        private readonly MesApiService _mes;
        private readonly ILogger<MesKhoService> _logger;

        public MesKhoService(ITFormContext db, MesApiService mes, ILogger<MesKhoService> logger)
        {
            _db = db;
            _mes = mes;
            _logger = logger;
        }

        /// <summary>
        /// Tra từ DB; mã chưa có trong DB (hoặc chưa tạo bảng) thì gọi MES trực tiếp và lưu lại.
        /// Mã rỗng = toàn bộ bản sao DB, dòng đóng gói mới nhất trước (không gọi MES vì MES không cho kéo cả kho).
        /// </summary>
        public Task<KetQuaKho> TraCuuAsync(string maPhoiMau, CancellationToken ct)
            => TraCuuAsync(new LocTraCuu(maPhoiMau, "", ""), ct);

        /// <summary>
        /// Như trên với thêm lọc mã khách hàng / mã hàng. Không có mã phối màu thì chỉ đọc DB
        /// (MES chỉ cho tra theo mã phối màu).
        /// </summary>
        public async Task<KetQuaKho> TraCuuAsync(LocTraCuu loc, CancellationToken ct)
        {
            var rong = new KetQuaKho(new(true, "", 0, new()), "DB", null);
            if (loc.Ma.Length == 0)
                return await DocDbAsync(loc, ct) ?? rong;
            try
            {
                var tuDb = await DocDbAsync(loc, ct);
                if (tuDb != null) return tuDb;
                // Mã phối màu đã có trong DB, chỉ là lọc phụ không khớp -> trả rỗng, không gọi MES vô ích
                if (loc.CoLocPhu && await LocMa(_db.QaMesDongGois.Where(x => x.NgayMat == null), loc.Ma).AnyAsync(ct))
                    return rong;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Chưa chạy script tạo bảng / DB lỗi: vẫn cho tra thẳng MES
                _logger.LogError(ex, "Không đọc được QA_MesDongGoi, chuyển sang gọi MES trực tiếp");
                var kqMes = LocPhu(await _mes.TraTatCaTheoPhoiMauAsync(loc.Ma, ct), loc);
                return new(kqMes, "MES", kqMes.ThanhCong ? DateTime.Now : null);
            }
            return await LamMoiTheoPhoiMauAsync(loc, ct);
        }

        /// <summary>
        /// Lọc mã phối màu. Mã MES luôn dạng "P" + 13 số (chữ P chỉ đứng đầu) nên chuỗi gõ bắt đầu bằng P mà nằm trong mã
        /// thì chắc chắn ở đầu mã -> dùng LIKE 'x%' (seek index IX_QA_MesDongGoi_PhoiMau, ~0 ms) thay LIKE '%x%' (quét ~500 ms),
        /// kết quả y hệt. Gõ đoạn giữa/cuối (vd "0294022") vẫn dò "chứa" như cũ.
        /// </summary>
        private static IQueryable<QaMesDongGoi> LocMa(IQueryable<QaMesDongGoi> q, string ma)
            => ma.StartsWith('P') || ma.StartsWith('p')
                ? q.Where(x => x.MaPhoiMau!.StartsWith(ma))
                : q.Where(x => x.MaPhoiMau!.Contains(ma));

        private static MesApiService.KetQuaTraCuu LocPhu(MesApiService.KetQuaTraCuu kq, LocTraCuu loc)
        {
            if (!kq.ThanhCong || !loc.CoLocPhu) return kq;
            var dong = kq.Dong!.Where(loc.KhopPhu).ToList();
            return kq with { Tong = dong.Count, Dong = dong };
        }

        // Số gợi ý tối đa mỗi lần (datalist): gõ thêm ký tự thì danh sách thu hẹp dần nên vẫn tới được mọi mã
        private const int SoGoiY = 200;

        // Cột được gợi ý khi gõ: ma = mã phối màu, kh = mã khách hàng, hang = mã hàng
        public async Task<List<string>> GoiYAsync(string truong, string q, LocTraCuu loc, CancellationToken ct)
        {
            var nguon = _db.QaMesDongGois.AsNoTracking().Where(x => x.NgayMat == null);
            // Gợi ý theo các ô còn lại đang nhập (vd chọn khách hàng rồi thì chỉ gợi ý mã hàng của khách đó)
            if (truong != "ma" && loc.Ma.Length > 0) nguon = LocMa(nguon, loc.Ma);
            if (truong != "kh" && loc.Kh.Length > 0) nguon = nguon.Where(x => x.MaKhachHang!.Contains(loc.Kh));
            if (truong != "hang" && loc.Hang.Length > 0) nguon = nguon.Where(x => x.MaHang!.Contains(loc.Hang));
            if (truong == "ma")
            {
                // Mã phối màu chứa ngày (P + yyMMdd...) -> mới nhất trước; gõ từ chữ P (hoặc chưa gõ) thì seek/quét ngược index
                var ma = nguon.Where(x => x.MaPhoiMau != null && x.MaPhoiMau != "");
                ma = q.Length == 0 ? ma : LocMa(ma, q);
                return (await ma.Select(x => x.MaPhoiMau).Distinct()
                    .OrderByDescending(v => v).Take(SoGoiY).ToListAsync(ct))!;
            }
            IQueryable<string?> cot = truong == "kh" ? nguon.Select(x => x.MaKhachHang) : nguon.Select(x => x.MaHang);
            return (await cot.Where(v => v != null && v != "" && v.Contains(q))
                .Distinct().OrderBy(v => v).Take(SoGoiY).ToListAsync(ct))!;
        }

        /// <summary>Dòng còn hiệu lực trong DB theo dấu vân tay (bỏ vào thùng khi đang xem kết quả tra trống / nhiều mã).</summary>
        public async Task<List<MesApiService.DongDongGoi>> TimTheoVanTayAsync(List<string> dsVanTay, CancellationToken ct)
        {
            var ds = await _db.QaMesDongGois.AsNoTracking()
                .Where(x => dsVanTay.Contains(x.DauVanTay))
                .Select(x => new { x.DauVanTay, x.SoDong, x.DuLieuMes })
                .ToListAsync(ct);
            var kq = new List<MesApiService.DongDongGoi>();
            foreach (var x in ds)
            {
                var d = JsonSerializer.Deserialize<MesApiService.DongDongGoi>(x.DuLieuMes, MesApiService.JsonOpt)!
                    with { Raw = x.DuLieuMes, DauVanTay = x.DauVanTay };
                kq.Add(d);
                for (int i = 1; i < x.SoDong; i++) kq.Add(d with { });
            }
            return kq;
        }

        /// <summary>Kéo lại 1 mã phối màu từ MES, cập nhật DB, trả dữ liệu MES vừa lấy.</summary>
        public async Task<KetQuaKho> LamMoiTheoPhoiMauAsync(LocTraCuu loc, CancellationToken ct)
        {
            var maPhoiMau = loc.Ma;
            var kq = await _mes.TraTatCaTheoPhoiMauAsync(maPhoiMau, ct);
            if (!kq.ThanhCong) return new(kq, "MES", null);
            try
            {
                // Chỉ đánh dấu "mất" trong đúng các mã MES vừa trả và khi đã lấy đủ — MES so khớp gần đúng,
                // tự suy phạm vi LIKE ở DB có thể lệch cách MES so khớp.
                var dsMa = kq.Dong!.Select(d => d.OrderUcode).Where(m => m != null).Distinct().ToList();
                var dayDu = kq.Dong!.Count >= kq.Tong;
                await LuuAsync(kq.Dong!, dayDu && dsMa.Count > 0 ? q => q.Where(x => dsMa.Contains(x.MaPhoiMau)) : null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Không lưu được dữ liệu MES mã {Ma} vào QA_MesDongGoi", maPhoiMau);
            }
            // Lưu đủ cả mã, trả về chỉ phần khớp các ô lọc phụ
            return new(LocPhu(kq, loc), "MES", DateTime.Now);
        }

        /// <summary>
        /// Nút "Đồng bộ MES" (ô mã trống): kéo lại soNgay ngày đóng gói gần nhất, mới nhất trước.
        /// Ngày vừa được đồng bộ trong 2 phút (bấm lặp / job vừa chạy) thì bỏ qua để không dội MES.
        /// Lỗi MES ở 1 ngày thì dừng, trả số đã làm + lỗi.
        /// </summary>
        public async Task<(int soNgay, int soDong, string? loi)> DongBoGanDayAsync(int soNgay, CancellationToken ct)
        {
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var tu = homNay.AddDays(-(soNgay - 1));
            var moiDay = DateTime.Now.AddMinutes(-2);
            var boQua = await _db.QaMesDongBoNgays.AsNoTracking()
                .Where(x => x.Ngay >= tu && x.DayDu && x.NgayDongBo > moiDay).Select(x => x.Ngay).ToListAsync(ct);
            int daLam = 0, tongDong = 0;
            for (var n = homNay; n >= tu; n = n.AddDays(-1))
            {
                if (boQua.Contains(n)) { daLam++; continue; }
                try
                {
                    var (dong, _) = await DongBoNgayAsync(n, ct);
                    daLam++;
                    tongDong += dong;
                }
                catch (InvalidOperationException ex) { return (daLam, tongDong, ex.Message); }
            }
            return (daLam, tongDong, null);
        }

        /// <summary>
        /// Đồng bộ 1 ngày đóng gói. Trả (số dòng, đủ hay chưa); lỗi MES thì ném InvalidOperationException.
        /// </summary>
        public async Task<(int soDong, bool dayDu)> DongBoNgayAsync(DateOnly ngay, CancellationToken ct)
        {
            var kq = await _mes.TraTatCaTheoNgayAsync(ngay, ct);
            if (!kq.ThanhCong) throw new InvalidOperationException(kq.ThongBao);
            var dong = kq.Dong!;

            // Chặn trường hợp MES bỏ qua lọc ngày (trả cả kho) — lưu vào sẽ sai phạm vi đánh dấu "mất"
            var lech = dong.FirstOrDefault(d => DocNgay(d.PbDate) is DateTime t && DateOnly.FromDateTime(t) != ngay);
            if (lech != null)
                throw new InvalidOperationException($"MES trả dòng ngày đóng gói {lech.PbDate} khi lọc {ngay:dd/MM/yyyy} — lọc ngày không hoạt động, dừng đồng bộ.");

            var dayDu = dong.Count >= kq.Tong;
            var tu = ngay.ToDateTime(TimeOnly.MinValue);
            var den = tu.AddDays(1);
            await LuuAsync(dong, dayDu ? q => q.Where(x => x.NgayDongGoi >= tu && x.NgayDongGoi < den) : null, ct);

            var tt = await _db.QaMesDongBoNgays.FirstOrDefaultAsync(x => x.Ngay == ngay, ct);
            if (tt == null) _db.QaMesDongBoNgays.Add(tt = new QaMesDongBoNgay { Ngay = ngay });
            tt.TongMes = kq.Tong;
            tt.SoDong = dong.Count;
            tt.DayDu = dayDu;
            tt.NgayDongBo = DateTime.Now;
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Máy chủ kia vừa ghi trạng thái cùng ngày: dữ liệu dòng đã lưu xong, bỏ qua
                _db.ChangeTracker.Clear();
            }
            return (dong.Count, dayDu);
        }

        private async Task<KetQuaKho?> DocDbAsync(LocTraCuu loc, CancellationToken ct)
        {
            var q = _db.QaMesDongGois.AsNoTracking().Where(x => x.NgayMat == null);
            if (loc.Ma.Length > 0) q = LocMa(q, loc.Ma);
            if (loc.Kh.Length > 0) q = q.Where(x => x.MaKhachHang!.Contains(loc.Kh));
            if (loc.Hang.Length > 0) q = q.Where(x => x.MaHang!.Contains(loc.Hang));
            // Không lọc mã phối màu: mới nhất trước để trần 10.000 dòng rơi vào hàng đang làm
            var sx = loc.Ma.Length > 0
                ? q.OrderBy(x => x.NgayDongGoi).ThenBy(x => x.IdDongMes)
                : q.OrderByDescending(x => x.NgayDongGoi).ThenByDescending(x => x.IdDongMes);
            var ds = await sx.Take(MesApiService.TranTongDong)
                .Select(x => new { x.DauVanTay, x.SoDong, x.DuLieuMes, x.NgayThayCuoi })
                .ToListAsync(ct);
            if (ds.Count == 0) return null;
            var tong = await q.SumAsync(x => x.SoDong, ct);

            var dong = new List<MesApiService.DongDongGoi>();
            foreach (var x in ds)
            {
                var d = JsonSerializer.Deserialize<MesApiService.DongDongGoi>(x.DuLieuMes, MesApiService.JsonOpt)!
                    with { Raw = x.DuLieuMes, DauVanTay = x.DauVanTay };
                dong.Add(d);
                // Dòng giống hệt nhau trên MES: trả đủ số dòng, mỗi dòng 1 object riêng vì MaThung được gán riêng từng dòng
                for (int i = 1; i < x.SoDong; i++) dong.Add(d with { });
            }
            if (dong.Count > MesApiService.TranTongDong) dong.RemoveRange(MesApiService.TranTongDong, dong.Count - MesApiService.TranTongDong);
            return new(new(true, "", tong, dong), "DB", ds.Max(x => x.NgayThayCuoi));
        }

        /// <summary>
        /// Upsert các dòng MES theo dấu vân tay. phamVi != null: dòng còn hiệu lực trong phạm vi đó mà MES không trả nữa
        /// thì đặt ngay_mat (dữ liệu MES đã đổi/xoá). Khoá ứng dụng SQL để 2 máy chủ + nút làm mới không ghi chồng nhau.
        /// </summary>
        private async Task LuuAsync(List<MesApiService.DongDongGoi> dong,
            Func<IQueryable<QaMesDongGoi>, IQueryable<QaMesDongGoi>>? phamVi, CancellationToken ct)
        {
            var nhom = dong.GroupBy(d => d.DauVanTay).ToDictionary(g => g.Key, g => (dau: g.First(), soDong: g.Count()));
            var dsVanTay = nhom.Keys.ToList();
            var bayGio = DateTime.Now;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.Database.ExecuteSqlRawAsync(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'QA_MesDongBo', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 120000; " +
                "IF @r < 0 THROW 50001, N'Đang có tiến trình khác đồng bộ MES, thử lại sau.', 1;", ct);

            if (phamVi != null)
                await phamVi(_db.QaMesDongGois)
                    .Where(x => x.NgayMat == null && !dsVanTay.Contains(x.DauVanTay))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayMat, bayGio), ct);

            // Từng lô để danh sách tham số IN không quá lớn
            foreach (var lo in dsVanTay.Chunk(2000))
            {
                var loList = lo.ToList();
                var daCo = await _db.QaMesDongGois.Where(x => loList.Contains(x.DauVanTay)).ToListAsync(ct);
                var daCoMap = daCo.ToDictionary(x => x.DauVanTay);
                foreach (var vt in loList)
                {
                    var (d, soDong) = nhom[vt];
                    if (daCoMap.TryGetValue(vt, out var cu))
                    {
                        cu.SoDong = soDong;
                        cu.NgayThayCuoi = bayGio;
                        cu.NgayMat = null;
                    }
                    else
                    {
                        _db.QaMesDongGois.Add(new QaMesDongGoi
                        {
                            DauVanTay = vt,
                            SoDong = soDong,
                            MaPhoiMau = Cat(d.OrderUcode, 50),
                            MaKhachCuoi = Cat(d.EndCustCode, 50),
                            MaKhachHang = Cat(d.CustCode, 50),
                            MaHang = Cat(d.ArtCode, 100),
                            MaMau = Cat(d.ColorCode, 100),
                            SoPhieuNhuom = Cat(d.ClNo, 50),
                            SoPhieuDongGoi = Cat(d.PackclNo, 50),
                            NgayDongGoi = DocNgay(d.PbDate),
                            DuLieuMes = BoTruongNull(d.Raw),
                            NgayDauThay = bayGio,
                            NgayThayCuoi = bayGio
                        });
                    }
                }
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
            }
            await tx.CommitAsync(ct);
        }

        // JSON dòng MES có ~130 trường, phần lớn null — bỏ đi cho nhẹ bảng; đọc lại thiếu trường thì vẫn là null.
        // Vân tay đã tính trên JSON gốc nên không đổi.
        private static string BoTruongNull(string raw)
        {
            if (JsonNode.Parse(raw) is not JsonObject obj) return raw;
            foreach (var k in obj.Where(p => p.Value == null).Select(p => p.Key).ToList()) obj.Remove(k);
            return obj.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        private static string? Cat(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;

        private static DateTime? DocNgay(string? s)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
    }
}
