/* ============================================================================
   Mục đích : Mục "Quản lý Switch" (switch mạng) cho 3 công ty BPVN · PFVN · MEGA, cùng khuôn
              "Quản lý AP": danh sách tài sản switch nhập tay + job nền ping theo dõi online/offline
              + nhật ký đổi trạng thái để làm báo cáo theo kỳ.

   Phạm vi   : TẠO MỚI 2 bảng (không đụng bảng/dữ liệu nào có sẵn)
               - dbo.KK_Switch        : tài sản switch + trạng thái kết nối gần nhất (soft-delete ngay_xoa)
               - dbo.KK_SwitchLichSu  : nhật ký UP <-> DOWN — APPEND-ONLY, không sửa/xoá
               - Khoá ngoại sang KK_CongTy / KK_BoPhan (giống KK_Camera)
               - Unique lọc (IDCongTy, dia_chi_ip) cho switch còn hiệu lực: 3 công ty là 3 mạng riêng nên
                 dải IP nội bộ có thể trùng nhau giữa công ty, nhưng trong 1 công ty thì không.
               - Unique (id_switch, thoi_gian, sang_trang_thai) chặn ghi trùng sự kiện.

   An toàn   : Chỉ tạo mới, không ALTER/UPDATE bảng cũ. Idempotent - chạy lại không nổ.
   Rollback  : kk-switch-20261008-rollback.sql
   Lưu ý chạy: file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   Thứ tự    : chạy script này TRƯỚC khi deploy code QLSwitch (model mới chạm bảng chưa có là lỗi runtime).
   ========================================================================== */

USE ITForm;
GO

/* sqlcmd mặc định QUOTED_IDENTIFIER OFF -> filtered index sẽ lỗi Msg 1934 */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('KK_Switch', 'KK_SwitchLichSu');
-- Kỳ vọng lần chạy đầu: 0 dòng

SELECT IDCongTy, TenCongTy FROM dbo.KK_CongTy WHERE TenCongTy IN (N'BPVN', N'PFVN', N'MEGA');
-- Kỳ vọng: đủ 3 dòng (trang /QLSwitch/{công ty} tra IDCongTy theo tên)
GO


/* ---------- 2. BẢNG TÀI SẢN switch ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_Switch')
BEGIN
    CREATE TABLE dbo.KK_Switch
    (
        id_switch           int            IDENTITY(1,1) NOT NULL,
        ma_switch           nvarchar(100)  NULL,              -- Mã tài sản / mã dán trên switch
        ten_switch          nvarchar(255)  NOT NULL,          -- VD: SW-X1-Core
        dia_chi_ip          varchar(50)    NULL,              -- IP quản lý
        dia_chi_mac         varchar(20)    NULL,              -- AA:BB:CC:DD:EE:FF
        hang_san_xuat       nvarchar(100)  NULL,              -- Huawei, Cisco, Aruba, TP-Link...
        model               nvarchar(100)  NULL,
        serial              nvarchar(100)  NULL,
        so_cong             int            NULL,              -- Số cổng (24, 48...)
        vai_tro             nvarchar(50)   NULL,              -- Core / Phân phối / Truy cập
        phien_ban           nvarchar(100)  NULL,              -- Firmware, nhập tay
        IDCongTy            int            NOT NULL,
        IDBoPhan            int            NULL,
        vi_tri              nvarchar(500)  NULL,              -- Phòng / tủ rack
        tinh_trang          nvarchar(100)  NULL,
        ngay_lap_dat        date           NULL,
        han_bao_hanh        date           NULL,
        ghi_chu             nvarchar(max)  NULL,
        trang_thai_ket_noi  varchar(20)    NULL,              -- UP / DOWN do job theo dõi ghi; NULL = chưa kiểm
        doi_trang_thai_luc  datetime       NULL,              -- Lúc chuyển sang trạng thái hiện tại
        kiem_tra_luc        datetime       NULL,              -- Lần kiểm gần nhất
        nguoi_tao          nvarchar(255)  NULL,
        ngay_tao            datetime       NULL CONSTRAINT DF_KK_Switch_NgayTao DEFAULT (GETDATE()),
        ngay_cap_nhat       datetime       NULL,
        ngay_xoa            datetime       NULL,              -- Soft delete: NULL = còn hiệu lực
        ly_do_xoa           nvarchar(500)  NULL,
        CONSTRAINT PK_KK_Switch PRIMARY KEY CLUSTERED (id_switch),
        CONSTRAINT FK_KK_Switch_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.KK_CongTy (IDCongTy),
        CONSTRAINT FK_KK_Switch_BoPhan FOREIGN KEY (IDBoPhan) REFERENCES dbo.KK_BoPhan (IDBoPhan)
    );
END
GO

/* switch đã xoá mềm không giữ chỗ IP, lắp switch mới vào lại đúng IP đó được */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_Switch_CongTyIp' AND object_id = OBJECT_ID('dbo.KK_Switch'))
    CREATE UNIQUE INDEX UQ_KK_Switch_CongTyIp
        ON dbo.KK_Switch (IDCongTy, dia_chi_ip)
        WHERE dia_chi_ip IS NOT NULL AND ngay_xoa IS NULL;
