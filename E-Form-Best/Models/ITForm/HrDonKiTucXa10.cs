using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DonKiTucXa_10")]
public partial class HrDonKiTucXa10
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    [StringLength(50)]
    public string? MaNhanVien { get; set; }

    [StringLength(250)]
    public string? HoTen { get; set; }

    [StringLength(250)]
    public string? PhongBan { get; set; }

    [StringLength(250)]
    public string? ChucVu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianNhanPhong { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianTraPhong { get; set; }

    [StringLength(100)]
    public string? LoaiPhong { get; set; }

    public string? GhiChu { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonKiTucXa10s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
