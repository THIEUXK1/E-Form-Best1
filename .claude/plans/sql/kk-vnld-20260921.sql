-- Phieu 'LD.xlsx' (VN-LD, luu 21/09/2026 08:46).
-- 8 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vnld-20260921-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(376, 1, 1) /* VN-LD022 - Hai Duong */,
(1924, 1, 0) /* VN-LD031 - Hai Duong */,
(2151, 1, 0) /* LD-COPOWER 2 - Hai Duong */,
(2207, 1, 0) /* VN-LD061 - Hai Duong */,
(2210, 1, 0) /* LD-COPOWER - Hai Duong */,
(2213, 1, 0) /* DOSORAMA - Hai Duong */,
(2215, 1, 0) /* DOSORAMA 2 - Hai Duong */,
(2217, 1, 0) /* DOSORAMA 3 - Hai Duong */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly,
              tb.can_cai_office = ISNULL(p.can_cai_office, tb.can_cai_office),
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @p p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly
       OR (p.can_cai_office IS NOT NULL AND ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int)));
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

-- Nhom 2: ghi chu tu phieu (ten dinh danh thuc te cua may / tinh trang mang).
-- DB dang trong -> ghi thang; DB da co noi dung khac -> NOI THEM dang 'cu | moi'.
DECLARE @gc TABLE (id_thiet_bi INT PRIMARY KEY, ghi_chu NVARCHAR(MAX));
INSERT INTO @gc (id_thiet_bi, ghi_chu) VALUES
(397, N'LD1-TIVI') /* VN-LD059 */,
(1058, N'LD1-CẮT VẢI') /* VN-LTB061 */,
(1924, N'LD1-COPOWER A (K HIỆN TT)') /* VN-LD031 */,
(2151, N'LD1-COPOWER (K CÓ MẠNG)') /* LD-COPOWER 2 */,
(2207, N'LD2-COPOWER (K HIỆN TT)') /* VN-LD061 */,
(2210, N'LD2-COPOWER D ( K HIỆN TT)') /* LD-COPOWER */,
(2213, N'DOSORAMA B (K HIỆN TT)') /* DOSORAMA */,
(2215, N'DOSORAMA C ( K CÓ MẠNG)') /* DOSORAMA 2 */,
(2217, N'DOSORAMA A (K CÓ MẠNG)') /* DOSORAMA 3 */;
UPDATE tb
SET tb.ghi_chu = CASE WHEN ISNULL(LTRIM(RTRIM(tb.ghi_chu)),'') = '' THEN g.ghi_chu
                      ELSE LTRIM(RTRIM(tb.ghi_chu)) + N' | ' + g.ghi_chu END,
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @gc g ON g.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND CHARINDEX(g.ghi_chu, ISNULL(tb.ghi_chu,'')) = 0;
PRINT N'Nhom 2 - da ghi ghi chu: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 2 THEN 1 ELSE 0 END) AS NgheAn,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 3 THEN 1 ELSE 0 END) AS HungYen,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-LD' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;



