using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DaoTao")]
public partial class DaoTao
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string TenDaoTao { get; set; } = null!;

    [StringLength(255)]
    public string? NguoiDaoTao { get; set; }

    public DateOnly? NgayDaoTao { get; set; }

    public string? NoiDungDaoTao { get; set; }

    [Column("idNguoiTao")]
    public int? IdNguoiTao { get; set; }

    [StringLength(255)]
    public string? TenNguoiTao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiTao { get; set; }

    // Trạng thái luồng phê duyệt: ChoDuyet -> DaDuyet/Huy -> DaHoanTat (khóa hồ sơ)
    [StringLength(50)]
    public string TrangThai { get; set; } = "ChoDuyet";

    [StringLength(255)]
    public string? BoPhan { get; set; }

    [StringLength(255)]
    public string? TenCongTy { get; set; }

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

    public int? DanhGiaHieuQua { get; set; }

    public string? GhiChuDanhGia { get; set; }

    // Mã ngắn để người tham dự tự nhập điểm danh (không cần được chọn sẵn trong danh sách tham dự)
    [StringLength(20)]
    public string? MaCuocHop { get; set; }

    [InverseProperty("IdDaoTaoNavigation")]
    public virtual ICollection<DaoTaoAnh> DaoTaoAnhs { get; set; } = new List<DaoTaoAnh>();

    [InverseProperty("IdDaoTaoNavigation")]
    public virtual ICollection<DaoTaoQuanLyDuyet> DaoTaoQuanLyDuyets { get; set; } = new List<DaoTaoQuanLyDuyet>();

    [InverseProperty("IdDaoTaoNavigation")]
    public virtual ICollection<DaoTaoNguoiThamGia> DaoTaoNguoiThamGias { get; set; } = new List<DaoTaoNguoiThamGia>();

    [InverseProperty("IdDaoTaoNavigation")]
    public virtual ICollection<DaoTaoTaiLieu> DaoTaoTaiLieus { get; set; } = new List<DaoTaoTaiLieu>();

    [InverseProperty("IdDaoTaoNavigation")]
    public virtual ICollection<LichSuFormDaoTao> LichSuFormDaoTaos { get; set; } = new List<LichSuFormDaoTao>();
}
