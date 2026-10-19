// Trang Nhân viên chưa hoàn thành đánh giá (/QLCongViec/DanhGia/ChuaHoanThanh).
// Dùng lại API danh sách của "Quản lý nhân viên"; lọc và chia cột theo bước còn thiếu ở client.
(function () {
    'use strict';

    var KHONG_RO = '(Chưa rõ)';
    // Chỉ cột có việc của quản lý mới có nút chấm ngay; "Chờ tự đánh giá" là việc của nhân viên
    var COT = [
        { ma: 'awaiting_manager', ul: 'chdgDsChoQl', dem: 'chdgDemChoQl', badge: 'chdgBadgeChoQl', nut: true },
        { ma: 'awaiting_self', ul: 'chdgDsChoTu', dem: 'chdgDemChoTu', badge: 'chdgBadgeChoTu' },
        { ma: 'not_started', ul: 'chdgDsChuaDg', dem: 'chdgDemChuaDg', badge: 'chdgBadgeChuaDg', nut: true }
    ];

    var st = { ds: [] };

    function $(id) { return document.getElementById(id); }

    function el(tag, attrs, con) {
        var e = document.createElement(tag);
        if (attrs) Object.keys(attrs).forEach(function (k) {
            if (k === 'text') e.textContent = attrs[k];
            else if (k === 'class') e.className = attrs[k];
            else if (attrs[k] !== null && attrs[k] !== undefined) e.setAttribute(k, attrs[k]);
        });
        (con || []).forEach(function (c) { if (c) e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
        return e;
    }

    function phongBan(nv) { return (nv.phongBan || '').trim() || KHONG_RO; }
    function nhom(nv) { return (nv.tenNhomCongViec || '').trim() || KHONG_RO; }

    function ngay(v) {
        if (!v) return '';
        var d = new Date(v);
        return isNaN(d) ? '' : ('0' + d.getDate()).slice(-2) + '/' + ('0' + (d.getMonth() + 1)).slice(-2) + '/' + d.getFullYear();
    }

    function baoLoi(msg) {
        $('chdgLoiText').textContent = msg;
        $('chdgLoi').hidden = !msg;
    }

    function napLuaChon(id, giaTri) {
        var sel = $(id), cu = sel.value;
        while (sel.options.length > 1) sel.remove(1);
        giaTri.forEach(function (v) { sel.appendChild(el('option', { value: v, text: v })); });
        if (giaTri.indexOf(cu) >= 0) sel.value = cu;
    }

    function duyNhat(fn) {
        var s = {};
        st.ds.forEach(function (nv) { s[fn(nv)] = true; });
        return Object.keys(s).sort(function (a, b) { return a.localeCompare(b, 'vi'); });
    }

    function ve() {
        var pb = $('chdgPhongBan').value, nh = $('chdgNhom').value, q = $('chdgTim').value.trim().toLowerCase();
        var ds = st.ds.filter(function (nv) {
            return (!pb || phongBan(nv) === pb) && (!nh || nhom(nv) === nh);
        });
        var tong = 0;
        COT.forEach(function (c) {
            var rows = ds.filter(function (nv) { return nv.trangThai === c.ma; });
            tong += rows.length;
            $(c.dem).textContent = rows.length;
            $(c.badge).textContent = rows.length;
            // Tìm kiếm chỉ thu hẹp danh sách, không đổi số đếm theo trạng thái
            if (q) rows = rows.filter(function (nv) { return (nv.maNv + ' ' + nv.hoTen).toLowerCase().indexOf(q) >= 0; });
            rows.sort(function (a, b) { return phongBan(a).localeCompare(phongBan(b), 'vi') || a.maNv.localeCompare(b.maNv); });

            var ul = $(c.ul);
            ul.textContent = '';
            if (!rows.length) { ul.appendChild(el('li', { class: 'trong', text: 'Không có nhân viên' })); return; }
            rows.forEach(function (nv) {
                var phu = [nv.maNv, nv.phongBan, nv.capBac, nv.ngayCapNhat ? 'cập nhật ' + ngay(nv.ngayCapNhat) : ''].filter(Boolean).join(' · ');
                ul.appendChild(el('li', null, [
                    el('div', { class: 'ten' }, [
                        el('a', { href: '/QLCongViec/DanhGia/ChiTiet/' + encodeURIComponent(nv.maNv), text: nv.hoTen }),
                        el('div', { class: 'tkdg-phu', text: phu, title: phu })
                    ]),
                    c.nut ? el('a', { class: 'tkdg-btn-dg', href: '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-pencil-square-o' }), ' Đánh giá']) : null
                ]));
            });
        });
        $('chdgTong').textContent = tong;
        $('chdgTyLe').textContent = tong + '/' + ds.length + ' nhân viên';
    }

    function taiDuLieu() {
        $('chdgDangTai').hidden = false;
        baoLoi('');
        fetch('/QLCongViec/DanhGia/QuanLy/DanhSach', { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được dữ liệu.');
                st.ds = kq.duLieu || [];
                napLuaChon('chdgPhongBan', duyNhat(phongBan));
                napLuaChon('chdgNhom', duyNhat(nhom));
                ve();
            })
            .catch(function (err) { baoLoi(err.message); })
            .then(function () { $('chdgDangTai').hidden = true; });
    }

    document.addEventListener('change', function (e) {
        if (e.target.id === 'chdgPhongBan' || e.target.id === 'chdgNhom') ve();
    });
    document.addEventListener('input', function (e) {
        if (e.target.id === 'chdgTim') ve();
    });
    document.addEventListener('click', function (e) {
        if (e.target.closest('#chdgLamMoi')) taiDuLieu();
    });

    document.addEventListener('DOMContentLoaded', taiDuLieu);
})();
