-- Phieu 'SS.xlsx' (VN-SS, luu 19/09/2026 15:31 (ban dien bo sung)).
-- 28 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vnss-20260919-dot2-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(484, 1, 0) /* VN-SS037 - Hai Duong */,
(485, 1, 0) /* VN-SS022 - Hai Duong */,
(489, 1, 0) /* VN-SS075 - Hai Duong */,
(490, 1, 0) /* VN-SS025 - Hai Duong */,
(491, 1, 0) /* VN-SS078 - Hai Duong */,
(492, 1, 0) /* VN-SS079 - Hai Duong */,
(493, 1, 0) /* VN-SS052 - Hai Duong */,
(494, 1, 0) /* VN-SS080 - Hai Duong */,
(495, 1, 0) /* VN-SS053 - Hai Duong */,
(496, 1, 0) /* VN-SS076 - Hai Duong */,
(500, 1, 0) /* VN-SS029 - Hai Duong */,
(501, 1, 0) /* VN-SS077 - Hai Duong */,
(502, 1, 0) /* VN-SS055 - Hai Duong */,
(504, 1, 0) /* VN-SS033 - Hai Duong */,
(505, 1, 0) /* VN-SS031 - Hai Duong */,
(833, 1, 0) /* VN-SS054 - Hai Duong */,
(834, 1, 0) /* VN-SS008 - Hai Duong */,
(838, 1, 0) /* VN-SS048 - Hai Duong */,
(843, 1, 0) /* VN-SS030-1 - Hai Duong */,
(1027, 1, 0) /* VN-SS021 - Hai Duong */,
(1029, 1, 0) /* VN-SS073 - Hai Duong */,
(1030, 1, 0) /* VN-SS068 - Hai Duong */,
(1468, 1, 0) /* VN-SS072 - Hai Duong */,
(1639, 1, 0) /* VN-SS020 - Hai Duong */,
(1738, 1, 0) /* VN-SS026 - Hai Duong */,
(1764, 1, 0) /* VN-SS071 - Hai Duong */,
(2228, 1, 1) /* VN-SS046 - Hai Duong */,
(2274, 1, 0) /* VN-SS051 - Hai Duong */;

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

