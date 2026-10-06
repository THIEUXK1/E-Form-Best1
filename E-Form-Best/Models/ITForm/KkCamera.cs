using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Camera giám sát (IP camera) do IT quản lý. Quản theo từng con có IP + đầu ghi/kênh nên tách
/// khỏi <see cref="KkCongCuDungCu"/>. Dùng soft-delete cùng quy ước KK_ThietBi: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_Camera")]
public partial class KkCamera
{
    [Key]
    [Column("id_camera")]
    public int IdCamera { get; set; }

    [Column("ma_camera")]
    [StringLength(100)]
    public string? MaCamera { get; set; }

    [Column("ten_camera")]
    [StringLength(255)]
    public string TenCamera { get; set; } = null!;

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("hang_san_xuat")]
    [StringLength(100)]
    public string? HangSanXuat { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("serial")]
    [StringLength(100)]
    public string? Serial { get; set; }

    [Column("dau_ghi")]
    [StringLength(255)]
    public string? DauGhi { get; set; }

    [Column("kenh")]
    public int? Kenh { get; set; }

    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    [Column("vi_tri")]
    [StringLength(500)]
    public string? ViTri { get; set; }

    [Column("tinh_trang")]
    [StringLength(100)]
    public string? TinhTrang { get; set; }

    [Column("ngay_lap_dat")]
    public DateOnly? NgayLapDat { get; set; }

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
