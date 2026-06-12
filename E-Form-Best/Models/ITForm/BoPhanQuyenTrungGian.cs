using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BoPhan_Quyen_TrungGian")]
public partial class BoPhanQuyenTrungGian
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_bo_phan")]
    public int IdBoPhan { get; set; }

    [Column("id_quyen")]
    public int IdQuyen { get; set; }

    [Column("cho_phep")]
    public bool? ChoPhep { get; set; }

    [Column("ngay_gan", TypeName = "datetime")]
    public DateTime? NgayGan { get; set; }

    [Column("nguoi_gan")]
    [StringLength(150)]
    public string? NguoiGan { get; set; }

    [ForeignKey("IdBoPhan")]
    [InverseProperty("BoPhanQuyenTrungGians")]
    public virtual BoPhan IdBoPhanNavigation { get; set; } = null!;

    [ForeignKey("IdQuyen")]
    [InverseProperty("BoPhanQuyenTrungGians")]
    public virtual DanhMucQuyenBoPhan IdQuyenNavigation { get; set; } = null!;
}
