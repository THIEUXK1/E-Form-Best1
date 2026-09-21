-- ROLLBACK cua mayin-counter-mau-dentrang-20260921.sql
-- Xoa 2 cot counter_in_mau / counter_in_den_trang khoi dbo.MayIn_ChiSo.
--
-- CANH BAO: DROP COLUMN xoa vinh vien so lieu tach mau da chot duoc ke tu luc bat tinh nang.
-- Neu da chay ban moi mot thoi gian thi BACKUP bang truoc khi chay file nay.
-- Truoc khi chay phai deploy lai ban code KHONG con hai cot trong Models/ITForm/MayInChiSo.cs,
-- neu khong moi truy van cham bang nay se loi runtime.
--
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-counter-mau-dentrang-20260921-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Xem truoc co bao nhieu dong that su dang giu so lieu se mat
SELECT COUNT(*) AS so_dong_co_so_tach_mau
FROM dbo.MayIn_ChiSo
WHERE counter_in_mau IS NOT NULL OR counter_in_den_trang IS NOT NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'counter_in_mau'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo DROP COLUMN counter_in_mau;
    PRINT N'Da xoa cot dbo.MayIn_ChiSo.counter_in_mau';
END
ELSE
BEGIN
    PRINT N'Cot counter_in_mau khong ton tai, khong lam gi.';
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.MayIn_ChiSo') AND name = 'counter_in_den_trang'
)
BEGIN
    ALTER TABLE dbo.MayIn_ChiSo DROP COLUMN counter_in_den_trang;
    PRINT N'Da xoa cot dbo.MayIn_ChiSo.counter_in_den_trang';
END
ELSE
BEGIN
    PRINT N'Cot counter_in_den_trang khong ton tai, khong lam gi.';
END
GO

-- Doi soat: khong con cot nao trong hai cot tren
SELECT c.name AS cot_con_lai
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.MayIn_ChiSo')
  AND c.name IN ('counter_in_mau', 'counter_in_den_trang');
GO
