using ClosedXML.Excel;
using static E_Form_Best.Areas.ITForm.Services.SwitchBaoCaoExcel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel báo cáo theo kỳ của /QLServer/TongQuan (tổng quan / 1 công ty). Chỉ trình bày, số liệu từ ServerBaoCaoService;
    /// khung bảng dùng chung SwitchBaoCaoExcel để 2 báo cáo cùng một kiểu.
    /// </summary>
    public static class ServerBaoCaoExcel
    {
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");
        private static readonly XLColor MauVang = XLColor.FromHtml("#b45309");

        // Bang() chỉ nhận int/long/double/DateTime là số — decimal phải đổi sang double, không thì thành chữ
        private static double? Tb(decimal? v) => v == null ? null : (double)v.Value;

        public static byte[] TaoBaoCao(ServerBaoCaoService.BaoCao bc, string? congTy, string? nguoiLap)
        {
            using var wb = new XLWorkbook();
            var tieuDe = congTy == null ? "BÁO CÁO TỔNG QUAN SERVER & LƯU TRỮ" : "BÁO CÁO SERVER & LƯU TRỮ " + congTy;
            var phuDe = PhuDe($"Kỳ báo cáo: {bc.TuNgay:dd/MM/yyyy} – {bc.DenNgay:dd/MM/yyyy} (so với {bc.KyTruocTu:dd/MM} – {bc.KyTruocDen:dd/MM/yyyy})   ·   Lập lúc: {bc.TaoLuc:HH:mm dd/MM/yyyy}", nguoiLap);

            var ws = TrangMoi(wb, "Báo cáo", new[] { 14, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 13, 14, 13 });
            var r = DauTrang(ws, tieuDe, phuDe, 14);
            r = TieuDeMuc(ws, r, "THEO CÔNG TY", 14);
            r = Bang(ws, r,
                new[] { "Công ty", "Tổng thiết bị", "Server / VM", "NAS / SAN", "Theo dõi", "Hoạt động", "Mất KN", "Phần cứng lỗi",
                        "Phần cứng cảnh báo", "Hết BH", "Sắp hết BH", "Lần mất KN", "Giờ mất KN", "% HĐ trong kỳ" },
                bc.CongTy.Select(c => new object?[] { c.CongTy, c.TongTaiSan, c.SoServer, c.SoLuuTru, c.Tong, c.HoatDong, c.MatKetNoi,
                                                      c.SucKhoeLoi, c.SucKhoeCanhBao, c.HetBaoHanh, c.SapHetBaoHanh, c.SoLanMat,
                                                      Math.Round(c.GiayMat / 3600.0, 1), c.TyLeTrongKy / 100 }).ToList(),
                new[] { null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0.0", "0.00%" });
            r++;

            r = TieuDeMuc(ws, r, $"SO VỚI KỲ TRƯỚC ({bc.KyTruocTu:dd/MM/yyyy} – {bc.KyTruocDen:dd/MM/yyyy})", 14);
            r = Bang(ws, r,
                new[] { "Công ty", "Lần mất KN", "Kỳ trước", "± lần", "TB bị mất", "Kỳ trước", "Giờ mất KN", "Kỳ trước",
                        "% HĐ trong kỳ", "Kỳ trước", "± điểm %", "Sự cố phần cứng", "Kỳ trước" },
                bc.CongTy.Select(c => new object?[] { c.CongTy, c.SoLanMat, c.SoLanMatTruoc, c.SoLanMat - c.SoLanMatTruoc,
                                                      c.SoSwitchBiMat, c.SoSwitchBiMatTruoc,
                                                      Math.Round(c.GiayMat / 3600.0, 1), Math.Round(c.GiayMatTruoc / 3600.0, 1),
                                                      c.TyLeTrongKy / 100, c.TyLeTruoc / 100,
                                                      c.TyLeTrongKy != null && c.TyLeTruoc != null ? Math.Round(c.TyLeTrongKy.Value - c.TyLeTruoc.Value, 2) : null,
                                                      c.SuCoPhanCung, c.SuCoPhanCungTruoc }).ToList(),
                new[] { null, "#,##0", "#,##0", "+#,##0;-#,##0;0", "#,##0", "#,##0", "#,##0.0", "#,##0.0", "0.00%", "0.00%", "+0.00;-0.00;0", "#,##0", "#,##0" });
            r++;

            r = TieuDeMuc(ws, r, "THEO LOẠI THIẾT BỊ", 14);
            r = Bang(ws, r,
                new[] { "Công ty", "Loại", "Tổng", "Theo dõi", "Hoạt động", "Mất KN", "Phần cứng lỗi", "Lần mất KN", "Lần mất kỳ trước", "Giờ mất KN" },
                bc.TheoLoai.Select(l => new object?[] { l.CongTy, l.Loai, l.TongTaiSan, l.Tong, l.HoatDong, l.MatKetNoi, l.SucKhoeLoi,
                                                        l.SoLanMat, l.SoLanMatTruoc, Math.Round(l.GiayMat / 3600.0, 1) }).ToList(),
                new[] { null, null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0.0" });
            r++;

            if (bc.LichSuTu != null && bc.LichSuTu > bc.TuNgay)
                r = GhiChu(ws, r, $"⚠ Lịch sử đổi trạng thái chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — số liệu trong kỳ trước mốc này chưa đầy đủ.", MauVang);
            else if (bc.LichSuTu != null && bc.LichSuTu > bc.KyTruocTu)
                r = GhiChu(ws, r, $"⚠ Lịch sử chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — kỳ trước chưa đủ dữ liệu, phần so sánh chỉ để tham khảo.", MauVang);
            r = GhiChu(ws, r, "Theo dõi = thiết bị có IP và không ở tình trạng Ngừng sử dụng / Trong kho. Hiện tại theo lần ping / SNMP gần nhất.", MauChuPhu);
            GhiChu(ws, r, "% hoạt động trong kỳ = 1 − tổng thời gian mất kết nối / (số thiết bị theo dõi × độ dài kỳ). Sự cố phần cứng = số lần SNMP chuyển sang Lỗi / Cảnh báo.", MauChuPhu);
            ThietLapIn(ws, null);

            var wsLuu = TrangMoi(wb, "Lưu trữ", new[] { 10, 30, 10, 15, 14, 13, 13, 13, 10, 12 });
            r = DauTrang(wsLuu, "DUNG LƯỢNG LƯU TRỮ", phuDe, 10);
            Bang(wsLuu, r,
                new[] { "Công ty", "Tên", "Loại", "IP", "RAID", "Dung lượng (TB)", "Đã dùng (TB)", "Còn trống (TB)", "% đã dùng", "Kết nối" },
                bc.LuuTru.Select(x => new object?[] { x.CongTy, x.Ten, x.Loai, x.Ip, x.Raid, Tb(x.DungLuongTb), Tb(x.DaDungTb),
                                                      x.DaDungTb != null ? Tb(x.DungLuongTb - x.DaDungTb) : null,
                                                      x.DaDungTb != null && x.DungLuongTb > 0 ? Tb(x.DaDungTb / x.DungLuongTb) : null,
                                                      x.KetNoi == "UP" ? "Online" : x.KetNoi == "DOWN" ? "Mất KN" : "–" }).ToList(),
                new[] { null, null, null, null, null, "#,##0.00", "#,##0.00", "#,##0.00", "0%", null }, locDuoc: true);
            ThietLapIn(wsLuu, r);

            var wsChuY = TrangMoi(wb, "Cần chú ý", new[] { 10, 30, 10, 15, 80 });
            r = DauTrang(wsChuY, "THIẾT BỊ CẦN CHÚ Ý", phuDe, 5);
            Bang(wsChuY, r,
                new[] { "Công ty", "Tên", "Loại", "IP", "Vấn đề" },
                bc.CanChuY.Select(x => new object?[] { x.CongTy, x.Ten, x.Loai, x.Ip, string.Join(" · ", x.LyDo) }).ToList(),
                new string?[] { null, null, null, null, null }, locDuoc: true);
            ThietLapIn(wsChuY, r);

            var bayGio = DateTime.Now;
            var wsMat = TrangMoi(wb, "Đang mất kết nối", new[] { 10, 30, 10, 15, 26, 26, 17, 12 });
            r = DauTrang(wsMat, "THIẾT BỊ ĐANG MẤT KẾT NỐI", phuDe, 8);
            Bang(wsMat, r,
                new[] { "Công ty", "Tên", "Loại", "IP", "Chức năng", "Vị trí", "Mất từ", "Đã mất (giờ)" },
                bc.DangMat.Select(a => new object?[] { a.CongTy, a.Ten, a.Loai, a.Ip, a.ChucNang, a.ViTri, a.MatTu,
                                                       a.MatTu != null ? Math.Round((bayGio - a.MatTu.Value).TotalHours, 1) : null }).ToList(),
                new[] { null, null, null, null, null, null, "dd/MM/yyyy HH:mm", "#,##0.0" }, locDuoc: true);
            ThietLapIn(wsMat, r);

            var wsNhieu = TrangMoi(wb, "Mất KN nhiều nhất", new[] { 10, 30, 10, 15, 26, 12, 12, 14 });
            r = DauTrang(wsNhieu, "MẤT KẾT NỐI NHIỀU NHẤT TRONG KỲ (TOP 20)", phuDe, 8);
            Bang(wsNhieu, r,
                new[] { "Công ty", "Tên", "Loại", "IP", "Chức năng", "Lần mất KN", "Lần kỳ trước", "Giờ mất KN" },
                bc.MatNhieu.Select(a => new object?[] { a.CongTy, a.Ten, a.Loai, a.Ip, a.ChucNang, a.SoLan, a.SoLanTruoc,
                                                        Math.Round(a.GiayMat / 3600.0, 1) }).ToList(),
                new[] { null, null, null, null, null, "#,##0", "#,##0", "#,##0.0" }, locDuoc: true);
            ThietLapIn(wsNhieu, r);

            return Luu(wb);
        }
    }
}
