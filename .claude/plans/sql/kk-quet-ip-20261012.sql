/* ============================================================================
   Mục đích : Quét dải IP máy chủ (mặc định PFVN 10.0.193.1–254) trên trang /QLSwitch/{công ty}:
              job nền QuetIpWorker ping cả dải, IP nào từng trả lời thì giữ 1 dòng theo dõi UP/DOWN
              + nhật ký đổi trạng thái (giống Switch) để thống kê server online/offline.

   Phạm vi   : TẠO MỚI 2 bảng (không đụng bảng/dữ liệu nào có sẵn)
               - dbo.KK_QuetIp        : mỗi IP đã từng thấy trả lời ping trong dải quét + trạng thái gần nhất
               - dbo.KK_QuetIpLichSu  : nhật ký UP <-> DOWN — APPEND-ONLY, không sửa/xoá
               - Unique (IDCongTy, dia_chi_ip): 2 máy chủ web cùng quét không tạo trùng dòng.
               - Unique (id_quet_ip, thoi_gian, sang_trang_thai) chặn ghi trùng sự kiện.

   An toàn   : Chỉ tạo mới, không ALTER/UPDATE bảng cũ. Idempotent - chạy lại không nổ.
   Rollback  : kk-quet-ip-20261012-rollback.sql
   Lưu ý chạy: file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   Thứ tự    : chạy script này TRƯỚC khi deploy code (job nền chạm bảng chưa có sẽ log lỗi mỗi chu kỳ).
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('KK_QuetIp', 'KK_QuetIpLichSu');
-- Kỳ vọng lần chạy đầu: 0 dòng

SELECT IDCongTy, TenCongTy FROM dbo.KK_CongTy WHERE TenCongTy = N'PFVN';
-- Kỳ vọng: 1 dòng
GO


/* ---------- 2. BẢNG IP ĐANG THEO DÕI ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_QuetIp')
BEGIN
    CREATE TABLE dbo.KK_QuetIp
    (
        id_quet_ip          int            IDENTITY(1,1) NOT NULL,
        IDCongTy            int            NOT NULL,
        dai_mang            varchar(20)    NOT NULL,          -- 3 octet đầu, VD: 10.0.193
        dia_chi_ip          varchar(50)    NOT NULL,
        ten_may             nvarchar(255)  NULL,              -- Tên DNS ngược (PTR), NULL = chưa phân giải được
        trang_thai_ket_noi  varchar(20)    NULL,              -- UP / DOWN theo lần ping gần nhất
        doi_trang_thai_luc  datetime       NULL,
        kiem_tra_luc        datetime       NULL,
        lan_dau_thay        datetime       NOT NULL CONSTRAINT DF_KK_QuetIp_LanDau DEFAULT (GETDATE()),
        lan_cuoi_song       datetime       NULL,              -- Lần gần nhất ping trả lời
        CONSTRAINT PK_KK_QuetIp PRIMARY KEY CLUSTERED (id_quet_ip),
        CONSTRAINT FK_KK_QuetIp_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.KK_CongTy (IDCongTy)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_QuetIp_CongTyIp' AND object_id = OBJECT_ID('dbo.KK_QuetIp'))
    CREATE UNIQUE INDEX UQ_KK_QuetIp_CongTyIp ON dbo.KK_QuetIp (IDCongTy, dia_chi_ip);
GO


/* ---------- 3. NHẬT KÝ ĐỔI TRẠNG THÁI (APPEND-ONLY) ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_QuetIpLichSu')
BEGIN
    CREATE TABLE dbo.KK_QuetIpLichSu
    (
        id_lich_su       bigint         IDENTITY(1,1) NOT NULL,
        id_quet_ip       int            NOT NULL,
        IDCongTy         int            NOT NULL,
        thoi_gian        datetime       NOT NULL,
        dia_chi_ip       varchar(50)    NULL,
        ten_may          nvarchar(255)  NULL,              -- chụp lại tên lúc xảy ra
        tu_trang_thai    varchar(20)    NULL,
        sang_trang_thai  varchar(20)    NOT NULL,
        thoi_luong_giay  int            NULL,              -- Khi UP lại: đã mất kết nối bao lâu
        ghi_nhan_luc     datetime       NOT NULL CONSTRAINT DF_KK_QuetIpLichSu_GhiNhan DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_QuetIpLichSu PRIMARY KEY CLUSTERED (id_lich_su),
        CONSTRAINT FK_KK_QuetIpLichSu_QuetIp FOREIGN KEY (id_quet_ip) REFERENCES dbo.KK_QuetIp (id_quet_ip)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_QuetIpLichSu_SuKien' AND object_id = OBJECT_ID('dbo.KK_QuetIpLichSu'))
    CREATE UNIQUE INDEX UQ_KK_QuetIpLichSu_SuKien ON dbo.KK_QuetIpLichSu (id_quet_ip, thoi_gian, sang_trang_thai);
GO

/* Tab lịch sử lọc theo công ty + khoảng ngày */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KK_QuetIpLichSu_CongTyThoiGian' AND object_id = OBJECT_ID('dbo.KK_QuetIpLichSu'))
    CREATE INDEX IX_KK_QuetIpLichSu_CongTyThoiGian ON dbo.KK_QuetIpLichSu (IDCongTy, thoi_gian);
GO


/* ---------- 4. ĐỐI SOÁT SAU ---------- */
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('KK_QuetIp', 'KK_QuetIpLichSu');
-- Kỳ vọng: 2 dòng
GO
