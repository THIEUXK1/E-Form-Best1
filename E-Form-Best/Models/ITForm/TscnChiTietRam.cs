using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_ChiTietRam")]
public partial class TscnChiTietRam
{
    [Key]
    public int IdRam { get; set; }

    public int? IdMay { get; set; }

    [StringLength(250)]
    public string ThanhRam { get; set; } = null!;

    [ForeignKey("IdMay")]
    [InverseProperty("TscnChiTietRams")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }
}
