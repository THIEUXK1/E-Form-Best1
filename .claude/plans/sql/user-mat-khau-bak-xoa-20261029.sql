-- Xoá bảng backup mật khẩu thô dbo.User_mat_khau_bak_20261029 (tạo bởi user-mat-khau-bam-hang-loat-20261029.sql).
-- Chạy khi đã kiểm đăng nhập ổn vài ngày. Sau đó KHÔNG rollback được nữa (cũng không cần: mật khẩu không đổi).
-- Chạy: sqlcmd -S 10.0.60.33 -d ITForm -U <user> -P *** -f 65001 -b -i user-mat-khau-bak-xoa-20261029.sql
IF OBJECT_ID('dbo.User_mat_khau_bak_20261029', 'U') IS NOT NULL
    DROP TABLE dbo.User_mat_khau_bak_20261029;

SELECT OBJECT_ID('dbo.User_mat_khau_bak_20261029', 'U') AS con_lai;
