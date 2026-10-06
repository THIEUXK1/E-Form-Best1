/* ============================================================================
   Mục đích : Danh sách ghi chú có sẵn cho camera (/QLCamera). Người dùng bấm "+" để lưu một
              câu ghi chú thành mẫu, lần sau chỉ cần chọn thay vì gõ. Dùng chung cho mọi người.

   Phạm vi   : - TẠO MỚI 1 bảng : dbo.KK_CameraGhiChuMau (không đụng bảng cũ)
               - 1 unique index lọc: không trùng nội dung trong các mẫu còn hiệu lực
   An toàn   : Idempotent. Xoá mẫu = xoá mềm (ngay_xoa), không xoá cứng.
   Rollback  : kk-camera-ghi-chu-mau-20261006-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SELECT COUNT(*) AS bang_da_ton_tai
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChuMau';
-- Kỳ vọng lần chạy đầu: 0
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChuMau')
BEGIN
    CREATE TABLE dbo.KK_CameraGhiChuMau
    (
        id_mau      int            IDENTITY(1,1) NOT NULL,
        noi_dung    nvarchar(200)  NOT NULL,
        nguoi_tao   nvarchar(255)  NULL,
        ngay_tao    datetime       NULL CONSTRAINT DF_KK_CameraGhiChuMau_NgayTao DEFAULT (GETDATE()),
        ngay_xoa    datetime       NULL,              -- Soft delete: NULL = còn hiệu lực
        nguoi_xoa   nvarchar(255)  NULL,
        CONSTRAINT PK_KK_CameraGhiChuMau PRIMARY KEY CLUSTERED (id_mau)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_CameraGhiChuMau_NoiDung'
                 AND object_id = OBJECT_ID('dbo.KK_CameraGhiChuMau'))
BEGIN
    CREATE UNIQUE INDEX UQ_KK_CameraGhiChuMau_NoiDung
        ON dbo.KK_CameraGhiChuMau (noi_dung)
        WHERE ngay_xoa IS NULL;
END
GO

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_CameraGhiChuMau'
ORDER BY ORDINAL_POSITION;
-- Kỳ vọng: 6 cột

SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.KK_CameraGhiChuMau');
-- Kỳ vọng: PK_KK_CameraGhiChuMau, UQ_KK_CameraGhiChuMau_NoiDung
GO
