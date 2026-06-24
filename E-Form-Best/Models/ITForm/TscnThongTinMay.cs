using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_ThongTinMay")]
public partial class TscnThongTinMay
{
    [Key]
    public int IdMay { get; set; }

    [StringLength(150)]
    public string TenMay { get; set; } = null!;

    [StringLength(150)]
    public string? SeriMay { get; set; }

    [StringLength(250)]
    public string? DongMay { get; set; }

    [StringLength(250)]
    public string? HeDieuHanh { get; set; }

    [StringLength(50)]
    public string? KienTruc { get; set; }

    [StringLength(150)]
    public string? PhienBanNet { get; set; }

    [Column("SoLoiCPU")]
    public int? SoLoiCpu { get; set; }

    [StringLength(150)]
    public string? TenNguoiDungHeThong { get; set; }

    [StringLength(250)]
    public string? ThuMucHeThong { get; set; }

    [StringLength(100)]
    public string? ThoiGianHoatDong { get; set; }

    [StringLength(100)]
    public string? RamKhaDung { get; set; }

    [StringLength(250)]
    public string? ThongTinManHinhNgoai { get; set; }

    [StringLength(500)]
    public string? ThongTinOffice { get; set; }

    [StringLength(250)]
    public string? BanQuyenWin { get; set; }

    [StringLength(250)]
    public string? BanQuyenOffice { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietMacWifi> TscnChiTietMacWifis { get; set; } = new List<TscnChiTietMacWifi>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietManHinh> TscnChiTietManHinhs { get; set; } = new List<TscnChiTietManHinh>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietOcung> TscnChiTietOcungs { get; set; } = new List<TscnChiTietOcung>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietRam> TscnChiTietRams { get; set; } = new List<TscnChiTietRam>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnLichSuXacThucAdmin> TscnLichSuXacThucAdmins { get; set; } = new List<TscnLichSuXacThucAdmin>();
}
