// Trang chủ: nút "Hệ thống liên quan" cạnh nút tài khoản — bấm mở/đóng bảng các hệ thống khác (mở tab mới).
// Danh sách do server render sẵn (đã lọc vai trò), JS chỉ lo bật/tắt.
(function () {
    function dongTatCa() {
        document.querySelectorAll('.htlq-khung:not([hidden])').forEach(function (k) {
            k.hidden = true;
            var n = k.parentElement.querySelector('.htlq-nut');
            if (n) n.setAttribute('aria-expanded', 'false');
        });
    }

    // Ô có data-anh: không mở hệ thống, chỉ hiện ảnh chụp màn hình phóng to trên lớp phủ.
    function moAnh(duongDan, ten) {
        var phu = document.getElementById('htlqXemAnh');
        if (!phu) {
            phu = document.createElement('div');
            phu.id = 'htlqXemAnh';
            phu.className = 'htlq-xem-anh';
            phu.innerHTML = '<div class="htlq-xem-anh-dau"><span class="htlq-xem-anh-ten"></span>'
                + '<button type="button" class="htlq-xem-anh-dong" aria-label="Đóng">&times;</button></div>'
                + '<img alt="">';
            document.body.appendChild(phu);
        }
        phu.querySelector('.htlq-xem-anh-ten').textContent = ten || '';
        var anh = phu.querySelector('img');
        anh.src = duongDan;
        anh.alt = ten || '';
        phu.hidden = false;
    }

    function dongAnh() {
        var phu = document.getElementById('htlqXemAnh');
        if (phu) phu.hidden = true;
    }

    document.addEventListener('click', function (e) {
        var oAnh = e.target.closest('.htlq-o[data-anh]');
        if (oAnh) {
            e.preventDefault();
            dongTatCa();
            moAnh(oAnh.getAttribute('data-anh'), oAnh.getAttribute('data-ten'));
            return;
        }
        // Bấm nền tối hoặc nút ✕ thì đóng ảnh; bấm vào chính ảnh thì giữ.
        if (e.target.closest('.htlq-xem-anh')) {
            if (!e.target.closest('.htlq-xem-anh img')) dongAnh();
            return;
        }

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
        if (e.key === 'Escape') { dongAnh(); dongTatCa(); }
    });
})();