GO

/* Một MAC chỉ thuộc một switch còn hiệu lực (chặn nhập trùng thiết bị) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_Switch_Mac' AND object_id = OBJECT_ID('dbo.KK_Switch'))
    CREATE UNIQUE INDEX UQ_KK_Switch_Mac
        ON dbo.KK_Switch (dia_chi_mac)
        WHERE dia_chi_mac IS NOT NULL AND ngay_xoa IS NULL;
GO


/* ---------- 3. NHẬT KÝ ĐỔI TRẠNG THÁI (APPEND-ONLY) ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_SwitchLichSu')
BEGIN
    CREATE TABLE dbo.KK_SwitchLichSu
    (
        id_lich_su       bigint         IDENTITY(1,1) NOT NULL,
        id_switch            int            NOT NULL,
        IDCongTy         int            NOT NULL,
        thoi_gian        datetime       NOT NULL,          -- lúc phát hiện đổi trạng thái
        ten_switch           nvarchar(255)  NULL,              -- chụp lại tên/IP lúc xảy ra (switch có thể đổi tên sau)
        dia_chi_ip       varchar(50)    NULL,
        tu_trang_thai    varchar(20)    NULL,
        sang_trang_thai  varchar(20)    NOT NULL,
        thoi_luong_giay  int            NULL,              -- Khi UP lại: đã mất kết nối bao lâu
        ghi_nhan_luc     datetime       NOT NULL CONSTRAINT DF_KK_SwitchLichSu_GhiNhan DEFAULT (GETDATE()),
        CONSTRAINT PK_KK_SwitchLichSu PRIMARY KEY CLUSTERED (id_lich_su),
        CONSTRAINT FK_KK_SwitchLichSu_Switch FOREIGN KEY (id_switch) REFERENCES dbo.KK_Switch (id_switch)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_SwitchLichSu_SuKien' AND object_id = OBJECT_ID('dbo.KK_SwitchLichSu'))
    CREATE UNIQUE INDEX UQ_KK_SwitchLichSu_SuKien ON dbo.KK_SwitchLichSu (id_switch, thoi_gian, sang_trang_thai);
GO

/* Tab lịch sử + báo cáo lọc theo công ty và khoảng ngày */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KK_SwitchLichSu_CongTyThoiGian' AND object_id = OBJECT_ID('dbo.KK_SwitchLichSu'))
    CREATE INDEX IX_KK_SwitchLichSu_CongTyThoiGian ON dbo.KK_SwitchLichSu (IDCongTy, thoi_gian DESC);
GO


/* ---------- 4. ĐỐI SOÁT SAU ---------- */
SELECT TABLE_NAME, COUNT(*) AS so_cot
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('KK_Switch', 'KK_SwitchLichSu')
GROUP BY TABLE_NAME;
-- Kỳ vọng: KK_Switch 26 cột, KK_SwitchLichSu 10 cột

SELECT t.name AS bang, i.name AS chi_muc
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN ('KK_Switch', 'KK_SwitchLichSu') AND i.name IS NOT NULL
ORDER BY t.name, i.name;
-- Kỳ vọng: KK_Switch: PK_..., UQ_..._CongTyIp, UQ_..._Mac ; KK_SwitchLichSu: IX_..._CongTyThoiGian, PK_..., UQ_..._SuKien

SELECT (SELECT COUNT(*) FROM dbo.KK_Switch) AS so_switch, (SELECT COUNT(*) FROM dbo.KK_SwitchLichSu) AS so_su_kien;
-- Kỳ vọng: 0, 0
GO
