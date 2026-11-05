using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Đầu việc IT trong ma trận phân công, nạp từ "IT_Phân công nhiệm vụ.xlsx". Xoá mềm: mọi truy vấn lọc NgayXoa == null.</summary>
[Table("DG_DauViec")]
public partial class DgDauViec
{
    [Key]
    [Column("id_dau_viec")]
    public Guid IdDauViec { get; set; }

    [Column("nhom")]
    [StringLength(50)]
    public string Nhom { get; set; } = null!;

    [Column("thu_tu")]
    public int ThuTu { get; set; }

    [Column("ten")]
    [StringLength(300)]
    public string Ten { get; set; } = null!;

    [Column("ten_en")]
    [StringLength(300)]
    public string? TenEn { get; set; }

    [Column("mo_ta")]
    public string? MoTa { get; set; }

    /// <summary>Số thiết bị/người dùng phục vụ.</summary>
    [Column("so_luong")]
    public int? SoLuong { get; set; }

    /// <summary>Số lượng một người phụ trách chính lo được; có cả 2 số mới xét Overload.</summary>
    [Column("chuan_moi_nguoi")]
    public int? ChuanMoiNguoi { get; set; }

    [Column("nguoi_tao")]
    [StringLength(20)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(20)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(20)]
    public string? NguoiXoa { get; set; }
}
