# Tiêu chuẩn code — E-Form-Best

Ngắn gọn, dạng bảng. Quy ước của repo **thắng** sở thích cá nhân.

## 1. Naming

| Đối tượng | Quy ước | Ví dụ thật trong repo |
|---|---|---|
| Namespace | `E_Form_Best.<Folder>` | `E_Form_Best.Areas.ITForm.Controllers` |
| Class entity | PascalCase, tiếng Việt không dấu | `KkThietBi`, `HrXinRaNgoai1`, `DmCongTy` |
| Tiền tố entity theo miền | `Kk` (kiểm kê), `Tscn` (cấu hình máy), `It`/`Hr`/`Shd`/`Cv` (phiếu), `Dm` (danh mục), `DaoTao` (đào tạo ISO) | `KkLoaiThietBi`, `TscnThongTinMay` |
| Hậu tố số ở model chi tiết phiếu | = mã loại đơn trong `DmLoaiDon` | `ItMail1`, `HrMangHangHoaRaCong2` |
| Property | PascalCase | `IdThietBi`, `NgayCapNhat` |
| Cột SQL | snake_case, **luôn khai `[Column]` tường minh** | `[Column("id_thiet_bi")]` |
| Bảng SQL | **luôn khai `[Table]` tường minh** | `[Table("KK_ThietBi")]` |
| Controller | `<Ten>Controller` | `ITFormController`, `CongViecFormController` |
| Action | PascalCase tiếng Việt không dấu | `DonMail`, `ThemNguoiHoTro`, `ExportExcel` |
| View | PascalCase, trùng tên action | `IndexThietBi.cshtml` |
| File JS | kebab-case, mô tả tính năng | `kiemke-chan-thietbi.js`, `fixed-page-layout.js` |
| Biến/tham số local | camelCase | `idThietBi`, `dsCongTy` |

## 2. C# / ASP.NET Core

| Quy tắc | Ghi chú |
|---|---|
| `Nullable` + `ImplicitUsings` đang **bật** | Không thêm `using System;` thừa; xử lý null tử tế, không rải `!` |
| **Attribute routing tuyệt đối** | `[HttpGet("/FormIT/ChiTiet/{id}")]` — không dựa vào route convention |
| Route mới phải đặt **trên** `homeActions` trong `Program.cs` | Route đó bắt `{action}/{id?}`, rất "tham" |
| Ưu tiên `async`/`await` cho mọi truy vấn EF | `ToListAsync`, `FirstOrDefaultAsync` |
| DbContext qua **constructor injection** cho code mới | `new ITFormContext()` là nợ kỹ thuật, không nhân rộng |
| Không `SELECT *` trên bảng rộng | Dùng `.Select(...)` lấy đúng cột cần |
| Cẩn thận **N+1** | Dùng `Include`/`Select` thay vì query trong vòng lặp |
| Dữ liệu tra cứu ít đổi → `IMemoryCache` | Đã áp dụng cho dropdown Công ty/Bộ phận. Nhớ: sửa danh mục **không hiện ngay** |
| Truy vấn phải **dịch được sang SQL** | `Contains` trên list ngoài, hàm .NET lạ → EF nổ runtime. Đã từng dính (commit `355993a`) |
| Raw SQL: chỉ dùng tham số hoá | `SqlParameter`, không nối chuỗi giá trị người dùng |
| Kiểm quyền ở **tầng server** | Ẩn nút trên UI không phải là phân quyền. Coi chừng IDOR khi nhận `id` từ URL |
| Ngày giờ | Cột `datetime` (`TypeName = "datetime"`), giờ máy chủ VN. Không trộn `DateTimeOffset` |

## 3. Razor / Frontend

| Quy tắc | Ghi chú |
|---|---|
| View **chỉ hiển thị** | Không truy vấn EF, không logic nghiệp vụ trong `.cshtml` |
| JS mới → `wwwroot/js/<ten>.js` | Không viết inline `<script>` cho code mới |
| Dùng jQuery + lib có sẵn trong `wwwroot/lib` | **Không thêm framework SPA / build step** — xem `project-scope.md` |
| Escape dữ liệu ra HTML | Razor `@` tự escape; dùng `Html.Raw`/`innerHTML` phải có lý do rõ |
| Layout riêng từng Area | Sửa `_Layout.cshtml` của Area nào chỉ ảnh hưởng Area đó — kiểm tra cả 5 nếu sửa dùng chung |
| Thêm file tĩnh nhớ cache 7 ngày | Đổi nội dung file cũ → cân nhắc đổi tên/thêm query version |

## 4. Comment & tài liệu

- Comment **tiếng Việt có dấu**, giải thích **tại sao**, không mô tả lại code.
  Mẫu tốt: phần giải thích `UseForwardedHeaders` và `OnPrepareResponse` trong `Program.cs`.
