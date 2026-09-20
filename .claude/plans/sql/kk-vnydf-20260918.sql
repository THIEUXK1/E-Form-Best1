-- Phieu 'YDF.xlsx' (VN-YDF, luu 18/09/2026 15:47).
-- 10 dong VN-YDF trong phieu deu chon Hai Duong. 6 dong da khop DB, 4 dong con lech:
--   1919 YDF002, 1923 YDF001, 2114 YDF003 : chua co vi tri -> Hai Duong; o Office de trong (khong can) - DB da la 0, giu nguyen.
--   2353 VN-LTB051 : chua co vi tri -> Hai Duong; phieu tich  can Office -> can_cai_office 0 => 1.
-- Cot Nguoi su dung trong phieu trung khop DB ca 10 dong. Cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Rollback: kk-vnydf-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @tb TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT);
INSERT INTO @tb (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(1919, 1, 0) /* YDF002 */,
(1923, 1, 0) /* YDF001 */,
(2114, 1, 0) /* YDF003 */,
(2353, 1, 1) /* VN-LTB051 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly, tb.can_cai_office = p.can_cai_office,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @tb p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int));
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNYDF,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-YDF' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

