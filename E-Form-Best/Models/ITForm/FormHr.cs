using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormHR")]
public partial class FormHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenForm { get; set; }

    public DateOnly? Ngay { get; set; }

    [StringLength(255)]
    public string? PhanBo { get; set; }

    [StringLength(50)]
    public string? SoNhanVien { get; set; }

    [Column("TenNguoiNV")]
    [StringLength(255)]
    public string? TenNguoiNv { get; set; }

    [StringLength(255)]
    public string? ViTri { get; set; }

    [Column("idNguoiTao")]
    public int? IdNguoiTao { get; set; }

    [StringLength(255)]
    public string? TenNguoiTao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiTao { get; set; }

    [Column("idNguoiDuyet")]
    public int? IdNguoiDuyet { get; set; }

    [StringLength(255)]
    public string? TenNguoiDuyet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiDuyet { get; set; }

    [Column("idAdmin")]
    public int? IdAdmin { get; set; }

    [StringLength(255)]
    public string? TenAdmin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeAdmin { get; set; }

    [Column("idForm")]
    [StringLength(250)]
    public string? IdForm { get; set; }

    [StringLength(250)]
    public string? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtDangKySuDungXeCongTac3> CtDangKySuDungXeCongTac3s { get; set; } = new List<CtDangKySuDungXeCongTac3>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtDangKySuDungXeDaily4> CtDangKySuDungXeDaily4s { get; set; } = new List<CtDangKySuDungXeDaily4>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtDoiCaLam8> CtDoiCaLam8s { get; set; } = new List<CtDoiCaLam8>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtDonTiepKhac5> CtDonTiepKhac5s { get; set; } = new List<CtDonTiepKhac5>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtHoTroTienDienThoai7> CtHoTroTienDienThoai7s { get; set; } = new List<CtHoTroTienDienThoai7>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtMangHangHoaRaCong2> CtMangHangHoaRaCong2s { get; set; } = new List<CtMangHangHoaRaCong2>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtNhaThauQuaCong6> CtNhaThauQuaCong6s { get; set; } = new List<CtNhaThauQuaCong6>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<CtXinRaNgoai1> CtXinRaNgoai1s { get; set; } = new List<CtXinRaNgoai1>();
}
