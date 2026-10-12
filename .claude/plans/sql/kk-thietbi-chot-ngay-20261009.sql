/* ============================================================================
   Mục đích : So sánh bản quyền Windows / Office / "Cần cài Office" / trạng thái thiết bị giữa các kỳ
              (Báo cáo định kì, tab Thiết bị). KK_ThietBi chỉ giữ giá trị hiện tại (ghi đè khi agent đồng bộ
              hoặc nhập Excel) nên phải chụp số liệu tổng hợp mỗi ngày để có số của kỳ trước.

   Phạm vi   : TẠO MỚI 1 bảng (không đụng bảng/dữ liệu nào có sẵn)
               - dbo.KK_ThietBiChotNgay : số lượng thiết bị theo (ngày, công ty, chỉ tiêu, giá trị)
                 do job nền ThietBiChotNgayWorker ghi 1 lần/ngày — APPEND-ONLY, không sửa/xoá.
                 Chỉ tiêu: win | office (chỉ Máy tính/Laptop, nhóm như báo cáo bản quyền BPVN 280926) · trang_thai (mọi thiết bị).
               - Unique (ngay, cong_ty, chi_tieu, gia_tri) chặn chụp đôi khi 2 máy chủ cùng chạy job.
               Dung lượng: ~3 công ty × ~20 giá trị ≈ 60 dòng/ngày ≈ 22k dòng/năm.

   An toàn   : Chỉ tạo mới, không ALTER/UPDATE bảng cũ. Idempotent - chạy lại không nổ.
   Rollback  : kk-thietbi-chot-ngay-20261009-rollback.sql
   Lưu ý chạy: file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   Thứ tự    : chạy script này TRƯỚC khi deploy code (job chạm bảng chưa có chỉ log lỗi, không làm sập app,
               nhưng sẽ không có bản chụp nào cho tới khi có bảng).
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ThietBiChotNgay';
-- Kỳ vọng lần chạy đầu: 0 dòng
GO


/* ---------- 2. BẢNG CHỤP SỐ LIỆU HẰNG NGÀY (APPEND-ONLY) ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ThietBiChotNgay')
BEGIN
    CREATE TABLE dbo.KK_ThietBiChotNgay
    (
        id_chot       bigint         IDENTITY(1,1) NOT NULL,
        ngay          date           NOT NULL,          -- ngày chụp (giờ VN)
        cong_ty       nvarchar(255)  NOT NULL,          -- tên công ty lúc chụp; "Chưa xác định" nếu thiết bị chưa gán
        chi_tieu      varchar(30)    NOT NULL,          -- win / office / trang_thai
        gia_tri       nvarchar(255)  NOT NULL,          -- VD: "Đạt - MAK công ty", "Cần Office - chưa có key (cần mua)", "Hoạt động"
        so_luong      int            NOT NULL,
        ghi_nhan_luc  datetime       NOT NULL CONSTRAINT DF_KK_ThietBiChotNgay_GhiNhan DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_ThietBiChotNgay PRIMARY KEY CLUSTERED (id_chot)
    );
END
GO

/* Một ngày chỉ một bản chụp cho mỗi (công ty, chỉ tiêu, giá trị) — job chạy lại/2 máy chủ không nhân đôi */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_ThietBiChotNgay_Ngay' AND object_id = OBJECT_ID('dbo.KK_ThietBiChotNgay'))
    CREATE UNIQUE INDEX UQ_KK_ThietBiChotNgay_Ngay
        ON dbo.KK_ThietBiChotNgay (ngay, cong_ty, chi_tieu, gia_tri)
        INCLUDE (so_luong);
GO


/* ---------- 3. ĐỐI SOÁT SAU ---------- */
SELECT COUNT(*) AS so_cot FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KK_ThietBiChotNgay';
-- Kỳ vọng: 7

SELECT i.name AS chi_muc FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.KK_ThietBiChotNgay') AND i.name IS NOT NULL ORDER BY i.name;
-- Kỳ vọng: PK_KK_ThietBiChotNgay, UQ_KK_ThietBiChotNgay_Ngay

SELECT COUNT(*) AS so_dong FROM dbo.KK_ThietBiChotNgay;
-- Kỳ vọng: 0
GO
