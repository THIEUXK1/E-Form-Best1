using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CV_CongViec_Order_1")]
public partial class CvCongViecOrder1
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormCongViec")]
    public int? IdFormCongViec { get; set; }

    public string? GhiChu { get; set; }

    public byte[]? Anh { get; set; }

    public string? Ten { get; set; }

    public string? DuongDanAnh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiHanHoanThanh { get; set; }

    [StringLength(50)]
    public string? MucDoUuTien { get; set; }

    [ForeignKey("IdFormCongViec")]
    [InverseProperty("CvCongViecOrder1s")]
    public virtual FormCongViec? IdFormCongViecNavigation { get; set; }
}
