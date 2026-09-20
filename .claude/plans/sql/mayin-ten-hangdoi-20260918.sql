-- Bo sung cot ten_hang_doi cho bang MayIn va nap ten hang doi in lay tu print server \\vn-printersrv.
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Cham DUY NHAT bang MayIn: them 1 cot NULL duoc,
-- roi UPDATE cot do theo dia_chi_ip. Khong xoa, khong sua cot nao khac.
-- Rollback: mayin-ten-hangdoi-20260918-rollback.sql (xoa cot vua them).
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-ten-hangdoi-20260918.sql
--
-- NGUON DU LIEU (doc luc 18/09/2026 bang Get-Printer/Get-PrinterPort tren \\vn-printersrv):
--   - 67 hang doi in, trong do 62 dia chi IP phan biet; 2 hang doi khong co IP (VN-WK DocuPrint 3105,
--     VN-WV OFFICE DocuPrint 3105) nen khong nap duoc.
--   - 53 IP khop voi bang MayIn -> duoc dat ten.
--   - 3 IP co NHIEU hang doi tro toi (10.0.59.15, 10.0.59.29, 10.0.59.83): ghep lai, ngan cach bang " | ".

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MayIn') AND name = 'ten_hang_doi')
BEGIN
    ALTER TABLE dbo.MayIn ADD ten_hang_doi NVARCHAR(200) NULL;
END;
GO

DECLARE @hangDoi TABLE (dia_chi_ip VARCHAR(45) PRIMARY KEY, ten_hang_doi NVARCHAR(200));

