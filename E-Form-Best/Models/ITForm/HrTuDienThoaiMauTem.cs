using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Mẫu tem in cho tủ điện thoại. NoiDung là JSON 1 trang nhãn (cùng định dạng file .tnl của app TemNhan).
// Xoá mềm: mọi truy vấn phải lọc NgayXoa == null.
[Table("HR_TuDienThoaiMauTem")]
public partial class HrTuDienThoaiMauTem
{
    [Key]
    [Column("id_mau")]
    public int IdMau { get; set; }

    [Column("ten_mau")]
    [StringLength(100)]
    public string TenMau { get; set; } = null!;

    [Column("noi_dung")]
    public string NoiDung { get; set; } = null!;

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_tao")]
    [StringLength(100)]
    public string? NguoiTao { get; set; }

    [Column("ngay_sua", TypeName = "datetime")]
    public DateTime? NgaySua { get; set; }

    [Column("nguoi_sua")]
    [StringLength(100)]
    public string? NguoiSua { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(100)]
    public string? NguoiXoa { get; set; }
}
