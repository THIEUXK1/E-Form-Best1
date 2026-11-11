// Trang chi tiết đơn (IT/HR/SHD/Công việc): mở từ dropdown Bình luận ở header thì link có đuôi #binh-luan
// → cuộn thẳng xuống khung bình luận. Không dùng anchor gốc của trình duyệt vì bình luận tải AJAX,
// ảnh/khối phía trên còn đổi chiều cao sau khi trang mở nên phải cuộn lại vài lần cho đến khi ổn định.
(function () {
    const HASH = '#binh-luan';

    function khungBinhLuan() {
        const ds = document.getElementById('commentList');
        if (!ds) return null;
        return document.getElementById('commentContainer') || ds.parentElement;
    }

    function cuon(muot) {
        const khung = khungBinhLuan();
        if (!khung) return;
        // Chừa chỗ cho header cố định phía trên.
        khung.style.scrollMarginTop = '80px';
        khung.scrollIntoView({ behavior: muot ? 'smooth' : 'auto', block: 'start' });
    }

    function xuLy() {
        if (location.hash !== HASH) return;
        cuon(false);
        const ds = document.getElementById('commentList');
        if (!ds) return;
        // Bình luận đổ vào sau → cuộn lại; dừng theo dõi sau 3 giây hoặc khi người dùng tự cuộn/bấm.
        const theoDoi = new MutationObserver(() => cuon(false));
        theoDoi.observe(ds, { childList: true });
        const ngung = () => {
            theoDoi.disconnect();
            ['wheel', 'touchstart', 'keydown', 'mousedown'].forEach(ev => window.removeEventListener(ev, ngung));
        };
        ['wheel', 'touchstart', 'keydown', 'mousedown'].forEach(ev => window.addEventListener(ev, ngung, { passive: true }));
        window.addEventListener('load', () => cuon(false), { once: true });
        setTimeout(ngung, 3000);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', xuLy);
    else xuLy();
    // Đang ở đúng đơn đó mà bấm lại trong dropdown: chỉ đổi hash, không tải lại trang.
    window.addEventListener('hashchange', () => { if (location.hash === HASH) cuon(true); });
})();
