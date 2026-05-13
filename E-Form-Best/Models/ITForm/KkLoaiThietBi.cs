using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_LoaiThietBi")]
public partial class KkLoaiThietBi
{
    [Key]
    [Column("id_loai")]
    public int IdLoai { get; set; }

    [Column("ten_loai")]
    [StringLength(255)]
    public string TenLoai { get; set; } = null!;

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }
}
