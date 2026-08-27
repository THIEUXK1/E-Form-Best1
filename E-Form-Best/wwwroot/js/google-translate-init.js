// Callback bắt buộc phải là hàm toàn cục và tồn tại TRƯỚC khi nạp script của Google Translate
// (script đó gọi ngược lại tên hàm này qua tham số ?cb=googleTranslateElementInit).
function googleTranslateElementInit() {
    new google.translate.TranslateElement({ pageLanguage: 'vi', autoDisplay: false }, 'google_translate_element');
}
