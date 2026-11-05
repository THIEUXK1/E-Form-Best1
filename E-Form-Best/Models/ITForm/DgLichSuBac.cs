using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Lịch sử đổi bậc, chỉ thêm. id_thi có giá trị khi lên bậc qua thi, null khi sửa trực tiếp ở hồ sơ.</summary>
[Table("DG_LichSuBac")]
public partial class DgLichSuBac
{
    [Key]
    [Column("id_lich_su")]
    public Guid IdLichSu { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("bac_cu")]
    [StringLength(10)]
    public string? BacCu { get; set; }

    [Column("bac_moi")]
    [StringLength(10)]
    public string? BacMoi { get; set; }

    [Column("ngay_hieu_luc")]
    public DateOnly NgayHieuLuc { get; set; }

    [Column("id_thi")]
    public Guid? IdThi { get; set; }

    [Column("ghi_chu")]
    [StringLength(200)]
    public string? GhiChu { get; set; }

    [Column("nguoi_thuc_hien")]
    [StringLength(20)]
    public string? NguoiThucHien { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }
}
