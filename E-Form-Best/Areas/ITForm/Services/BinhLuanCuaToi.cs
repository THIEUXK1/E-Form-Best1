namespace E_Form_Best.Areas.ITForm.Services
{
    // Bảng LichSu* không có cột người tạo, nên nhận diện "bình luận do chính mình viết/xoá" qua Mota
    // mà phần bình luận của các Area ghi ra. Dùng để header không kêu/không đếm thông báo của chính mình.
    // Các định dạng đang có:
    //   IT, Công việc : "👤 {Tên} ({Mã})\n🏢 ...\n💬 ..."
    //   HR, SHD       : "Người dùng {Tên} đã bình luận: ..."
    //   Xoá bình luận : "{Tên} đã xóa ..."
    public static class BinhLuanCuaToi
    {
        public static bool LaCuaToi(string? moTa, string? tenToi, string? maToi)
        {
            if (string.IsNullOrEmpty(moTa)) return false;
            tenToi = tenToi?.Trim() ?? "";
            maToi = maToi?.Trim() ?? "";

            if (moTa.StartsWith("👤 "))
            {
                var dongDau = moTa.Split('\n')[0].Trim();
                // Có mã thì so mã (trùng tên giữa hai người là chuyện thường), không có mới so tên.
                if (maToi != "") return dongDau.EndsWith($"({maToi})", StringComparison.OrdinalIgnoreCase);
                return tenToi != "" && dongDau.StartsWith($"👤 {tenToi} (");
            }

            if (tenToi == "") return false;
            return moTa.StartsWith($"Người dùng {tenToi} đã bình luận")
                || moTa.StartsWith($"{tenToi} đã xóa");
        }
    }
}
