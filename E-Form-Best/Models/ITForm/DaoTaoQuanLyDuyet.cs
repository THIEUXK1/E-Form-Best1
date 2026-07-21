using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DaoTao_QuanLyDuyet")]
public partial class DaoTaoQuanLyDuyet
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_DaoTao")]
    public int IdDaoTao { get; set; }

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

    [StringLength(50)]
    public string? Loai { get; set; }

    [ForeignKey("IdDaoTao")]
    [InverseProperty("DaoTaoQuanLyDuyets")]
    public virtual DaoTao IdDaoTaoNavigation { get; set; } = null!;
}
