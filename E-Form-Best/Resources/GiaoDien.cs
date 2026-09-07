namespace E_Form_Best.Resources
{
    /// <summary>
    /// Lớp mốc cho bộ chuỗi giao diện dùng chung (menu bên trái, thanh trên cùng, nút bấm
    /// của 4 layout). View lấy chuỗi qua <c>IHtmlLocalizer&lt;GiaoDien&gt;</c>.
    ///
    /// Khoá tra cứu chính là CÂU TIẾNG VIỆT, ví dụ <c>L["Trang chủ"]</c>. Nhờ vậy tiếng Việt
    /// không cần file resource (thiếu khoá thì trả về chính khoá), và view vẫn đọc được như cũ.
    /// Bản dịch nằm ở <c>Resources/GiaoDien.en.resx</c> và <c>Resources/GiaoDien.zh-CN.resx</c>.
    /// </summary>
    public class GiaoDien
    {
    }
}
