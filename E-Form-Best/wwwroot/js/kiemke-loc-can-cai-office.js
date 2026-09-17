// Bộ lọc "Cần cài Office" cho trang QL Kiểm kê > Thiết bị.
// Ba trạng thái đúng theo cột can_cai_office: 1 = cần, 0 = không cần, NULL = chưa trả lời phiếu.
// Danh sách cố định nên vẽ ngay khi trang sẵn sàng, không phải gọi API.
window.LocCanCaiOfficeKiemKe = (function () {
    const KHOA_LUU = 'kk_filterCanCaiOffice';

    const LUA_CHON = [
        { ma: 'can', nhan: '✅ Cần cài Office' },
        { ma: 'khong', nhan: '🚫 Không cần Office' },
        { ma: 'chua', nhan: '⚠ Chưa trả lời' }
    ];

    function layDaLuu() {
        try { return JSON.parse(localStorage.getItem(KHOA_LUU)) || []; } catch (e) { return []; }
    }

    function ve() {
        const daLuu = layDaLuu();
        const $items = $('#menuCanCaiOffice .dropdown-combo-items').empty();
        LUA_CHON.forEach(function (x) {
            $items.append(
                '<label class="dropdown-combo-item">' +
                '<input type="checkbox" class="chk-cancaioffice" value="' + x.ma + '"' +
                (daLuu.includes(x.ma) ? ' checked' : '') + '>' +
                '<span>' + x.nhan + '</span>' +
                '</label>'
            );
        });
        if (typeof updateDropdownButtonLabel === 'function') updateDropdownButtonLabel($('#menuCanCaiOffice'));
    }

    const api = {
        layDangChon: function () {
            return $('.chk-cancaioffice:checked').map(function () { return $(this).val(); }).get();
        },

        luu: function () {
            localStorage.setItem(KHOA_LUU, JSON.stringify(api.layDangChon()));
        },

        xoaLuaChon: function () {
            localStorage.removeItem(KHOA_LUU);
        },

        loc: function (danhSach) {
            const chon = api.layDangChon();
            if (!chon.length) return danhSach;
            return danhSach.filter(function (x) {
                if (x.canCaiOffice === true) return chon.includes('can');
                if (x.canCaiOffice === false) return chon.includes('khong');
                return chon.includes('chua');   // null / undefined = chưa trả lời
            });
        }
    };

    $(function () { ve(); });

    return api;
})();
