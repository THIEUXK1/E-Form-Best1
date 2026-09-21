-- Them 2 cot counter_in_mau / counter_in_den_trang vao dbo.MayIn_ChiSo de tach so to IN MAU
-- va IN DEN TRANG, thay vi chi luu tong nhu truoc.
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Chi ADD COLUMN NULL, khong sua du lieu dang co.
-- Rollback: mayin-counter-mau-dentrang-20260921-rollback.sql (DROP COLUMN).
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-counter-mau-dentrang-20260921.sql
--
-- LY DO:
--   May in doi moi tra ve dong ho dem rieng qua /home/api/billing-counter:
--     PRINT_TOTAL_COLOR_IMPRESSION  -> so to in mau tu luc xuat xuong
--     PRINT_TOTAL_BW_IMPRESSION     -> so to in den trang tu luc xuat xuong
--   Truoc day MayInApiService co y bo qua hai dong nay (chung la thanh phan con cua
--   PRINT_TOTAL_IMPRESSION, cong vao la dem trung) nen bang chi so khong co so tach mau.
--   Sau thay doi nay hai so duoc luu rieng, bao cao tinh duoc "ky nay in bao nhieu to mau".
--
-- Y NGHIA COT (giong counter_tong: la DONG HO TICH LUY, khong phai so to trong ngay):
--   counter_in_mau       = PRINT_TOTAL_COLOR_IMPRESSION tai thoi diem chot
--   counter_in_den_trang = PRINT_TOTAL_BW_IMPRESSION tai thoi diem chot
--   So to trong ky = hieu cua hai lan chot, dung cach dang tinh cho counter_tong.
--
-- PHAM VI DU LIEU:
--   - Chi may doc duoc qua /home/api/billing-counter moi co hai so nay.
--   - May doc qua trang CentreWare cu (prcnt.htm) hoac PJL cong 9100 chi co tong -> de NULL.
--   - Dong nhap tay, dong nap tu nhat ky loi cung de NULL.
--   - KHONG cap nhat nguoc du lieu cu: cac dong da chot truoc do khong giu so tach mau,
--     de NULL, giao dien hien "--" va bao cao bo qua dong do khi tinh chenh lech.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'counter_in_mau'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo ADD counter_in_mau INT NULL;
    PRINT N'Da them cot dbo.MayIn_ChiSo.counter_in_mau';
END
ELSE
BEGIN
    PRINT N'Cot counter_in_mau da co san, khong lam gi.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'counter_in_den_trang'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo ADD counter_in_den_trang INT NULL;
    PRINT N'Da them cot dbo.MayIn_ChiSo.counter_in_den_trang';
END
ELSE
BEGIN
    PRINT N'Cot counter_in_den_trang da co san, khong lam gi.';
END
GO

-- Doi soat: hai cot da ton tai chua, dung kieu chua
SELECT c.name AS cot, t.name AS kieu, c.is_nullable AS cho_null
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.MayIn_ChiSo')
  AND c.name IN ('counter_in_mau', 'counter_in_den_trang')
ORDER BY c.name;
GO

-- Doi soat: so dong cua bang khong duoc doi truoc/sau khi chay
SELECT COUNT(*) AS tong_dong_chi_so FROM dbo.MayIn_ChiSo;
GO
