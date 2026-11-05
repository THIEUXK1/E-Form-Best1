using ClosedXML.Excel;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// File Excel kiểu lịch cho /FormCongViec/LichCongViec: mỗi nhân sự một hàng, mỗi ngày một cột,
    /// mỗi việc là một thanh trải trên những ngày đang làm việc đó. Chỉ lo định dạng; lấy dữ liệu do controller làm.
    /// </summary>
    public static class LichCongViecExcel
    {
        private const int CotNhanSu = 1;
        private const int CotNgayDau = 2;
        private const int DongChuGiai = 2;
        private const int DongNgay = 3;
        private const int DongThu = 4;
        private const int DongDuLieuDau = 5;
        private const int RongCotNgay = 15;
        private const double CaoDongToiThieu = 32;

        private static readonly XLColor MauChinh = XLColor.FromHtml("#65a30d");
        private static readonly XLColor MauHomNay = XLColor.FromHtml("#ea580c");
        private static readonly XLColor MauCuoiTuan = XLColor.FromHtml("#f1f5f9");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly string[] TenThu = { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

        private static readonly (string Ten, string Mau)[] ChuGiai =
        {
            ("Quá hạn", "#dc2626"), ("Hết hạn hôm nay", "#ea580c"), ("Còn ≤ 3 ngày", "#f59e0b"), ("Còn ≤ 7 ngày", "#0ea5e9"),
            ("Còn nhiều", "#0891b2"), ("Chưa đặt hạn", "#8b5cf6"), ("Hoàn tất", "#16a34a"),
        };

        /// <summary>Một việc trên hàng của một nhân sự. TuNgay/DenNgay = các ngày người đó đang làm việc này.</summary>
        public record ThanhViec(string TieuDe, DateTime TuNgay, DateTime DenNgay, string Mau);

        public record HangNhanSu(string HoTen, string Ma, IReadOnlyList<ThanhViec> Viec);

        public static byte[] TaoFile(DateTime tuNgay, DateTime denNgay, IReadOnlyList<HangNhanSu> dsNhanSu)
        {
            int soNgay = (denNgay - tuNgay).Days + 1;
            int cotCuoi = CotNgayDau + soNgay - 1;
            // Khoảng ngắn hơn số ô chú giải thì tiêu đề và chú giải vẫn cần đủ chỗ
            int cotRongNhat = Math.Max(cotCuoi, CotNgayDau + ChuGiai.Length - 1);
            var homNay = DateTime.Now.Date;

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Lich cong viec");

            ws.Cell(1, 1).Value = $"LỊCH CÔNG VIỆC ADMIN IT - TỪ {tuNgay:dd/MM/yyyy} ĐẾN {denNgay:dd/MM/yyyy}";
            var tieuDe = ws.Range(1, 1, 1, cotRongNhat).Merge();
            tieuDe.Style.Font.Bold = true;
            tieuDe.Style.Font.FontSize = 14;
            tieuDe.Style.Font.FontColor = MauChinh;
            ws.Row(1).Height = 26;

            for (int i = 0; i < ChuGiai.Length; i++)
            {
                var o = ws.Cell(DongChuGiai, CotNgayDau + i);
                o.Value = ChuGiai[i].Ten;
                o.Style.Font.FontColor = XLColor.White;
                o.Style.Font.FontSize = 9;
                o.Style.Fill.BackgroundColor = XLColor.FromHtml(ChuGiai[i].Mau);
                o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Cell(DongNgay, CotNhanSu).Value = "Nhân sự";
            ws.Range(DongNgay, CotNhanSu, DongThu, CotNhanSu).Merge();
            ws.Column(CotNhanSu).Width = 26;
            ws.Columns(CotNgayDau, cotRongNhat).Width = RongCotNgay;
            for (int d = 0; d < soNgay; d++)
            {
                var ngay = tuNgay.AddDays(d);
                // Ghi cả tháng vì khoảng chọn có thể vắt qua nhiều tháng
                ws.Cell(DongNgay, CotNgayDau + d).Value = ngay.ToString("dd/MM");
                ws.Cell(DongThu, CotNgayDau + d).Value = TenThu[(int)ngay.DayOfWeek];
            }
            var dau = ws.Range(DongNgay, CotNhanSu, DongThu, cotCuoi);
            dau.Style.Font.Bold = true;
            dau.Style.Font.FontColor = XLColor.White;
            dau.Style.Fill.BackgroundColor = MauChinh;
            dau.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dau.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (homNay >= tuNgay && homNay <= denNgay)
            {
                int cotHomNay = CotNgayDau + (homNay - tuNgay).Days;
                ws.Range(DongNgay, cotHomNay, DongThu, cotHomNay).Style.Fill.BackgroundColor = MauHomNay;
            }

            int dong = DongDuLieuDau;
            foreach (var nv in dsNhanSu)
            {
                var cacLan = XepLan(nv.Viec);
                int dongCuoi = dong + cacLan.Count - 1;

                ws.Cell(dong, CotNhanSu).Value = nv.HoTen + "\n" + nv.Ma;
                var oTen = ws.Range(dong, CotNhanSu, dongCuoi, CotNhanSu).Merge();
                oTen.Style.Font.Bold = true;
                oTen.Style.Alignment.WrapText = true;
                oTen.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Tô cuối tuần trước khi vẽ thanh để thanh việc đè lên nền
                for (int d = 0; d < soNgay; d++)
                {
                    var thu = tuNgay.AddDays(d).DayOfWeek;
                    if (thu == DayOfWeek.Saturday || thu == DayOfWeek.Sunday)
                        ws.Range(dong, CotNgayDau + d, dongCuoi, CotNgayDau + d).Style.Fill.BackgroundColor = MauCuoiTuan;
                }

                for (int lan = 0; lan < cacLan.Count; lan++)
                {
                    ws.Row(dong + lan).Height = cacLan[lan].Select(CaoCan).Append(CaoDongToiThieu).Max();
                    foreach (var v in cacLan[lan])
                    {
                        var thanh = ws.Range(dong + lan, CotNgayDau + (v.TuNgay - tuNgay).Days, dong + lan, CotNgayDau + (v.DenNgay - tuNgay).Days);
                        thanh.FirstCell().Value = v.TieuDe;
                        thanh.Merge();
                        thanh.Style.Fill.BackgroundColor = XLColor.FromHtml(v.Mau);
                        thanh.Style.Font.FontColor = XLColor.White;
                        thanh.Style.Font.FontSize = 9;
                        thanh.Style.Alignment.WrapText = true;
                        thanh.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }
                dong = dongCuoi + 1;
            }

            var bang = ws.Range(DongNgay, CotNhanSu, Math.Max(DongThu, dong - 1), cotCuoi);
            bang.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            bang.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            bang.Style.Border.OutsideBorderColor = MauVien;
            bang.Style.Border.InsideBorderColor = MauVien;

            ws.SheetView.Freeze(DongThu, CotNhanSu);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // Người có việc chồng ngày thì tách thành nhiều làn (mỗi làn một dòng Excel) vì ô gộp không được đè nhau;
        // luôn trả ít nhất 1 làn để người không có việc vẫn có hàng trên lịch.
        private static List<List<ThanhViec>> XepLan(IEnumerable<ThanhViec> dsViec)
        {
            var cacLan = new List<List<ThanhViec>>();
            foreach (var v in dsViec.OrderBy(x => x.TuNgay).ThenBy(x => x.DenNgay))
            {
                var lan = cacLan.FirstOrDefault(l => l[^1].DenNgay < v.TuNgay);
                if (lan == null) cacLan.Add(lan = new List<ThanhViec>());
                lan.Add(v);
            }
            if (cacLan.Count == 0) cacLan.Add(new List<ThanhViec>());
            return cacLan;
        }

        // Excel không tự giãn dòng cho ô gộp, phải ước lượng số dòng chữ từ độ rộng thanh
        private static double CaoCan(ThanhViec v)
        {
            int soNgay = (v.DenNgay - v.TuNgay).Days + 1;
            int soDong = (int)Math.Ceiling(v.TieuDe.Length / (soNgay * (double)RongCotNgay));
            return Math.Min(soDong * 13 + 6, 200);
        }
    }
}
