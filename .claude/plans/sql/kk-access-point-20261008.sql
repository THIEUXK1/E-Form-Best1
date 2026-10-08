/* ============================================================================
   Mục đích : Mục "Quản lý AP" (Access Point Wi-Fi) cho 3 công ty BPVN · PFVN · MEGA, cùng khuôn
              "Quản lý camera": danh sách tài sản AP nhập tay + job nền ping theo dõi online/offline
              + nhật ký đổi trạng thái để làm báo cáo theo kỳ.

   Phạm vi   : TẠO MỚI 2 bảng (không đụng bảng/dữ liệu nào có sẵn)
               - dbo.KK_AccessPoint        : tài sản AP + trạng thái kết nối gần nhất (soft-delete ngay_xoa)
               - dbo.KK_AccessPointLichSu  : nhật ký UP <-> DOWN — APPEND-ONLY, không sửa/xoá
               - Khoá ngoại sang KK_CongTy / KK_BoPhan (giống KK_Camera)
               - Unique lọc (IDCongTy, dia_chi_ip) cho AP còn hiệu lực: 3 công ty là 3 mạng riêng nên
                 dải IP nội bộ có thể trùng nhau giữa công ty, nhưng trong 1 công ty thì không.
               - Unique (id_ap, thoi_gian, sang_trang_thai) chặn ghi trùng sự kiện.

   An toàn   : Chỉ tạo mới, không ALTER/UPDATE bảng cũ. Idempotent - chạy lại không nổ.
   Rollback  : kk-access-point-20261008-rollback.sql
   Lưu ý chạy: file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   Thứ tự    : chạy script này TRƯỚC khi deploy code QLAP (model mới chạm bảng chưa có là lỗi runtime).
   ========================================================================== */

USE ITForm;
GO

/* sqlcmd mặc định QUOTED_IDENTIFIER OFF -> filtered index sẽ lỗi Msg 1934 */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('KK_AccessPoint', 'KK_AccessPointLichSu');
-- Kỳ vọng lần chạy đầu: 0 dòng

SELECT IDCongTy, TenCongTy FROM dbo.KK_CongTy WHERE TenCongTy IN (N'BPVN', N'PFVN', N'MEGA');
-- Kỳ vọng: đủ 3 dòng (trang /QLAP/{công ty} tra IDCongTy theo tên)
GO


/* ---------- 2. BẢNG TÀI SẢN AP ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_AccessPoint')
BEGIN
    CREATE TABLE dbo.KK_AccessPoint
    (
        id_ap               int            IDENTITY(1,1) NOT NULL,
        ma_ap               nvarchar(100)  NULL,              -- Mã tài sản / mã dán trên AP
        ten_ap              nvarchar(255)  NOT NULL,          -- VD: AP Văn phòng tầng 2
        dia_chi_ip          varchar(50)    NULL,
        dia_chi_mac         varchar(20)    NULL,              -- AA:BB:CC:DD:EE:FF
        hang_san_xuat       nvarchar(100)  NULL,              -- UniFi, Aruba, Ruijie, TP-Link...
        model               nvarchar(100)  NULL,
        serial              nvarchar(100)  NULL,
        ssid                nvarchar(255)  NULL,              -- Các SSID phát, cách nhau dấu phẩy
        controller          nvarchar(255)  NULL,              -- Controller quản lý (tên/IP), để trống nếu standalone
        IDCongTy            int            NOT NULL,
        IDBoPhan            int            NULL,
        vi_tri              nvarchar(500)  NULL,
        tinh_trang          nvarchar(100)  NULL,
        ngay_lap_dat        date           NULL,
        han_bao_hanh        date           NULL,
        ghi_chu             nvarchar(max)  NULL,
        trang_thai_ket_noi  varchar(20)    NULL,              -- UP / DOWN do job theo dõi ghi; NULL = chưa kiểm
        doi_trang_thai_luc  datetime       NULL,              -- Lúc chuyển sang trạng thái hiện tại
        kiem_tra_luc        datetime       NULL,              -- Lần kiểm gần nhất
        nguon_trang_thai    varchar(10)    NULL,              -- AC = đọc từ AP controller, PING = máy chủ tự ping
        -- 5 cột dưới do job đọc từ AP controller (Huawei AC...) ghi đè mỗi lượt, không nhập tay
        trang_thai_controller varchar(30)  NULL,              -- normal / fault / idle... đúng như controller trả
        nhom_ap             nvarchar(100)  NULL,              -- AP group trên controller
        phien_ban           nvarchar(100)  NULL,              -- Firmware
        so_client           int            NULL,              -- Số thiết bị đang kết nối
        thoi_gian_chay_giay bigint         NULL,              -- Uptime
        nguoi_tao           nvarchar(255)  NULL,
        ngay_tao            datetime       NULL CONSTRAINT DF_KK_AccessPoint_NgayTao DEFAULT (GETDATE()),
        ngay_cap_nhat       datetime       NULL,
        ngay_xoa            datetime       NULL,              -- Soft delete: NULL = còn hiệu lực
        ly_do_xoa           nvarchar(500)  NULL,
        CONSTRAINT PK_KK_AccessPoint PRIMARY KEY CLUSTERED (id_ap),
        CONSTRAINT FK_KK_AccessPoint_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.KK_CongTy (IDCongTy),
        CONSTRAINT FK_KK_AccessPoint_BoPhan FOREIGN KEY (IDBoPhan) REFERENCES dbo.KK_BoPhan (IDBoPhan)
    );
END
GO

/* AP đã xoá mềm không giữ chỗ IP, lắp AP mới vào lại đúng IP đó được */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_AccessPoint_CongTyIp' AND object_id = OBJECT_ID('dbo.KK_AccessPoint'))
    CREATE UNIQUE INDEX UQ_KK_AccessPoint_CongTyIp
        ON dbo.KK_AccessPoint (IDCongTy, dia_chi_ip)
        WHERE dia_chi_ip IS NOT NULL AND ngay_xoa IS NULL;
