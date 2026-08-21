# Phase 1 — Tính năng cốt lõi

Mô tả nghiệp vụ & số liệu vận hành đầy đủ đã có trong
[`BaoCao_TongKet_DuAn_EFormBest.md`](../../BaoCao_TongKet_DuAn_EFormBest.md) — **không lặp lại ở đây**.
File này chỉ nêu: mỗi phân hệ nằm ở đâu, hoàn thiện tới đâu, còn gì phải làm.

## Khung chung của một "phiếu/đơn"

Cả 4 phân hệ đơn từ dùng chung một hình dạng dữ liệu — **thêm loại phiếu mới thì bám đúng khuôn này**:

| Vai trò | ITForm | HRform | SHDForm | QLCongViec |
|---|---|---|---|---|
| Bảng đơn tổng | `FormIt` | `FormHr` | `FormShd` | `FormCongViec` |
| Chi tiết theo loại phiếu | `ItMail1`, `ItOrderIt2`, `ItDangKiSuDungWifi3`, … | `HrXinRaNgoai1`, `HrMangHangHoaRaCong2`, … | `ShdDangKySuDungXeCongTac1`, … | `CvCongViecOrder1` |
| Bình luận | `BinhLuanFormIt` | `BinhLuanFormHr` | `BinhLuanFormShd` | `BinhLuanFormCongViec` |
| Lịch sử | `LichSuFormIt` | `LichSuFormHr` | `LichSuFormShd` | `LichSuFormCongViec` |
| Người hỗ trợ | `ItNguoiHoTro` / `ItCtNguoiHoTro` | `HrNguoiHoTro` / `HrCtNguoiHoTro` | `ShdNguoiHoTro` / `ShdCtNguoiHoTro` | `FormCongViecNguoiLienQuan` |

Hậu tố số trong tên model (`…1`, `…2`) = **mã loại đơn**, khớp `DmLoaiDon`. Giữ nguyên quy ước.

Đơn coi là **đã hoàn tất** khi `idAdmin IS NOT NULL`.

Danh mục dùng chung: `DmCongTy`, `DmBoPhan`, `DmLoaiDon`, `DmNguoiXacNhan*`, `DmNguoiUyQuyen`,
`User`, `UserQuyen`, `UserBoPhan`, `Quyen`, `DanhMucQuyenBoPhan`.

## Trạng thái từng phân hệ

### ITForm — trụ cột, ~73% hoạt động phát triển
- **Phiếu IT (8 loại):** xong. Route `[HttpGet/HttpPost("/FormIT/...")]` trong `ITFormController.cs`.
- **Kiểm kê thiết bị (`KK_*`):** CRUD, nhập Excel/CSV hàng loạt, tự phân loại PC/Laptop, tra chủ sở
  hữu theo Serial, thống kê + xuất Excel. `KkThietBi` là bảng **duy nhất có soft-delete** (`ngay_xoa`).
- **Thu thập cấu hình máy (`TSCN_*`):** quét RAM/ổ cứng/màn hình/MAC Wifi/bản quyền, đồng bộ **một
  chiều** từ chi nhánh `10.0.55.3`.
- **Đang làm:** danh sách chặn thiết bị (`KkThietBiChan`) — xem [`00-context-memory.md`](00-context-memory.md).
- **Còn lại:** xoá view thừa `TaoMail.cshtml` (không route nào gọi tới); rà soát các route
  Export Excel/Word/PDF đã có ở controller nhưng chưa chắc có nút trên UI.

### HRform — quy trình phức tạp nhất
- 12 loại phiếu, duyệt tối đa 4 cấp (Quản lý → B2 theo bộ phận, hỗ trợ AND/OR + uỷ quyền → Giám đốc → HR/Admin).
- Tích hợp Bảo vệ kiểm soát cổng (`BaoVeHr`, `HrBaoVeXacNhan`), phòng họp `PhongHopHr` (chống trùng lịch).
- Mẫu xuất song ngữ Việt–Trung cho đơn thẻ nhân viên & đơn điện thoại.
- **Còn lại:** đo thời gian xử lý theo từng cấp duyệt để tìm điểm nghẽn (tỷ lệ hoàn tất thấp hơn IT).

### SHDForm — mở rộng dở dang
- Luồng duyệt tương tự HR, có uỷ quyền (`ShdQuanLyDuyetB2UyQuyen`).
- **Còn lại:** model `ShdDangKySuDungXeDaily2` đã có nhưng **chưa có giao diện tạo đơn** tương ứng.

### QLCongViec — mới nhất, quy mô nhỏ
- Giao việc cho nhiều nhân sự (autocomplete + chip), duyệt/huỷ/hoàn tất/đánh giá, bình luận kéo-thả ảnh,
  xuất biên bản có khối chữ ký 3 bên, xoá đơn trong transaction.
- `AutoRatingWorker` tự chấm điểm lúc 0h.

### AdminForm — nền tảng dùng chung
- Xác thực hybrid Domain/DB, khoá tạm sau nhiều lần sai, phân quyền theo Bộ phận/Vai trò khi đăng nhập.
- Menu trung tâm (`[HttpGet("/")]` trả thẳng `MenuA`, không redirect), quản lý tài khoản/ảnh đại diện,
  Web Push (`sw.js`, `UserDevice`), endpoint `ThongKeHeThong`.
- **Module Đào tạo ISO** (`DaoTao*`) — luồng duyệt, điểm danh, tài liệu, mã cuộc họp (commit `22cd336`).

## Checklist khi thêm một loại phiếu mới

1. Tạo bảng chi tiết trên SQL Server (DDL thủ công, xem `../rules/database-safety.md`) + thêm bản ghi `DmLoaiDon`.
2. Thêm model vào `Models/ITForm/` theo đúng quy ước `[Table]/[Column]`.
3. Thêm `DbSet` vào `Context/ITFormContext.cs` **và** cấu hình trong `OnModelCreating` nếu cần.
4. Thêm action GET (form) + POST (lưu) vào controller của Area, route tuyệt đối, đặt **trên** route `homeActions`.
5. Thêm view `.cshtml` vào `Areas/<Area>/Views/<Controller>/`; JS mới **tách ra `wwwroot/js/`**, không viết inline.
6. Gắn vào luồng duyệt: bình luận, lịch sử, người hỗ trợ, xuất Excel/Word/PDF nếu nghiệp vụ cần.
7. Ghi lại quyết định đáng nhớ vào Decision Log của [`00-context-memory.md`](00-context-memory.md).
