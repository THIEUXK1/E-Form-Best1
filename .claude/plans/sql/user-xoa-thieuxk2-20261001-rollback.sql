-- Rollback: khôi phục tài khoản NV2 (thieuxk2, id 6) + 3 phiếu từ các bảng bk_thieuxk2_* — 01/10/2026
-- Chạy: sqlcmd ... -f 65001 -i user-xoa-thieuxk2-20261001-rollback.sql
-- Thứ tự: cha trước, con sau. Giữ nguyên id cũ bằng IDENTITY_INSERT. Xong và kiểm ổn mới DROP các bảng bk_thieuxk2_*.

SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID('dbo.bk_thieuxk2_User') IS NULL
BEGIN
    RAISERROR(N'Không thấy bảng bk_thieuxk2_* — không có gì để khôi phục.', 16, 1);
    RETURN;
END
IF EXISTS (SELECT 1 FROM [User] WHERE id_nguoi_dung = 6)
BEGIN
    RAISERROR(N'User id 6 đang tồn tại — đã khôi phục rồi, dừng.', 16, 1);
    RETURN;
END

DECLARE @ds TABLE (thu_tu INT, bang SYSNAME);
INSERT INTO @ds VALUES
 (1,'User'), (2,'User_Quyen'), (3,'User_BoPhan'), (4,'UserDevices'), (5,'LichSuTruyCap'),
 (10,'FormIT'), (11,'IT_DangKiSuDungDTBan_4'), (12,'IT_CT_NguoiHoTro'), (13,'LichSuFormIT'),
 (20,'FormHR'), (21,'HR_XinRaNgoai_1'), (22,'HR_CT_NguoiHoTro'), (23,'HR_QuanLyDuyetB2'), (24,'HR_QuanLyDuyetB2_UyQuyen'), (25,'LichSuFormHR'),
 (30,'FormSHD'), (31,'SHD_DangKySuDungXeCongTac_1'), (32,'SHD_CT_NguoiHoTro'), (33,'SHD_QuanLyDuyetB2'), (34,'SHD_QuanLyDuyetB2_UyQuyen'), (35,'LichSuFormSHD');

BEGIN TRAN;

DECLARE @bang SYSNAME, @cot NVARCHAR(MAX), @sql NVARCHAR(MAX), @coId BIT;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT bang FROM @ds ORDER BY thu_tu;
OPEN c;
FETCH NEXT FROM c INTO @bang;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- Liệt kê cột thường (bỏ computed/rowversion) để INSERT tường minh
    SET @cot = NULL;
    SELECT @cot = COALESCE(@cot + N', ', N'') + QUOTENAME(name)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@bang)) AND is_computed = 0 AND system_type_id <> 189
    ORDER BY column_id;

    SET @coId = CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@bang))) THEN 1 ELSE 0 END;

    SET @sql = CASE WHEN @coId = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@bang) + N' ON; ' ELSE N'' END
             + N'INSERT INTO dbo.' + QUOTENAME(@bang) + N' (' + @cot + N') SELECT ' + @cot + N' FROM dbo.' + QUOTENAME(N'bk_thieuxk2_' + @bang) + N'; '
             + CASE WHEN @coId = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@bang) + N' OFF;' ELSE N'' END;
    EXEC sp_executesql @sql;

    FETCH NEXT FROM c INTO @bang;
END
CLOSE c; DEALLOCATE c;

COMMIT;

SELECT (SELECT COUNT(*) FROM [User] WHERE id_nguoi_dung = 6)          AS [user],
       (SELECT COUNT(*) FROM LichSuTruyCap WHERE id_nguoi_dung = 6)   AS lich_su_truy_cap,
       (SELECT COUNT(*) FROM User_BoPhan WHERE id_nguoi_dung = 6)     AS bo_phan,
       (SELECT COUNT(*) FROM FormIT  WHERE id = 720)                  AS form_it,
       (SELECT COUNT(*) FROM FormHR  WHERE id = 145)                  AS form_hr,
       (SELECT COUNT(*) FROM FormSHD WHERE id = 37)                   AS form_shd;
-- Kỳ vọng: 1 | 37 | 51 | 1 | 1 | 1
