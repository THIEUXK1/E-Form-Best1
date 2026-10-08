/* ============================================================================
   Mục đích : Bỏ 6 CCDC dữ liệu test (nguoi_tao = N'Script test', id 7..12) khỏi /QLCCDC.
   Phạm vi   : UPDATE tối đa 6 dòng dbo.KK_CongCuDungCu (xoá mềm: ngay_xoa + ly_do_xoa).
               Không xoá cứng, không đụng KK_CCDC_MuonTra (4 phiếu test, 3 còn mở) —
               trang "Đang cho mượn" đã lọc CCDC bị xoá mềm nên các phiếu này tự ẩn.
   An toàn   : Idempotent (chỉ chạm dòng chưa xoá), khoá cả id lẫn nguoi_tao.
   Rollback  : xoa-ccdc-test-20261008-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

UPDATE dbo.KK_CongCuDungCu
SET ngay_xoa = GETDATE(),
    ly_do_xoa = N'Dữ liệu test'
WHERE id_ccdc BETWEEN 7 AND 12
  AND nguoi_tao = N'Script test'
  AND ngay_xoa IS NULL;

PRINT CONCAT('Đã xoá mềm: ', @@ROWCOUNT, ' CCDC');

COMMIT;
GO

SELECT id_ccdc, ten_ccdc, ngay_xoa, ly_do_xoa
FROM dbo.KK_CongCuDungCu
WHERE nguoi_tao = N'Script test';
GO
