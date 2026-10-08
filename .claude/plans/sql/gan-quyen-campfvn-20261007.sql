/* ============================================================================
   Mục đích : Gán quyền CamPFVN (chỉ xem camera PFVN) cho Nguyễn Hồ Thắng (Y260013)
              và Nguyễn Quang Huy (Y240789).
   Phạm vi   : INSERT tối đa 2 dòng vào dbo.User_Quyen. Không đụng quyền khác.
   An toàn   : Idempotent (bỏ qua người đã có CamPFVN).
   Rollback  : gan-quyen-campfvn-20261007-rollback.sql
   Lưu ý     : người được gán phải đăng xuất/đăng nhập lại.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;

INSERT INTO dbo.User_Quyen (id_nguoi_dung, id_quyen)
SELECT u.id_nguoi_dung, q.id_quyen
FROM dbo.[User] u
CROSS JOIN dbo.Quyen q
WHERE u.ma_nhan_vien IN (N'Y260013', N'Y240789')
  AND q.ten_quyen = N'CamPFVN'
  AND NOT EXISTS (SELECT 1 FROM dbo.User_Quyen x WHERE x.id_nguoi_dung = u.id_nguoi_dung AND x.id_quyen = q.id_quyen);

PRINT CONCAT('Da gan: ', @@ROWCOUNT);
GO

SELECT u.ma_nhan_vien, q.ten_quyen
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE u.ma_nhan_vien IN (N'Y260013', N'Y240789') AND q.ten_quyen LIKE N'Cam%';
GO
