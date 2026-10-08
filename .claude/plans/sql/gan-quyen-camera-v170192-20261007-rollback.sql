/* Rollback gan-quyen-camera-v170192-20261007.sql: gỡ 3 quyền Cam* của V170192.
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE u.ma_nhan_vien = N'V170192'
  AND q.ten_quyen IN (N'CamBPVN', N'CamPFVN', N'CamMEGA');
GO
