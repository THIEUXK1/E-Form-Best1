using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// 1 dòng NH-MES đã bỏ vào thùng QA — CHỤP LẠI dữ liệu lúc bỏ vào vì MES không có id dòng.
// DauVanTay = SHA-256 của JSON dòng MES, đếm theo nó để 1 dòng MES không vào thùng 2 lần.
// Script: .claude/plans/sql/qa-thung-20261112.sql
[Table("QA_ThungDong")]
public partial class QaThungDong
{
    [Key]
    [Column("id_dong")]
    public int IdDong { get; set; }

    [Column("id_thung")]
    public int IdThung { get; set; }

    [Column("dau_van_tay")]
    [StringLength(64)]
    public string DauVanTay { get; set; } = null!;

    [Column("ngay_them", TypeName = "datetime")]
    public DateTime NgayThem { get; set; }

    [Column("nguoi_them")]
    [StringLength(100)]
    public string? NguoiThem { get; set; }

    [Column("ma_phoi_mau")]
    [StringLength(50)]
    public string? MaPhoiMau { get; set; }

    [Column("ma_khach_cuoi")]
    [StringLength(50)]
    public string? MaKhachCuoi { get; set; }

    [Column("ma_khach_hang")]
    [StringLength(50)]
    public string? MaKhachHang { get; set; }

    [Column("ma_hang")]
    [StringLength(100)]
    public string? MaHang { get; set; }

    [Column("ma_mau")]
    [StringLength(100)]
    public string? MaMau { get; set; }

    [Column("sl_don_hang", TypeName = "decimal(18, 3)")]
    public decimal? SlDonHang { get; set; }

    [Column("sl_dong_goi", TypeName = "decimal(18, 3)")]
    public decimal? SlDongGoi { get; set; }

    [Column("kl_dong_goi", TypeName = "decimal(18, 3)")]
    public decimal? KlDongGoi { get; set; }

    [Column("so_hop", TypeName = "decimal(18, 3)")]
    public decimal? SoHop { get; set; }

    [Column("loai")]
    [StringLength(20)]
    public string? Loai { get; set; }

    [Column("mo_ta_bat_thuong")]
    [StringLength(500)]
    public string? MoTaBatThuong { get; set; }

    [Column("y_kien_qc")]
    [StringLength(500)]
    public string? YKienQc { get; set; }

    [Column("cong_doan")]
    [StringLength(50)]
    public string? CongDoan { get; set; }

    [Column("so_phieu_nhuom")]
    [StringLength(50)]
    public string? SoPhieuNhuom { get; set; }

    [Column("ngay_nhan_hang")]
    [StringLength(30)]
    public string? NgayNhanHang { get; set; }

    [Column("ngay_dong_goi")]
    [StringLength(30)]
    public string? NgayDongGoi { get; set; }

    [Column("so_phieu_dong_goi")]
    [StringLength(50)]
    public string? SoPhieuDongGoi { get; set; }

    [Column("ma_nv_dong_goi")]
    [StringLength(50)]
    public string? MaNvDongGoi { get; set; }

    [Column("ghi_chu_mes")]
    [StringLength(500)]
    public string? GhiChuMes { get; set; }

    // Nguyên JSON dòng MES lúc bỏ vào thùng
    [Column("du_lieu_mes")]
    public string DuLieuMes { get; set; } = null!;

    // Bỏ ra khỏi thùng = xoá mềm (giữ để truy vết); mọi truy vấn dòng đang ở thùng lọc NgayBo == null.
    // Script: .claude/plans/sql/qa-thung-bo-dong-20261112.sql
    [Column("ngay_bo", TypeName = "datetime")]
    public DateTime? NgayBo { get; set; }

    [Column("nguoi_bo")]
    [StringLength(100)]
    public string? NguoiBo { get; set; }

    // Số bản nhập tay ở chi tiết thùng (null = chưa nhập). Script: qa-thung-so-ban-20261112.sql
    [Column("so_ban")]
    public int? SoBan { get; set; }

    [ForeignKey(nameof(IdThung))]
    public virtual QaThung Thung { get; set; } = null!;
}
