using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>KPI mẫu theo nhóm công việc; nhân viên chỉ thấy KPI được giao qua DG_KpiPhanCong.</summary>
[Table("DG_KpiMau")]
public partial class DgKpiMau
{
    [Key]
    [Column("id_kpi_mau")]
    public Guid IdKpiMau { get; set; }

    [Column("id_nhom_cong_viec")]
    public Guid IdNhomCongViec { get; set; }

    [Column("ma_kpi")]
    [StringLength(50)]
    public string? MaKpi { get; set; }

    [Column("cong_viec")]
    [StringLength(200)]
    public string CongViec { get; set; } = null!;

    [Column("cong_viec_en")]
    [StringLength(200)]
    public string? CongViecEn { get; set; }

    [Column("kpi_goi_y")]
    public string? KpiGoiY { get; set; }

    [Column("kpi_goi_y_en")]
    public string? KpiGoiYEn { get; set; }

    [Column("ket_qua_goi_y")]
    public string? KetQuaGoiY { get; set; }

    [Column("ket_qua_goi_y_en")]
    public string? KetQuaGoiYEn { get; set; }

    [Column("mo_ta")]
    public string? MoTa { get; set; }

    [Column("don_vi")]
    [StringLength(50)]
    public string? DonVi { get; set; }

    [Column("nguon_du_lieu")]
    [StringLength(100)]
    public string? NguonDuLieu { get; set; }

    [Column("chu_ky")]
    [StringLength(50)]
    public string? ChuKy { get; set; }

    [Column("chi_tieu", TypeName = "decimal(10, 2)")]
    public decimal? ChiTieu { get; set; }

    [Column("trong_so_bo_phan", TypeName = "decimal(5, 2)")]
    public decimal? TrongSoBoPhan { get; set; }

    [Column("la_dinh_luong")]
    public bool LaDinhLuong { get; set; }

    [Column("bo_phan")]
    [StringLength(100)]
    public string? BoPhan { get; set; }

    [Column("thu_tu")]
    public int ThuTu { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "active";
}
