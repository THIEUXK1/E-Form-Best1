namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đối chiếu tên file client xin tải với giá trị cột lưu file trong DB, để biết file thuộc đơn nào rồi
    /// kiểm quyền xem đơn đó. Trước đây các route DownloadFile chỉ cần đúng tên file là tải được file của
    /// bất kỳ đơn nào (tên file chứa ID đơn + timestamp nên đoán được).
    /// Cột có thể chứa nhiều file nối bằng '|' (hoặc ';'), có dòng cũ lưu cả đường dẫn đầy đủ.
    /// Truy vấn DB dùng Contains để lọc thô, còn ở đây so khớp ĐÚNG tên — tránh "abc.jpg" khớp nhầm "xabc.jpg"
    /// của đơn khác mà người xin tải có quyền.
    /// </summary>
    public static class TepDinhKem
    {
        public static bool CoChua(string? giaTri, string tenFile)
        {
            if (string.IsNullOrEmpty(giaTri) || string.IsNullOrEmpty(tenFile)) return false;
            foreach (var phan in giaTri.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ten = phan.Trim().Replace('/', '\\');
                int viTri = ten.LastIndexOf('\\');
                if (viTri >= 0) ten = ten[(viTri + 1)..];
                if (string.Equals(ten, tenFile, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Id các đơn có cột file khớp đúng tên file (đã lọc thô ở SQL).</summary>
        public static List<int> IdDonKhop(IEnumerable<(int? IdDon, string? GiaTri)> dong, string tenFile) =>
            dong.Where(d => d.IdDon != null && CoChua(d.GiaTri, tenFile)).Select(d => d.IdDon!.Value).Distinct().ToList();
    }
}
