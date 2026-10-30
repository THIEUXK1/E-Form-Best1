using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhật ký append-only của sự cố hệ thống (KK_HeThongSuCo): tạo, đổi trạng thái/nguyên nhân/người phụ trách,
/// ghi chú tiến độ. KHÔNG sửa/xoá dòng cũ.
/// </summary>
[Table("KK_HeThongSuCoLichSu")]
public partial class KkHeThongSuCoLichSu
{
    public const string Tao = "TAO";
    public const string CapNhat = "CAP_NHAT";
    public const string GhiChuTienDo = "GHI_CHU";

    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("id_su_co")]
    public int IdSuCo { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("nguoi_thao_tac")]
    [StringLength(100)]
    public string? NguoiThaoTac { get; set; }

    /// <summary>TAO / CAP_NHAT / GHI_CHU / XOA.</summary>
    [Column("hanh_dong")]
    [StringLength(20)]
    [Unicode(false)]
    public string HanhDong { get; set; } = null!;

    [Column("thay_doi")]
    [StringLength(2000)]
    public string? ThayDoi { get; set; }

    [Column("ghi_chu")]
    [StringLength(1000)]
    public string? GhiChu { get; set; }

    /// <summary>Khoá chống bấm lưu 2 lần khi cập nhật — unique (lọc NOT NULL) ở tầng DB.</summary>
    [Column("ma_gui")]
    public Guid? MaGui { get; set; }
}
