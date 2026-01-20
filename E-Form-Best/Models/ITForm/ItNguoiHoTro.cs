using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_NguoiHoTro")]
public partial class ItNguoiHoTro
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("MaNV")]
    [StringLength(20)]
    [Unicode(false)]
    public string? MaNv { get; set; }

    [StringLength(100)]
    public string? Ten { get; set; }

    public string? GhiChu { get; set; }

    [StringLength(100)]
    public string? BoPhan { get; set; }

    [InverseProperty("IdItNguoiHoTroNavigation")]
    public virtual ICollection<ItCtNguoiHoTro> ItCtNguoiHoTros { get; set; } = new List<ItCtNguoiHoTro>();
}
