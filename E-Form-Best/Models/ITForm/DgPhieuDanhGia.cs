using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Phiếu đánh giá. Mỗi nhân viên một phiếu cho mỗi loại (self/manager) trong một kỳ — UNIQUE
/// (id_ky, ma_nhan_vien, loai) ở DB chống bấm lưu 2 lần sinh 2 phiếu. TrangThai pending = chờ quản lý đánh giá.
/// </summary>
[Table("DG_PhieuDanhGia")]
public partial class DgPhieuDanhGia
{
    [Key]
    [Column("id_phieu")]
    public Guid IdPhieu { get; set; }

    [Column("id_ky")]
    public Guid IdKy { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(20)]
    public string MaNhanVien { get; set; } = null!;

    [Column("loai")]
    [StringLength(20)]
    public string Loai { get; set; } = null!;

    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = "pending";

    [Column("cap_bac")]
    [StringLength(10)]
    public string? CapBac { get; set; }

    [Column("id_nhom_cong_viec")]
    public Guid? IdNhomCongViec { get; set; }

    [Column("diem_kpi", TypeName = "decimal(18, 4)")]
    public decimal? DiemKpi { get; set; }

    [Column("diem_nang_luc", TypeName = "decimal(18, 4)")]
    public decimal? DiemNangLuc { get; set; }

    [Column("tong_yeu_cau", TypeName = "decimal(18, 4)")]
    public decimal? TongYeuCau { get; set; }

    [Column("diem_thuc_te", TypeName = "decimal(18, 4)")]
    public decimal? DiemThucTe { get; set; }

    [Column("chenh_lech", TypeName = "decimal(18, 4)")]
    public decimal? ChenhLech { get; set; }

    [Column("xep_loai")]
    [StringLength(100)]
    public string? XepLoai { get; set; }

    [Column("ngay_nop", TypeName = "datetime")]
    public DateTime? NgayNop { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    /// <summary>Mã NV quản lý chấm (phiếu loại manager).</summary>
    [Column("nguoi_danh_gia")]
    [StringLength(20)]
    public string? NguoiDanhGia { get; set; }

    // Part 3 mẫu Excel 2025: 4 ô đầu nhân viên điền trong phiếu self, nhận xét quản lý điền trong phiếu manager
    [Column("diem_manh")]
    public string? DiemManh { get; set; }

    [Column("diem_yeu")]
    public string? DiemYeu { get; set; }

    [Column("lo_trinh")]
    public string? LoTrinh { get; set; }

    [Column("ke_hoach")]
    public string? KeHoach { get; set; }

    [Column("nhan_xet")]
    public string? NhanXet { get; set; }

    // Luồng ký 3 cấp, chỉ dùng trên phiếu manager. BuocKy null = chưa vào luồng hoặc chốt trước khi có luồng ký
    public const string BuocChoDuyet = "cho_duyet";
    public const string BuocTuChoi = "tu_choi";
    public const string BuocChoKy = "cho_ky";
    public const string BuocDaKy = "da_ky";

    [Column("buoc_ky")]
    [StringLength(20)]
    public string? BuocKy { get; set; }

    [Column("nguoi_duyet")]
    [StringLength(20)]
    public string? NguoiDuyet { get; set; }

    [Column("ngay_duyet", TypeName = "datetime")]
    public DateTime? NgayDuyet { get; set; }

    [Column("ly_do_tu_choi")]
    [StringLength(1000)]
    public string? LyDoTuChoi { get; set; }

    [Column("ngay_nv_ky", TypeName = "datetime")]
    public DateTime? NgayNvKy { get; set; }

    [Column("nv_dong_y")]
    public bool? NvDongY { get; set; }

    [Column("nhan_xet_nv")]
    public string? NhanXetNv { get; set; }
}
