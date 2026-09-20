-- Rollback cho mayin-bosung-tu-printsrv-20260918.sql.
-- Xoa 2 may moi them va tra may 100108 ve trang thai cu (khong co IP, khong doc tu dong).
-- Chi so da thu thap cua 2 may moi (neu co) bi xoa theo.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DELETE c
FROM   dbo.MayIn_ChiSo c
JOIN   dbo.MayIn m ON m.id_may_in = c.id_may_in
WHERE  m.model = N'FX3360s' AND m.serial IN (N'100121', N'100239');

PRINT N'Da xoa chi so cua may moi: ' + CAST(@@ROWCOUNT AS varchar);

DELETE FROM dbo.MayIn WHERE model = N'FX3360s' AND serial IN (N'100121', N'100239');
PRINT N'Da xoa may moi: ' + CAST(@@ROWCOUNT AS varchar);

UPDATE dbo.MayIn
SET    dia_chi_ip       = NULL,
       ten_hang_doi     = NULL,
       theo_doi_tu_dong = 0,
       ghi_chu          = N'Ket noi theo Excel: USB',
       ngay_cap_nhat    = GETDATE()
WHERE  model = N'FX3360s' AND serial = N'100108';

PRINT N'Da tra may 100108 ve trang thai cu: ' + CAST(@@ROWCOUNT AS varchar);
