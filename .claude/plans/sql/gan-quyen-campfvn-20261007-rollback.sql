/* Rollback gan-quyen-campfvn-20261007.sql: gỡ CamPFVN của Y260013, Y240789.
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE u.ma_nhan_vien IN (N'Y260013', N'Y240789')
  AND q.ten_quyen = N'CamPFVN';
GO
