using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_LichSuXacThucAdmin")]
public partial class TscnLichSuXacThucAdmin
{
    [Key]
    public int IdLog { get; set; }

    public int? IdMay { get; set; }

    [StringLength(150)]
    public string TaiKhoanXacThuc { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianXacThuc { get; set; }

    [StringLength(100)]
    public string TrangThai { get; set; } = null!;

    [ForeignKey("IdMay")]
    [InverseProperty("TscnLichSuXacThucAdmins")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }
}
