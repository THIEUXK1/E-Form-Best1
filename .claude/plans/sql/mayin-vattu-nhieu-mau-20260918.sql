-- Them cot vat_tu_json vao dbo.MayIn_ChiSo de luu DAY DU danh sach vat tu may bao ve,
-- phuc vu may in mau (TONER_C / TONER_M / TONER_Y / TONER_K, DRUM_C/M/Y/K...).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Chi ADD COLUMN NULL, khong sua du lieu dang co.
-- Rollback: mayin-vattu-nhieu-mau-20260918-rollback.sql (DROP COLUMN).
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-vattu-nhieu-mau-20260918.sql
--
-- LY DO:
--   Truoc day MayInApiService chi bat dung 2 ten TONER_K / DRUM_K, nen may in mau chi theo doi
--   duoc muc den, ba mau C/M/Y doc len roi vut di. Sau thay doi nay:
--     - vat_tu_json  = mang JSON [{"ten":"TONER_C","phanTram":37}, ...] cua moi vat tu do duoc %.
--     - toner_phan_tram doi nghia thanh MUC THAP NHAT trong cac TONER_* (may den trang van la
--       TONER_K nen so cu khong doi y nghia), drum_phan_tram tuong tu voi DRUM_*.
--   Cot cu giu nguyen kieu/ten nen bang danh sach, bieu do, lich su dang chay khong phai sua.
--
-- KHONG cap nhat nguoc du lieu cu: cac dong da chot truoc do khong con giu danh sach vat tu goc,
-- de vat_tu_json = NULL, giao dien tu dong lui ve hien 2 thanh Muc/Trong nhu truoc.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'vat_tu_json'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo ADD vat_tu_json NVARCHAR(1000) NULL;
    PRINT N'Da them cot dbo.MayIn_ChiSo.vat_tu_json';
END
ELSE
BEGIN
    PRINT N'Cot vat_tu_json da co san, khong lam gi.';
END
GO

-- Doi soat: cot da ton tai chua
SELECT c.name AS cot, t.name AS kieu, c.max_length AS do_dai_byte, c.is_nullable AS cho_null
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND c.name = 'vat_tu_json';
GO
