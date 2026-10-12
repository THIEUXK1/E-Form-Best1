/* Rollback kk-quet-ip-20261012.sql — XOÁ 2 bảng quét dải IP và toàn bộ lịch sử trong đó.
   Chỉ chạy khi gỡ hẳn tính năng (deploy lại code chưa có QuetIpWorker TRƯỚC, không job nền sẽ báo lỗi).
   Lịch sử là append-only: xuất dữ liệu ra trước nếu cần giữ. */

USE ITForm;
GO

SELECT COUNT(*) AS so_ip FROM dbo.KK_QuetIp;
SELECT COUNT(*) AS so_su_kien FROM dbo.KK_QuetIpLichSu;
GO

IF OBJECT_ID('dbo.KK_QuetIpLichSu', 'U') IS NOT NULL DROP TABLE dbo.KK_QuetIpLichSu;
GO
IF OBJECT_ID('dbo.KK_QuetIp', 'U') IS NOT NULL DROP TABLE dbo.KK_QuetIp;
GO
