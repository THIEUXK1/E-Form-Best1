# Quy trình làm việc, Git Flow & Phân bổ file

## 1. Kiến trúc chung

ASP.NET Core MVC monolith, chia theo **Areas**. Một CSDL SQL Server, một `DbContext`.
Chi tiết stack: [`../plans/00-master-plan.md`](../plans/00-master-plan.md).

```
E-Form-Best/
├─ Program.cs              ← DI, middleware pipeline, route. Sửa cẩn thận, thứ tự có chủ đích
├─ appsettings.json        ← config + hằng số nghiệp vụ (vd BanQuyenWindows:MakKeyCongTy)
├─ Context/ITFormContext.cs← DbContext DUY NHẤT. Thêm bảng = thêm DbSet ở đây
├─ Models/ITForm/          ← entity EF (DB-first). MỘT file = MỘT bảng
├─ Areas/<Ten>/
│  ├─ Controllers/         ← business logic + action
│  ├─ Services/            ← background worker / logic tái sử dụng (hiện chỉ ITForm có)
│  └─ Views/
│     ├─ <Controller>/     ← view của Area
│     └─ Shared/_Layout.cshtml  ← layout riêng từng Area
└─ wwwroot/
   ├─ js/, css/            ← JS/CSS tự viết
   ├─ lib/                 ← thư viện bên thứ ba — KHÔNG sửa tay
   ├─ FileIT/, HuongDanIT/ ← file người dùng upload / tài liệu
   └─ sw.js                ← service worker (Web Push)
```

## 2. QUY TẮC PHÂN BỔ FILE — bắt buộc

**Mỗi thứ một chỗ. Không tạo file ngoài cấu trúc trên.**

| Loại nội dung | Nơi duy nhất được đặt |
|---|---|
| Giao diện / Razor markup | `Areas/<Area>/Views/<Controller>/*.cshtml` |
| Layout, partial dùng chung trong Area | `Areas/<Area>/Views/Shared/` |
| Business logic, xử lý request | `Areas/<Area>/Controllers/*Controller.cs` |
| Logic tái sử dụng, background job, tích hợp ngoài (AD, WMI, export) | `Areas/<Area>/Services/` |
| Truy vấn CSDL / entity | `Models/ITForm/` + `Context/ITFormContext.cs` |
| JavaScript tự viết | `wwwroot/js/<ten-tinh-nang>.js` |
| CSS tự viết | `wwwroot/css/` (hoặc `_Layout.cshtml.css` cho layout của Area) |
| Thư viện bên thứ ba | `wwwroot/lib/` — chỉ thêm nguyên gói, không sửa |
| Cấu hình / hằng số nghiệp vụ | `appsettings.json` |
| Tài liệu kế hoạch, rules | `.claude/plans/`, `.claude/rules/` |

### Cấm

- ❌ **Trộn UI và business logic trong cùng một file.** View chỉ hiển thị; mọi truy vấn, tính toán,
  quyết định nghiệp vụ nằm ở Controller/Service.
- ❌ **Viết `@{ }` chứa logic nghiệp vụ / truy vấn EF trong `.cshtml`.** Dữ liệu vào view qua Model
  hoặc ViewBag đã được controller chuẩn bị sẵn.
- ❌ **Viết JS mới inline trong `<script>` của `.cshtml`.** File JS mới → `wwwroot/js/`.
  *(Hiện trạng: nhiều view cũ đang vi phạm — vd `IndexThietBi.cshtml` 2.9k dòng. Không nhân rộng;
  khi sửa lớn một view thì tách dần phần JS ra.)*
- ❌ **Tạo thư mục/file mới ở gốc repo** hoặc ngoài các vị trí trong bảng trên nếu chưa hỏi người dùng.
- ❌ **Sửa file trong `wwwroot/lib/`**, `bin/`, `obj/`.
- ❌ Tạo `DbContext` thứ hai. *(`new ITFormContext()` ngoài DI là nợ kỹ thuật đang tồn tại — code mới
  nên nhận context qua constructor injection.)*

### Trước khi tạo file mới — tự hỏi

1. Đã có file nào đúng vai trò này chưa? (Sửa file cũ tốt hơn đẻ file mới.)
2. Nội dung này thuộc ô nào trong bảng trên?
3. Nếu không thuộc ô nào → **hỏi người dùng**, đừng tự chọn chỗ.

## 3. Git Flow

- Nhánh chính: `master`. Nhánh này **đang chạy production**.
- Việc mới → tạo nhánh riêng: `feature/<mo-ta-ngan>`, `fix/<mo-ta-ngan>`.
  **Không commit thẳng lên `master`** trừ khi người dùng yêu cầu rõ.
- **Chỉ commit/push khi người dùng yêu cầu.** Không tự động commit sau khi sửa code.
- Commit message: theo lệ hiện có của repo — **tiếng Việt không dấu**, mô tả việc đã làm,
  ví dụ `Them nhap ban quyen Windows tu Excel va toi uu dinh tuyen sau reverse proxy`.
