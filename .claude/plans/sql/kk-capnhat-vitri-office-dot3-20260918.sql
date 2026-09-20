-- Dot 3: cap nhat vi tri dia ly + nhu cau cai Office tu cac phieu nhan sau dot 1 (thu muc 'Sua chua 5')
--   IT.xlsx          (18/09 13:54) - SD, VN-IT       : 48 may
-- Quy uoc giu nguyen nhu dot 1: o 'Can cai Office' khong tich = KHONG can -> can_cai_office = 0.
-- DA LOAI TRU 10 may ma nguoi dung nhap tren web SAU khi phieu duoc luu (ngay_tra_loi_office > gio luu file):
--   VN-QA 4, VN-WH 4, VN-AC 1, VN-YDF 1 -> du lieu tren web moi hon, khong ghi de.
-- Chay lai lan hai an toan (chi UPDATE dong con lech). Rollback: kk-capnhat-vitri-office-dot3-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Dia diem moi: 4 may cua VN-QA ghi 'HAI PHONG' - danh muc KK_ViTriDiaLy chua co.
-- Them mot lan, chay lai khong tao trung.
IF NOT EXISTS (SELECT 1 FROM KK_ViTriDiaLy WHERE ten_vi_tri_dia_ly = N'Hải Phòng')
BEGIN
    INSERT INTO KK_ViTriDiaLy (ten_vi_tri_dia_ly, mo_ta, thu_tu, dang_su_dung, ngay_tao)
    VALUES (N'Hải Phòng', N'Bổ sung theo phiếu xác nhận VN-QA ngày 17/09/2026', 4, 1, GETDATE());
END;
DECLARE @idHP INT = (SELECT TOP 1 id_vi_tri_dia_ly FROM KK_ViTriDiaLy WHERE ten_vi_tri_dia_ly = N'Hải Phòng');
PRINT N'id_vi_tri_dia_ly Hai Phong = ' + CAST(@idHP AS varchar);

