// Nút "Thông tin & Bảo mật" ở trang chủ: bấm mở menu chọn Xem thông tin / Đăng xuất thay vì mở thẳng trang hồ sơ.
document.addEventListener('click', function (e) {
    var nut = e.target.closest('.nut-tai-khoan');
    var dsMo = document.querySelectorAll('.menu-tai-khoan-ds:not([hidden])');

    if (nut) {
        e.preventDefault();
        var ds = nut.parentElement.querySelector('.menu-tai-khoan-ds');
        var dangDong = ds.hidden;
        dsMo.forEach(function (d) { d.hidden = true; });
        ds.hidden = !dangDong;
        nut.setAttribute('aria-expanded', String(dangDong));
        return;
    }

    // Bấm ra ngoài hoặc chọn một mục → đóng menu.
    dsMo.forEach(function (d) {
        d.hidden = true;
        var n = d.parentElement.querySelector('.nut-tai-khoan');
        if (n) n.setAttribute('aria-expanded', 'false');
    });
});

document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    document.querySelectorAll('.menu-tai-khoan-ds:not([hidden])').forEach(function (d) { d.hidden = true; });
});
