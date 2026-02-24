using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BinhLuanFormIT")]
public partial class BinhLuanFormIt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idForm")]
    public int? IdForm { get; set; }

    public string? NoiDung { get; set; }

    [Column("idNguoiBinhLuan")]
    [StringLength(50)]
    public string? IdNguoiBinhLuan { get; set; }

    [StringLength(255)]
    public string? TenNguoiBinhLuan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGian { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [StringLength(255)]
    public string? PhongBan { get; set; }

    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [StringLength(50)]
    public string? Ma { get; set; }

    [ForeignKey("IdForm")]
    [InverseProperty("BinhLuanFormIts")]
    public virtual FormIt? IdFormNavigation { get; set; }
}
