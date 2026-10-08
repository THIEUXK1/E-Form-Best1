/* ============================================================================
   Rollback cho kk-dau-ghi-cong-ty-20261007.sql — bỏ cột IDCongTy khỏi KK_DauGhi.
   CẢNH BÁO: mất thông tin đầu ghi nào thuộc công ty nào (đầu ghi PFVN/MEGA sẽ lẫn vào BPVN).
   Trước khi chạy: gỡ code dùng KkDauGhi.IdcongTy, không thì tab Đầu ghi lỗi.
   Bảng sao lưu dbo.KK_DauGhi_bak_20261007 (tạo ở script chính) giữ nguyên, xoá tay khi chắc không cần.
   ========================================================================== */

USE ITForm;
GO

SELECT SUM(CASE WHEN IDCongTy IS NOT NULL THEN 1 ELSE 0 END) AS so_dau_ghi_mat_cong_ty FROM dbo.KK_DauGhi;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KK_DauGhi_KK_CongTy')
    ALTER TABLE dbo.KK_DauGhi DROP CONSTRAINT FK_KK_DauGhi_KK_CongTy;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi' AND COLUMN_NAME = 'IDCongTy')
    ALTER TABLE dbo.KK_DauGhi DROP COLUMN IDCongTy;
GO

SELECT COUNT(*) AS cot_con_lai_phai_0 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi' AND COLUMN_NAME = 'IDCongTy';
GO
