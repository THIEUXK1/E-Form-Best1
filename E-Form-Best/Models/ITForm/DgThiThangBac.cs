using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Kết quả thi thăng bậc. Đạt không tự đổi bậc: quản lý xác nhận riêng (ngay_len_bac) rồi hồ sơ mới đổi.
/// Xoá mềm; dòng đã xác nhận lên bậc không xoá được.
/// </summary>
[Table("DG_ThiThangBac")]
public partial class DgThiThangBac
{
    public const string Dat = "pass";
    public const string KhongDat = "fail";

    [Key]
    [Column("id_thi")]
    public Guid IdThi { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("bac_hien_tai")]
    [StringLength(10)]
    public string? BacHienTai { get; set; }

    [Column("bac_de_xuat")]
    [StringLength(10)]
    public string BacDeXuat { get; set; } = null!;

    [Column("ngay_thi")]
    public DateOnly NgayThi { get; set; }

    [Column("diem", TypeName = "decimal(6, 2)")]
    public decimal? Diem { get; set; }

    [Column("ket_qua")]
    [StringLength(10)]
    public string KetQua { get; set; } = null!;

    [Column("ghi_chu")]
    [StringLength(1000)]
    public string? GhiChu { get; set; }

    [Column("ma_gui")]
    public Guid? MaGui { get; set; }

    [Column("nguoi_ghi")]
    [StringLength(20)]
    public string? NguoiGhi { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("ngay_len_bac")]
    public DateOnly? NgayLenBac { get; set; }

    [Column("nguoi_xac_nhan")]
    [StringLength(20)]
    public string? NguoiXacNhan { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(20)]
    public string? NguoiXoa { get; set; }
}
