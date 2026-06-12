using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BinhLuanFormCongViec")]
public partial class BinhLuanFormCongViec
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idForm")]
    [StringLength(50)]
    public string? IdForm { get; set; }

    public string? NoiDung { get; set; }

    [Column("idNguoiBinhLuan")]
    public int? IdNguoiBinhLuan { get; set; }

    [StringLength(255)]
    public string? TenNguoiBinhLuan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGian { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [StringLength(100)]
    public string? PhongBan { get; set; }

    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [StringLength(50)]
    public string? Ma { get; set; }
}
