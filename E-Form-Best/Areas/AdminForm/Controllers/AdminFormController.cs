using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        public async Task<IActionResult> CapNhatNguoiDung(User updatedData)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var user = await _context.Users.FindAsync(updatedData.IdNguoiDung);
            if (user == null) return NotFound("Người dùng không tồn tại");

            // Cập nhật tất cả các trường từ form
            user.HoTen = updatedData.HoTen;
            user.Tk = updatedData.Tk;
            user.PhongBan = updatedData.PhongBan;
            user.TenCongTy = updatedData.TenCongTy;
            user.MatKhau = updatedData.MatKhau;
            user.VaiTro = updatedData.VaiTro;
            user.SoDienThoai = updatedData.SoDienThoai;
            user.TrangThai = updatedData.TrangThai;
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

        #region QL Phân quyền & Bộ phận nhân viên

        // 1. Lấy dữ liệu chi tiết quyền và bộ phận của 1 User (Dùng cho Modal Chi Tiết)
        [HttpGet("/QLtaiKhoan/GetUserDetail")]
        public IActionResult GetUserDetail(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var allQuyen = _context.Quyens.ToList();

            // Đảm bảo lấy trường MoTa từ bảng BoPhan
            var allBoPhan = _context.BoPhans
                .Select(b => new {
                    b.IdBoPhan,
                    b.TenBoPhan,
                    b.MoTa // Trường bạn vừa yêu cầu hiển thị
                })
                .ToList();

            var userQuyenIds = _context.UserQuyens
                .Where(uq => uq.IdNguoiDung == id)
                .Select(uq => uq.IdQuyen)
                .ToList();

            var userBoPhanIds = _context.UserBoPhans
                .Where(ub => ub.IdNguoiDung == id)
                .Select(ub => ub.IdBoPhan)
                .ToList();

            return Json(new
            {
                allQuyen,
                allBoPhan,
                userQuyenIds,
                userBoPhanIds
            });
        }

        // 2. Xử lý tích/bỏ tích Quyền (Thêm/Xóa bảng User_Quyen)
        [HttpPost("/QLtaiKhoan/TogglePermission")]
        public async Task<IActionResult> TogglePermission(int userId, int quyenId, bool isChecked)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var existing = _context.UserQuyens
                .FirstOrDefault(x => x.IdNguoiDung == userId && x.IdQuyen == quyenId);

            if (isChecked && existing == null)
            {
                _context.UserQuyens.Add(new UserQuyen
                {
                    IdNguoiDung = userId,
                    IdQuyen = quyenId
                });
            }
            else if (!isChecked && existing != null)
            {
                _context.UserQuyens.Remove(existing);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 3. Xử lý tích/bỏ tích Bộ Phận (Thêm/Xóa bảng User_BoPhan)
        [HttpPost("/QLtaiKhoan/ToggleDepartment")]
        public async Task<IActionResult> ToggleDepartment(int userId, int boPhanId, bool isChecked)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var existing = _context.UserBoPhans
                .FirstOrDefault(x => x.IdNguoiDung == userId && x.IdBoPhan == boPhanId);

            if (isChecked && existing == null)
            {
                _context.UserBoPhans.Add(new UserBoPhan
                {
                    IdNguoiDung = userId,
                    IdBoPhan = boPhanId,
                    NgayChiDinh = DateTime.Now
                });
            }
            else if (!isChecked && existing != null)
            {
                _context.UserBoPhans.Remove(existing);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        #endregion

        #region QL người Hỗ trợ IT
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

        #region QUẢN LÝ NGƯỜI HỖ TRỢ VÀ CÔNG VIỆC HR

        // GET: /QLtaiKhoan/NguoiHoTroHR
        [HttpGet("/QLtaiKhoan/NguoiHoTroHR")]
        public IActionResult NguoiHoTroHR()
        {
            if (!IsLoggedIn()) return Redirect("/QL");
            return View();
        }

        // --- PHẦN: NGƯỜI HỖ TRỢ HR (HrNguoiHoTro) ---

        [HttpGet("/QLtaiKhoan/GetAllNguoiHoTroHR")]
        public IActionResult GetAllNguoiHoTroHR()
        {
            if (!IsLoggedIn()) return Unauthorized();
            return Json(_context.HrNguoiHoTros.OrderByDescending(x => x.Id).ToList());
        }

        [HttpPost("/QLtaiKhoan/AddNguoiHoTroHR")]
        public async Task<IActionResult> AddNguoiHoTroHR(HrNguoiHoTro model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrEmpty(model.MaNv)) return BadRequest("Vui lòng nhập Mã nhân viên");

            _context.HrNguoiHoTros.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLtaiKhoan/UpdateNguoiHoTroHR")]
        public async Task<IActionResult> UpdateNguoiHoTroHR(HrNguoiHoTro model)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var nht = await _context.HrNguoiHoTros.FindAsync(model.Id);
            if (nht == null) return NotFound();

            nht.MaNv = model.MaNv;
            nht.Ten = model.Ten;
            nht.BoPhan = model.BoPhan;
            nht.GhiChu = model.GhiChu;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/QLtaiKhoan/DeleteNguoiHoTroHR")]
        public async Task<IActionResult> DeleteNguoiHoTroHR(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var nht = await _context.HrNguoiHoTros.FindAsync(id);
            if (nht == null) return NotFound();

            _context.HrNguoiHoTros.Remove(nht);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- PHẦN: CÔNG VIỆC HR (CongViecHr) ---

        [HttpGet("/QLtaiKhoan/GetAllCongViecHR")]
        public IActionResult GetAllCongViecHR()
        {
            if (!IsLoggedIn()) return Unauthorized();

            var data = _context.CongViecHrs
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Ten,
                    x.TrangThai,
                    x.IdHrNguoiHoTro,
                    // Lấy tên người hỗ trợ từ navigation property hoặc join
                    TenNguoiHoTroHR = x.IdHrNguoiHoTroNavigation != null ? x.IdHrNguoiHoTroNavigation.Ten : "Chưa phân công"
                }).ToList();

            return Json(data);
        }

        [HttpPost("/QLtaiKhoan/AddCongViecHR")]
        public async Task<IActionResult> AddCongViecHR(CongViecHr model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrEmpty(model.Ten)) return BadRequest("Vui lòng nhập tên công việc");

            model.TrangThai = "Hiển thị";
            _context.CongViecHrs.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLtaiKhoan/UpdateCongViecHR")]
        public async Task<IActionResult> UpdateCongViecHR(CongViecHr model)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var cv = await _context.CongViecHrs.FindAsync(model.Id);
            if (cv == null) return NotFound();

            cv.Ten = model.Ten;
            cv.IdHrNguoiHoTro = model.IdHrNguoiHoTro;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/QLtaiKhoan/UpdateStatusCVHR")]
        public async Task<IActionResult> UpdateStatusCVHR(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var cv = await _context.CongViecHrs.FindAsync(id);
            if (cv == null) return Json(new { success = false });

            cv.TrangThai = (cv.TrangThai == "Hiển thị") ? "Ẩn" : "Hiển thị";
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost("/QLtaiKhoan/DeleteCongViecHR")]
        public async Task<IActionResult> DeleteCongViecHR(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var cv = await _context.CongViecHrs.FindAsync(id);
            if (cv == null) return NotFound();

            _context.CongViecHrs.Remove(cv);
            await _context.SaveChangesAsync();
            return Ok();
        }

        #endregion

        #region Quản lý Giám đốc - Loại Đơn & Người Xác Nhận

        [HttpGet("/QLtaiKhoan/QLGiamDoc")]
        public IActionResult QLGiamDoc()
        {
            if (!IsLoggedIn()) return Redirect("/QL");
            return View();
        }

        // --- QUẢN LÝ LOẠI ĐƠN ---

        [HttpGet("/QLGiamDoc/GetAllLoaiDon")]
        public IActionResult GetAllLoaiDon()
        {
            if (!IsLoggedIn()) return Unauthorized();
            return Json(_context.DmLoaiDons.OrderByDescending(x => x.IdloaiDon).ToList());
        }

        [HttpPost("/QLGiamDoc/SaveLoaiDon")]
        public async Task<IActionResult> SaveLoaiDon(DmLoaiDon model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrEmpty(model.MaLoaiDon)) return BadRequest("Vui lòng nhập Mã loại đơn");

            if (model.IdloaiDon == 0)
            {
                model.NgayTao = DateTime.Now;
                _context.DmLoaiDons.Add(model);
            }
            else
            {
                var exist = await _context.DmLoaiDons.FindAsync(model.IdloaiDon);
                if (exist == null) return NotFound();
                exist.MaLoaiDon = model.MaLoaiDon;
                exist.TenLoaiDon = model.TenLoaiDon;
                exist.MoTa = model.MoTa;
                exist.TrangThai = model.TrangThai;
            }
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLGiamDoc/DeleteLoaiDon")]
        public async Task<IActionResult> DeleteLoaiDon(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var item = await _context.DmLoaiDons.FindAsync(id);
            if (item == null) return NotFound();

            if (_context.DmNguoiXacNhanLoaiDons.Any(x => x.IdloaiDon == id))
                return BadRequest("Loại đơn này đang có người xác nhận, không thể xóa.");

            _context.DmLoaiDons.Remove(item);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- QUẢN LÝ DANH MỤC NGƯỜI XÁC NHẬN ---

        [HttpGet("/QLGiamDoc/GetAllNguoiXacNhan")]
        public IActionResult GetAllNguoiXacNhan()
        {
            if (!IsLoggedIn()) return Unauthorized();
            return Json(_context.DmNguoiXacNhans.OrderByDescending(x => x.IdnguoiXacNhan).ToList());
        }

        [HttpPost("/QLGiamDoc/SaveNguoiXacNhan")]
        public async Task<IActionResult> SaveNguoiXacNhan(DmNguoiXacNhan model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrEmpty(model.MaNv)) return BadRequest("Vui lòng nhập Mã nhân viên");

            if (model.IdnguoiXacNhan == 0)
            {
                _context.DmNguoiXacNhans.Add(model);
            }
            else
            {
                var exist = await _context.DmNguoiXacNhans.FindAsync(model.IdnguoiXacNhan);
                if (exist == null) return NotFound();
                exist.MaNv = model.MaNv;
                exist.HoTen = model.HoTen;
                exist.Email = model.Email;
                exist.PhongBan = model.PhongBan;
                exist.ChucVu = model.ChucVu;
                exist.TrangThai = model.TrangThai;
            }
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLGiamDoc/DeleteNguoiXacNhan")]
        public async Task<IActionResult> DeleteNguoiXacNhan(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var item = await _context.DmNguoiXacNhans.FindAsync(id);
            if (item == null) return NotFound();

            if (_context.DmNguoiXacNhanLoaiDons.Any(x => x.IdnguoiXacNhan == id))
                return BadRequest("Người này đang tham gia quy trình xác nhận, không thể xóa.");

            _context.DmNguoiXacNhans.Remove(item);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- QUẢN LÝ LIÊN KẾT (BƯỚC DUYỆT) ---

        [HttpGet("/QLGiamDoc/GetCauHinhByLoaiDon")]
        public IActionResult GetCauHinhByLoaiDon(int idLoaiDon)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var list = _context.DmNguoiXacNhanLoaiDons
                .Where(x => x.IdloaiDon == idLoaiDon)
                .Include(x => x.IdnguoiXacNhanNavigation)
                .OrderBy(x => x.CapDoXacNhan)
                .Select(x => new {
                    x.Idrel,
                    x.IdloaiDon,
                    x.IdnguoiXacNhan,
                    x.CapDoXacNhan,
                    x.GhiChu,
                    TenNguoiXacNhan = x.IdnguoiXacNhanNavigation.HoTen
                })
                .ToList();
            return Json(list);
        }

        [HttpPost("/QLGiamDoc/SaveCauHinh")]
        public async Task<IActionResult> SaveCauHinh(DmNguoiXacNhanLoaiDon model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (model.Idrel == 0)
            {
                _context.DmNguoiXacNhanLoaiDons.Add(model);
            }
            else
            {
                var exist = await _context.DmNguoiXacNhanLoaiDons.FindAsync(model.Idrel);
                if (exist == null) return NotFound();
                exist.IdnguoiXacNhan = model.IdnguoiXacNhan;
                exist.CapDoXacNhan = model.CapDoXacNhan;
                exist.GhiChu = model.GhiChu;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/QLGiamDoc/DeleteCauHinh")]
        public async Task<IActionResult> DeleteCauHinh(int idRel)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var item = await _context.DmNguoiXacNhanLoaiDons.FindAsync(idRel);
            if (item == null) return NotFound();
            _context.DmNguoiXacNhanLoaiDons.Remove(item);
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
            var list = _context.CongViecIts
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Ten,
                    x.TrangThai,
                    x.IdItNguoiHoTro,
                    TenNguoiHoTro = x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : "Chưa phân công"
                }).ToList();
            return Json(list);
        }

        [HttpPost("/QLtaiKhoan/AddCongViec")]
        public async Task<IActionResult> AddCongViec(CongViecIt model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            model.TrangThai = "Hiển thị"; // Mặc định là hiển thị như bạn yêu cầu
            _context.CongViecIts.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpPost("/QLtaiKhoan/UpdateCongViec")]
        public async Task<IActionResult> UpdateCongViec(CongViecIt model)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cv = await _context.CongViecIts.FindAsync(model.Id);
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
            var cv = await _context.CongViecIts.FindAsync(id);
            if (cv == null) return NotFound();
            _context.CongViecIts.Remove(cv);
            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpPost("/QLtaiKhoan/UpdateStatusCV")]
        public async Task<IActionResult> UpdateStatusCV(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cv = await _context.CongViecIts.FindAsync(id);
            if (cv == null) return NotFound();

            // Đổi trạng thái qua lại
            cv.TrangThai = (cv.TrangThai == "Hiển thị" || string.IsNullOrEmpty(cv.TrangThai)) ? "Ẩn" : "Hiển thị";

            await _context.SaveChangesAsync();
            return Ok(new { success = true, newStatus = cv.TrangThai });
        }
        #endregion

        #region QL Bộ Phận

        [HttpGet("/QLBoPhanAamin")]
        public IActionResult QLBoPhanAamin()
        {
            // Sắp xếp danh sách user theo tên để dễ tìm kiếm trên View
            ViewBag.Users = _context.Users.OrderBy(u => u.HoTen).ToList();
            return View();
        }

        [HttpGet("/QLBoPhanAamin/GetListBoPhan")]
        public IActionResult GetListBoPhan()
        {
            var data = _context.BoPhans.OrderByDescending(x => x.IdBoPhan).ToList();
            return Json(new { data = data });
        }

        [HttpPost("/QLBoPhanAamin/SaveBoPhan")]
        public IActionResult SaveBoPhan(BoPhan model)
        {
            try
            {
                if (model.IdBoPhan == 0)
                {
                    model.NgayTao = DateTime.Now;
                    _context.BoPhans.Add(model);
                }
                else
                {
                    var existing = _context.BoPhans.Find(model.IdBoPhan);
                    if (existing != null)
                    {
                        existing.TenBoPhan = model.TenBoPhan;
                        existing.MoTa = model.MoTa;
                    }
                }
                _context.SaveChanges();
                return Json(new { success = true, msg = "Thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLBoPhanAamin/DeleteBoPhan")]
        public IActionResult DeleteBoPhan(int id)
        {
            try
            {
                var item = _context.BoPhans.Find(id);
                if (item != null)
                {
                    // Chặn xóa nếu bộ phận đang có nhân sự
                    var hasUsers = _context.UserBoPhans.Any(x => x.IdBoPhan == id);
                    if (hasUsers) return Json(new { success = false, msg = "Bộ phận này đang có nhân sự, không thể xóa!" });

                    _context.BoPhans.Remove(item);
                    _context.SaveChanges();
                }
                return Json(new { success = true, msg = "Đã xóa thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLBoPhanAamin/DeleteSelected")]
        public IActionResult DeleteSelected(List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0) return Json(new { success = false, msg = "Chưa chọn dòng nào!" });
                var listToDelete = _context.BoPhans.Where(x => ids.Contains(x.IdBoPhan)).ToList();
                int deletedCount = 0;
                string errorMsg = "";
                foreach (var item in listToDelete)
                {
                    if (!_context.UserBoPhans.Any(x => x.IdBoPhan == item.IdBoPhan))
                    {
                        _context.BoPhans.Remove(item);
                        deletedCount++;
                    }
                    else { errorMsg += $"- {item.TenBoPhan} đang có nhân sự.\n"; }
                }
                _context.SaveChanges();
                return Json(new { success = true, msg = $"Đã xóa {deletedCount} mục." + (string.IsNullOrEmpty(errorMsg) ? "" : "\nLỗi:\n" + errorMsg) });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLBoPhanAamin/SyncFromUser")]
        public IActionResult SyncFromUser()
        {
            try
            {
                var listFromUser = _context.Users
                    .Where(u => !string.IsNullOrEmpty(u.PhongBan) && !string.IsNullOrEmpty(u.TenCongTy))
                    .Select(u => new { TenBP = u.PhongBan.Trim(), MoTaBP = u.TenCongTy.Trim() })
                    .Distinct().ToList();

                int count = 0;
                foreach (var item in listFromUser)
                {
                    if (!_context.BoPhans.Any(x => x.TenBoPhan == item.TenBP))
                    {
                        _context.BoPhans.Add(new BoPhan { TenBoPhan = item.TenBP, MoTa = item.MoTaBP, NgayTao = DateTime.Now });
                        count++;
                    }
                }
                _context.SaveChanges();
                return Json(new { success = true, msg = $"Đã đồng bộ {count} bộ phận mới!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // --- CÁC ACTION QUẢN LÝ NHÂN SỰ ---

        [HttpGet("/QLBoPhanAamin/GetUsersByBP")]
        public IActionResult GetUsersByBP(int idBoPhan)
        {
            var data = (from ub in _context.UserBoPhans
                        join u in _context.Users on ub.IdNguoiDung equals u.IdNguoiDung
                        where ub.IdBoPhan == idBoPhan
                        select new
                        {
                            u.IdNguoiDung,
                            HoTen = u.HoTen,
                            TaiKhoan = u.Tk,
                            Loai = ub.Loai
                        }).ToList();
            return Json(new { data = data });
        }

        /// <summary>
        /// Thêm User vào bộ phận. 
        /// Đã sửa: Nếu tồn tại thì cập nhật 'Loai', tránh trả về lỗi để UI mượt mà.
        /// </summary>
        [HttpPost("/QLBoPhanAamin/AddUserToBP")]
        public IActionResult AddUserToBP(int idUser, int idBP, string loai)
        {
            try
            {
                var existing = _context.UserBoPhans.FirstOrDefault(x => x.IdBoPhan == idBP && x.IdNguoiDung == idUser);

                if (existing != null)
                {
                    existing.Loai = loai;
                    existing.NgayChiDinh = DateTime.Now; // Cập nhật lại ngày gán mới nhất
                }
                else
                {
                    _context.UserBoPhans.Add(new UserBoPhan
                    {
                        IdBoPhan = idBP,
                        IdNguoiDung = idUser,
                        Loai = loai,
                        NgayChiDinh = DateTime.Now
                    });
                }

                _context.SaveChanges();
                return Json(new { success = true, msg = "Đã cập nhật nhân sự vào bộ phận." });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLBoPhanAamin/RemoveUserFromBP")]
        public IActionResult RemoveUserFromBP(int idUser, int idBP)
        {
            try
            {
                var item = _context.UserBoPhans.FirstOrDefault(x => x.IdBoPhan == idBP && x.IdNguoiDung == idUser);
                if (item != null)
                {
                    _context.UserBoPhans.Remove(item);
                    _context.SaveChanges();
                }
                return Json(new { success = true, msg = "Đã gỡ nhân sự khỏi bộ phận." });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        /// <summary>
        /// Lấy danh sách ID các bộ phận mà User hiện tại đã thuộc về
        /// </summary>
        [HttpGet("/QLBoPhanAamin/GetUserCurrentBPs")]
        public IActionResult GetUserCurrentBPs(int idUser)
        {
            try
            {
                var ids = _context.UserBoPhans
                    .Where(x => x.IdNguoiDung == idUser)
                    .Select(x => x.IdBoPhan)
                    .ToList();
                return Json(new { success = true, data = ids });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        /// <summary>
        /// Gán hàng loạt (Giữ lại để hỗ trợ các chức năng xử lý theo mảng nếu cần)
        /// </summary>
        [HttpPost("/QLBoPhanAamin/AssignMultipleBP")]
        public IActionResult AssignMultipleBP(int IdNguoiDung, List<int> IdsBoPhan, string Loai)
        {
            try
            {
                if (IdNguoiDung <= 0) return Json(new { success = false, msg = "Vui lòng chọn nhân viên!" });
                if (IdsBoPhan == null || IdsBoPhan.Count == 0) return Json(new { success = false, msg = "Hãy chọn ít nhất một bộ phận!" });

                int countAdded = 0;
                foreach (var idBP in IdsBoPhan)
                {
                    var existing = _context.UserBoPhans.FirstOrDefault(x => x.IdNguoiDung == IdNguoiDung && x.IdBoPhan == idBP);
                    if (existing == null)
                    {
                        _context.UserBoPhans.Add(new UserBoPhan
                        {
                            IdNguoiDung = IdNguoiDung,
                            IdBoPhan = idBP,
                            Loai = Loai,
                            NgayChiDinh = DateTime.Now
                        });
                        countAdded++;
                    }
                    else
                    {
                        existing.Loai = Loai; // Đồng bộ lại loại nếu đã tồn tại
                    }
                }
                _context.SaveChanges();
                return Json(new { success = true, msg = $"Đã xử lý {IdsBoPhan.Count} bộ phận cho nhân viên!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        #endregion

        #region Ql Quyền Truy Cập

        [HttpGet("/QLQuyenTruyCap")]
        public IActionResult QLQuyenTruyCap()
        {
            // Lấy danh sách sắp xếp theo tên quyền
            var model = _context.Quyens.OrderBy(q => q.TenQuyen).ToList();
            return View(model);
        }

        // 1. Thêm mới quyền
        [HttpPost("/QLQuyenTruyCap/Create")]
        public IActionResult CreateQuyen(Quyen quyen)
        {
            if (ModelState.IsValid)
            {
                _context.Quyens.Add(quyen);
                _context.SaveChanges();
                return Json(new { success = true, message = "Thêm mới thành công!" });
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
        }

        // 2. Cập nhật quyền
        [HttpPost("/QLQuyenTruyCap/Edit")]
        public IActionResult EditQuyen(Quyen quyen)
        {
            var existing = _context.Quyens.Find(quyen.IdQuyen);
            if (existing != null)
            {
                existing.TenQuyen = quyen.TenQuyen;
                existing.MoTa = quyen.MoTa;

                _context.Quyens.Update(existing);
                _context.SaveChanges();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy dữ liệu." });
        }

        // 3. Xóa một hoặc nhiều quyền bằng danh sách ID
        [HttpPost("/QLQuyenTruyCap/DeleteMultiple")]
        public IActionResult DeleteMultiple([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một mục để xóa." });
            }

            try
            {
                var itemsToDelete = _context.Quyens.Where(q => ids.Contains(q.IdQuyen)).ToList();

                // Kiểm tra xem quyền có đang được gán cho User nào không để tránh lỗi khóa ngoại
                // (Tùy chọn: Bạn có thể xóa liên kết ở bảng trung gian trước nếu cần)

                _context.Quyens.RemoveRange(itemsToDelete);
                _context.SaveChanges();

                return Json(new { success = true, message = $"Đã xóa thành công {itemsToDelete.Count} mục." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }

        #endregion

        #region Thông Tin Cá Nhân & Ảnh Đại Diện

        [HttpGet("/ThongTinTaiKhoan")]
        [Authorize]
        public async Task<IActionResult> ThongTinTaiKhoan()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return RedirectToAction("Login");

            int userId = int.Parse(userIdClaim);
            var user = _context.Users.FirstOrDefault(u => u.IdNguoiDung == userId);

            // Truyền trực tiếp đường dẫn ảnh từ Database vào ViewBag để View hiển thị
            ViewBag.AnhDaiDien = user?.AnhDaiDien;

            return View();
        }

        [HttpPost("/DonXetDuyet/CapNhatAnh")]
        [Authorize]
        public async Task<IActionResult> CapNhatAnh(IFormFile fileAnh)
        {
            if (fileAnh == null || fileAnh.Length == 0) return Json(new { success = false, message = "File không hợp lệ." });

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                // 1. Xóa ảnh cũ nếu có (để dọn dẹp bộ nhớ File Server)
                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    System.IO.File.Delete(user.AnhDaiDien);
                }

                // 2. Lưu ảnh mới vào File Server
                string folderPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\User";
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"Avatar_{userId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileAnh.FileName)}";
                string fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileAnh.CopyToAsync(stream);
                }

                // 3. Lưu đường dẫn mới vào DB
                user.AnhDaiDien = fullPath;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/DonXetDuyet/XoaAnhDaiDien")]
        [Authorize]
        public async Task<IActionResult> XoaAnhDaiDien()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    System.IO.File.Delete(user.AnhDaiDien); // Xóa file vật lý trong thư mục
                }

                user.AnhDaiDien = null; // Gán lại null trong DB
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/DonXetDuyet/GetAvatar")]
        [Authorize]
        public IActionResult GetAvatar(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return NotFound();
            }
            var image = System.IO.File.OpenRead(path);
            return File(image, "image/jpeg"); // Trả về FileStream cho thẻ <img>
        }

        #endregion

        #region QL Phòng họp HR

        [HttpGet("/QLPhongHopHR")]
        public IActionResult QLPhongHopHR()
        {
            return View();
        }

        // Địa chỉ lấy danh sách: /QLPhongHopHR/GetData
        [HttpGet("/QLPhongHopHR/GetData")]
        public JsonResult GetPhongHopData()
        {
            var data = _context.PhongHopHrs.ToList();
            return Json(new { data = data });
        }

        // Địa chỉ lưu/cập nhật: /QLPhongHopHR/Save
        [HttpPost("/QLPhongHopHR/Save")]
        public IActionResult SavePhongHop(PhongHopHr model)
        {
            if (model.Id == 0)
            {
                _context.PhongHopHrs.Add(model);
            }
            else
            {
                _context.Update(model);
            }
            _context.SaveChanges();
            return Json(new { success = true });
        }

        // Địa chỉ xóa: /QLPhongHopHR/Delete
        [HttpPost("/QLPhongHopHR/Delete")]
        public IActionResult DeletePhongHop(int id)
        {
            var item = _context.PhongHopHrs.Find(id);
            if (item != null)
            {
                _context.PhongHopHrs.Remove(item);
                _context.SaveChanges();
            }
            return Json(new { success = true });
        }

        #endregion

        #region QUẢN LÝ QUY TRÌNH DUYỆT BƯỚC 2 - FULL CRUD

        [HttpGet("/HR_QuanLyDuyetB2")]
        public async Task<IActionResult> HR_QuanLyDuyetB2()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            // Load dữ liệu cho các Dropdownlist trong View
            ViewBag.BoPhan = await _context.DmBoPhans.AsNoTracking().Where(x => x.TrangThai == true).OrderBy(x => x.TenBoPhan).ToListAsync();
            ViewBag.LoaiDon = await _context.DmLoaiDons.AsNoTracking().Where(x => x.TrangThai == true).OrderBy(x => x.TenLoaiDon).ToListAsync();
            ViewBag.CongTy = await _context.DmCongTies.AsNoTracking().Where(x => x.TrangThai == true).OrderBy(x => x.TenCongTy).ToListAsync();
            ViewBag.NguoiXacNhan = await _context.DmNguoiXacNhans.AsNoTracking().Where(x => x.TrangThai == true).OrderBy(x => x.HoTen).ToListAsync();

            return View();
        }

        // ==========================================
        // 1. QUẢN LÝ CHÍNH (DM_NguoiDuyet_LoaiDon_BoPhan)
        // ==========================================

        [HttpGet("/HR_QuanLyDuyetB2/GetData")]
        public async Task<IActionResult> HR_GetData()
        {
            var data = await _context.DmNguoiDuyetLoaiDonBoPhans
                .AsNoTracking()
                .Select(x => new {
                    x.Id,
                    x.Stt,
                    x.IdloaiDon,
                    x.IdboPhan,
                    x.IdcongTy,
                    x.IdnguoiXacNhan,
                    TenLoaiDon = x.IdloaiDonNavigation.TenLoaiDon ?? "---",
                    TenCongTy = x.IdcongTyNavigation.TenCongTy ?? "---",
                    TenBoPhan = x.IdboPhanNavigation.TenBoPhan ?? "Tất cả",
                    HoTenNguoiDuyet = x.IdnguoiXacNhanNavigation.HoTen ?? "Chưa xác định",
                    x.GhiChu,
                    x.TrangThai
                })
                .OrderBy(x => x.TenCongTy)
                .ThenBy(x => x.TenLoaiDon)
                .ThenBy(x => x.Stt)
                .ToListAsync();

            return Json(new { data = data });
        }

        [HttpPost("/HR_QuanLyDuyetB2/Save")]
        public async Task<IActionResult> HR_Save([FromBody] DmNguoiDuyetLoaiDonBoPhan model)
        {
            try
            {
                // 1. Kiểm tra trùng lặp (Logic Business)
                var isDuplicate = await _context.DmNguoiDuyetLoaiDonBoPhans
                    .AnyAsync(x => x.Id != model.Id &&
                                   x.IdcongTy == model.IdcongTy &&
                                   x.IdloaiDon == model.IdloaiDon &&
                                   x.IdboPhan == model.IdboPhan &&
                                   x.Stt == model.Stt);

                if (isDuplicate)
                    return Json(new { success = false, message = "Cấu hình này đã tồn tại (Trùng Công ty, Loại đơn, Bộ phận và STT)!" });

                if (model.Id == 0)
                {
                    // Thêm mới
                    model.NgayTao = DateTime.Now;
                    _context.DmNguoiDuyetLoaiDonBoPhans.Add(model);
                }
                else
                {
                    // Cập nhật
                    var upd = await _context.DmNguoiDuyetLoaiDonBoPhans.FindAsync(model.Id);
                    if (upd == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu để cập nhật!" });

                    upd.Stt = model.Stt;
                    upd.IdloaiDon = model.IdloaiDon;
                    upd.IdboPhan = model.IdboPhan;
                    upd.IdcongTy = model.IdcongTy;
                    upd.IdnguoiXacNhan = model.IdnguoiXacNhan;
                    upd.GhiChu = model.GhiChu;
                    upd.TrangThai = model.TrangThai;
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Lưu dữ liệu thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPost("/HR_QuanLyDuyetB2/Delete/{id}")]
        public async Task<IActionResult> HR_Delete(int id)
        {
            try
            {
                var item = await _context.DmNguoiDuyetLoaiDonBoPhans.FindAsync(id);
                if (item == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu!" });

                _context.DmNguoiDuyetLoaiDonBoPhans.Remove(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Không thể xóa: " + ex.Message }); }
        }

        // ==========================================
        // 2. QUẢN LÝ CÔNG TY
        // ==========================================

        [HttpGet("/HR_QuanLyDuyetB2/GetCongTy")]
        public async Task<IActionResult> HR_GetCongTy()
        {
            var data = await _context.DmCongTies.AsNoTracking().OrderBy(x => x.TenCongTy).ToListAsync();
            return Json(new { data = data });
        }

        [HttpPost("/HR_QuanLyDuyetB2/SaveCongTy")]
        public async Task<IActionResult> HR_SaveCongTy([FromBody] DmCongTy model)
        {
            try
            {
                if (model.IdcongTy == 0)
                {
                    model.NgayTao = DateTime.Now;
                    _context.DmCongTies.Add(model);
                }
                else
                {
                    var upd = await _context.DmCongTies.FindAsync(model.IdcongTy);
                    if (upd == null) return Json(new { success = false, message = "Không tìm thấy công ty!" });
                    upd.TenCongTy = model.TenCongTy;
                    upd.GhiChu = model.GhiChu;
                    upd.TrangThai = model.TrangThai;
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // ==========================================
        // 3. QUẢN LÝ LOẠI ĐƠN
        // ==========================================

        [HttpGet("/HR_QuanLyDuyetB2/GetLoaiDon")]
        public async Task<IActionResult> HR_GetLoaiDon()
        {
            var data = await _context.DmLoaiDons.AsNoTracking().OrderBy(x => x.TenLoaiDon).ToListAsync();
            return Json(new { data = data });
        }

        [HttpPost("/HR_QuanLyDuyetB2/SaveLoaiDon")]
        public async Task<IActionResult> HR_SaveLoaiDon([FromBody] DmLoaiDon model)
        {
            try
            {
                if (model.IdloaiDon == 0)
                {
                    model.NgayTao = DateTime.Now;
                    _context.DmLoaiDons.Add(model);
                }
                else
                {
                    var exist = await _context.DmLoaiDons.FindAsync(model.IdloaiDon);
                    if (exist == null) return Json(new { success = false, message = "Không tìm thấy loại đơn!" });
                    exist.MaLoaiDon = model.MaLoaiDon;
                    exist.TenLoaiDon = model.TenLoaiDon;
                    exist.MoTa = model.MoTa;
                    exist.TrangThai = model.TrangThai;
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // ==========================================
        // 4. BỘ PHẬN & ĐỒNG BỘ (Sync)
        // ==========================================

        [HttpGet("/HR_QuanLyDuyetB2/GetBoPhan")]
        public async Task<IActionResult> HR_GetBoPhan()
        {
            var data = await _context.DmBoPhans.AsNoTracking().OrderBy(x => x.TenBoPhan).ToListAsync();
            return Json(new { data = data });
        }

        [HttpPost("/HR_QuanLyDuyetB2/SyncBoPhan")]
        public async Task<IActionResult> HR_SyncBoPhan()
        {
            try
            {
                var nguon = await _context.BoPhans.AsNoTracking().ToListAsync();
                var dich = await _context.DmBoPhans.ToListAsync();

                int added = 0, updated = 0;

                foreach (var b in nguon)
                {
                    var tonTai = dich.FirstOrDefault(x => x.IdboPhan == b.IdBoPhan);
                    if (tonTai == null)
                    {
                        _context.DmBoPhans.Add(new DmBoPhan
                        {
                            IdboPhan = b.IdBoPhan, // Gán ID từ nguồn nếu DB đích không tự tăng Identity
                            TenBoPhan = b.TenBoPhan,
                            GhiChu = b.MoTa,
                            NgayTao = DateTime.Now,
                            TrangThai = true
                        });
                        added++;
                    }
                    else
                    {
                        if (tonTai.TenBoPhan != b.TenBoPhan || tonTai.GhiChu != b.MoTa)
                        {
                            tonTai.TenBoPhan = b.TenBoPhan;
                            tonTai.GhiChu = b.MoTa;
                            updated++;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Đồng bộ thành công! Thêm mới: {added}, Cập nhật: {updated}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi đồng bộ: " + ex.Message });
            }
        }

        #endregion
    }

    }
