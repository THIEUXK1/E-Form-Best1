using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace E_Form_Best.Areas.AdminForm.Controllers
{
    [Area("AdminForm")]
    public class AdminFormController : Controller
    {
        public ITFormContext _context;
        public AdminFormController()
        {
            _context = new ITFormContext();
        }
        #region MenuA
        [HttpGet("/MenuA")]
        public IActionResult MenuA()
        {
            return View();
        }
        #endregion

        #region Lớp dùng deserialize API

        // Response gốc từ API
        private class QLtaiKhoanResponse
        {
            [JsonPropertyName("CODE")]
            public int CODE { get; set; }

            [JsonPropertyName("rows")]
            public Rows rows { get; set; }
        }

        private class Rows
        {
            [JsonPropertyName("employees")]
            public List<Employee> employees { get; set; }
        }

        private class Employee
        {
            [JsonPropertyName("ENAME")]
            public string ENAME { get; set; }

            [JsonPropertyName("STATUS")]
            public int STATUS { get; set; }

            [JsonPropertyName("DEPT_NAME")]
            public string DEPT_NAME { get; set; }

            [JsonPropertyName("DEPT_CODE")]
            public string DEPT_CODE { get; set; }

            [JsonPropertyName("POSITION_CODE")]
            public string POSITION_CODE { get; set; }

            [JsonPropertyName("EMPNO")]
            public string EMPNO { get; set; }

            [JsonPropertyName("USER_NAME")]
            public string USER_NAME { get; set; }

            [JsonPropertyName("ENTRY_DATE")]
            public string ENTRY_DATE { get; set; }

            [JsonPropertyName("POSITION_NAME")]
            public string POSITION_NAME { get; set; }

            [JsonPropertyName("PRO_LEAVE_DATE")]
            public string PRO_LEAVE_DATE { get; set; }
        }

        #endregion

        #region Đăng nhập quản lý

        // Hiển thị trang đăng nhập
        [HttpGet("/QL")]
        public IActionResult CheckTK()
        {
            return View();
        }

        // Xử lý đăng nhập
        [HttpPost("/CheckTK")]
        [ValidateAntiForgeryToken]
        public IActionResult CheckTK(string email, string matkhau)
        {
            // Kiểm tra tài khoản cố định với .\administrator
            if (email == @".\administrator" && matkhau == "bpvn-2017")
            {
                // Lưu thông tin vào session để check trạng thái đăng nhập
                HttpContext.Session.SetString("SS_Email", email);
                HttpContext.Session.SetString("SS_HoTen", "Administrator");
                HttpContext.Session.SetString("SS_VaiTro", "QuanLy");

                return Redirect("/QLtaiKhoan");
            }

            // Nếu không đúng, hiển thị lỗi
            ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
            return View();
        }

        // Logout
        [HttpGet("/Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            // Quay lại trang đăng nhập qua route /QL
            return Redirect("/QL");
        }

        // Kiểm tra đã đăng nhập hay chưa (Dựa trên Session Email)
        private bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("SS_Email"));
        }
        #endregion

        #region QL tài khoản

        // 1. Trang chính (Chỉ trả về View)
        [HttpGet("/QLtaiKhoan")]
        public IActionResult QLtaiKhoan()
        {
            if (!IsLoggedIn()) return Redirect("/QL");
            return View();
        }

        // 2. API lấy toàn bộ danh sách (Dùng cho AJAX load bảng)
        [HttpGet("/QLtaiKhoan/GetAllUsers")]
        public IActionResult GetAllUsers()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var nguoiDungs = _context.Users.OrderByDescending(u => u.IdNguoiDung).ToList();
            return Json(nguoiDungs);
        }

        // 3. Thêm tài khoản thủ công (Yêu cầu mới)
        [HttpPost("/QLtaiKhoan/ThemThuCong")]
        public async Task<IActionResult> ThemThuCong(User userModel)
        {
            if (!IsLoggedIn()) return Unauthorized();

            if (string.IsNullOrEmpty(userModel.Tk) || string.IsNullOrEmpty(userModel.HoTen))
                return BadRequest("Vui lòng nhập đầy đủ Mã tài khoản và Họ tên.");

            // Kiểm tra trùng tài khoản
            var isExist = _context.Users.Any(u => u.Tk == userModel.Tk);
            if (isExist) return BadRequest("Tài khoản (Mã NV) này đã tồn tại trên hệ thống.");

            var newUser = new User
            {
                HoTen = userModel.HoTen,
                Tk = userModel.Tk,
                PhongBan = userModel.PhongBan,
                VaiTro = userModel.VaiTro ?? "NhanVien",
                MatKhau = "abc12345", // Mật khẩu mặc định
                TrangThai = "Đang làm",
                NgayTao = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm mới thành công!" });
        }

        // 4. Đồng bộ toàn bộ nhân viên từ HR API
        [HttpPost("/QLtaiKhoan/DongBoNhanVien")]
        public async Task<IActionResult> DongBoNhanVien(string type)
        {
            if (!IsLoggedIn()) return Unauthorized();

            string url = "";
            string companyName = "";

            // Lấy định dạng yyyy-MM (Ví dụ: 2026-02)
            string currentYM = DateTime.Now.ToString("yyyy-MM");

            if (type == "PFVN")
            {
                url = "https://bptehr.bestpacific.com/ehr/open/rmt/getPfvn?ym=" + currentYM;
                companyName = "PFVN";
            }
            else
            {
                url = "https://bptehr.bestpacific.com/ehr/open/rmt/getBpvn?ym=" + currentYM;
                companyName = "BPVN";
            }

            using var client = new HttpClient();
            try
            {
                var response = await client.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<QLtaiKhoanResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.rows?.employees == null) return BadRequest(new { message = "Không có dữ liệu từ HR." });

                // Build a lookup of existing users by TK so we can both add new users and update existing ones
                var usersByTk = _context.Users.ToDictionary(u => u.Tk, StringComparer.OrdinalIgnoreCase);
                var newUsers = new List<User>();
                var updatedCount = 0;

                foreach (var emp in result.rows.employees)
                {
                    if (string.IsNullOrEmpty(emp?.EMPNO)) continue;

                    if (usersByTk.TryGetValue(emp.EMPNO, out var existingUser))
                    {
                        // Nếu user tồn tại nhưng chưa có TenCongTy thì cập nhật
                        if (string.IsNullOrEmpty(existingUser.TenCongTy))
                        {
                            existingUser.TenCongTy = companyName;
                            existingUser.PhongBan = existingUser.PhongBan ?? emp.DEPT_NAME;
                            existingUser.NgayCapNhat = DateTime.Now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        newUsers.Add(new User
                        {
                            HoTen = emp.USER_NAME,
                            Tk = emp.EMPNO,
                            MatKhau = "abc12345",
                            PhongBan = emp.DEPT_NAME,
                            VaiTro = "NhanVien",
                            TrangThai = "Đang làm",
                            TenCongTy = companyName, // Gán theo loại đồng bộ
                            NgayTao = DateTime.Now
                        });
                    }
                }

                if (newUsers.Any() || updatedCount > 0)
                {
                    if (newUsers.Any()) _context.Users.AddRange(newUsers);
                    await _context.SaveChangesAsync();

                    var messages = new List<string>();
                    if (newUsers.Any()) messages.Add($"Đã đồng bộ {newUsers.Count} nhân viên {companyName}");
                    if (updatedCount > 0) messages.Add($"Cập nhật {updatedCount} tài khoản thiếu tên công ty");

                    return Ok(new { message = string.Join("; ", messages) + "." });
                }

                return Ok(new { message = $"Dữ liệu nhân viên {companyName} đã mới nhất." });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // 5. Thêm một nhân viên cụ thể từ HR
        [HttpPost("/QLtaiKhoan/ThemNhanVien")]
        public async Task<IActionResult> ThemNhanVien(string empNo, string type = "BPVN")
        {
            if (!IsLoggedIn()) return Unauthorized();

            using var client = new HttpClient();
            string url = "https://bptehr.bestpacific.com/ehr/open/rmt/getBpvn?ym=2025-05";
            var companyName = type == "PFVN" ? "PFVN" : "BPVN";

            try
            {
                var response = await client.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<QLtaiKhoanResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var emp = result?.rows?.employees?.FirstOrDefault(e => e.EMPNO == empNo);
                if (emp == null) return NotFound("Không tìm thấy mã này trên hệ thống HR");

                if (_context.Users.Any(u => u.Tk == emp.EMPNO)) return BadRequest("Nhân viên này đã có tài khoản.");

                var user = new User
                {
                    HoTen = emp.USER_NAME,
                    Tk = emp.EMPNO,
                    MatKhau = "abc12345",
                    PhongBan = emp.DEPT_NAME,
                    VaiTro = "NhanVien",
                    TrangThai = emp.STATUS == 1 ? "Đang làm" : "Đã nghỉ",
                    TenCongTy = companyName,
                    NgayTao = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return Ok(user);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // 6. Cập nhật (Dùng cho nút Lưu trên bảng)
        [HttpPost("/QLtaiKhoan/CapNhatNguoiDung")]
        public async Task<IActionResult> CapNhatNguoiDung(int IdNguoiDung, string MatKhau, string VaiTro, string SoDienThoai)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var user = await _context.Users.FindAsync(IdNguoiDung);
            if (user == null) return NotFound("User không tồn tại");

            user.MatKhau = MatKhau;
            user.VaiTro = VaiTro;
            user.SoDienThoai = SoDienThoai;
            user.NgayCapNhat = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công" });
        }

        // 7. Xóa tài khoản
        [HttpPost("/QLtaiKhoan/XoaNguoiDung")]
        public async Task<IActionResult> XoaNguoiDung(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 8. Tìm kiếm nhanh từ HR
        [HttpGet("/QLtaiKhoan/GetNhanVien")]
        public async Task<IActionResult> GetNhanVien(string empNo)
        {
            if (!IsLoggedIn()) return Unauthorized();
            using var client = new HttpClient();
            string url = "https://bptehr.bestpacific.com/ehr/open/rmt/getBpvn?ym=2025-05";
            try
            {
                var response = await client.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<QLtaiKhoanResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var emp = result?.rows?.employees?.FirstOrDefault(e => e.EMPNO == empNo || e.USER_NAME.Contains(empNo));
                return Json(emp);
            }
            catch { return Json(null); }
        }

        #endregion

        #region QL người Hỗ trợ
        [HttpGet("/QLtaiKhoan/NguoiHoTro")]
        public IActionResult NguoiHoTro()
        {
            if (!IsLoggedIn()) return Redirect("/QL");
            return View();
        }

        [HttpGet("/QLtaiKhoan/GetAllNguoiHoTro")]
        public IActionResult GetAllNguoiHoTro()
        {
            if (!IsLoggedIn()) return Unauthorized();
            return Json(_context.ItNguoiHoTros.OrderByDescending(x => x.Id).ToList());
        }

        [HttpPost("/QLtaiKhoan/AddNguoiHoTro")]
        public async Task<IActionResult> AddNguoiHoTro(ItNguoiHoTro model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrEmpty(model.MaNv)) return BadRequest("Vui lòng nhập Mã nhân viên");
            _context.ItNguoiHoTros.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLtaiKhoan/UpdateNguoiHoTro")]
        public async Task<IActionResult> UpdateNguoiHoTro(ItNguoiHoTro model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var nht = await _context.ItNguoiHoTros.FindAsync(model.Id);
            if (nht == null) return NotFound();
            nht.MaNv = model.MaNv; nht.Ten = model.Ten; nht.BoPhan = model.BoPhan; nht.GhiChu = model.GhiChu;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/QLtaiKhoan/DeleteNguoiHoTro")]
        public async Task<IActionResult> DeleteNguoiHoTro(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var nht = await _context.ItNguoiHoTros.FindAsync(id);
            if (nht == null) return NotFound();
            _context.ItNguoiHoTros.Remove(nht);
            await _context.SaveChangesAsync();
            return Ok();
        }
        #endregion

        #region QL Công Việc
        [HttpGet("/QLtaiKhoan/GetAllCongViec")]
        public IActionResult GetAllCongViec()
        {
            if (!IsLoggedIn()) return Unauthorized();
            // Select ra object mới để tránh lỗi vòng lặp JSON và lấy được tên người hỗ trợ
            var list = _context.CongViecs
                .OrderByDescending(x => x.Id)
                .Select(x => new {
                    x.Id,
                    x.Ten,
                    x.TrangThai,
                    x.IdItNguoiHoTro,
                    TenNguoiHoTro = x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : "Chưa phân công"
                }).ToList();
            return Json(list);
        }

        [HttpPost("/QLtaiKhoan/AddCongViec")]
        public async Task<IActionResult> AddCongViec(CongViec model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            model.TrangThai = "Hiển thị"; // Mặc định là hiển thị như bạn yêu cầu
            _context.CongViecs.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLtaiKhoan/UpdateCongViec")]
        public async Task<IActionResult> UpdateCongViec(CongViec model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cv = await _context.CongViecs.FindAsync(model.Id);
            if (cv == null) return NotFound();
            cv.Ten = model.Ten;
            cv.IdItNguoiHoTro = model.IdItNguoiHoTro;
            // Có thể cập nhật trạng thái nếu cần, ở đây tôi giữ nguyên hoặc mặc định
            cv.TrangThai = model.TrangThai ?? "Hiển thị";
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/QLtaiKhoan/DeleteCongViec")]
        public async Task<IActionResult> DeleteCongViec(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cv = await _context.CongViecs.FindAsync(id);
            if (cv == null) return NotFound();
            _context.CongViecs.Remove(cv);
            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpPost("/QLtaiKhoan/UpdateStatusCV")]
        public async Task<IActionResult> UpdateStatusCV(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cv = await _context.CongViecs.FindAsync(id);
            if (cv == null) return NotFound();

            // Đổi trạng thái qua lại
            cv.TrangThai = (cv.TrangThai == "Hiển thị" || string.IsNullOrEmpty(cv.TrangThai)) ? "Ẩn" : "Hiển thị";

            await _context.SaveChangesAsync();
            return Ok(new { success = true, newStatus = cv.TrangThai });
        }
        #endregion  
    }
    }
