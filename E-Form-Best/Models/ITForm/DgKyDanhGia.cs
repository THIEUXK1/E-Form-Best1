using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Kỳ đánh giá. Tối đa 1 kỳ 'open' cùng lúc (filtered unique index UX_DG_KyDanhGia_DangMo),
/// kỳ 'closed' chỉ xem. ThangCu = phiếu chấm thang % trước 02/11/2026, không so được với chuẩn 60/100.
/// </summary>
[Table("DG_KyDanhGia")]
public partial class DgKyDanhGia
{
    [Key]
    [Column("id_ky")]
    public Guid IdKy { get; set; }

    [Column("ten")]
    [StringLength(100)]
    public string Ten { get; set; } = null!;

    [Column("ngay_bat_dau")]
    public DateOnly? NgayBatDau { get; set; }

    [Column("han_nop")]
    public DateOnly? HanNop { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "open";

    [Column("thang_cu")]
    public bool ThangCu { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_tao")]
    [StringLength(20)]
    public string? NguoiTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(20)]
    public string? NguoiCapNhat { get; set; }
}
