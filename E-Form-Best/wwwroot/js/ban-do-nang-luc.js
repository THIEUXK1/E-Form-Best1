// Trang Bản đồ năng lực (/QLCongViec/DanhGia/NangLuc): GAP từng người theo 4 nhóm năng lực trong 1 kỳ.
(function () {
    'use strict';

    var NHOM = ['KN', 'KS', 'TD', 'NN'];
    var st = { ds: [], loc: '' };

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

    function coGT(v) { return v !== null && v !== undefined; }
    function fmt(n) { return (n > 0 ? '+' : '') + (Math.round(n * 100) / 100).toString(); }
    function lop(gap) { return !coGT(gap) ? '' : gap >= 1 ? 'vuot' : gap >= 0 ? 'dat' : gap >= -1 ? 'cai' : 'chua'; }
    function oGap(gap) { return el('span', { class: 'nl-o ' + lop(gap), text: coGT(gap) ? fmt(gap) : '—' }); }

    function baoLoi(msg) {
        $('bdnlLoi').hidden = !msg;
        $('bdnlLoiText').textContent = msg || '';
    }

    function dsLoc() {
        var nhom = $('bdnlNhom').value, tim = $('bdnlTim').value.trim().toLowerCase();
        return st.ds.filter(function (nv) {
            if (nhom && nv.tenNhomCongViec !== nhom) return false;
            if (st.loc === 'chua_cham' && coGT(nv.tbGap)) return false;
            if (st.loc && st.loc !== 'chua_cham' && nv.xepLoai !== st.loc) return false;
            return !tim || (nv.maNv + ' ' + nv.hoTen).toLowerCase().indexOf(tim) >= 0;
        });
    }

    function ve() {
        var ds = dsLoc();
        var dem = function (x) { return st.ds.filter(function (nv) { return nv.xepLoai === x; }).length; };
        $('bdnlTong').textContent = st.ds.length;
        $('bdnlVuot').textContent = dem('Vượt chuẩn');
        $('bdnlDat').textContent = dem('Đạt chuẩn');
        $('bdnlCai').textContent = dem('Cần cải thiện');
        $('bdnlChua').textContent = dem('Chưa đạt');
        $('bdnlChuaCham').textContent = st.ds.filter(function (nv) { return !coGT(nv.tbGap); }).length;
        document.querySelectorAll('#bdnl .tkdg-kpi').forEach(function (o) {
            o.classList.toggle('dang-chon', o.getAttribute('data-loc') === st.loc && !!st.loc);
        });
        var thangCu = !!(st.ky && st.ky.thangCu);
        $('bdnlMoTaLoc').textContent = thangCu ? '• Kỳ thang cũ: không tính GAP và xếp loại'
            : st.loc ? '• ' + (st.loc === 'chua_cham' ? 'Chưa chấm' : st.loc) : '';

        var tb = $('bdnlBang');
        tb.textContent = '';
        ds.forEach(function (nv) {
            var theoNhom = {};
            (nv.theoNhom || []).forEach(function (n) { theoNhom[n.maNhom] = n.gap; });
            tb.appendChild(el('tr', null, [
                el('td', null, [el('a', { href: '/QLCongViec/DanhGia/NangLuc/' + encodeURIComponent(nv.maNv), text: nv.maNv })]),
                el('td', { text: nv.hoTen }),
                el('td', { text: nv.tenNhomCongViec || 'Chưa gán' }),
                el('td', { class: 'so', text: nv.capBac || '—' }),
                el('td', { class: 'so' }, [oGap(theoNhom.KN)]),
                el('td', { class: 'so' }, [oGap(theoNhom.KS)]),
                el('td', { class: 'so' }, [oGap(theoNhom.TD)]),
                el('td', { class: 'so' }, [oGap(theoNhom.NN)]),
                el('td', { class: 'so' }, [oGap(nv.tbGap)]),
                el('td', { text: nv.xepLoai || (thangCu ? '—' : nv.soNangLuc ? 'Chưa chấm' : 'Chưa có khung') }),
                el('td', { class: 'so', text: nv.soDaCham + '/' + nv.soNangLuc })
            ]));
        });
        $('bdnlTrong').hidden = ds.length > 0;
    }

    function napNhom() {
        var sel = $('bdnlNhom'), cu = sel.value;
        var ten = st.ds.map(function (nv) { return nv.tenNhomCongViec; }).filter(function (t, i, a) { return t && a.indexOf(t) === i; }).sort();
        while (sel.options.length > 1) sel.remove(1);
        ten.forEach(function (t) { sel.appendChild(el('option', { value: t, text: t })); });
        sel.value = ten.indexOf(cu) >= 0 ? cu : '';
    }

    function tai() {
        baoLoi('');
        fetch(KyDanhGia.thamSo('/QLCongViec/DanhGia/NangLucApi/BanDo'), { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được dữ liệu.');
                st.ds = kq.duLieu.dsNhanVien || [];
                st.ky = kq.duLieu.ky;
                napNhom();
                ve();
            })
            .catch(function (e) { baoLoi(e.message); });
    }

    document.addEventListener('click', function (e) {
        var kpi = e.target.closest('#bdnl .tkdg-kpi');
        if (kpi) {
            var loc = kpi.getAttribute('data-loc');
            st.loc = st.loc === loc ? '' : loc;
            ve();
            return;
        }
        if (e.target.closest('#bdnlLamMoi')) tai();
    });
    document.addEventListener('change', function (e) { if (e.target.id === 'bdnlNhom') ve(); });
    document.addEventListener('input', function (e) { if (e.target.id === 'bdnlTim') ve(); });

    function batDau() { KyDanhGia.khoiTao('bdnlKy', tai); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', batDau);
    else batDau();
})();
