using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_LichSuThaoTac")]
public partial class KkLichSuThaoTac
{
    [Key]
    public int IdLichSu { get; set; }

    [StringLength(100)]
    public string HanhDong { get; set; } = null!;

    [StringLength(100)]
    public string DoiTuong { get; set; } = null!;

    public int IdDoiTuong { get; set; }

    public string? ChiTiet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGian { get; set; }

    [StringLength(100)]
    public string? NguoiThaoTac { get; set; }
}
