/* ============================================================================
   Mục đích : Thêm 3 quyền xem AP theo công ty cho /QLAP:
              ApBPVN · ApPFVN · ApMEGA (chỉ xem AP công ty đó).
              Quyền "All" xem/sửa cả 3 công ty; AdminIT chỉ xem được trang Tổng quan.
   Phạm vi   : INSERT tối đa 3 dòng vào dbo.Quyen. Không đụng User_Quyen — gán quyền cho
               người dùng qua màn hình phân quyền như các quyền khác.
   An toàn   : Idempotent (chỉ thêm khi chưa có tên quyền).
   Rollback  : quyen-ap-cong-ty-20261008-rollback.sql
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
    (N'ApBPVN', N'Xem AP Wi-Fi BPVN (chỉ xem)'),
    (N'ApPFVN', N'Xem AP Wi-Fi PFVN (chỉ xem)'),
    (N'ApMEGA', N'Xem AP Wi-Fi MEGA (chỉ xem)')
) v(ten_quyen, mo_ta)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Quyen q WHERE q.ten_quyen = v.ten_quyen);

PRINT CONCAT('Đã thêm: ', @@ROWCOUNT, ' quyền');

COMMIT;
GO

SELECT id_quyen, ten_quyen, mo_ta FROM dbo.Quyen WHERE ten_quyen LIKE N'Ap%';
GO
