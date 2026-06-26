using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_LichSuXacThucNguoiDung")]
public partial class TscnLichSuXacThucNguoiDung
{
    [Key]
    public int IdLichSu { get; set; }

    public int IdMay { get; set; }

    public int IdNguoiDung { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayXacThuc { get; set; }

    [StringLength(500)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdMay")]
    [InverseProperty("TscnLichSuXacThucNguoiDungs")]
    public virtual TscnThongTinMay IdMayNavigation { get; set; } = null!;

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("TscnLichSuXacThucNguoiDungs")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
