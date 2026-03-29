using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_XinRaNgoai_1")]
public partial class HrXinRaNgoai1
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    [StringLength(500)]
    public string? LiDo { get; set; }

    [StringLength(255)]
    public string? DiaDiem { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianRa { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianVeDuTinh { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrXinRaNgoai1s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
