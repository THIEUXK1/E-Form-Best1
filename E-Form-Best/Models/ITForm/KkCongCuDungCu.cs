using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Công cụ dụng cụ (CCDC) của bộ phận IT: khoan, tô vít, thang, cáp mạng, máy hàn...
/// Tách khỏi <see cref="KkThietBi"/> vì CCDC quản theo số lượng/đơn vị tính chứ không theo
/// từng máy có serial. Dùng soft-delete cùng quy ước với KK_ThietBi: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_CongCuDungCu")]
public partial class KkCongCuDungCu
{
    [Key]
    [Column("id_ccdc")]
    public int IdCcdc { get; set; }

    [Column("ma_ccdc")]
    [StringLength(100)]
    public string? MaCcdc { get; set; }

    [Column("ten_ccdc")]
    [StringLength(255)]
    public string TenCcdc { get; set; } = null!;

    [Column("loai_ccdc")]
    [StringLength(255)]
    public string? LoaiCcdc { get; set; }

    [Column("don_vi_tinh")]
    [StringLength(50)]
    public string? DonViTinh { get; set; }

    [Column("so_luong")]
    public int SoLuong { get; set; }

    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    [Column("nguoi_quan_ly")]
    [StringLength(255)]
    public string? NguoiQuanLy { get; set; }

    [Column("vi_tri")]
    [StringLength(500)]
    public string? ViTri { get; set; }

    [Column("tinh_trang")]
    [StringLength(100)]
    public string? TinhTrang { get; set; }

    [Column("ngay_mua")]
    public DateOnly? NgayMua { get; set; }

    [Column("gia_tri", TypeName = "decimal(18, 2)")]
    public decimal? GiaTri { get; set; }

    [Column("han_bao_hanh")]
    public DateOnly? HanBaoHanh { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    [Column("nguoi_tao")]
    [StringLength(255)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("ly_do_xoa")]
    [StringLength(500)]
    public string? LyDoXoa { get; set; }

    [ForeignKey("IdcongTy")]
    public virtual KkCongTy? IdcongTyNavigation { get; set; }

    [ForeignKey("IdboPhan")]
    public virtual KkBoPhan? IdboPhanNavigation { get; set; }
}
