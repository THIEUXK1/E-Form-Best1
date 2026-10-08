/* Rollback xoa-tai-khoan-thieuxk3-20261007.sql: dựng lại tài khoản Thieuxk3 (id 7) từ 3 bảng sao lưu.
   Danh sách cột lấy từ bảng sao lưu (SELECT * INTO) nên không phải gõ tay. */

USE ITForm;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.[User] WHERE id_nguoi_dung = 7)
BEGIN
    PRINT N'Tài khoản id 7 đang tồn tại — không cần khôi phục.';
    RETURN;
END

DECLARE @cot nvarchar(max), @sql nvarchar(max);

BEGIN TRAN;

-- User (có cột identity id_nguoi_dung)
SELECT @cot = STUFF((SELECT ',' + QUOTENAME(name) FROM sys.columns
                     WHERE object_id = OBJECT_ID('dbo.User_bak_Thieuxk3_20261007') ORDER BY column_id FOR XML PATH('')), 1, 1, '');
SET @sql = N'SET IDENTITY_INSERT dbo.[User] ON; INSERT INTO dbo.[User] (' + @cot + N') SELECT ' + @cot
         + N' FROM dbo.User_bak_Thieuxk3_20261007; SET IDENTITY_INSERT dbo.[User] OFF;';
EXEC sp_executesql @sql;

INSERT INTO dbo.User_Quyen (id_nguoi_dung, id_quyen, ghi_chu)
SELECT id_nguoi_dung, id_quyen, ghi_chu FROM dbo.User_Quyen_bak_Thieuxk3_20261007;

SELECT @cot = STUFF((SELECT ',' + QUOTENAME(name) FROM sys.columns
                     WHERE object_id = OBJECT_ID('dbo.UserDevices_bak_Thieuxk3_20261007') ORDER BY column_id FOR XML PATH('')), 1, 1, '');
SET @sql = N'SET IDENTITY_INSERT dbo.UserDevices ON; INSERT INTO dbo.UserDevices (' + @cot + N') SELECT ' + @cot
         + N' FROM dbo.UserDevices_bak_Thieuxk3_20261007; SET IDENTITY_INSERT dbo.UserDevices OFF;';
EXEC sp_executesql @sql;

COMMIT;
PRINT N'Đã khôi phục Thieuxk3.';
GO
