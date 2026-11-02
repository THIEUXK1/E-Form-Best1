using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Một KPI trong phiếu (UNIQUE id_phieu + id_kpi_mau). TrongSo + DiemQuanLy do quản lý điền.</summary>
[Table("DG_PhieuKpi")]
public partial class DgPhieuKpi
{
    [Key]
    [Column("id_phieu_kpi")]
    public Guid IdPhieuKpi { get; set; }

    [Column("id_phieu")]
    public Guid IdPhieu { get; set; }

    [Column("id_kpi_mau")]
    public Guid IdKpiMau { get; set; }

    [Column("cong_viec")]
    [StringLength(200)]
    public string CongViec { get; set; } = null!;

    [Column("chi_so_kpi")]
    public string? ChiSoKpi { get; set; }

    [Column("ket_qua")]
    public string? KetQua { get; set; }

    [Column("trong_so", TypeName = "decimal(5, 2)")]
    public decimal TrongSo { get; set; }

    [Column("diem_tu_danh_gia", TypeName = "decimal(5, 2)")]
    public decimal? DiemTuDanhGia { get; set; }

    [Column("diem_quan_ly", TypeName = "decimal(5, 2)")]
    public decimal? DiemQuanLy { get; set; }

    /// <summary>Mẫu Excel 2025: mức 1–5 (5 = Excellent … 1 = Very Poor); điểm dòng = TrongSo × mức / 5. Thay cột % ở trên.</summary>
    [Column("muc_tu_danh_gia")]
    public byte? MucTuDanhGia { get; set; }

    [Column("muc_quan_ly")]
    public byte? MucQuanLy { get; set; }

    /// <summary>Tên file trần trên fileserver (thư mục KPI; file cũ trước 16/10/2026 ở DanhGia), không lưu đường dẫn. Ảnh hoặc tài liệu.</summary>
    [Column("anh_minh_chung")]
    [StringLength(260)]
    public string? AnhMinhChung { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }
}
