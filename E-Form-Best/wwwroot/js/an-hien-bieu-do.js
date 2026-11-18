// Nút ẩn/hiện hàng biểu đồ trên các trang giám sát (Camera, AP, Switch, Server).
// Nút: <button data-an-hien="#idHang" data-khoa="khoaLocalStorage"><i></i><span></span></button>
// Trạng thái nhớ theo từng máy (localStorage) nên F5 vẫn giữ; localStorage có thể bị chặn nên bọc try.
(function () {
    'use strict';

    function docAn(khoa) {
        try { return localStorage.getItem(khoa) === '1'; } catch (e) { return false; }
    }

    function ghiAn(khoa, an) {
        try { localStorage.setItem(khoa, an ? '1' : '0'); } catch (e) { }
    }

    function datTrangThai(nut, an) {
        var hang = document.querySelector(nut.getAttribute('data-an-hien'));
        if (hang) hang.classList.toggle('d-none', an);
        var chu = an ? 'Hiện biểu đồ' : 'Ẩn biểu đồ';
        nut.title = chu;
        var icon = nut.querySelector('i');
        if (icon) icon.className = (an ? 'fas fa-eye' : 'fas fa-eye-slash') + ' me-1';
        var span = nut.querySelector('span');
        if (span) span.textContent = chu;
        // Bố cục cố định 1 màn hình + Chart.js tính lại kích thước theo sự kiện resize
        window.dispatchEvent(new Event('resize'));
    }

    // Áp trạng thái đã nhớ ngay khi DOM sẵn sàng để tránh nháy biểu đồ
    function khoiTao() {
        document.querySelectorAll('[data-an-hien][data-khoa]').forEach(function (nut) {
            datTrangThai(nut, docAn(nut.getAttribute('data-khoa')));
        });
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('[data-an-hien][data-khoa]');
        if (!nut) return;
        e.preventDefault();
        var khoa = nut.getAttribute('data-khoa');
        var an = !docAn(khoa);
        ghiAn(khoa, an);
        datTrangThai(nut, an);
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', khoiTao);
    else khoiTao();
})();
