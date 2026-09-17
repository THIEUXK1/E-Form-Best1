// Bộ lọc "Vị trí địa lý" cho trang QL Kiểm kê > Thiết bị.
// Dùng chung khuôn dropdown nhiều lựa chọn (.dropdown-combo-menu) đã có sẵn của trang,
// nên chỉ cần đổ checkbox vào #menuViTriDiaLy rồi lọc trên mảng thiết bị đang hiển thị.
window.LocViTriDiaLyKiemKe = (function () {
    const KHOA_LUU = 'kk_filterViTriDiaLy';
    const CHUA_XAC_DINH = '__CHUA_XAC_DINH__';

    let danhMuc = [];

    function layDaLuu() {
        try { return JSON.parse(localStorage.getItem(KHOA_LUU)) || []; } catch (e) { return []; }
    }

    // Tên địa điểm là dữ liệu người dùng nhập nên phải escape trước khi ghép vào HTML
    function thoat(giaTri) {
        return String(giaTri == null ? '' : giaTri)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function ve() {
        const daLuu = layDaLuu();
        const $items = $('#menuViTriDiaLy .dropdown-combo-items').empty();

        // Luôn có tuỳ chọn "Chưa xác định" để soi ra máy còn thiếu vị trí - đây mới là nhóm cần đôn đốc
        $items.append(
            '<label class="dropdown-combo-item">' +
            '<input type="checkbox" class="chk-vitridialy" value="' + CHUA_XAC_DINH + '"' +
            (daLuu.includes(CHUA_XAC_DINH) ? ' checked' : '') + '>' +
            '<span class="text-danger fw-bold">⚠ Chưa xác định vị trí</span>' +
            '</label><div class="dropdown-divider"></div>'
        );

        danhMuc.forEach(function (v) {
            const ten = thoat(v.tenViTriDiaLy);
            $items.append(
                '<label class="dropdown-combo-item">' +
                '<input type="checkbox" class="chk-vitridialy" value="' + ten + '"' +
                (daLuu.includes(v.tenViTriDiaLy) ? ' checked' : '') + '>' +
                '<span>' + ten + '</span>' +
                '</label>'
            );
        });

        if (typeof updateDropdownButtonLabel === 'function') updateDropdownButtonLabel($('#menuViTriDiaLy'));
    }

    return {
        taiDanhSach: function () {
            $.get('/QLKiemKe/GetKkViTriDiaLys', function (res) {
                if (!res || !res.success) return;
                danhMuc = res.data || [];
                ve();
                // Danh mục về sau khi bảng đã vẽ xong, nên có lựa chọn cũ thì phải lọc lại một lần
                if (layDaLuu().length && typeof applyFilters === 'function') applyFilters();
            });
        },

        layDangChon: function () {
            return $('.chk-vitridialy:checked').map(function () { return $(this).val(); }).get();
        },

        luu: function () {
            localStorage.setItem(KHOA_LUU, JSON.stringify(this.layDangChon()));
        },

        xoaLuaChon: function () {
            localStorage.removeItem(KHOA_LUU);
        },

        // Lọc mảng thiết bị theo các địa điểm đang tích; không tích gì thì giữ nguyên danh sách
        loc: function (danhSach) {
            const chon = this.layDangChon();
            if (!chon.length) return danhSach;
            return danhSach.filter(function (x) {
                const ten = (x.tenViTriDiaLy || '').trim();
                if (ten === '') return chon.includes(CHUA_XAC_DINH);
                return chon.includes(ten);
            });
        }
    };
})();
