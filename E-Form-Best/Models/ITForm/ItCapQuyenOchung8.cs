using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_CapQuyenOChung_8")]
public partial class ItCapQuyenOchung8
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int IdFormIt { get; set; }

    [Column("TenThuMuc_OChung")]
    [StringLength(500)]
    public string? TenThuMucOchung { get; set; }

    [StringLength(100)]
    public string? LoaiQuyenYeuCau { get; set; }

    public string? MucDich { get; set; }

    [StringLength(100)]
    public string? LoaiYeuCau { get; set; }

    [StringLength(250)]
    public string? NguoiSuDung { get; set; }

    [StringLength(250)]
    public string? TenMayTinh { get; set; }

    public string? GhiChu { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItCapQuyenOchung8s")]
    public virtual FormIt IdFormItNavigation { get; set; } = null!;

    [InverseProperty("IdCapQuyenOchung8Navigation")]
    public virtual ICollection<ItXacNhanCapQuyen8> ItXacNhanCapQuyen8s { get; set; } = new List<ItXacNhanCapQuyen8>();
}
