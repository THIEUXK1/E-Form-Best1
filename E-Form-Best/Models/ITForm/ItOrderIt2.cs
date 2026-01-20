using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_OrderIT_2")]
public partial class ItOrderIt2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    public string? GhiChu { get; set; }

    public byte[]? Anh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItOrderIt2s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
