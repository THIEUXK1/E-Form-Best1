using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_ChiTietOCung")]
public partial class TscnChiTietOcung
{
    [Key]
    [Column("IdOCung")]
    public int IdOcung { get; set; }

    public int? IdMay { get; set; }

    [Column("ThongTinOCung")]
    [StringLength(500)]
    public string ThongTinOcung { get; set; } = null!;

    [ForeignKey("IdMay")]
    [InverseProperty("TscnChiTietOcungs")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }
}
