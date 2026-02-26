using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DanhGiaFormIT")]
public partial class DanhGiaFormIt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormIT")]
    public int? IdFormIt { get; set; }

    public int? IdNguoiDanhGia { get; set; }

    [StringLength(100)]
    public string? TenNguoiDanhGia { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiDanhGia { get; set; }

    public int? MucDo { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("DanhGiaFormIts")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
