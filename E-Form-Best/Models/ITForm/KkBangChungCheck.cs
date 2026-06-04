using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_BangChungCheck")]
public partial class KkBangChungCheck
{
    [Key]
    [Column("id_bang_chung")]
    public int IdBangChung { get; set; }

    [Column("id_thiet_bi")]
    public int IdThietBi { get; set; }

    [Column("thoi_gian_check", TypeName = "datetime")]
    public DateTime ThoiGianCheck { get; set; }

    [Column("duong_dan_anh")]
    [StringLength(500)]
    public string? DuongDanAnh { get; set; }

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdThietBi")]
    [InverseProperty("KkBangChungChecks")]
    public virtual KkThietBi IdThietBiNavigation { get; set; } = null!;
}
