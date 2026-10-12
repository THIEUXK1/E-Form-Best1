/* ============================================================================
   Rollback cho kk-switch-pfvn-data-20261009.sql
   Xoá MỀM (ngay_xoa) đúng 19 thiết bị PFVN vừa nạp, KHÔNG xoá cứng (giữ truy vết + lịch sử).
   Chỉ chạm dòng do script nạp tạo ra (nguoi_tao = N'Khởi tạo danh sách PFVN') và còn hiệu lực.
   Lưu ý: file có tiếng Việt -> sqlcmd phải thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @IDCongTy int = (SELECT IDCongTy FROM dbo.KK_CongTy WHERE TenCongTy = N'PFVN');

UPDATE dbo.KK_Switch
SET ngay_xoa = GETDATE(),
    ly_do_xoa = N'Rollback nạp danh sách PFVN 2026-10-09'
WHERE IDCongTy = @IDCongTy
  AND nguoi_tao = N'Khởi tạo danh sách PFVN'
  AND ngay_xoa IS NULL
  AND dia_chi_ip IN (
      '10.0.198.199','10.0.193.1','10.0.198.200','10.0.198.201','10.0.198.202',
      '10.0.198.203','10.0.198.204','10.0.198.205','10.0.198.206','10.0.198.241',
      '10.0.198.240','10.0.198.15','10.0.198.208','10.0.198.216','10.0.198.209',
      '10.0.198.210','10.0.198.213','10.0.198.207','10.0.198.211'
  );

PRINT N'Đã rollback (xoá mềm) ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' switch PFVN.';
GO
