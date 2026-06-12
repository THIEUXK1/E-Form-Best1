using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormCongViec")]
public partial class FormCongViec
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

    [InverseProperty("IdFormCongViecNavigation")]
    public virtual ICollection<FormCongViecNguoiLienQuan> FormCongViecNguoiLienQuans { get; set; } = new List<FormCongViecNguoiLienQuan>();
}
