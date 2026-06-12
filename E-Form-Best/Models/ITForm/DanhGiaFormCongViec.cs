using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DanhGiaFormCongViec")]
public partial class DanhGiaFormCongViec
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormCongViec")]
    public int? IdFormCongViec { get; set; }

    public int? IdNguoiDanhGia { get; set; }

    [StringLength(255)]
    public string? TenNguoiDanhGia { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiDanhGia { get; set; }

    [StringLength(50)]
    public string? MucDo { get; set; }
}
