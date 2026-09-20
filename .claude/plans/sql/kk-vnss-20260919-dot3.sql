-- Phieu 'SS.xlsx' (VN-SS, luu 19/09/2026 15:38 (ban sua lan 3)).
-- 2 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vnss-20260919-dot3-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(2228, 3, 1) /* VN-SS046 - Hung Yen */,
(2274, 3, 0) /* VN-SS051 - Hung Yen */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly,
              tb.can_cai_office = ISNULL(p.can_cai_office, tb.can_cai_office),
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @p p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly
       OR (p.can_cai_office IS NOT NULL AND ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int)));
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 2 THEN 1 ELSE 0 END) AS NgheAn,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 3 THEN 1 ELSE 0 END) AS HungYen,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-SS' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

