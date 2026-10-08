-- Rollback it-order-vi-tri-may-20261007.sql — XOÁ 2 cột (mất dữ liệu đã nhập vào 2 cột này).
-- Phải gỡ code dùng ViTriMay/TenMayTinh khỏi ItOrderIt2.cs TRƯỚC khi chạy, không thì EF lỗi "Invalid column name".
SET XACT_ABORT ON;
BEGIN TRAN;

IF COL_LENGTH('dbo.IT_OrderIT_2', 'ViTriMay') IS NOT NULL
    ALTER TABLE dbo.IT_OrderIT_2 DROP COLUMN ViTriMay;

IF COL_LENGTH('dbo.IT_OrderIT_2', 'TenMayTinh') IS NOT NULL
    ALTER TABLE dbo.IT_OrderIT_2 DROP COLUMN TenMayTinh;

COMMIT;
