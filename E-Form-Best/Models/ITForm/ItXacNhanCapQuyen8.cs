using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_XacNhanCapQuyen_8")]
public partial class ItXacNhanCapQuyen8
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_CapQuyenOChung_8")]
    public int IdCapQuyenOchung8 { get; set; }

    [StringLength(250)]
    public string? BoPhan { get; set; }

    [StringLength(100)]
    public string? TrangThai { get; set; }

    [Column("idNguoiXacNhan")]
    [StringLength(50)]
    [Unicode(false)]
    public string? IdNguoiXacNhan { get; set; }

    [StringLength(250)]
    public string? TenNguoiXacNhan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeXacNhan { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdCapQuyenOchung8")]
    [InverseProperty("ItXacNhanCapQuyen8s")]
    public virtual ItCapQuyenOchung8 IdCapQuyenOchung8Navigation { get; set; } = null!;
}
