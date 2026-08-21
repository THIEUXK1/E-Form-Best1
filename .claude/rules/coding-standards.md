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

## 6. Terminal Output — bắt buộc (tiết kiệm token)

Mọi lệnh build/lint/test/chạy phải **thu gọn output**, không đổ nguyên log ra phiên làm việc.

| Việc | Dùng | Không dùng |
|---|---|---|
| Build | `dotnet build -v quiet --nologo` | `dotnet build` trần |
| Chỉ xem lỗi | `dotnet build -v quiet --nologo \| grep -E "error\|Error"` | đọc toàn bộ log |
| Restore | `dotnet restore --nologo -v quiet` | |
| Chạy app | `cd E-Form-Best && dotnet watch run` (**nền/background**, đừng stream log vào context) | chạy foreground rồi đọc hết log |
| Log dài (`watch_log.txt`, `dotnet-watch.log`) | `tail -n 30 <file>` hoặc `grep -E "error\|fail\|Exception" <file> \| tail -20` | `cat` cả file |
| Liệt kê file | `ls`, `find ... \| head -40` | `ls -R` toàn repo |
| Đọc file lớn | `sed -n 'a,bp'`, `head`, `grep -n` | `cat` file 3000 dòng |
| Git | `git log --oneline -20`, `git diff --stat` | `git log -p`, `git diff` toàn bộ |

Nguyên tắc: **luôn ưu tiên cờ `--quiet`/`-v quiet`/`--nologo`/`--silent`, hoặc lọc chỉ dòng
`FAILED`/`ERROR`/`Exception`.** Chỉ đọc log đầy đủ khi bản thu gọn không đủ để chẩn đoán.
