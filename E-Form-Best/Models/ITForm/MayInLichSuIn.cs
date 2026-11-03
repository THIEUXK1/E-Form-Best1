using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Một lệnh in/copy/scan kéo về từ trang Job History của máy in (xem MayInLichSuInService).
/// Append-only: máy chỉ giữ vài trăm lệnh gần nhất nên đây là nơi duy nhất còn lịch sử cũ.
/// </summary>
[Table("MayIn_LichSuIn")]
public partial class MayInLichSuIn
{
    [Key]
    [Column("id_lich_su_in")]
    public long IdLichSuIn { get; set; }

    [Column("id_may_in")]
    public int IdMayIn { get; set; }

    /// <summary>Giờ theo đồng hồ máy in — có máy lệch vài giờ so với giờ thật.</summary>
    [Column("thoi_diem_in", TypeName = "datetime")]
    public DateTime ThoiDiemIn { get; set; }

    /// <summary>Owner trên máy in = tài khoản Windows gửi lệnh, đối chiếu được với User.TK.</summary>
    [Column("nguoi_in")]
    [StringLength(100)]
    public string? NguoiIn { get; set; }

    [Column("ten_tai_lieu")]
    [StringLength(500)]
    public string? TenTaiLieu { get; set; }

    [Column("loai")]
    [StringLength(30)]
    public string? Loai { get; set; }

    [Column("trang_thai")]
    [StringLength(30)]
    public string? TrangThai { get; set; }

    [Column("so_trang")]
    public int? SoTrang { get; set; }

    [Column("khay_ra")]
    [StringLength(100)]
    public string? KhayRa { get; set; }

    [Column("cong_nhan")]
    [StringLength(50)]
    public string? CongNhan { get; set; }

    /// <summary>SHA-256 các trường của dòng; cùng <see cref="SoLan"/> tạo khoá chống nhân bản.</summary>
    [Column("khoa")]
    [StringLength(64)]
    [Unicode(false)]
    public string Khoa { get; set; } = "";

    /// <summary>Thứ tự của dòng trong các dòng giống hệt nhau ở cùng một lần đọc (1, 2, 3...).</summary>
    [Column("so_lan")]
    public short SoLan { get; set; }

    [Column("ngay_dong_bo", TypeName = "datetime")]
    public DateTime NgayDongBo { get; set; }
}
