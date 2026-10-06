-- XOÁ CỨNG tài khoản NV2 (TK = thieuxk2, id_nguoi_dung = 6) — 01/10/2026
-- Người dùng đã quyết định xoá cả lịch sử truy cập + 3 phiếu đã huỷ do NV2 tạo (ngoại lệ có chủ đích với luật append-only LichSu*).
-- Chạy: sqlcmd ... -f 65001 -i user-xoa-thieuxk2-20261001.sql
-- Nên backup DB trước. Mọi dòng bị xoá được chép vào bảng bk_thieuxk2_* để rollback: user-xoa-thieuxk2-20261001-rollback.sql
--
-- Số dòng dự kiến (đo lúc soạn 01/10/2026):
--   User 1 · LichSuTruyCap 37 · User_Quyen 1 · User_BoPhan 51 · UserDevices 7
--   FormIT #720:  IT_DangKiSuDungDTBan_4 1, IT_CT_NguoiHoTro 1, LichSuFormIT 2
--   FormHR #145:  HR_XinRaNgoai_1 1, HR_CT_NguoiHoTro 1, HR_QuanLyDuyetB2 1 (+UyQuyen 2), LichSuFormHR 2
--   FormSHD #37:  SHD_DangKySuDungXeCongTac_1 1, SHD_CT_NguoiHoTro 1, SHD_QuanLyDuyetB2 1 (+UyQuyen 2), LichSuFormSHD 2

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRAN;

-- Chốt đúng đối tượng: sai thì dừng, không đụng gì
IF NOT EXISTS (SELECT 1 FROM [User] WHERE id_nguoi_dung = 6 AND TK = 'thieuxk2')
   OR (SELECT COUNT(*) FROM FormIT  WHERE 6 IN (idNguoiTao, idNguoiDuyet, idAdmin)) <> 1
   OR (SELECT COUNT(*) FROM FormHR  WHERE 6 IN (idNguoiTao, idNguoiDuyet, idAdmin)) <> 1
   OR (SELECT COUNT(*) FROM FormSHD WHERE 6 IN (idNguoiTao, idNguoiDuyet, idAdmin)) <> 1
   OR NOT EXISTS (SELECT 1 FROM FormIT  WHERE id = 720 AND idNguoiTao = 6)
   OR NOT EXISTS (SELECT 1 FROM FormHR  WHERE id = 145 AND idNguoiTao = 6)
   OR NOT EXISTS (SELECT 1 FROM FormSHD WHERE id = 37  AND idNguoiTao = 6)
BEGIN
    ROLLBACK;
    RAISERROR(N'Dữ liệu không còn khớp lúc soạn script — dừng, soạn lại.', 16, 1);
    RETURN;
END

IF OBJECT_ID('dbo.bk_thieuxk2_User') IS NOT NULL
BEGIN
    ROLLBACK;
    RAISERROR(N'Đã có bảng bk_thieuxk2_* — script đã chạy rồi, dừng.', 16, 1);
    RETURN;
END

-- ===== Sao lưu (chép trước khi xoá) =====
SELECT * INTO dbo.bk_thieuxk2_User                       FROM [User]                      WHERE id_nguoi_dung = 6;
SELECT * INTO dbo.bk_thieuxk2_LichSuTruyCap              FROM LichSuTruyCap               WHERE id_nguoi_dung = 6;
SELECT * INTO dbo.bk_thieuxk2_User_Quyen                 FROM User_Quyen                  WHERE id_nguoi_dung = 6;
SELECT * INTO dbo.bk_thieuxk2_User_BoPhan                FROM User_BoPhan                 WHERE id_nguoi_dung = 6;
SELECT * INTO dbo.bk_thieuxk2_UserDevices                FROM UserDevices                 WHERE id_nguoi_dung = 6;

SELECT * INTO dbo.bk_thieuxk2_FormIT                     FROM FormIT                      WHERE id = 720;
SELECT * INTO dbo.bk_thieuxk2_IT_DangKiSuDungDTBan_4     FROM IT_DangKiSuDungDTBan_4      WHERE id_FormIT = 720;
SELECT * INTO dbo.bk_thieuxk2_IT_CT_NguoiHoTro           FROM IT_CT_NguoiHoTro            WHERE idFormIT = 720;
SELECT * INTO dbo.bk_thieuxk2_LichSuFormIT               FROM LichSuFormIT                WHERE idFormIT = 720;