GO

/* MAC là khoá đồng bộ với AP controller: bấm "Đồng bộ từ controller" 2 lần không được tạo 2 dòng */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_AccessPoint_Mac' AND object_id = OBJECT_ID('dbo.KK_AccessPoint'))
    CREATE UNIQUE INDEX UQ_KK_AccessPoint_Mac
        ON dbo.KK_AccessPoint (dia_chi_mac)
        WHERE dia_chi_mac IS NOT NULL AND ngay_xoa IS NULL;
GO


/* ---------- 3. NHẬT KÝ ĐỔI TRẠNG THÁI (APPEND-ONLY) ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_AccessPointLichSu')
BEGIN
    CREATE TABLE dbo.KK_AccessPointLichSu
    (
        id_lich_su       bigint         IDENTITY(1,1) NOT NULL,
        id_ap            int            NOT NULL,
        IDCongTy         int            NOT NULL,
        thoi_gian        datetime       NOT NULL,          -- lúc phát hiện đổi trạng thái
        ten_ap           nvarchar(255)  NULL,              -- chụp lại tên/IP lúc xảy ra (AP có thể đổi tên sau)
        dia_chi_ip       varchar(50)    NULL,
        tu_trang_thai    varchar(20)    NULL,
        sang_trang_thai  varchar(20)    NOT NULL,
        thoi_luong_giay  int            NULL,              -- Khi UP lại: đã mất kết nối bao lâu
        ghi_nhan_luc     datetime       NOT NULL CONSTRAINT DF_KK_AccessPointLichSu_GhiNhan DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_AccessPointLichSu PRIMARY KEY CLUSTERED (id_lich_su),
        CONSTRAINT FK_KK_AccessPointLichSu_AP FOREIGN KEY (id_ap) REFERENCES dbo.KK_AccessPoint (id_ap)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_AccessPointLichSu_SuKien' AND object_id = OBJECT_ID('dbo.KK_AccessPointLichSu'))
    CREATE UNIQUE INDEX UQ_KK_AccessPointLichSu_SuKien ON dbo.KK_AccessPointLichSu (id_ap, thoi_gian, sang_trang_thai);
GO

/* Tab lịch sử + báo cáo lọc theo công ty và khoảng ngày */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KK_AccessPointLichSu_CongTyThoiGian' AND object_id = OBJECT_ID('dbo.KK_AccessPointLichSu'))
    CREATE INDEX IX_KK_AccessPointLichSu_CongTyThoiGian ON dbo.KK_AccessPointLichSu (IDCongTy, thoi_gian DESC);
GO


/* ---------- 4. ĐỐI SOÁT SAU ---------- */
SELECT TABLE_NAME, COUNT(*) AS so_cot
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('KK_AccessPoint', 'KK_AccessPointLichSu')
GROUP BY TABLE_NAME;
-- Kỳ vọng: KK_AccessPoint 31 cột, KK_AccessPointLichSu 10 cột

SELECT t.name AS bang, i.name AS chi_muc
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN ('KK_AccessPoint', 'KK_AccessPointLichSu') AND i.name IS NOT NULL
ORDER BY t.name, i.name;
-- Kỳ vọng: KK_AccessPoint: PK_..., UQ_..._CongTyIp, UQ_..._Mac ; KK_AccessPointLichSu: IX_..._CongTyThoiGian, PK_..., UQ_..._SuKien

SELECT (SELECT COUNT(*) FROM dbo.KK_AccessPoint) AS so_ap, (SELECT COUNT(*) FROM dbo.KK_AccessPointLichSu) AS so_su_kien;
-- Kỳ vọng: 0, 0
GO
