/*
  Mục đích : Thêm 20 laptop HP 240R G9 tồn kho phòng IT (VN-ITV031 -> VN-ITV050)
             vào bảng KK_ThietBi, theo đúng khuôn 30 máy VN-ITV001..030 đã có;
             đồng thời cả lô 50 máy: can_cai_office = 1 và bộ phận VN-IT / công ty BPVN.
  Nguồn    : Downloads/Sửa chữa 6/thietbi(1).xlsx (50 dòng, 30 dòng đầu đã tồn tại).
  Ngày     : 2026-09-17
  Chạy     : sqlcmd -S <ip> -d ITForm -U <user> -P <pass> -C -f 65001 -i kk-them-20-laptop-vn-itv031-050-20260917.sql
  An toàn  : idempotent — chạy lại nhiều lần không sinh bản ghi trùng (chốt theo seribacode).
             Các khối UPDATE chỉ chạm can_cai_office/ngay_tra_loi_office (26 máy) và IDCongTy/IDBoPhan (6 máy).
             Rollback: file *-rollback.sql cùng thư mục (khôi phục đúng giá trị cũ từng dòng).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @moi TABLE (ten_may nvarchar(255), serial nvarchar(255));
INSERT INTO @moi (ten_may, serial) VALUES
    ('VN-ITV031','5CG5270T97'),
    ('VN-ITV032','5CG5270T2W'),
    ('VN-ITV033','5CG5270T24'),
    ('VN-ITV034','5CG5270T2P'),
    ('VN-ITV035','5CG5270T5K'),
    ('VN-ITV036','5CG5270T15'),
    ('VN-ITV037','5CG5270T1S'),
    ('VN-ITV038','5CG5270T0P'),
    ('VN-ITV039','5CG5270T02'),
    ('VN-ITV040','5CG5270T9X'),
    ('VN-ITV041','5CG5270T65'),
    ('VN-ITV042','5CG5270T09'),
    ('VN-ITV043','5CG5270T0S'),
    ('VN-ITV044','5CG5270T5Q'),
    ('VN-ITV045','5CG5270T98'),
    ('VN-ITV046','5CG5270T96'),
    ('VN-ITV047','5CG5270T66'),
    ('VN-ITV048','5CG5270T5G'),
    ('VN-ITV049','5CG5270T9L'),
    ('VN-ITV050','5CG5270T8Y');

BEGIN TRAN;

DECLARE @now datetime = GETDATE();

INSERT INTO KK_ThietBi
(
    ten_vi_tri, ten_may_tinh, ten_dang_nhap, id_nguoi_dung,
    IDCongTy, IDBoPhan, loai_thiet_bi, id_trang_thai,
    quy_cach, seribacode, ghi_chu, can_cai_office, ngay_tra_loi_office,
    ngay_tao, ngay_cap_nhat, thoi_gian_check
)
SELECT
    N'TẦNG 2 -PHÒNG IT',              -- giống hệt 16 máy tồn kho VN-ITV001..030
    m.ten_may,
    N'',                              -- chưa bàn giao cho ai -> chuỗi rỗng như các máy cũ
    449,                              -- tài khoản phòng IT đang giữ máy
    1,                                -- IDCongTy = BPVN (KK_CongTy.TenCongTy)
    26,                               -- IDBoPhan = VN-IT (KK_BoPhan.TenBoPhan, thuộc BPVN)
    N'Laptop',
    1,                                -- KK_TrangThai 1 = Đang hoạt động
    N'HP - HP 240R 14 inch G9 Notebook PC',
    m.serial,
    N'Phòng IT để cho mượn về nhà làm',
    1, @now,                          -- cả lô 50 máy đều CÓ cài Office (chốt ngày 2026-09-17)
    @now, @now, @now                  -- app cũng set cả 3 mốc khi thêm mới (ITFormController.cs:7433)
FROM @moi m
WHERE NOT EXISTS (                    -- chốt idempotency: đã có serial này (kể cả đã xoá mềm) thì bỏ qua
    SELECT 1 FROM KK_ThietBi t WHERE t.seribacode = m.serial
)
AND NOT EXISTS (
    SELECT 1 FROM KK_ThietBi t WHERE t.ten_may_tinh = m.ten_may AND t.NgayXoa IS NULL
);

PRINT N'Số dòng đã thêm: ' + CAST(@@ROWCOUNT AS nvarchar(10));


/* --- Đồng bộ 30 máy cũ VN-ITV001..030: cả lô 50 máy đều CÓ cài Office --- */
UPDATE KK_ThietBi
SET can_cai_office      = 1,
    ngay_tra_loi_office = @now,
    ngay_cap_nhat       = @now
WHERE NgayXoa IS NULL
  AND ten_may_tinh BETWEEN 'VN-ITV001' AND 'VN-ITV030'
  AND (can_cai_office IS NULL OR can_cai_office = 0);   -- idempotent: máy đã = 1 thì không đụng

PRINT N'Số máy cũ đổi sang có cài Office: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* --- Kéo 6 máy cũ đang đứng tên bộ phận khác về VN-IT / BPVN ---
   VN-ITV003 (VN-GL), VN-ITV005 + VN-ITV009 (VN-AC), VN-ITV008 + VN-ITV020 + VN-ITV024 (VN-SD).
   CHỈ đổi công ty/bộ phận; giữ nguyên ten_vi_tri và id_nguoi_dung để không mất vết ai đang giữ máy. */
UPDATE KK_ThietBi
SET IDCongTy      = 1,      -- BPVN
    IDBoPhan      = 26,     -- VN-IT
    ngay_cap_nhat = @now
WHERE NgayXoa IS NULL
  AND ten_may_tinh BETWEEN 'VN-ITV001' AND 'VN-ITV050'
  AND (IDCongTy <> 1 OR IDBoPhan <> 26 OR IDCongTy IS NULL OR IDBoPhan IS NULL);

PRINT N'Số máy cũ kéo về VN-IT/BPVN: ' + CAST(@@ROWCOUNT AS nvarchar(10));
COMMIT;

-- Đối soát sau khi chạy
SELECT t.id_thiet_bi, t.ten_may_tinh, t.seribacode, t.ten_vi_tri, b.TenBoPhan, t.id_trang_thai, t.can_cai_office, t.ngay_tao
FROM KK_ThietBi t LEFT JOIN KK_BoPhan b ON b.IDBoPhan = t.IDBoPhan
WHERE t.NgayXoa IS NULL AND t.ten_may_tinh BETWEEN 'VN-ITV001' AND 'VN-ITV050'
ORDER BY t.ten_may_tinh;
