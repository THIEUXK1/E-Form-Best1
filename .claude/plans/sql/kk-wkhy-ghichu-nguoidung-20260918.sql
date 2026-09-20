-- Phieu 'WK, WV-HY.xlsx' (luu 18/09/2026 14:49) - cap nhat theo cot Ghi chu.
-- Nhom 1: 4 may ghi 'nghi viec doi nguoi moi' -> id_nguoi_dung = NULL (de trong nguoi su dung).
-- Nhom 2: 1 may (VN-WT003) ghi chu la ten nguoi moi -> gan lai cho Giang Thi Loan (V261505, id 6956).
-- Nhom 3: ghi noi dung Ghi chu cua 9 may vao cot ghi_chu (hien tai deu dang trong).
-- Vi tri dia ly / can_cai_office cua phieu nay da khop DB, khong dong toi.
-- Chay lai lan hai an toan. Rollback: kk-wkhy-ghichu-nguoidung-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Nhom 1: nguoi dung cu da nghi viec, chua co nguoi thay -> de trong
DECLARE @trong TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @trong (id_thiet_bi) VALUES
(1821) /* VN-WV001 - dang la Đỗ Thị Nhàn (V251649) */,
(1825) /* VN-WV032 - dang la Lê Thùy Trang (V251648) */,
(2243) /* VN-WK050 - dang la Đỗ Diệu Linh (V260017) */,
(2371) /* VN-WK056 - dang la Đỗ Diệu Linh (V260017) */;
UPDATE tb SET tb.id_nguoi_dung = NULL, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @trong t ON t.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND tb.id_nguoi_dung IS NOT NULL;
PRINT N'Nhom 1 - da bo nguoi su dung: ' + CAST(@@ROWCOUNT AS varchar);

-- Nhom 2: ghi chu ghi ten nguoi tiep nhan may
UPDATE tb SET tb.id_nguoi_dung = 6956, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb
WHERE tb.id_thiet_bi = 1838 AND tb.NgayXoa IS NULL AND ISNULL(tb.id_nguoi_dung,-1) <> 6956;
PRINT N'Nhom 2 - da gan nguoi moi: ' + CAST(@@ROWCOUNT AS varchar);

-- Nhom 3: ghi chu tu phieu; DB dang co noi dung khac thi noi them, khong xoa cai cu
DECLARE @gc TABLE (id_thiet_bi INT PRIMARY KEY, ghi_chu NVARCHAR(MAX));
INSERT INTO @gc (id_thiet_bi, ghi_chu) VALUES
(340, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WK057 */,
(1293, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WV026 */,
(1353, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WV027 */,
(1773, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WK049 */,
(1821, N'nghỉ việc đợi người mới') /* VN-WV001 */,
(1825, N'nghỉ việc đợi người mới') /* VN-WV032 */,
(1838, N'Giàng Thị Loan 江氏鸾 (V261505)') /* VN-WT003 */,
(2243, N'nghỉ việc đợi người mới') /* VN-WK050 */,
(2371, N'nghỉ việc đợi người mới') /* VN-WK056 */;
UPDATE tb
SET tb.ghi_chu = CASE WHEN ISNULL(LTRIM(RTRIM(tb.ghi_chu)),'') = '' THEN g.ghi_chu
                      ELSE LTRIM(RTRIM(tb.ghi_chu)) + N' | ' + g.ghi_chu END,
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @gc g ON g.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND CHARINDEX(g.ghi_chu, ISNULL(tb.ghi_chu,'')) = 0;
PRINT N'Nhom 3 - da ghi ghi chu: ' + CAST(@@ROWCOUNT AS varchar);

SELECT tb.id_thiet_bi, tb.ten_may_tinh, ISNULL(u.ho_ten, N'(trong)') AS NguoiSuDung, tb.ghi_chu
FROM @gc g JOIN KK_ThietBi tb ON tb.id_thiet_bi = g.id_thiet_bi AND tb.NgayXoa IS NULL
LEFT JOIN [User] u ON u.id_nguoi_dung = tb.id_nguoi_dung
ORDER BY tb.id_thiet_bi;

-- COMMIT;
-- ROLLBACK;

