-- Rollback cho mayin-tao-bang-20260918.sql: xoa hoan toan 2 bang cua module Quan ly may in.
-- CANH BAO: mat toan bo chi so da thu thap. Chi chay khi muon go han module nay.
SET XACT_ABORT ON;
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MayIn_ChiSo') DROP TABLE dbo.MayIn_ChiSo;
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MayIn')       DROP TABLE dbo.MayIn;
PRINT N'Da xoa MayIn_ChiSo va MayIn.';