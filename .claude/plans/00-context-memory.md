# Bộ nhớ bối cảnh — cập nhật liên tục

> File này là **trạng thái sống**. Sửa trực tiếp mỗi khi đổi việc / gặp blocker / chốt quyết định.
> Giữ ngắn: phần "Đang làm" tối đa vài dòng, mục cũ chuyển xuống Decision Log hoặc xoá.
> **Luôn ghi ngày tuyệt đối (dd/mm/yyyy)**, không viết "tuần trước", "hôm qua".

---

## Đang làm (cập nhật: 21/08/2026)

**Việc:** Module chặn thiết bị trong Kiểm kê IT — ẩn máy khỏi mọi danh sách/thống kê
(`KK_ThietBi` + `TSCN_ThongTinMay`) và chặn đồng bộ/thêm mới trở lại, đối chiếu theo Serial
(ưu tiên) hoặc Tên máy khi Serial rỗng.

**File đang chạm (theo `git status`):**

| File | Trạng thái |
|---|---|
| `E-Form-Best/Models/ITForm/KkThietBiChan.cs` | mới |
| `E-Form-Best/wwwroot/js/kiemke-chan-thietbi.js` | mới |
| `E-Form-Best/Context/ITFormContext.cs` | sửa (đăng ký DbSet) |
| `E-Form-Best/Areas/ITForm/Controllers/ITFormController.cs` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/IndexThietBi.cshtml` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/ViewDanhSachTatCaMayTinh.cshtml` | sửa |

**Nhánh:** `master` (commit gần nhất `4653ff5` — layout cố định + nút thu gọn sidebar).

---

## Blockers

| # | Vấn đề | Ảnh hưởng | Cần ai/ cái gì để gỡ |
|---|---|---|---|
| B1 | Bảng `KK_ThietBiChan` phải được tạo **thủ công** trên SQL Server production trước khi deploy — repo không có Migration | Deploy code mà chưa chạy DDL → lỗi runtime khi mở trang Kiểm kê | Người có quyền trên `10.0.60.33`; script DDL phải được duyệt trước (xem `../rules/database-safety.md`) |
| B2 | `appsettings.json` đang commit connection string tài khoản `sa` của cả 2 server | Rủi ro bảo mật; đổi mật khẩu là phải sửa file trong repo | Cấp tài khoản SQL riêng quyền tối thiểu + chuyển secret sang biến môi trường |
| B3 | Không có công cụ giám sát production; `/health/ready` đã có nhưng chưa có gì gọi nó | Sự cố chỉ biết khi người dùng báo | Cấu hình uptime check trỏ vào `/health/ready` |
| ~~B4~~ | ~~Bảng `IT_ThietKeTemIn_9` chưa được tạo trên production~~ | **ĐÃ GỠ 27/08/2026** | DDL đã chạy trên `10.0.60.33`; đối soát trước 0/0/0 → sau 1/1/1, bảng 0 đơn, FK `FK_ITThietKeTemIn_FormIT` (SET_NULL) đã có |

---

## Decision Log

Mỗi dòng: **ngày — quyết định — vì sao — hệ quả**. Chỉ ghi quyết định còn ảnh hưởng tới code hôm nay.

| Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|
| — (từ trước) | Dùng **EF Core DB-first**, không dùng `Migrations/` | Schema có sẵn/được quản trên SQL Server; nhiều bảng dùng chung với hệ thống khác | Mọi thay đổi schema là DDL thủ công + sửa model tay. Không chạy `dotnet ef migrations` |
| — (từ trước) | Một `ITFormContext` duy nhất cho cả 5 Area | Chung một CSDL, tránh trùng entity | Context rất lớn; thêm bảng = thêm `DbSet` vào đúng file này |
| — (từ trước) | Route bằng **attribute tuyệt đối** (`[HttpGet("/FormIT/...")]`) thay vì convention | URL nghiệp vụ không khớp `{area}/{controller}/{action}` | Đổi URL = sửa attribute; route convention trong `Program.cs` chỉ là fallback |
| 22/07/2026 (`8cdfa54`) | Cache dropdown Công ty/Bộ phận bằng `IMemoryCache`; cache static file 7 ngày, riêng `sw.js` `no-cache` | Giảm truy vấn lặp và tải lại file tĩnh | Sửa danh mục Công ty/Bộ phận **không hiện ngay** — phải chờ cache hết hạn hoặc invalidate |
| (`f56705c`) | Bật `UseForwardedHeaders`, xoá `KnownProxies/KnownIPNetworks` | nginx nằm máy khác nên không thuộc loopback tin cậy | An toàn **chỉ khi** Kestrel không mở trực tiếp ra Internet — giữ nguyên ràng buộc này |
| (`f56705c`) | Quy ước bản quyền Windows: chỉ MAK công ty + OEM là Đạt chuẩn; GVLK/Retail generic không tính | Quy định nội bộ | Mua key MAK mới → thêm 5 ký tự cuối vào `BanQuyenWindows:MakKeyCongTy` trong `appsettings.json`, **không sửa code** |
| (`599ba5f`) | Bật Razor RuntimeCompilation **chỉ ở Development** | Sửa `.cshtml` thấy ngay khi F5 | Production vẫn phải build lại khi đổi view |
| 27/08/2026 | Thêm đơn số 9 **Thiết kế tem in** (`IT_ThietKeTemIn_9`), nhân sự phụ trách là V200887 (Nguyễn Văn Phúc, `CongViecIT.id=1026`) | Nghiệp vụ mới của IT; bám đúng khuôn `FormIt + chi tiết + LichSu + NguoiHoTro` | Đổi người phụ trách = sửa dòng `CongViecIT` có `Ten = N'Thiết kế tem in'`, **không sửa code**. Đây là đơn đầu tiên đạt chuẩn không-reload ngay từ đầu |
| 27/08/2026 | Cho phép Claude chạy `sqlcmd` qua `.claude/settings.local.json` (không commit) | Chạy DDL đã duyệt mà không bị auto mode chặn | Quyền này là **toàn quyền ghi** trên CSDL bằng tài khoản `sa` — gắn liền với blocker B2. Gỡ quyền = xoá file đó |

---

## Ghi chú vận hành

- Chạy dự án: `cd E-Form-Best && dotnet watch run` (không dùng `dotnet run` trần).
- Log của `dotnet watch` đổ ra `watch_log.txt`, `dotnet-watch.log`, `dotnet-watch.err.log` ở gốc repo —
  **file rác, đã nằm trong `.claudeignore`, đừng commit.**

---

## Ràng buộc thường trực (không được quên giữa các phiên)

1. **View chỉ là View.** `.cshtml` không chứa business logic, không truy vấn EF, không tính toán
   nặng, không gọi API trực tiếp. Dữ liệu vào view qua Model/ViewBag do Controller/Service chuẩn bị.
2. **Thao tác trên web tuyệt đối không load lại trang** — xử lý 100% bằng JavaScript
   (`fetch`/Ajax + DOM), luôn `event.preventDefault()`, server trả JSON, cập nhật UI cục bộ.
   Ngoại lệ: đăng nhập/đăng xuất, mở trang chi tiết có URL riêng, tải file xuất.
3. **JS mới luôn nằm ở file riêng** `wwwroot/js/<ten-tinh-nang>.js`, không inline trong view.

Chi tiết: [`../rules/architecture-workflow.md`](../rules/architecture-workflow.md) mục 5 và
[`../rules/coding-standards.md`](../rules/coding-standards.md) mục 7.

> **Tuyệt đối, không trừ view cũ.** Chạm view nào còn submit đồng bộ → chuyển view đó sang AJAX
> ngay trong lần sửa. Danh sách form còn vi phạm: [`00-master-plan.md`](00-master-plan.md) mục 4.
