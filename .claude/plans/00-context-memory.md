# Bộ nhớ bối cảnh — đọc đầu mỗi task, giữ ≤ 3 KB

> 5 quyết định gần nhất, tóm 1 dòng; chi tiết + cũ hơn ở [`decision-log.md`](decision-log.md).
> Ngày tuyệt đối dd/mm/yyyy.

## Đang làm (09/10/2026)

- Quản lý AP + quyền xem CCDC đã commit (`1018d26`).
- Quản lý Switch (`QLSwitchController`, `Switch*` service, `switch-*.js`) + Sơ đồ (`SoDoController`,
  `so-do.js`) — **chưa commit**. Script `kk-switch-*`, `quyen-switch-cong-ty-*` trong `plans/sql/`.

## Blockers

| # | Vấn đề | Cần để gỡ |
|---|---|---|
| B1 | `KK_ThietBiChan` phải tạo tay trên `10.0.60.33` trước deploy | Người có quyền chạy DDL đã duyệt |
| B2 | Connection string dùng `sa` | Tài khoản SQL quyền tối thiểu + đổi mật khẩu `sa` |
| B3 | Chưa có gì gọi `/health/ready` | Uptime check |
| B7 | `KK_ThietBiChotNgay` chưa tạo → `ThietBiChotNgayWorker` log lỗi, tab Thiết bị `/BaoCaoDinhKy` báo "không đọc được bản chụp" | Chạy `plans/sql/kk-thietbi-chot-ngay-20261009.sql` trên prod trước deploy; có số kỳ trước sau ≥ 1 kỳ chụp |
| B6 | `KK_AccessPoint` + `KK_AccessPointLichSu` chưa tạo → `/QLAP/*` lỗi, `AccessPointPingWorker` log `Invalid object name` | Chạy `kk-access-point-20261008.sql` + `quyen-ap-cong-ty-20261008.sql` trước deploy; PFVN cần `AccessPointController__PFVN__*` trong `.env` |


## 5 quyết định gần nhất

- 12/10 — Kết luận Win/Office từng máy BPVN của file 280926 ghi thẳng vào `win_license`/`office_license`/`can_cai_office` (người dùng chọn thay vì bảng riêng); `NhomWindows` nhận nguyên nhãn, đồng bộ agent không ghi đè nhãn đã xác minh (`LaNhanWinDaXacMinh`); key còn trống ở `BanQuyenKeyTrong` trừ vào "Cần mua". Script `kk-ban-quyen-280926-20261012.sql` đã COMMIT trên prod 12/10 (973 dòng) — không chạy lại.
- 09/10 — Báo cáo định kì so kỳ trước (`CameraBaoCaoService.KyTruoc`); bản quyền/Office/trạng thái thiết bị so theo bản chụp hằng ngày `KK_ThietBiChotNgay` (22h), nhóm Win/Office như báo cáo BPVN 280926 (`NhomWindows`/`NhomOffice`), chỉ Máy tính/Laptop.
- 08/10 — Mục Quản lý AP cùng khuôn camera; máy dev nên `AccessPoint__GhiLichSu=false`.
- 06/10 — Xem camera 3 chế độ; ảnh lưu sẵn do `CameraAnhLuuWorker` chụp từ máy dev.
- 06/10 — Tab Lịch sử camera: `CameraLichSuWorker` ghi `KK_CameraLichSu` (append-only).

## Ghi chú phiên sau

- `kk-wkna-bosung-20260917.sql` chưa chạy (tính đến 17/09/2026).
- Form còn submit đồng bộ: [`00-master-plan.md`](00-master-plan.md) mục 4.
- Màu thẻ đơn IT đã dùng 10 hue (21°…333°), hở lớn nhất ~64° (78°–142°) → đơn mới phân biệt bằng độ đậm/icon.