- Entity mới nên có `/// <summary>` nêu vai trò nghiệp vụ (xem `KkThietBiChan.cs`).
- Mật độ comment bám theo file xung quanh, không nhiều hơn.

## 5. Design pattern đang dùng

| Pattern | Hiện trạng |
|---|---|
| MVC + Areas | Chuẩn của dự án |
| DB-first EF Core, entity `partial` | Không có `Migrations/` |
| `IHostedService` cho job nền | `AutoRatingWorker` (0h hằng ngày) |
| Cookie Auth + `SecurityStamp` | Đổi stamp trong DB = đá toàn bộ phiên của user đó |
| **Chưa có** Service/Repository layer | Logic nằm trong controller. Code mới **nên** tách sang `Areas/<Area>/Services/` khi đủ lớn |

## 6. Terminal Output, Zero-Fluff & Lazy-Loading

Ba nhóm quy tắc tiết kiệm token chỉ viết ở **một** chỗ:
[`../rules/core.md`](../rules/core.md) — mục 6 zero-fluff, mục 7 output clamping,
mục 8 hot reload, mục 9 bảng "task nào đọc file nào".

## 7. JavaScript & tương tác không reload trang

Chi tiết ràng buộc: [`architecture-workflow.md`](architecture-workflow.md) mục 5.

| Quy tắc | Ghi chú |
|---|---|
| **Một tính năng = một file JS độc lập** trong `wwwroot/js/<ten-tinh-nang>.js` | kebab-case, vd `kiemke-chan-thietbi.js`. Không viết JS mới inline trong `.cshtml` |
| View chỉ `<script src>` + `data-*` | View không chứa logic; JS đọc cấu hình/URL/id từ `data-*` hoặc `<script type="application/json">` |
| Mọi submit/hành động phải `event.preventDefault()` | Không để trình duyệt tự tải lại trang |
| Giao tiếp bằng `fetch()`, server trả **JSON** | Action AJAX trả `Json(...)`, không trả `View`/`Redirect` |
| Không `location.reload()` / gán `window.location` sau khi lưu | Cập nhật DOM cục bộ đúng vùng bị ảnh hưởng |
| Khoá nút + hiện loading khi đang gửi | Chống double-submit (tạo trùng đơn/bản ghi) |
| Lỗi hiển thị tại chỗ, giữ nguyên dữ liệu đã nhập | `try/catch` quanh `fetch`, kiểm `res.ok` trước khi `res.json()` |
| Đặt listener theo **event delegation** cho nội dung vẽ động | Tránh listener chết sau khi render lại bảng |
| Escape khi chèn dữ liệu vào DOM | Ưu tiên `textContent`; dùng `innerHTML` phải có lý do rõ |
| Chống CSRF cho POST AJAX | Gửi kèm token nếu action yêu cầu `[ValidateAntiForgeryToken]` |
| Đổi nội dung file JS cũ | Cache tĩnh 7 ngày — cân nhắc thêm query version |

## 8. Bảo mật Secrets & biến môi trường (`.env`)

Mọi giá trị nhạy cảm **bắt buộc** nằm trong `.env` (đặt tại `E-Form-Best/` khi dev, cạnh
`E-Form-Best.dll` khi chạy IIS), nạp vào cấu hình ngay đầu `Program.cs` trước `CreateBuilder`.

| Quy tắc | Cụ thể |
|---|---|
| Danh mục bắt buộc vào `.env` | Connection string + mật khẩu DB, API key bên thứ ba, khoá VAPID/Web Push, secret JWT/session, OAuth client secret, thông tin tài khoản mail, mọi loại token |
| **TUYỆT ĐỐI không hardcode** | Không để mật khẩu/khoá trong `.cs`, `.cshtml`, `.js`, `appsettings.json`, script `.sql`, hay comment |
| Đặt tên biến | Theo cú pháp cấu hình .NET: `Section__Key` (vd `ConnectionStrings__DefaultConnection`) để `AddEnvironmentVariables()` map đúng |
| Luôn duy trì `.env.example` | Mỗi khi thêm biến mới vào `.env` phải thêm **cùng key** vào `.env.example` với giá trị giả (`<mat-khau>`), để người clone không thiếu cấu hình |
| Không commit `.env` thật | `.gitignore` đã chặn `.env`, `.env.local` và giữ lại `.env.example`. Kiểm `git status` trước khi commit |
| Không phơi secret ra client | Không đưa key backend vào `.cshtml`, `data-*`, hay file trong `wwwroot/`. Client chỉ nhận khoá công khai (vd VAPID public key) |
| Biến môi trường máy chủ thắng `.env` | IIS/hệ thống đã đặt sẵn biến cùng tên thì `.env` bị bỏ qua — khi đổi giá trị nhớ kiểm cả hai nơi |
| Lỡ commit secret | Coi như đã lộ: **đổi mật khẩu/khoá ngay**, rồi mới dọn lịch sử git |
