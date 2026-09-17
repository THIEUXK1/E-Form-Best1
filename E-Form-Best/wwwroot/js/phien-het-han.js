// Chốt xử lý phiên đăng nhập hết hạn cho mọi lời gọi fetch của layout.
//
// Bối cảnh: chuông thông báo poll 30 giây/lần. Khi cookie bị OnValidatePrincipal loại
// (đổi mật khẩu, security_stamp lệch, hoặc mở trang mà chưa đăng nhập), endpoint trả 401
// với body rỗng. Code cũ gọi thẳng res.json() nên ném lỗi và bị try/catch nuốt im lặng:
// người dùng không biết mình đã bị đăng xuất, còn tab vẫn bắn 401 vào log WAF mãi mãi.
//
// Cách xử lý: bọc window.fetch một lần ở head. Gặp 401 đầu tiên thì báo cho người dùng và
// khoá lại — các fetch sau đó không ra khỏi trình duyệt nữa, trả thẳng Response 401 giả lập
// để code gọi cũ không vỡ. Hết spam request, và người dùng thấy rõ phải đăng nhập lại.
(function () {
    'use strict';

    if (window.__phienHetHanDaGan) return;
    window.__phienHetHanDaGan = true;

    var DUONG_DAN_DANG_NHAP = '/DonXetDuyet/DangNhap';
    var daHetHan = false;

    // Response giả lập cho các lời gọi sau khi đã khoá: giữ nguyên status 401 để nhánh
    // kiểm res.ok của code mới vẫn đúng, kèm JSON hợp lệ để res.json() không ném.
    function taoResponseGiaLap() {
        return new Response(
            JSON.stringify({ success: false, message: 'Phiên đăng nhập đã hết hạn.' }),
            { status: 401, headers: { 'Content-Type': 'application/json' } }
        );
    }

    function hienBanner() {
        if (document.getElementById('bannerPhienHetHan')) return;

        var banner = document.createElement('div');
        banner.id = 'bannerPhienHetHan';
        banner.setAttribute('role', 'alert');
        banner.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:2147483647;' +
            'background:#dc2626;color:#fff;padding:12px 20px;display:flex;align-items:center;' +
            'justify-content:center;gap:16px;font-weight:700;font-size:0.95rem;' +
            'box-shadow:0 2px 12px rgba(0,0,0,.25);font-family:inherit;';

        var chu = document.createElement('span');
        chu.textContent = 'Phiên đăng nhập đã hết hạn. Dữ liệu trên trang không còn được cập nhật.';

        var nut = document.createElement('button');
        nut.type = 'button';
        nut.textContent = 'Đăng nhập lại';
        nut.style.cssText = 'background:#fff;color:#dc2626;border:none;border-radius:8px;' +
            'padding:6px 16px;font-weight:800;cursor:pointer;';
        // Điều hướng thật là hợp lệ ở đây: đây đúng là luồng "chưa đăng nhập -> trang đăng nhập".
        nut.addEventListener('click', function () {
            window.location.href = DUONG_DAN_DANG_NHAP + '?returnUrl=' +
                encodeURIComponent(window.location.pathname + window.location.search);
        });

        banner.appendChild(chu);
        banner.appendChild(nut);
        (document.body || document.documentElement).appendChild(banner);
    }

    function khoaPhien() {
        if (daHetHan) return;
        daHetHan = true;

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', hienBanner);
        } else {
            hienBanner();
        }
        document.dispatchEvent(new CustomEvent('phien:hethan'));
    }

    // Chỉ can thiệp request cùng origin; fetch ra CDN/dịch vụ ngoài giữ nguyên hành vi.
    function laCungOrigin(input) {
        try {
            var url = typeof input === 'string' ? input : (input && input.url) || '';
            return new URL(url, window.location.href).origin === window.location.origin;
        } catch (e) {
            return false;
        }
    }

    var fetchGoc = window.fetch.bind(window);

    window.fetch = function (input, init) {
        if (daHetHan && laCungOrigin(input)) {
            return Promise.resolve(taoResponseGiaLap());
        }

        return fetchGoc(input, init).then(function (res) {
            if (res.status === 401 && laCungOrigin(input)) khoaPhien();
            return res;
        });
    };
})();
