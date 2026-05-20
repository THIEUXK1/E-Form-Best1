using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_QuanLyDuyetB2")]
public partial class ShdQuanLyDuyetB2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormSHD")]
    public int? IdFormShd { get; set; }

    [Column("IDNguoiXacNhan")]
    public int? IdnguoiXacNhan { get; set; }

    public int? ThuTuXacNhan { get; set; }

    public int? TrangThaiXacNhan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianXacNhan { get; set; }

    public string? GhiChu { get; set; }

    [StringLength(50)]
    public string? MaNguoiXacNhan { get; set; }

    [StringLength(255)]
    public string? TenNguoiXacNhan { get; set; }

    [StringLength(100)]
    public string? Loai { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("ShdQuanLyDuyetB2s")]
    public virtual FormShd? IdFormShdNavigation { get; set; }

    [InverseProperty("IdShdQuanLyDuyetB2Navigation")]
    public virtual ICollection<ShdQuanLyDuyetB2UyQuyen> ShdQuanLyDuyetB2UyQuyens { get; set; } = new List<ShdQuanLyDuyetB2UyQuyen>();
}
