using ClosedXML.Excel;
using System.Globalization;
using System.Text;

namespace E_Form_Best.Areas.HRform.Services
{
    /// <summary>
    /// File Excel cho /FormHR/QuanLyTuDienThoai: tạo file mẫu nhập tủ và đọc file người dùng tải lên.
    /// Chỉ lo định dạng; đối chiếu bộ phận / tủ đã có và ghi DB do TuDienThoaiController làm.
    /// </summary>
    public static class TuDienThoaiExcel
    {
        public const string TenSheetDuLieu = "TuDienThoai";
        public const int SoDongToiDa = 2000;

        private static readonly XLColor MauChinh = XLColor.FromHtml("#7c3aed");
        private static readonly XLColor MauNhat = XLColor.FromHtml("#ede9fe");
        private static readonly XLColor MauVien = XLColor.FromHtml("#d4d4d8");
        private static readonly XLColor MauChuPhu = XLColor.FromHtml("#64748b");

        // Thứ tự cột của file mẫu; khi đọc thì dò theo tên cột nên người dùng đổi thứ tự cột vẫn được
        private static readonly (string Ten, int Rong, string GhiChu)[] Cot =
        {
            ("Mã BP", 10, "Mã ngắn của bộ phận (QC, HR…) — xem sheet BoPhan. Để trống cột Bộ phận thì hệ thống tìm bộ phận theo mã này."),
            ("Bộ phận", 34, "Chọn trong danh sách (đúng tên bộ phận trên hệ thống). Bắt buộc nếu không ghi Mã BP hoặc mã trùng nhiều bộ phận."),
            ("Tên tủ", 18, "Bắt buộc, tối đa 100 ký tự. Cùng bộ phận không được trùng tên."),
            ("Vị trí tủ", 40, "Không bắt buộc, tối đa 200 ký tự."),
            ("Mã NV", 14, "Mã nhân viên người dùng tủ. Để trống nếu tủ còn trống."),
            ("Người sử dụng", 28, "Họ tên người dùng tủ. Để trống nếu tủ còn trống."),
        };

        /// <summary>Một dòng đọc từ file (đã gọn khoảng trắng). Dong = số dòng trên Excel để báo lỗi đúng chỗ.</summary>
        public record DongNhap(int Dong, string? MaBoPhan, string? BoPhan, string? TenTu, string? ViTri, string? MaNhanVien, string? NguoiSuDung);

