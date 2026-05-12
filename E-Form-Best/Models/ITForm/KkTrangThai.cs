using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_TrangThai")]
public partial class KkTrangThai
{
    [Key]
    [Column("id_trang_thai")]
    public int IdTrangThai { get; set; }

    [Column("ten_trang_thai")]
    [StringLength(100)]
    public string TenTrangThai { get; set; } = null!;

    [Column("mo_ta")]
    [StringLength(255)]
    public string? MoTa { get; set; }

    [InverseProperty("IdTrangThaiNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();
}
