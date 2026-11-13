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
        private readonly MesApiService _mes;
        private readonly QaThungService _thung;
        private readonly QaKeService _ke;
        private readonly ILogger<QAFormController> _logger;

        public QAFormController(MesApiService mes, QaThungService thung, QaKeService ke, ILogger<QAFormController> logger)
        {
            _mes = mes;
            _thung = thung;
            _ke = ke;
            _logger = logger;
        }

        // DsIdDongChuyen: dòng đang ở thùng khác (id QA_ThungDong) cần chuyển sang thùng đích
        public record BoVaoThungVao(string? MaPhoiMau, List<string>? DsDauVanTay, List<int>? DsIdDongChuyen, int? IdThung, string? GhiChu);
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

        [HttpGet("/QAForm/TraCuuMes/DuLieu")]
        public async Task<IActionResult> TraCuuMesDuLieu(string? maPhoiMau, CancellationToken ct = default)
        {
            maPhoiMau = (maPhoiMau ?? "").Trim();
            // Bắt buộc nhập đủ dài để không kéo cả kho dữ liệu MES về
            if (maPhoiMau.Length < 4 || maPhoiMau.Length > 50)
                return Json(new { thanhCong = false, thongBao = "Nhập mã phối màu (ít nhất 4 ký tự)." });

            // Trả hết 1 lần, trình duyệt không phải chuyển trang
            var kq = await _mes.TraTatCaTheoPhoiMauAsync(maPhoiMau, ct);
            if (!kq.ThanhCong) return Json(new { thanhCong = false, thongBao = kq.ThongBao });

            try
            {
                await _thung.GanMaThungAsync(kq.Dong!, ct);
            }
            catch (Exception ex)
            {
                // Không đọc được bảng thùng vẫn cho xem dữ liệu MES, chỉ báo thiếu cột "Thùng"
                _logger.LogError(ex, "Không gắn được mã thùng QA cho kết quả tra cứu");
                return Json(new { thanhCong = true, thongBao = "Không đọc được dữ liệu thùng: " + ex.Message,
                    duLieu = new { tong = kq.Tong, tran = MesApiService.TranTongDong, dong = kq.Dong } });
            }
            return Json(new { thanhCong = true, thongBao = "", duLieu = new { tong = kq.Tong, tran = MesApiService.TranTongDong, dong = kq.Dong } });
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
                    thung = new { thung.IdThung, thung.MaThung, thung.TrangThai, thung.GhiChu, thung.NgayTao, thung.NguoiTao, thung.NgayDong, thung.NguoiDong,
                        thung.IdKe, thung.Tang, thung.O, ViTri = QaKeService.MaViTri(thung.Ke?.MaKe, thung.Tang, thung.O) },
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
                var kq = await _thung.BoVaoThungAsync(vao.MaPhoiMau ?? "", vao.DsDauVanTay ?? new(), vao.DsIdDongChuyen, vao.IdThung, vao.GhiChu, NguoiDung, ct);
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

        [HttpPost("/QAForm/Thung/{id:int}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThungXoa(int id, CancellationToken ct = default)
        {
            var kq = await _thung.XoaThungAsync(id, NguoiDung, ct);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao });
        }

        // ===== KỆ =====

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
