using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Một lượt cho mượn công cụ dụng cụ. Cho phép trả từng phần: trả đủ
/// (<see cref="SoLuongDaTra"/> == <see cref="SoLuongMuon"/>) thì phiếu được đóng bằng
/// <see cref="NgayTra"/>. Họ tên / bộ phận / công ty là ảnh chụp tại thời điểm mượn,
/// cố ý không join lại sang bảng User khi hiển thị để người đổi bộ phận về sau
/// không làm sai lệch phiếu mượn cũ.
/// </summary>
[Table("KK_CCDC_MuonTra")]
public partial class KkCcdcMuonTra
{
    [Key]
    [Column("id_muon")]
    public int IdMuon { get; set; }

    [Column("id_ccdc")]
    public int IdCcdc { get; set; }

    [Column("so_luong_muon")]
    public int SoLuongMuon { get; set; }

    [Column("so_luong_da_tra")]
    public int SoLuongDaTra { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaNhanVien { get; set; }

    [Column("id_nguoi_dung")]
    public int? IdNguoiDung { get; set; }

    [Column("ho_ten")]
    [StringLength(255)]
    public string? HoTen { get; set; }

    [Column("bo_phan")]
    [StringLength(255)]
    public string? BoPhan { get; set; }

    [Column("ten_cong_ty")]
    [StringLength(255)]
    public string? TenCongTy { get; set; }

    [Column("ngay_muon", TypeName = "datetime")]
    public DateTime NgayMuon { get; set; }

    [Column("ngay_hen_tra")]
    public DateOnly? NgayHenTra { get; set; }

    [Column("ngay_tra", TypeName = "datetime")]
    public DateTime? NgayTra { get; set; }

    [Column("tinh_trang_khi_tra")]
    [StringLength(100)]
    public string? TinhTrangKhiTra { get; set; }

    [Column("nguoi_cho_muon")]
    [StringLength(255)]
    public string? NguoiChoMuon { get; set; }

    [Column("nguoi_nhan_tra")]
    [StringLength(255)]
    public string? NguoiNhanTra { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [ForeignKey("IdCcdc")]
    public virtual KkCongCuDungCu? IdCcdcNavigation { get; set; }
}
