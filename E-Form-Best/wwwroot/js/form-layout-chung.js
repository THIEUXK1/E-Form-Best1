// Phần JS giống hệt nhau ở layout của 4 Area (ITForm, HRform, SHDForm, QLCongViec):
// menu mobile, đổi ngôn ngữ qua Google Translate, zoom trang, đóng dropdown khi bấm ra ngoài,
// và ghi nhớ vị trí cuộn sidebar.
//
// Phần chuông thông báo KHÔNG nằm ở đây: mỗi Area gọi API khác nhau, lưu trạng thái đã đọc
// khác nhau và vẽ HTML khác nhau — vẫn để riêng trong từng layout.
//
// Các hàm changeLanguageGTranslate / changeZoom được gọi từ onclick trong HTML nên phải ở
// phạm vi toàn cục — đừng bọc file này vào IIFE.
//
// Cấu hình theo Area đọc từ <body data-scroll-key="...">.

// ==========================================
// MENU MOBILE
// ==========================================
(function initMobileMenu() {
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');
    const sidebarEl = document.getElementById('sidebar');
    const backdropEl = document.getElementById('sidebar-backdrop');
    if (!mobileMenuBtn || !sidebarEl || !backdropEl) return;

    mobileMenuBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        sidebarEl.classList.toggle('open');
        backdropEl.classList.toggle('show');
    });

    backdropEl.addEventListener('click', () => {
        sidebarEl.classList.remove('open');
        backdropEl.classList.remove('show');
    });
})();

// ==========================================
// COOKIE HELPER
// ==========================================
function setCookieForm(cname, cvalue, exdays) {
    const d = new Date();
    d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
    document.cookie = cname + "=" + cvalue + ";expires=" + d.toUTCString() + ";path=/";
}

function getCookieForm(cname) {
    const name = cname + "=";
    const ca = decodeURIComponent(document.cookie).split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') { c = c.substring(1); }
        if (c.indexOf(name) === 0) { return c.substring(name.length, c.length); }
    }
    return "";
}

// ==========================================
// ĐA NGÔN NGỮ BẰNG GOOGLE TRANSLATE (NUCLEAR OPTION CHỐNG KẸT)
// ==========================================

