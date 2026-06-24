using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_ChiTietManHinh")]
public partial class TscnChiTietManHinh
{
    [Key]
    public int IdManHinh { get; set; }

    public int? IdMay { get; set; }

    [StringLength(500)]
    public string ThongTinManHinh { get; set; } = null!;

    [ForeignKey("IdMay")]
    [InverseProperty("TscnChiTietManHinhs")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }
}
