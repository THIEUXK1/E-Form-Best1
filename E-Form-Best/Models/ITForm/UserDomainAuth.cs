using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("UserDomainAuth")]
[Index("DomainUsername", Name = "IX_UserDomainAuth_Username")]
public partial class UserDomainAuth
{
    [Key]
    [Column("id_auth_domain")]
    public int IdAuthDomain { get; set; }

    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Column("domain_username")]
    [StringLength(256)]
    public string DomainUsername { get; set; } = null!;

    [Column("domain_sid")]
    [StringLength(128)]
    public string? DomainSid { get; set; }

    [Column("last_sync_date", TypeName = "datetime")]
    public DateTime? LastSyncDate { get; set; }

    [Column("login_mode")]
    public int LoginMode { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("UserDomainAuths")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
