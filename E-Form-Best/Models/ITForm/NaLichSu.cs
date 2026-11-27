using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>Nhật ký append-only phân hệ Nhà ăn (sửa/xoá thực đơn). KHÔNG sửa/xoá dòng cũ.</summary>
[Table("NA_LichSu")]
public partial class NaLichSu
{
    public const string DoiTuongThucDon = "THUC_DON";

    public const string Tao = "TAO";
    public const string CapNhat = "CAP_NHAT";
    public const string Xoa = "XOA";

    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("nguoi_thao_tac")]
    [StringLength(100)]
    public string? NguoiThaoTac { get; set; }

    [Column("doi_tuong")]
    [StringLength(20)]
    [Unicode(false)]
    public string DoiTuong { get; set; } = null!;

    [Column("id_doi_tuong")]
    public int IdDoiTuong { get; set; }

    [Column("hanh_dong")]
    [StringLength(20)]
    [Unicode(false)]
    public string HanhDong { get; set; } = null!;

    [Column("noi_dung")]
    [StringLength(2000)]
    public string? NoiDung { get; set; }
}
