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

    [StringLength(500)]
    public string TenMay { get; set; } = null!;

    [StringLength(500)]
    public string? SeriMay { get; set; }

    [StringLength(1000)]
    public string? DongMay { get; set; }

    public string? HeDieuHanh { get; set; }

    [StringLength(250)]
    public string? KienTruc { get; set; }

    public string? PhienBanNet { get; set; }

    [Column("SoLoiCPU")]
    public int? SoLoiCpu { get; set; }

    [StringLength(500)]
    public string? TenNguoiDungHeThong { get; set; }

    [StringLength(1000)]
    public string? ThuMucHeThong { get; set; }

    [StringLength(500)]
    public string? ThoiGianHoatDong { get; set; }

    [StringLength(500)]
    public string? RamKhaDung { get; set; }

    public string? ThongTinManHinhNgoai { get; set; }

    public string? ThongTinOffice { get; set; }

    public string? BanQuyenWin { get; set; }

    public string? BanQuyenOffice { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    public int? IdNguoiDung { get; set; }

    [StringLength(500)]
    public string? LoaiThietBi { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("TscnThongTinMays")]
    public virtual User? IdNguoiDungNavigation { get; set; }

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietMacWifi> TscnChiTietMacWifis { get; set; } = new List<TscnChiTietMacWifi>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietManHinh> TscnChiTietManHinhs { get; set; } = new List<TscnChiTietManHinh>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietOcung> TscnChiTietOcungs { get; set; } = new List<TscnChiTietOcung>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnChiTietRam> TscnChiTietRams { get; set; } = new List<TscnChiTietRam>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnLichSuThayDoi> TscnLichSuThayDois { get; set; } = new List<TscnLichSuThayDoi>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnLichSuXacThucAdmin> TscnLichSuXacThucAdmins { get; set; } = new List<TscnLichSuXacThucAdmin>();

    [InverseProperty("IdMayNavigation")]
    public virtual ICollection<TscnLichSuXacThucNguoiDung> TscnLichSuXacThucNguoiDungs { get; set; } = new List<TscnLichSuXacThucNguoiDung>();
}
