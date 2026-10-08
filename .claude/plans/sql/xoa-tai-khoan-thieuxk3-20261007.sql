/* ============================================================================
   Mục đích : Xoá hẳn tài khoản Thieuxk3 (id_nguoi_dung = 7, "NV3", tài khoản thử, quyền AdminIT + AdminHR).
   Phạm vi   : DELETE 2 dòng User_Quyen + 5 dòng UserDevices + 1 dòng User. Các bảng khác có khoá ngoại
              tới User (LichSuTruyCap, KK_ThietBi, KK_CCDC_MuonTra, User_BoPhan, ...) đã kiểm = 0 dòng.
   An toàn   : Sao lưu nguyên dòng vào 3 bảng *_bak_20261007 trước khi xoá; khoá theo id 7 + mã Thieuxk3;
              số dòng xoá khác dự kiến thì ROLLBACK cả lô. Chạy lại lần 2 không xoá gì thêm.
   Rollback  : xoa-tai-khoan-thieuxk3-20261007-rollback.sql (dựng lại từ bảng sao lưu)
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.[User] WHERE id_nguoi_dung = 7 AND ma_nhan_vien = N'Thieuxk3')
BEGIN
    PRINT N'Không còn tài khoản Thieuxk3 (id 7) — bỏ qua.';
    RETURN;
END

BEGIN TRAN;

IF OBJECT_ID('dbo.User_bak_Thieuxk3_20261007') IS NULL
    SELECT * INTO dbo.User_bak_Thieuxk3_20261007 FROM dbo.[User] WHERE id_nguoi_dung = 7;
IF OBJECT_ID('dbo.User_Quyen_bak_Thieuxk3_20261007') IS NULL
    SELECT * INTO dbo.User_Quyen_bak_Thieuxk3_20261007 FROM dbo.User_Quyen WHERE id_nguoi_dung = 7;
IF OBJECT_ID('dbo.UserDevices_bak_Thieuxk3_20261007') IS NULL
    SELECT * INTO dbo.UserDevices_bak_Thieuxk3_20261007 FROM dbo.UserDevices WHERE id_nguoi_dung = 7;

DECLARE @q int, @d int, @u int;
DELETE FROM dbo.User_Quyen  WHERE id_nguoi_dung = 7; SET @q = @@ROWCOUNT;
DELETE FROM dbo.UserDevices WHERE id_nguoi_dung = 7; SET @d = @@ROWCOUNT;
DELETE FROM dbo.[User]      WHERE id_nguoi_dung = 7 AND ma_nhan_vien = N'Thieuxk3'; SET @u = @@ROWCOUNT;

IF @q > 2 OR @d > 5 OR @u <> 1
BEGIN
    ROLLBACK;
    RAISERROR(N'Số dòng xoá khác dự kiến (User_Quyen=%d, UserDevices=%d, User=%d) — đã huỷ', 16, 1, @q, @d, @u);
    RETURN;
END

COMMIT;
PRINT CONCAT('Da xoa: User_Quyen=', @q, ', UserDevices=', @d, ', User=', @u);
GO

SELECT COUNT(*) AS con_lai FROM dbo.[User] WHERE ma_nhan_vien = N'Thieuxk3';
GO
