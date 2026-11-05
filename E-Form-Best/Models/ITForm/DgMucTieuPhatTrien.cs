using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Mục tiêu trong kế hoạch phát triển (Part 3 tách thành từng việc có hạn, tiến độ, kết quả). Xoá mềm: NgayXoa == null là còn hiệu lực.
/// IdDaoTao không có FK vì trang Đào tạo xoá cứng buổi đào tạo; UX_DG_MucTieu_MaGui chống bấm lưu 2 lần sinh 2 mục tiêu.
/// </summary>
[Table("DG_MucTieuPhatTrien")]
public partial class DgMucTieuPhatTrien
{
    public const string DangLam = "doing";
    public const string HoanThanh = "done";
    public const string DaHuy = "cancelled";

    [Key]
    [Column("id_muc_tieu")]
    public Guid IdMucTieu { get; set; }

    [Column("id_ky")]
    public Guid IdKy { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("noi_dung")]
    [StringLength(500)]
    public string NoiDung { get; set; } = null!;

    [Column("ket_qua_mong_doi")]
    [StringLength(500)]
    public string? KetQuaMongDoi { get; set; }

    [Column("han_hoan_thanh")]
    public DateOnly? HanHoanThanh { get; set; }

    [Column("tien_do")]
    public byte TienDo { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = DangLam;

    [Column("ket_qua_thuc_te")]
    public string? KetQuaThucTe { get; set; }

    [Column("id_nang_luc")]
    public Guid? IdNangLuc { get; set; }

    [Column("id_dao_tao")]
    public int? IdDaoTao { get; set; }

    [Column("ma_gui")]
    public Guid? MaGui { get; set; }

    [Column("nguoi_tao")]
    [StringLength(20)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(20)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(20)]
    public string? NguoiXoa { get; set; }
}
