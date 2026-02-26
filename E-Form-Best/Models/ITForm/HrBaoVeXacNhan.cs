using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_BaoVeXacNhan")]
public partial class HrBaoVeXacNhan
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int IdFormHr { get; set; }

    [StringLength(250)]
    public string? TieuDe { get; set; }

    public string? NoiDung { get; set; }

    public string? DuongDanFile { get; set; }

    [Column("idBaoVe")]
    public int? IdBaoVe { get; set; }

    [StringLength(250)]
    public string? TenBaoVe { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianHeThong { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrBaoVeXacNhans")]
    public virtual FormHr IdFormHrNavigation { get; set; } = null!;
}
