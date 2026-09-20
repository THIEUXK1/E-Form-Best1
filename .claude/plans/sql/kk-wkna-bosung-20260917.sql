-- Bo sung theo phieu 'WK NA.xlsx' (ban lam lai cua phieu WK, luu 17/09/2026 11:42),
-- chay SAU script kk-capnhat-vitri-office-20260917.sql.
-- Gom 3 viec:
--   1) Them bo phan VN-WK NA (nhom WK dong tai Nghe An) vao KK_BoPhan
--   2) Chuyen 12 may Nghe An sang bo phan do (dang nam o VN-WK va VN-WK S)
--   3) Cap nhat 11 thay doi vi tri / can cai Office ma phieu moi khac voi DB
-- O vi tri trong phieu go HOA 'NGHE AN' -> van map ve dia diem Nghe An (id = 2) trong danh muc, khong tao dia diem moi.
-- Rollback: kk-wkna-bosung-20260917-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Bo phan moi. Chay lai lan hai khong tao trung (IF NOT EXISTS).
IF NOT EXISTS (SELECT 1 FROM KK_BoPhan WHERE TenBoPhan = N'VN-WK NA')
BEGIN
    INSERT INTO KK_BoPhan (TenBoPhan, IDCongTy, GhiChu, NgayTao, TrangThai)
    VALUES (N'VN-WK NA', 1, N'Nhom WK dong tai Nghe An - tach theo phieu xac nhan 17/09/2026', GETDATE(), 1);
END;

DECLARE @idWkNa INT = (SELECT TOP 1 IDBoPhan FROM KK_BoPhan WHERE TenBoPhan = N'VN-WK NA');
PRINT N'IDBoPhan VN-WK NA = ' + CAST(@idWkNa AS varchar);

-- 2) 12 may Nghe An theo phieu WK NA
DECLARE @mayWkNa TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @mayWkNa (id_thiet_bi) VALUES
(178),  /* VN-WK063 */
(181),  /* VN-WK065 */
(182),  /* VN-WK045 */
(186),  /* VN-WK070 */
(188),  /* VN-WK068 */
(191),  /* VN-WK046 */
(192),  /* VN-WK044 */
(194),  /* VN-WK062 */
(196),  /* VN-WK059 */
(201),  /* VN-WK071 */
(203),  /* VN-WK048 */
(2367); /* VN-WK064 */

-- Ghi lich su TRUOC khi doi, de con luu lai bo phan cu
INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
SELECT N'Cập nhật', N'Thiết Bị', tb.id_thiet_bi,
       N'Chuyển bộ phận: ' + ISNULL(bp.TenBoPhan, N'(trống)') + N' -> VN-WK NA (theo phiếu xác nhận WK NA ngày 17/09/2026)',
       GETDATE(), N'Script IT'
FROM KK_ThietBi tb
JOIN @mayWkNa m ON m.id_thiet_bi = tb.id_thiet_bi
LEFT JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE tb.NgayXoa IS NULL AND ISNULL(tb.IDBoPhan, -1) <> @idWkNa;

UPDATE tb
SET tb.IDBoPhan = @idWkNa,
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb
JOIN @mayWkNa m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND ISNULL(tb.IDBoPhan, -1) <> @idWkNa;
PRINT N'So may da chuyen sang VN-WK NA: ' + CAST(@@ROWCOUNT AS varchar);

-- 3) Vi tri / can cai Office theo phieu moi
--    Chi liet ke nhung dong PHIEU MOI khac DB hien tai.
DECLARE @phieu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NOT NULL);
INSERT INTO @phieu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(186, 2, 0),  /* VN-WK070 - them vi tri Nghe An, o Office de trong */
(191, 2, 1),  /* VN-WK046 - them vi tri Nghe An, tich can Office */
(201, 2, 0),  /* VN-WK071 - them vi tri Nghe An, o Office de trong */
(203, 2, 0),  /* VN-WK048 - them vi tri Nghe An, o Office de trong */
(188, 2, 0),  /* VN-WK068 - bo tich Office (1 -> 0) */
(548, 1, 0),  /* VN-WK014 - bo tich Office */
(550, 1, 0),  /* VN-WK054 - bo tich Office */
(552, 1, 0),  /* VN-WK033 - bo tich Office */
(857, 1, 0),  /* VN-WK012 - bo tich Office */
(1089, 1, 0), /* VN-WK061 - bo tich Office */
(1404, 1, 0); /* VN-WK060 - bo tich Office */

SELECT N'TRUOC' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN tb.can_cai_office = p.can_cai_office THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

UPDATE tb
SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly,
    tb.can_cai_office = p.can_cai_office,
    tb.ngay_tra_loi_office = GETDATE(),
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb
JOIN @phieu p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly, -1) <> p.id_vi_tri_dia_ly
       OR tb.can_cai_office IS NULL OR tb.can_cai_office <> p.can_cai_office);
PRINT N'So dong vi tri/Office da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT N'SAU' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN tb.can_cai_office = p.can_cai_office THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

SELECT bp.TenBoPhan, COUNT(*) AS SoMay
FROM KK_ThietBi tb JOIN @mayWkNa m ON m.id_thiet_bi = tb.id_thiet_bi
LEFT JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
GROUP BY bp.TenBoPhan;

SELECT COUNT(*) AS TongThietBi,
       SUM(CASE WHEN id_vi_tri_dia_ly IS NOT NULL THEN 1 ELSE 0 END) AS CoViTri,
       SUM(CASE WHEN can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice,
       SUM(CASE WHEN can_cai_office = 0 THEN 1 ELSE 0 END) AS KhongCanOffice
FROM KK_ThietBi WHERE NgayXoa IS NULL;

COMMIT;
