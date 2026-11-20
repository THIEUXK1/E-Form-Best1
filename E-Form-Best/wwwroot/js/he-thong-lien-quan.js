// Trang chủ: nút "Hệ thống liên quan" cạnh nút tài khoản — bấm mở/đóng bảng các hệ thống khác (mở tab mới).
// Danh sách do server render sẵn (đã lọc quyền), JS chỉ lo bật/tắt.
(function () {
    function dongTatCa() {
        document.querySelectorAll('.htlq-khung:not([hidden])').forEach(function (k) {
            k.hidden = true;
            var n = k.parentElement.querySelector('.htlq-nut');
            if (n) n.setAttribute('aria-expanded', 'false');
        });
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('.htlq-nut');
        if (nut) {
            var khung = nut.parentElement.querySelector('.htlq-khung');
            var dangDong = khung.hidden;
            dongTatCa();
            khung.hidden = !dangDong;
            nut.setAttribute('aria-expanded', String(dangDong));
            return;
        }
        if (e.target.closest('.htlq-dong')) { dongTatCa(); return; }
        // Bấm vào một ô thì để link mở tab mới rồi đóng bảng; bấm ra ngoài cũng đóng.
        if (!e.target.closest('.htlq-khung') || e.target.closest('.htlq-o')) dongTatCa();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') dongTatCa();
    });
})();
