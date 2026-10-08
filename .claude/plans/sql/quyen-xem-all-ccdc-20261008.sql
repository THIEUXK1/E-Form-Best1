/* ============================================================================
   Mục đích : Thêm quyền XemAllCCDC cho /QLCCDC — xem CCDC của mọi người (chỉ xem,
              chỉ sửa được CCDC do mình tạo). AdminIT giờ chỉ thấy CCDC của mình;
              "All" vẫn xem/sửa tất cả.
   Phạm vi   : INSERT tối đa 1 dòng vào dbo.Quyen. Không đụng User_Quyen — gán quyền cho
               người dùng qua màn hình phân quyền như các quyền khác.
   An toàn   : Idempotent (chỉ thêm khi chưa có tên quyền).
   Rollback  : quyen-xem-all-ccdc-20261008-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   Lưu ý     : role nạp vào cookie lúc đăng nhập → người được gán phải đăng xuất/đăng nhập lại.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

INSERT INTO dbo.Quyen (ten_quyen, mo_ta)
SELECT N'XemAllCCDC', N'Xem CCDC của tất cả mọi người (chỉ sửa CCDC do mình tạo)'
WHERE NOT EXISTS (SELECT 1 FROM dbo.Quyen q WHERE q.ten_quyen = N'XemAllCCDC');

PRINT CONCAT('Đã thêm: ', @@ROWCOUNT, ' quyền');

COMMIT;
GO

SELECT id_quyen, ten_quyen, mo_ta FROM dbo.Quyen WHERE ten_quyen = N'XemAllCCDC';
GO
