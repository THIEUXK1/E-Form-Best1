-- Bo sung danh muc MayIn theo hien trang print server \\vn-printersrv (doc 18/09/2026).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Cham DUY NHAT bang MayIn.
-- Chay SAU mayin-ten-hangdoi-20260918.sql (can cot ten_hang_doi).
-- Rollback: mayin-bosung-tu-printsrv-20260918-rollback.sql
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-bosung-tu-printsrv-20260918.sql
--
-- 3 may dang chay tren print server nhung file Excel khong co:
--   1) 10.0.59.153 serial 100121 -> may MOI, hang doi "VN-Printer TL OFFICE NEW 153"
--   2) 10.0.59.158 serial 100239 -> may MOI, hang doi "VN-Pitel-59.158"
--   3) 10.0.59.215 serial 100108 -> DA CO trong danh muc (dong WV, Excel ghi ket noi "USB"),
--      thuc te da len mang -> chi cap nhat dia_chi_ip + bat doc tu dong, KHONG them dong moi.
-- Ca 3 deu tra loi /home/api/billing-counter (dong ApeosPrint 3360 S) nen bat theo_doi_tu_dong = 1.
-- Bo phan cua 10.0.59.158 chua ro tu ten hang doi -> de NULL, sua tay sau tren giao dien.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @moi TABLE
(
    bo_phan NVARCHAR(50), model NVARCHAR(50), serial NVARCHAR(50), dia_chi_ip VARCHAR(45),
    vi_tri NVARCHAR(200), ten_hang_doi NVARCHAR(200), ghi_chu NVARCHAR(500)
);

INSERT INTO @moi (bo_phan, model, serial, dia_chi_ip, vi_tri, ten_hang_doi, ghi_chu) VALUES
(N'TL', N'FX3360s', N'100121', '10.0.59.153', N'TL OFFICE', N'VN-Printer TL OFFICE NEW 153',
 N'Bo sung tu print server 18/09/2026 - khong co trong file Excel 2025'),
(NULL,  N'FX3360s', N'100239', '10.0.59.158', N'Pitel',     N'VN-Pitel-59.158',
 N'Bo sung tu print server 18/09/2026 - chua ro bo phan, can xac minh tai hien truong');

INSERT INTO dbo.MayIn (bo_phan, model, serial, dia_chi_ip, vi_tri, ten_hang_doi, trang_thai, theo_doi_tu_dong, ghi_chu)
SELECT n.bo_phan, n.model, n.serial, n.dia_chi_ip, n.vi_tri, n.ten_hang_doi, N'HoatDong', 1, n.ghi_chu
FROM   @moi n
WHERE  NOT EXISTS (SELECT 1 FROM dbo.MayIn m WHERE m.model = n.model AND m.serial = n.serial);

PRINT N'Da them may moi: ' + CAST(@@ROWCOUNT AS varchar);

-- May 100108: Excel ghi ket noi USB, thuc te dang o 10.0.59.215 va doc duoc chi so qua mang.
-- Chi ghi khi thuc su khac de chay lai lan hai khong dong vao du lieu.
UPDATE dbo.MayIn
SET    dia_chi_ip       = '10.0.59.215',
       ten_hang_doi     = N'VN-WV-Printer-OF-215 TAM',
       theo_doi_tu_dong = 1,
       ghi_chu          = N'Excel ghi ket noi USB; thuc te da len mang tai 10.0.59.215 (doi soat print server 18/09/2026)',
       ngay_cap_nhat    = GETDATE()
WHERE  model = N'FX3360s' AND serial = N'100108'
  AND  ISNULL(dia_chi_ip, '') <> '10.0.59.215';

PRINT N'Da cap nhat may 100108: ' + CAST(@@ROWCOUNT AS varchar);
GO

-- Doi soat sau khi chay (ky vong: 113 may, 3 dong duoi deu co IP + theo_doi_tu_dong = 1)
SELECT COUNT(*) AS tong_may_in FROM dbo.MayIn;

SELECT bo_phan, model, serial, dia_chi_ip, ten_hang_doi, theo_doi_tu_dong, vi_tri
FROM   dbo.MayIn
WHERE  serial IN (N'100121', N'100239', N'100108')
ORDER  BY serial;
