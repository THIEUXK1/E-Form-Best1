using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Năng lực áp dụng cho từng nhóm công việc (nhiều-nhiều như sheet "Nhóm công việc" của file tổng hợp);
/// DgNangLuc.IdNhomCongViec chỉ giữ được 1 nhóm nên bản đồ năng lực đọc bảng này.
/// </summary>
[Table("DG_NhomCongViecNangLuc")]
[PrimaryKey(nameof(IdNhomCongViec), nameof(IdNangLuc))]
public partial class DgNhomCongViecNangLuc
{
    [Column("id_nhom_cong_viec")]
    public Guid IdNhomCongViec { get; set; }

    [Column("id_nang_luc")]
    public Guid IdNangLuc { get; set; }
}
