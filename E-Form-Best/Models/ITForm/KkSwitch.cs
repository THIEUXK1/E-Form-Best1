using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Switch mạng do IT quản lý, chia theo 3 công ty (trang /QLSwitch/{công ty}). Nhập tay như KK_AccessPoint;
/// 3 cột trạng thái kết nối do SwitchPingWorker ghi, người dùng không sửa.
/// Soft-delete: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_Switch")]
public partial class KkSwitch
{
    [Key]
    [Column("id_switch")]
    public int IdSwitch { get; set; }

    [Column("ma_switch")]
    [StringLength(100)]
    public string? MaSwitch { get; set; }

    [Column("ten_switch")]
    [StringLength(255)]
    public string TenSwitch { get; set; } = null!;

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("dia_chi_mac")]
    [StringLength(20)]
    [Unicode(false)]
    public string? DiaChiMac { get; set; }

    [Column("hang_san_xuat")]
    [StringLength(100)]
    public string? HangSanXuat { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("serial")]
    [StringLength(100)]
    public string? Serial { get; set; }

    /// <summary>Số cổng mạng (24, 48...).</summary>
    [Column("so_cong")]
    public int? SoCong { get; set; }

    /// <summary>Core / Phân phối / Truy cập — danh mục cố định ở QLSwitchController.DsVaiTro.</summary>
    [Column("vai_tro")]
    [StringLength(50)]
    public string? VaiTro { get; set; }

    /// <summary>Firmware đang chạy, nhập tay.</summary>
    [Column("phien_ban")]
    [StringLength(100)]
    public string? PhienBan { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    /// <summary>Vị trí lắp (phòng / tủ rack).</summary>
    [Column("vi_tri")]
    [StringLength(500)]
    public string? ViTri { get; set; }

    [Column("tinh_trang")]
    [StringLength(100)]
    public string? TinhTrang { get; set; }

    [Column("ngay_lap_dat")]
    public DateOnly? NgayLapDat { get; set; }

    [Column("han_bao_hanh")]
    public DateOnly? HanBaoHanh { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    /// <summary>UP / DOWN theo lần ping gần nhất; NULL = chưa kiểm (chưa khai IP hoặc job chưa chạy tới).</summary>
    [Column("trang_thai_ket_noi")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThaiKetNoi { get; set; }

    [Column("doi_trang_thai_luc", TypeName = "datetime")]
    public DateTime? DoiTrangThaiLuc { get; set; }

    [Column("kiem_tra_luc", TypeName = "datetime")]
    public DateTime? KiemTraLuc { get; set; }

    [Column("nguoi_tao")]
    [StringLength(255)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("ly_do_xoa")]
    [StringLength(500)]
    public string? LyDoXoa { get; set; }

    [ForeignKey("IdcongTy")]
    public virtual KkCongTy? IdcongTyNavigation { get; set; }

    [ForeignKey("IdboPhan")]
    public virtual KkBoPhan? IdboPhanNavigation { get; set; }
}
