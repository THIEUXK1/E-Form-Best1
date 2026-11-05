// Khối "Số liệu tham khảo" cạnh Part 1, dùng ở Tự đánh giá, Đánh giá nhân viên, Chi tiết nhân viên.
// SoLieuKpi.ve(idHop, url): url đã kèm kỳ; đổi khoảng ngày thì tải lại ngay trong khối.
window.SoLieuKpi = (function () {
    'use strict';

    function el(tag, lop, chu, con) {
        var e = document.createElement(tag);
        if (lop) e.className = lop;
        if (chu !== null && chu !== undefined) e.textContent = chu;
        (con || []).forEach(function (c) { if (c) e.appendChild(c); });
        return e;
    }

    function so(v) { return String(v === null || v === undefined ? '—' : v).replace('.', ','); }
    function o(nhan, giaTri, phu) {
        return el('div', 'sl-o', null, [el('span', 'sl-nhan', nhan), el('b', null, giaTri), phu ? el('small', null, phu) : null]);
    }

    function veSoLieu(hop, d) {
        var ds = hop.querySelector('.sl-luoi');
        ds.textContent = '';
        var don = d.don, hl = d.haiLong, dt = d.daoTao, ht = d.heThong;
        if (don.laNguoiHoTro) {
            ds.appendChild(o('Đơn IT được giao', String(don.duocGiao), 'Hoàn tất ' + don.hoanTat + ' • chưa xong ' + don.chuaXong));
            ds.appendChild(o('Thời gian xử lý', don.gioTrungVi === null ? '—' : so(don.gioTrungVi) + ' giờ',
                don.tyLeTrong24Gio === null ? 'Chưa có đơn đủ mốc thời gian' : 'Trung vị từ lúc duyệt • ' + so(don.tyLeTrong24Gio) + '% trong 24 giờ'));
            ds.appendChild(o('Hài lòng', hl.diemTrungBinh === null ? '—' : so(hl.diemTrungBinh) + ' / 5',
                hl.soDanhGia + ' lượt người chấm • bỏ ' + hl.soTuDong + ' lượt tự động'));
        } else {
            ds.appendChild(o('Đơn IT', '—', 'Không có trong danh sách người hỗ trợ IT'));
        }
        ds.appendChild(o('Đào tạo', dt.soBuoiGiangDay + ' giảng • ' + dt.soBuoiThamGia + ' dự', 'Buổi đã duyệt trong khoảng này'));
        ds.appendChild(o('Sự cố hệ thống', String(ht.soSuCo),
            'Toàn hệ thống • đang mở ' + ht.dangMo + (ht.gioKhacPhucTrungBinh === null ? '' : ' • khắc phục TB ' + so(ht.gioKhacPhucTrungBinh) + ' giờ')));
        var thap = (ht.thapNhat || []).map(function (s) { return s.ten + ' ' + so(s.uptime) + '%'; }).join(', ');
        ds.appendChild(o('Uptime máy chủ', ht.uptime === null ? '—' : so(ht.uptime) + '%',
            ht.soServer + ' máy' + (ht.doTuNgay ? ', đo từ ' + new Date(ht.doTuNgay).toLocaleDateString('vi-VN') : '') + (thap ? ' • thấp nhất: ' + thap : '')));
    }

    function taiLai(hop) {
        var loi = hop.querySelector('.sl-loi');
        loi.textContent = '';
        var tu = hop.querySelector('[data-sl="tu"]').value, den = hop.querySelector('[data-sl="den"]').value;
        var url = hop.getAttribute('data-url');
        if (tu && den) url += (url.indexOf('?') >= 0 ? '&' : '?') + 'tuNgay=' + tu + '&denNgay=' + den;
        fetch(url, { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được số liệu.');
                hop.querySelector('[data-sl="tu"]').value = kq.duLieu.tuNgay;
                hop.querySelector('[data-sl="den"]').value = kq.duLieu.denNgay;
                veSoLieu(hop, kq.duLieu);
            })
            .catch(function (e) { loi.textContent = e.message; });
    }

    function nhapNgay(ten) {
        var i = document.createElement('input');
        i.type = 'date';
        i.className = 'form-control form-control-sm';
        i.setAttribute('data-sl', ten);
        i.setAttribute('aria-label', ten === 'tu' ? 'Từ ngày' : 'Đến ngày');
        return i;
    }

    function ve(idHop, url) {
        var hop = document.getElementById(idHop);
        hop.textContent = '';
        hop.className = 'sl-khung';
        hop.setAttribute('data-url', url);
        var nut = el('button', 'btn btn-sm btn-outline-secondary', 'Xem');
        nut.type = 'button';
        nut.setAttribute('data-sl', 'xem');
        hop.appendChild(el('div', 'sl-dau', null, [
            el('b', null, 'Số liệu tham khảo'),
            el('small', null, 'Lấy từ hệ thống, không tự tính vào điểm'),
            el('span', 'sl-ngay', null, [nhapNgay('tu'), nhapNgay('den'), nut])
        ]));
        hop.appendChild(el('div', 'sl-luoi'));
        hop.appendChild(el('div', 'sl-loi'));
        taiLai(hop);
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('[data-sl="xem"]');
        if (nut) taiLai(nut.closest('.sl-khung'));
    });

    return { ve: ve };
})();
