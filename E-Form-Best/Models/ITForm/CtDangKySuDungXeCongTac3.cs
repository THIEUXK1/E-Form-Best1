using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CT_DangKySuDungXeCongTac_3")]
public partial class CtDangKySuDungXeCongTac3
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    [StringLength(255)]
    public string? LiDo { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("CtDangKySuDungXeCongTac3s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