DECLARE @phieu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @phieu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(342,1,1) /* VN-IT086 - BPVN - VN-IT - IT.xlsx */,
(343,1,0) /* VN-DCV002 - BPVN - VN-IT - IT.xlsx */,
(345,1,1) /* VN-ITV015 - BPVN - VN-IT - IT.xlsx */,
(358,1,1) /* VN-IT004 - BPVN - VN-IT - IT.xlsx */,
(359,1,1) /* VN-IT168 - BPVN - VN-IT - IT.xlsx */,
(360,1,1) /* VN-IT018 - BPVN - VN-IT - IT.xlsx */,
(427,1,0) /* VN-MAXHUB-360 - BPVN - VN-IT - IT.xlsx */,
(477,1,1) /* VN-SD101 - BPVN - VN-IT - IT.xlsx */,
(631,1,1) /* VN-IT001 - BPVN - VN-IT - IT.xlsx */,
(633,1,1) /* VN-IT011 - BPVN - VN-IT - IT.xlsx */,
(901,1,1) /* VN-ITV026 - BPVN - VN-IT - IT.xlsx */,
(904,1,1) /* VN-ITV010 - BPVN - VN-IT - IT.xlsx */,
(905,1,1) /* VN-ITV014 - BPVN - VN-IT - IT.xlsx */,
(995,1,1) /* VN-IT091 - BPVN - VN-IT - IT.xlsx */,
(1063,1,1) /* VN-ITV018 - BPVN - VN-IT - IT.xlsx */,
(1567,1,0) /* VN-HR005 - BPVN - VN-IT - IT.xlsx */,
(1861,1,1) /* VN-ITV011 - BPVN - VN-IT - IT.xlsx */,
(1942,1,1) /* VN-ITV017 - BPVN - VN-IT - IT.xlsx */,
(2080,1,1) /* VN-ITV021 - BPVN - VN-IT - IT.xlsx */,
(2081,1,1) /* VN-ITV013 - BPVN - VN-IT - IT.xlsx */,
(2119,1,1) /* VN-ITV001 - BPVN - VN-IT - IT.xlsx */,
(2120,1,1) /* VN-ITV002 - BPVN - VN-IT - IT.xlsx */,
(2121,1,1) /* VN-ITV004 - BPVN - VN-IT - IT.xlsx */,
(2122,1,1) /* VN-ITV006 - BPVN - VN-IT - IT.xlsx */,
(2123,1,1) /* VN-ITV007 - BPVN - VN-IT - IT.xlsx */,
(2124,1,1) /* VN-ITV012 - BPVN - VN-IT - IT.xlsx */,
(2125,1,1) /* VN-ITV016 - BPVN - VN-IT - IT.xlsx */,
(2126,1,1) /* VN-ITV019 - BPVN - VN-IT - IT.xlsx */,
(2127,1,1) /* VN-ITV022 - BPVN - VN-IT - IT.xlsx */,
(2128,1,1) /* VN-ITV023 - BPVN - VN-IT - IT.xlsx */,
(2129,1,0) /* VN-ITV024 - BPVN - SD - IT.xlsx */,
(2130,1,1) /* VN-ITV025 - BPVN - VN-IT - IT.xlsx */,
(2131,1,1) /* VN-ITV027 - BPVN - VN-IT - IT.xlsx */,
(2132,1,1) /* VN-ITV028 - BPVN - VN-IT - IT.xlsx */,
(2133,1,1) /* VN-ITV029 - BPVN - VN-IT - IT.xlsx */,
(2134,1,1) /* VN-ITV030 - BPVN - VN-IT - IT.xlsx */,
(2163,1,1) /* VN-PUR021 - BPVN - VN-IT - IT.xlsx */,
(2178,1,1) /* VN-IT088 - BPVN - VN-IT - IT.xlsx */,
(2179,1,1) /* VN-IT1001 - BPVN - VN-IT - IT.xlsx */,
(2180,1,1) /* VN-IT090 - BPVN - VN-IT - IT.xlsx */,
(2354,1,1) /*  - BPVN - VN-IT - IT.xlsx */,
(2356,1,1) /* N/A - BPVN - VN-IT - IT.xlsx */,
(2357,1,1) /* N/A - BPVN - VN-IT - IT.xlsx */,
(2360,1,1) /* N/A - BPVN - VN-IT - IT.xlsx */,
(2361,1,1) /* N/A - BPVN - VN-IT - IT.xlsx */,
(2364,1,0) /* GL-SRV - BPVN - VN-IT - IT.xlsx */,
(2365,1,0) /* YMDC - BPVN - VN-IT - IT.xlsx */,
(2373,1,0) /* VN-PMC032 - BPVN - VN-IT - IT.xlsx */;

SELECT N'TRUOC' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN ISNULL(tb.id_vi_tri_dia_ly,-1) = ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1)) THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN ISNULL(tb.can_cai_office,-1) = ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)) THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

UPDATE tb
SET tb.id_vi_tri_dia_ly    = ISNULL(p.id_vi_tri_dia_ly, tb.id_vi_tri_dia_ly),
    tb.can_cai_office      = ISNULL(p.can_cai_office, tb.can_cai_office),
    tb.ngay_tra_loi_office = GETDATE(),
    tb.ngay_cap_nhat       = GETDATE()
FROM KK_ThietBi tb JOIN @phieu p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1))
       OR ISNULL(tb.can_cai_office,-1) <> ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)));
PRINT N'So dong da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT N'SAU' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN ISNULL(tb.id_vi_tri_dia_ly,-1) = ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1)) THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN ISNULL(tb.can_cai_office,-1) = ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)) THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

SELECT COUNT(*) AS TongThietBi,
       SUM(CASE WHEN id_vi_tri_dia_ly IS NOT NULL THEN 1 ELSE 0 END) AS CoViTri,
       SUM(CASE WHEN can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice,
       SUM(CASE WHEN can_cai_office = 0 THEN 1 ELSE 0 END) AS KhongCanOffice
FROM KK_ThietBi WHERE NgayXoa IS NULL;

-- Xem so lieu doi soat roi bo chu thich dong duoi de ghi that. Sai thi ROLLBACK;
-- COMMIT;
-- ROLLBACK;


