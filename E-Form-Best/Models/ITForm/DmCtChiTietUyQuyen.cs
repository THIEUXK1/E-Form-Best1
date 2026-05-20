using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_CT_ChiTietUyQuyen")]
public partial class DmCtChiTietUyQuyen
{
    [Key]
    [Column("IDChiTiet")]
    public int IdchiTiet { get; set; }

    [Column("ID_CauHinhDuyet")]
    public int? IdCauHinhDuyet { get; set; }

    [Column("IDUyQuyen")]
    public int? IduyQuyen { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianBatDau { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianKetThuc { get; set; }

    public bool? TrangThai { get; set; }

    [ForeignKey("IdCauHinhDuyet")]
    [InverseProperty("DmCtChiTietUyQuyens")]
    public virtual DmNguoiDuyetLoaiDonBoPhan? IdCauHinhDuyetNavigation { get; set; }

    [ForeignKey("IduyQuyen")]
    [InverseProperty("DmCtChiTietUyQuyens")]
    public virtual DmNguoiUyQuyen? IduyQuyenNavigation { get; set; }
}
