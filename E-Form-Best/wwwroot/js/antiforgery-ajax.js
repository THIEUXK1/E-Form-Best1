// Tự động gắn token chống giả mạo (CSRF) vào mọi request AJAX ghi dữ liệu của Area ITForm.
//
// Vì sao làm ở một chỗ thay vì sửa từng lời gọi: module Kiểm kê có hơn 30 lời gọi $.ajax nằm rải
// trong các view lớn (IndexThietBi.cshtml gần 3.1k dòng). Sửa tay từng chỗ dễ sót một cái, mà sót
// thì server trả 400 và tính năng đó chết im lặng trên production. Đặt prefilter một lần ở đây thì
// mọi lời gọi cũ lẫn mới đều được bảo vệ.
//
// Token do @Html.AntiForgeryToken() trong _Layout.cshtml sinh ra. Tên header "RequestVerificationToken"
// là mặc định của ASP.NET Core, khớp với các endpoint đã dùng [ValidateAntiForgeryToken] sẵn trong repo.
(function () {
    'use strict';

    if (typeof window.jQuery === 'undefined') return;

    function layToken() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : null;
    }

    jQuery.ajaxPrefilter(function (options) {
        // GET/HEAD không đổi dữ liệu nên không cần token.
        var method = (options.type || options.method || 'GET').toUpperCase();
        if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS') return;

        // Chỉ gắn cho request về chính máy chủ này. Tuyệt đối không gửi token sang tên miền khác.
        var url = options.url || '';
        if (/^https?:\/\//i.test(url) && url.indexOf(window.location.origin) !== 0) return;

        var token = layToken();
        if (!token) return;

        options.headers = options.headers || {};
        if (!options.headers['RequestVerificationToken']) {
            options.headers['RequestVerificationToken'] = token;
        }
    });
})();
