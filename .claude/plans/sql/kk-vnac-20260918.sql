-- Phieu 'AC.xlsx' (VN-AC, luu 18/09/2026 16:24). 21 dong, tat ca chon Hai Duong va tich  can Office.
-- Nhom 1: 5 may con lech -> id_vi_tri_dia_ly = 1 + can_cai_office = 1.
--   Ca 5 deu co ngay_tra_loi_office = 17/09 11:27 (mac dinh dot 1), cu hon file -> ghi de an toan.
-- Nhom 2: 912 VN-AC017 - ghi chu phieu ghi ten nguoi tiep nhan 'Nguyen Thuy Linh (V261448)',
--   DB dang de Vu Thi Thuy Linh (V251432) kem ghi chu 'nghi viec dang o phong ke toan' -> doi sang id 6822.
-- Ghi chu con lai cua phieu (636 VN-AC031 -> Vu Thi Lan V261572) DA duoc nguoi dung tu doi tren web luc 16:21, khong can lam gi.
-- Rollback: kk-vnac-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @tb TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @tb (id_thiet_bi) VALUES
(634) /* VN-AC020 */,
(912) /* VN-AC017 */,
(917) /* VN-LD008 */,
(967) /* VN-AC021 */,
(1887) /* VN-AC006 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 1,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @tb p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> 1);
PRINT N'Nhom 1 - da cap nhat vi tri + Office: ' + CAST(@@ROWCOUNT AS varchar);

UPDATE KK_ThietBi SET id_nguoi_dung = 6822, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi = 912 AND NgayXoa IS NULL AND ISNULL(id_nguoi_dung,-1) <> 6822;
PRINT N'Nhom 2 - da doi nguoi su dung: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNAC,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-AC' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

