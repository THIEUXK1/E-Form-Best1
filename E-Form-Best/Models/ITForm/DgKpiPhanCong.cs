using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>KPI mẫu giao cho một nhân viên (UNIQUE ma_nhan_vien + id_kpi_mau).</summary>
[Table("DG_KpiPhanCong")]
public partial class DgKpiPhanCong
{
    [Key]
    [Column("id_phan_cong")]
    public Guid IdPhanCong { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("id_kpi_mau")]
    public Guid IdKpiMau { get; set; }

    [Column("nguoi_giao")]
    [StringLength(20)]
    public string? NguoiGiao { get; set; }

    [Column("ngay_giao", TypeName = "datetime")]
    public DateTime NgayGiao { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "active";
}
