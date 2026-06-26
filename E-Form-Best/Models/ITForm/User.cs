using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("User")]
public partial class User
{
    [Key]
    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Column("ho_ten")]
    [StringLength(255)]
    public string? HoTen { get; set; }

    [Column("TK")]
    [StringLength(255)]
    public string? Tk { get; set; }

    [Column("mat_khau")]
    [StringLength(255)]
    public string? MatKhau { get; set; }

    [Column("so_dien_thoai")]
    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [Column("phong_ban")]
    [StringLength(100)]
    public string? PhongBan { get; set; }

    [Column("vai_tro")]
    [StringLength(50)]
    public string? VaiTro { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    public string? TrangThai { get; set; }

    [Column("ngay_tao")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("push_endpoint")]
    public string? PushEndpoint { get; set; }

    [Column("push_p256dh")]
    public string? PushP256dh { get; set; }

    [Column("push_auth")]
    public string? PushAuth { get; set; }

    [Column("ten_cong_ty")]
    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [Column("anh_dai_dien")]
    public string? AnhDaiDien { get; set; }

    [Column("security_stamp")]
    [StringLength(255)]
    public string? SecurityStamp { get; set; }

    public int? FailedAttempts { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LockoutEnd { get; set; }

    [Column("ten_tieng_trung")]
    [StringLength(250)]
    public string? TenTiengTrung { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaNhanVien { get; set; }

    [Column("ma_phong_ban")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaPhongBan { get; set; }

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<FormCongViecNguoiLienQuan> FormCongViecNguoiLienQuans { get; set; } = new List<FormCongViecNguoiLienQuan>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<LichSuTruyCap> LichSuTruyCaps { get; set; } = new List<LichSuTruyCap>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<TscnLichSuXacThucNguoiDung> TscnLichSuXacThucNguoiDungs { get; set; } = new List<TscnLichSuXacThucNguoiDung>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<TscnThongTinMay> TscnThongTinMays { get; set; } = new List<TscnThongTinMay>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<UserBoPhan> UserBoPhans { get; set; } = new List<UserBoPhan>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<UserDevice> UserDevices { get; set; } = new List<UserDevice>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<UserDomainAuth> UserDomainAuths { get; set; } = new List<UserDomainAuth>();

    [InverseProperty("IdNguoiDungNavigation")]
    public virtual ICollection<UserQuyen> UserQuyens { get; set; } = new List<UserQuyen>();
}
