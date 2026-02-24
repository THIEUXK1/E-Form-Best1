using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormIT")]
public partial class FormIt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenForm { get; set; }

    public DateOnly? Ngay { get; set; }

    [StringLength(100)]
    public string? BoPhan { get; set; }

    [StringLength(50)]
    public string? SoNhanVien { get; set; }

    [Column("TenNguoiNV")]
    [StringLength(255)]
    public string? TenNguoiNv { get; set; }

    [StringLength(100)]
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
    [StringLength(50)]
    public string? IdForm { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [Column("danhmuc")]
    [StringLength(255)]
    public string? Danhmuc { get; set; }

    [Column("ten_cong_ty")]
    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [InverseProperty("IdFormNavigation")]
    public virtual ICollection<BinhLuanFormIt> BinhLuanFormIts { get; set; } = new List<BinhLuanFormIt>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<DanhGium> DanhGia { get; set; } = new List<DanhGium>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<ItCtNguoiHoTro> ItCtNguoiHoTros { get; set; } = new List<ItCtNguoiHoTro>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<ItDangKiSuDungDtban4> ItDangKiSuDungDtban4s { get; set; } = new List<ItDangKiSuDungDtban4>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<ItDangKiSuDungWifi3> ItDangKiSuDungWifi3s { get; set; } = new List<ItDangKiSuDungWifi3>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<ItMail1> ItMail1s { get; set; } = new List<ItMail1>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<ItOrderIt2> ItOrderIt2s { get; set; } = new List<ItOrderIt2>();

    [InverseProperty("IdFormItNavigation")]
    public virtual ICollection<LichSu> LichSus { get; set; } = new List<LichSu>();
}
