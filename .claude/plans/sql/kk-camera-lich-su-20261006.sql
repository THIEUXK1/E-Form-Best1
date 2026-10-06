/* ============================================================================
   Mục đích : Tab "Lịch sử" ở /QLCamera — camera nào chuyển Hoạt động -> Mất kết nối hoặc
              ngược lại, lúc nào, tên gì, IP nào, ở đầu ghi nào.
              Hệ thống ISAPI chỉ cho trạng thái HIỆN TẠI, nên job nền CameraLichSuWorker của
              E-Form đọc định kỳ, so với trạng thái lần trước và ghi sự kiện khi có thay đổi.

   Phạm vi   : TẠO MỚI 2 bảng (không đụng bảng cũ)
               - dbo.KK_CameraTrangThai : trạng thái gần nhất của từng kênh (để so sánh), 1 dòng/kênh
               - dbo.KK_CameraLichSu    : nhật ký sự kiện — APPEND-ONLY, không sửa/xoá
   Idempotent: unique (nvr_ip, kenh, thoi_gian, sang_trang_thai) chặn ghi trùng khi job chạy
               lại hoặc chạy song song trên 2 máy chủ.
   Rollback  : kk-camera-lich-su-20261006-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('KK_CameraTrangThai', 'KK_CameraLichSu');
-- Kỳ vọng lần chạy đầu: 0 dòng
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraTrangThai')
BEGIN
    CREATE TABLE dbo.KK_CameraTrangThai
    (
        id_trang_thai   int            IDENTITY(1,1) NOT NULL,
        nvr_ip          varchar(50)    NOT NULL,
        kenh            int            NOT NULL,
        trang_thai      varchar(20)    NOT NULL,       -- UP / DOWN / ... đúng như ISAPI trả
        ten_camera      nvarchar(255)  NULL,
        ip_camera       varchar(50)    NULL,
        doi_luc         datetime       NULL,           -- status_changed_at của ISAPI
        cap_nhat_luc    datetime       NOT NULL CONSTRAINT DF_KK_CameraTrangThai_CapNhat DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_CameraTrangThai PRIMARY KEY CLUSTERED (id_trang_thai)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_CameraTrangThai_NvrKenh' AND object_id = OBJECT_ID('dbo.KK_CameraTrangThai'))
    CREATE UNIQUE INDEX UQ_KK_CameraTrangThai_NvrKenh ON dbo.KK_CameraTrangThai (nvr_ip, kenh);
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraLichSu')
BEGIN
    CREATE TABLE dbo.KK_CameraLichSu
    (
        id_lich_su       bigint         IDENTITY(1,1) NOT NULL,
        thoi_gian        datetime       NOT NULL,       -- lúc đổi trạng thái (status_changed_at)
        nvr_ip           varchar(50)    NOT NULL,
        ten_dau_ghi      nvarchar(255)  NULL,
        khu_vuc          nvarchar(100)  NULL,
        kenh             int            NOT NULL,
        ten_camera       nvarchar(255)  NULL,
        ip_camera        varchar(50)    NULL,
        tu_trang_thai    varchar(20)    NULL,
        sang_trang_thai  varchar(20)    NOT NULL,
        thoi_luong_giay  int            NULL,           -- Khi hoạt động lại: đã mất kết nối bao lâu
        ghi_nhan_luc     datetime       NOT NULL CONSTRAINT DF_KK_CameraLichSu_GhiNhan DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_CameraLichSu PRIMARY KEY CLUSTERED (id_lich_su)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_CameraLichSu_SuKien' AND object_id = OBJECT_ID('dbo.KK_CameraLichSu'))
    CREATE UNIQUE INDEX UQ_KK_CameraLichSu_SuKien ON dbo.KK_CameraLichSu (nvr_ip, kenh, thoi_gian, sang_trang_thai);
GO

/* Tab lịch sử lọc theo khoảng ngày, mới nhất trước */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KK_CameraLichSu_ThoiGian' AND object_id = OBJECT_ID('dbo.KK_CameraLichSu'))
    CREATE INDEX IX_KK_CameraLichSu_ThoiGian ON dbo.KK_CameraLichSu (thoi_gian DESC);
GO

SELECT t.name AS bang, i.name AS chi_muc
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN ('KK_CameraTrangThai', 'KK_CameraLichSu') AND i.name IS NOT NULL
ORDER BY t.name, i.name;
-- Kỳ vọng: KK_CameraLichSu: IX_..._ThoiGian, PK_..., UQ_..._SuKien ; KK_CameraTrangThai: PK_..., UQ_..._NvrKenh
GO
