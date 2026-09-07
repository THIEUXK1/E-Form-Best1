// Mức phóng to/thu nhỏ trang (% kích thước) do người dùng chọn — lưu ở cookie
// siteZoomGlobal, phần đổi mức nằm trong form-layout-chung.js.
//
// File này chỉ làm một việc: ÁP mức đó ngay trong <head>, trước khi thân trang
// được vẽ. Nếu để initZoom() ở DOMContentLoaded áp thì trang đã vẽ xong ở 100%
// rồi mới co/giãn lại, nên mỗi lần chuyển mục trong menu là thấy nháy một cái.
// Lúc này chưa có <body> để gán style nội tuyến, nên chèn thẳng một quy tắc CSS
// vào <head> — trình duyệt tính mức phóng ngay từ nhịp vẽ đầu tiên.
(function () {
    var m = document.cookie.match(/(?:^|;\s*)siteZoomGlobal=([^;]+)/);
    if (!m) return;

    var pct = parseInt(decodeURIComponent(m[1]), 10);
    // 100% là mặc định, không cần chèn gì; ngoài khoảng cho phép thì bỏ qua cho an toàn
    if (!pct || pct === 100 || pct < 25 || pct > 300) return;

    var st = document.createElement("style");
    st.id = "zoomSom";
    st.textContent = "body{zoom:" + pct + "%}";
    document.head.appendChild(st);
})();
