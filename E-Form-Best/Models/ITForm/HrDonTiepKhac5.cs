using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DonTiepKhac_5")]
public partial class HrDonTiepKhac5
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? AnhPhong { get; set; }

    public byte[]? AnhDatCom { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonTiepKhac5s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
