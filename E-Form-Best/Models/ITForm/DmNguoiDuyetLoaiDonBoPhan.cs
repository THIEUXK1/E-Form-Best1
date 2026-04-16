using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_NguoiDuyet_LoaiDon_BoPhan")]
public partial class DmNguoiDuyetLoaiDonBoPhan
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("STT")]
    public int? Stt { get; set; }

    [Column("IDLoaiDon")]
    public int? IdloaiDon { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [Column("IDNguoiXacNhan")]
    public int? IdnguoiXacNhan { get; set; }

    public string? GhiChu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [ForeignKey("IdboPhan")]
    [InverseProperty("DmNguoiDuyetLoaiDonBoPhans")]
    public virtual DmBoPhan? IdboPhanNavigation { get; set; }

    [ForeignKey("IdcongTy")]
    [InverseProperty("DmNguoiDuyetLoaiDonBoPhans")]
    public virtual DmCongTy? IdcongTyNavigation { get; set; }

    [ForeignKey("IdloaiDon")]
    [InverseProperty("DmNguoiDuyetLoaiDonBoPhans")]
    public virtual DmLoaiDon? IdloaiDonNavigation { get; set; }

    [ForeignKey("IdnguoiXacNhan")]
    [InverseProperty("DmNguoiDuyetLoaiDonBoPhans")]
    public virtual DmNguoiXacNhan? IdnguoiXacNhanNavigation { get; set; }
}
