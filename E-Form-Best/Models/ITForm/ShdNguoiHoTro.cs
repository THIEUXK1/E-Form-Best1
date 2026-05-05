using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_NguoiHoTro")]
public partial class ShdNguoiHoTro
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("MaNV")]
    [StringLength(50)]
    public string? MaNv { get; set; }

    [StringLength(255)]
    public string? Ten { get; set; }

    public string? GhiChu { get; set; }

    [StringLength(250)]
    public string? BoPhan { get; set; }

    [InverseProperty("IdShdNguoiHoTroNavigation")]
    public virtual ICollection<ShdCtNguoiHoTro> ShdCtNguoiHoTros { get; set; } = new List<ShdCtNguoiHoTro>();
}
