using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_ChiTietMacWifi")]
public partial class TscnChiTietMacWifi
{
    [Key]
    public int IdMacWifi { get; set; }

    public int? IdMay { get; set; }

    [StringLength(250)]
    public string? TenCard { get; set; }

    [StringLength(100)]
    public string DiaChiMac { get; set; } = null!;

    [ForeignKey("IdMay")]
    [InverseProperty("TscnChiTietMacWifis")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }
}
