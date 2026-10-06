/* ============================================================================
   Rollback cho kk-dau-ghi-20261006.sql — xoá bảng KK_DauGhi.
   CẢNH BÁO: mất các đầu ghi đã nhập/sửa trên web. Có dữ liệu thì backup trước:
       SELECT * INTO dbo.KK_DauGhi_bak_YYYYMMDD FROM dbo.KK_DauGhi;
   Trước khi chạy: gỡ code tab "Đầu ghi" của QLCamera, không thì tab đó lỗi.
   ========================================================================== */

USE ITForm;
GO

SELECT COUNT(*) AS so_dau_ghi_se_mat FROM dbo.KK_DauGhi;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi')
    DROP TABLE dbo.KK_DauGhi;
GO

SELECT OBJECT_ID('dbo.KK_DauGhi') AS phai_la_null;
GO
