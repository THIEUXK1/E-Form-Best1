/* Rollback gan-quyen-cambpvn-20261007.sql: gỡ CamBPVN của đúng 6 người đã gán.
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen = N'CamBPVN'
  AND u.ma_nhan_vien IN (N'V210817', N'V240298', N'V261861', N'V200888', N'V200887', N'V240822');
GO
