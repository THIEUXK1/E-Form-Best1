using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Điểm một tiêu chí Part 2 trong phiếu (UNIQUE id_phieu + id_tieu_chi). TomTat chỉ phiếu self dùng.</summary>
[Table("DG_PhieuTieuChi")]
public partial class DgPhieuTieuChi
{
    [Key]
    [Column("id_phieu_tieu_chi")]
    public Guid IdPhieuTieuChi { get; set; }

    [Column("id_phieu")]
    public Guid IdPhieu { get; set; }

    [Column("id_tieu_chi")]
    public Guid IdTieuChi { get; set; }

    [Column("tom_tat")]
    public string? TomTat { get; set; }

    [Column("diem", TypeName = "decimal(5, 2)")]
    public decimal? Diem { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }
}
