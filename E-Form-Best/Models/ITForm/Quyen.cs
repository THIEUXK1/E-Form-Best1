using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("Quyen")]
public partial class Quyen
{
    [Key]
    [Column("id_quyen")]
    public int IdQuyen { get; set; }

    [Column("ten_quyen")]
    [StringLength(100)]
    public string? TenQuyen { get; set; }

    [Column("mo_ta")]
    [StringLength(255)]
    public string? MoTa { get; set; }

    [InverseProperty("IdQuyenNavigation")]
    public virtual ICollection<UserQuyen> UserQuyens { get; set; } = new List<UserQuyen>();
}
