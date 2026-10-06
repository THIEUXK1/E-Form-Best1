/* ============================================================================
   Mục đích : Cột "Ghi chú" người dùng tự ghi cho từng camera ở tab Giám sát của /QLCamera.
              Danh sách camera lấy từ hệ thống ISAPI (không nằm trong DB E-Form) nên ghi chú
              lưu riêng, khoá theo đầu ghi + kênh (IP camera có thể đổi khi thay camera).

   Phạm vi   : - TẠO MỚI 1 bảng : dbo.KK_CameraGhiChu (không đụng bảng cũ)
               - 1 unique index (nvr_ip, kenh): mỗi kênh đúng một dòng ghi chú
   An toàn   : Idempotent. Rollback: kk-camera-ghi-chu-20261006-rollback.sql
               Lịch sử sửa ghi chú (trước/sau) ghi ở KK_LichSuThaoTac.
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SELECT COUNT(*) AS bang_da_ton_tai
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChu';
-- Kỳ vọng lần chạy đầu: 0
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChu')
BEGIN
    CREATE TABLE dbo.KK_CameraGhiChu
    (
        id_ghi_chu      int             IDENTITY(1,1) NOT NULL,
        nvr_ip          varchar(50)     NOT NULL,
        kenh            int             NOT NULL,
        ten_camera      nvarchar(255)   NULL,         -- Tên camera lúc ghi, để đọc DB còn biết là con nào
        ghi_chu         nvarchar(1000)  NULL,         -- Xoá trắng = NULL, không xoá dòng
        nguoi_cap_nhat  nvarchar(255)   NULL,
        ngay_cap_nhat   datetime        NULL CONSTRAINT DF_KK_CameraGhiChu_NgayCapNhat DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_CameraGhiChu PRIMARY KEY CLUSTERED (id_ghi_chu)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_CameraGhiChu_NvrKenh'
                 AND object_id = OBJECT_ID('dbo.KK_CameraGhiChu'))
BEGIN
    CREATE UNIQUE INDEX UQ_KK_CameraGhiChu_NvrKenh
        ON dbo.KK_CameraGhiChu (nvr_ip, kenh);
END
GO

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_CameraGhiChu'
ORDER BY ORDINAL_POSITION;
-- Kỳ vọng: 7 cột

SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.KK_CameraGhiChu');
-- Kỳ vọng: PK_KK_CameraGhiChu, UQ_KK_CameraGhiChu_NvrKenh
GO
