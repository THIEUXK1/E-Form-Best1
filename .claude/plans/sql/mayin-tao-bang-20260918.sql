-- Tao module Quan ly may in: 2 bang moi MayIn + MayIn_ChiSo, va nap 111 may in
-- tu file "Print out information2026.xlsx" (sheet BANG THEO DOI TRANG IN 2025).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Chi TAO BANG MOI, khong cham bang nao dang co.
-- Rollback: mayin-tao-bang-20260918-rollback.sql (DROP 2 bang moi).
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-tao-bang-20260918.sql
--
-- LUU Y VE DU LIEU EXCEL:
--   - Cot moc thoi gian trong file la serial 43852..44187 = 22/01/2020..22/12/2020, khong khop
--     tieu de "2025", nen KHONG nhap lich su theo thang. Chi lay chi so cua cot co du lieu MOI NHAT
--     lam moc ban dau (nguon = N'Excel', ngay_chot = 18/09/2026).
--   - O nao ghi "Tam dung" / "Bao phe" / "Chua nap" duoc doi thanh trang_thai tuong ung, chi so de NULL.
--   - O cot IP ghi "USB", "Day USB", "10.0.59.146/no"... khong phai IP -> dia_chi_ip = NULL,
--     giu nguyen van trong ghi_chu.
--   - Cap (model, serial) la duy nhat trong file (serial 100068 va 100099 bi trung nhung khac model).
--
-- CO SO CUA COT theo_doi_tu_dong (do thuc te luc 08:30 ngay 18/09/2026, chi GET, khong ghi gi):
--   - Da thu 83 IP trong file, 24 may tra loi endpoint /home/api/billing-counter (dong Apeos /
--     ApeosPrint doi moi) -> bat doc tu dong.
--   - So con lai la Fuji Xerox doi cu chay CentreWare Internet Services, khong co endpoint nay va
--     SNMP (cong 161) cung bi tat -> de theo_doi_tu_dong = 0, phai nhap chi so tay.
--   - Doi chieu serial may tra ve voi serial trong Excel: khop het, TRU 10.0.59.68 dang la serial
--     100090 nhung file con ghi them may 100093 cung IP -> may 100093 de tat doc tu dong kem ghi chu.

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1) Danh muc may in
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MayIn')
BEGIN
    CREATE TABLE dbo.MayIn
    (
        id_may_in         INT IDENTITY(1,1) NOT NULL,
        bo_phan           NVARCHAR(50)  NULL,
        model             NVARCHAR(50)  NOT NULL,
        serial            NVARCHAR(50)  NOT NULL,
        dia_chi_ip        VARCHAR(45)   NULL,
        vi_tri            NVARCHAR(200) NULL,
        -- HoatDong | TamDung | BaoPhe
        trang_thai        NVARCHAR(20)  NOT NULL CONSTRAINT DF_MayIn_trang_thai DEFAULT (N'HoatDong'),
        -- 1 = cho phep job nen goi API web cua may in de lay chi so
        theo_doi_tu_dong  BIT           NOT NULL CONSTRAINT DF_MayIn_theo_doi DEFAULT (0),
        lan_doc_cuoi      DATETIME      NULL,
        ket_qua_doc_cuoi  NVARCHAR(255) NULL,
        ghi_chu           NVARCHAR(500) NULL,
        ngay_tao          DATETIME      NOT NULL CONSTRAINT DF_MayIn_ngay_tao DEFAULT (GETDATE()),
        ngay_cap_nhat     DATETIME      NULL,
        CONSTRAINT PK_MayIn PRIMARY KEY (id_may_in),
        -- Chan nhap trung khi chay lai script hoac import lai file Excel
        CONSTRAINT UQ_MayIn_model_serial UNIQUE (model, serial)
    );
    CREATE INDEX IX_MayIn_bo_phan ON dbo.MayIn (bo_phan);
    CREATE INDEX IX_MayIn_dia_chi_ip ON dbo.MayIn (dia_chi_ip);
