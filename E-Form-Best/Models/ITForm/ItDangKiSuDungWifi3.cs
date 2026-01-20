using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_DangKiSuDungWifi_3")]
public partial class ItDangKiSuDungWifi3
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(255)]
    public string? LoaiThietBi { get; set; }

    [Column("MacTB")]
    [StringLength(100)]
    public string? MacTb { get; set; }

    [StringLength(100)]
    public string? LoaiThoiGian { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianBatDau { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianKetThuc { get; set; }

    public string? LyDo { get; set; }

    public string? GhiChu { get; set; }

    public byte[]? Anh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItDangKiSuDungWifi3s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
