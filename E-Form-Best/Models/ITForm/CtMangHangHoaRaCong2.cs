using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CT_MangHangHoaRaCong_2")]
public partial class CtMangHangHoaRaCong2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [StringLength(255)]
    public string? BaoVeTruc { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianRa { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianVao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    [StringLength(255)]
    public string? MoTa { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("CtMangHangHoaRaCong2s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
