using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

// Nhật ký đăng nhập thất bại — append-only, không sửa/xoá. Không bao giờ chứa mật khẩu
// (nhưng TaiKhoanNhap có thể là mật khẩu gõ nhầm ô -> chỉ admin IT được đọc).
// Không có navigation tới User: IdNguoiDung NULL khi tài khoản không tồn tại, không FK.
[Table("LichSuDangNhapLoi")]
public partial class LichSuDangNhapLoi
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("tai_khoan_nhap")]
    [StringLength(100)]
    public string? TaiKhoanNhap { get; set; }

    [Column("id_nguoi_dung")]
    public int? IdNguoiDung { get; set; }

    [Column("dia_chi_ip")]
    [StringLength(45)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("trinh_duyet")]
    [StringLength(400)]
    public string? TrinhDuyet { get; set; }

    [Column("ly_do")]
    [StringLength(20)]
    [Unicode(false)]
    public string LyDo { get; set; } = null!;

    [Column("noi_goi")]
    [StringLength(30)]
    [Unicode(false)]
    public string NoiGoi { get; set; } = null!;
}
