-- Gỡ user-mat-khau-hash-20261029.sql: cột dbo.[User].mat_khau_hash
-- CẢNH BÁO: tài khoản đã chuyển sang băm có mat_khau = NULL -> gỡ cột là KHÔNG ai trong số đó đăng nhập bằng
-- mật khẩu DB được nữa (đăng nhập domain không ảnh hưởng). Trước khi gỡ phải đặt lại mật khẩu thô cho họ, ví dụ
-- về mặc định, rồi báo người dùng:
--   UPDATE dbo.[User] SET mat_khau = N'<mật khẩu mặc định>' WHERE mat_khau_hash IS NOT NULL AND (mat_khau IS NULL OR mat_khau = '');
-- Backup trước: SELECT id_nguoi_dung, mat_khau_hash INTO dbo.User_mat_khau_hash_bak_<yyyymmdd> FROM dbo.[User] WHERE mat_khau_hash IS NOT NULL;
-- Gỡ code (entity User.MatKhauHash ở cả E-Form và Admin-E-Form-Best1) trước, rồi mới chạy script này.
SET XACT_ABORT ON;
BEGIN TRAN;

IF COL_LENGTH('dbo.[User]', 'mat_khau_hash') IS NOT NULL
    ALTER TABLE dbo.[User] DROP COLUMN mat_khau_hash;

COMMIT;

SELECT COL_LENGTH('dbo.[User]', 'mat_khau_hash') AS cot_con_lai;
