using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_ThietBi")]
public partial class KkThietBi
{
    [Key]
    [Column("id_thiet_bi")]
    public int IdThietBi { get; set; }

    [Column("ten_vi_tri")]
    [StringLength(500)]
    public string? TenViTri { get; set; }

    [Column("ten_may_tinh")]
    [StringLength(255)]
    public string? TenMayTinh { get; set; }

    [Column("ten_dang_nhap")]
    [StringLength(255)]
    public string? TenDangNhap { get; set; }

    [Column("id_nguoi_dung")]
    public int? IdNguoiDung { get; set; }

    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("loai_thiet_bi")]
    [StringLength(255)]
    public string? LoaiThietBi { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    [StringLength(500)]
    public string? LyDoXoa { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("id_trang_thai")]
    public int? IdTrangThai { get; set; }

    [Column("duong_dan_anh")]
    public string? DuongDanAnh { get; set; }

    [Column("thoi_gian_check", TypeName = "datetime")]
    public DateTime? ThoiGianCheck { get; set; }

    [Column("quy_cach")]
    public string? QuyCach { get; set; }

    [Column("seribacode")]
    [StringLength(255)]
    public string? Seribacode { get; set; }

    [Column("han_bao_hanh")]
    public DateOnly? HanBaoHanh { get; set; }

    [Column("win_license")]
    [StringLength(255)]
    public string? WinLicense { get; set; }

    [Column("office_license")]
    [StringLength(255)]
    public string? OfficeLicense { get; set; }

    [Column("ip")]
    [StringLength(50)]
    public string? Ip { get; set; }

    [Column("version")]
    [StringLength(100)]
    public string? Version { get; set; }

    [Column("error")]
    [StringLength(500)]
    public string? Error { get; set; }

    public int? IdMay { get; set; }

    /// <summary>
    /// Trả lời câu hỏi bắt buộc khi kiểm kê "Máy này có cần cài Office không?".
    /// NULL = chưa từng hỏi (thiết bị có từ trước khi có câu hỏi này); true = cần; false = không cần.
    /// Dùng để quyết định máy có được cài/nâng cấp Office trong đợt tới.
    /// </summary>
    [Column("can_cai_office")]
    public bool? CanCaiOffice { get; set; }

    [Column("ngay_tra_loi_office", TypeName = "datetime")]
    public DateTime? NgayTraLoiOffice { get; set; }

    /// <summary>Vị trí địa lý thực tế (tỉnh/địa bàn) - trỏ tới danh mục KK_ViTriDiaLy.</summary>
    [Column("id_vi_tri_dia_ly")]
    public int? IdViTriDiaLy { get; set; }

    [ForeignKey("IdViTriDiaLy")]
    [InverseProperty("KkThietBis")]
    public virtual KkViTriDiaLy? IdViTriDiaLyNavigation { get; set; }

    [ForeignKey("IdMay")]
    [InverseProperty("KkThietBis")]
    public virtual TscnThongTinMay? IdMayNavigation { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("KkThietBis")]
    public virtual User? IdNguoiDungNavigation { get; set; }

    [ForeignKey("IdTrangThai")]
    [InverseProperty("KkThietBis")]
    public virtual KkTrangThai? IdTrangThaiNavigation { get; set; }

    [ForeignKey("IdboPhan")]
    [InverseProperty("KkThietBis")]
    public virtual KkBoPhan? IdboPhanNavigation { get; set; }

    [ForeignKey("IdcongTy")]
    [InverseProperty("KkThietBis")]
    public virtual KkCongTy? IdcongTyNavigation { get; set; }

    [InverseProperty("IdThietBiNavigation")]
    public virtual ICollection<KkBangChungCheck> KkBangChungChecks { get; set; } = new List<KkBangChungCheck>();
}
