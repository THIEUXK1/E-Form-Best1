using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DanhMucQuyenBoPhan")]
[Index("MaQuyen", Name = "UQ__DanhMucQ__B9A4290ED67093A0", IsUnique = true)]
public partial class DanhMucQuyenBoPhan
{
    [Key]
    [Column("id_quyen")]
    public int IdQuyen { get; set; }

    [Column("ma_quyen")]
    [StringLength(100)]
    public string MaQuyen { get; set; } = null!;

    [Column("ten_quyen")]
    [StringLength(255)]
    public string TenQuyen { get; set; } = null!;

    [Column("mo_ta")]
    public string? MoTa { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [InverseProperty("IdQuyenNavigation")]
    public virtual ICollection<BoPhanQuyenTrungGian> BoPhanQuyenTrungGians { get; set; } = new List<BoPhanQuyenTrungGian>();
}
