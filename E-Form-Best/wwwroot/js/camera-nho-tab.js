// Nhớ tab đang mở trên trang Camera để khi chuyển công ty (BPVN/PFVN/MEGA) mở sẵn đúng tab đó.
// Sở thích riêng từng máy → localStorage. Nạp SAU các file camera-*.js để handler shown.bs.tab
// của từng tab đã gắn xong, khi kích hoạt tab nhớ thì dữ liệu tab đó vẫn được tải như bấm tay.
(function ($) {
    'use strict';

    var KHOA_TAB = 'camera.tabDangMo';

    $(function () {
        var $nhom = $('#cameraTab');
        if (!$nhom.length) return;

        // Ghi lại mỗi lần đổi tab (kể cả do khôi phục — cùng giá trị nên vô hại).
        $nhom.on('shown.bs.tab', 'button[data-bs-toggle="pill"]', function () {
            try { localStorage.setItem(KHOA_TAB, this.id); } catch (e) { /* storage bị chặn: chỉ không nhớ tab */ }
        });

        var idTab = null;
        try { idTab = localStorage.getItem(KHOA_TAB); } catch (e) { idTab = null; }
        if (!idTab) return;

        // Tab không có ở công ty này (vd. "Tài sản camera" chỉ BPVN) → giữ tab mặc định,
        // không ghi đè để quay lại BPVN vẫn mở đúng tab cũ.
        var nut = document.getElementById(idTab);
        if (!nut || !$nhom[0].contains(nut) || nut.classList.contains('active')) return;
        bootstrap.Tab.getOrCreateInstance(nut).show();
    });
})(jQuery);
