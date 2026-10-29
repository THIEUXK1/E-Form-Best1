-- Băm hàng loạt mật khẩu thô còn lại của dbo.[User] (tài khoản chưa đăng nhập lại kể từ khi có mat_khau_hash,
-- người đã nghỉ, người chỉ đăng nhập domain). Sau script này mat_khau = NULL hết -> có thể bỏ cột ở bước sau.
-- Mật khẩu KHÔNG đổi: ai đang dùng mật khẩu gì vẫn đăng nhập bằng mật khẩu đó.
-- Bản băm do tool tạm (PasswordHasher mặc định, cùng định dạng E-Form/Admin) sinh ra file BAM_FILE gồm các
-- INSERT INTO #bam (id_nguoi_dung, hash, kiem); kiem = HASHBYTES('SHA2_256', mat_khau) lúc xuất ->
-- chỉ ghi dòng có mật khẩu vẫn như lúc xuất (ai vừa đổi mật khẩu / vừa được băm thì bỏ qua). File đó không có mật khẩu thô.
-- Chạy: sqlcmd -S 10.0.60.33 -d ITForm -U <user> -P *** -f 65001 -b -v BAM_FILE="<đường dẫn file bam>" -i user-mat-khau-bam-hang-loat-20261029.sql
-- Backup mật khẩu thô ở dbo.User_mat_khau_bak_20261029 để quay lại (user-mat-khau-bam-hang-loat-20261029-rollback.sql).
-- Bảng backup CHỨA MẬT KHẨU THÔ: kiểm đăng nhập ổn vài ngày thì xoá bằng user-mat-khau-bak-xoa-20261029.sql.
-- Chạy lại lần hai an toàn: dòng đã băm bị lọc bởi mat_khau_hash IS NULL, backup không ghi đè dòng đã có.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

CREATE TABLE #bam (
    id_nguoi_dung INT           NOT NULL PRIMARY KEY,
    hash          NVARCHAR(255) NOT NULL,
    kiem          VARBINARY(32) NOT NULL
);

:r $(BAM_FILE)

BEGIN TRAN;

IF OBJECT_ID('dbo.User_mat_khau_bak_20261029', 'U') IS NULL
    CREATE TABLE dbo.User_mat_khau_bak_20261029 (
        id_nguoi_dung INT           NOT NULL PRIMARY KEY,
        mat_khau      NVARCHAR(255) NOT NULL,
        hash_bam      NVARCHAR(255) NOT NULL,   -- bản băm script này ghi; rollback chỉ trả lại nếu chưa đổi
        ngay          DATETIME      NOT NULL DEFAULT (GETDATE())
    );

INSERT INTO dbo.User_mat_khau_bak_20261029 (id_nguoi_dung, mat_khau, hash_bam)
SELECT u.id_nguoi_dung, u.mat_khau, b.hash
FROM dbo.[User] u
JOIN #bam b ON b.id_nguoi_dung = u.id_nguoi_dung
WHERE u.mat_khau_hash IS NULL
  AND HASHBYTES('SHA2_256', u.mat_khau) = b.kiem
  AND NOT EXISTS (SELECT 1 FROM dbo.User_mat_khau_bak_20261029 k WHERE k.id_nguoi_dung = u.id_nguoi_dung);

UPDATE u
SET mat_khau_hash = b.hash,
    mat_khau      = NULL
FROM dbo.[User] u
JOIN #bam b ON b.id_nguoi_dung = u.id_nguoi_dung
WHERE u.mat_khau_hash IS NULL
  AND HASHBYTES('SHA2_256', u.mat_khau) = b.kiem;

SELECT @@ROWCOUNT AS so_dong_vua_bam;

COMMIT;
GO

SELECT COUNT(*) AS tong,
       SUM(CASE WHEN mat_khau_hash IS NOT NULL THEN 1 ELSE 0 END) AS da_bam,
       SUM(CASE WHEN mat_khau IS NOT NULL AND mat_khau <> '' THEN 1 ELSE 0 END) AS con_mat_khau_tho
FROM dbo.[User];
