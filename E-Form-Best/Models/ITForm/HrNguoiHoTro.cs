using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_NguoiHoTro")]
public partial class HrNguoiHoTro
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

    [InverseProperty("IdHrNguoiHoTroNavigation")]
    public virtual ICollection<HrCtNguoiHoTro> HrCtNguoiHoTros { get; set; } = new List<HrCtNguoiHoTro>();
}
