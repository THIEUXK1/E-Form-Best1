-- Gỡ user-mat-khau-bam-hang-loat-20261029.sql: trả mật khẩu thô từ dbo.User_mat_khau_bak_20261029.
-- Chỉ trả cho dòng vẫn mang đúng bản băm hàng loạt (người đã tự đổi mật khẩu sau đó thì giữ mật khẩu mới).
-- Không cần thiết về chức năng: E-Form/Admin đăng nhập được cả bản băm lẫn bản thô. Chỉ dùng khi bản băm hỏng.
-- Chạy: sqlcmd -S 10.0.60.33 -d ITForm -U <user> -P *** -f 65001 -b -i user-mat-khau-bam-hang-loat-20261029-rollback.sql
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRAN;

UPDATE u
SET mat_khau      = k.mat_khau,
    mat_khau_hash = NULL
FROM dbo.[User] u
JOIN dbo.User_mat_khau_bak_20261029 k ON k.id_nguoi_dung = u.id_nguoi_dung
WHERE u.mat_khau_hash = k.hash_bam;

SELECT @@ROWCOUNT AS so_dong_tra_lai;

COMMIT;
