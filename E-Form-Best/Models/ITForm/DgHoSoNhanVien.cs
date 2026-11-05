using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Cấp bậc + nhóm công việc dùng khi đánh giá, khoá theo mã NV (claim MaNv). Chưa có dòng → dùng mặc định.</summary>
[Table("DG_HoSoNhanVien")]
public partial class DgHoSoNhanVien
{
    [Key]
    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("cap_bac")]
    [StringLength(10)]
    public string? CapBac { get; set; }

    [Column("id_nhom_cong_viec")]
    public Guid? IdNhomCongViec { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(255)]
    public string? NguoiCapNhat { get; set; }

    /// <summary>Quản lý bấm "Ngừng đánh giá" (xoá mềm): khác null là ẩn khỏi danh sách quản lý.</summary>
    [Column("ngay_ngung", TypeName = "datetime")]
    public DateTime? NgayNgung { get; set; }

    [Column("nguoi_ngung")]
    [StringLength(255)]
    public string? NguoiNgung { get; set; }

    /// <summary>Ngày bắt đầu giữ bậc hiện tại: quản lý nhập tay, tự cập nhật khi xác nhận lên bậc.</summary>
    [Column("ngay_giu_bac")]
    public DateOnly? NgayGiuBac { get; set; }
}
