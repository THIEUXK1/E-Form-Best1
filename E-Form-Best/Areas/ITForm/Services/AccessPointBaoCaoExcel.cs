using ClosedXML.Excel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel cho /QLAP: báo cáo theo kỳ (tổng quan / 1 công ty) và danh sách AP của 1 công ty.
    /// Chỉ trình bày, số liệu lấy từ AccessPointBaoCaoService / QLAPController.
    /// </summary>
    public static class AccessPointBaoCaoExcel
    {
        private static readonly XLColor MauChinh = XLColor.FromHtml("#0e7490");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#cffafe");
        private static readonly XLColor MauSoc = XLColor.FromHtml("#f0fdfa");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");
        private static readonly XLColor MauVang = XLColor.FromHtml("#b45309");

        /// <summary>Một dòng sheet danh sách AP (controller chiếu sẵn, không kéo entity vào đây).</summary>
        public record DongDanhSach(string? Ma, string Ten, string? Ip, string? Mac, string? HangModel, string? Serial,
            string? Ssid, string? Controller, string? BoPhan, string? ViTri, string? TinhTrang, string KetNoi,
            DateTime? KiemTraLuc, DateOnly? NgayLapDat, DateOnly? HanBaoHanh, string? GhiChu);

        public static byte[] TaoBaoCao(AccessPointBaoCaoService.BaoCao bc, string? congTy, string? nguoiLap)
        {
            using var wb = new XLWorkbook();
            var tieuDe = congTy == null ? "BÁO CÁO TỔNG QUAN AP WI-FI" : "BÁO CÁO AP WI-FI " + congTy;
            var phuDe = PhuDe($"Kỳ báo cáo: {bc.TuNgay:dd/MM/yyyy} – {bc.DenNgay:dd/MM/yyyy} (so với {bc.KyTruocTu:dd/MM} – {bc.KyTruocDen:dd/MM/yyyy})   ·   Lập lúc: {bc.TaoLuc:HH:mm dd/MM/yyyy}", nguoiLap);

            var ws = TrangMoi(wb, "Báo cáo", new[] { 14, 11, 11, 11, 11, 12, 12, 12, 12, 12, 13, 14 });
            var r = DauTrang(ws, tieuDe, phuDe, 12);
            r = TieuDeMuc(ws, r, "THEO CÔNG TY", 12);
            r = Bang(ws, r,
                new[] { "Công ty", "Tổng AP", "Theo dõi", "Hoạt động", "Mất KN", "Chưa kiểm", "Chưa có IP", "Hết BH",
                        "Lần mất KN", "AP bị mất", "Giờ mất KN", "% HĐ trong kỳ" },
                bc.CongTy.Select(c => new object?[] { c.CongTy, c.TongTaiSan, c.Tong, c.HoatDong, c.MatKetNoi, c.ChuaKiemTra,
                                                      c.ChuaKhaiIp, c.HetBaoHanh, c.SoLanMat, c.SoApBiMat,
                                                      Math.Round(c.GiayMat / 3600.0, 1), c.TyLeTrongKy / 100 }).ToList(),
                new[] { null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0.0", "0.00%" });
            r++;

            // So với kỳ trước (CameraBaoCaoService.KyTruoc); nhiều công ty thì thêm dòng Toàn bộ
            r = TieuDeMuc(ws, r, $"SO VỚI KỲ TRƯỚC ({bc.KyTruocTu:dd/MM/yyyy} – {bc.KyTruocDen:dd/MM/yyyy})", 12);
            var dongSoSanh = bc.CongTy.Select(c => DongSoSanh(c.CongTy, c.SoLanMat, c.SoLanMatTruoc, c.SoApBiMat, c.SoApBiMatTruoc,
                c.GiayMat, c.GiayMatTruoc, c.TyLeTrongKy, c.TyLeTruoc)).ToList();
            if (bc.CongTy.Count > 1)
            {
                // Cùng độ dài kỳ cho mọi công ty nên % gộp = trung bình có trọng số theo số AP theo dõi
                var coTyLe = bc.CongTy.Where(x => x.TyLeTrongKy != null && x.TyLeTruoc != null && x.Tong > 0).ToList();
                var tongAp = coTyLe.Sum(x => x.Tong);
                dongSoSanh.Add(DongSoSanh("Toàn bộ", bc.CongTy.Sum(x => x.SoLanMat), bc.CongTy.Sum(x => x.SoLanMatTruoc),
                    bc.CongTy.Sum(x => x.SoApBiMat), bc.CongTy.Sum(x => x.SoApBiMatTruoc), bc.CongTy.Sum(x => x.GiayMat), bc.CongTy.Sum(x => x.GiayMatTruoc),
                    tongAp > 0 ? coTyLe.Sum(x => x.TyLeTrongKy!.Value * x.Tong) / tongAp : null,
                    tongAp > 0 ? coTyLe.Sum(x => x.TyLeTruoc!.Value * x.Tong) / tongAp : null));
            }
            r = Bang(ws, r,
                new[] { "Công ty", "Lần mất KN", "Kỳ trước", "± lần", "AP bị mất", "Kỳ trước", "Giờ mất KN", "Kỳ trước",
                        "% HĐ trong kỳ", "Kỳ trước", "± điểm %" },
                dongSoSanh,
                new[] { null, "#,##0", "#,##0", "+#,##0;-#,##0;0", "#,##0", "#,##0", "#,##0.0", "#,##0.0", "0.00%", "0.00%", "+0.00;-0.00;0" });
            r++;

            r = TieuDeMuc(ws, r, "THEO BỘ PHẬN", 12);
            r = Bang(ws, r,
                new[] { "Công ty", "Bộ phận", "Tổng", "Hoạt động", "Mất KN", "Lần mất KN", "Lần mất kỳ trước", "Giờ mất KN" },
                bc.BoPhan.Select(b => new object?[] { b.CongTy, b.BoPhan, b.Tong, b.HoatDong, b.MatKetNoi, b.SoLanMat, b.SoLanMatTruoc,
                                                      Math.Round(b.GiayMat / 3600.0, 1) }).ToList(),
                new[] { null, null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0.0" });
            r++;

            if (bc.LichSuTu != null && bc.LichSuTu > bc.TuNgay)
                r = GhiChu(ws, r, $"⚠ Lịch sử đổi trạng thái chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — số liệu trong kỳ trước mốc này chưa đầy đủ.", MauVang);
            else if (bc.LichSuTu != null && bc.LichSuTu > bc.KyTruocTu)
                r = GhiChu(ws, r, $"⚠ Lịch sử chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — kỳ trước chưa đủ dữ liệu, phần so sánh chỉ để tham khảo.", MauVang);
            r = GhiChu(ws, r, "Theo dõi = AP có IP và không ở tình trạng Ngừng sử dụng / Trong kho. Hiện tại theo lần ping gần nhất.", MauChuPhu);
            GhiChu(ws, r, "% hoạt động trong kỳ = 1 − tổng thời gian mất kết nối / (số AP theo dõi × độ dài kỳ).", MauChuPhu);
            ThietLapIn(ws, null);

            var bayGio = DateTime.Now;
            var wsMat = TrangMoi(wb, "Đang mất kết nối", new[] { 10, 30, 15, 22, 30, 17, 12 });
            r = DauTrang(wsMat, "AP ĐANG MẤT KẾT NỐI", phuDe, 7);
            Bang(wsMat, r,
                new[] { "Công ty", "Tên AP", "IP", "Bộ phận", "Vị trí", "Mất từ", "Đã mất (giờ)" },
                bc.DangMat.Select(a => new object?[] { a.CongTy, a.Ten, a.Ip, a.BoPhan, a.ViTri, a.MatTu,
                                                       a.MatTu != null ? Math.Round((bayGio - a.MatTu.Value).TotalHours, 1) : null }).ToList(),
                new[] { null, null, null, null, null, "dd/MM/yyyy HH:mm", "#,##0.0" }, locDuoc: true);
            ThietLapIn(wsMat, r);

            var wsNhieu = TrangMoi(wb, "Mất KN nhiều nhất", new[] { 10, 30, 15, 22, 30, 12, 12, 14 });
            r = DauTrang(wsNhieu, "AP MẤT KẾT NỐI NHIỀU NHẤT TRONG KỲ (TOP 20)", phuDe, 8);
            Bang(wsNhieu, r,
                new[] { "Công ty", "Tên AP", "IP", "Bộ phận", "Vị trí", "Lần mất KN", "Lần kỳ trước", "Giờ mất KN" },
                bc.MatNhieu.Select(a => new object?[] { a.CongTy, a.Ten, a.Ip, a.BoPhan, a.ViTri, a.SoLan, a.SoLanTruoc,
                                                        Math.Round(a.GiayMat / 3600.0, 1) }).ToList(),
                new[] { null, null, null, null, null, "#,##0", "#,##0", "#,##0.0" }, locDuoc: true);
            ThietLapIn(wsNhieu, r);

            return Luu(wb);
        }

        public static byte[] TaoDanhSach(IReadOnlyList<DongDanhSach> ds, string congTy, string? nguoiLap)
        {
            using var wb = new XLWorkbook();
            var ws = TrangMoi(wb, "Danh sách AP", new[] { 6, 12, 26, 15, 19, 22, 16, 22, 18, 20, 26, 15, 11, 16, 12, 12, 30 });
            var r = DauTrang(ws, "DANH SÁCH AP WI-FI " + congTy,
                PhuDe($"Xuất lúc: {DateTime.Now:HH:mm dd/MM/yyyy}   ·   {ds.Count} AP", nguoiLap), 17);
            Bang(ws, r,
                new[] { "#", "Mã", "Tên AP", "IP", "MAC", "Hãng / Model", "Serial", "SSID", "Controller", "Bộ phận", "Vị trí",
                        "Tình trạng", "Kết nối", "Kiểm tra lúc", "Ngày lắp", "Hạn BH", "Ghi chú" },
                ds.Select((x, i) => new object?[] { i + 1, x.Ma, x.Ten, x.Ip, x.Mac, x.HangModel, x.Serial, x.Ssid, x.Controller,
                                                    x.BoPhan, x.ViTri, x.TinhTrang, x.KetNoi, x.KiemTraLuc,
                                                    x.NgayLapDat?.ToDateTime(TimeOnly.MinValue), x.HanBaoHanh?.ToDateTime(TimeOnly.MinValue),
                                                    x.GhiChu }).ToList(),
                new[] { "0", null, null, null, null, null, null, null, null, null, null, null, null,
                        "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "dd/MM/yyyy", null }, locDuoc: true);
            ThietLapIn(ws, r);
            return Luu(wb);
        }

        private static object?[] DongSoSanh(string ten, int lan, int lanTruoc, int biMat, int biMatTruoc, long giay, long giayTruoc,
            double? tyLe, double? tyLeTruoc)
            => new object?[] { ten, lan, lanTruoc, lan - lanTruoc, biMat, biMatTruoc,
                               Math.Round(giay / 3600.0, 1), Math.Round(giayTruoc / 3600.0, 1), tyLe / 100, tyLeTruoc / 100,
                               tyLe != null && tyLeTruoc != null ? Math.Round(tyLe.Value - tyLeTruoc.Value, 2) : null };

        // ---------------- Dựng trang ----------------

        private static string PhuDe(string noiDung, string? nguoiLap)
            => noiDung + (string.IsNullOrWhiteSpace(nguoiLap) ? "" : "   ·   Người lập: " + nguoiLap);

        private static IXLWorksheet TrangMoi(XLWorkbook wb, string ten, int[] doRong)
        {
            var ws = wb.Worksheets.Add(ten);
            ws.ShowGridLines = false;
            ws.Style.Font.FontName = "Segoe UI";
            ws.Style.Font.FontSize = 10;
            for (var c = 0; c < doRong.Length; c++) ws.Column(c + 1).Width = doRong[c];
            return ws;
        }

        /// <summary>Tiêu đề + phụ đề trải ngang soCot cột; trả về dòng kế tiếp.</summary>
        private static int DauTrang(IXLWorksheet ws, string tieuDe, string phuDe, int soCot)
        {
            var o = ws.Range(1, 1, 1, soCot).Merge();
            o.Value = tieuDe;
            o.Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(MauChinh);
            o.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetIndent(1);
            ws.Row(1).Height = 28;

            var p = ws.Range(2, 1, 2, soCot).Merge();
            p.Value = phuDe;
            p.Style.Font.SetFontColor(MauChuPhu).Font.SetItalic().Fill.SetBackgroundColor(MauNhat).Alignment.SetIndent(1);
            return 4;
        }

        private static int TieuDeMuc(IXLWorksheet ws, int r, string ten, int soCot)
        {
            var o = ws.Range(r, 1, r, soCot).Merge();
            o.Value = ten;
            o.Style.Font.SetBold().Font.SetFontColor(MauChinh)
                .Border.SetBottomBorder(XLBorderStyleValues.Medium).Border.SetBottomBorderColor(MauChinh);
            return r + 1;
        }

        /// <summary>Bảng có dòng tiêu đề đậm, sọc xen kẽ, viền nhạt; trả về dòng ngay dưới bảng.</summary>
        private static int Bang(IXLWorksheet ws, int r, string[] cot, List<object?[]> dong, string?[] dinhDang, bool locDuoc = false)
        {
            var dauBang = r;
            for (var c = 0; c < cot.Length; c++)
            {
                var o = ws.Cell(r, c + 1);
                o.Value = cot[c];
                o.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(MauChinh)
                    .Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            }
            r++;

            if (dong.Count == 0)
            {
                var o = ws.Range(r, 1, r, cot.Length).Merge();
                o.Value = "Không có dữ liệu.";
                o.Style.Font.SetItalic().Font.SetFontColor(MauChuPhu).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                r++;
            }

            for (var i = 0; i < dong.Count; i++, r++)
            {
                for (var c = 0; c < cot.Length; c++)
                {
                    var o = ws.Cell(r, c + 1);
                    GanGiaTri(o, dong[i][c]);
                    if (dinhDang[c] != null) o.Style.NumberFormat.Format = dinhDang[c];
                    if (i % 2 == 1) o.Style.Fill.SetBackgroundColor(MauSoc);
                }
            }

            var vung = ws.Range(dauBang, 1, r - 1, cot.Length);
            vung.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(MauVien).Border.SetInsideBorderColor(MauVien);
            if (locDuoc && dong.Count > 0)
            {
                ws.Range(dauBang, 1, r - 1, cot.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(dauBang);
            }
            return r;
        }

        private static void GanGiaTri(IXLCell o, object? v)
        {
            switch (v)
            {
                case null: o.Value = Blank.Value; break;
                case int i: o.Value = i; break;
                case long l: o.Value = l; break;
                case double d: o.Value = d; break;
                case DateTime t: o.Value = t; break;
                default: o.Value = v.ToString(); break;
            }
        }

        private static int GhiChu(IXLWorksheet ws, int r, string noiDung, XLColor mau)
        {
            ws.Cell(r, 1).Value = noiDung;
            ws.Cell(r, 1).Style.Font.SetFontColor(mau).Font.SetFontSize(9);
            return r + 1;
        }

        /// <summary>A4 ngang, vừa 1 trang ngang, lặp dòng tiêu đề bảng (dongLap) khi in nhiều trang.</summary>
        private static void ThietLapIn(IXLWorksheet ws, int? dongLap)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.SetLeft(0.4).SetRight(0.4).SetTop(0.5).SetBottom(0.5);
            if (dongLap != null) ws.PageSetup.SetRowsToRepeatAtTop(dongLap.Value, dongLap.Value);
        }

        private static byte[] Luu(XLWorkbook wb)
        {
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
