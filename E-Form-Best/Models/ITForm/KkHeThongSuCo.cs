using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Sự cố của một hệ thống nội bộ ở trang /TheoDoiHeThong (BPVN-MES / WMS / SCM / EHR...).
/// MaHeThong khớp "Ten" trong appsettings TheoDoiHeThong:HeThong. Xoá mềm qua NgayXoa.
/// Mọi lần tạo/sửa ghi thêm 1 dòng KK_HeThongSuCoLichSu.
/// </summary>
[Table("KK_HeThongSuCo")]
public partial class KkHeThongSuCo
{
    public const string DangXuLy = "DANG_XU_LY";
    public const string TheoDoi = "THEO_DOI";
    public const string DaKhacPhuc = "DA_KHAC_PHUC";

    [Key]
    [Column("id_su_co")]
    public int IdSuCo { get; set; }

    [Column("ma_he_thong")]
    [StringLength(50)]
    [Unicode(false)]
    public string MaHeThong { get; set; } = null!;

    [Column("tieu_de")]
    [StringLength(200)]
    public string TieuDe { get; set; } = null!;

    [Column("mo_ta")]
    [StringLength(2000)]
    public string? MoTa { get; set; }

    /// <summary>DANG_XU_LY / THEO_DOI / DA_KHAC_PHUC.</summary>
    [Column("trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string TrangThai { get; set; } = null!;

    [Column("nguyen_nhan")]
    [StringLength(1000)]
    public string? NguyenNhan { get; set; }

    [Column("nguoi_chiu_trach_nhiem")]
    [StringLength(100)]
    public string? NguoiChiuTrachNhiem { get; set; }

    [Column("bat_dau_luc", TypeName = "datetime")]
    public DateTime BatDauLuc { get; set; }

    [Column("ket_thuc_luc", TypeName = "datetime")]
    public DateTime? KetThucLuc { get; set; }

    /// <summary>Khoá chống bấm lưu 2 lần khi tạo mới — unique (lọc NOT NULL) ở tầng DB.</summary>
    [Column("ma_gui")]
    public Guid? MaGui { get; set; }

    [Column("nguoi_tao")]
    [StringLength(100)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(100)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(100)]
    public string? NguoiXoa { get; set; }
}
