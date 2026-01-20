using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CT_DonTiepKhac_5")]
public partial class CtDonTiepKhac5
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? AnhPhong { get; set; }

    public byte[]? AnhDatCom { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("CtDonTiepKhac5s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