// Quét sạch mọi ngóc ngách cookie/storage vì widget hay giữ lại giá trị cũ làm kẹt ngôn ngữ
function clearGoogTransCookie() {
    let domain = window.location.hostname;
    let paths = ['/', window.location.pathname];
    let domainParts = domain.split('.');
    let domainsToClear = [domain, '.' + domain, ''];

    // Nếu có sub-domain (vd portal.bestpacific.com) thì quét thêm cấp root (.bestpacific.com)
    if (domainParts.length >= 2) {
        domainsToClear.push('.' + domainParts.slice(-2).join('.'));
        domainsToClear.push(domainParts.slice(-2).join('.'));
    }

    paths.forEach(p => {
        domainsToClear.forEach(d => {
            let dStr = d ? `; domain=${d}` : '';
            document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=${p}${dStr};`;
        });
    });

    window.localStorage.removeItem('googtrans');
    window.sessionStorage.removeItem('googtrans');
}

// Gắn cookie ở cả 2 cấp domain để chắc ăn
function setGoogTransCookie(lang) {
    let domain = window.location.hostname;
    let domainParts = domain.split('.');
    let rootDomain = domainParts.length >= 2 ? '.' + domainParts.slice(-2).join('.') : domain;

    document.cookie = `googtrans=/vi/${lang}; path=/; domain=${rootDomain};`;
    document.cookie = `googtrans=/vi/${lang}; path=/;`;
}

function initLanguageGTranslate() {
    let currentLang = localStorage.getItem('siteLangGlobal') || 'vi';
    if (!localStorage.getItem('siteLangGlobal')) {
        localStorage.setItem('siteLangGlobal', 'vi');
    }

    const labels = { 'vi': 'Tiếng Việt', 'en': 'English', 'zh-CN': '中文' };
    const currentLangText = document.getElementById('currentLangText');
    if (currentLangText) currentLangText.innerText = labels[currentLang] || 'Tiếng Việt';

    document.querySelectorAll('#langDropdown .lang-item i').forEach(i => i.style.display = 'none');
    const activeCheck = document.querySelector(`#lang-${currentLang} i`);
    if (activeCheck) activeCheck.style.display = 'inline-block';

    // Trình duyệt có thể lén giữ cookie sai lệch — đối chiếu rồi ép về đúng
    let match = document.cookie.match(/(^| )googtrans=([^;]+)/);
    let currentCookie = match ? match[2] : null;
    let expectedCookie = currentLang === 'vi' ? null : `/vi/${currentLang}`;

    if (currentCookie !== expectedCookie) {
        clearGoogTransCookie();
        if (currentLang !== 'vi') {
            setGoogTransCookie(currentLang);
        }
    }
}

function changeLanguageGTranslate(lang, label) {
    localStorage.setItem('siteLangGlobal', lang);

    // BƯỚC 1: phá DOM của widget để nó không kịp ghi lại cookie cũ lúc unload
    const gtEl = document.getElementById('google_translate_element');
    if (gtEl) gtEl.innerHTML = '';
    const iframes = document.querySelectorAll('iframe.goog-te-menu-frame, iframe.goog-te-banner-frame');
    iframes.forEach(f => f.remove());

    // BƯỚC 2: dọn rác cookie
    clearGoogTransCookie();

    // BƯỚC 3: gán cookie mới rồi điều hướng bằng href (tránh dính POST resubmit như reload)
    setTimeout(() => {
        if (lang !== 'vi') {
            setGoogTransCookie(lang);
        }
        window.location.href = window.location.pathname + window.location.search;
    }, 50);
}

// ==========================================
// ZOOM KÍCH THƯỚC WEB
// ==========================================
function initZoom() {
    applyZoom(getCookieForm('siteZoomGlobal') || '100');
}

function changeZoom(percent) {
    setCookieForm('siteZoomGlobal', percent, 365);
    applyZoom(percent);
    const zoomDropEl = document.getElementById('zoomDropdown');
    if (zoomDropEl) zoomDropEl.style.display = 'none';
}

function applyZoom(percent) {
    document.body.style.zoom = percent + '%';

    const currentZoomText = document.getElementById('currentZoomText');
    if (currentZoomText) currentZoomText.innerText = percent + '%';

    const zoomDropEl = document.getElementById('zoomDropdown');
    if (!zoomDropEl) return;

    zoomDropEl.querySelectorAll('.lang-item').forEach(item => {
        const text = item.querySelector('span').innerText;
        const check = item.querySelector('i');
        if (text === percent + '%') {
            if (check) check.style.display = 'inline-block';
            item.classList.add('active');
        } else {
            if (check) check.style.display = 'none';
            item.classList.remove('active');
        }
    });
}

// ==========================================
// DROPDOWN NGÔN NGỮ / ZOOM + ĐÓNG KHI BẤM RA NGOÀI
// ==========================================
(function initHeaderDropdowns() {
    const btnLang = document.getElementById('btnLanguage');
    const btnZoom = document.getElementById('btnZoom');

    if (btnLang) {
        btnLang.addEventListener('click', (e) => {
            e.stopPropagation();
            const langDrop = document.getElementById('langDropdown');
            const zoomDrop = document.getElementById('zoomDropdown');
            if (zoomDrop) zoomDrop.style.display = 'none';
            if (!langDrop) return;
            const isHidden = langDrop.style.display === 'none' || langDrop.style.display === '';
            langDrop.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (btnZoom) {
        btnZoom.addEventListener('click', (e) => {
            e.stopPropagation();
            const langDrop = document.getElementById('langDropdown');
            const zoomDrop = document.getElementById('zoomDropdown');
            if (langDrop) langDrop.style.display = 'none';
            if (!zoomDrop) return;
            const isHidden = zoomDrop.style.display === 'none' || zoomDrop.style.display === '';
            zoomDrop.style.display = isHidden ? 'block' : 'none';
        });
    }

    // Bấm ra ngoài thì đóng hết. Tra phần tử ngay trong handler để không phụ thuộc
    // biến khai ở phần thông báo riêng của từng Area.
    document.addEventListener('click', () => {
        ['langDropdown', 'zoomDropdown', 'notificationDropdown', 'userDropdown'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
        const userArrow = document.getElementById('arrowIcon');
        if (userArrow) userArrow.style.transform = 'rotate(0deg)';
    });
})();

// ==========================================
// NHỚ VỊ TRÍ CUỘN SIDEBAR
// ==========================================
document.addEventListener('DOMContentLoaded', () => {
    initLanguageGTranslate();
    initZoom();

    const sidebarNav = document.getElementById('main-sidebar-nav');
    if (!sidebarNav) return;

    // Mỗi Area một key riêng để chuyển qua lại giữa các hệ không bị nhảy vị trí của nhau
    const scrollKey = document.body.dataset.scrollKey || 'sidebarScrollPos';

    const saved = sessionStorage.getItem(scrollKey);
    if (saved) sidebarNav.scrollTop = parseInt(saved);

    sidebarNav.addEventListener('scroll', function () {
        sessionStorage.setItem(scrollKey, this.scrollTop);
    });
});
