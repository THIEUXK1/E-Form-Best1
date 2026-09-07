// Callback bắt buộc phải là hàm toàn cục và tồn tại TRƯỚC khi nạp script của Google Translate
// (script đó gọi ngược lại tên hàm này qua tham số ?cb=googleTranslateElementInit).
//
// Hai script được nạp ngay trong <head> để widget bắt đầu dịch sớm nhất có thể:
// nạp ở cuối <body> thì trang đã hiện nguyên tiếng Việt một lúc rồi chữ mới lật
// sang ngôn ngữ đang chọn — mỗi lần chuyển mục trong menu là thấy nháy chữ.
// Đổi lại, lúc callback chạy thì ô #google_translate_element (nằm giữa thân
// trang) có thể chưa được phân tích tới, nên phải chờ nó xuất hiện.
function googleTranslateElementInit() {
    function dung() {
        new google.translate.TranslateElement({ pageLanguage: 'vi', autoDisplay: false }, 'google_translate_element');
    }

    // Chờ bằng vòng lặp ngắn thay vì DOMContentLoaded: ô này nằm giữa thân trang
    // nên có sớm hơn hẳn thời điểm trang phân tích xong.
    (function cho() {
        if (document.getElementById('google_translate_element')) {
            dung();
            return;
        }
        if (document.readyState === 'loading') setTimeout(cho, 10);
        else document.addEventListener('DOMContentLoaded', dung, { once: true });
    })();
}
