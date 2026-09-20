-- Rollback cua mayin-vattu-nhieu-mau-20260918.sql: bo cot vat_tu_json khoi dbo.MayIn_ChiSo.
--
-- CANH BAO: chay file nay se MAT vinh vien danh sach % tung mau muc da luu. Cac cot cu
-- toner_phan_tram / drum_phan_tram khong bi anh huong (van giu muc thap nhat da ghi).
-- Phai go code doc cot nay (MayInChiSo.VatTuJson) truoc khi chay, neu khong app se loi truy van.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'vat_tu_json'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo DROP COLUMN vat_tu_json;
    PRINT N'Da bo cot dbo.MayIn_ChiSo.vat_tu_json';
END
ELSE
BEGIN
    PRINT N'Cot vat_tu_json khong ton tai, khong lam gi.';
END
GO
