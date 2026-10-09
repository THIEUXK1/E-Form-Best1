using ClosedXML.Excel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel báo cáo camera (tổng quan 3 công ty hoặc riêng 1 công ty): thẻ số liệu, bảng có màu/viền,
    /// lọc + cố định dòng tiêu đề, khổ in A4 ngang vừa 1 trang ngang. Chỉ trình bày, số liệu lấy từ CameraBaoCaoService.
    /// </summary>
    public static class CameraBaoCaoExcel
    {
        private const int SoCot = 10;   // trang "Báo cáo" chia lưới 10 cột, thẻ số liệu rộng 2 cột

        private static readonly XLColor MauChinh = XLColor.FromHtml("#5b21b6");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#ede9fe");
        private static readonly XLColor MauSoc = XLColor.FromHtml("#faf5ff");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");
        private static readonly XLColor MauXanh = XLColor.FromHtml("#15803d");
        private static readonly XLColor MauVang = XLColor.FromHtml("#b45309");
        private static readonly XLColor MauDo = XLColor.FromHtml("#b91c1c");

        public static byte[] Tao(CameraBaoCaoService.BaoCao bc, string? congTy, string? nguoiLap)
        {
            using var wb = new XLWorkbook();
            var tenBaoCao = congTy == null ? "BÁO CÁO TỔNG QUAN CAMERA" : "BÁO CÁO CAMERA " + congTy;
            var phuDe = $"Kỳ báo cáo: {bc.TuNgay:dd/MM/yyyy} – {bc.DenNgay:dd/MM/yyyy} (so với {bc.KyTruocTu:dd/MM} – {bc.KyTruocDen:dd/MM/yyyy})   ·   Lập lúc: {bc.TaoLuc:HH:mm dd/MM/yyyy}"
                        + (string.IsNullOrWhiteSpace(nguoiLap) ? "" : "   ·   Người lập: " + nguoiLap);

            TrangBaoCao(wb.Worksheets.Add("Báo cáo"), bc, tenBaoCao, phuDe, congTy == null);
            TrangCamera(wb.Worksheets.Add("Đang mất kết nối"), "CAMERA ĐANG MẤT KẾT NỐI", phuDe, bc.DangMat, true);
            TrangCamera(wb.Worksheets.Add("Mất KN nhiều nhất"), "CAMERA MẤT KẾT NỐI NHIỀU NHẤT TRONG KỲ", phuDe, bc.MatNhieu, false);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ---------------- Trang "Báo cáo" ----------------

        private static void TrangBaoCao(IXLWorksheet ws, CameraBaoCaoService.BaoCao bc, string tieuDe, string phuDe, bool nhieuCongTy)
        {
            ws.ShowGridLines = false;
            ws.Style.Font.FontName = "Segoe UI";
            ws.Style.Font.FontSize = 10;
            for (var c = 1; c <= SoCot; c++) ws.Column(c).Width = 14;
            ws.Column(1).Width = 18;

            var r = DauTrang(ws, tieuDe, phuDe, SoCot);
            var ok = bc.CongTy.Where(x => x.Loi == null).ToList();

            // Thẻ số liệu hiện tại
            int tong = ok.Sum(x => x.Tong), hoatDong = ok.Sum(x => x.HoatDong), mat = ok.Sum(x => x.MatKetNoi);
            double? tyLe = tong > 0 ? (double)hoatDong / tong : null;
            r = TieuDeMuc(ws, r, "TÌNH TRẠNG HIỆN TẠI", SoCot);
            The(ws, r, 1, "Tổng camera", tong, null, XLColor.FromHtml("#1e293b"));
            The(ws, r, 3, "Đang hoạt động", hoatDong, null, MauXanh);
            The(ws, r, 5, "Mất kết nối", mat, null, mat > 0 ? MauDo : MauXanh);
            The(ws, r, 7, "% hoạt động", tyLe, "0.0%", MauTyLe(tyLe));
            The(ws, r, 9, "Đầu ghi", ok.Sum(x => x.SoDauGhi), null, XLColor.FromHtml("#1e293b"));
            r += 3;

            // Thẻ số liệu trong kỳ
            long giayMat = ok.Sum(x => x.GiayMat);
            var giayKy = Math.Max(0, ((bc.DenNgay.AddDays(1) < bc.TaoLuc ? bc.DenNgay.AddDays(1) : bc.TaoLuc) - bc.TuNgay).TotalSeconds);
            double? tyLeKy = tong > 0 && giayKy > 0 ? Math.Max(0, 1 - giayMat / (tong * giayKy)) : null;
            var chuaCoAnh = ok.Where(x => x.ChuaCoAnh != null).Sum(x => x.ChuaCoAnh!.Value);
            r = TieuDeMuc(ws, r, "TRONG KỲ BÁO CÁO", SoCot);
            The(ws, r, 1, "Lần mất kết nối", ok.Sum(x => x.SoLanMat), null, XLColor.FromHtml("#1e293b"));
            The(ws, r, 3, "Camera bị mất KN", ok.Sum(x => x.SoCameraBiMat), null, XLColor.FromHtml("#1e293b"));
            The(ws, r, 5, "Tổng giờ mất KN", Math.Round(giayMat / 3600.0, 1), "#,##0.0", giayMat > 0 ? MauVang : MauXanh);
            The(ws, r, 7, "% hoạt động trong kỳ", tyLeKy, "0.00%", MauTyLe(tyLeKy));
            The(ws, r, 9, "Chưa có ảnh lưu", chuaCoAnh, null, chuaCoAnh > 0 ? MauDo : MauXanh);
            r += 3;

            // So với kỳ trước (CameraBaoCaoService.KyTruoc): mỗi công ty 1 dòng + dòng Toàn bộ khi nhiều công ty
            r = TieuDeMuc(ws, r, $"SO VỚI KỲ TRƯỚC ({bc.KyTruocTu:dd/MM/yyyy} – {bc.KyTruocDen:dd/MM/yyyy})", SoCot);
            var dongSoSanh = ok.Select(c => DongSoSanh(c.CongTy, c.SoLanMat, c.SoLanMatTruoc, c.SoCameraBiMat, c.SoCameraBiMatTruoc,
                c.GiayMat, c.GiayMatTruoc, c.TyLeTrongKy, c.TyLeTruoc)).ToList();
            if (ok.Count > 1)
            {
                // Cùng độ dài kỳ cho mọi công ty nên % gộp = trung bình có trọng số theo số camera
                var coTyLe = ok.Where(x => x.TyLeTrongKy != null && x.TyLeTruoc != null && x.Tong > 0).ToList();
                var tongCam = coTyLe.Sum(x => x.Tong);
                dongSoSanh.Add(DongSoSanh("Toàn bộ", ok.Sum(x => x.SoLanMat), ok.Sum(x => x.SoLanMatTruoc),
                    ok.Sum(x => x.SoCameraBiMat), ok.Sum(x => x.SoCameraBiMatTruoc), ok.Sum(x => x.GiayMat), ok.Sum(x => x.GiayMatTruoc),
                    tongCam > 0 ? coTyLe.Sum(x => x.TyLeTrongKy!.Value * x.Tong) / tongCam : null,
                    tongCam > 0 ? coTyLe.Sum(x => x.TyLeTruoc!.Value * x.Tong) / tongCam : null));
            }
            var dongDauSoSanh = r;
            r = Bang(ws, r,
                new[] { "Công ty", "Lần mất KN", "Kỳ trước", "± lần", "Camera bị mất", "Kỳ trước", "Giờ mất KN", "Kỳ trước", "% HĐ trong kỳ", "Kỳ trước" },
                dongSoSanh,
                new[] { null, "#,##0", "#,##0", "+#,##0;-#,##0;0", "#,##0", "#,##0", "#,##0.0", "#,##0.0", "0.00%", "0.00%" },
                tyLeCot: new[] { 9, 10 }, doCot: 0);
            // Cột "± lần": tăng là xấu (đỏ), giảm là tốt (xanh)
            for (var i = 1; i <= dongSoSanh.Count; i++)
            {
                var o = ws.Cell(dongDauSoSanh + i, 4);
                if (dongSoSanh[i - 1][3] is int d && d != 0) o.Style.Font.SetBold().Font.SetFontColor(d > 0 ? MauDo : MauXanh);
            }
            if (bc.LichSuTu != null && bc.LichSuTu > bc.KyTruocTu && bc.LichSuTu <= bc.TuNgay)
                r = GhiChu(ws, r, $"⚠ Lịch sử chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — kỳ trước chưa đủ dữ liệu, phần so sánh chỉ để tham khảo.", MauVang);
            r++;

            if (nhieuCongTy)
            {
                r = TieuDeMuc(ws, r, "THEO CÔNG TY", SoCot);
                r = Bang(ws, r,
                    new[] { "Công ty", "Tổng", "Hoạt động", "Mất KN", "% hoạt động", "Đầu ghi", "Chưa có ảnh", "Lần mất KN", "Giờ mất KN", "% HĐ trong kỳ" },
                    bc.CongTy.Select(c => c.Loi != null
                        ? new object?[] { c.CongTy, c.Loi, null, null, null, null, null, null, null, null }
                        : new object?[] { c.CongTy, c.Tong, c.HoatDong, c.MatKetNoi, c.Tong > 0 ? (double)c.HoatDong / c.Tong : null,
                                          c.SoDauGhi, c.ChuaCoAnh, c.SoLanMat, Math.Round(c.GiayMat / 3600.0, 1),
                                          c.TyLeTrongKy / 100 }).ToList(),
                    new[] { null, "#,##0", "#,##0", "#,##0", "0.0%", "#,##0", "#,##0", "#,##0", "#,##0.0", "0.00%" },
                    tyLeCot: new[] { 5, 10 }, doCot: 4);
                r++;
            }

            r = TieuDeMuc(ws, r, "THEO ĐẦU GHI", SoCot);
            r = Bang(ws, r,
                new[] { "Công ty", "Đầu ghi", "IP đầu ghi", "Tổng", "Hoạt động", "Mất KN", "% hoạt động", "Lần mất KN", "Lần mất kỳ trước", "Giờ mất KN" },
                bc.DauGhi.Select(d => new object?[] { d.CongTy, d.Ten ?? d.NvrIp, d.NvrIp, d.Tong, d.HoatDong, d.MatKetNoi,
                                                      d.Tong > 0 ? (double)d.HoatDong / d.Tong : null, d.SoLanMat, d.SoLanMatTruoc,
                                                      Math.Round(d.GiayMat / 3600.0, 1) }).ToList(),
                new[] { null, null, null, "#,##0", "#,##0", "#,##0", "0.0%", "#,##0", "#,##0", "#,##0.0" },
                tyLeCot: new[] { 7 }, doCot: 6);
            r++;

            foreach (var c in bc.CongTy.Where(x => x.Loi != null))
                r = GhiChu(ws, r, "⚠ " + c.CongTy + ": " + c.Loi, MauDo);
            if (bc.LichSuTu != null && bc.LichSuTu > bc.TuNgay)
                r = GhiChu(ws, r, $"⚠ Lịch sử đổi trạng thái chỉ có từ {bc.LichSuTu:HH:mm dd/MM/yyyy} — số liệu trong kỳ trước mốc này chưa đầy đủ.", MauVang);
            r = GhiChu(ws, r, "Hiện tại: BPVN theo hệ thống giám sát (không tính camera đã loại trừ), PFVN/MEGA theo lần kiểm tra trạng thái gần nhất.", MauChuPhu);
            GhiChu(ws, r, "% hoạt động trong kỳ = 1 − tổng thời gian mất kết nối / (số camera × độ dài kỳ).", MauChuPhu);

            ThietLapIn(ws, 3);
        }

        private static object?[] DongSoSanh(string ten, int lan, int lanTruoc, int cam, int camTruoc, long giay, long giayTruoc,
            double? tyLe, double? tyLeTruoc)
            => new object?[] { ten, lan, lanTruoc, lan - lanTruoc, cam, camTruoc,
                               Math.Round(giay / 3600.0, 1), Math.Round(giayTruoc / 3600.0, 1), tyLe / 100, tyLeTruoc / 100 };

        // ---------------- Trang danh sách camera ----------------

        private static void TrangCamera(IXLWorksheet ws, string tieuDe, string phuDe, List<CameraBaoCaoService.CameraDong> ds, bool dangMat)
        {
            ws.ShowGridLines = false;
            ws.Style.Font.FontName = "Segoe UI";
            ws.Style.Font.FontSize = 10;
            var doRong = new[] { 9, 22, 15, 7, 30, 15, 18, 13, 13, 40 };
            for (var c = 0; c < doRong.Length; c++) ws.Column(c + 1).Width = doRong[c];

            var r = DauTrang(ws, tieuDe, phuDe, doRong.Length);
            var dongTieuDe = r;
            var bayGio = DateTime.Now;
            r = Bang(ws, r,
                new[] { "Công ty", "Đầu ghi", "IP đầu ghi", "Kênh", "Tên camera", "IP camera",
                        dangMat ? "Mất kết nối từ" : "Đang mất từ", dangMat ? "Đã mất (giờ)" : "Lần mất KN", "Giờ mất trong kỳ", "Ghi chú" },
                ds.Select(c => new object?[] { c.CongTy, c.DauGhi ?? c.NvrIp, c.NvrIp, c.Kenh, c.Ten, c.Ip, c.MatTu,
                                               dangMat ? (c.MatTu != null ? Math.Round((bayGio - c.MatTu.Value).TotalHours, 1) : null) : c.SoLan,
                                               Math.Round(c.GiayMat / 3600.0, 1), c.GhiChu }).ToList(),
                new[] { null, null, null, "0", null, null, "dd/MM/yyyy HH:mm", dangMat ? "#,##0.0" : "#,##0", "#,##0.0", null },
                tyLeCot: Array.Empty<int>(), doCot: 0, locDuoc: true);

            if (ds.Count == 0)
                GhiChu(ws, r, dangMat ? "Không có camera nào đang mất kết nối." : "Không có camera nào mất kết nối trong kỳ.", MauXanh);
            else
            {
                // Thanh dữ liệu cho cột số: nhìn là thấy camera nào nặng nhất
                ws.Range(dongTieuDe + 1, 8, dongTieuDe + ds.Count, 8).AddConditionalFormat()
                    .DataBar(XLColor.FromHtml("#f87171")).LowestValue().HighestValue();
                ws.Range(dongTieuDe + 1, 9, dongTieuDe + ds.Count, 9).AddConditionalFormat()
                    .DataBar(XLColor.FromHtml("#fbbf24")).LowestValue().HighestValue();
                ws.Range(dongTieuDe + 1, 10, dongTieuDe + ds.Count, 10).Style.Alignment.WrapText = true;
            }

            ws.SheetView.FreezeRows(dongTieuDe);
            ThietLapIn(ws, dongTieuDe);
        }

        // ---------------- Khối dùng chung ----------------

        /// <summary>Dải tiêu đề tím + dòng phụ đề; trả về dòng trống kế tiếp.</summary>
        private static int DauTrang(IXLWorksheet ws, string tieuDe, string phuDe, int soCot)
        {
            var t = ws.Range(1, 1, 1, soCot).Merge();
            t.Value = tieuDe;
            t.Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White);
            t.Style.Fill.BackgroundColor = MauChinh;
            t.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Row(1).Height = 34;

            var p = ws.Range(2, 1, 2, soCot).Merge();
            p.Value = phuDe;
            p.Style.Font.SetItalic().Font.SetFontColor(MauChuPhu);
            p.Style.Fill.BackgroundColor = MauNhat;
            p.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(2).Height = 20;
            return 4;
        }

        private static int TieuDeMuc(IXLWorksheet ws, int r, string ten, int soCot)
        {
            var o = ws.Range(r, 1, r, soCot);
            ws.Cell(r, 1).Value = ten;
            o.Style.Font.SetBold().Font.SetFontColor(MauChinh).Font.SetFontSize(11);
            o.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            o.Style.Border.BottomBorderColor = MauChinh;
            ws.Row(r).Height = 20;
            return r + 1;
        }

        /// <summary>Thẻ số liệu 2 cột × 2 dòng: nhãn nhỏ phía trên, số lớn có màu phía dưới.</summary>
        private static void The(IXLWorksheet ws, int r, int c, string nhan, object? giaTri, string? dinhDang, XLColor mau)
        {
            var nhanO = ws.Range(r, c, r, c + 1).Merge();
            nhanO.Value = nhan;
            nhanO.Style.Font.SetFontColor(MauChuPhu).Font.SetFontSize(9);
            nhanO.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var soO = ws.Range(r + 1, c, r + 1, c + 1).Merge();
            GanGiaTri(ws.Cell(r + 1, c), giaTri);
            if (dinhDang != null) soO.Style.NumberFormat.Format = dinhDang;
            soO.Style.Font.SetBold().Font.SetFontSize(20).Font.SetFontColor(mau);
            soO.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            var khung = ws.Range(r, c, r + 1, c + 1);
            khung.Style.Fill.BackgroundColor = MauSoc;
            khung.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            khung.Style.Border.OutsideBorderColor = XLColor.FromHtml("#ddd6fe");
            ws.Row(r + 1).Height = 32;
        }

        /// <summary>
        /// Bảng có tiêu đề tím nhạt, sọc xen kẽ, viền mảnh. tyLeCot: cột % tô xanh/vàng/đỏ theo ngưỡng;
        /// doCot: cột số "mất" tô đỏ khi > 0. Trả về dòng trống kế tiếp.
        /// </summary>
        private static int Bang(IXLWorksheet ws, int r, string[] tieuDe, List<object?[]> dong, string?[] dinhDang,
            int[] tyLeCot, int doCot, bool locDuoc = false, int? soCotThat = null)
        {
            var n = soCotThat ?? tieuDe.Length;
            for (var c = 0; c < n; c++) ws.Cell(r, c + 1).Value = tieuDe[c];
            var dau = ws.Range(r, 1, r, n);
            dau.Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#3b0764"));
            dau.Style.Fill.BackgroundColor = MauNhat;
            dau.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true);
            ws.Row(r).Height = 30;
            var dongDau = r;

            foreach (var d in dong)
            {
                r++;
                for (var c = 0; c < n; c++)
                {
                    var o = ws.Cell(r, c + 1);
                    GanGiaTri(o, d[c]);
                    if (dinhDang[c] != null) o.Style.NumberFormat.Format = dinhDang[c];
                    if (tyLeCot.Contains(c + 1) && d[c] is double t)
                        o.Style.Font.SetBold().Font.SetFontColor(MauTyLe(t));
                    if (c + 1 == doCot && d[c] is int so && so > 0)
                        o.Style.Font.SetBold().Font.SetFontColor(MauDo);
                }
                if ((r - dongDau) % 2 == 0) ws.Range(r, 1, r, n).Style.Fill.BackgroundColor = MauSoc;
                ws.Range(r, 1, r, n).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                if (!locDuoc) ws.Row(r).Height = 20;   // trang danh sách camera có ghi chú xuống dòng: để Excel tự giãn
                ws.Cell(r, 1).Style.Font.SetBold();
            }

            var bang = ws.Range(dongDau, 1, Math.Max(r, dongDau), n);
            bang.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            bang.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            bang.Style.Border.OutsideBorderColor = MauVien;
            bang.Style.Border.InsideBorderColor = MauVien;
            if (locDuoc && dong.Count > 0) bang.SetAutoFilter();
            return r + 1;
        }

        private static int GhiChu(IXLWorksheet ws, int r, string noiDung, XLColor mau)
        {
            ws.Cell(r, 1).Value = noiDung;
            ws.Cell(r, 1).Style.Font.SetFontColor(mau).Font.SetFontSize(9).Font.SetItalic();
            return r + 1;
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

        /// <summary>≥ 98% xanh, ≥ 90% vàng, còn lại đỏ (tỷ lệ dạng 0..1).</summary>
        private static XLColor MauTyLe(double? t)
            => t == null ? MauChuPhu : t >= 0.98 ? MauXanh : t >= 0.90 ? MauVang : MauDo;

        private static void ThietLapIn(IXLWorksheet ws, int dongLapLai)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.4;
            ws.PageSetup.Margins.Right = 0.4;
            ws.PageSetup.SetRowsToRepeatAtTop(1, dongLapLai);
        }
    }
}
