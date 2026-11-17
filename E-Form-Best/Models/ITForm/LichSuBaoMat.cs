using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

// Nhật ký bảo mật — append-only, không sửa/xoá. Ghi lần bị chặn (chưa đăng nhập / thiếu quyền) và lần lấy
// dữ liệu nhạy cảm. Chỉ quyền All đọc (trang /NhatKyBaoMat). Không FK tới User để log không chặn xoá user.
// Bảng tạo bởi .claude/plans/sql/lich-su-bao-mat-20261117.sql.
[Table("LichSuBaoMat")]
public partial class LichSuBaoMat
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("loai")]
    [StringLength(20)]
    [Unicode(false)]
    public string Loai { get; set; } = null!;

    [Column("id_nguoi_dung")]
    public int? IdNguoiDung { get; set; }

    [Column("tai_khoan")]
    [StringLength(100)]
    public string? TaiKhoan { get; set; }

    [Column("ho_ten")]
    [StringLength(150)]
    public string? HoTen { get; set; }

    [Column("phuong_thuc")]
    [StringLength(10)]
    [Unicode(false)]
    public string? PhuongThuc { get; set; }

    [Column("duong_dan")]
    [StringLength(500)]
    public string DuongDan { get; set; } = null!;

    [Column("dia_chi_ip")]
    [StringLength(45)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("trinh_duyet")]
    [StringLength(400)]
    public string? TrinhDuyet { get; set; }

    [Column("mo_ta")]
    [StringLength(200)]
    public string? MoTa { get; set; }
}
