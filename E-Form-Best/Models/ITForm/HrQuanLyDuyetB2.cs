using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_QuanLyDuyetB2")]
public partial class HrQuanLyDuyetB2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int? IdFormHr { get; set; }

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

    [StringLength(255)]
    public string? Loai { get; set; }

    [InverseProperty("IdHrQuanLyDuyetB2Navigation")]
    public virtual ICollection<HrQuanLyDuyetB2UyQuyen> HrQuanLyDuyetB2UyQuyens { get; set; } = new List<HrQuanLyDuyetB2UyQuyen>();

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrQuanLyDuyetB2s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
