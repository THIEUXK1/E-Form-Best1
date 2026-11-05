// Dashboard cá nhân /QLCongViec/Logo. Đơn công việc lấy từ API sẵn có của trang Đơn chờ để giữ một cách đếm;
// mỗi nguồn lỗi độc lập, nguồn này hỏng không làm mất phần còn lại.
(function () {
    'use strict';

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

    function layJson(url) {
        return fetch(url, { headers: { 'Accept': 'application/json' } }).then(function (res) {
            if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
            return res.json();
        });
    }

    function so(v) { return v === null || v === undefined ? '—' : String(Math.round(v * 100) / 100); }

    function coDau(v) { return v > 0 ? '+' + so(v) : so(v); }

    function ngayVn(s) {
        var p = String(s || '').split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : '';
    }

    var dsViec = { danhGia: null, don: null };

    function veViec() {
        if (dsViec.danhGia === null && dsViec.don === null) return;
        var ds = (dsViec.danhGia || []).concat(dsViec.don || []);
        $('tqcnSoViec').textContent = String(ds.length);
        var gap = ds.filter(function (x) { return x.gap; }).length;
        $('tqcnSoViecGhiChu').textContent = gap ? gap + ' việc cần xử lý ngay' : '';
        var o = $('tqcnDsViec');
        o.textContent = '';
        if (!ds.length) { o.appendChild(el('li', { class: 'tqcn-trong', text: 'Không có việc nào đang chờ bạn.' })); return; }
        ds.sort(function (a, b) { return (b.gap ? 1 : 0) - (a.gap ? 1 : 0); }).forEach(function (v) {
            o.appendChild(el('li', null, [
                el('a', { href: v.url, class: v.gap ? 'gap' : '' }, [
                    el('i', { class: 'fa ' + v.icon }),
                    el('span', null, [el('b', { text: v.tieuDe }), v.moTa ? el('small', { text: v.moTa }) : null])
                ])
            ]));
        });
    }

    function veDiem(d) {
        var k = d.diem;
        $('tqcnODiem').hidden = !d.coDanhGia;
        if (k) {
            $('tqcnDiem').textContent = so(k.diemThucTe) + ' / 100';
            $('tqcnDiemGhiChu').textContent = k.tenKy + (k.xepLoai ? ' · ' + k.xepLoai : '');
        } else {
            $('tqcnDiemGhiChu').textContent = 'Chưa có kỳ nào chốt điểm';
        }
    }

    function veGap(d) {
        var g = d.gap;
        $('tqcnOGap').hidden = !d.coDanhGia;
        if (!g) { $('tqcnGapGhiChu').textContent = 'Chưa có điểm năng lực'; return; }
        $('tqcnGap').textContent = coDau(g.tbGap);
        $('tqcnGap').className = g.tbGap < 0 ? 'am' : '';
        $('tqcnGapGhiChu').textContent = g.tenKy + (g.xepLoai ? ' · ' + g.xepLoai : '');
        $('tqcnKhungGap').hidden = false;
        $('tqcnGapKy').textContent = g.tenKy;
        var body = $('tqcnBangGap');
        body.textContent = '';
        g.dsNhom.forEach(function (n) {
            body.appendChild(el('tr', null, [el('td', { text: n.tenNhom }), el('td', { class: n.gap < 0 ? 'so am' : 'so', text: coDau(n.gap) })]));
        });
        $('tqcnTieuDeYeu').hidden = g.dsYeu.length === 0;
        var yeu = $('tqcnDsYeu');
        yeu.textContent = '';
        g.dsYeu.forEach(function (n) { yeu.appendChild(el('li', { text: n.ten + ' (' + coDau(n.gap) + ')' })); });
    }

    function veDaoTao(ds) {
        $('tqcnSoDaoTao').textContent = String(ds.length);
        $('tqcnDaoTaoGhiChu').textContent = ds.length ? 'Gần nhất ' + ngayVn(ds[0].ngay) : '';
        var o = $('tqcnDsDaoTao');
        o.textContent = '';
        if (!ds.length) { o.appendChild(el('li', { class: 'tqcn-trong', text: 'Chưa có buổi đào tạo nào sắp tới.' })); return; }
        ds.forEach(function (x) {
            o.appendChild(el('li', null, [
                el('a', { href: '/FormCongViec/DaoTao' }, [
                    el('i', { class: 'fa fa-graduation-cap' }),
                    el('span', null, [
                        el('b', { text: x.ten }),
                        el('small', { text: ngayVn(x.ngay) + ' · ' + (x.laGiangVien ? 'Bạn giảng' : 'Tham gia') + (x.trangThai === 'ChoDuyet' ? ' · Chờ duyệt' : '') })
                    ])
                ])
            ]));
        });
    }

    function veThem(d) {
        if (d.hoTen) $('tqcnChao').textContent = 'Xin chào, ' + d.hoTen;
        if (!d.coDanhGia) return;
        $('tqcnOLoTrinh').hidden = false;
        $('tqcnBac').textContent = d.capBac || '—';
        if (d.phuTrach) {
            $('tqcnPhuTrach').textContent = d.phuTrach.soChinh + ' việc chính, ' + d.phuTrach.soDuPhong + ' dự phòng'
                + (d.phuTrach.soCanhBao ? ', ' + d.phuTrach.soCanhBao + ' có cảnh báo' : '');
        }
    }

    function baoLoi(chu) {
        var o = $('tqcnLoi');
        o.hidden = false;
        o.textContent = o.textContent ? o.textContent + ' ' + chu : chu;
    }

    function taiTongQuan() {
        layJson('/QLCongViec/TongQuanApi')
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được tổng quan.');
                var d = kq.duLieu;
                veThem(d);
                veDiem(d);
                veGap(d);
                veDaoTao(d.dsDaoTao);
                dsViec.danhGia = d.dsViec;
                veViec();
            })
            .catch(function (e) {
                dsViec.danhGia = [];
                veViec();
                $('tqcnSoDaoTao').textContent = '—';
                $('tqcnDsDaoTao').textContent = '';
                baoLoi(e.message);
            });
    }

    // Cùng cách phân loại với trang Đơn chờ: đơn đã huỷ và đã đánh giá không còn việc gì để làm
    function taiDon() {
        layJson('/FormCongViec/GetDonChoData')
            .then(function (ds) {
                var conHieuLuc = ds.filter(function (x) { return (x.tenForm || '').indexOf('[ĐÃ HỦY]') < 0; });
                var dangChay = conHieuLuc.filter(function (x) { return x.idAdmin === null; }).length;
                var choDanhGia = conHieuLuc.filter(function (x) { return x.idAdmin !== null && !x.daDanhGia; }).length;
                var kq = [];
                if (dangChay) kq.push({ tieuDe: dangChay + ' đơn công việc đang xử lý', moTa: 'Đơn bạn tạo', url: '/FormCongViec/DonCho', icon: 'fa-file-text-o', gap: false });
                if (choDanhGia) kq.push({ tieuDe: choDanhGia + ' đơn chờ bạn đánh giá', url: '/FormCongViec/DonCho', icon: 'fa-star-o', gap: false });
                dsViec.don = kq;
                return layJson('/FormCongViec/GetSoLuongSapQuaHan');
            })
            .then(function (kq) {
                if (kq && kq.count > 0) dsViec.don.push({ tieuDe: kq.count + ' đơn sắp quá hạn', url: '/FormCongViec/HoanTatDonChiDinh', icon: 'fa-clock-o', gap: true });
                veViec();
            })
            .catch(function () {
                dsViec.don = dsViec.don || [];
                veViec();
                baoLoi('Không tải được đơn công việc.');
            });
    }

    document.addEventListener('DOMContentLoaded', function () {
        taiTongQuan();
        taiDon();
    });
})();
