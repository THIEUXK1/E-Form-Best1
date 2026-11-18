using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Check xuất hàng: ghép dữ liệu xuất kho NH-WMS (出仓查询, API rwms/cpOtherOutM/MdList) với thông tin đơn hàng
    /// NH-MES (订单综合查询, API plan/saleItem/list) để có đủ các cột QA cần cho báo cáo khiếu nại khách hàng
    /// (制衣厂, 终客, PO, 制单号, 货号, 宽度, 颜色, 内部色号, 出货数量).
    /// Không lưu DB — mỗi lần tra gọi thẳng 2 hệ thống (cùng phiên SSO của MesApiService).
    /// </summary>
    public class CheckXuatHangService
    {
        public enum LoaiMa { TuNhan, Barcode, MaLot, PhieuXuat, HoaDon }

        // 1 cuộn/hộp đã xuất (1 dòng WMS)
        public record DongXuat(
            string? HoaDon, string? PhieuXuat, string? Barcode, string? MaLot, string? MaKhachHang, string? KhachCuoi,
            string? MaHangBan, string? MaHang, string? MaMau, string? Loai, decimal? SlXuat, decimal? SlKhach,
            decimal? KlTinh, decimal? KlCaBi, string? NgayNhapKho, string? NgayXuat, string? LoaiXuat, string? ViTri,
            string? HinhThucGiao, string? DiaChiGiao);

        // 1 mã lot: thông tin đơn hàng MES + tổng hợp xuất kho
        public record DongLot(
            string MaLot, bool CoDonHang, string? NhaMay, string? TenNhaMay, string? KhachCuoi, string? TenKhachCuoi,
            string? Po, string? MaHangGoc, string? MaHang, string? MauKhach, string? MaMauNoiBo, string? MaHangKhach,
            string? Mua, string? TrangThai, decimal? SlDon, string? NgayYeuCau,
            int SoCuon, decimal SlXuat, decimal SlKhach, List<string> DsHoaDon, string? NgayXuatDau, string? NgayXuatCuoi);

        public record KetQua(bool ThanhCong, string ThongBao, LoaiMa Loai = LoaiMa.TuNhan, List<DongLot>? DsLot = null,
            List<DongXuat>? DsXuat = null, int TongXuat = 0, string? CanhBao = null);

        // WMS mỗi lần lấy tối đa 1000 dòng; 1 hoá đơn/phiếu xuất thường vài chục–vài trăm cuộn, trần để mã gõ sai không kéo cả kho
        private const int DongMoiLanGoi = 1000;
        private const int TranDongXuat = 5000;
        // Số lot tra MES tối đa cho 1 lần (mỗi lot 1 lần gọi)
        private const int TranLot = 60;

        private readonly MesApiService _mes;
        private readonly IConfiguration _configuration;

        public CheckXuatHangService(MesApiService mes, IConfiguration configuration)
        {
            _mes = mes;
            _configuration = configuration;
        }

        private string FactoryCode => (_configuration["MesApi:FactoryCode"] ?? "BPVN").Trim();

        /// <summary>Đoán loại mã: VCH… = phiếu xuất, P + số = mã lot, toàn số = barcode, còn lại (VN2010…) = hoá đơn.</summary>
        public static LoaiMa NhanLoai(string ma)
        {
            if (ma.StartsWith("VCH", StringComparison.OrdinalIgnoreCase)) return LoaiMa.PhieuXuat;
            if (Regex.IsMatch(ma, "^[Pp]\\d{6,}$")) return LoaiMa.MaLot;
            if (Regex.IsMatch(ma, "^\\d{8,}$")) return LoaiMa.Barcode;
            return LoaiMa.HoaDon;
        }

        public async Task<KetQua> TraCuuAsync(string ma, LoaiMa loai, CancellationToken ct)
        {
            ma = ma.Trim();
            if (ma.Length == 0) return new(false, "Nhập mã cần tra.");
            if (ma.Length > 50) return new(false, "Mã quá dài.");
            if (loai == LoaiMa.TuNhan) loai = NhanLoai(ma);
            if (loai == LoaiMa.MaLot) ma = ma.ToUpperInvariant();

            // Bộ lọc WMS: tên trường đúng như trang 出仓查询 gửi lên; phải luôn có 1 điều kiện (để trống = cả triệu dòng)
            var loc = new Dictionary<string, object?>
            {
                ["barcode"] = loai == LoaiMa.Barcode ? ma : null,
                ["ordCode"] = loai == LoaiMa.MaLot ? ma : null,
                ["outCode"] = loai == LoaiMa.PhieuXuat ? ma : null,
                ["invCode"] = loai == LoaiMa.HoaDon ? ma : null,
            };

            var (okXuat, loiXuat, tongXuat, dsXuat) = await LayXuatKhoAsync(loc, ct);
            if (!okXuat) return new(false, loiXuat, loai);

            // Lot cần tra đơn hàng: lấy từ dữ liệu xuất; tra theo lot thì luôn tra dù lot chưa xuất
            var dsMaLot = dsXuat.Select(x => x.MaLot).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (loai == LoaiMa.MaLot && !dsMaLot.Contains(ma, StringComparer.OrdinalIgnoreCase)) dsMaLot.Insert(0, ma);

            if (dsMaLot.Count == 0)
                return new(true, "Không tìm thấy dữ liệu xuất kho cho mã này trên NH-WMS.", loai, new(), dsXuat, tongXuat);

            string? canhBao = tongXuat > dsXuat.Count ? $"Có {tongXuat:N0} dòng xuất, chỉ lấy {dsXuat.Count:N0} dòng đầu." : null;
            if (dsMaLot.Count > TranLot)
            {
                canhBao = (canhBao == null ? "" : canhBao + " ") + $"Có {dsMaLot.Count} mã lot, chỉ tra đơn hàng {TranLot} lot đầu.";
                dsMaLot = dsMaLot.Take(TranLot).ToList();
            }

            // Tra MES song song tối đa 4 lot một lúc cho nhanh mà không dồn MES
            var donHang = new Dictionary<string, JsonElement?>(StringComparer.OrdinalIgnoreCase);
            string? loiMes = null;
            using (var khoa = new SemaphoreSlim(4))
            {
                var viec = dsMaLot.Select(async lot =>
                {
                    await khoa.WaitAsync(ct);
                    try { return (lot, kq: await LayDonHangAsync(lot, ct)); }
                    finally { khoa.Release(); }
                }).ToList();
                foreach (var (lot, kq) in await Task.WhenAll(viec))
                {
                    if (!kq.ok) loiMes ??= kq.loi;
                    donHang[lot] = kq.dong;
                }
            }
            if (loiMes != null && donHang.Values.All(x => x == null))
                canhBao = (canhBao == null ? "" : canhBao + " ") + "Không lấy được thông tin đơn hàng NH-MES: " + loiMes;

            var dsLot = dsMaLot.Select(lot =>
            {
                var cuon = dsXuat.Where(x => string.Equals(x.MaLot, lot, StringComparison.OrdinalIgnoreCase)).ToList();
                var ngayXuat = cuon.Select(x => x.NgayXuat).Where(x => !string.IsNullOrEmpty(x)).OrderBy(x => x).ToList();
                var d = donHang.GetValueOrDefault(lot);
                string? S(string ten) => d is JsonElement e ? Chuoi(e, ten) : null;
                decimal? N(string ten) => d is JsonElement e ? So(e, ten) : null;
                return new DongLot(
                    lot, d != null,
                    S("custCode") ?? cuon.FirstOrDefault()?.MaKhachHang, S("cnName"),
                    S("endcustCode") ?? cuon.FirstOrDefault()?.KhachCuoi, S("customerEndName"),
                    S("custPoCode"), S("rootArt"), S("itemCode") ?? cuon.FirstOrDefault()?.MaHang, S("custColor"),
                    S("colorCode") ?? cuon.FirstOrDefault()?.MaMau, S("custItem"),
                    S("season"), TrangThaiDon(S("orderStatus")),
                    N("reqNumLtd"), NgayGon(S("reqDate")),
                    cuon.Count, cuon.Sum(x => x.SlXuat ?? 0), cuon.Sum(x => x.SlKhach ?? 0),
                    cuon.Select(x => x.HoaDon).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList(),
                    ngayXuat.FirstOrDefault(), ngayXuat.LastOrDefault());
            }).ToList();

            return new(true, "", loai, dsLot, dsXuat, tongXuat, canhBao);
        }

        // MES 订单状态: 1 在产, 2 销单 (giống bộ lọc trên trang MES)
        private static string? TrangThaiDon(string? orderStatus)
            => orderStatus switch { "1" => "Đang sản xuất (在产)", "2" => "Đã giao (销单)", _ => orderStatus };

        private async Task<(bool ok, string loi, int tong, List<DongXuat> ds)> LayXuatKhoAsync(Dictionary<string, object?> loc, CancellationToken ct)
        {
            var ds = new List<DongXuat>();
            int tong = 0;
            for (int trang = 1; ds.Count < TranDongXuat; trang++)
            {
                var body = new Dictionary<string, object?>(loc)
                {
                    ["sortOrder"] = "asc", ["pageSize"] = DongMoiLanGoi, ["pageNumber"] = trang,
                    ["shfs"] = Array.Empty<string>(), ["itemGrade"] = Array.Empty<string>(), ["outType"] = Array.Empty<string>(),
                    ["ltd"] = FactoryCode, ["invstartDate"] = "", ["invendDate"] = "", ["startDate"] = "", ["endDate"] = "",
                    ["outStatus"] = "", ["firstSearch"] = false
                };
                var kq = await _mes.GoiJsonAsync(true, "/rwms/cpOtherOutM/MdList", body, ct);
                if (!kq.ThanhCong) return (false, kq.ThongBao, 0, ds);

                using var doc = JsonDocument.Parse(kq.Json!);
                var goc = doc.RootElement;
                tong = goc.TryGetProperty("total", out var t) && t.TryGetInt32(out var tt) ? tt : 0;
                if (!goc.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0) break;
                foreach (var r in rows.EnumerateArray())
                {
                    ds.Add(new DongXuat(
                        Chuoi(r, "INV_CODE"), Chuoi(r, "OUT_CODE"), Chuoi(r, "BARCODE"), Chuoi(r, "ORD_CODE"),
                        Chuoi(r, "CUST_CODE"), Chuoi(r, "ENDCUST_CODE"), Chuoi(r, "SALE_ITEM_CODE"), Chuoi(r, "ITEM_CODE"),
                        Chuoi(r, "COLOR_CODE"), Chuoi(r, "ITEM_GRADE"), So(r, "ACTUAL_NUM_LTD"), So(r, "ACTUAL_NUM_CUST"),
                        So(r, "ACTUAL_GW"), So(r, "ACTUAL_NW"), NgayGio(Chuoi(r, "IN_DATE")), NgayGio(Chuoi(r, "OUT_DATE")),
                        TenLoaiXuat(Chuoi(r, "OUT_TYPE")), ViTriKho(r), Chuoi(r, "SHFS_NAME"), Chuoi(r, "SHADD")));
                }
                if (ds.Count >= tong) break;
            }
            if (ds.Count > TranDongXuat) ds.RemoveRange(TranDongXuat, ds.Count - TranDongXuat);
            return (true, "", tong, ds);
        }

        // Tra 1 lot trên 订单综合查询 (so khớp đúng mã, mọi trạng thái đơn)
        private async Task<(bool ok, string loi, JsonElement? dong)> LayDonHangAsync(string lot, CancellationToken ct)
        {
            var body = new
            {
                pageNumber = 1, pageSize = 5, ucode = lot, factoryCodes = new[] { FactoryCode },
                suppliers = Array.Empty<string>(), orderStatusList = Array.Empty<string>(),
                isLike = "notLike", firstSearch = false, sortOrder = "asc"
            };
            var kq = await _mes.GoiJsonAsync(false, "/plan/saleItem/list", body, ct);
            if (!kq.ThanhCong) return (false, kq.ThongBao, null);
            using var doc = JsonDocument.Parse(kq.Json!);
            if (doc.RootElement.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                foreach (var r in rows.EnumerateArray())
                    if (string.Equals(Chuoi(r, "ucode"), lot, StringComparison.OrdinalIgnoreCase))
                        return (true, "", r.Clone());
            return (true, "", null);
        }

        private static string? TenLoaiXuat(string? ma) => ma switch
        {
            "SL" => "Xuất bán (SL)", "MC" => "Đổi màu (MC)", "RQ" => "Làm lại (RQ)", _ => ma
        };

        // WMS trả vị trí dưới khoá lỗi "UPPER(D": {"WHLOCATION_CODE)": "..."} (alias SQL hỏng phía WMS)
        private static string? ViTriKho(JsonElement r)
        {
            if (r.TryGetProperty("WHLOCATION_CODE", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
            foreach (var p in r.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Object)
                    foreach (var q in p.Value.EnumerateObject())
                        if (q.Name.Contains("WHLOCATION_CODE") && q.Value.ValueKind == JsonValueKind.String) return q.Value.GetString();
            return null;
        }

        private static string? Chuoi(JsonElement e, string ten)
        {
            if (!e.TryGetProperty(ten, out var v)) return null;
            var s = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static decimal? So(JsonElement e, string ten)
        {
            if (!e.TryGetProperty(ten, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        // "2026-11-02 09:33:52.184" -> "2026-11-02 09:33"
        private static string? NgayGio(string? s) => s == null ? null : s.Length > 16 ? s[..16] : s;
        private static string? NgayGon(string? s) => s == null ? null : s.Length > 10 ? s[..10] : s;
    }
}
