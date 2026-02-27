using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_MangHangHoaRaCong_2")]
public partial class HrMangHangHoaRaCong2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [StringLength(255)]
    public string? TenCong { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianRa { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianVao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    [StringLength(255)]
    public string? MoTa { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrMangHangHoaRaCong2s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
