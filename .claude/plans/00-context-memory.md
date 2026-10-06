# Bộ nhớ bối cảnh — cập nhật liên tục

> File này là **trạng thái sống**, tự nạp mỗi phiên → giữ ngắn.
> Sửa trực tiếp mỗi khi đổi việc / gặp blocker / chốt quyết định.
> Decision Log chỉ giữ **5 dòng gần nhất**, cũ hơn đẩy xuống [`decision-log.md`](decision-log.md).
> **Luôn ghi ngày tuyệt đối (dd/mm/yyyy)**, không viết "tuần trước", "hôm qua".

---

## Đang làm (cập nhật: 17/09/2026)

**Việc:** Kiểm kê IT — bộ lọc vị trí địa lý / nhu cầu cài Office, bổ sung dữ liệu WKNA, và dán ảnh
vào bình luận đơn IT.

**File đang chạm (theo `git status` ngày 17/09/2026):**

| File | Trạng thái |
|---|---|
| `.claude/plans/sql/kk-wkna-bosung-20260917.sql` + `-rollback.sql` | mới — **chưa chạy**, chờ duyệt |
| `.claude/plans/sql/kk-capnhat-vitri-office-20260917*` (`.sql`, `.csv`) | sửa |
| `E-Form-Best/wwwroot/js/kiemke-loc-can-cai-office.js` | mới |
| `E-Form-Best/wwwroot/js/formit-binhluan-dan-anh.js` | mới |
| `E-Form-Best/wwwroot/js/kiemke-loc-vitri-dialy.js` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/IndexThietBi.cshtml` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/ChiTiet.cshtml` | sửa |
| `watch-supervisor.ps1` | mới — supervisor cho `dotnet watch` |

**Nhánh:** `master` (commit gần nhất `fe78d82` — lọc vị trí địa lý, đổi đơn vị đơn IT,
bắt phiên hết hạn sớm).

---

## Blockers

| # | Vấn đề | Ảnh hưởng | Cần ai/cái gì để gỡ |
|---|---|---|---|
| B1 | Bảng `KK_ThietBiChan` phải được tạo **thủ công** trên SQL Server production trước khi deploy — repo không có Migration | Deploy code mà chưa chạy DDL → lỗi runtime khi mở trang Kiểm kê | Người có quyền trên `10.0.60.33`; script DDL phải được duyệt trước ([`../docs/database-safety.md`](../docs/database-safety.md)) |
| B2 | Connection string **đã** ra `.env` (`c3d22a1`), nhưng tài khoản dùng vẫn là `sa` | Lộ file `.env` = toàn quyền ghi trên cả 2 server SQL | Cấp tài khoản SQL riêng quyền tối thiểu, đổi mật khẩu `sa` (đã từng nằm trong lịch sử git) |
| B3 | Không có công cụ giám sát production; `/health/ready` đã có nhưng chưa có gì gọi nó | Sự cố chỉ biết khi người dùng báo | Cấu hình uptime check trỏ vào `/health/ready` |

*(B5 — bảng `KK_Camera` — đã gỡ 06/10/2026: người dùng xác nhận trong phiên, DDL đã chạy trên `10.0.60.33`, đối soát 21 cột + `PK_KK_Camera` + `UQ_KK_Camera_DiaChiIp`. Lần chạy đầu lỗi Msg 1934 do sqlcmd tắt `QUOTED_IDENTIFIER` → script đã thêm `SET QUOTED_IDENTIFIER ON`.)*

*(B4 — bảng `IT_ThietKeTemIn_9` — đã gỡ 27/08/2026: DDL đã chạy trên `10.0.60.33`, đối soát
0/0/0 → 1/1/1, FK `FK_ITThietKeTemIn_FormIT` SET_NULL đã có.)*

---

## Decision Log — 5 quyết định gần nhất

Cũ hơn: [`decision-log.md`](decision-log.md).

| Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|
| 06/10/2026 | Cửa sổ xem camera có 3 chế độ: **Video / Ảnh 1 giây / Ảnh lưu sẵn**. Ảnh lưu sẵn do job `CameraAnhLuuWorker` chụp theo giờ (`CameraNvr__GioChupAnhLuu`, mặc định 08:30, chụp bù khi khởi động nếu lỡ mốc) vào `CameraNvr__ThuMucAnhLuu` = `\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\E-Camera`; chỉ bật ở máy gọi được đầu ghi (`CameraNvr__ChupAnhLuu=true`, hiện là máy dev ZP-IT003) | Máy chủ production không thông mạng tới dải đầu ghi | Chụp ảnh đi theo `api_port`/`https` của inventory ISAPI rồi lùi về http:80 (10.0.28.254 :8001, 10.0.29.254 https:8003). NVR cũ 10.0.28.5 (DS-7732NI-K4 V4.30) chỉ chụp được qua `/ISAPI/ContentMgmt/StreamingProxy/channels/{k}02/picture`. Lượt đầu 268/319 (thiếu = camera mất kết nối + kênh lẻ). Production muốn hiện ảnh thì identity app pool phải đọc được thư mục chia sẻ |
| 06/10/2026 | Tab **Lịch sử** `/QLCamera`: job nền `CameraLichSuWorker` (mỗi 120 s, `CameraIsapi__ChuKyLichSuGiay`; tắt bằng `CameraIsapi__GhiLichSu=false`) so trạng thái ISAPI với `KK_CameraTrangThai`, đổi thì ghi `KK_CameraLichSu` (append-only). Script `kk-camera-lich-su-20261006.sql` **đã chạy** | ISAPI không có API nhật ký, chỉ trạng thái hiện tại + `status_changed_at` | Thời điểm sự kiện = `status_changed_at` của ISAPI. Không có dữ liệu trước 06/10/2026 14:55; camera chập chờn trong một chu kỳ poll ISAPI (~5 phút) không thấy được. Máy dev cũng chạy job ghi vào DB production — unique `(nvr_ip,kenh,thoi_gian,sang_trang_thai)` chặn trùng |
| 06/10/2026 | **Xem trực tiếp camera** ở `/QLCamera` (bấm 1 camera tab Giám sát): video qua **go2rtc v1.9.14 chạy cùng máy, chỉ nghe `127.0.0.1:1984`**, E-Form chuyển tiếp fMP4 (`/QLCamera/Xem/Video`); ảnh chụp gọi thẳng ISAPI đầu ghi (`/QLCamera/Xem/AnhChup`) | Người dùng chọn video mượt thay vì ảnh 1 giây. Đi qua E-Form để dùng đăng nhập/phân quyền sẵn có và giấu mật khẩu đầu ghi; chỉ cho IP đầu ghi có trong danh sách ISAPI (chặn SSRF) | Luồng phụ nhiều camera BPVN là **H.265** → go2rtc khai 2 nguồn (RTSP gốc + `ffmpeg:#video=h264`) và E-Form xin `video=h264`: camera H.264 đi thẳng, camera H.265 chuyển mã (~15% 1 nhân CPU/luồng, hình đầu ~2,6 s). ffmpeg đọc lại qua RTSP nội bộ go2rtc `127.0.0.1:8554`. Production cần cài go2rtc (`.claude/pending/cai-go2rtc-10.0.60.39.ps1`) + thêm `CameraNvr__*` vào `.env`; nginx phải tôn trọng `X-Accel-Buffering: no` |
| 06/10/2026 | `/QLCamera` thêm tab **Giám sát** đọc API hệ thống **BPVN Camera ISAPI** (`10.0.60.238:3005`, chỉ GET qua `CameraGiamSatService`) + tab **Đầu ghi chi nhánh** (bảng mới `KK_DauGhi`, 15 dòng từ `CCTV.xlsx`, script `kk-dau-ghi-20261006.sql` **đã chạy** khi người dùng xác nhận) | Hệ thống ISAPI đã poll NVR Hikvision ~5 phút/lần — E-Form poll lại là trùng việc | Cấu hình `CameraIsapi__BaseUrl/TaiKhoan/MatKhau` ở `.env` — **production `10.0.60.39` phải thêm 3 biến này** trước khi deploy, thiếu thì tab Giám sát báo lỗi cấu hình. `KK_DauGhi` cố ý **không có cột mật khẩu**; đầu ghi mạng BPVN không nhập vào vì ISAPI đã theo dõi. Cột **Ghi chú** tab Giám sát lưu ở bảng `KK_CameraGhiChu` (unique `nvr_ip`+`kenh`, script `kk-camera-ghi-chu-20261006.sql` **đã chạy**), xoá trắng = NULL, lịch sử trước/sau ở `KK_LichSuThaoTac`. Câu ghi chú có sẵn (nút +, dùng chung) ở `KK_CameraGhiChuMau` (unique lọc theo nội dung còn hiệu lực, xoá mềm; script `kk-camera-ghi-chu-mau-20261006.sql` **đã chạy**) |
| 06/10/2026 | Mục **Quản lý camera** (`/QLCamera`, quyền `AdminIT`/`All`) trong nhóm menu "Máy in & CCDC": bảng mới `KK_Camera` (script `kk-camera-20261006.sql`), xoá mềm `ngay_xoa`, unique index lọc theo IP còn hiệu lực | Camera quản theo từng con có IP + đầu ghi/kênh, khác CCDC (theo số lượng) và không muốn nhồi vào `KK_ThietBi` | Ping dùng chung `MayInQuetService.PingNhieuAsync` với khoá cache riêng `Camera:TrangThaiPing`. Lịch sử ghi `KK_LichSuThaoTac` với `DoiTuong = "Camera"`. Response dùng chuẩn `thanhCong/thongBao/duLieu` (khác CCDC dùng `success/message`) |

**Lưu ý còn hiệu lực:** bảng màu thẻ đơn IT đã dùng 10 hue (21°, 43°, 78°, 142°, 189°, 221°, 245°,
258°, 293°, 333°) — khoảng hở lớn nhất chỉ còn ~64° (giữa 78° và 142°); đơn mới nên phân biệt bằng
độ đậm/icon thay vì tìm hue mới.

---

## Ghi chú vận hành

- Cách chạy dự án + quy tắc hot reload: [`../rules/core.md`](../rules/core.md) mục 8.
- Log `dotnet watch` đổ ra `watch_log.txt`, `dotnet-watch.log`, `dotnet-watch.err.log` ở gốc repo —
  **file rác, đã nằm trong `.claudeignore`, đừng commit.**
- Chạy file `.sql` tiếng Việt bằng `sqlcmd` phải thêm `-f 65001`, không thì dữ liệu vào DB lỗi font.
- Deploy: production ở `10.0.60.39`, IIS `C:\inetpub\wed\E-Form-Best`, dùng `app_offline` + `robocopy /E`.
- Ràng buộc thường trực (view là Dumb UI, không reload trang, JS ra file riêng, secret trong `.env`)
  nằm ở [`../rules/core.md`](../rules/core.md) mục 3–4 — **không chép lại ở đây**.

## Ghi chú cho phiên sau

- `kk-wkna-bosung-20260917.sql` **chưa chạy** — cần duyệt rồi chạy tay; đã có file rollback kèm theo.
- Danh sách form còn submit đồng bộ (phải về 0): [`00-master-plan.md`](00-master-plan.md) mục 4.