INSERT INTO @hangDoi (dia_chi_ip, ten_hang_doi) VALUES
('10.0.17.170',N'VN-PRINTERS-QA-Dai-17.170'),
('10.0.17.192',N'VN-Printer-SD-NgheAn-17.192'),
('10.0.59.10',N'VN-AC PRINTER C325 -Chanel .10'),
('10.0.59.100',N'VN-OFFICE DS FX DocuPrint 3105 PCL 6'),
('10.0.59.101',N'VN-PT-OFFICE-Printer'),
('10.0.59.102',N'VN-QC-OFFICE-Printer 3360s .102'),
('10.0.59.103',N'YDF-3360S'),
('10.0.59.106',N'Brother PT-P750W'),
('10.0.59.110',N'VN-SD-OFFICE-Printer'),
('10.0.59.111',N'VN-HR-OFFICE-Printer'),
('10.0.59.113',N'DocuPrint 3205 d Micheal'),
('10.0.59.115',N'Printer-EGE6'),
('10.0.59.128',N'VN-PUR-OFFICE-Printer'),
('10.0.59.129',N'VN-IT-OFFICE'),
('10.0.59.130',N'VN-LTB Printer.130'),
('10.0.59.131',N'VN-Printer LTB'),
('10.0.59.133',N'VN-LTB Piteo.133'),
('10.0.59.137',N'FX DocuPrint 3205 d QC Dong Goi'),
('10.0.59.141',N'VN-WV-OFFICE-Printer-141'),
('10.0.59.142',N'VN-RD-OFFICE 3205.142'),
('10.0.59.143',N'VN-RD Dai'),
('10.0.59.144',N'VN-Printer WT'),
('10.0.59.145',N'VN-WA OFFICE PRINTER 4830'),
('10.0.59.146',N'VN-Printer PQC 3360s 146'),
('10.0.59.147',N'VN-WA-3360s-59.147'),
('10.0.59.148',N'VN-WA-Printer 3360s 59.148'),
('10.0.59.149',N'VN-WA-K-T2-59.149'),
('10.0.59.15',N'FX DocuPrint 3105 PCL 6 | VN-PMC-OFFICE-Printer_3105'),
('10.0.59.150',N'VN-Printer LTB gần WH.159'),
('10.0.59.153',N'VN-Printer TL OFFICE NEW 153'),
('10.0.59.156',N'VN-PRINTER-AC-NEW-4570.156'),
('10.0.59.157',N'VN-SD-Printer-Michelle-4830.157'),
('10.0.59.158',N'VN-Pitel-59.158'),
('10.0.59.159',N'VN-F8-WH+SS+DH-59.159'),
('10.0.59.215',N'VN-WV-Printer-OF-215 TAM'),
('10.0.59.26',N'VN-QC_DAI 59.26'),
('10.0.59.27',N'VN-QA-OFFICE-Printer'),
('10.0.59.28',N'VN-PMC-OFFICE-Printer'),
('10.0.59.29',N'VN-SS DocuPrint 3105 | VN-SS-OFFICE-Printer'),
('10.0.59.30',N'VN-WD DocuPrint 3105'),
('10.0.59.36',N'VN_EG_Dept_Canon 2525'),
('10.0.59.39',N'VN-WA DocuPrint 3105'),
('10.0.59.40',N'VN-LAB  DocuPrint 3105'),
('10.0.59.42',N'VN-WV DocuPrint 3105'),
('10.0.59.43',N'VN-EG  DocuPrint 3105'),
('10.0.59.46',N'VN-WH-OFFICE-DocuPrint 3105.46'),
('10.0.59.47',N'VN-VatLy.47'),
('10.0.59.55',N'FX DocuPrint 3105 PCL 6 WV - WS Office'),
('10.0.59.58',N'VN-WV Printer of David .58'),
('10.0.59.61',N'VN-AC-OFFICE-Printer'),
('10.0.59.62',N'FX LD 3105 59.62'),
('10.0.59.65',N'FX LD NEW 3205D PCL 6'),
('10.0.59.66',N'VN-SHD FX DocuPrint59.66'),
('10.0.59.67',N'VN-DF FX3105 59.67'),
('10.0.59.68',N'VN-DF 3205D 59.68'),
('10.0.59.71',N'FX  PW 3205D 59.71'),
('10.0.59.81',N'FX WK 3205D 59.81'),
('10.0.59.83',N'FX YDF 3205D 59.83 | VN-YDF Printer'),
('10.0.59.96',N'VN-Printer SS DH LTB'),
('10.0.59.97',N'VN-SHD-3360 S.97'),
('10.0.59.98',N'VN-PRINTER-SD-PMC-NEW'),
('10.0.59.99',N'VN-PRINTER-HR-NEW');

DECLARE @soHangDoi INT = (SELECT COUNT(*) FROM @hangDoi);
PRINT N'Hang doi doc tu print server: ' + CAST(@soHangDoi AS varchar);

-- Chi ghi khi ten thuc su khac, de chay lai lan hai khong dong vao du lieu dang dung
UPDATE m
SET    m.ten_hang_doi = h.ten_hang_doi,
       m.ngay_cap_nhat = GETDATE()
FROM   dbo.MayIn m
JOIN   @hangDoi h ON h.dia_chi_ip = m.dia_chi_ip
WHERE  ISNULL(m.ten_hang_doi, N'') <> h.ten_hang_doi;

PRINT N'So may duoc dat ten: ' + CAST(@@ROWCOUNT AS varchar);
GO

-- Doi soat sau khi chay (ky vong: 56 dong MayIn co ten -- nhieu hon 53 vi mot so IP dung chung
-- cho 2 dong trong danh muc; phan con lai chua khop IP nao tren print server)
SELECT COUNT(*) AS may_co_ten_hang_doi FROM dbo.MayIn WHERE ten_hang_doi IS NOT NULL;

SELECT bo_phan, model, serial, dia_chi_ip, ten_hang_doi, vi_tri
FROM   dbo.MayIn
WHERE  ten_hang_doi IS NULL AND trang_thai = N'HoatDong' AND dia_chi_ip IS NOT NULL
ORDER  BY bo_phan, dia_chi_ip;