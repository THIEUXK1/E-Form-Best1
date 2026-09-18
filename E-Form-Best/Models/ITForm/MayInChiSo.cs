using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chỉ số đọc được của một máy in, mỗi máy mỗi ngày giữ đúng một dòng (bản đọc mới nhất trong ngày).
/// Số trang in trong ngày = chênh lệch <see cref="CounterTong"/> giữa hai ngày liên tiếp.
/// </summary>
[Table("MayIn_ChiSo")]
public partial class MayInChiSo
{
    [Key]
    [Column("id_chi_so")]
    public long IdChiSo { get; set; }

    [Column("id_may_in")]
    public int IdMayIn { get; set; }

    [Column("ngay_chot")]
    public DateOnly NgayChot { get; set; }

    [Column("thoi_diem_doc", TypeName = "datetime")]
    public DateTime ThoiDiemDoc { get; set; }

    [Column("counter_in")]
    public int? CounterIn { get; set; }

    [Column("counter_copy")]
    public int? CounterCopy { get; set; }

    [Column("counter_scan")]
    public int? CounterScan { get; set; }

    [Column("counter_tong")]
    public int? CounterTong { get; set; }

    /// <summary>Mực còn lại, tính theo mực THẤP NHẤT nếu máy có nhiều màu (TONER_C/M/Y/K).</summary>
    [Column("toner_phan_tram")]
    public int? TonerPhanTram { get; set; }

    /// <summary>Trống còn lại, tính theo trống thấp nhất nếu máy có nhiều trống (DRUM_C/M/Y/K).</summary>
    [Column("drum_phan_tram")]
    public int? DrumPhanTram { get; set; }

    /// <summary>
    /// Toàn bộ vật tư đo được % tại lần chốt, dạng JSON <c>[{"ten":"TONER_C","phanTram":37}, ...]</c>.
    /// Máy đen trắng chỉ có TONER_K/DRUM_K; máy màu có đủ 4 mực 4 trống. NULL với dòng chốt cũ
    /// (chốt trước khi có cột này) và với dòng nhập tay.
    /// </summary>
    [Column("vat_tu_json")]
    [StringLength(1000)]
    public string? VatTuJson { get; set; }

    [Column("trang_thai_thiet_bi")]
    [StringLength(50)]
    public string? TrangThaiThietBi { get; set; }

    /// <summary>API | Excel | NhapTay</summary>
    [Column("nguon")]
    [StringLength(20)]
    public string Nguon { get; set; } = "API";

    [Column("ghi_chu")]
    [StringLength(255)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdMayIn")]
    [InverseProperty("MayInChiSos")]
    public virtual MayIn MayInNavigation { get; set; } = null!;
}
