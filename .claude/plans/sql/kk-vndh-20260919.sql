-- Phieu 'DH.xlsx' (VN-DH, luu 19/09/2026 10:47).
-- 32 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vndh-20260919-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(413, 1, 1) /* VN-DH021 - Hai Duong */,
(414, 1, 1) /* VN-DH013 - Hai Duong */,
(415, 1, 1) /* VN-DH038 - Hai Duong */,
(416, 1, 1) /* VN-DH005 - Hai Duong */,
(417, 1, 1) /* VN-DH006 - Hai Duong */,
(418, 1, 1) /* VN-DH012 - Hai Duong */,
(419, 1, 1) /* VN-DH031 - Hai Duong */,
(420, 1, 1) /* VN-DH032 - Hai Duong */,
(421, 1, 1) /* VN-DH024 - Hai Duong */,
(422, 1, 1) /* VN-DH026 - Hai Duong */,
(423, 1, 1) /* VN-DH011 - Hai Duong */,
(701, 1, 1) /* VN-DH040 - Hai Duong */,
(702, 1, 1) /* VN-DH020 - Hai Duong */,
(703, 1, 1) /* VN-DH022 - Hai Duong */,
(704, 1, 1) /* VN-DH007 - Hai Duong */,
(705, 1, 1) /* VN-DH010 - Hai Duong */,
(706, 1, 1) /* VN-DH008 - Hai Duong */,
(707, 1, 1) /* VN-DH002 - Hai Duong */,
(708, 1, 1) /* VN-DH034 - Hai Duong */,
(709, 1, 1) /* VN-DH003 - Hai Duong */,
(911, 1, 1) /* VN-DH023 - Hai Duong */,
(921, 1, 1) /* VN-DH025 - Hai Duong */,
(1066, 1, 1) /* VN-DH037 - Hai Duong */,
(1918, 1, 0) /* SM-4 - Hai Duong */,
(1920, 1, 0) /* SM-3 - Hai Duong */,
(1921, 1, 0) /* DH02 - Hai Duong */,
(1963, 1, 0) /* TRS - Hai Duong */,
(1964, 1, 0) /* DAPG - Hai Duong */,
(2116, 1, 0) /*  - Hai Duong */,
(2118, 1, 0) /* VN-DH035 - Hai Duong */,
(2141, 1, 1) /* VN-DH041 - Hai Duong */,
(2344, 1, 0) /* CÂN NHÀ KÍNH - Hai Duong */;

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
WHERE bp.TenBoPhan = N'VN-DH' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

