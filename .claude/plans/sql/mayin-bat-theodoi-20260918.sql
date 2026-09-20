-- Bat doc chi so tu dong cho nhung may THUC TE doc duoc (do lai 18/09/2026).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Cham DUY NHAT cot theo_doi_tu_dong cua bang MayIn.
-- Rollback: mayin-bat-theodoi-20260918-rollback.sql (tat lai dung nhung dong nay).
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-bat-theodoi-20260918.sql
--
-- LY DO: ban dau chi 27 may duoc bat vi chi tinh nhom co /home/api. Do lai bang 2 duong:
--   1) /home/api/billing-counter (may Apeos/ApeosPrint doi moi) - 7 may bi bo sot do lan quet dau
--      dat timeout 5 giay, may dang ngu tra loi cham hon the.
--   2) http://<ip>/prcnt.htm cua CentreWare (DocuPrint 3105/3205, ApeosPort-V/VI/VII) - 21 may.
--      Trang nay nhung so lieu ngay trong bien JavaScript var info=[...], khong can dang nhap.
--      Nhom nay KHONG co % muc, chi co dong ho dem.
-- Chi bat cho dong da doi chieu dung: nhom 1 khop serial may tra ve, nhom 2 chi bat khi IP do
-- duy nhat mot dong HoatDong trong danh muc (tranh 2 dong cung doc mot may).

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @bat TABLE (id_may_in INT PRIMARY KEY);

-- Nhom 1: doc qua /home/api (khop serial)
INSERT INTO @bat (id_may_in) VALUES
(110),  /* 10.0.59.95 - FX3360s 100148 */
(19),  /* 10.0.59.156 - FX4570 134447 */
(67),  /* 10.0.59.91 - FX3360s 100150 */
(69),  /* 10.0.59.90 - FX3360S 100119 */
(84),  /* 10.0.59.159 - FX3360s 100240 */
(89),  /* 10.0.17.192 - FX3360s 100236 */
(90);   /* 10.0.59.107 - FX3360s 100237 */

-- Nhom 2: doc qua trang CentreWare doi cu
INSERT INTO @bat (id_may_in) VALUES
(1),  /* 10.0.59.61 - FX4070 148986 */
(102),  /* 10.0.59.58 - FX3105 108493 */
(111),  /* 10.0.59.113 - FX3205 100269 */
(13),  /* 10.0.59.111 - FX3373 108659 */
(17),  /* 10.0.59.54 - FX3105 109924 */
(2),  /* 10.0.59.45 - FX3105 109553 */
(22),  /* 10.0.59.130 - FX3205 100668 */
(25),  /* 10.0.59.62 - FX3105 108520 */
(30),  /* 10.0.59.150 - FX3205 100037 */
(32),  /* 10.0.59.28 - FX3370 504725 */
(34),  /* 10.0.59.101 - FX3371 515206 */
(38),  /* 10.0.59.140 - FX3205 101102 */
(40),  /* 10.0.59.27 - FX4070 148279 */
(42),  /* 10.0.59.26 - FX3105 109925 */
(49),  /* 10.0.59.110 - FX 3373 100099 */
(65),  /* 10.0.59.30 - FX3105 109552 */
(74),  /* 10.0.59.92 - FX3105 108494 */
(75),  /* 10.0.59.93 - FX3105 108393 */
(83),  /* 10.0.59.46 - FX3105 109899 */
(9),  /* 10.0.59.51 - Docuprint 3105 108470 */
(97);   /* 10.0.59.38 - FX3105 109550 */

UPDATE m
SET    m.theo_doi_tu_dong = 1,
       m.ngay_cap_nhat = GETDATE()
FROM   dbo.MayIn m
JOIN   @bat b ON b.id_may_in = m.id_may_in
WHERE  m.theo_doi_tu_dong = 0;

PRINT N'So may vua bat doc tu dong: ' + CAST(@@ROWCOUNT AS varchar);
GO

-- Doi soat (ky vong: 55 may bat doc tu dong)
SELECT COUNT(*) AS may_doc_tu_dong FROM dbo.MayIn WHERE theo_doi_tu_dong = 1;