using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Lượt cho mượn 1 dòng hàng trong thùng QA. Đang mượn = NgayTra null; trả thì điền NgayTra (không xoá, giữ lịch sử).
// Script: .claude/plans/sql/qa-thung-muon-20261117.sql
[Table("QA_ThungDongMuon")]
public partial class QaThungDongMuon
{
    [Key]
    [Column("id_muon")]
    public int IdMuon { get; set; }

    [Column("id_dong")]
    public int IdDong { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(50)]
    public string MaNhanVien { get; set; } = null!;

    // Họ tên chụp lại lúc cho mượn (User đổi tên / nghỉ sau này vẫn biết ai mượn)
    [Column("ten_nguoi_muon")]
    [StringLength(200)]
    public string? TenNguoiMuon { get; set; }

    [Column("ngay_muon", TypeName = "datetime")]
    public DateTime NgayMuon { get; set; }

    [Column("nguoi_cho_muon")]
    [StringLength(50)]
    public string? NguoiChoMuon { get; set; }

    [Column("ngay_tra", TypeName = "datetime")]
    public DateTime? NgayTra { get; set; }

    [Column("nguoi_nhan_tra")]
    [StringLength(50)]
    public string? NguoiNhanTra { get; set; }
}
