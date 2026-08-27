# Kế hoạch tổng thể — E-Form-Best

> Nguồn tham chiếu chính: [`BaoCao_TongKet_DuAn_EFormBest.md`](../../BaoCao_TongKet_DuAn_EFormBest.md)
> (báo cáo tổng kết kỳ 01/2026–07/2026: số liệu vận hành, rủi ro, roadmap gốc).
> File này **không chép lại** số liệu đó — chỉ nêu bối cảnh kỹ thuật + lộ trình để làm việc.

## 1. Hệ thống là gì

Nền tảng quản lý phiếu/đơn nội bộ đa phân hệ. **Một mã nguồn, một CSDL SQL Server**, chia theo
**Areas** của ASP.NET Core MVC — không phải microservice, không container.

| Hạng mục | Thực tế |
|---|---|
| Framework | ASP.NET Core MVC, `net10.0` (`E-Form-Best/E-Form-Best.csproj`) |
| ORM | EF Core 10 + `Microsoft.EntityFrameworkCore.SqlServer` |
| CSDL | SQL Server (`ITForm`), production `10.0.60.33`; chi nhánh chỉ-đọc `10.0.55.3` |
| DbContext | `E-Form-Best/Context/ITFormContext.cs` — **một context duy nhất**, scaffold DB-first, **không có thư mục `Migrations/`** |
| Xác thực | Cookie (`ITForm_Auth_Cookie`) + `SecurityStamp` kiểm ở `OnValidatePrincipal`; hybrid Domain (AD) / DB |
| View | Razor `.cshtml`, mỗi Area có `_Layout.cshtml` riêng; RuntimeCompilation bật ở Development |
| Frontend | Không build step. jQuery + lib tĩnh trong `wwwroot/lib`, JS/CSS dùng chung ở `wwwroot/js`, `wwwroot/css` |
| Nền chạy | Kestrel/IIS trên Windows, sau reverse proxy nginx (`UseForwardedHeaders`) |
| Health check | `GET /health`, `GET /health/ready` (khai báo trong `Program.cs`) |

## 2. Bản đồ 5 phân hệ (Areas)

| Area | Controller | Nghiệp vụ |
|---|---|---|
| `ITForm` | `ITFormController.cs` (~8.4k dòng, 111 action) | 8 loại phiếu IT + Kiểm kê thiết bị (KK_*) + Thu thập cấu hình máy (TSCN_*) |
| `HRform` | `HRFormController.cs` (~6.0k dòng) | 12 loại phiếu nhân sự, duyệt tối đa 4 cấp, phối hợp Bảo vệ/cổng |
| `SHDForm` | `SHDFormController.cs` (~2.6k dòng) | Đăng ký xe công tác/đưa đón, luồng duyệt tương tự HR |
| `QLCongViec` | `CongViecFormController.cs` (~2.8k dòng) | Giao việc chỉ định nội bộ, đánh giá sau hoàn tất |
| `AdminForm` | `AdminFormController.cs` (~330 dòng) | Menu trung tâm, tài khoản, push notification, thống kê hệ thống |

Ngoài ra: `Areas/ITForm/Services/AutoRatingWorker.cs` — `IHostedService` chạy nền lúc 0h.

## 3. Vấn đề kiến trúc đã biết (nợ kỹ thuật)

Đây là **hiện trạng thực tế**, ghi ra để không ai "phát hiện lại" và cũng để không vô tình nhân rộng:

1. **Controller khổng lồ** — `ITFormController` 8.4k dòng / 111 action, `HRFormController` 6k dòng.
   Không có tầng Service/Repository. Mọi thứ (truy vấn EF, dựng file Excel/Word/PDF, gọi AD, xử lý
   upload) nằm thẳng trong action.
2. **View khổng lồ trộn UI + logic** — `IndexThietBi.cshtml` 2.9k dòng, `ThongKeKiemKe.cshtml` 2.6k dòng,
   JS inline trong `<script>` ngay trong `.cshtml`. Chỉ 3 file JS được tách ra `wwwroot/js`.
3. **`new ITFormContext()` ngoài DI** — mỗi controller đều có, buộc `OnConfiguring` phải tự nạp
   `appsettings.json`. Vòng đời context không do DI quản lý.
4. **Không dùng Migration của EF Core** — schema thay đổi bằng SQL thủ công trên DB rồi mới sửa model.
   Xem [`../rules/database-safety.md`](../rules/database-safety.md).
5. **Connection string chứa tài khoản `sa` nằm trong `appsettings.json` đã commit vào git.**
6. **Bus factor = 1** — toàn bộ lịch sử commit do một người.

## 4. Lộ trình

Kế thừa roadmap trong báo cáo tổng kết, cụ thể hoá theo góc kỹ thuật:

