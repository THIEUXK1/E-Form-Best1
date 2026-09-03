// Mở chi tiết máy tính ở tab mới và cho phép nút "Quay lại danh sách" đóng chính tab đó.
// Phải dùng window.open() thay vì <a target="_blank">: trình duyệt chỉ cho window.close()
// đối với tab do script mở ra.
(function () {
    'use strict';

    document.addEventListener('click', function (e) {
        var link = e.target.closest ? e.target.closest('a.link-chi-tiet-may') : null;
        if (!link) return;
        e.preventDefault();
        var tab = window.open(link.getAttribute('href'), '_blank');
        if (tab) tab.opener = window; // giữ opener để tab con biết mình được script mở
    });

    document.addEventListener('DOMContentLoaded', function () {
        var nut = document.getElementById('btnQuayLaiDanhSach');
        if (!nut) return;
        nut.addEventListener('click', function (e) {
            e.preventDefault();
            window.close();
            // Mở trực tiếp bằng URL (không qua danh sách) thì trình duyệt chặn close → điều hướng bù
            setTimeout(function () {
                if (!window.closed) window.location.href = nut.dataset.urlDanhSach;
            }, 150);
        });
    });
})();
