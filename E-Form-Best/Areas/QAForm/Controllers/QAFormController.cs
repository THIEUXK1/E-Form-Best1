using E_Form_Best.Areas.QAForm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Controllers
{
    // Phân hệ QA: quyền All hoặc quyền bộ phận QA_VIEW (bộ phận QA 品控部 BPVN) — xem QaQuyen.
    // Chặn ở cấp controller để mọi action thêm sau này (kể cả API JSON) đều được bảo vệ,
    // không phụ thuộc việc ẩn thẻ ở trang menu.
    [Area("QAForm")]
    [Authorize(Policy = QaQuyen.Policy)]
    public class QAFormController : Controller
    {
        private readonly MesKhoService _kho;
        private readonly QaThungService _thung;
        private readonly QaKeService _ke;
        private readonly ILogger<QAFormController> _logger;
        private readonly IConfiguration _configuration;

        public QAFormController(MesKhoService kho, QaThungService thung, QaKeService ke, ILogger<QAFormController> logger, IConfiguration configuration)
        {
            _kho = kho;
            _thung = thung;
            _ke = ke;
            _logger = logger;
            _configuration = configuration;
        }

        // DsIdDongChuyen: dòng đang ở thùng khác (id QA_ThungDong) cần chuyển sang thùng đích
        public record BoVaoThungVao(string? MaPhoiMau, List<string>? DsDauVanTay, List<int>? DsIdDongChuyen, int? IdThung, string? GhiChu, List<int>? DsThuTu = null, string? TenThung = null);
        // Số paiban cho dòng MES chưa vào thùng; SoBan null = xoá số
        public record MesSoBanVao(string? DauVanTay, int? ThuTu, int? SoBan);
        public record BoRaThungVao(List<int>? DsIdDong);
        // IdKe null = gỡ thùng khỏi kệ
        public record XepThungVao(int? IdKe, int? Tang, int? O);
        // SoBan null = xoá số đã nhập
        public record SoBanVao(int? SoBan);
        // ViTri gõ tay "A-2-3"; để trống = gỡ khỏi kệ
        public record XepNhieuThungVao(List<int>? DsIdThung, string? ViTri);

        private string? NguoiDung => User.Identity?.Name;

        [HttpGet("/QAForm/Logo")]
        public IActionResult Logo()
        {
            return View();
        }

        // Trang tra cứu mã phối màu (配色单号) trên NH-MES
        [HttpGet("/QAForm/TraCuuMes")]
        public IActionResult TraCuuMes()
        {
            return View();
        }

        // Check xuất hàng: barcode / mã lot / phiếu xuất / hoá đơn -> dữ liệu xuất kho NH-WMS + thông tin đơn hàng NH-MES
        [HttpGet("/QAForm/CheckXuatHang")]
        public IActionResult CheckXuatHang()
        {
            return View();
        }

        [HttpGet("/QAForm/CheckXuatHang/DuLieu")]
        public async Task<IActionResult> CheckXuatHangDuLieu([FromServices] CheckXuatHangService svc, string? ma,
            CheckXuatHangService.LoaiMa loai = CheckXuatHangService.LoaiMa.TuNhan, CancellationToken ct = default)
        {
            var kq = await svc.TraCuuAsync(ma ?? "", loai, ct);
            return Json(new
            {
                thanhCong = kq.ThanhCong,
                thongBao = kq.ThongBao,
                duLieu = kq.ThanhCong ? new { loai = kq.Loai.ToString(), dsLot = kq.DsLot, dsXuat = kq.DsXuat, tongXuat = kq.TongXuat, canhBao = kq.CanhBao } : null
            });
        }

        // ---- Bảng khiếu nại khách hàng (QA_KhieuNai) trên trang Check xuất hàng ----
        public record KhieuNaiThemVao(string? KhoaTao);
        public record KhieuNaiSuaVao(string? Truong, string? GiaTri);

        [HttpGet("/QAForm/CheckXuatHang/KhieuNai")]
        public async Task<IActionResult> KhieuNaiDanhSach([FromServices] QaKhieuNaiService svc, DateTime? tuNgay, DateTime? denNgay,
            string? trangThai, string? tuKhoa, CancellationToken ct = default)
        {
            try
            {
                var (ds, tong) = await svc.DanhSachAsync(new(tuNgay, denNgay, trangThai, tuKhoa), ct);
                var goiY = await svc.GoiYChungLoaiAsync(ct);
                var cot = QaKhieuNaiService.DsCot.Select(c => new
                {
                    khoa = c.Khoa, cotExcel = c.CotExcel, tieuDe = c.TieuDe, kieu = c.Kieu.ToString(), doDai = c.DoDai, tuDong = c.TuDong, vang = c.Vang
                });
                return Json(new { thanhCong = true, thongBao = "", duLieu = new { cot, ds, tong, goiYChungLoai = goiY } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc bảng khiếu nại QA");
                return Json(new { thanhCong = false, thongBao = "Không đọc được bảng khiếu nại: " + ex.Message });
            }
        }

        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/Them")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> KhieuNaiThem([FromServices] QaKhieuNaiService svc, [FromBody] KhieuNaiThemVao vao, CancellationToken ct = default)
            => KhieuNaiGhiAsync(() => svc.ThemAsync(vao.KhoaTao, NguoiDung, ct), "thêm dòng");

        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/{id:int}/Sua")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> KhieuNaiSua([FromServices] QaKhieuNaiService svc, int id, [FromBody] KhieuNaiSuaVao vao, CancellationToken ct = default)
            => KhieuNaiGhiAsync(() => svc.SuaAsync(id, vao.Truong, vao.GiaTri, NguoiDung, ct), "lưu ô");

        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/{id:int}/DienLai")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> KhieuNaiDienLai([FromServices] QaKhieuNaiService svc, int id, CancellationToken ct = default)
            => KhieuNaiGhiAsync(() => svc.DienLaiAsync(id, NguoiDung, ct), "lấy lại từ MES");

        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/{id:int}/Xoa")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> KhieuNaiXoa([FromServices] QaKhieuNaiService svc, int id, CancellationToken ct = default)
            => KhieuNaiGhiAsync(() => svc.XoaAsync(id, NguoiDung, ct), "xoá dòng");

        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/NhapExcel")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> KhieuNaiNhapExcel([FromServices] QaKhieuNaiService svc, IFormFile? tep, CancellationToken ct = default)
        {
            if (tep == null || tep.Length == 0) return Json(new { thanhCong = false, thongBao = "Chưa chọn file." });
            if (!tep.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Json(new { thanhCong = false, thongBao = "Chỉ nhận file .xlsx." });
            await using var st = tep.OpenReadStream();
            return await KhieuNaiGhiAsync(() => svc.NhapExcelAsync(st, NguoiDung, ct), "nhập Excel");
        }

        // Đồng bộ ngay thư mục QA trên file server (job nền cũng tự chạy 30 phút/lần); đọc lại mọi file
        [HttpPost("/QAForm/CheckXuatHang/KhieuNai/DongBoThuMuc")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> KhieuNaiDongBoThuMuc([FromServices] QaKhieuNaiService svc, CancellationToken ct = default)
            => KhieuNaiGhiAsync(() => svc.DongBoThuMucAsync(QaKhieuNaiService.ThuMucDongBo(_configuration), tatCa: true, NguoiDung, ct), "đồng bộ thư mục QA");

        [HttpGet("/QAForm/CheckXuatHang/KhieuNai/XuatExcel")]
        public async Task<IActionResult> KhieuNaiXuatExcel([FromServices] QaKhieuNaiService svc, DateTime? tuNgay, DateTime? denNgay,
            string? trangThai, string? tuKhoa, CancellationToken ct = default)
        {
            var file = await svc.XuatExcelAsync(new(tuNgay, denNgay, trangThai, tuKhoa), ct);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CUSTOMER COMPLAINT SUMMARY {DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }

        private async Task<IActionResult> KhieuNaiGhiAsync(Func<Task<QaKhieuNaiService.KetQua>> lam, string viec)
        {
            try
            {
                var kq = await lam();
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = kq.Dong });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi {Viec} bảng khiếu nại QA", viec);
                return Json(new { thanhCong = false, thongBao = $"Lỗi {viec}: " + ex.Message });
            }
        }

        // Đọc từ bản sao DB (job đồng bộ MES); mã chưa có trong DB thì gọi MES trực tiếp
        [HttpGet("/QAForm/TraCuuMes/DuLieu")]
        public Task<IActionResult> TraCuuMesDuLieu(string? maPhoiMau, string? maKhachHang, string? maHang, CancellationToken ct = default)
            => TraCuuMesAsync(maPhoiMau, maKhachHang, maHang, lamMoi: false, ct);

        // Gợi ý khi gõ vào ô lọc, lấy từ giá trị đang có trong bản sao DB. truong: ma | kh | hang
        [HttpGet("/QAForm/TraCuuMes/GoiY")]
        public async Task<IActionResult> TraCuuMesGoiY(string? truong, string? q, string? maPhoiMau, string? maKhachHang, string? maHang, CancellationToken ct = default)
        {
            truong = truong is "kh" or "hang" ? truong : "ma";
            q = (q ?? "").Trim();
            if (q.Length > 100) return Json(new { thanhCong = true, thongBao = "", duLieu = Array.Empty<string>() });
            try
            {
                var ds = await _kho.GoiYAsync(truong, q, TaoLoc(maPhoiMau, maKhachHang, maHang), ct);
                return Json(new { thanhCong = true, thongBao = "", duLieu = ds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gợi ý tra cứu MES");
                return Json(new { thanhCong = false, thongBao = "Không đọc được gợi ý: " + ex.Message });
            }
        }

        private const int SoNgayDongBoNgay = 7;

        private static MesKhoService.LocTraCuu TaoLoc(string? ma, string? kh, string? hang)
        {
            static string Cat(string? s) { s = (s ?? "").Trim(); return s.Length > 100 ? s[..100] : s; }
            return new(Cat(ma), Cat(kh), Cat(hang));
        }

        // Nút "Đồng bộ MES": có mã phối màu -> kéo lại riêng mã đó; để trống -> kéo lại cả SoNgayDongBoNgay ngày gần nhất,
        // rồi trả kết quả theo bộ lọc đang nhập
        [HttpPost("/QAForm/TraCuuMes/LamMoi")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> TraCuuMesLamMoi(string? maPhoiMau, string? maKhachHang, string? maHang, CancellationToken ct = default)
            => TraCuuMesAsync(maPhoiMau, maKhachHang, maHang, lamMoi: true, ct);

        private async Task<IActionResult> TraCuuMesAsync(string? maPhoiMau, string? maKhachHang, string? maHang, bool lamMoi, CancellationToken ct)
        {
            var loc = TaoLoc(maPhoiMau, maKhachHang, maHang);
            // Để trống cả 3 ô = xem toàn bộ bản sao DB (mới nhất trước); có nhập mã phối màu thì đủ dài để không kéo cả kho MES về
            if (loc.Ma.Length > 0 && (loc.Ma.Length < 4 || loc.Ma.Length > 50))
                return Json(new { thanhCong = false, thongBao = "Mã phối màu cần ít nhất 4 ký tự (hoặc để trống)." });

            string? dongBo = null;
            MesKhoService.KetQuaKho kho;
            if (lamMoi && loc.Ma.Length == 0)
            {
                var (soNgay, soDong, loiDongBo) = await _kho.DongBoGanDayAsync(SoNgayDongBoNgay, ct);
                if (loiDongBo != null && soNgay == 0) return Json(new { thanhCong = false, thongBao = "Đồng bộ MES lỗi: " + loiDongBo });
                dongBo = $"Đã đồng bộ {soNgay} ngày gần nhất từ MES ({soDong:N0} dòng)" + (loiDongBo != null ? " — dừng giữa chừng: " + loiDongBo : ".");
                kho = await _kho.TraCuuAsync(loc, ct);
            }
            else kho = lamMoi ? await _kho.LamMoiTheoPhoiMauAsync(loc, ct) : await _kho.TraCuuAsync(loc, ct);
            var kq = kho.Kq;
            if (!kq.ThanhCong) return Json(new { thanhCong = false, thongBao = kq.ThongBao });

            var thongBao = "";
            try
            {
                await _thung.GanMaThungAsync(kq.Dong!, ct);
            }
            catch (Exception ex)
            {
                // Không đọc được bảng thùng vẫn cho xem dữ liệu, chỉ báo thiếu cột "Thùng"
                _logger.LogError(ex, "Không gắn được mã thùng QA cho kết quả tra cứu");
                thongBao = "Không đọc được dữ liệu thùng: " + ex.Message;
            }
            return Json(new
            {
                thanhCong = true,
                thongBao,
                duLieu = new
                {
                    tong = kq.Tong,
                    tran = MesApiService.TranTongDong,
                    dong = kq.Dong,
                    nguon = kho.Nguon,
                    capNhatLuc = kho.CapNhatLuc?.ToString("dd/MM/yyyy HH:mm"),
                    dongBo
                }
            });
        }

        // ===== THÙNG =====

        [HttpGet("/QAForm/Thung")]
        public IActionResult Thung()
        {
            return View();
        }

        // ===== DRIVER MÁY IN TEM TSC =====
        // Dùng chung kho driver với trang tủ điện thoại HR (file server, mỗi thư mục con = 1 dòng máy in).
        // QA chỉ thấy thư mục tên bắt đầu "TSC"; IT chép thêm thư mục "TSC xxx" là trang tự hiện.
        private string ThuMucDriver => _configuration["TemInAgent:ThuMucDriver"]
            ?? Path.Combine(_configuration["TemInAgent:ThuMuc"] ?? E_Form_Best.Areas.HRform.Controllers.TuDienThoaiController.ThuMucAgentMacDinh, "Driver");

        private static bool LaThuMucTsc(string ten) => ten.StartsWith("TSC", StringComparison.OrdinalIgnoreCase);

        [HttpGet("/QAForm/DriverTsc/DanhSach")]
        public IActionResult DriverTscDanhSach()
        {
            try
            {
                var goc = new DirectoryInfo(ThuMucDriver);
                if (!goc.Exists)
                    return Json(new { thanhCong = false, thongBao = "Chưa có driver nào trên file server, liên hệ IT." });

                var ds = goc.GetDirectories()
                    .Where(d => LaThuMucTsc(d.Name))
                    .OrderBy(d => d.Name)
                    .Select(d => new
                    {
                        may = d.Name,
                        files = d.GetFiles()
                            .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                            .OrderBy(f => f.Name)
                            .Select(f => new { ten = f.Name, kichThuocMb = Math.Round(f.Length / 1048576.0, 1), ngayCapNhat = f.LastWriteTime })
                            .ToList()
                    })
                    .Where(m => m.files.Count > 0)
                    .ToList();
                return Json(new { thanhCong = true, thongBao = "", duLieu = ds });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Không trả đường dẫn share / ex.Message (có \\máy\thư mục) về trình duyệt
                _logger.LogError(ex, "Không đọc được thư mục driver máy in TSC");
                return Json(new { thanhCong = false, thongBao = "Máy chủ không đọc được file server, liên hệ IT." });
            }
        }

        [HttpGet("/QAForm/DriverTsc/Tai")]
        public IActionResult DriverTscTai(string may, string file)
        {
            // Chỉ nhận đúng tên thư mục TSC / tên file — chặn ..\ hay đường dẫn tuyệt đối đọc file ngoài kho driver
            if (string.IsNullOrWhiteSpace(may) || string.IsNullOrWhiteSpace(file)
                || may.Contains("..") || file.Contains("..")
                || Path.GetFileName(may) != may || Path.GetFileName(file) != file
                || !LaThuMucTsc(may))
                return BadRequest("Tên driver không hợp lệ.");

            var duongDan = Path.Combine(ThuMucDriver, may, file);
            if (!System.IO.File.Exists(duongDan)) return NotFound("Không tìm thấy driver này, liên hệ IT.");
            return PhysicalFile(duongDan, "application/octet-stream", file, enableRangeProcessing: true);
        }

        // chiThungMo=true: danh sách cho hộp chọn "Thêm vào thùng"
        // tuKhoa: tìm theo mã thùng hoặc hàng trong thùng (mã phối màu, mã hàng, mã màu, số phiếu)
        [HttpGet("/QAForm/Thung/DanhSach")]
        public async Task<IActionResult> ThungDanhSach(bool chiThungMo = false, string? tuKhoa = null, CancellationToken ct = default)
        {
            try
            {
                var ds = await _thung.DsThungAsync(chiThungMo, tuKhoa, ct);
                return Json(new { thanhCong = true, thongBao = "", duLieu = ds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc danh sách thùng QA");
                return Json(new { thanhCong = false, thongBao = "Không đọc được danh sách thùng: " + ex.Message });
            }
        }

        [HttpGet("/QAForm/Thung/ChiTiet/{id:int}")]
        public async Task<IActionResult> ThungChiTiet(int id, CancellationToken ct = default)
        {
            var (thung, dong, lichSu) = await _thung.ChiTietAsync(id, ct);
            if (thung == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy thùng (có thể đã bị xoá)." });
            var coBangMuon = await _thung.CoBangMuonAsync(ct);
            var dangMuon = await _thung.DangMuonAsync(dong.Select(d => d.IdDong).ToList(), ct);
            return Json(new
            {
                thanhCong = true,
                thongBao = "",
                duLieu = new
                {
                    thung = new { thung.IdThung, thung.MaThung, thung.TenThung, thung.TrangThai, thung.GhiChu, thung.NgayTao, thung.NguoiTao, thung.NgayDong, thung.NguoiDong,
                        thung.IdKe, thung.Tang, thung.O, ViTri = QaKeService.MaViTri(thung.Ke?.MaKe, thung.Tang, thung.O, thung.Ke?.SoTang ?? 0) },
                    dong = dong.Select(d => new
                    {
                        d.IdDong, d.MaPhoiMau, d.MaKhachCuoi, d.MaKhachHang, d.MaHang, d.MaMau, d.SlDonHang, d.SlDongGoi, d.KlDongGoi,
                        d.SoHop, d.SoBan, d.Loai, d.MoTaBatThuong, d.YKienQc, d.CongDoan, d.SoPhieuNhuom, d.NgayNhanHang, d.NgayDongGoi,
                        d.SoPhieuDongGoi, d.MaNvDongGoi, d.GhiChuMes, d.NgayThem, d.NguoiThem,
                        Muon = dangMuon.TryGetValue(d.IdDong, out var m) ? new { m.MaNhanVien, m.TenNguoiMuon, m.NgayMuon, m.NguoiChoMuon } : null
                    }),
                    lichSu = lichSu.Select(l => new { l.Ngay, l.Nguoi, l.HanhDong, l.NoiDung }),
                    coBangMuon
                }
            });
        }

        // ===== CHO MƯỢN DÒNG TRONG THÙNG =====
        public record MuonVao(List<int>? DsIdDong, string? MaNhanVien);

        // Tra tên người mượn trước khi xác nhận (mã NV hoặc tài khoản, bỏ người đã nghỉ)
        [HttpGet("/QAForm/NhanVien")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("tra-nhan-vien")] // chống gõ lần lượt mã NV để gom danh sách
        public async Task<IActionResult> TimNhanVien(string? ma, CancellationToken ct = default)
        {
            var nv = await _thung.TimNhanVienAsync(ma, ct);
            if (nv == null) return Json(new { thanhCong = false, thongBao = $"Không tìm thấy nhân viên mã \"{(ma ?? "").Trim()}\" (hoặc đã nghỉ)." });
            return Json(new { thanhCong = true, thongBao = "", duLieu = nv });
        }

        [HttpPost("/QAForm/Thung/ChoMuon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungChoMuon([FromBody] MuonVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.ChoMuonAsync(vao.DsIdDong, vao.MaNhanVien, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cho mượn dòng thùng QA");
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu cho mượn: " + ex.Message });
            }
        }

        [HttpPost("/QAForm/Thung/TraMuon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungTraMuon([FromBody] MuonVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.TraMuonAsync(vao.DsIdDong, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nhận trả dòng thùng QA");
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu nhận trả: " + ex.Message });
            }
        }

        // idThung null = tạo thùng mới; server tự gọi lại MES lấy dữ liệu gốc theo dấu vân tay
        [HttpPost("/QAForm/Thung/BoVao")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungBoVao([FromBody] BoVaoThungVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.BoVaoThungAsync(vao.MaPhoiMau ?? "", vao.DsDauVanTay ?? new(), vao.DsIdDongChuyen, vao.IdThung, vao.GhiChu, NguoiDung, ct, vao.DsThuTu, vao.TenThung);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = new { kq.IdThung, kq.MaThung } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi bỏ dòng vào thùng QA");
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu thùng: " + ex.Message });
            }
        }

        // Bỏ dòng ra khỏi thùng (xoá mềm, có lịch sử); trình duyệt đã hỏi xác nhận trước khi gọi
        [HttpPost("/QAForm/Thung/BoRa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungBoRa([FromBody] BoRaThungVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.BoRaKhoiThungAsync(vao.DsIdDong, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi bỏ dòng ra khỏi thùng QA");
                return Json(new { thanhCong = false, thongBao = "Lỗi bỏ dòng ra khỏi thùng: " + ex.Message });
            }
        }

        // Nhập số paiban cho dòng MES chưa vào thùng (trang tra cứu); vào thùng thì số được mang sang QA_ThungDong
        [HttpPost("/QAForm/MesSoBan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MesSoBan([FromBody] MesSoBanVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.LuuSoBanMesAsync(vao.DauVanTay, vao.ThuTu ?? -1, vao.SoBan, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu số paiban dòng MES");
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu số paiban: " + ex.Message });
            }
        }

        // Nhập tay số bản cho 1 dòng ở chi tiết thùng
        [HttpPost("/QAForm/Thung/Dong/{idDong:int}/SoBan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungDongSoBan(int idDong, [FromBody] SoBanVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _thung.SuaSoBanAsync(idDong, vao.SoBan, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu số bản dòng thùng QA");
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu số paiban: " + ex.Message });
            }
        }

        [HttpPost("/QAForm/Thung/{id:int}/TrangThai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungTrangThai(int id, bool dong, CancellationToken ct = default)
        {
            var kq = await _thung.DoiTrangThaiAsync(id, dong, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        [HttpPost("/QAForm/Thung/{id:int}/DoiTen")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungDoiTen(int id, string? ten, CancellationToken ct = default)
        {
            var kq = await _thung.DoiTenThungAsync(id, ten, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        [HttpPost("/QAForm/Thung/{id:int}/GhiChu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungGhiChu(int id, [FromForm] string? ghiChu, CancellationToken ct = default)
        {
            var kq = await _thung.SuaGhiChuAsync(id, ghiChu, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        [HttpPost("/QAForm/Thung/{id:int}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungXoa(int id, CancellationToken ct = default)
        {
            var kq = await _thung.XoaThungAsync(id, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        // ===== THÙNG ĐÃ XOÁ (chỉ All): xem nhật ký + khôi phục khi xoá nhầm =====
        private bool LaAll => User.IsInRole("All");
        private JsonResult KhongCoQuyenAll() => Json(new { thanhCong = false, thongBao = "Chỉ tài khoản quyền All được xem/khôi phục thùng đã xoá." });

        // Trang theo dõi thao tác (menu "Nhật ký thao tác"): nhật ký mọi thùng + thùng đã xoá để khôi phục
        [HttpGet("/QAForm/LichSu")]
        public IActionResult LichSu()
        {
            if (!LaAll) return Forbid();
            return View();
        }

        [HttpGet("/QAForm/LichSu/DuLieu")]
        public async Task<IActionResult> LichSuDuLieu(DateTime? tuNgay, DateTime? denNgay, string? nguoi, string? hanhDong, string? tuKhoa, CancellationToken ct = default)
        {
            if (!LaAll) return KhongCoQuyenAll();
            try
            {
                var ds = await _thung.NhatKyAsync(tuNgay, denNgay, nguoi, hanhDong, tuKhoa, ct);
                return Json(new { thanhCong = true, thongBao = "", duLieu = ds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc nhật ký thao tác QA");
                return Json(new { thanhCong = false, thongBao = "Không đọc được nhật ký: " + ex.Message });
            }
        }

        [HttpGet("/QAForm/Thung/DaXoa")]
        public async Task<IActionResult> ThungDaXoa(string? tuKhoa = null, CancellationToken ct = default)
        {
            if (!LaAll) return KhongCoQuyenAll();
            try
            {
                var ds = await _thung.DsThungDaXoaAsync(tuKhoa, ct);
                return Json(new { thanhCong = true, thongBao = "", duLieu = ds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc danh sách thùng đã xoá");
                return Json(new { thanhCong = false, thongBao = "Không đọc được thùng đã xoá: " + ex.Message });
            }
        }

        [HttpGet("/QAForm/Thung/DaXoa/{id:int}")]
        public async Task<IActionResult> ThungDaXoaChiTiet(int id, CancellationToken ct = default)
        {
            if (!LaAll) return KhongCoQuyenAll();
            var (thung, dong, lichSu) = await _thung.ChiTietDaXoaAsync(id, ct);
            if (thung == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy thùng đã xoá (có thể vừa được khôi phục)." });
            return Json(new
            {
                thanhCong = true,
                thongBao = "",
                duLieu = new
                {
                    dong = dong.Select(d => new { d.MaPhoiMau, d.MaHang, d.MaMau, d.SlDongGoi, d.SoHop, d.SoBan, d.SoPhieuDongGoi, d.NgayThem, d.NguoiThem }),
                    lichSu = lichSu.Select(l => new { l.Ngay, l.Nguoi, l.HanhDong, l.NoiDung })
                }
            });
        }

        [HttpPost("/QAForm/Thung/{id:int}/KhoiPhuc")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungKhoiPhuc(int id, bool boDongTrung = false, CancellationToken ct = default)
        {
            if (!LaAll) return KhongCoQuyenAll();
            try
            {
                var (kq, coDongTrung) = await _thung.KhoiPhucThungAsync(id, boDongTrung, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = new { coDongTrung } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khôi phục thùng QA {Id}", id);
                return Json(new { thanhCong = false, thongBao = "Lỗi khôi phục thùng: " + ex.Message });
            }
        }

        // ===== KỆ =====

        // Trang quản lý kệ riêng (menu "Quản lý kệ"); cùng khung với nút "Kệ" ở trang Danh sách thùng
        [HttpGet("/QAForm/Ke")]
        public IActionResult Ke()
        {
            return View();
        }

        [HttpGet("/QAForm/Ke/DanhSach")]
        public async Task<IActionResult> KeDanhSach(CancellationToken ct = default)
        {
            try
            {
                return Json(new { thanhCong = true, thongBao = "", duLieu = await _ke.DsKeAsync(ct) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc danh sách kệ QA");
                return Json(new { thanhCong = false, thongBao = "Không đọc được danh sách kệ: " + ex.Message });
            }
        }

        [HttpGet("/QAForm/Ke/{id:int}/SoDo")]
        public async Task<IActionResult> KeSoDo(int id, CancellationToken ct = default)
        {
            var (ke, thung) = await _ke.SoDoAsync(id, ct);
            if (ke == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy kệ (có thể đã bị xoá)." });
            return Json(new
            {
                thanhCong = true,
                thongBao = "",
                duLieu = new { ke = new { ke.IdKe, ke.MaKe, ke.TenKe, ke.KhuVuc, ke.SoTang, ke.SoO, ke.GhiChu }, thung }
            });
        }

        // IdKe null = thêm kệ mới
        [HttpPost("/QAForm/Ke/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KeLuu([FromBody] QaKeService.LuuKeVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _ke.LuuKeAsync(vao, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = new { kq.IdKe } });
            }
            catch (DbUpdateException ex)
            {
                // Trùng mã kệ do 2 người lưu cùng lúc (UX_QA_Ke_MaKe_ConDung)
                _logger.LogWarning(ex, "Lỗi lưu kệ QA");
                return Json(new { thanhCong = false, thongBao = "Không lưu được (có thể trùng mã kệ với người khác vừa lưu), kiểm tra lại." });
            }
        }

        // Tạo nhanh dãy kệ A..Z (bỏ qua mã đã có)
        [HttpPost("/QAForm/Ke/TaoDay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KeTaoDay([FromBody] QaKeService.TaoDayKeVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _ke.TaoDayKeAsync(vao, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Lỗi tạo dãy kệ QA");
                return Json(new { thanhCong = false, thongBao = "Không tạo được (có thể người khác vừa tạo cùng mã kệ), tải lại rồi thử lại." });
            }
        }

        [HttpPost("/QAForm/Ke/{id:int}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KeXoa(int id, CancellationToken ct = default)
        {
            var kq = await _ke.XoaKeAsync(id, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        [HttpPost("/QAForm/Thung/{id:int}/ViTri")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungViTri(int id, [FromBody] XepThungVao vao, CancellationToken ct = default)
        {
            var kq = await _ke.XepThungAsync(new List<int> { id }, vao.IdKe, vao.Tang, vao.O, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        // Tích nhiều thùng ở danh sách rồi xếp cùng 1 ô / gỡ khỏi kệ (IdKe null)
        [HttpPost("/QAForm/Thung/XepKe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungXepKe([FromBody] XepNhieuThungVao vao, CancellationToken ct = default)
        {
            try
            {
                var kq = await _ke.XepTheoViTriAsync(vao.DsIdThung, vao.ViTri, NguoiDung, ct);
                return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xếp thùng QA lên kệ");
                return Json(new { thanhCong = false, thongBao = "Lỗi xếp kệ: " + ex.Message });
            }
        }
    }
}
