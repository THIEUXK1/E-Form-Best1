/* ============================================================================
   Mục đích : Thêm quyền "BaoCaoDinhKy" — chỉ xem trang /BaoCaoDinhKy (6 tab, cả 3 công ty)
              mà không phải trao AdminIT; gán quyền này cho tài khoản V160002.
   Phạm vi   : INSERT tối đa 1 dòng dbo.Quyen + tối đa 1 dòng dbo.User_Quyen.
   An toàn   : Idempotent (chỉ thêm khi chưa có). Không thấy TK V160002 → không gán gì.
   Rollback  : quyen-bao-cao-dinh-ky-20261012-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   Lưu ý     : role nạp vào cookie lúc đăng nhập → V160002 phải đăng xuất/đăng nhập lại.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

INSERT INTO dbo.Quyen (ten_quyen, mo_ta)
SELECT N'BaoCaoDinhKy', N'Xem Báo cáo định kì (chỉ xem)'
WHERE NOT EXISTS (SELECT 1 FROM dbo.Quyen WHERE ten_quyen = N'BaoCaoDinhKy');
PRINT CONCAT('Quyen đã thêm: ', @@ROWCOUNT);

INSERT INTO dbo.User_Quyen (id_nguoi_dung, id_quyen)
SELECT u.id_nguoi_dung, q.id_quyen
FROM dbo.[User] u
CROSS JOIN dbo.Quyen q
WHERE u.TK = N'V160002'
  AND q.ten_quyen = N'BaoCaoDinhKy'
  AND NOT EXISTS (SELECT 1 FROM dbo.User_Quyen uq
                  WHERE uq.id_nguoi_dung = u.id_nguoi_dung AND uq.id_quyen = q.id_quyen);
PRINT CONCAT('User_Quyen đã gán: ', @@ROWCOUNT);

COMMIT;
GO

SELECT u.id_nguoi_dung, u.TK, u.ho_ten, q.id_quyen, q.ten_quyen
FROM dbo.[User] u
JOIN dbo.User_Quyen uq ON uq.id_nguoi_dung = u.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE u.TK = N'V160002';
GO