| Giai đoạn | Mục tiêu | Chi tiết |
|---|---|---|
| **Phase 0 — Khung & DB** | Chuẩn hoá môi trường chạy, làm rõ hợp đồng schema | [`phase0-setup.md`](phase0-setup.md) |
| **Phase 1 — Tính năng cốt lõi** | Hoàn thiện 5 phân hệ + Kiểm kê/TSCN | [`phase1-core-features.md`](phase1-core-features.md) |
| **Phase 2 — Giảm nợ kỹ thuật** | Tách Service/Repository khỏi controller khổng lồ; tách JS inline khỏi view; bỏ `new ITFormContext()` | Làm dần theo module đang chạm, **không refactor ồ ạt** |
| **Phase 3 — Vận hành** | Thu hẹp quyền tài khoản SQL (bỏ `sa`), backup + restore test, log tập trung, giám sát dựa trên `/health/ready` | Theo đề xuất mục "Rủi ro" của báo cáo tổng kết |

Nguyên tắc xuyên suốt: **không đổi stack** (không thêm SPA framework, không đổi ORM, không chia
microservice) trừ khi có quyết định tường minh của người dùng — xem
[`../rules/project-scope.md`](../rules/project-scope.md).

## 5. Trạng thái hiện tại

Đang làm gì, vướng gì, đã quyết gì → [`00-context-memory.md`](00-context-memory.md).

## 6. Backlog Phase 2 — Chuyển form đồng bộ sang AJAX (bắt buộc về 0)

Ràng buộc: [`../rules/architecture-workflow.md`](../rules/architecture-workflow.md) mục 5.
Khảo sát 27/08/2026: 79 view / 35 thẻ `<form>`; ~537 điểm trả JSON so với **24** `RedirectToAction`
— codebase đã gần như AJAX toàn bộ, phần vi phạm còn lại là **22 form tạo đơn** cùng một khuôn
(`<form method="post" action="/FormXX/...">` → action POST → `RedirectToAction("DonCho")`,
không file nào có `preventDefault`).

| # | Area | View | Trạng thái |
|---|---|---|---|
| 1 | ITForm | `DonMail.cshtml` | ✅ **đã chuyển (pilot 27/08/2026)** — mẫu để nhân ra các form còn lại |
| 2–8 | ITForm | `DonCapQuyenOChung`, `DonDienThoaiBan`, `DonLapDatThietBi`, `DonTaiKhoanHeThong`, `DonTaiKhoanMayTinh`, `TaoIT_Order`, `TaoIT_Wifi` | ⬜ |
| 9–20 | HRform | `DonXinRaNgoai`, `MangHangHoaRaCong`, `DoiCaLam`, `DonHoTroCongTac`, `DonKiTucXa`, `DonLamLaiThe`, `DonSuDungDienThoai`, `DonTiepKhac`, `HoTroTienDienThoai`, `NhaThauQuaCong`, `DangKySuDungXeCongTac`, `DangKySuDungXeDaily` | ⬜ |
| 21 | HRform | `QuanLyPhongHop` → `/FormHR/LuuPhongHop` | ⬜ |
| 22 | SHDForm | `DangKySuDungXeCongTac` | ⬜ |
| 23 | QLCongViec | `DonCvCongViecOrder` | ⬜ |

**Không nằm trong backlog (điều hướng thật là đúng):**

| View / luồng | Lý do |
|---|---|
| `ITForm/DangNhap.cshtml` | Cookie auth + `returnUrl`; điều hướng là một phần của luồng đăng nhập |
| `HRform/XuatBaoCao`, `SHDForm/XuatBaoCao` | `method="get"` tải file Excel — không phải cập nhật UI |
| `ITForm/TaoMail.cshtml` | View chết, không route nào gọi tới → **xoá**, không chuyển |

### Công thức chuyển (đã kiểm chứng ở form pilot)

1. Action POST: bỏ `Redirect`/`return View(form)`, trả `Json(new { thanhCong, thongBao, idDon })`.
2. Thêm **chốt idempotency phía server** bù cho việc mất POST-Redirect-GET (xem `DonMail`:
   chặn đơn trùng cùng người tạo + cùng `IdForm` trong 30 giây).
3. Tách toàn bộ JS inline của view ra `wwwroot/js/<ten-don>.js`.
4. View: bỏ `method`/`action` khỏi `<form>`, chuyển sang `data-url`; nút bấm `type="submit"`,
   bỏ `onclick` inline; giữ `@Html.AntiForgeryToken()` trong form để `FormData` gửi kèm token.
5. JS: `submit` + `event.preventDefault()` → `fetch(FormData)` → SweetAlert2 báo kết quả,
   khoá nút khi đang gửi, thành công thì reset form tại chỗ (không tự điều hướng).