END;
GO

-- 2) Chi so doc duoc theo tung ngay. Moi may moi ngay chi giu MOT dong (ban doc moi nhat trong ngay)
--    -> job nen chay lai nhieu lan trong ngay khong lam phinh bang, va so trang in moi ngay
--    tinh duoc bang chenh lech counter_tong giua hai ngay lien tiep.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MayIn_ChiSo')
BEGIN
    CREATE TABLE dbo.MayIn_ChiSo
    (
        id_chi_so           BIGINT IDENTITY(1,1) NOT NULL,
        id_may_in           INT           NOT NULL,
        ngay_chot           DATE          NOT NULL,
        thoi_diem_doc       DATETIME      NOT NULL,
        counter_in          INT           NULL,
        counter_copy        INT           NULL,
        counter_scan        INT           NULL,
        counter_tong        INT           NULL,
        toner_phan_tram     INT           NULL,
        drum_phan_tram      INT           NULL,
        trang_thai_thiet_bi NVARCHAR(50)  NULL,
        -- API | Excel | NhapTay
        nguon               NVARCHAR(20)  NOT NULL CONSTRAINT DF_MayInChiSo_nguon DEFAULT (N'API'),
        ghi_chu             NVARCHAR(255) NULL,
        CONSTRAINT PK_MayIn_ChiSo PRIMARY KEY (id_chi_so),
        CONSTRAINT FK_MayIn_ChiSo_MayIn FOREIGN KEY (id_may_in) REFERENCES dbo.MayIn (id_may_in),
        CONSTRAINT UQ_MayIn_ChiSo_ngay UNIQUE (id_may_in, ngay_chot)
    );
    CREATE INDEX IX_MayIn_ChiSo_ngay ON dbo.MayIn_ChiSo (ngay_chot);
END;
GO

-- 3) Nap danh muc + moc chi so ban dau. Chay lai lan hai: khong them dong nao (loc theo model+serial).
DECLARE @nguon TABLE
(
    bo_phan NVARCHAR(50), model NVARCHAR(50), serial NVARCHAR(50), dia_chi_ip VARCHAR(45),
    vi_tri NVARCHAR(200), trang_thai NVARCHAR(20), theo_doi BIT, ghi_chu NVARCHAR(500), counter_tong INT
);

