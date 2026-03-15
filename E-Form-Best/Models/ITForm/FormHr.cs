using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormHR")]
public partial class FormHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenForm { get; set; }

    public DateOnly? Ngay { get; set; }

    [StringLength(50)]
    public string? SoNhanVien { get; set; }

    [Column("TenNguoiNV")]
    [StringLength(255)]
    public string? TenNguoiNv { get; set; }

    [StringLength(255)]
    public string? ViTri { get; set; }

    [Column("idNguoiTao")]
    public int? IdNguoiTao { get; set; }

    [StringLength(255)]
    public string? TenNguoiTao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiTao { get; set; }

    [Column("idNguoiDuyet")]
    public int? IdNguoiDuyet { get; set; }

    [StringLength(255)]
    public string? TenNguoiDuyet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiDuyet { get; set; }

    [Column("idAdmin")]
    public int? IdAdmin { get; set; }

    [StringLength(255)]
    public string? TenAdmin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeAdmin { get; set; }

    [Column("idForm")]
    [StringLength(250)]
    public string? IdForm { get; set; }

    [StringLength(250)]
    public string? TrangThai { get; set; }

    public string? FileDinhKem { get; set; }

    [StringLength(250)]
    public string? BoPhan { get; set; }

    [Column("danhmuc")]
    [StringLength(250)]
    public string? Danhmuc { get; set; }

    [Column("ten_cong_ty")]
    [StringLength(250)]
    public string? TenCongTy { get; set; }

    public bool? HienThiBaoVe { get; set; }

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<BinhLuanFormHr> BinhLuanFormHrs { get; set; } = new List<BinhLuanFormHr>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrBaoVeXacNhan> HrBaoVeXacNhans { get; set; } = new List<HrBaoVeXacNhan>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrCtNguoiHoTro> HrCtNguoiHoTros { get; set; } = new List<HrCtNguoiHoTro>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrDangKySuDungXeCongTac3> HrDangKySuDungXeCongTac3s { get; set; } = new List<HrDangKySuDungXeCongTac3>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrDangKySuDungXeDaily4> HrDangKySuDungXeDaily4s { get; set; } = new List<HrDangKySuDungXeDaily4>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrDoiCaLam8> HrDoiCaLam8s { get; set; } = new List<HrDoiCaLam8>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrDonHoTroCongTac9> HrDonHoTroCongTac9s { get; set; } = new List<HrDonHoTroCongTac9>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrDonTiepKhac5> HrDonTiepKhac5s { get; set; } = new List<HrDonTiepKhac5>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrHoTroTienDienThoai7> HrHoTroTienDienThoai7s { get; set; } = new List<HrHoTroTienDienThoai7>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrMangHangHoaRaCong2> HrMangHangHoaRaCong2s { get; set; } = new List<HrMangHangHoaRaCong2>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrNhaThauQuaCong6> HrNhaThauQuaCong6s { get; set; } = new List<HrNhaThauQuaCong6>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<HrXinRaNgoai1> HrXinRaNgoai1s { get; set; } = new List<HrXinRaNgoai1>();

    [InverseProperty("IdFormHrNavigation")]
    public virtual ICollection<LichSuFormHr> LichSuFormHrs { get; set; } = new List<LichSuFormHr>();
}
