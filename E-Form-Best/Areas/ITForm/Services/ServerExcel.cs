using ClosedXML.Excel;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// File Excel danh sách Server và lưu trữ của 1 công ty (/QLServer/{công ty}/XuatExcel).
    /// Chỉ trình bày, số liệu do QLServerController chiếu sẵn.
    /// </summary>
    public static class ServerExcel
    {
        private static readonly XLColor MauChinh = XLColor.FromHtml("#4338ca");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#e0e7ff");
        private static readonly XLColor MauSoc = XLColor.FromHtml("#f5f7ff");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");

        public record Dong(string? Ma, string Ten, string Loai, string? ChucNang, string? Ip, string? IpQuanTri,
            string? HangModel, string? Serial, string? HeDieuHanh, string? Cpu, int? RamGb,
            decimal? DungLuongTb, decimal? DaDungTb, string? Raid, string? BoPhan, string? ViTri, string? TinhTrang,
            string KetNoi, DateTime? KiemTraLuc, DateOnly? NgayLapDat, DateOnly? HanBaoHanh, string? GhiChu);

        public static byte[] TaoDanhSach(IReadOnlyList<Dong> ds, string congTy, string? nguoiLap)
        {
            var cot = new[] { "#", "Mã", "Tên", "Loại", "Chức năng", "IP", "IP quản trị", "Hãng / Model", "Serial", "Hệ điều hành",
                              "CPU", "RAM (GB)", "Dung lượng (TB)", "Đã dùng (TB)", "% dùng", "RAID", "Bộ phận", "Vị trí",
                              "Tình trạng", "Kết nối", "Kiểm tra lúc", "Ngày lắp", "Hạn BH", "Ghi chú" };
            var doRong = new[] { 6, 12, 24, 10, 22, 15, 15, 24, 18, 22, 22, 10, 13, 12, 9, 10, 18, 22, 15, 12, 16, 12, 12, 30 };
            var dinhDang = new Dictionary<int, string> { [12] = "0", [13] = "0.00", [14] = "0.00", [15] = "0%", [21] = "dd/MM/yyyy HH:mm", [22] = "dd/MM/yyyy", [23] = "dd/MM/yyyy" };

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Server và lưu trữ");
            ws.ShowGridLines = false;
            ws.Style.Font.FontName = "Segoe UI";
            ws.Style.Font.FontSize = 10;
            for (var c = 0; c < doRong.Length; c++) ws.Column(c + 1).Width = doRong[c];

            var tieuDe = ws.Range(1, 1, 1, cot.Length).Merge();
            tieuDe.Value = "DANH SÁCH SERVER VÀ LƯU TRỮ " + congTy;
            tieuDe.Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(MauChinh);
            tieuDe.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetIndent(1);
            ws.Row(1).Height = 28;

            var phuDe = ws.Range(2, 1, 2, cot.Length).Merge();
            phuDe.Value = $"Xuất lúc: {DateTime.Now:HH:mm dd/MM/yyyy}   ·   {ds.Count} thiết bị"
                          + (string.IsNullOrWhiteSpace(nguoiLap) ? "" : "   ·   Người lập: " + nguoiLap);
            phuDe.Style.Font.SetFontColor(MauChuPhu).Font.SetItalic().Fill.SetBackgroundColor(MauNhat).Alignment.SetIndent(1);

            const int dauBang = 4;
            for (var c = 0; c < cot.Length; c++)
            {
                var o = ws.Cell(dauBang, c + 1);
                o.Value = cot[c];
                o.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(MauChinh)
                    .Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            }

            for (var i = 0; i < ds.Count; i++)
            {
                var x = ds[i];
                decimal? tyLe = x.DungLuongTb > 0 && x.DaDungTb != null ? x.DaDungTb / x.DungLuongTb : null;
                var giaTri = new object?[]
                {
                    i + 1, x.Ma, x.Ten, x.Loai, x.ChucNang, x.Ip, x.IpQuanTri, x.HangModel, x.Serial, x.HeDieuHanh,
                    x.Cpu, x.RamGb, x.DungLuongTb, x.DaDungTb, tyLe, x.Raid, x.BoPhan, x.ViTri, x.TinhTrang, x.KetNoi,
                    x.KiemTraLuc, x.NgayLapDat?.ToDateTime(TimeOnly.MinValue), x.HanBaoHanh?.ToDateTime(TimeOnly.MinValue), x.GhiChu
                };
                var r = dauBang + 1 + i;
                for (var c = 0; c < giaTri.Length; c++)
                {
                    var o = ws.Cell(r, c + 1);
                    o.Value = XLCellValue.FromObject(giaTri[c]);
                    if (dinhDang.TryGetValue(c + 1, out var dd)) o.Style.NumberFormat.Format = dd;
                }
                if (i % 2 == 1) ws.Range(r, 1, r, cot.Length).Style.Fill.SetBackgroundColor(MauSoc);
            }

            var cuoi = dauBang + Math.Max(ds.Count, 1);
            var bang = ws.Range(dauBang, 1, cuoi, cot.Length);
            bang.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(MauVien).Border.SetInsideBorderColor(MauVien)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            if (ds.Count > 0) ws.Range(dauBang, 1, cuoi, cot.Length).SetAutoFilter();
            ws.SheetView.FreezeRows(dauBang);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(dauBang, dauBang);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