INSERT INTO @nguon (bo_phan, model, serial, dia_chi_ip, vi_tri, trang_thai, theo_doi, ghi_chu, counter_tong) VALUES
(N'AC',N'FX4070',N'148986','10.0.59.61',N'Office  AC',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',1071670),
(N'CV',N'FX3105',N'109553','10.0.59.45',N'Workshop CV',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',312719),
(N'DF',N'FX3360s',N'100111','10.0.59.52',N'Workshop DF',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',312683),
(N'DF',N'FX3205',N'100040','10.0.59.71',N'DF',N'TamDung',0,NULL,NULL),
(N'DF',N'FX3360s',N'100090','10.0.59.68',N'OFFICE DF',N'HoatDong',1,NULL,221728),
(N'DF',N'FX3205',N'100677','10.0.59.127',N'Workshop DF',N'TamDung',0,NULL,NULL),
(N'DF',N'FX3360s',N'100093','10.0.59.68',N'DF',N'HoatDong',0,N'IP 10.0.59.68 dang tra ve serial 100090 - IP trong Excel co the da doi, tam tat doc tu dong',269477),
(N'EG',N'FX3105',N'109900','10.0.59.43',N'Office EG',N'BaoPhe',0,NULL,NULL),
(N'EG',N'Docuprint 3105',N'108470','10.0.59.51',N'XLNT',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',88369),
(N'EG',N'FX4830',N'010072','10.0.59.115',N'OFFICE EG',N'HoatDong',1,NULL,58805),
(N'GL',N'FX6340',N'000353','10.10.197.252',N'Phòng mẫu Gia Lộc',N'HoatDong',1,NULL,66791),
(N'GL',N'FX3567',N'399088',NULL,N'Phòng mẫu Gia Lộc',N'HoatDong',0,NULL,7172),
(N'HR',N'FX3373',N'108659','10.0.59.111',N'OFFICE HR',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',1513832),
(N'HR',N'FX3570',N'140928','10.0.59.99',N'OFFICE HR mới',N'HoatDong',1,NULL,247748),
(N'HR',N'FX3570',N'139159',NULL,N'HR MEGA',N'HoatDong',0,NULL,26735),
(N'HR',N'3205',N'101131','10.0.6.199',N'KhoNghệ An',N'BaoPhe',0,NULL,NULL),
(N'IE',N'FX3105',N'109924','10.0.59.54',N'Office  IE',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',20523),
(N'IT',N'C325 DW',N'TR7-004226','10.0.59.10',N'Office AC',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',4474),
(N'AC',N'FX4570',N'134447','10.0.59.156',N'Office AC',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',100250),
(N'GM',N'FX3360s',N'100149','10.0.17.150',NULL,N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',986),
(N'IT',N'FX4830',N'010069','10.0.59.129',N'IT OFFICE',N'HoatDong',1,NULL,30171),
(N'LAB',N'FX3205',N'100668','10.0.59.130',N'OFFICE LAB new',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',94973),
(N'LD',N'FX3105',N'109914','10.0.59.40',N'Office LD Cài USB',N'TamDung',0,NULL,NULL),
(N'LD',N'FX3205',N'100035','10.0.59.107',N'Office LD',N'BaoPhe',0,NULL,NULL),
(N'LD',N'FX3105',N'108520','10.0.59.62',N'Office LD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',223764),
(N'LD',N'FX3570',N'100333','10.0.59.131',N'LD OFFICE',N'HoatDong',1,NULL,700587),
(N'LTB',N'FX3205',N'101148',NULL,N'Office DH+LTB .USB',N'TamDung',0,N'Ket noi theo Excel: USB',NULL),
(N'LTB',N'FX3360s',N'100103','10.0.59.103',N'Office YDF',N'HoatDong',1,NULL,190170),
(N'LTB',N'FX3360s',N'100105','10.0.59.100',N'LTB Soi Màu',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',237191),
(N'LTB 2',N'FX3205',N'100037','10.0.59.150',N'LTB tầng 2, gần VP Kho',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',394131),
(N'PMC',N'FX3105',N'109913','10.0.59.15',N'Office  PMC',N'TamDung',0,NULL,NULL),
(N'PMC',N'FX3370',N'504725','10.0.59.28',N'Office  PMC',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',1863814),
(N'PMC',N'FX3570',N'140927','10.0.59.98',N'Office  PMC Mới',N'HoatDong',1,NULL,158373),
(N'PT',N'FX3371',N'515206','10.0.59.101',N'PT',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',304258),
(N'PUR',N'FX3570',N'506801','10.0.59.128',N'PURCHASE',N'HoatDong',1,NULL,673600),
(N'PW',N'FX3205',N'100041','10.0.59.72',N'Workshop PW',N'BaoPhe',0,NULL,NULL),
(N'PW',N'FX3205',N'100046','10.0.59.73',N'Workshop PW',N'BaoPhe',0,NULL,NULL),
(N'PW',N'FX3205',N'101102','10.0.59.140',N'OFFICE PW',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',62856),
(N'PW',N'FX3360s',N'100068','10.0.59.146',N'WORKSHOP PW',N'HoatDong',1,NULL,64679),
(N'QA',N'FX4070',N'148279','10.0.59.27',N'Office QA',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',156809),
(N'QA',N'FX3205',N'100957','10.0.59.133',N'QA (soi mau)',N'TamDung',0,NULL,NULL),
(N'QC',N'FX3105',N'109925','10.0.59.26',N'Workshop QC Đai',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',338319),
(N'QC',N'FX3360s',N'100210',NULL,N'QC vải',N'HoatDong',0,N'Ket noi theo Excel: Dây USB',338319),
(N'QC',N'FX3105',N'109917','10.0.59.56',N'Workshop QC vải(cài USB) báo phế',N'BaoPhe',0,NULL,NULL),
(N'QC',N'FX3205',N'100675','10.0.59.137',N'QC Vải',N'TamDung',0,NULL,NULL),
(N'QC',N'FX3360s',N'100116','10.0.59.137',N'QC Vải',N'HoatDong',1,NULL,189866),
(N'QC',N'FX3360s',N'100101','10.0.59.102',N'OFFICE QC Vải',N'HoatDong',1,NULL,20901),
(N'R&D',N'FX3205',N'101018','10.0.59.142',N'R&D',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',27326),
(N'SD',N'FX 3373',N'100099','10.0.59.110',N'OFFICE SD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',740016),
(N'SD',N'FX4830',N'012388','10.0.59.157',NULL,N'HoatDong',1,NULL,11805),
(N'SHD',N'FX3105',N'109909','10.0.59.66',N'Cắt nghệ an',N'BaoPhe',0,NULL,NULL),
(N'SHD',N'FX3360s',N'100115','10.0.59.97',N'IT dự phòng',N'TamDung',0,NULL,NULL),
(N'SHD',N'FX3530',N'011583','10.0.59.97',N'OFFICE SHD',N'HoatDong',1,NULL,120006),
(N'SS',N'FX3570',N'132026','10.0.59.96',N'Office SS+DH+LTB',N'HoatDong',1,NULL,566859),
(N'SS',N'FX3360s',N'100091','10.0.59.29',N'OFFICE SS tầng 1',N'HoatDong',1,NULL,52537),
(N'SS',N'FX3360s',N'100127',NULL,N'SS D1',N'HoatDong',0,NULL,36504),
(N'SS',N'FX3360s',N'100034','10.0.59.151',N'Office SS+DH ( giai đoạn 2)',N'HoatDong',1,NULL,183282),
(N'SS',N'FX3205',N'100052',NULL,N'SS LeeHing',N'HoatDong',0,N'Ket noi theo Excel: USB',234868),
(N'TL',N'FX3360s',N'100096','10.0.59.47',N'TL phòng vật lý',N'HoatDong',1,NULL,487004),
(N'WA',N'FX3105',N'109557','10.0.59.39',N'Office WA',N'TamDung',0,NULL,NULL),
(N'WA',N'FX4830',N'010025','10.0.59.145',N'WA xưởng H3',N'HoatDong',1,NULL,16385),
(N'WA',N'FX3360s',N'100094','10.0.59.147',N'wa h1 xưởng',N'HoatDong',1,NULL,47031),
(N'WA',N'FX3360s',N'100095','10.0.59.148',N'wa h1 xưởng',N'HoatDong',1,NULL,62000),
(N'WA',N'FX3360s',N'100097','10.0.59.149',N'wa h1 xưởng',N'HoatDong',1,NULL,28952),
(N'WD',N'FX3105',N'109552','10.0.59.30',N'Office  WD+CV',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',274792),
(N'WD',N'FX3105',N'108517','10.0.59.91',N'Workshop WD',N'BaoPhe',0,NULL,NULL),
(N'WD',N'FX3360s',N'100150','10.0.59.91',N'Workshop WD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',16601),
(N'WD',N'FX3105',N'108472','10.0.59.90',N'Workshop WD báo phế',N'BaoPhe',0,NULL,NULL),
(N'WD',N'FX3360S',N'100119','10.0.59.90',N'Workshop WD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',43712),
(N'WD',N'FX3360s',N'100106','10.0.59.154',N'Workshop WD Hưng Yên',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',54400),
(N'WD',N'FX3105',N'108410','10.0.59.95',N'Workshop WD',N'BaoPhe',0,NULL,NULL),
(N'WD',N'FX3105',N'108469','10.0.59.94',N'Workshop WD',N'TamDung',0,NULL,NULL),
(N'WD',N'FX3360s',N'100126','10.0.59.94',N'Workshop WD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',2558),
(N'WD',N'FX3105',N'108494','10.0.59.92',N'Workshop WD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',189512),
(N'WD',N'FX3105',N'108393','10.0.59.93',N'Workshop WD',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',200261),
(N'WH',N'FX3105',N'108415','10.0.59.11',N'Workshop WH PV B4',N'TamDung',0,NULL,NULL),
(N'WH',N'FX3105',N'108414','10.0.59.25',N'Workshop WH E1(e2)',N'TamDung',0,NULL,NULL),
(N'WH',N'FX3205',N'100068','10.0.59.12',N'Workshop WH(H3)',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',51138),
(N'WH',N'FX3105',N'108492','10.0.59.48',N'Workshop WH (semcope)',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',460144),
(N'WH',N'FX3105',N'109915','10.0.59.64',N'Workshop WH E2',N'TamDung',0,NULL,NULL),
(N'WH',N'FX3105',N'109923','10.0.59.50',N'Workshop WH Đai',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',374130),
(N'WH',N'FX3360s',N'100238',NULL,N'Kho Semcobp',N'HoatDong',0,N'Ket noi theo Excel: USB',NULL),
(N'WH',N'FX3105',N'109899','10.0.59.46',N'Office WH',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',320062),
(N'DH',N'FX3360s',N'100240','10.0.59.159',N'F8=>WH-SS-DH',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',NULL),
(N'WH',N'FX3360s',N'100107',NULL,N'Workshop WH (semcope)',N'HoatDong',0,N'Ket noi theo Excel: USB',44522),
(N'WV',N'FX3360s',N'100108',NULL,N'Workshop WH ngũ kim (WV mượn)',N'HoatDong',0,N'Ket noi theo Excel: USB',138115),
(N'WH',N'FX3360s',N'100109',NULL,N'Workshop WH (semcope)',N'TamDung',0,N'Ket noi theo Excel: USB',NULL),
(N'QA',N'FX3360s',N'100234','10.0.17.170',N'QA ĐAI OFFICE',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',1938),
(N'SD',N'FX3360s',N'100236','10.0.17.192',N'SD Team Nghệ An',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',1379),
(N'TL',N'FX3360s',N'100237','10.0.59.107',N'TL phòng giặt',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',47399),
(N'WD',N'FX3360s',N'100232',NULL,N'USB-WD Hưng Yên',N'HoatDong',0,NULL,1366),
(N'WD',N'FX3360s',N'100208',NULL,N'USB-WD Hưng Yên',N'TamDung',0,N'Chua lap dat (theo Excel)',NULL),
(N'WH',N'FX3360s',N'100065',NULL,N'WH (H2) chuyển về E2',N'HoatDong',0,N'Ket noi theo Excel: USB',12405),
(N'WH',N'FX3360s',N'100092',NULL,N'PQC chuyển kho E2',N'HoatDong',0,N'Ket noi theo Excel: 10.0.59.146/no',539939),
(N'WH',N'FX3360s',N'100152',NULL,N'Kho E2',N'HoatDong',0,NULL,94497),
(N'WH',N'FX3360s',N'100100',NULL,N'WH E2-F2',N'HoatDong',0,N'Ket noi theo Excel: USB',21366),
(N'WK',N'FX3105',N'109550','10.0.59.38',N'Office WK',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',354229),
(N'WK',N'FX3360s',N'100102',NULL,N'WK Nghệ An',N'HoatDong',0,N'Ket noi theo Excel: USB',43399),
(N'WK',N'FX3360s',N'100104','10.0.59.81',N'WK 2',N'HoatDong',1,NULL,211430),
(N'WK',N'FX3360s',N'100117',NULL,N'WK',N'HoatDong',0,N'Ket noi theo Excel: USB Yen mỹ',83530),
(N'WV',N'FX3105',N'109863','10.0.59.55',N'Office WV',N'TamDung',0,NULL,NULL),
(N'WV',N'FX3105',N'108493','10.0.59.58',N'Office WV phòng David',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',285834),
(N'WV',N'FX3105',N'109555','10.0.59.120',N'Workshop WV(USB)',N'TamDung',0,NULL,NULL),
(N'WV',N'6340',N'000115','10.0.59.116',N'Workshop WV',N'TamDung',0,NULL,NULL),
(N'WV',N'3205',N'100030','10.0.59.141',N'Office WV',N'BaoPhe',0,NULL,NULL),
(N'WV',N'FX4830',N'010017',NULL,N'Workshop WV',N'TamDung',0,N'Ket noi theo Excel: USB',NULL),
(N'WV',N'FX3205',N'101132','10.0.59.144',N'OFFICE WV(Hưng Yên)',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',160291),
(N'WV',N'FX3360s',N'100050','10.0.59.143',N'Phát Triển Đai',N'HoatDong',1,NULL,28875),
(N'WV',N'FX3360s',N'100099','10.0.59.116',N'WV',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',287620),
(N'WD',N'FX3360s',N'100148','10.0.59.95',N'WD  thay cho 3105:108410',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',24314),
(NULL,N'FX3205',N'100269','10.0.59.113',N'OFFICE Michael',N'HoatDong',0,N'May doi cu: khong co API /home/api, phai nhap chi so tay',4592)
;

DECLARE @soDong INT = (SELECT COUNT(*) FROM @nguon);
PRINT N'Dong doc tu Excel: ' + CAST(@soDong AS varchar);

INSERT INTO dbo.MayIn (bo_phan, model, serial, dia_chi_ip, vi_tri, trang_thai, theo_doi_tu_dong, ghi_chu)
SELECT n.bo_phan, n.model, n.serial, n.dia_chi_ip, n.vi_tri, n.trang_thai, n.theo_doi, n.ghi_chu
FROM @nguon n
WHERE NOT EXISTS (SELECT 1 FROM dbo.MayIn m WHERE m.model = n.model AND m.serial = n.serial);

PRINT N'Da them vao MayIn: ' + CAST(@@ROWCOUNT AS varchar);

-- Moc chi so ban dau (chi cho may con so trong Excel). Khong ghi de neu ngay do da co ban doc.
INSERT INTO dbo.MayIn_ChiSo (id_may_in, ngay_chot, thoi_diem_doc, counter_tong, nguon, ghi_chu)
SELECT m.id_may_in, '2026-09-18', GETDATE(), n.counter_tong, N'Excel',
       N'Moc ban dau lay tu cot moi nhat cua file Print out information2026.xlsx'
FROM @nguon n
JOIN dbo.MayIn m ON m.model = n.model AND m.serial = n.serial
WHERE n.counter_tong IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.MayIn_ChiSo c WHERE c.id_may_in = m.id_may_in AND c.ngay_chot = '2026-09-18');

PRINT N'Da them vao MayIn_ChiSo: ' + CAST(@@ROWCOUNT AS varchar);
GO

-- 4) Doi soat sau khi chay (ky vong: 111 may / 79 moc chi so / 24 may bat doc tu dong)
SELECT COUNT(*) AS tong_may_in FROM dbo.MayIn;
SELECT trang_thai, COUNT(*) AS so_luong FROM dbo.MayIn GROUP BY trang_thai;
SELECT COUNT(*) AS may_theo_doi_tu_dong FROM dbo.MayIn WHERE theo_doi_tu_dong = 1;
SELECT COUNT(*) AS moc_chi_so_ban_dau FROM dbo.MayIn_ChiSo WHERE nguon = N'Excel';