using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormSHD")]
public partial class FormShd
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenForm { get; set; }

    public DateOnly? Ngay { get; set; }

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

    [StringLength(250)]
    public string? BoPhan { get; set; }

    [Column("danhmuc")]
    [StringLength(250)]
    public string? Danhmuc { get; set; }

    [Column("ten_cong_ty")]
    [StringLength(250)]
    public string? TenCongTy { get; set; }

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<BinhLuanFormShd> BinhLuanFormShds { get; set; } = new List<BinhLuanFormShd>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<LichSuFormShd> LichSuFormShds { get; set; } = new List<LichSuFormShd>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<ShdCtNguoiHoTro> ShdCtNguoiHoTros { get; set; } = new List<ShdCtNguoiHoTro>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<ShdDangKySuDungXeCongTac1> ShdDangKySuDungXeCongTac1s { get; set; } = new List<ShdDangKySuDungXeCongTac1>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<ShdDangKySuDungXeDaily2> ShdDangKySuDungXeDaily2s { get; set; } = new List<ShdDangKySuDungXeDaily2>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<ShdNguoiXacNhan> ShdNguoiXacNhans { get; set; } = new List<ShdNguoiXacNhan>();

    [InverseProperty("IdFormShdNavigation")]
    public virtual ICollection<ShdQuanLyDuyetB2> ShdQuanLyDuyetB2s { get; set; } = new List<ShdQuanLyDuyetB2>();
}
