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

**Output Clamping — bắt buộc kẹp số dòng.** Mọi lệnh có thể trả về output dài (`grep`, `find`,
`cat`, `ls`, `git`, `dotnet build`) phải đi kèm pipe giới hạn dòng, mặc định `| head -n 20`:

```bash
grep -rn "TenHam" E-Form-Best/Areas | head -n 20
find E-Form-Best/wwwroot/js -name "*.js" | head -n 20
cat watch_log.txt | tail -n 20          # log: lấy phần cuối, không cat cả file
git status --short | head -n 20
dotnet build -v quiet --nologo | grep -E "error|Error" | head -n 20
```

Không có lệnh nào được phép "xả" nguyên output vào phiên làm việc. Cần xem thêm thì tăng dần
(`head -n 50`), không bỏ pipe.

### 6.1. Chạy server hot reload — bắt buộc

- Web **phải auto chạy hot reload**: `cd E-Form-Best && dotnet watch run` (không dùng `dotnet run` trần).
  Chạy ở **nền**, ghi output ra `watch_log.txt` / `watch_err.txt`, đọc lại bằng `tail -n 30` —
  không stream log vào phiên làm việc.
- **Luôn kèm cờ/cấu hình không xoá lịch sử terminal.** Trình chạy nào có cờ đó thì phải bật:
  Vite → `vite --clearScreen false`; công cụ khác → cờ tương đương (`--no-clear`, `--preserve-output`).
  `dotnet watch` không có cờ này, nên **bù bằng cách ghi log ra file** (log không bị xoá,
  vẫn tra cứu được lỗi của lần chạy trước).
- `dotnet watch` của SDK .NET 10 thỉnh thoảng tự chết khi hot-reload → **bọc bằng supervisor**
  (vòng lặp tự chạy lại) thay vì để app tắt im lặng.
- Không tự ý thêm build step frontend (Vite/Node) vào dự án này — xem
  [`project-scope.md`](project-scope.md). Mục Vite ở trên chỉ là quy ước áp dụng **nếu** dự án có.

**Bảng tra công cụ hot reload theo ngôn ngữ** (nhận diện từ file manifest — `*.csproj`,
`package.json`, `go.mod`, `composer.json`, `pyproject.toml`, `Cargo.toml` — rồi chọn đúng dòng;
với repo này luôn là dòng .NET):

| Stack | Lệnh dev bắt buộc | Cờ giữ lịch sử terminal |
|---|---|---|
| **.NET (repo này)** | `dotnet watch run` | không có cờ → **ghi log ra file** `watch_log.txt` |
| JS/TS + Vite | `vite --clearScreen false` | `--clearScreen false` |
| Node/Express | `nodemon --exec ...` | không tự clear; tránh `console.clear()` |
| Next.js | `next dev` | ghi log ra file nếu cần giữ |
| Python FastAPI | `uvicorn app:app --reload` | `--no-use-colors` nếu log khó đọc; không clear |
| Python khác | `watchfiles`/`watchdog` | — |
| PHP/Laravel | `php artisan serve` + `npm run dev` (Vite) | `--clearScreen false` cho Vite |
| Go | `air` | `-c .air.toml` với `clear_on_rebuild = false` |
| Rust | `cargo watch -x run` | `--no-clear` (mặc định `cargo watch` có clear) |

Nguyên tắc chung: **không bao giờ chạy server dev ở chế độ không hot reload**, và **không bao giờ
để công cụ xoá màn hình terminal** — log của lần chạy trước là dữ liệu debug.

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

## 8. Zero-Fluff & Diff Output — cách trả lời

| Quy tắc | Cụ thể |
|---|---|
| Trả lời trực diện | Không chào hỏi, không mở bài, không tóm tắt lại yêu cầu vừa nhận. Vào thẳng kết quả/kết luận |
| Chỉ xuất phần thay đổi | Khi sửa code, in **đoạn code đã đổi** hoặc diff, kèm đường dẫn + số dòng. Không in lại cả file, không in lại hàm không bị ảnh hưởng |
| Không lặp lại code vừa ghi | File đã sửa bằng công cụ edit thì **không dán lại nội dung** vào câu trả lời — chỉ nói đã đổi gì, ở đâu |
| Không tự thêm phần thừa | Không viết changelog, không viết doc, không format lại file, không "dọn dẹp" nếu không được yêu cầu |
| Báo cáo đúng sự thật | Cái gì đã build/chạy thử thì nói rõ; cái gì chưa kiểm thì ghi "chưa kiểm", không suy đoán thành khẳng định |
| Độ dài bám việc | Việc nhỏ → vài dòng. Không dàn trang mục lục, bảng biểu cho một sửa đổi một dòng |
