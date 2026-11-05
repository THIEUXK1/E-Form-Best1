// Hàng đợi cấp trên (/QLCongViec/DanhGia/DuyetPhieu): chỉ liệt kê, việc duyệt/từ chối làm ở trang Chi tiết sau khi đọc phiếu.
(function () {
    'use strict';

    var NHAN = {
        cho_duyet: ['awaiting_manager', 'Chờ duyệt'],
        tu_choi: ['awaiting_self', 'Đã trả về chấm lại'],
        cho_ky: ['awaiting_manager', 'Chờ nhân viên ký'],
        da_ky: ['completed', 'Đã ký']
    };

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

    function ve(ds) {
        var dem = function (f) { return String(ds.filter(f).length); };
        $('dpdgChoDuyet').textContent = dem(function (x) { return x.buocKy === 'cho_duyet'; });
        $('dpdgTuChoi').textContent = dem(function (x) { return x.buocKy === 'tu_choi'; });
        $('dpdgChoKy').textContent = dem(function (x) { return x.buocKy === 'cho_ky'; });
        $('dpdgDaKy').textContent = dem(function (x) { return x.buocKy === 'da_ky'; });
        $('dpdgKhongDongY').textContent = dem(function (x) { return x.nvDongY === false; });

        var body = $('dpdgBang');
        body.textContent = '';
        if (!ds.length) {
            body.appendChild(el('tr', null, [el('td', { colspan: '7', class: 'tdg-empty', text: 'Chưa có phiếu nào vào luồng ký trong kỳ này.' })]));
            return;
        }
        ds.forEach(function (x) {
            var nhan = NHAN[x.buocKy] || ['not_started', 'Chốt trước khi có luồng ký'];
            var o = el('td', null, [el('span', { class: 'tdg-badge ' + nhan[0], text: nhan[1] })]);
            if (x.nvDongY === false) o.appendChild(el('span', { class: 'tdg-badge awaiting_self', text: 'Không đồng ý' }));
            var url = KyDanhGia.thamSo('/QLCongViec/DanhGia/ChiTiet/' + encodeURIComponent(x.maNv));
            body.appendChild(el('tr', null, [
                el('td', null, [el('b', { text: x.hoTen }), el('div', { class: 'tdg-card-desc m-0', text: x.maNv })]),
                el('td', { text: x.capBac || '—' }),
                el('td', { text: x.diemThucTe === null ? '—' : String(Math.round(x.diemThucTe * 100) / 100) }),
                el('td', { text: x.xepLoai || '—' }),
                el('td', { text: x.tenNguoiCham || '—' }),
                o,
                el('td', null, [el('a', { class: 'btn btn-sm btn-outline-primary', href: url, text: x.buocKy === 'cho_duyet' ? 'Xem & duyệt' : 'Xem phiếu' })])
            ]));
        });
    }

    function tai() {
        var tt = $('dpdgTrangThai');
        tt.hidden = false;
        tt.classList.remove('tdg-error');
        tt.textContent = 'Đang tải dữ liệu...';
        fetch(KyDanhGia.thamSo('/QLCongViec/DanhGia/KyXacNhanApi/DanhSach'), { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được danh sách.');
                ve(kq.duLieu);
                tt.hidden = true;
                $('dpdgKhung').hidden = false;
                $('dpdgTomTat').hidden = false;
            })
            .catch(function (e) {
                tt.textContent = e.message;
                tt.classList.add('tdg-error');
            });
    }

    document.addEventListener('DOMContentLoaded', function () { KyDanhGia.khoiTao('dpdgKy', tai); });
})();
