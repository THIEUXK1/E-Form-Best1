/* ============================================================================
   Mục đích : Thêm mục "Quản lý camera" vào nhóm menu "Máy in & CCDC".
              Camera quản theo từng con có IP + đầu ghi/kênh, khác hẳn CCDC (quản theo
              số lượng) nên tách bảng riêng thay vì nhồi vào KK_ThietBi / KK_CongCuDungCu.

   Phạm vi   : - TẠO MỚI 1 bảng : dbo.KK_Camera   (không đụng bảng/dữ liệu nào có sẵn)
               - 2 khoá ngoại sang KK_CongTy / KK_BoPhan (giống KK_CongCuDungCu)
               - 1 unique index lọc: không cho 2 camera CÒN HIỆU LỰC trùng IP

   An toàn   : Chỉ tạo mới, không ALTER/UPDATE bảng cũ. Idempotent - chạy lại không nổ.
   Rollback  : kk-camera-20261006-rollback.sql
   Lưu ý chạy: file có tiếng Việt -> chạy bằng sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

/* sqlcmd mặc định QUOTED_IDENTIFIER OFF -> CREATE INDEX có WHERE (filtered index) sẽ lỗi Msg 1934 */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT COUNT(*) AS bang_camera_da_ton_tai
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_Camera';
-- Kỳ vọng lần chạy đầu: 0
GO


/* ---------- 2. TẠO BẢNG ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_Camera')
BEGIN
    CREATE TABLE dbo.KK_Camera
    (
        id_camera      int            IDENTITY(1,1) NOT NULL,
        ma_camera      nvarchar(100)  NULL,              -- Mã tài sản / mã dán trên camera
        ten_camera     nvarchar(255)  NOT NULL,          -- VD: Cổng chính, Kho thành phẩm góc trái
        dia_chi_ip     varchar(50)    NULL,
        hang_san_xuat  nvarchar(100)  NULL,              -- Hikvision, Dahua, KBVision...
        model          nvarchar(100)  NULL,
        serial         nvarchar(100)  NULL,
        dau_ghi        nvarchar(255)  NULL,              -- Tên/IP đầu ghi NVR/DVR đang gắn
        kenh           int            NULL,              -- Kênh trên đầu ghi
        IDCongTy       int            NULL,
        IDBoPhan       int            NULL,
        vi_tri         nvarchar(500)  NULL,
        tinh_trang     nvarchar(100)  NULL,
        ngay_lap_dat   date           NULL,
        han_bao_hanh   date           NULL,
        ghi_chu        nvarchar(max)  NULL,
        nguoi_tao      nvarchar(255)  NULL,
        ngay_tao       datetime       NULL CONSTRAINT DF_KK_Camera_NgayTao DEFAULT (GETDATE()),
        ngay_cap_nhat  datetime       NULL,
        ngay_xoa       datetime       NULL,              -- Soft delete: NULL = còn hiệu lực
        ly_do_xoa      nvarchar(500)  NULL,
        CONSTRAINT PK_KK_Camera PRIMARY KEY CLUSTERED (id_camera),
        CONSTRAINT FK_KK_Camera_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.KK_CongTy (IDCongTy),
        CONSTRAINT FK_KK_Camera_BoPhan FOREIGN KEY (IDBoPhan) REFERENCES dbo.KK_BoPhan (IDBoPhan)
    );
END
GO

/* Chặn trùng IP ở tầng DB (bấm Lưu 2 lần / 2 người nhập cùng lúc). Lọc theo ngay_xoa để
   camera đã xoá mềm không giữ chỗ IP, cho phép lắp camera mới vào lại đúng IP đó. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_Camera_DiaChiIp'
                 AND object_id = OBJECT_ID('dbo.KK_Camera'))
BEGIN
    CREATE UNIQUE INDEX UQ_KK_Camera_DiaChiIp
        ON dbo.KK_Camera (dia_chi_ip)
        WHERE dia_chi_ip IS NOT NULL AND ngay_xoa IS NULL;
END
GO


/* ---------- 3. ĐỐI SOÁT SAU ---------- */
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_Camera'
ORDER BY ORDINAL_POSITION;
-- Kỳ vọng: 21 cột, chỉ id_camera và ten_camera là NOT NULL

SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.KK_Camera');
-- Kỳ vọng: PK_KK_Camera, UQ_KK_Camera_DiaChiIp

SELECT COUNT(*) AS so_camera FROM dbo.KK_Camera;
-- Kỳ vọng: 0
GO
