/* Rollback quyen-ap-cong-ty-20261008.sql: bỏ 3 quyền xem AP theo công ty.
   Xoá cả dòng gán User_Quyen của 3 quyền này (bảng gán quyền, không phải bảng LichSu*).
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

SET NOCOUNT ON;

SELECT q.ten_quyen, COUNT(uq.id_quyen) AS so_nguoi_dang_gan
FROM dbo.Quyen q
LEFT JOIN dbo.User_Quyen uq ON uq.id_quyen = q.id_quyen
WHERE q.ten_quyen IN (N'ApBPVN', N'ApPFVN', N'ApMEGA')
GROUP BY q.ten_quyen;
GO

BEGIN TRAN;

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen IN (N'ApBPVN', N'ApPFVN', N'ApMEGA');

DELETE FROM dbo.Quyen WHERE ten_quyen IN (N'ApBPVN', N'ApPFVN', N'ApMEGA');

COMMIT;
GO
