using ClosedXML.Excel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel báo cáo sự cố hệ thống (/TheoDoiHeThong): sheet tổng hợp (theo hệ thống + nguyên nhân)
    /// và sheet chi tiết sự cố. Chỉ trình bày, số liệu lấy từ HeThongSuCoBaoCaoService.
    /// </summary>
    public static class HeThongSuCoBaoCaoExcel
    {
        private static readonly XLColor MauChinh = XLColor.FromHtml("#b91c1c");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#fee2e2");
        private static readonly XLColor MauSoc = XLColor.FromHtml("#fef2f2");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");

        public static byte[] Tao(HeThongSuCoBaoCaoService.BaoCao bc, string? maHeThong, string? nguoiLap)
        {
            using var wb = new XLWorkbook();
            var tieuDe = maHeThong == null ? "BÁO CÁO SỰ CỐ HỆ THỐNG" : "BÁO CÁO SỰ CỐ " + maHeThong;
            var phuDe = PhuDe($"Kỳ báo cáo: {bc.TuNgay:dd/MM/yyyy} – {bc.DenNgay:dd/MM/yyyy}   ·   Lập lúc: {bc.TaoLuc:HH:mm dd/MM/yyyy}", nguoiLap);

            // Cột 1 rộng vì bảng nguyên nhân bên dưới dùng chung cột này
            var ws = TrangMoi(wb, "Tổng hợp", new[] { 40, 11, 13, 11, 16, 18, 16, 18 });
            var r = DauTrang(ws, tieuDe, phuDe, 8);
            r = TieuDeMuc(ws, r, "THEO HỆ THỐNG", 8);
            r = Bang(ws, r,
                new[] { "Hệ thống", "Số sự cố", "Đã khắc phục", "Đang mở", "Giờ sự cố trong kỳ", "TB giờ khắc phục", "Sự cố dài nhất (giờ)", "Sự cố gần nhất" },
                bc.HeThong.Select(h => new object?[] { h.MaHeThong, h.SoSuCo, h.DaKhacPhuc, h.DangMo, h.SoGioTrongKy,
                                                        h.SoGioKhacPhucTrungBinh, h.SoGioDaiNhat, h.SuCoGanNhat }).ToList(),
                new[] { null, "#,##0", "#,##0", "#,##0", "#,##0.0", "#,##0.0", "#,##0.0", "dd/MM/yyyy HH:mm" });
            r++;

            r = TieuDeMuc(ws, r, "NGUYÊN NHÂN", 8);
            r = Bang(ws, r,
                new[] { "Nguyên nhân", "Số lần", "Hệ thống" },
                bc.NguyenNhan.Select(n => new object?[] { n.NguyenNhan, n.SoLan, n.HeThong }).ToList(),
                new[] { null, "#,##0", null });
            r++;
            r = GhiChu(ws, r, "Sự cố thuộc kỳ = có thời gian diễn ra giao với kỳ báo cáo. Sự cố chưa đóng tính thời lượng tới lúc lập báo cáo.", MauChuPhu);
            GhiChu(ws, r, "Giờ sự cố trong kỳ = cộng thời lượng từng sự cố phần nằm trong kỳ (các sự cố trùng giờ được cộng riêng).", MauChuPhu);
            ThietLapIn(ws, null);

            var wsCt = TrangMoi(wb, "Chi tiết sự cố", new[] { 13, 36, 14, 16, 16, 10, 32, 20, 40, 40 });
            r = DauTrang(wsCt, "CHI TIẾT SỰ CỐ", phuDe, 10);
            var dauBang = r;
            Bang(wsCt, r,
                new[] { "Hệ thống", "Tiêu đề", "Trạng thái", "Bắt đầu", "Kết thúc", "Số giờ", "Nguyên nhân", "Người chịu trách nhiệm", "Mô tả", "Ghi chú xử lý gần nhất" },
                bc.SuCo.Select(x => new object?[] { x.MaHeThong, x.TieuDe, x.TenTrangThai, x.BatDauLuc, x.KetThucLuc, x.SoGio,
                                                     x.NguyenNhan, x.NguoiChiuTrachNhiem, x.MoTa, x.GhiChuCuoi }).ToList(),
                new[] { null, null, null, "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm", "#,##0.0", null, null, null, null },
                locDuoc: true);
            wsCt.Columns(1, 10).Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            ThietLapIn(wsCt, dauBang);

            return Luu(wb);
        }

        // ---------------- Dựng trang (cùng khuôn các báo cáo IT khác) ----------------

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
