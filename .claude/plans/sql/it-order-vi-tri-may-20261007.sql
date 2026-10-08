-- Thêm "Vị trí" và "Tên máy tính" cho đơn Yêu cầu hỗ trợ IT (/FormIT/TaoIT_Order)
-- Bảng: dbo.IT_OrderIT_2 — chỉ THÊM 2 cột NULL, không chạm dữ liệu cũ.
-- Chạy: sqlcmd -S 10.0.60.33 -d <DB> -E -f 65001 -i it-order-vi-tri-may-20261007.sql
-- Chạy lại lần hai an toàn (IF COL_LENGTH ... IS NULL).
SET XACT_ABORT ON;
BEGIN TRAN;

IF COL_LENGTH('dbo.IT_OrderIT_2', 'ViTriMay') IS NULL
    ALTER TABLE dbo.IT_OrderIT_2 ADD ViTriMay NVARCHAR(200) NULL;

IF COL_LENGTH('dbo.IT_OrderIT_2', 'TenMayTinh') IS NULL
    ALTER TABLE dbo.IT_OrderIT_2 ADD TenMayTinh NVARCHAR(100) NULL;

COMMIT;

-- Đối soát: phải ra 2 dòng
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'IT_OrderIT_2' AND COLUMN_NAME IN ('ViTriMay', 'TenMayTinh');