        public static byte[] TaoFileMau(IReadOnlyList<(string Ten, string? Ma)> dsBoPhan)
        {
            using var wb = new XLWorkbook();

            var ws = wb.AddWorksheet(TenSheetDuLieu);
            for (int c = 0; c < Cot.Length; c++)
            {
                var o = ws.Cell(1, c + 1);
                o.Value = Cot[c].Ten;
                o.GetComment().AddText(Cot[c].GhiChu);
                ws.Column(c + 1).Width = Cot[c].Rong;
            }
            var dau = ws.Range(1, 1, 1, Cot.Length);
            dau.Style.Font.Bold = true;
            dau.Style.Font.FontColor = XLColor.White;
            dau.Style.Fill.BackgroundColor = MauChinh;
            dau.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(1).Height = 22;
            ws.SheetView.FreezeRows(1);
            // Mã BP, Mã NV dạng chữ để không mất số 0 đầu
            ws.Range(2, 1, SoDongToiDa + 1, 1).Style.NumberFormat.Format = "@";
            ws.Range(2, 5, SoDongToiDa + 1, 5).Style.NumberFormat.Format = "@";

            // Danh sách bộ phận ở sheet riêng làm nguồn ô chọn cho cột Bộ phận
            var wsBp = wb.AddWorksheet("BoPhan");
            wsBp.Cell(1, 1).Value = "Bộ phận trên hệ thống";
            wsBp.Cell(1, 2).Value = "Mã BP";
            wsBp.Range(1, 1, 1, 2).Style.Font.Bold = true;
            wsBp.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = MauNhat;
            for (int i = 0; i < dsBoPhan.Count; i++)
            {
                wsBp.Cell(i + 2, 1).Value = dsBoPhan[i].Ten;
                wsBp.Cell(i + 2, 2).Value = dsBoPhan[i].Ma ?? "";
            }
            wsBp.Column(1).Width = 50;
            wsBp.Column(2).Width = 12;
            if (dsBoPhan.Count > 0)
            {
                var dv = ws.Range(2, 2, SoDongToiDa + 1, 2).CreateDataValidation();
                dv.List(wsBp.Range(2, 1, dsBoPhan.Count + 1, 1), true);
                dv.ErrorTitle = "Bộ phận không có";
                dv.ErrorMessage = "Chọn bộ phận trong danh sách (sheet BoPhan).";
            }

            var hd = wb.AddWorksheet("HuongDan");
            string[] dong =
            {
                "HƯỚNG DẪN NHẬP TỦ ĐIỆN THOẠI",
                "",
                "1. Điền mỗi tủ 1 dòng ở sheet \"" + TenSheetDuLieu + "\", bắt đầu từ dòng 2, giữ nguyên dòng tiêu đề.",
                "2. Cột Bộ phận chọn trong danh sách thả xuống (lấy từ sheet BoPhan). Hoặc chỉ ghi Mã BP (QC, HR…) và để trống Bộ phận — mã trùng nhiều bộ phận thì phải ghi tên Bộ phận.",
                "3. Tủ đã có (cùng Bộ phận + Tên tủ) sẽ được CẬP NHẬT vị trí / Mã NV / Người sử dụng theo file — ô để trống nghĩa là xoá giá trị cũ (tủ còn trống).",
                "4. Tủ chưa có sẽ được THÊM MỚI. Tủ không có trong file thì giữ nguyên, không bị xoá.",
                "5. Trên trang, bấm \"Nhập từ Excel\" → chọn file → \"Kiểm tra\" để xem trước, rồi mới bấm \"Nhập\".",
                "6. Nhập lại cùng một file không sinh tủ trùng. Tối đa " + SoDongToiDa + " dòng mỗi lần.",
                "",
                "Ví dụ:",
            };
            for (int i = 0; i < dong.Length; i++) hd.Cell(i + 1, 1).Value = dong[i];
            hd.Cell(1, 1).Style.Font.Bold = true;
            hd.Cell(1, 1).Style.Font.FontSize = 14;
            hd.Cell(1, 1).Style.Font.FontColor = MauChinh;
            int r = dong.Length + 1;
            for (int c = 0; c < Cot.Length; c++)
            {
                hd.Cell(r, c + 1).Value = Cot[c].Ten;
                hd.Cell(r, c + 1).Style.Font.Bold = true;
                hd.Cell(r, c + 1).Style.Fill.BackgroundColor = MauNhat;
            }
            var bp0 = dsBoPhan.FirstOrDefault(b => !string.IsNullOrEmpty(b.Ma));
            if (bp0.Ten == null) bp0 = dsBoPhan.Count > 0 ? dsBoPhan[0] : ("Kho", "KHO");
            object[][] viDu =
            {
                new object[] { bp0.Ma ?? "", bp0.Ten, "Tủ A-01", "Cửa ra vào xưởng số 1", "V123456", "Nguyễn Văn A" },
                new object[] { bp0.Ma ?? "", "", "Tủ A-02", "Cửa ra vào xưởng số 1", "", "" },
            };
            foreach (var v in viDu)
            {
                r++;
                for (int c = 0; c < v.Length; c++)
                {
                    hd.Cell(r, c + 1).Value = v[c]?.ToString() ?? "";
                    hd.Cell(r, c + 1).Style.Font.FontColor = MauChuPhu;
                }
            }
            hd.Range(dong.Length + 1, 1, r, Cot.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hd.Range(dong.Length + 1, 1, r, Cot.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            hd.Range(dong.Length + 1, 1, r, Cot.Length).Style.Border.OutsideBorderColor = MauVien;
            hd.Range(dong.Length + 1, 1, r, Cot.Length).Style.Border.InsideBorderColor = MauVien;
            hd.Column(1).Width = 34; hd.Column(2).Width = 30; hd.Column(3).Width = 14; hd.Column(4).Width = 28; hd.Column(5).Width = 12; hd.Column(6).Width = 22;

            ws.SetTabActive();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Đọc sheet dữ liệu (sheet "TuDienThoai", không có thì sheet đầu). Dò cột theo tên ở dòng tiêu đề.
        /// Ném InvalidDataException với thông báo dễ hiểu khi file sai mẫu.
        /// </summary>
        public static List<DongNhap> DocFile(Stream luong)
        {
            XLWorkbook wb;
            try { wb = new XLWorkbook(luong); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw new InvalidDataException("Không đọc được file. Hãy dùng file .xlsx (tải mẫu Excel trên trang).", ex);
            }
            using (wb)
            {
                var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, TenSheetDuLieu, StringComparison.OrdinalIgnoreCase))
                         ?? wb.Worksheets.First();
                var dauRange = ws.FirstRowUsed();
                if (dauRange == null) return new List<DongNhap>();

                // Tên cột → chỉ số cột (bỏ dấu, bỏ khoảng trắng để "Mã nhân viên", "ma nv"... đều nhận)
                var viTriCot = new Dictionary<string, int>();
                foreach (var o in dauRange.CellsUsed())
                {
                    var k = BoDau(o.GetString());
                    if (!viTriCot.ContainsKey(k)) viTriCot[k] = o.Address.ColumnNumber;
                }
                int? Tim(params string[] ten) => ten.Select(BoDau).Where(viTriCot.ContainsKey).Select(k => (int?)viTriCot[k]).FirstOrDefault();
                int? cMaBp = Tim("Mã BP", "Mã bộ phận");
                int? cBp = Tim("Bộ phận", "Bo phan", "Phòng ban");
                int? cTen = Tim("Tên tủ", "Ten tu", "Tủ");
                int? cViTri = Tim("Vị trí tủ", "Vị trí");
                int? cMa = Tim("Mã NV", "Mã nhân viên", "MSNV");
                int? cNguoi = Tim("Người sử dụng", "Người dùng", "Họ tên");
                if ((cBp == null && cMaBp == null) || cTen == null)
                    throw new InvalidDataException("File thiếu cột \"Bộ phận\" (hoặc \"Mã BP\") hay \"Tên tủ\" ở dòng tiêu đề. Hãy dùng file mẫu tải trên trang.");

                int dongDau = dauRange.RowNumber() + 1;
                int dongCuoi = ws.LastRowUsed()?.RowNumber() ?? dongDau - 1;
                if (dongCuoi - dongDau + 1 > SoDongToiDa + 50)
                    throw new InvalidDataException($"File quá nhiều dòng (tối đa {SoDongToiDa} tủ mỗi lần).");

                string? Doc(int dong, int? cot) => cot == null ? null : Gon(ws.Cell(dong, cot.Value).GetFormattedString());
                var kq = new List<DongNhap>();
                for (int d = dongDau; d <= dongCuoi; d++)
                {
                    var dn = new DongNhap(d, Doc(d, cMaBp), Doc(d, cBp), Doc(d, cTen), Doc(d, cViTri), Doc(d, cMa), Doc(d, cNguoi));
                    if (dn.MaBoPhan == null && dn.BoPhan == null && dn.TenTu == null && dn.ViTri == null && dn.MaNhanVien == null && dn.NguoiSuDung == null)
                        continue; // dòng trống
                    kq.Add(dn);
                }
                if (kq.Count > SoDongToiDa)
                    throw new InvalidDataException($"File có {kq.Count} tủ, tối đa {SoDongToiDa} tủ mỗi lần.");
                return kq;
            }
        }

        private static string? Gon(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Bỏ dấu, viết hoa, bỏ khoảng trắng: "Mã NV" → "MANV" — dùng so khớp tên cột / tên bộ phận.</summary>
        public static string BoDau(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || char.IsWhiteSpace(c)) continue;
                sb.Append(c);
            }
            return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();
        }
    }
}
