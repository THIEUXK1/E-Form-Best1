-- Băm mật khẩu đăng nhập: dbo.[User] thêm cột mat_khau_hash (PasswordHasher của ASP.NET Core, PBKDF2).
-- Dùng chung cho E-Form (đăng nhập, đổi mật khẩu) và Admin-E-Form-Best1 (/QLtaiKhoan: tạo tài khoản, reset mật khẩu).
-- Chuyển dần, không cần nạp dữ liệu: ai đăng nhập đúng bằng mật khẩu thô (mat_khau) thì app băm vào mat_khau_hash
-- rồi đặt mat_khau = NULL. Cột mat_khau giữ lại đến khi hết dòng còn mật khẩu thô (truy vấn kiểm ở cuối file).
-- Chạy: sqlcmd -S 10.0.60.33 -d ITForm -U <user> -P *** -f 65001 -b -i user-mat-khau-hash-20261029.sql
-- Chạy lại lần hai an toàn (IF COL_LENGTH). Gỡ: user-mat-khau-hash-20261029-rollback.sql
-- PHẢI chạy trước khi deploy code của CẢ HAI app: entity User có thêm cột, mọi trang đọc User sẽ lỗi nếu thiếu cột.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRAN;

-- Chuỗi băm V3 dài ~84 ký tự; để 255 cho các phiên bản định dạng sau.
IF COL_LENGTH('dbo.[User]', 'mat_khau_hash') IS NULL
    ALTER TABLE dbo.[User] ADD mat_khau_hash NVARCHAR(255) NULL;

COMMIT;
GO

-- Kiểm sau khi chạy / theo dõi tiến độ chuyển đổi
SELECT COL_LENGTH('dbo.[User]', 'mat_khau_hash') AS do_dai_cot;
SELECT COUNT(*) AS tong,
       SUM(CASE WHEN mat_khau_hash IS NOT NULL THEN 1 ELSE 0 END) AS da_bam,
       SUM(CASE WHEN mat_khau IS NOT NULL AND mat_khau <> '' THEN 1 ELSE 0 END) AS con_mat_khau_tho
FROM dbo.[User];
