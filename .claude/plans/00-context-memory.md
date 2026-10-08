# Bộ nhớ bối cảnh — đọc đầu mỗi task, giữ ≤ 3 KB

> 5 quyết định gần nhất, tóm 1 dòng; chi tiết + cũ hơn ở [`decision-log.md`](decision-log.md).
> Ngày tuyệt đối dd/mm/yyyy.

## Đang làm (08/10/2026)

- Quản lý AP (`QLAPController`, `AccessPoint*` service) + quyền xem CCDC (`QLCCDCController`,
  `ccdc-quanly.js`) — **chưa commit**. Script SQL 07–08/10 trong `plans/sql/` chờ người dùng chạy.

## Blockers

| # | Vấn đề | Cần để gỡ |
|---|---|---|
| B1 | `KK_ThietBiChan` phải tạo tay trên `10.0.60.33` trước deploy | Người có quyền chạy DDL đã duyệt |
| B2 | Connection string dùng `sa` | Tài khoản SQL quyền tối thiểu + đổi mật khẩu `sa` |
| B3 | Chưa có gì gọi `/health/ready` | Uptime check |
| B6 | `KK_AccessPoint` + `KK_AccessPointLichSu` chưa tạo → `/QLAP/*` lỗi, `AccessPointPingWorker` log `Invalid object name` | Chạy `kk-access-point-20261008.sql` + `quyen-ap-cong-ty-20261008.sql` trước deploy; PFVN cần `AccessPointController__PFVN__*` trong `.env` |

## 5 quyết định gần nhất

- 08/10 — `/QLAP/PFVN` đồng bộ AP từ Huawei AC `10.0.198.199` theo MAC; mật khẩu AC có `&` sẽ hỏng đăng nhập.
- 08/10 — Mục Quản lý AP cùng khuôn camera; máy dev nên `AccessPoint__GhiLichSu=false`.
- 06/10 — Xem camera 3 chế độ; ảnh lưu sẵn do `CameraAnhLuuWorker` chụp từ máy dev.
- 06/10 — Tab Lịch sử camera: `CameraLichSuWorker` ghi `KK_CameraLichSu` (append-only).
- 06/10 — Xem trực tiếp camera qua go2rtc `127.0.0.1:1984`; production cần cài go2rtc (`.claude/pending/`).

## Ghi chú phiên sau

- `kk-wkna-bosung-20260917.sql` chưa chạy (tính đến 17/09/2026).
- Form còn submit đồng bộ: [`00-master-plan.md`](00-master-plan.md) mục 4.
- Màu thẻ đơn IT đã dùng 10 hue (21°…333°), hở lớn nhất ~64° (78°–142°) → đơn mới phân biệt bằng độ đậm/icon.
