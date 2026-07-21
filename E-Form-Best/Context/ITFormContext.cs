using System;
using System.Collections.Generic;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace E_Form_Best.Context;

public partial class ITFormContext : DbContext
{
    public ITFormContext()
    {
    }

    public ITFormContext(DbContextOptions<ITFormContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BaoVeHr> BaoVeHrs { get; set; }

    public virtual DbSet<BinhLuanFormCongViec> BinhLuanFormCongViecs { get; set; }

    public virtual DbSet<BinhLuanFormHr> BinhLuanFormHrs { get; set; }

    public virtual DbSet<BinhLuanFormIt> BinhLuanFormIts { get; set; }

    public virtual DbSet<DaoTao> DaoTaos { get; set; }

    public virtual DbSet<DaoTaoAnh> DaoTaoAnhs { get; set; }

    public virtual DbSet<DaoTaoQuanLyDuyet> DaoTaoQuanLyDuyets { get; set; }

    public virtual DbSet<DaoTaoNguoiThamGia> DaoTaoNguoiThamGias { get; set; }

    public virtual DbSet<DaoTaoTaiLieu> DaoTaoTaiLieus { get; set; }

    public virtual DbSet<LichSuFormDaoTao> LichSuFormDaoTaos { get; set; }

    public virtual DbSet<BinhLuanFormShd> BinhLuanFormShds { get; set; }

    public virtual DbSet<BoPhan> BoPhans { get; set; }

    public virtual DbSet<BoPhanQuyenTrungGian> BoPhanQuyenTrungGians { get; set; }

    public virtual DbSet<CongViecHr> CongViecHrs { get; set; }

    public virtual DbSet<CongViecIt> CongViecIts { get; set; }

    public virtual DbSet<CongViecShd> CongViecShds { get; set; }

    public virtual DbSet<CvCongViecOrder1> CvCongViecOrder1s { get; set; }

    public virtual DbSet<DanhGiaFormCongViec> DanhGiaFormCongViecs { get; set; }

    public virtual DbSet<DanhGiaFormIt> DanhGiaFormIts { get; set; }

    public virtual DbSet<DanhMucQuyenBoPhan> DanhMucQuyenBoPhans { get; set; }

    public virtual DbSet<DmBoPhan> DmBoPhans { get; set; }

    public virtual DbSet<DmCongTy> DmCongTies { get; set; }

    public virtual DbSet<DmCtChiTietUyQuyen> DmCtChiTietUyQuyens { get; set; }

    public virtual DbSet<DmLoaiDon> DmLoaiDons { get; set; }

    public virtual DbSet<DmNguoiDuyetLoaiDonBoPhan> DmNguoiDuyetLoaiDonBoPhans { get; set; }

    public virtual DbSet<DmNguoiUyQuyen> DmNguoiUyQuyens { get; set; }

    public virtual DbSet<DmNguoiXacNhan> DmNguoiXacNhans { get; set; }

    public virtual DbSet<DmNguoiXacNhanLoaiDon> DmNguoiXacNhanLoaiDons { get; set; }

    public virtual DbSet<FormCongViec> FormCongViecs { get; set; }

    public virtual DbSet<FormCongViecNguoiLienQuan> FormCongViecNguoiLienQuans { get; set; }

    public virtual DbSet<FormHr> FormHrs { get; set; }

    public virtual DbSet<FormIt> FormIts { get; set; }

    public virtual DbSet<FormShd> FormShds { get; set; }

    public virtual DbSet<HrBaoVeXacNhan> HrBaoVeXacNhans { get; set; }

    public virtual DbSet<HrCtNguoiHoTro> HrCtNguoiHoTros { get; set; }

    public virtual DbSet<HrDangKySuDungXeCongTac3> HrDangKySuDungXeCongTac3s { get; set; }

    public virtual DbSet<HrDangKySuDungXeDaily4> HrDangKySuDungXeDaily4s { get; set; }

    public virtual DbSet<HrDoiCaLam8> HrDoiCaLam8s { get; set; }

    public virtual DbSet<HrDonHoTroCongTac9> HrDonHoTroCongTac9s { get; set; }

    public virtual DbSet<HrDonKiTucXa10> HrDonKiTucXa10s { get; set; }

    public virtual DbSet<HrDonLamLaiThe11> HrDonLamLaiThe11s { get; set; }

    public virtual DbSet<HrDonSuDungDienThoai12> HrDonSuDungDienThoai12s { get; set; }

    public virtual DbSet<HrDonTiepKhac5> HrDonTiepKhac5s { get; set; }

    public virtual DbSet<HrHoTroTienDienThoai7> HrHoTroTienDienThoai7s { get; set; }

    public virtual DbSet<HrMangHangHoaRaCong2> HrMangHangHoaRaCong2s { get; set; }

    public virtual DbSet<HrNguoiHoTro> HrNguoiHoTros { get; set; }

    public virtual DbSet<HrNguoiXacNhan> HrNguoiXacNhans { get; set; }

    public virtual DbSet<HrNhaThauQuaCong6> HrNhaThauQuaCong6s { get; set; }

    public virtual DbSet<HrQuanLyDuyetB2> HrQuanLyDuyetB2s { get; set; }

    public virtual DbSet<HrQuanLyDuyetB2UyQuyen> HrQuanLyDuyetB2UyQuyens { get; set; }

    public virtual DbSet<HrXinRaNgoai1> HrXinRaNgoai1s { get; set; }

    public virtual DbSet<ItCapQuyenOchung8> ItCapQuyenOchung8s { get; set; }

    public virtual DbSet<ItCtNguoiHoTro> ItCtNguoiHoTros { get; set; }

    public virtual DbSet<ItDangKiSuDungDtban4> ItDangKiSuDungDtban4s { get; set; }

    public virtual DbSet<ItDangKiSuDungWifi3> ItDangKiSuDungWifi3s { get; set; }

    public virtual DbSet<ItDangKiTaiKhoanHeThong5> ItDangKiTaiKhoanHeThong5s { get; set; }

    public virtual DbSet<ItDangkiTaiKhoanMayTinh6> ItDangkiTaiKhoanMayTinh6s { get; set; }

    public virtual DbSet<ItDonLapDatThietBi7> ItDonLapDatThietBi7s { get; set; }

    public virtual DbSet<ItMail1> ItMail1s { get; set; }

    public virtual DbSet<ItNguoiHoTro> ItNguoiHoTros { get; set; }

    public virtual DbSet<ItOrderIt2> ItOrderIt2s { get; set; }

    public virtual DbSet<ItXacNhanCapQuyen8> ItXacNhanCapQuyen8s { get; set; }

    public virtual DbSet<KkBangChungCheck> KkBangChungChecks { get; set; }

    public virtual DbSet<KkBoPhan> KkBoPhans { get; set; }

    public virtual DbSet<KkCongTy> KkCongTies { get; set; }

    public virtual DbSet<KkLichSuThaoTac> KkLichSuThaoTacs { get; set; }

    public virtual DbSet<KkLoaiThietBi> KkLoaiThietBis { get; set; }

    public virtual DbSet<KkThietBi> KkThietBis { get; set; }

    public virtual DbSet<KkTrangThai> KkTrangThais { get; set; }

    public virtual DbSet<LichSuFormCongViec> LichSuFormCongViecs { get; set; }

    public virtual DbSet<LichSuFormHr> LichSuFormHrs { get; set; }

    public virtual DbSet<LichSuFormIt> LichSuFormIts { get; set; }

    public virtual DbSet<LichSuFormShd> LichSuFormShds { get; set; }

    public virtual DbSet<LichSuTruyCap> LichSuTruyCaps { get; set; }

    public virtual DbSet<PhongHopHr> PhongHopHrs { get; set; }

    public virtual DbSet<Quyen> Quyens { get; set; }

    public virtual DbSet<ShdCtNguoiHoTro> ShdCtNguoiHoTros { get; set; }

    public virtual DbSet<ShdDangKySuDungXeCongTac1> ShdDangKySuDungXeCongTac1s { get; set; }

    public virtual DbSet<ShdDangKySuDungXeDaily2> ShdDangKySuDungXeDaily2s { get; set; }

    public virtual DbSet<ShdNguoiHoTro> ShdNguoiHoTros { get; set; }

    public virtual DbSet<ShdNguoiXacNhan> ShdNguoiXacNhans { get; set; }

    public virtual DbSet<ShdQuanLyDuyetB2> ShdQuanLyDuyetB2s { get; set; }

    public virtual DbSet<ShdQuanLyDuyetB2UyQuyen> ShdQuanLyDuyetB2UyQuyens { get; set; }

    public virtual DbSet<TscnChiTietMacWifi> TscnChiTietMacWifis { get; set; }

    public virtual DbSet<TscnChiTietManHinh> TscnChiTietManHinhs { get; set; }

    public virtual DbSet<TscnChiTietOcung> TscnChiTietOcungs { get; set; }

    public virtual DbSet<TscnChiTietRam> TscnChiTietRams { get; set; }

    public virtual DbSet<TscnLichSuThayDoi> TscnLichSuThayDois { get; set; }

    public virtual DbSet<TscnLichSuXacThucAdmin> TscnLichSuXacThucAdmins { get; set; }

    public virtual DbSet<TscnLichSuXacThucNguoiDung> TscnLichSuXacThucNguoiDungs { get; set; }

    public virtual DbSet<TscnThongTinMay> TscnThongTinMays { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBoPhan> UserBoPhans { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    public virtual DbSet<UserDomainAuth> UserDomainAuths { get; set; }

    public virtual DbSet<UserQuyen> UserQuyens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(GetConnectionStringFromConfig());

    // Đọc connection string từ appsettings.json / biến môi trường thay vì hardcode trong source code.
    // Cần thiết vì các controller tạo context bằng "new ITFormContext()" (không qua DI) nên không có
    // DbContextOptions sẵn để dùng - OnConfiguring phải tự nạp cấu hình.
    private static string GetConnectionStringFromConfig()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong cấu hình (appsettings.json hoặc biến môi trường ConnectionStrings__DefaultConnection).");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BaoVeHr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BaoVeHr__3213E83FC2ADD887");

            entity.Property(e => e.TimeBaoVe).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(0);

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.BaoVeHrs).HasConstraintName("FK_BaoVeHr_FormHR");
        });

        modelBuilder.Entity<BinhLuanFormCongViec>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BinhLuan__3213E83F5EC9014F");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.BinhLuanFormCongViecs)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BinhLuanFormCongViec_Form");
        });

        modelBuilder.Entity<BinhLuanFormHr>(entity =>
        {
            entity.Property(e => e.ThoiGian).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.BinhLuanFormHrs).HasConstraintName("FK_BinhLuanFormHR_FormHR");
        });

        modelBuilder.Entity<BinhLuanFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BinhLuan__3213E83F980A0B9F");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.BinhLuanFormIts).HasConstraintName("FK_BinhLuan_Form");
        });

        modelBuilder.Entity<BinhLuanFormShd>(entity =>
        {
            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.BinhLuanFormShds)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BinhLuan_FormSHD");
        });

        modelBuilder.Entity<BoPhan>(entity =>
        {
            entity.HasKey(e => e.IdBoPhan).HasName("PK__BoPhan__E66DCED5E953B918");
        });

        modelBuilder.Entity<BoPhanQuyenTrungGian>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BoPhan_Q__3213E83F41006A03");

            entity.Property(e => e.ChoPhep).HasDefaultValue(true);
            entity.Property(e => e.NgayGan).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdBoPhanNavigation).WithMany(p => p.BoPhanQuyenTrungGians).HasConstraintName("FK_TrungGian_BoPhan");

            entity.HasOne(d => d.IdQuyenNavigation).WithMany(p => p.BoPhanQuyenTrungGians).HasConstraintName("FK_TrungGian_QuyenBoPhan");
        });

        modelBuilder.Entity<CongViecHr>(entity =>
        {
            entity.HasOne(d => d.IdHrNguoiHoTroNavigation).WithMany(p => p.CongViecHrs).HasConstraintName("FK_CongViecHR_HR_NguoiHoTro");
        });

        modelBuilder.Entity<CongViecIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CongViec__3213E83F5D615D57");

            entity.HasOne(d => d.IdItNguoiHoTroNavigation).WithMany(p => p.CongViecIts).HasConstraintName("FK_CongViec_NguoiHoTro");
        });

        modelBuilder.Entity<CvCongViecOrder1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CongViec__3213E83FAEF11AEC");

            entity.HasOne(d => d.IdFormCongViecNavigation).WithMany(p => p.CvCongViecOrder1s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CongViecOrder1_Form");
        });

        modelBuilder.Entity<DanhGiaFormCongViec>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhGiaF__3213E83F62D9E74A");
        });

        modelBuilder.Entity<DanhGiaFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhGia__3213E83F747EAFE3");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.DanhGiaFormIts).HasConstraintName("FK_DanhGia_FormIT");
        });

        modelBuilder.Entity<DanhMucQuyenBoPhan>(entity =>
        {
            entity.HasKey(e => e.IdQuyen).HasName("PK__DanhMucQ__AE8CD30FCA670FDF");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<DmBoPhan>(entity =>
        {
            entity.HasKey(e => e.IdboPhan).HasName("PK__DM_BoPha__0E503FF59DA33DC2");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DmCongTy>(entity =>
        {
            entity.HasKey(e => e.IdcongTy).HasName("PK__DM_CongT__3E616C9013DB38FC");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DmCtChiTietUyQuyen>(entity =>
        {
            entity.HasKey(e => e.IdchiTiet).HasName("PK__DM_CT_Ch__EF38009B7B840251");

            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdCauHinhDuyetNavigation).WithMany(p => p.DmCtChiTietUyQuyens).HasConstraintName("FK_DM_CT_ChiTietUyQuyen_CauHinh");

            entity.HasOne(d => d.IduyQuyenNavigation).WithMany(p => p.DmCtChiTietUyQuyens).HasConstraintName("FK_DM_CT_ChiTietUyQuyen_NguoiUyQuyen");
        });

        modelBuilder.Entity<DmLoaiDon>(entity =>
        {
            entity.HasKey(e => e.IdloaiDon).HasName("PK__DM_LoaiD__712924D0B1F6E7F1");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DmNguoiDuyetLoaiDonBoPhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DM_Nguoi__3214EC27AFBA2FD0");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdboPhanNavigation).WithMany(p => p.DmNguoiDuyetLoaiDonBoPhans).HasConstraintName("FK_NguoiDuyet_BoPhan");

            entity.HasOne(d => d.IdcongTyNavigation).WithMany(p => p.DmNguoiDuyetLoaiDonBoPhans).HasConstraintName("FK_NguoiDuyet_CongTy");

            entity.HasOne(d => d.IdloaiDonNavigation).WithMany(p => p.DmNguoiDuyetLoaiDonBoPhans).HasConstraintName("FK_NguoiDuyet_LoaiDon");

            entity.HasOne(d => d.IdnguoiXacNhanNavigation).WithMany(p => p.DmNguoiDuyetLoaiDonBoPhans).HasConstraintName("FK_NguoiDuyet_NguoiXacNhan");
        });

        modelBuilder.Entity<DmNguoiUyQuyen>(entity =>
        {
            entity.HasKey(e => e.IduyQuyen).HasName("PK__DM_Nguoi__9201248E9A4870F2");
        });

        modelBuilder.Entity<DmNguoiXacNhan>(entity =>
        {
            entity.HasKey(e => e.IdnguoiXacNhan).HasName("PK__DM_Nguoi__ED07D2F9CFE1E4C7");

            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DmNguoiXacNhanLoaiDon>(entity =>
        {
            entity.HasKey(e => e.Idrel).HasName("PK__DM_Nguoi__A681937ED3C6173D");

            entity.Property(e => e.CapDoXacNhan).HasDefaultValue(1);

            entity.HasOne(d => d.IdloaiDonNavigation).WithMany(p => p.DmNguoiXacNhanLoaiDons).HasConstraintName("FK_Rel_LoaiDon");

            entity.HasOne(d => d.IdnguoiXacNhanNavigation).WithMany(p => p.DmNguoiXacNhanLoaiDons).HasConstraintName("FK_Rel_NguoiXacNhan");
        });

        modelBuilder.Entity<FormCongViec>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormCong__3213E83F921E2CBE");
        });

        modelBuilder.Entity<FormCongViecNguoiLienQuan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormCong__3213E83F2DD38C95");

            entity.Property(e => e.NgayGan).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormCongViecNavigation).WithMany(p => p.FormCongViecNguoiLienQuans).HasConstraintName("FK_NguoiLienQuan_Form");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.FormCongViecNguoiLienQuans).HasConstraintName("FK_NguoiLienQuan_User");
        });

        modelBuilder.Entity<FormHr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormHR__3213E83F63FD39F1");
        });

        modelBuilder.Entity<FormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormIT__3213E83F25A63261");
        });

        modelBuilder.Entity<HrBaoVeXacNhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HR_CT_Ba__3213E83F2C627C82");

            entity.Property(e => e.ThoiGianHeThong).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrBaoVeXacNhans).HasConstraintName("FK_BaoVeXacNhan_FormHR");
        });

        modelBuilder.Entity<HrCtNguoiHoTro>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrCtNguoiHoTros).HasConstraintName("FK_HR_CT_NguoiHoTro_FormHR");

            entity.HasOne(d => d.IdHrNguoiHoTroNavigation).WithMany(p => p.HrCtNguoiHoTros).HasConstraintName("FK_HR_CT_NguoiHoTro_NguoiHoTro");
        });

        modelBuilder.Entity<HrDangKySuDungXeCongTac3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F55A9ACA6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDangKySuDungXeCongTac3s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DangKy_FormHR");
        });

        modelBuilder.Entity<HrDangKySuDungXeDaily4>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F3E15E888");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDangKySuDungXeDaily4s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_Daily_FormHR");
        });

        modelBuilder.Entity<HrDoiCaLam8>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DoiCa__3213E83F1067B20A");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDoiCaLam8s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_DoiCaLam_8_FormHR");
        });

        modelBuilder.Entity<HrDonHoTroCongTac9>(entity =>
        {
            entity.Property(e => e.BookXeCtyDuaDon).HasDefaultValue(false);
            entity.Property(e => e.DatBuaAn).HasDefaultValue(false);
            entity.Property(e => e.DatChoO).HasDefaultValue(false);
            entity.Property(e => e.DatVeMayBay).HasDefaultValue(false);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonHoTroCongTac9s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DonHoTro_FormHR");
        });

        modelBuilder.Entity<HrDonKiTucXa10>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonKiTucXa10s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HR_DonKiTucXa_FormHR");
        });

        modelBuilder.Entity<HrDonLamLaiThe11>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonLamLaiThe11s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HR_DonLamLaiThe_FormHR");
        });

        modelBuilder.Entity<HrDonSuDungDienThoai12>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonSuDungDienThoai12s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HR_DonSuDungDienThoai_FormHR");
        });

        modelBuilder.Entity<HrDonTiepKhac5>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DonTi__3213E83FDE6F0C24");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonTiepKhac5s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DonTiep_FormHR");
        });

        modelBuilder.Entity<HrHoTroTienDienThoai7>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_CT_HoTroTienDienThoai_7");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrHoTroTienDienThoai7s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_HoTroTienDienThoai_7_FormHR");
        });

        modelBuilder.Entity<HrMangHangHoaRaCong2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_MangH__3213E83F50B03DD7");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrMangHangHoaRaCong2s).HasConstraintName("FK_CT_MangHangHoa_FormHR");
        });

        modelBuilder.Entity<HrNguoiXacNhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HR_Nguoi__3213E83F7AE260F9");

            entity.Property(e => e.ThuTuXacNhan).HasDefaultValue(1);
            entity.Property(e => e.TrangThaiXacNhan).HasDefaultValue(0);

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrNguoiXacNhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HR_FormHR");

            entity.HasOne(d => d.IdnguoiXacNhanNavigation).WithMany(p => p.HrNguoiXacNhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HR_DM_NguoiXacNhan");
        });

        modelBuilder.Entity<HrNhaThauQuaCong6>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_NhaTh__3213E83F93B09EB6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrNhaThauQuaCong6s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_NhaThau_FormHR");
        });

        modelBuilder.Entity<HrQuanLyDuyetB2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HR_QuanL__3213E83FD6554556");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrQuanLyDuyetB2s).HasConstraintName("FK_HR_QuanLyDuyetB2_FormHR");
        });

        modelBuilder.Entity<HrQuanLyDuyetB2UyQuyen>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HR_QuanL__3213E83F4D23E7F1");

            entity.HasOne(d => d.IdHrQuanLyDuyetB2Navigation).WithMany(p => p.HrQuanLyDuyetB2UyQuyens).HasConstraintName("FK_HR_DuyetB2_NguoiUyQuyen");
        });

        modelBuilder.Entity<HrXinRaNgoai1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_XinRa__3213E83F517B9771");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrXinRaNgoai1s).HasConstraintName("FK_CT_XinRaNgoai_FormHR");
        });

        modelBuilder.Entity<ItCapQuyenOchung8>(entity =>
        {
            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItCapQuyenOchung8s).HasConstraintName("FK_IT_CapQuyenOChung_8_FormIT");
        });

        modelBuilder.Entity<ItCtNguoiHoTro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_CT_Ng__3213E83F994EF380");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItCtNguoiHoTros).HasConstraintName("FK_CT_FormIT");

            entity.HasOne(d => d.IdItNguoiHoTroNavigation).WithMany(p => p.ItCtNguoiHoTros).HasConstraintName("FK_CT_NguoiHoTro");
        });

        modelBuilder.Entity<ItDangKiSuDungDtban4>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_DangK__3213E83F76CB6499");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangKiSuDungDtban4s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITDTBan_FormIT");
        });

        modelBuilder.Entity<ItDangKiSuDungWifi3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_DangK__3213E83F8C22D6FB");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangKiSuDungWifi3s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITWifi_FormIT");
        });

        modelBuilder.Entity<ItDangKiTaiKhoanHeThong5>(entity =>
        {
            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangKiTaiKhoanHeThong5s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_IT_DangKiTaiKhoanHeThong_5_FormIT");
        });

        modelBuilder.Entity<ItDangkiTaiKhoanMayTinh6>(entity =>
        {
            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangkiTaiKhoanMayTinh6s).HasConstraintName("FK_IT_6_FormIT");
        });

        modelBuilder.Entity<ItDonLapDatThietBi7>(entity =>
        {
            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDonLapDatThietBi7s).HasConstraintName("FK_IT_DonLapDatThietBi_7_FormIT");
        });

        modelBuilder.Entity<ItMail1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Mail___3213E83F52160549");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItMail1s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITMail_FormIT");
        });

        modelBuilder.Entity<ItNguoiHoTro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Nguoi__3213E83F4E28386C");
        });

        modelBuilder.Entity<ItOrderIt2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Order__3213E83F4A9C39E3");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItOrderIt2s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITOrder_FormIT");
        });

        modelBuilder.Entity<ItXacNhanCapQuyen8>(entity =>
        {
            entity.HasOne(d => d.IdCapQuyenOchung8Navigation).WithMany(p => p.ItXacNhanCapQuyen8s).HasConstraintName("FK_IT_XacNhanCapQuyen_8_IT_CapQuyenOChung_8");
        });

        modelBuilder.Entity<KkBangChungCheck>(entity =>
        {
            entity.HasKey(e => e.IdBangChung).HasName("PK__KK_BangC__BE1C75A93526C6CE");

            entity.Property(e => e.ThoiGianCheck).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdThietBiNavigation).WithMany(p => p.KkBangChungChecks).HasConstraintName("FK_BangChungCheck_ThietBi");
        });

        modelBuilder.Entity<KkBoPhan>(entity =>
        {
            entity.HasKey(e => e.IdboPhan).HasName("PK__KK_BoPha__0E503FF5B3982733");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdcongTyNavigation).WithMany(p => p.KkBoPhans)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KK_BoPhan_KK_CongTy");
        });

        modelBuilder.Entity<KkCongTy>(entity =>
        {
            entity.HasKey(e => e.IdcongTy).HasName("PK__KK_CongT__3E616C9080673D61");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<KkLichSuThaoTac>(entity =>
        {
            entity.HasKey(e => e.IdLichSu).HasName("PK__KK_LichS__823B17723D03585D");

            entity.Property(e => e.ThoiGian).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<KkLoaiThietBi>(entity =>
        {
            entity.HasKey(e => e.IdLoai).HasName("PK__DM_LoaiT__9A03AA723C838926");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<KkThietBi>(entity =>
        {
            entity.HasKey(e => e.IdThietBi).HasName("PK__KK_Thiet__68D7BE4D3451C805");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.KkThietBis).HasConstraintName("FK_KK_ThietBi_TSCN_ThongTinMay");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.KkThietBis)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KK_ThietBi_User");

            entity.HasOne(d => d.IdTrangThaiNavigation).WithMany(p => p.KkThietBis).HasConstraintName("FK_ThietBi_TrangThai");

            entity.HasOne(d => d.IdboPhanNavigation).WithMany(p => p.KkThietBis)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KK_ThietBi_KK_BoPhan");

            entity.HasOne(d => d.IdcongTyNavigation).WithMany(p => p.KkThietBis)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KK_ThietBi_KK_CongTy");
        });

        modelBuilder.Entity<KkTrangThai>(entity =>
        {
            entity.HasKey(e => e.IdTrangThai).HasName("PK__KK_Trang__D82677B0116A98B3");
        });

        modelBuilder.Entity<LichSuFormCongViec>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LichSuFo__3213E83FB7F6EAE4");

            entity.HasOne(d => d.IdFormCongViecNavigation).WithMany(p => p.LichSuFormCongViecs)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LichSuForm_Form");
        });

        modelBuilder.Entity<LichSuFormHr>(entity =>
        {
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Time).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThaiAnHien).HasDefaultValue(true, "DF_LichSuFormHR_TrangThaiAnHien");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.LichSuFormHrs).HasConstraintName("FK_LichSuFormHR_FormHR");
        });

        modelBuilder.Entity<LichSuFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LichSu__3213E83F7913614E");

            entity.Property(e => e.TrangThaiAnHien).HasDefaultValue(true, "DF_LichSuFormIT_TrangThaiAnHien");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.LichSuFormIts).HasConstraintName("FK_LichSu_FormIT");
        });

        modelBuilder.Entity<LichSuFormShd>(entity =>
        {
            entity.Property(e => e.TrangThaiAnHien).HasDefaultValue(true, "DF_LichSuFormSHD_TrangThaiAnHien");

            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.LichSuFormShds)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LichSuFormSHD_FormSHD");
        });

        modelBuilder.Entity<LichSuTruyCap>(entity =>
        {
            entity.HasKey(e => e.IdLichSu).HasName("PK__LichSuTr__016DB95DC467B7FB");

            entity.Property(e => e.ThoiGianDangNhap).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.LichSuTruyCaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LichSu_User");
        });

        modelBuilder.Entity<PhongHopHr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PhongHop__3213E83F30E5D681");
        });

        modelBuilder.Entity<Quyen>(entity =>
        {
            entity.HasKey(e => e.IdQuyen).HasName("PK__Quyen__AE8CD30F6693B373");
        });

        modelBuilder.Entity<ShdCtNguoiHoTro>(entity =>
        {
            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.ShdCtNguoiHoTros)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SHD_CT_NguoiHoTro_FormSHD");

            entity.HasOne(d => d.IdShdNguoiHoTroNavigation).WithMany(p => p.ShdCtNguoiHoTros).HasConstraintName("FK_SHD_CT_NguoiHoTro_NguoiHoTro");
        });

        modelBuilder.Entity<ShdDangKySuDungXeCongTac1>(entity =>
        {
            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.ShdDangKySuDungXeCongTac1s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_XeCongTac_FormSHD");
        });

        modelBuilder.Entity<ShdDangKySuDungXeDaily2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SHD_DangKySuDungXeDaily_1");

            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.ShdDangKySuDungXeDaily2s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_XeDaily_FormSHD");
        });

        modelBuilder.Entity<ShdNguoiXacNhan>(entity =>
        {
            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.ShdNguoiXacNhans)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SHD_NguoiXacNhan_FormSHD");

            entity.HasOne(d => d.IdnguoiXacNhanNavigation).WithMany(p => p.ShdNguoiXacNhans).HasConstraintName("FK_SHD_NguoiXacNhan_DM_NguoiXacNhan");
        });

        modelBuilder.Entity<ShdQuanLyDuyetB2>(entity =>
        {
            entity.HasOne(d => d.IdFormShdNavigation).WithMany(p => p.ShdQuanLyDuyetB2s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SHD_QuanLyDuyetB2_FormSHD");
        });

        modelBuilder.Entity<ShdQuanLyDuyetB2UyQuyen>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SHD_Quan__3213E83F9A1D16BE");

            entity.HasOne(d => d.IdShdQuanLyDuyetB2Navigation).WithMany(p => p.ShdQuanLyDuyetB2UyQuyens).HasConstraintName("FK_SHD_DuyetB2_NguoiUyQuyen");
        });

        modelBuilder.Entity<TscnChiTietMacWifi>(entity =>
        {
            entity.HasKey(e => e.IdMacWifi).HasName("PK__TSCN_Chi__2C086FBB20A85692");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnChiTietMacWifis)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__TSCN_ChiT__IdMay__1D4655FB");
        });

        modelBuilder.Entity<TscnChiTietManHinh>(entity =>
        {
            entity.HasKey(e => e.IdManHinh).HasName("PK__TSCN_Chi__A1FB5E394DB5A60B");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnChiTietManHinhs)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__TSCN_ChiT__IdMay__1A69E950");
        });

        modelBuilder.Entity<TscnChiTietOcung>(entity =>
        {
            entity.HasKey(e => e.IdOcung).HasName("PK__TSCN_Chi__611D073D98CC3B04");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnChiTietOcungs)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__TSCN_ChiT__IdMay__178D7CA5");
        });

        modelBuilder.Entity<TscnChiTietRam>(entity =>
        {
            entity.HasKey(e => e.IdRam).HasName("PK__TSCN_Chi__2A4A2E83DCEDEF8C");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnChiTietRams)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__TSCN_ChiT__IdMay__14B10FFA");
        });

        modelBuilder.Entity<TscnLichSuThayDoi>(entity =>
        {
            entity.HasKey(e => e.IdLog).HasName("PK__TSCN_Lic__0C54DBC6460C4AC8");

            entity.Property(e => e.ThoiGianThayDoi).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnLichSuThayDois).HasConstraintName("FK_TSCN_LichSuThayDoi_TSCN_ThongTinMay");
        });

        modelBuilder.Entity<TscnLichSuXacThucAdmin>(entity =>
        {
            entity.HasKey(e => e.IdLog).HasName("PK__TSCN_Lic__0C54DBC6E8DFB420");

            entity.Property(e => e.ThoiGianXacThuc).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnLichSuXacThucAdmins)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__TSCN_Lich__IdMay__2022C2A6");
        });

        modelBuilder.Entity<TscnLichSuXacThucNguoiDung>(entity =>
        {
            entity.HasKey(e => e.IdLichSu).HasName("PK__TSCN_Lic__823B1772A0B132B8");

            entity.Property(e => e.NgayXacThuc).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdMayNavigation).WithMany(p => p.TscnLichSuXacThucNguoiDungs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TSCN_Lich__IdMay__2AA05119");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.TscnLichSuXacThucNguoiDungs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TSCN_Lich__IdNgu__2B947552");
        });

        modelBuilder.Entity<TscnThongTinMay>(entity =>
        {
            entity.HasKey(e => e.IdMay).HasName("PK__TSCN_Tho__0D13B75918C6D82F");

            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.TscnThongTinMays)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TSCN_ThongTinMay_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdNguoiDung).HasName("PK__User__75D6A11EB2C54D7B");

            entity.Property(e => e.FailedAttempts).HasDefaultValue(0);
            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<UserBoPhan>(entity =>
        {
            entity.HasKey(e => new { e.IdNguoiDung, e.IdBoPhan }).HasName("PK__User_BoP__3BB07DF3A33D7C1A");

            entity.HasOne(d => d.IdBoPhanNavigation).WithMany(p => p.UserBoPhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BoPhan");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserBoPhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User");
        });

        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserDevi__3214EC07321181E5");

            entity.Property(e => e.IsTrusted).HasDefaultValue(true);
            entity.Property(e => e.LastLogin).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserDevices).HasConstraintName("FK_UserDevice_User");
        });

        modelBuilder.Entity<UserDomainAuth>(entity =>
        {
            entity.HasKey(e => e.IdAuthDomain).HasName("PK__UserDoma__47B83539BDB0667B");

            entity.Property(e => e.LastSyncDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserDomainAuths).HasConstraintName("FK_UserDomainAuth_User");
        });

        modelBuilder.Entity<UserQuyen>(entity =>
        {
            entity.HasKey(e => new { e.IdNguoiDung, e.IdQuyen }).HasName("PK__User_Quy__9F3E6C2E8D999CB5");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserQuyens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserQuyen_User_Current");

            entity.HasOne(d => d.IdQuyenNavigation).WithMany(p => p.UserQuyens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserQuyen_Quyen_Current");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
