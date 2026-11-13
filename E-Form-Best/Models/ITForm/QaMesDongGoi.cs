using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Bản sao dòng đóng gói NH-MES (khoá = dấu vân tay JSON dòng, MES không có id). Không xoá cứng: hết trên MES thì đặt NgayMat.
// Script: .claude/plans/sql/qa-mes-dong-goi-20261113.sql
[Table("QA_MesDongGoi")]
public partial class QaMesDongGoi
{
    [Key]
    [Column("id_dong_mes")]
    public int IdDongMes { get; set; }

    [Column("dau_van_tay")]
    [StringLength(64)]
    public string DauVanTay { get; set; } = null!;

    // Số dòng MES giống hệt nhau (cùng vân tay)
    [Column("so_dong")]
    public int SoDong { get; set; }

    [Column("ma_phoi_mau")]
    [StringLength(50)]
    public string? MaPhoiMau { get; set; }

    [Column("ma_khach_cuoi")]
    [StringLength(50)]
    public string? MaKhachCuoi { get; set; }

    [Column("ma_khach_hang")]
    [StringLength(50)]
    public string? MaKhachHang { get; set; }

    [Column("ma_hang")]
    [StringLength(100)]
    public string? MaHang { get; set; }

    [Column("ma_mau")]
    [StringLength(100)]
    public string? MaMau { get; set; }

    [Column("so_phieu_nhuom")]
    [StringLength(50)]
    public string? SoPhieuNhuom { get; set; }

    [Column("so_phieu_dong_goi")]
    [StringLength(50)]
    public string? SoPhieuDongGoi { get; set; }

    [Column("ngay_dong_goi", TypeName = "datetime")]
    public DateTime? NgayDongGoi { get; set; }

    [Column("du_lieu_mes")]
    public string DuLieuMes { get; set; } = null!;

    [Column("ngay_dau_thay", TypeName = "datetime")]
    public DateTime NgayDauThay { get; set; }

    [Column("ngay_thay_cuoi", TypeName = "datetime")]
    public DateTime NgayThayCuoi { get; set; }

    [Column("ngay_mat", TypeName = "datetime")]
    public DateTime? NgayMat { get; set; }
}

// Trạng thái đồng bộ từng ngày đóng gói
[Table("QA_MesDongBoNgay")]
public partial class QaMesDongBoNgay
{
    [Key]
    [Column("ngay")]
    public DateOnly Ngay { get; set; }

    [Column("tong_mes")]
    public int TongMes { get; set; }

    [Column("so_dong")]
    public int SoDong { get; set; }

    [Column("day_du")]
    public bool DayDu { get; set; }

    [Column("ngay_dong_bo", TypeName = "datetime")]
    public DateTime NgayDongBo { get; set; }
}
