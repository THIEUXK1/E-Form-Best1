// Công tắc "Ưu tiên đơn của tôi" trên /FormIT/HoanTatDon: đưa đơn có tên người đang đăng nhập
// trong Người hỗ trợ lên đầu. Lọc và phân trang vẫn nằm ở view, file này chỉ lo thứ tự.
window.UuTienDonCuaToi = (function () {
    var KHOA_LUU = 'uuTienDonCuaToiIT';

    function congTac() { return document.getElementById('chkUuTienDonCuaToi'); }

    // Mặc định bật: chỉ tắt khi người dùng đã chủ động tắt trên trình duyệt này
    function napTrangThai() {
        var o = congTac();
        if (!o) return;
        try { o.checked = localStorage.getItem(KHOA_LUU) !== '0'; } catch (e) { /* giữ mặc định bật */ }
    }

    function hienCongTac(duLieu) {
        var khung = document.getElementById('khungUuTienDonCuaToi');
        // Người không hỗ trợ đơn nào thì công tắc không có tác dụng, ẩn cho đỡ rối
        if (khung) khung.style.display = duLieu.some(function (x) { return x.coTenToi; }) ? '' : 'none';
    }

    function sapXep(ds) {
        var o = congTac();
        if (!o || !o.checked) return ds;
        // sort ổn định: trong mỗi nhóm vẫn giữ thứ tự đơn mới nhất trước từ server
        return ds.slice().sort(function (a, b) { return (b.coTenToi ? 1 : 0) - (a.coTenToi ? 1 : 0); });
    }

    document.addEventListener('change', function (e) {
        if (e.target.id !== 'chkUuTienDonCuaToi') return;
        try { localStorage.setItem(KHOA_LUU, e.target.checked ? '1' : '0'); } catch (err) { /* bỏ qua */ }
        handleSearch();
    });

    napTrangThai();

    return { hienCongTac: hienCongTac, sapXep: sapXep };
})();
