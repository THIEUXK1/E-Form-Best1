/* ============================================================================
   Mục đích : Gán quyền CamMEGA (chỉ xem camera MEGA) cho 5 tài khoản IT bên MEGA.
   Phạm vi   : INSERT tối đa 5 dòng vào dbo.User_Quyen. Không đụng quyền khác.
   An toàn   : Idempotent (bỏ qua người đã có CamMEGA), khoá theo mã nhân viên.
   Rollback  : gan-quyen-cammega-20261007-rollback.sql
   Lưu ý     : người được gán phải đăng xuất/đăng nhập lại.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

DECLARE @idQuyen int = (SELECT id_quyen FROM dbo.Quyen WHERE ten_quyen = N'CamMEGA');
IF @idQuyen IS NULL
BEGIN
    ROLLBACK;
    RAISERROR(N'Chưa có quyền CamMEGA, chạy quyen-camera-cong-ty-20261007.sql trước', 16, 1);
    RETURN;
END

INSERT INTO dbo.User_Quyen (id_nguoi_dung, id_quyen)
SELECT u.id_nguoi_dung, @idQuyen
FROM dbo.[User] u
WHERE u.ma_nhan_vien IN (N'M250813', N'V250813', N'M260001', N'V260154', N'M250785')
  AND NOT EXISTS (SELECT 1 FROM dbo.User_Quyen x WHERE x.id_nguoi_dung = u.id_nguoi_dung AND x.id_quyen = @idQuyen);

PRINT CONCAT('Da gan: ', @@ROWCOUNT);

COMMIT;
GO

SELECT u.ma_nhan_vien, u.ho_ten
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen = N'CamMEGA'
ORDER BY u.ma_nhan_vien;
GO