SELECT * INTO dbo.bk_thieuxk2_FormHR                     FROM FormHR                      WHERE id = 145;
SELECT * INTO dbo.bk_thieuxk2_HR_XinRaNgoai_1            FROM HR_XinRaNgoai_1             WHERE id_FormHR = 145;
SELECT * INTO dbo.bk_thieuxk2_HR_CT_NguoiHoTro           FROM HR_CT_NguoiHoTro            WHERE idFormHR = 145;
SELECT * INTO dbo.bk_thieuxk2_HR_QuanLyDuyetB2           FROM HR_QuanLyDuyetB2            WHERE idFormHR = 145;
SELECT * INTO dbo.bk_thieuxk2_HR_QuanLyDuyetB2_UyQuyen   FROM HR_QuanLyDuyetB2_UyQuyen    WHERE id_HR_QuanLyDuyetB2 IN (SELECT id FROM HR_QuanLyDuyetB2 WHERE idFormHR = 145);
SELECT * INTO dbo.bk_thieuxk2_LichSuFormHR               FROM LichSuFormHR                WHERE idFormHR = 145;

SELECT * INTO dbo.bk_thieuxk2_FormSHD                    FROM FormSHD                     WHERE id = 37;
SELECT * INTO dbo.bk_thieuxk2_SHD_DangKySuDungXeCongTac_1 FROM SHD_DangKySuDungXeCongTac_1 WHERE id_FormSHD = 37;
SELECT * INTO dbo.bk_thieuxk2_SHD_CT_NguoiHoTro          FROM SHD_CT_NguoiHoTro           WHERE idFormSHD = 37;
SELECT * INTO dbo.bk_thieuxk2_SHD_QuanLyDuyetB2          FROM SHD_QuanLyDuyetB2           WHERE idFormSHD = 37;
SELECT * INTO dbo.bk_thieuxk2_SHD_QuanLyDuyetB2_UyQuyen  FROM SHD_QuanLyDuyetB2_UyQuyen   WHERE id_SHD_QuanLyDuyetB2 IN (SELECT id FROM SHD_QuanLyDuyetB2 WHERE idFormSHD = 37);
SELECT * INTO dbo.bk_thieuxk2_LichSuFormSHD              FROM LichSuFormSHD               WHERE idFormSHD = 37;

-- ===== Xoá: con trước, cha sau =====
DELETE FROM HR_QuanLyDuyetB2_UyQuyen  WHERE id_HR_QuanLyDuyetB2  IN (SELECT id FROM HR_QuanLyDuyetB2  WHERE idFormHR  = 145);
DELETE FROM SHD_QuanLyDuyetB2_UyQuyen WHERE id_SHD_QuanLyDuyetB2 IN (SELECT id FROM SHD_QuanLyDuyetB2 WHERE idFormSHD = 37);

DELETE FROM IT_DangKiSuDungDTBan_4 WHERE id_FormIT = 720;
DELETE FROM IT_CT_NguoiHoTro       WHERE idFormIT  = 720;
DELETE FROM LichSuFormIT           WHERE idFormIT  = 720;
DELETE FROM FormIT                 WHERE id        = 720;

DELETE FROM HR_XinRaNgoai_1  WHERE id_FormHR = 145;
DELETE FROM HR_CT_NguoiHoTro WHERE idFormHR  = 145;
DELETE FROM HR_QuanLyDuyetB2 WHERE idFormHR  = 145;
DELETE FROM LichSuFormHR     WHERE idFormHR  = 145;
DELETE FROM FormHR           WHERE id        = 145;

DELETE FROM SHD_DangKySuDungXeCongTac_1 WHERE id_FormSHD = 37;
DELETE FROM SHD_CT_NguoiHoTro           WHERE idFormSHD  = 37;
DELETE FROM SHD_QuanLyDuyetB2           WHERE idFormSHD  = 37;
DELETE FROM LichSuFormSHD               WHERE idFormSHD  = 37;
DELETE FROM FormSHD                     WHERE id         = 37;

DELETE FROM LichSuTruyCap WHERE id_nguoi_dung = 6;
DELETE FROM User_Quyen    WHERE id_nguoi_dung = 6;
DELETE FROM User_BoPhan   WHERE id_nguoi_dung = 6;
DELETE FROM UserDevices   WHERE id_nguoi_dung = 6;
-- Các bảng còn lại trỏ tới User đang 0 dòng; FK CASCADE/SET_NULL tự lo nếu phát sinh trước lúc chạy.
-- User_HieuUngNen / KK_CCDC_MuonTra / TSCN_LichSuXacThucNguoiDung là NO_ACTION: có dòng mới thì DELETE dưới sẽ lỗi và cả transaction huỷ.
DELETE FROM [User] WHERE id_nguoi_dung = 6;

COMMIT;

-- Đối soát SAU: kỳ vọng 0 hết
SELECT (SELECT COUNT(*) FROM [User] WHERE id_nguoi_dung = 6)          AS [user],
       (SELECT COUNT(*) FROM LichSuTruyCap WHERE id_nguoi_dung = 6)   AS lich_su_truy_cap,
       (SELECT COUNT(*) FROM FormIT  WHERE id = 720)                  AS form_it,
       (SELECT COUNT(*) FROM FormHR  WHERE id = 145)                  AS form_hr,
       (SELECT COUNT(*) FROM FormSHD WHERE id = 37)                   AS form_shd;
