using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_DangKiSuDungDTBan_4")]
[Index("IdFormIt", Name = "IX_IT_DangKiSuDungDTBan_4_idFormIT")]
public partial class ItDangKiSuDungDtban4
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    public string? ThongTin { get; set; }

    public string? MucDich { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItDangKiSuDungDtban4s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
