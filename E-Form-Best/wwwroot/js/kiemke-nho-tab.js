// Trang Quản lý Thiết bị: nhớ tab đang mở (Tổng quan / Thiết bị / Thùng rác) để F5 không bị đẩy về tab mặc định.
(function () {
    const KHOA_LUU = 'kk_tabThietBi';
    const CAC_TAB = ['thongke-tab', 'thietbi-tab', 'daxoa-tab'];

    // localStorage có thể bị chặn (chế độ riêng tư) — khi đó chỉ mất tính năng nhớ tab, trang vẫn chạy
    function doc() { try { return localStorage.getItem(KHOA_LUU); } catch (e) { return null; } }
    function ghi(v) { try { localStorage.setItem(KHOA_LUU, v); } catch (e) { } }

    $(document).on('shown.bs.tab', '#myTab [data-bs-toggle="tab"]', function () {
        if (CAC_TAB.includes(this.id)) ghi(this.id);
    });

    $(function () {
        // Vào từ menu "Kho IT" / "Tài sản báo phế" thì luôn mở tab Thiết bị để thấy ngay danh sách
        if (new URLSearchParams(location.search).get('xem')) return;
        const id = doc();
        if (!id || id === 'thietbi-tab' || !CAC_TAB.includes(id)) return;
        // Bấm thật vào nút để chạy cả changeTab() (ẩn/hiện bộ lọc, nút Thêm mới) lẫn chuyển tab của Bootstrap
        const nut = document.getElementById(id);
        if (nut) nut.click();
    });
})();
