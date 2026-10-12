/* ============================================================================
   Mục đích : Nạp danh sách switch/thiết bị mạng công ty PFVN vào dbo.KK_Switch để
              trang /QLSwitch/PFVN theo dõi online/offline (SwitchPingWorker ping theo IP).
   Nguồn    : Bảng kê switch PFVN do IT cung cấp (19 thiết bị).

   An toàn  : CHỈ INSERT, không ĐỘNG dữ liệu cũ. Idempotent theo (IDCongTy, dia_chi_ip):
              chạy lại không thêm trùng. Không chứa mật khẩu đăng nhập switch
              (bảng không có cột đó; theo quy tắc không hardcode secret trong .sql).
   Rollback : kk-switch-pfvn-data-20261009-rollback.sql
   Phụ thuộc: chạy SAU kk-switch-20261008.sql (phải có bảng KK_Switch + công ty PFVN).
   Lưu ý    : file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
DECLARE @IDCongTy int = (SELECT IDCongTy FROM dbo.KK_CongTy WHERE TenCongTy = N'PFVN');
IF @IDCongTy IS NULL
BEGIN
    RAISERROR(N'Chưa có công ty PFVN trong KK_CongTy - dừng, không nạp.', 16, 1);
    RETURN;
END

SELECT @IDCongTy AS IDCongTy_PFVN,
       (SELECT COUNT(*) FROM dbo.KK_Switch WHERE IDCongTy = @IDCongTy AND ngay_xoa IS NULL) AS switch_pfvn_hien_co;
-- Kỳ vọng lần chạy đầu: switch_pfvn_hien_co = 0 (hoặc số đang có nếu đã nhập tay trước đó)

/* ---------- 2. DỮ LIỆU NGUỒN ---------- */
-- vai_tro chỉ nhận: N'Core' / N'Phân phối' / N'Truy cập' (khớp QLSwitchController.DsVaiTro)
-- tinh_trang N'Đang hoạt động' để được job ping theo dõi.
DECLARE @src TABLE (
    ten_switch    nvarchar(255),
    dia_chi_ip    varchar(50),
    hang_san_xuat nvarchar(100),
    model         nvarchar(100),
    vai_tro       nvarchar(50),
    ghi_chu       nvarchar(max)
);

INSERT INTO @src (ten_switch, dia_chi_ip, hang_san_xuat, model, vai_tro, ghi_chu) VALUES
(N'CONTROLLER',                         '10.0.198.199', NULL,         NULL,              NULL,          N'Controller'),
(N'PFVN_SW_CORE',                       '10.0.193.1',   N'Huawei',    NULL,              N'Core',       N'Switch core'),
(N'X1_SW1_CuoiX1_ServerU_ServerRoom',   '10.0.198.200', N'Huawei',    N'S5735-L24T4X-A1', N'Truy cập',  N'VRP V200R021C00SPC100'),
(N'X1_SW1_CuoiX1_ServerU_ServerRoom',   '10.0.198.201', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X1_SW2_DauX1_20U_V14',               '10.0.198.202', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X1_SW3_DauX1_20U_V14',               '10.0.198.203', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X2_SW1_GiuaX2_20U',                  '10.0.198.204', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X3_SW1_CuoiX1_ServerU_ServerRoom',   '10.0.198.205', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X1_SW1_GiuaX1_KTC',                  '10.0.198.206', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'FW-Sonic',                           '10.0.198.241', N'SonicWall', NULL,              NULL,          N'Firewall'),
(N'X2-SW1-CuoiX2-6U-TTPP',              '10.0.198.240', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X3_SW1_DauX3_6U_TrenLoc',            '10.0.198.15',  N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X2_SW1_DauX2_20U_PMC',               '10.0.198.208', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X3_SW1_CuoiX3_20U_FN',               '10.0.198.216', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X4_SW1_DauX4_20U',                   '10.0.198.209', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X4-SW1-GiuaX4-KTC',                  '10.0.198.210', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X4_SW1_CuoiX4_6U_NhaAn',             '10.0.198.213', N'Huawei',    NULL,              N'Truy cập',   NULL),
(N'X3_SW1_GiuaX3_CUT',                  '10.0.198.207', N'Huawei',    NULL,              N'Truy cập',   N'Tên trên bảng kê: X3_SW1_GiuaX3_CUT>'),
(N'X2_SW1_DauX2_6U',                    '10.0.198.211', N'Huawei',    NULL,              N'Truy cập',   NULL);

/* ---------- 3. NẠP (bỏ qua IP đã có switch còn hiệu lực) ---------- */
INSERT INTO dbo.KK_Switch
    (ten_switch, dia_chi_ip, hang_san_xuat, model, vai_tro, IDCongTy, tinh_trang, ghi_chu, nguoi_tao, ngay_tao)
SELECT s.ten_switch, s.dia_chi_ip, s.hang_san_xuat, s.model, s.vai_tro, @IDCongTy,
       N'Đang hoạt động', s.ghi_chu, N'Khởi tạo danh sách PFVN', GETDATE()
FROM @src s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.KK_Switch k
    WHERE k.IDCongTy = @IDCongTy AND k.dia_chi_ip = s.dia_chi_ip AND k.ngay_xoa IS NULL
);

PRINT N'Đã nạp ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' switch PFVN mới.';
GO

/* ---------- 4. ĐỐI SOÁT SAU ---------- */
DECLARE @IDCongTy int = (SELECT IDCongTy FROM dbo.KK_CongTy WHERE TenCongTy = N'PFVN');
SELECT ten_switch, dia_chi_ip, hang_san_xuat, vai_tro, tinh_trang
FROM dbo.KK_Switch
WHERE IDCongTy = @IDCongTy AND ngay_xoa IS NULL
ORDER BY dia_chi_ip;
-- Kỳ vọng: đủ 19 thiết bị PFVN ở trên.
GO
