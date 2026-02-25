using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BinhLuanFormHR")]
public partial class BinhLuanFormHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int? IdFormHr { get; set; }

    public string? NoiDung { get; set; }

    [Column("idNguoiBinhLuan")]
    public int? IdNguoiBinhLuan { get; set; }

    [StringLength(255)]
    public string? TenNguoiBinhLuan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGian { get; set; }

    public int? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [StringLength(255)]
    public string? PhongBan { get; set; }

    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [StringLength(50)]
    public string? Ma { get; set; }
}
