// Bộ lọc "Vị trí địa lý" cho trang QL Kiểm kê > Thiết bị.
// Gồm 2 lối vào: dropdown nhiều lựa chọn (#menuViTriDiaLy) trong Bộ lọc nâng cao,
// và nút gạt nhanh "Thiếu vị trí" (#swThieuViTriDiaLy) ở hàng Lọc nhanh.
// Dùng chung khuôn .dropdown-combo-menu có sẵn của trang nên chỉ cần đổ checkbox vào rồi lọc mảng thiết bị.
window.LocViTriDiaLyKiemKe = (function () {
    const KHOA_LUU = 'kk_filterViTriDiaLy';
    const KHOA_THIEU = 'kk_locThieuViTriDiaLy';
    const CHUA_XAC_DINH = '__CHUA_XAC_DINH__';

    let danhMuc = [];

    function layDaLuu() {
        try { return JSON.parse(localStorage.getItem(KHOA_LUU)) || []; } catch (e) { return []; }
    }

    function dangLocThieu() {
        return $('#swThieuViTriDiaLy').is(':checked');
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

    const api = {
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
            localStorage.setItem(KHOA_LUU, JSON.stringify(api.layDangChon()));
            localStorage.setItem(KHOA_THIEU, dangLocThieu() ? 'true' : 'false');
        },

        xoaLuaChon: function () {
            localStorage.removeItem(KHOA_LUU);
        },

        // Lọc mảng thiết bị: nút gạt "Thiếu vị trí" thắng tất cả, sau đó tới các địa điểm đang tích
        loc: function (danhSach) {
            if (dangLocThieu()) {
                danhSach = danhSach.filter(function (x) { return !x.tenViTriDiaLy || x.tenViTriDiaLy.trim() === ''; });
            }
            const chon = api.layDangChon();
            if (!chon.length) return danhSach;
            return danhSach.filter(function (x) {
                const ten = (x.tenViTriDiaLy || '').trim();
                if (ten === '') return chon.includes(CHUA_XAC_DINH);
                return chon.includes(ten);
            });
        }
    };

    $(function () {
        $('#swThieuViTriDiaLy').prop('checked', localStorage.getItem(KHOA_THIEU) === 'true');
        $(document).on('change', '#swThieuViTriDiaLy', function () {
            if (typeof applyFilters === 'function') applyFilters();
        });
    });

    return api;
})();