- Không commit: `bin/`, `obj/`, `*.log`, `watch_log.txt`, `.vs/`, file upload trong `wwwroot/FileIT/`,
  và **bất kỳ secret nào**.

## 4. Quy trình một thay đổi

1. **Đọc trước khi sửa** — file liên quan + [`../plans/00-context-memory.md`](../plans/00-context-memory.md)
   (biết đang vướng gì, đã quyết gì).
2. **Chạm schema?** → theo [`database-safety.md`](database-safety.md) *trước*, không sửa model trước DDL.
3. **Sửa code** theo [`coding-standards.md`](coding-standards.md) và bảng phân bổ file ở trên.
4. **Kiểm tra trong phạm vi**: `dotnet build` (xem mục Terminal Output trong `coding-standards.md`),
   rồi `cd E-Form-Best && dotnet watch run` và thử luồng thật trên trình duyệt.
   Dự án **không có test tự động** — kiểm chứng bằng luồng thật là bắt buộc.
5. **Cập nhật `00-context-memory.md`** nếu phát sinh quyết định/blocker mới.
6. Báo đúng sự thật: cái gì đã kiểm, cái gì chưa.

## 5. Quy tắc View & Tương tác Web — bắt buộc

### 5.1. View chỉ là View (Dumb / Presentational UI)

`.cshtml` chỉ làm **một việc**: đổ dữ liệu đã chuẩn bị sẵn ra HTML.

| Trong View được phép | Trong View bị cấm |
|---|---|
| `@Model.X`, `@ViewBag.Y`, vòng lặp render, `@Html.*`, partial | Truy vấn EF / gọi `DbContext` |
| Định dạng hiển thị đơn giản (`ToString("dd/MM/yyyy")`) | Tính toán nghiệp vụ, tổng hợp, thống kê trong `@{ }` |
| Đặt `data-*` để JS đọc lại | Gọi HTTP/API trực tiếp từ view |
| `<script src="~/js/<ten>.js">` | Viết logic JS mới inline trong `<script>` |

Mọi dữ liệu view cần **phải được Controller/Service chuẩn bị trước** rồi đưa vào qua Model/ViewBag.
Cần thêm dữ liệu → sửa Controller, **không** sửa view để tự đi lấy.

### 5.2. Thao tác trên web KHÔNG load lại trang — 100% JavaScript

Mọi tương tác người dùng (submit form, lọc, phân trang, duyệt/huỷ đơn, xoá, upload, đổi trạng thái)
xử lý **bất đồng bộ bằng JavaScript**, cập nhật UI **cục bộ**, tuyệt đối không full page reload.

| Yêu cầu | Cụ thể |
|---|---|
| Chặn hành vi mặc định | `form.addEventListener('submit', e => { e.preventDefault(); … })`; link hành động dùng `<button>` hoặc `e.preventDefault()` |
| Không dùng | `location.reload()`, `window.location = …` sau khi lưu, `<form method="post">` submit thẳng, `RedirectToAction` cho luồng AJAX |
| Giao tiếp server | `fetch()` (hoặc `$.ajax` theo lệ file cũ) → action trả **JSON** (`Json(new { thanhCong, thongBao, duLieu })`), không trả về view |
| Cập nhật UI | Chỉ vẽ lại đúng vùng bị ảnh hưởng (dòng bảng, badge, tổng số) bằng DOM API |
| Trạng thái | Có loading khi đang gửi, khoá nút chống double-submit, hiện lỗi ngay tại chỗ — không mất dữ liệu người dùng đã nhập |
| Chống double-submit | Bắt buộc — xem [`database-safety.md`](database-safety.md) mục Idempotency |

**Ngoại lệ hợp lệ** (được phép điều hướng thật): đăng nhập/đăng xuất, mở trang chi tiết là một URL
riêng cần bookmark được, tải file xuất Excel/Word/PDF.
### 5.3. Ràng buộc là TUYỆT ĐỐI — kể cả view cũ

Không có khái niệm "view cũ được miễn". Mọi view trong repo **phải** đạt trạng thái không reload;
danh sách form còn vi phạm được theo dõi ở [`../plans/00-master-plan.md`](../plans/00-master-plan.md)
mục 4 (Phase 2) và phải về 0.

- Chạm vào một view còn submit đồng bộ → **chuyển nó sang AJAX trong chính lần sửa đó**, không
  được sửa thêm tính năng lên trên form đồng bộ.
- Không thêm mới bất kỳ `<form method="post">` submit thẳng, `RedirectToAction` sau khi lưu,
  hay `location.reload()` nào nữa — kể cả trong view đã có sẵn.
- `RedirectToAction`/`Redirect` chỉ còn được phép cho: chưa đăng nhập → trang đăng nhập.

**Khi chuyển một form sang AJAX, bắt buộc kèm chốt chống trùng.** POST-Redirect-GET đang *vô tình*
chống double-submit (F5 sau khi lưu không tạo đơn thứ hai); bỏ redirect là mất lá chắn đó.
Mỗi form chuyển đổi phải có: khoá nút khi đang gửi **và** chốt idempotency phía server
(xem [`database-safety.md`](database-safety.md) mục 4).
