using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BoPhan")]
public partial class BoPhan
{
    [Key]
    [Column("id_bo_phan")]
    public int IdBoPhan { get; set; }

    [Column("ten_bo_phan")]
    [StringLength(255)]
    public string? TenBoPhan { get; set; }

    [Column("mo_ta")]
    [StringLength(500)]
    public string? MoTa { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [InverseProperty("IdBoPhanNavigation")]
    public virtual ICollection<UserBoPhan> UserBoPhans { get; set; } = new List<UserBoPhan>();
}
