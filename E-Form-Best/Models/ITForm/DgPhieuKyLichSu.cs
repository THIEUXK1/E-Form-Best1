using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Từng thao tác trong luồng ký của phiếu, chỉ thêm. Một người giữ cả 2 cấp vẫn để lại 2 dòng riêng.</summary>
[Table("DG_PhieuKyLichSu")]
public partial class DgPhieuKyLichSu
{
    public const string GuiDuyet = "gui_duyet";
    public const string Duyet = "duyet";
    public const string TuChoi = "tu_choi";
    public const string NvKy = "nv_ky";
    public const string MoLai = "mo_lai";

    [Key]
    [Column("id_lich_su")]
    public Guid IdLichSu { get; set; }

    [Column("id_phieu")]
    public Guid IdPhieu { get; set; }

    [Column("hanh_dong")]
    [StringLength(20)]
    public string HanhDong { get; set; } = null!;

    [Column("nguoi")]
    [StringLength(20)]
    public string? Nguoi { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    [Column("ngay", TypeName = "datetime")]
    public DateTime Ngay { get; set; }
}
