using ClosedXML.Excel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel tổng hợp của "Báo cáo định kì": sheet Tổng hợp + chép nguyên các sheet báo cáo Camera / AP / Switch
    /// (đã có builder riêng, không vẽ lại) + sheet Thiết bị, Đơn IT, Công việc. Chỉ trình bày, số liệu lấy từ service.
    /// </summary>
    public static class BaoCaoDinhKyExcel
    {
        private static readonly XLColor MauChinh = XLColor.FromHtml("#4338ca");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#e0e7ff");
        private static readonly XLColor MauSoc = XLColor.FromHtml("#f5f7ff");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");

        public record DauVao(
            DateTime TuNgay, DateTime DenNgay, string? CongTy, string? NguoiLap,
            CameraBaoCaoService.BaoCao Camera, byte[] FileCamera,
            AccessPointBaoCaoService.BaoCao Ap, byte[] FileAp,
            SwitchBaoCaoService.BaoCao Switch, byte[] FileSwitch,
            BaoCaoDinhKyService.ThongKeThietBi ThietBi,
            BaoCaoDinhKyService.ThongKeDon DonIt,
            BaoCaoDinhKyService.ThongKeDon DonCongViec);

        public static byte[] Tao(DauVao d)
        {
            using var wb = new XLWorkbook();
            var phuDe = $"Kỳ báo cáo: {d.TuNgay:dd/MM/yyyy} – {d.DenNgay:dd/MM/yyyy}   ·   Phạm vi Camera/AP/Switch: {d.CongTy ?? "tất cả công ty"}"
                        + $"   ·   Lập lúc: {DateTime.Now:HH:mm dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(d.NguoiLap) ? "" : "   ·   Người lập: " + d.NguoiLap);

            SheetTongHop(wb, d, phuDe);
            ChepSheet(wb, d.FileCamera, "Camera");
            ChepSheet(wb, d.FileAp, "AP");
            ChepSheet(wb, d.FileSwitch, "Switch");
            SheetThietBi(wb, d.ThietBi, phuDe);
            SheetDon(wb, "Đơn IT", "THỐNG KÊ ĐƠN IT", d.DonIt, phuDe);
            SheetDon(wb, "Công việc", "THỐNG KÊ ĐƠN CÔNG VIỆC", d.DonCongViec, phuDe);

            wb.Worksheet(1).SetTabActive();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ---------------- Tổng hợp ----------------

        private static void SheetTongHop(XLWorkbook wb, DauVao d, string phuDe)
        {
            var ws = TrangMoi(wb, "Tổng hợp", new[] { 26, 13, 13, 13, 13, 15, 15 });
            var r = DauTrang(ws, "BÁO CÁO ĐỊNH KÌ", phuDe, 7);

            r = TieuDeMuc(ws, r, "HẠ TẦNG MẠNG & CAMERA", 7);
            var hang = new List<object?[]>
            {
                DongHaTang("Camera", d.Camera.CongTy.Sum(c => c.Tong), d.Camera.CongTy.Sum(c => c.HoatDong), d.Camera.CongTy.Sum(c => c.MatKetNoi),
                    d.Camera.CongTy.Sum(c => c.SoLanMat), d.Camera.CongTy.Sum(c => c.GiayMat), TyLeKy(d.Camera.CongTy.Select(c => (c.TyLeTrongKy, c.Tong)))),
                DongHaTang("AP Wi-Fi", d.Ap.CongTy.Sum(c => c.TongTaiSan), d.Ap.CongTy.Sum(c => c.HoatDong), d.Ap.CongTy.Sum(c => c.MatKetNoi),
                    d.Ap.CongTy.Sum(c => c.SoLanMat), d.Ap.CongTy.Sum(c => c.GiayMat), TyLeKy(d.Ap.CongTy.Select(c => (c.TyLeTrongKy, c.Tong)))),
                DongHaTang("Switch", d.Switch.CongTy.Sum(c => c.TongTaiSan), d.Switch.CongTy.Sum(c => c.HoatDong), d.Switch.CongTy.Sum(c => c.MatKetNoi),
                    d.Switch.CongTy.Sum(c => c.SoLanMat), d.Switch.CongTy.Sum(c => c.GiayMat), TyLeKy(d.Switch.CongTy.Select(c => (c.TyLeTrongKy, c.Tong)))),
            };
            r = Bang(ws, r, new[] { "Hạng mục", "Tổng", "Hoạt động", "Mất KN", "Lần mất KN (kỳ)", "Giờ mất KN (kỳ)", "% HĐ trong kỳ" },
                hang, new[] { null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0.0", "0.00%" });
            r++;

            r = TieuDeMuc(ws, r, "THIẾT BỊ (HIỆN TRẠNG)", 7);
            var tb = d.ThietBi.CongTy;
            r = Bang(ws, r, new[] { "Hạng mục", "Tổng", "Hoạt động", "Hỏng", "Bảo trì", "Hết BH", "Chưa kiểm" },
                new List<object?[]> { new object?[] { "Thiết bị", tb.Sum(x => x.Tong), tb.Sum(x => x.HoatDong), tb.Sum(x => x.Hong),
                    tb.Sum(x => x.BaoTri), tb.Sum(x => x.HetBaoHanh), tb.Sum(x => x.ChuaKiem) } },
                new[] { null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0" });
            r++;

            r = TieuDeMuc(ws, r, "ĐƠN TRONG KỲ", 7);
            var cot = new[] { "Loại đơn", "Tổng" }.Concat(BaoCaoDinhKyService.DsTrangThaiDon).ToArray();
            object?[] DongDon(string ten, BaoCaoDinhKyService.ThongKeDon t) =>
                new object?[] { ten, t.Tong }.Concat(t.TheoTrangThai.Select(x => (object?)x.SoLuong)).ToArray();
            r = Bang(ws, r, cot, new List<object?[]> { DongDon("Đơn IT", d.DonIt), DongDon("Đơn công việc", d.DonCongViec) },
                cot.Select((_, i) => i == 0 ? null : "#,##0").ToArray());
            if (d.DonCongViec.QuaHan != null)
                r = GhiChu(ws, r, $"Đơn công việc đang quá hạn: {d.DonCongViec.QuaHan:#,##0}");
            r++;

            r = GhiChu(ws, r, "Chi tiết từng mục ở các sheet sau: Camera·…, AP·…, Switch·…, Thiết bị, Đơn IT, Công việc.");
            r = GhiChu(ws, r, "Đơn tính theo thời điểm quản lý duyệt (TimeNguoiDuyet) trong kỳ. Thiết bị là hiện trạng lúc xuất, không theo kỳ.");
            GhiChu(ws, r, "% HĐ trong kỳ của từng hạng mục = trung bình các công ty, có trọng số theo số thiết bị theo dõi.");
            ThietLapIn(ws, null);
        }

        private static object?[] DongHaTang(string ten, int tong, int hoatDong, int mat, int lanMat, long giayMat, double? tyLe) =>
            new object?[] { ten, tong, hoatDong, mat, lanMat, Math.Round(giayMat / 3600.0, 1), tyLe / 100 };

        // % hoạt động gộp nhiều công ty: bình quân gia quyền theo số thiết bị theo dõi của từng công ty
        private static double? TyLeKy(IEnumerable<(double? TyLe, int SoThietBi)> ds)
        {
            var co = ds.Where(x => x.TyLe != null && x.SoThietBi > 0).ToList();
            var tong = co.Sum(x => x.SoThietBi);
            return tong == 0 ? null : co.Sum(x => x.TyLe!.Value * x.SoThietBi) / tong;
        }

        // Chép nguyên mọi sheet của file báo cáo module sang file tổng hợp, tên sheet thêm tiền tố module
        private static void ChepSheet(XLWorkbook dich, byte[] file, string tienTo)
        {
            using var ms = new MemoryStream(file);
            using var nguon = new XLWorkbook(ms);
            foreach (var ws in nguon.Worksheets)
            {
                var ten = $"{tienTo}·{ws.Name}";
                if (ten.Length > 31) ten = ten[..31];   // giới hạn tên sheet của Excel
                ws.CopyTo(dich, ten);
            }
        }

        // ---------------- Thiết bị ----------------

        private static void SheetThietBi(XLWorkbook wb, BaoCaoDinhKyService.ThongKeThietBi tb, string phuDe)
        {
            var ws = TrangMoi(wb, "Thiết bị", new[] { 24, 26, 10, 11, 9, 9, 9, 9, 11, 10, 11, 11, 13 });
            var r = DauTrang(ws, "TỔNG QUAN THIẾT BỊ (HIỆN TRẠNG LÚC XUẤT)", phuDe, 13);

            r = TieuDeMuc(ws, r, "THEO CÔNG TY", 13);
            r = Bang(ws, r,
                new[] { "Công ty", "", "Tổng", "Hoạt động", "Hỏng", "Bảo trì", "Kho IT", "Khác", "Chưa cấp phát", "Hết BH", "Sắp hết BH", "Chưa kiểm", "Kiểm > 30 ngày" },
                tb.CongTy.Select(c => new object?[] { c.CongTy, null, c.Tong, c.HoatDong, c.Hong, c.BaoTri, c.KhoIt, c.Khac,
                                                      c.ChuaCapPhat, c.HetBaoHanh, c.SapHetBaoHanh, c.ChuaKiem, c.LauChuaKiem }).ToList(),
                new[] { null, null, "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0", "#,##0" });
            r++;

            r = TieuDeMuc(ws, r, "THEO LOẠI THIẾT BỊ", 13);
            r = Bang(ws, r, new[] { "Loại", "", "Số lượng" },
                tb.TheoLoai.Select(x => new object?[] { x.Ten, null, x.SoLuong }).ToList(), new[] { null, null, "#,##0" });
            r++;

            r = TieuDeMuc(ws, r, "THEO BỘ PHẬN", 13);
            r = Bang(ws, r, new[] { "Công ty", "Bộ phận", "Tổng", "Hoạt động", "Hỏng" },
                tb.TheoBoPhan.Select(x => new object?[] { x.CongTy, x.BoPhan, x.Tong, x.HoatDong, x.Hong }).ToList(),
                new[] { null, null, "#,##0", "#,##0", "#,##0" });
            r++;
            r = GhiChu(ws, r, "Không tính thiết bị đã xoá và thiết bị trong danh sách chặn. Sắp hết BH = còn ≤ 90 ngày.");
            ThietLapIn(ws, null);
        }

        // ---------------- Đơn IT / Công việc ----------------

        private static void SheetDon(XLWorkbook wb, string tenSheet, string tieuDe, BaoCaoDinhKyService.ThongKeDon t, string phuDe)
        {
            var dsTt = BaoCaoDinhKyService.DsTrangThaiDon;
            var ws = TrangMoi(wb, tenSheet, new[] { 30, 11, 11, 13, 11, 13, 11, 15 });
            var r = DauTrang(ws, tieuDe, phuDe, 8);

            r = TieuDeMuc(ws, r, "THEO TRẠNG THÁI", 8);
            var dongTt = t.TheoTrangThai.Select(x => new object?[] { x.Ten, x.SoLuong, t.Tong > 0 ? (double)x.SoLuong / t.Tong : null }).ToList();
            dongTt.Add(new object?[] { "Tổng", t.Tong, null });
            if (t.QuaHan != null) dongTt.Add(new object?[] { "Đang quá hạn", t.QuaHan, t.Tong > 0 ? (double)t.QuaHan / t.Tong : null });
            r = Bang(ws, r, new[] { "Trạng thái", "Số đơn", "Tỷ lệ" }, dongTt, new[] { null, "#,##0", "0.0%" });
            r++;

            r = TieuDeMuc(ws, r, "THEO NGƯỜI HỖ TRỢ (người được gán sau cùng)", 8);
            var cotNguoi = new[] { "Người hỗ trợ", "Tổng" }.Concat(dsTt).Append("TB xử lý (giờ)").ToArray();
            r = Bang(ws, r, cotNguoi,
                t.TheoNguoiHoTro.Select(n => new object?[] { n.Ten, n.Tong }
                    .Concat(dsTt.Select(s => (object?)n.TheoTrangThai.GetValueOrDefault(s)))
                    .Append(n.PhutTrungBinh != null ? Math.Round(n.PhutTrungBinh.Value / 60, 1) : null).ToArray()).ToList(),
                cotNguoi.Select((_, i) => i == 0 ? null : i == cotNguoi.Length - 1 ? "#,##0.0" : "#,##0").ToArray());
            r++;

            foreach (var (ten, cotTen, ds) in new[] { ("THEO BỘ PHẬN YÊU CẦU", "Bộ phận", t.TheoBoPhan), ("THEO DANH MỤC", "Danh mục", t.TheoDanhMuc), ("THEO MÃ FORM", "Mã form", t.TheoMaForm) })
            {
                r = TieuDeMuc(ws, r, ten, 8);
                r = Bang(ws, r, new[] { cotTen, "Số đơn" },
                    ds.Select(x => new object?[] { x.Ten, x.SoLuong }).ToList(), new[] { null, "#,##0" });
                r++;
            }
            GhiChu(ws, r, "Tính các đơn có thời điểm quản lý duyệt trong kỳ. TB xử lý = từ lúc quản lý duyệt đến lúc IT/admin hoàn tất.");
            ThietLapIn(ws, null);
        }

        // ---------------- Khung trình bày ----------------

        private static IXLWorksheet TrangMoi(XLWorkbook wb, string ten, int[] doRong)
        {
            var ws = wb.Worksheets.Add(ten);
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 10;
            for (var i = 0; i < doRong.Length; i++) ws.Column(i + 1).Width = doRong[i];
            ws.ShowGridLines = false;
            return ws;
        }

        private static int DauTrang(IXLWorksheet ws, string tieuDe, string phuDe, int soCot)
        {
            var o = ws.Range(1, 1, 1, soCot).Merge();
            o.Value = tieuDe;
            o.Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(MauChinh)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Row(1).Height = 26;
            var p = ws.Range(2, 1, 2, soCot).Merge();
            p.Value = phuDe;
            p.Style.Font.SetFontColor(MauChuPhu).Font.SetItalic();
            return 4;
        }

        private static int TieuDeMuc(IXLWorksheet ws, int r, string noiDung, int soCot)
        {
            var o = ws.Range(r, 1, r, soCot).Merge();
            o.Value = noiDung;
            o.Style.Font.SetBold().Font.SetFontColor(MauChinh).Border.SetBottomBorder(XLBorderStyleValues.Medium).Border.SetBottomBorderColor(MauChinh);
            return r + 1;
        }

        private static int Bang(IXLWorksheet ws, int r, string[] cot, List<object?[]> dong, string?[] dinhDang)
        {
            var dauBang = r;
            for (var c = 0; c < cot.Length; c++)
            {
                var o = ws.Cell(r, c + 1);
                o.Value = cot[c];
                o.Style.Font.SetBold().Fill.SetBackgroundColor(MauNhat).Alignment.SetWrapText(true)
                    .Alignment.SetHorizontal(c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center);
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
                    GanGiaTri(o, c < dong[i].Length ? dong[i][c] : null);
                    if (dinhDang[c] != null) o.Style.NumberFormat.Format = dinhDang[c];
                    if (i % 2 == 1) o.Style.Fill.SetBackgroundColor(MauSoc);
                }
            }
            ws.Range(dauBang, 1, r - 1, cot.Length).Style
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(MauVien).Border.SetInsideBorderColor(MauVien);
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

        private static int GhiChu(IXLWorksheet ws, int r, string noiDung)
        {
            ws.Cell(r, 1).Value = noiDung;
            ws.Cell(r, 1).Style.Font.SetFontColor(MauChuPhu).Font.SetFontSize(9);
            return r + 1;
        }

        private static void ThietLapIn(IXLWorksheet ws, int? dongLap)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.SetLeft(0.4).SetRight(0.4).SetTop(0.5).SetBottom(0.5);
            if (dongLap != null) ws.PageSetup.SetRowsToRepeatAtTop(dongLap.Value, dongLap.Value);
        }
    }
}
