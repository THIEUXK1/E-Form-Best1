using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Vai trò của một người trên đầu việc. Đổi hoặc gỡ là đặt ngay_go rồi thêm dòng mới, nên giữ được lịch sử phân công.</summary>
[Table("DG_DauViecPhanCong")]
public partial class DgDauViecPhanCong
{
    public const string Chinh = "chinh";
    public const string DuPhong1 = "du_phong_1";
    public const string DuPhong2 = "du_phong_2";

    [Key]
    [Column("id_phan_cong")]
    public Guid IdPhanCong { get; set; }

    [Column("id_dau_viec")]
    public Guid IdDauViec { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("vai_tro")]
    [StringLength(20)]
    public string VaiTro { get; set; } = null!;

    [Column("nguoi_giao")]
    [StringLength(20)]
    public string? NguoiGiao { get; set; }

    [Column("ngay_giao", TypeName = "datetime")]
    public DateTime NgayGiao { get; set; }

    [Column("ngay_go", TypeName = "datetime")]
    public DateTime? NgayGo { get; set; }

    [Column("nguoi_go")]
    [StringLength(20)]
    public string? NguoiGo { get; set; }
}
