// Trang Kỳ đánh giá (/QLCongViec/DanhGia/KyDanhGia): mở, sửa, đóng, mở lại kỳ.
(function () {
    'use strict';

    var st = { ds: [], sua: null, dangLuu: false };

    function $(id) { return document.getElementById(id); }

    function el(tag, attrs, con) {
        var e = document.createElement(tag);
        if (attrs) Object.keys(attrs).forEach(function (k) {
            if (k === 'text') e.textContent = attrs[k];
            else if (k === 'class') e.className = attrs[k];
            else if (attrs[k] !== null && attrs[k] !== undefined && attrs[k] !== false) e.setAttribute(k, attrs[k]);
        });
        (con || []).forEach(function (c) { if (c) e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
        return e;
    }

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function goiApi(url, body) {
        var opt = body === undefined
            ? { headers: { 'Accept': 'application/json' } }
            : { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() }, body: JSON.stringify(body) };
        return fetch(url, opt).then(function (res) {
            if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
            if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
            return res.json();
        }).then(function (kq) {
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Thao tác thất bại.');
            return kq;
        });
    }

    function ngay(s) {
        var p = String(s || '').split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : '—';
    }

    function ve() {
        var tb = $('kydgBang');
        tb.textContent = '';
        var coMo = st.ds.some(function (k) { return k.dangMo; });
        if (!st.ds.length) tb.appendChild(el('tr', null, [el('td', { colspan: '6', class: 'text-muted', text: 'Chưa có kỳ nào.' })]));
        st.ds.forEach(function (k) {
            var nut = [el('button', { type: 'button', class: 'btn btn-sm btn-outline-secondary', 'data-sua': k.idKy, text: 'Sửa' })];
            if (k.dangMo) nut.push(el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-dong': k.idKy, text: 'Đóng kỳ' }));
            // Đang có kỳ mở thì không mở lại kỳ khác; kỳ thang cũ không chấm tiếp được
            else if (!coMo && !k.thangCu) nut.push(el('button', { type: 'button', class: 'btn btn-sm btn-outline-primary', 'data-mo-lai': k.idKy, text: 'Mở lại' }));
            tb.appendChild(el('tr', null, [
                el('td', null, [el('span', { class: 'tdg-badge ' + (k.dangMo ? 'completed' : 'not_started'), text: k.dangMo ? 'Đang mở' : 'Đã đóng' })]),
                el('td', { text: k.ten }),
                el('td', { text: ngay(k.ngayBatDau) }),
                el('td', { text: ngay(k.hanNop) }),
                el('td', { text: String(k.soPhieu) }),
                el('td', null, [el('div', { class: 'ky-nut' }, nut)])
            ]));
        });
        capNhatForm();
    }

    function capNhatForm() {
        var dangSua = !!st.sua;
        var coMo = st.ds.some(function (k) { return k.dangMo; });
        $('kydgTieuDeForm').textContent = dangSua ? 'Sửa kỳ: ' + st.sua.ten : 'Mở kỳ mới';
        var nut = $('kydgLuu');
        nut.textContent = '';
        nut.appendChild(el('i', { class: dangSua ? 'fa fa-save' : 'fa fa-plus' }));
        nut.appendChild(document.createTextNode(dangSua ? ' Lưu' : ' Mở kỳ'));
        nut.disabled = st.dangLuu || (!dangSua && coMo);
        nut.title = !dangSua && coMo ? 'Đang có kỳ mở — đóng kỳ đó trước khi mở kỳ mới' : '';
        $('kydgHuySua').hidden = !dangSua;
    }

    function napForm(k) {
        st.sua = k;
        $('kydgTen').value = k ? k.ten : '';
        $('kydgBatDau').value = k && k.ngayBatDau ? k.ngayBatDau : '';
        $('kydgHanNop').value = k && k.hanNop ? k.hanNop : '';
        $('kydgLoi').textContent = '';
        capNhatForm();
    }

    function tai() {
        goiApi('/QLCongViec/DanhGia/DsKy')
            .then(function (kq) { st.ds = kq.duLieu || []; $('kydgTai').hidden = true; ve(); })
            .catch(function (e) { var o = $('kydgTai'); o.classList.add('tdg-error'); o.textContent = e.message; });
    }

    function luu(e) {
        e.preventDefault();
        if (st.dangLuu) return;
        st.dangLuu = true;
        capNhatForm();
        $('kydgLoi').textContent = '';
        var dangSua = !!st.sua;
        goiApi(dangSua ? '/QLCongViec/DanhGia/QuanLy/SuaKy' : '/QLCongViec/DanhGia/QuanLy/MoKy', {
            idKy: dangSua ? st.sua.idKy : null,
            ten: $('kydgTen').value,
            ngayBatDau: $('kydgBatDau').value || null,
            hanNop: $('kydgHanNop').value || null
        })
            .then(function (kq) {
                st.ds = kq.duLieu || [];
                napForm(null);
                ve();
                Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1500, showConfirmButton: false });
            })
            .catch(function (err) { $('kydgLoi').textContent = err.message; })
            .finally(function () { st.dangLuu = false; capNhatForm(); });
    }

    function doiTrangThai(url, nut, cauHoi) {
        Swal.fire({ icon: 'question', text: cauHoi, showCancelButton: true, confirmButtonText: 'Đồng ý', cancelButtonText: 'Huỷ' }).then(function (r) {
            if (!r.isConfirmed) return;
            nut.disabled = true;
            goiApi(url, { idKy: nut.getAttribute('data-dong') || nut.getAttribute('data-mo-lai') })
                .then(function (kq) {
                    st.ds = kq.duLieu || [];
                    ve();
                    Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1800, showConfirmButton: false });
                })
                .catch(function (e) { nut.disabled = false; Swal.fire({ icon: 'error', text: e.message }); });
        });
    }

    document.addEventListener('click', function (e) {
        var sua = e.target.closest('[data-sua]');
        if (sua) { napForm(st.ds.filter(function (k) { return k.idKy === sua.getAttribute('data-sua'); })[0] || null); $('kydgTen').focus(); return; }
        var dong = e.target.closest('[data-dong]');
        if (dong) { doiTrangThai('/QLCongViec/DanhGia/QuanLy/DongKy', dong, 'Đóng kỳ này? Phiếu chưa gửi cũng bị khoá, nhân viên và quản lý chỉ còn xem được.'); return; }
        var moLai = e.target.closest('[data-mo-lai]');
        if (moLai) { doiTrangThai('/QLCongViec/DanhGia/QuanLy/MoLaiKy', moLai, 'Mở lại kỳ này để tiếp tục chấm?'); return; }
        if (e.target.closest('#kydgHuySua')) napForm(null);
    });

    document.addEventListener('submit', function (e) {
        if (e.target.id === 'kydgForm') luu(e);
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', tai);
    else tai();
})();
