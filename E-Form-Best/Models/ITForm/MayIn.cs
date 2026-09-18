using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Danh mục máy in trong nhà máy, nguồn ban đầu là file Excel "Print out information2026.xlsx".
/// </summary>
[Table("MayIn")]
public partial class MayIn
{
    [Key]
    [Column("id_may_in")]
    public int IdMayIn { get; set; }

    [Column("bo_phan")]
    [StringLength(50)]
    public string? BoPhan { get; set; }

    [Column("model")]
    [StringLength(50)]
    public string Model { get; set; } = null!;

    [Column("serial")]
    [StringLength(50)]
    public string Serial { get; set; } = null!;

    /// <summary>Chỉ lưu IPv4 hợp lệ; máy cắm USB hoặc chưa rõ để null và ghi nguyên văn vào <see cref="GhiChu"/>.</summary>
    [Column("dia_chi_ip")]
    [StringLength(45)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    /// <summary>Tên hàng đợi in trên print server \\vn-printersrv — tên người dùng thật sự nhìn thấy khi in.</summary>
    [Column("ten_hang_doi")]
    [StringLength(200)]
    public string? TenHangDoi { get; set; }

    [Column("vi_tri")]
    [StringLength(200)]
    public string? ViTri { get; set; }

    /// <summary>HoatDong | TamDung | BaoPhe</summary>
    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "HoatDong";

    /// <summary>Cho phép job nền gọi API web của máy in để lấy chỉ số.</summary>
    [Column("theo_doi_tu_dong")]
    public bool TheoDoiTuDong { get; set; }

    [Column("lan_doc_cuoi", TypeName = "datetime")]
    public DateTime? LanDocCuoi { get; set; }

    [Column("ket_qua_doc_cuoi")]
    [StringLength(255)]
    public string? KetQuaDocCuoi { get; set; }

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [InverseProperty("MayInNavigation")]
    public virtual ICollection<MayInChiSo> MayInChiSos { get; set; } = new List<MayInChiSo>();
}
