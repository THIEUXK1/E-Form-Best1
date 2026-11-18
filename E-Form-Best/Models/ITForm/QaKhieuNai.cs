using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Khiếu nại khách hàng (CUSTOMER COMPLAINT SUMMARY) — 1 dòng = 1 dòng sheet "detail"; xoá mềm (NgayXoa).
// Script: .claude/plans/sql/qa-khieu-nai-20261118.sql
[Table("QA_KhieuNai")]
public partial class QaKhieuNai
{
    [Key]
    [Column("id_khieu_nai")]
    public int IdKhieuNai { get; set; }

    [Column("ngay_khieu_nai", TypeName = "date")]
    public DateTime? NgayKhieuNai { get; set; }

    [Column("nha_may")] [StringLength(100)] public string? NhaMay { get; set; }
    [Column("khach_cuoi")] [StringLength(100)] public string? KhachCuoi { get; set; }
    [Column("quoc_gia")] [StringLength(50)] public string? QuocGia { get; set; }
    [Column("po")] [StringLength(100)] public string? Po { get; set; }
    [Column("ma_lot")] [StringLength(50)] public string? MaLot { get; set; }
    [Column("ma_hang")] [StringLength(100)] public string? MaHang { get; set; }
    [Column("kho_rong")] [StringLength(100)] public string? KhoRong { get; set; }
    [Column("mau_khach")] [StringLength(200)] public string? MauKhach { get; set; }
    [Column("ma_mau_noi_bo")] [StringLength(100)] public string? MaMauNoiBo { get; set; }

    [Column("sl_xuat", TypeName = "decimal(18,2)")] public decimal? SlXuat { get; set; }
    [Column("sl_khieu_nai", TypeName = "decimal(18,2)")] public decimal? SlKhieuNai { get; set; }

    [Column("chung_loai")] [StringLength(100)] public string? ChungLoai { get; set; }
    [Column("van_de")] [StringLength(2000)] public string? VanDe { get; set; }
    [Column("qa_boc_kiem")] [StringLength(2000)] public string? QaBocKiem { get; set; }
    [Column("y_kien_bp")] [StringLength(2000)] public string? YKienBp { get; set; }
    [Column("y_kien_khach")] [StringLength(2000)] public string? YKienKhach { get; set; }

    [Column("sl_bu", TypeName = "decimal(18,2)")] public decimal? SlBu { get; set; }
    [Column("ti_le_bu", TypeName = "decimal(9,2)")] public decimal? TiLeBu { get; set; }
    [Column("sl_ton_kho", TypeName = "decimal(18,2)")] public decimal? SlTonKho { get; set; }
    [Column("foc", TypeName = "decimal(18,2)")] public decimal? Foc { get; set; }

    [Column("ngay_xuat_tp", TypeName = "date")] public DateTime? NgayXuatTp { get; set; }
    [Column("ngay_xac_nhan", TypeName = "date")] public DateTime? NgayXacNhan { get; set; }

    [Column("car_no")] [StringLength(100)] public string? CarNo { get; set; }
    [Column("ton_that_cuoi", TypeName = "decimal(18,2)")] public decimal? TonThatCuoi { get; set; }
    [Column("ghi_chu_tra_doi")] [StringLength(1000)] public string? GhiChuTraDoi { get; set; }
    [Column("ma_nv_qa")] [StringLength(50)] public string? MaNvQa { get; set; }
    // Script: .claude/plans/sql/qa-khieu-nai-qa-phu-trach-20261118.sql
    [Column("qa_phu_trach")] [StringLength(200)] public string? QaPhuTrach { get; set; }

    // pending | close (CHECK ở DB)
    [Column("trang_thai")] [StringLength(20)] public string TrangThai { get; set; } = "pending";
    [Column("ghi_chu")] [StringLength(2000)] public string? GhiChu { get; set; }

    [Column("dien_tu_dong_luc", TypeName = "datetime")] public DateTime? DienTuDongLuc { get; set; }
    // Chống tạo trùng: GUID trình duyệt (thêm dòng) hoặc "xlsx:" + SHA-256 (nhập Excel); unique lọc NOT NULL
    [Column("khoa_tao")] [StringLength(80)] public string? KhoaTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")] public DateTime NgayTao { get; set; }
    [Column("nguoi_tao")] [StringLength(100)] public string? NguoiTao { get; set; }
    [Column("ngay_sua", TypeName = "datetime")] public DateTime? NgaySua { get; set; }
    [Column("nguoi_sua")] [StringLength(100)] public string? NguoiSua { get; set; }
    [Column("ngay_xoa", TypeName = "datetime")] public DateTime? NgayXoa { get; set; }
    [Column("nguoi_xoa")] [StringLength(100)] public string? NguoiXoa { get; set; }
}

// Nhật ký khiếu nại — append-only. HanhDong: Tao | NhapExcel | Sua | DienTuDong | Xoa
[Table("QA_KhieuNaiLichSu")]
public partial class QaKhieuNaiLichSu
{
    [Key]
    [Column("id_lich_su")]
    public int IdLichSu { get; set; }

    [Column("id_khieu_nai")] public int IdKhieuNai { get; set; }
    [Column("ngay", TypeName = "datetime")] public DateTime Ngay { get; set; }
    [Column("nguoi")] [StringLength(100)] public string? Nguoi { get; set; }
    [Column("hanh_dong")] [StringLength(30)] public string HanhDong { get; set; } = null!;
    [Column("truong")] [StringLength(50)] public string? Truong { get; set; }
    [Column("gia_tri_cu")] [StringLength(2000)] public string? GiaTriCu { get; set; }
    [Column("gia_tri_moi")] [StringLength(2000)] public string? GiaTriMoi { get; set; }
}
