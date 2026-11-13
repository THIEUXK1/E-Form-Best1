// Hành vi chung của layout QA: bật/tắt sidebar trên mobile và menu tài khoản.
(function () {
    document.addEventListener('click', function (e) {
        var body = document.body;

        if (e.target.closest('#qa-toggle')) {
            body.classList.toggle('qa-sidebar-open');
            return;
        }
        if (e.target.closest('#qa-backdrop')) {
            body.classList.remove('qa-sidebar-open');
            return;
        }

        var user = document.querySelector('.qa-user');
        if (!user) return;
        if (e.target.closest('#qa-user-btn')) {
            user.classList.toggle('open');
        } else if (!e.target.closest('#qa-user-menu')) {
            user.classList.remove('open');
        }
    });

    // Bảng QA nhiều cột: mặc định chỉ hiện cột quan trọng (th/td có lớp qa-cot-phu bị ẩn);
    // nút [data-hien-cot="<id bảng>"] bật/tắt, nhớ theo từng bảng trên trình duyệt này
    function apHienCot(nut, hienHet) {
        var bang = document.getElementById(nut.dataset.hienCot);
        if (!bang) return;
        bang.classList.toggle('qa-hien-het', hienHet);
        nut.classList.toggle('active', hienHet);
        var chu = nut.querySelector('span');
        if (chu) chu.textContent = hienHet ? 'Chỉ cột chính' : 'Hiện tất cả cột';
    }
    document.querySelectorAll('[data-hien-cot]').forEach(function (nut) {
        var hien = false;
        try { hien = localStorage.getItem('qa-hien-cot-' + nut.dataset.hienCot) === '1'; } catch (e) { /* storage bị chặn: dùng mặc định */ }
        apHienCot(nut, hien);
    });
    document.addEventListener('click', function (e) {
        var nut = e.target.closest('[data-hien-cot]');
        if (!nut) return;
        var bang = document.getElementById(nut.dataset.hienCot);
        if (!bang) return;
        var hien = !bang.classList.contains('qa-hien-het');
        apHienCot(nut, hien);
        try { localStorage.setItem('qa-hien-cot-' + nut.dataset.hienCot, hien ? '1' : '0'); } catch (e2) { /* bỏ qua */ }
    });
})();
