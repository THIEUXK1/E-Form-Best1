/* ============================================================================
   Mục đích : Danh mục đầu ghi camera (NVR/DVR) ở chi nhánh / site ngoài cho trang /QLCamera.
              Đầu ghi trong mạng BPVN (10.0.2x.x) đã được hệ thống ISAPI 10.0.60.238:3005
              theo dõi nên KHÔNG nhập vào đây.
              Nguồn: CCTV.xlsx (Sheet1) ngày 06/10/2026.

   BẢO MẬT  : KHÔNG nhập mật khẩu đầu ghi, tài khoản Hải quan / HR / modem có trong file
              Excel. Cột mật khẩu không tồn tại trong bảng này.

   Phạm vi   : - TẠO MỚI 1 bảng : dbo.KK_DauGhi (không đụng bảng cũ)
               - NẠP 15 dòng từ Excel (chỉ nạp dòng chưa có, khớp theo dia_diem + ip_local + ip_public)
   An toàn   : Idempotent. Rollback: kk-dau-ghi-20261006-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT COUNT(*) AS bang_dau_ghi_da_ton_tai
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi';
-- Kỳ vọng lần chạy đầu: 0
GO


/* ---------- 2. TẠO BẢNG ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi')
BEGIN
    CREATE TABLE dbo.KK_DauGhi
    (
        id_dau_ghi     int            IDENTITY(1,1) NOT NULL,
        dia_diem       nvarchar(255)  NOT NULL,          -- Gia Lộc, Sembcorp HD, KTX-T5.1...
        ip_public      nvarchar(255)  NULL,              -- IP tĩnh hoặc tên miền DDNS
        ip_local       varchar(50)    NULL,
        port_sv        int            NULL,              -- Cổng dịch vụ (iVMS/SDK), thường 8000
        port_web       int            NULL,
        tong_camera    int            NULL,
        raid           nvarchar(50)   NULL,
        trang_thai     nvarchar(100)  NULL,              -- Đang dùng / Đã tháo / Ngừng dùng
        ghi_chu        nvarchar(max)  NULL,
        nguoi_tao      nvarchar(255)  NULL,
        ngay_tao       datetime       NULL CONSTRAINT DF_KK_DauGhi_NgayTao DEFAULT (GETDATE()),
        ngay_cap_nhat  datetime       NULL,
        ngay_xoa       datetime       NULL,              -- Soft delete: NULL = còn hiệu lực
        ly_do_xoa      nvarchar(500)  NULL,
        CONSTRAINT PK_KK_DauGhi PRIMARY KEY CLUSTERED (id_dau_ghi)
    );
END
GO


/* ---------- 3. NẠP DỮ LIỆU TỪ EXCEL (chỉ thêm dòng còn thiếu) ---------- */
INSERT INTO dbo.KK_DauGhi (dia_diem, ip_public, ip_local, port_sv, port_web, tong_camera, raid, trang_thai, ghi_chu, nguoi_tao, ngay_tao)
SELECT ds.dia_diem, ds.ip_public, ds.ip_local, ds.port_sv, ds.port_web, ds.tong_camera, ds.raid, ds.trang_thai, ds.ghi_chu,
       N'Nhập từ CCTV.xlsx', GETDATE()
FROM (VALUES
    (N'Gia Lộc',            N'14.241.51.116',   '10.10.197.254',  8000, 80,   NULL, NULL,     N'Đang dùng', NULL),
    (N'Minh Thư',           N'14.241.106.124',  '192.168.88.250', 8000, 80,   NULL, NULL,     N'Đã tháo',   N'Đã tháo mang về'),
    (N'Sembcorp HD',        N'113.160.129.0',   '192.168.1.250',  8000, 80,   NULL, NULL,     N'Đang dùng', N'Web HTTPS: https://113.160.129.0:8443/'),
    (N'Sembcorp NA',        N'103.153.220.152', '192.168.1.250',  8000, 8001, NULL, NULL,     N'Đang dùng', NULL),
    (N'Yên Mỹ - Hưng Yên',  N'117.4.123.218',   '172.16.1.250',   8000, 8001, NULL, NULL,     N'Đang dùng', N'IP public cũ 116.97.183.134 đã bỏ'),
    (N'Nghệ An HQ Xưởng 4', N'14.241.55.126',   '10.0.200.252',   8000, 8001, 23,   NULL,     N'Đang dùng', N'Web HTTPS: https://14.241.55.126:8005'),
    (N'Nghệ An',            N'14.241.71.125',   '10.0.200.254',   8000, NULL, 32,   NULL,     N'Đang dùng', NULL),
    (N'Nghệ An',            N'14.241.55.238',   '10.0.200.250',   7979, NULL, 32,   NULL,     N'Đang dùng', NULL),
    (N'Nghệ An',            N'14.254.71.125',   '10.0.200.251',   7878, NULL, 21,   NULL,     N'Đang dùng', NULL),
    (N'Nghệ An new',        NULL,               '10.0.200.249',   NULL, NULL, NULL, NULL,     N'Đang dùng', NULL),
    (N'Daiwa MQ-HY',        N'14.241.80.50',    '172.16.1.250',   8000, NULL, NULL, N'RAID 5', N'Đang dùng', NULL),
    (N'Diễn Châu',          N'222.252.204.173', NULL,             NULL, 8003, NULL, NULL,     N'Đang dùng', N'Web HTTPS: https://222.252.204.173:8003'),
    (N'KTX-T5.1',           N'ktx-best5-1.cameraddns.net', NULL,  NULL, NULL, NULL, NULL,     N'Đang dùng', N'Web: http://cameraktxt51.ddns.net:8080'),
    (N'KTX-T5.2',           N'ktx-best52.cameraddns.net',  NULL,  NULL, NULL, NULL, NULL,     N'Đang dùng', NULL),
    (N'Phòng CCTV (máy VN-HR005)', NULL,        '10.0.28.249',    NULL, NULL, NULL, NULL,     N'Đang dùng', N'Máy xem camera tại phòng CCTV')
) AS ds (dia_diem, ip_public, ip_local, port_sv, port_web, tong_camera, raid, trang_thai, ghi_chu)
WHERE NOT EXISTS (SELECT 1 FROM dbo.KK_DauGhi d
                  WHERE d.dia_diem = ds.dia_diem
                    AND ISNULL(d.ip_local, '')  = ISNULL(ds.ip_local, '')
                    AND ISNULL(d.ip_public, N'') = ISNULL(ds.ip_public, N''));
GO


/* ---------- 4. ĐỐI SOÁT SAU ---------- */
SELECT id_dau_ghi, dia_diem, ip_public, ip_local, port_sv, port_web, tong_camera, trang_thai
FROM dbo.KK_DauGhi
ORDER BY id_dau_ghi;
-- Kỳ vọng: 15 dòng, chữ có dấu hiển thị đúng. Chạy lại lần hai vẫn 15.

SELECT COUNT(*) AS loi_font FROM dbo.KK_DauGhi WHERE dia_diem LIKE N'%' + NCHAR(195) + N'%';
-- Kỳ vọng: 0
GO
