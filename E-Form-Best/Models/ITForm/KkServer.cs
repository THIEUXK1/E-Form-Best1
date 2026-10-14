using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Server và thiết bị lưu trữ (NAS/SAN) do IT quản lý, chia theo 3 công ty (trang /QLServer/{công ty}).
/// Nhập tay như KK_Switch; 3 cột trạng thái kết nối do ServerPingWorker ghi, người dùng không sửa.
/// Soft-delete: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_Server")]
public partial class KkServer
{
    [Key]
    [Column("id_server")]
    public int IdServer { get; set; }

    [Column("ma_server")]
    [StringLength(100)]
    public string? MaServer { get; set; }

    [Column("ten_server")]
    [StringLength(255)]
    public string TenServer { get; set; } = null!;

    /// <summary>Server / Máy ảo / NAS / SAN — danh mục cố định ở QLServerController.DsLoai.</summary>
    [Column("loai")]
    [StringLength(50)]
    public string Loai { get; set; } = null!;

    /// <summary>Vai trò dịch vụ: AD/DNS, File server, ERP, Backup...</summary>
    [Column("chuc_nang")]
    [StringLength(255)]
    public string? ChucNang { get; set; }

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    /// <summary>iDRAC / iLO / IPMI / trang quản trị NAS.</summary>
    [Column("ip_quan_tri")]
    [StringLength(50)]
    [Unicode(false)]
    public string? IpQuanTri { get; set; }

    [Column("hang_san_xuat")]
    [StringLength(100)]
    public string? HangSanXuat { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("serial")]
    [StringLength(100)]
    public string? Serial { get; set; }

    [Column("he_dieu_hanh")]
    [StringLength(150)]
    public string? HeDieuHanh { get; set; }

    [Column("cpu")]
    [StringLength(150)]
    public string? Cpu { get; set; }

    [Column("ram_gb")]
    public int? RamGb { get; set; }

    /// <summary>Tổng dung lượng khả dụng (TB).</summary>
    [Column("dung_luong_tb", TypeName = "decimal(10, 2)")]
    public decimal? DungLuongTb { get; set; }

    /// <summary>Đã dùng (TB), nhập tay.</summary>
    [Column("da_dung_tb", TypeName = "decimal(10, 2)")]
    public decimal? DaDungTb { get; set; }

    [Column("raid")]
    [StringLength(100)]
    public string? Raid { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    /// <summary>Phòng máy / tủ rack / U.</summary>
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

    /// <summary>UP / DOWN theo lần ping gần nhất; NULL = chưa kiểm.</summary>
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
