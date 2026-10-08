/* ============================================================================
   Mục đích : Thêm 3 quyền xem camera theo công ty cho /QLCamera:
              CamBPVN · CamPFVN · CamMEGA (chỉ xem camera công ty đó).
              Quyền "All" vẫn xem/sửa cả 3 công ty; AdminIT không còn tự có quyền camera.
   Phạm vi   : INSERT tối đa 3 dòng vào dbo.Quyen. Không đụng User_Quyen — gán quyền cho
               người dùng qua màn hình phân quyền như các quyền khác.
   An toàn   : Idempotent (chỉ thêm khi chưa có tên quyền).
   Rollback  : quyen-camera-cong-ty-20261007-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   Lưu ý     : role nạp vào cookie lúc đăng nhập → người được gán phải đăng xuất/đăng nhập lại.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

INSERT INTO dbo.Quyen (ten_quyen, mo_ta)
SELECT v.ten_quyen, v.mo_ta
FROM (VALUES
    (N'CamBPVN', N'Xem camera BPVN (chỉ xem)'),
    (N'CamPFVN', N'Xem camera PFVN (chỉ xem)'),
    (N'CamMEGA', N'Xem camera MEGA (chỉ xem)')
) v(ten_quyen, mo_ta)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Quyen q WHERE q.ten_quyen = v.ten_quyen);

PRINT CONCAT('Đã thêm: ', @@ROWCOUNT, ' quyền');

COMMIT;
GO

SELECT id_quyen, ten_quyen, mo_ta FROM dbo.Quyen WHERE ten_quyen LIKE N'Cam%';
GO
