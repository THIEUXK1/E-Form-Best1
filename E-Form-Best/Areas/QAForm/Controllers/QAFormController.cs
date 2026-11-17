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

        public QAFormController(MesKhoService kho, QaThungService thung, QaKeService ke, ILogger<QAFormController> logger)
        {
            _kho = kho;
            _thung = thung;
            _ke = ke;
            _logger = logger;
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
                        d.SoPhieuDongGoi, d.MaNvDongGoi, d.GhiChuMes, d.NgayThem, d.NguoiThem
                    }),
                    lichSu = lichSu.Select(l => new { l.Ngay, l.Nguoi, l.HanhDong, l.NoiDung })
                }
            });
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
