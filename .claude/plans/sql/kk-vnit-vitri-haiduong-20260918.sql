-- VN-IT: 20 Laptop VN-ITV031..VN-ITV050 -> id_vi_tri_dia_ly = 1 (Hai Duong).
-- Nguoi dung xac nhan mieng ngay 18/09/2026. can_cai_office cua ca 20 may da = 1 tu truoc, khong dong toi.
-- Chay lai lan hai an toan. Rollback: kk-vnit-vitri-haiduong-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(2468) /* VN-ITV031 */,
(2469) /* VN-ITV032 */,
(2470) /* VN-ITV033 */,
(2471) /* VN-ITV034 */,
(2472) /* VN-ITV035 */,
(2473) /* VN-ITV036 */,
(2474) /* VN-ITV037 */,
(2475) /* VN-ITV038 */,
(2476) /* VN-ITV039 */,
(2477) /* VN-ITV040 */,
(2478) /* VN-ITV041 */,
(2479) /* VN-ITV042 */,
(2480) /* VN-ITV043 */,
(2481) /* VN-ITV044 */,
(2482) /* VN-ITV045 */,
(2483) /* VN-ITV046 */,
(2484) /* VN-ITV047 */,
(2485) /* VN-ITV048 */,
(2486) /* VN-ITV049 */,
(2487) /* VN-ITV050 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1;
PRINT N'Da cap nhat vi tri: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNIT,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL AND (tb.loai_thiet_bi LIKE N'Laptop%' OR tb.loai_thiet_bi LIKE N'M_y t_nh%') THEN 1 ELSE 0 END) AS MayChuaCoViTri
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-IT' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

