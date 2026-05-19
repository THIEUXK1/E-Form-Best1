using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DonSuDungDienThoai_12")]
public partial class HrDonSuDungDienThoai12
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    [StringLength(50)]
    public string? MaSoThe { get; set; }

    [StringLength(250)]
    public string? HoTen { get; set; }

    [StringLength(100)]
    public string? CapBac { get; set; }

    [StringLength(250)]
    public string? ChucVu { get; set; }

    [StringLength(250)]
    public string? BoPhan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianBatDauSuDung { get; set; }

    public string? LyDoSuDung { get; set; }

    public string? GhiChu { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonSuDungDienThoai12s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
