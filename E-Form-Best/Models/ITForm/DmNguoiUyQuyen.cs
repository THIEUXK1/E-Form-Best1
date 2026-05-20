using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_NguoiUyQuyen")]
public partial class DmNguoiUyQuyen
{
    [Key]
    [Column("IDUyQuyen")]
    public int IduyQuyen { get; set; }

    [Column("MaNVUyQuyen")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaNvuyQuyen { get; set; }

    [StringLength(255)]
    public string? HoTenUyQuyen { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? EmailUyQuyen { get; set; }

    [InverseProperty("IduyQuyenNavigation")]
    public virtual ICollection<DmCtChiTietUyQuyen> DmCtChiTietUyQuyens { get; set; } = new List<DmCtChiTietUyQuyen>();
}
