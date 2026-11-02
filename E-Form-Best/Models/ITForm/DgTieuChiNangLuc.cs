using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Tiêu chí năng lực Part 2 (mẫu Excel 2025) — riêng từng nhân viên, quản lý đặt ở "Hồ sơ & KPI".
/// Tổng điểm tối đa các tiêu chí active = 50. Bỏ tiêu chí = trang_thai 'inactive' (phiếu cũ vẫn tham chiếu).
/// </summary>
[Table("DG_TieuChiNangLuc")]
public partial class DgTieuChiNangLuc
{
    [Key]
    [Column("id_tieu_chi")]
    public Guid IdTieuChi { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("ten")]
    [StringLength(300)]
    public string Ten { get; set; } = null!;

    [Column("mo_ta")]
    public string? MoTa { get; set; }

    [Column("diem_toi_da", TypeName = "decimal(5, 2)")]
    public decimal DiemToiDa { get; set; }

    [Column("thu_tu")]
    public int ThuTu { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "active";

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(20)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }
}
