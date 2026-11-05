// Trang Kế hoạch phát triển dùng chung: /QLCongViec/DanhGia/KeHoachCuaToi (nhân viên), /QLCongViec/DanhGia/KeHoach/{maNv} (quản lý).
// Mục tiêu tạo mới chỉ trong kỳ đang mở; mục tiêu của kỳ đã đóng vẫn cập nhật được tiến độ và kết quả.
(function () {
    'use strict';

    var TEN_TT = { doing: 'Đang làm', done: 'Hoàn thành', cancelled: 'Đã huỷ' };
    var LOP_TT = { doing: 'awaiting_manager', done: 'completed', cancelled: 'not_started' };
    var st = { ma: '', laCuaToi: true, d: null, sua: null, maGui: null, dangLuu: false };

    function $(id) { return document.getElementById(id); }

    function el(tag, attrs, con) {
        var e = document.createElement(tag);
        if (attrs) Object.keys(attrs).forEach(function (k) {
            if (k === 'text') e.textContent = attrs[k];
            else if (k === 'class') e.className = attrs[k];
            else if (attrs[k] !== null && attrs[k] !== undefined && attrs[k] !== false) e.setAttribute(k, attrs[k] === true ? '' : attrs[k]);
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

    function maGuiMoi() {
        return (window.crypto && crypto.randomUUID) ? crypto.randomUUID()
            : 'xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx'.replace(/x/g, function () { return Math.floor(Math.random() * 16).toString(16); });
    }

    function homNay() {
        var d = new Date();
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }

    function quaHan(m) { return m.trangThai === 'doing' && !!m.hanHoanThanh && m.hanHoanThanh < homNay(); }

    function tai() {
        var tt = $('khptTrangThai');
        tt.hidden = false;
        tt.classList.remove('tdg-error');
        tt.textContent = 'Đang tải dữ liệu...';
        $('khptNoiDung').hidden = true;
        var url = st.laCuaToi ? '/QLCongViec/DanhGia/KeHoachApi/CuaToi' : '/QLCongViec/DanhGia/KeHoachApi/NhanVien?maNv=' + encodeURIComponent(st.ma);
        goiApi(KyDanhGia.thamSo(url))
            .then(function (kq) {
                nap(kq.duLieu);
                tt.hidden = true;
                $('khptNoiDung').hidden = false;
            })
            .catch(function (e) {
                tt.classList.add('tdg-error');
                tt.textContent = e.message;
            });
    }

    function nap(d) {
        st.d = d;
        if (!st.laCuaToi) $('khptTieuDe').textContent = 'Kế hoạch phát triển — ' + (d.hoTen || d.maNv);
        veTomTat();
        veGoiY();
        napLuaChon();
        veBang();
        moForm(null);
    }

    function veTomTat() {
        var ds = st.d.dsMucTieu.filter(function (m) { return m.trangThai !== 'cancelled'; });
        $('khptTong').textContent = ds.length;
        $('khptDangLam').textContent = ds.filter(function (m) { return m.trangThai === 'doing'; }).length;
        $('khptXong').textContent = ds.filter(function (m) { return m.trangThai === 'done'; }).length;
        $('khptQuaHan').textContent = ds.filter(quaHan).length;
        var tong = ds.reduce(function (s, m) { return s + m.tienDo; }, 0);
        $('khptTienDo').textContent = (ds.length ? Math.round(tong / ds.length) : 0) + '%';
    }

    function veGoiY() {
        var ds = st.d.dsGoiY || [];
        $('khptKhungGoiY').hidden = !ds.length;
        $('khptNguonGoiY').textContent = st.d.goiYTuKy ? 'Kỳ này chưa có điểm năng lực, gợi ý theo kết quả kỳ “' + st.d.goiYTuKy + '”. ' : '';
        var tb = $('khptGoiY');
        tb.textContent = '';
        ds.forEach(function (g) {
            var noiDung = g.dsNoiDung.length
                ? el('ul', { class: 'kh-ds-nd' }, g.dsNoiDung.map(function (n) {
                    return el('li', { text: n.noiDung + (n.nguoiDaoTao ? ' — ' + n.nguoiDaoTao : '') });
                }))
                : el('span', { class: 'text-muted', text: 'Chưa có nội dung trong kế hoạch đào tạo, tự đặt mục tiêu.' });
            var coMo = st.d.ky.dangMo;
            tb.appendChild(el('tr', null, [
                el('td', null, [el('b', { text: g.ma }), ' ' + g.ten]),
                el('td', null, [el('span', { class: 'nl-o ' + (g.gap >= -1 ? 'cai' : 'chua'), text: String(Math.round(g.gap * 100) / 100) })]),
                el('td', null, [noiDung]),
                el('td', null, [g.daCoMucTieu ? el('span', { class: 'text-muted', text: 'Đã có mục tiêu' })
                    : coMo ? el('button', { type: 'button', class: 'btn btn-sm btn-outline-primary', 'data-tao': g.idNangLuc, text: 'Tạo mục tiêu' }) : null])
            ]));
        });
    }

    function napSelect(sel, ds) {
        while (sel.options.length > 1) sel.remove(1);
        ds.forEach(function (x) { sel.appendChild(el('option', { value: x[0], text: x[1] })); });
    }

    function napLuaChon() {
        var nl = (st.d.dsGoiY || []).map(function (g) { return [g.idNangLuc, g.ma + ' ' + g.ten]; });
        // Mục tiêu cũ có thể gắn năng lực nay đã hết GAP âm, vẫn phải chọn lại được
        st.d.dsMucTieu.forEach(function (m) {
            if (m.idNangLuc && !nl.some(function (x) { return x[0] === m.idNangLuc; })) nl.push([m.idNangLuc, m.tenNangLuc || '']);
        });
        napSelect($('khptNangLuc'), nl);
        napSelect($('khptDaoTao'), (st.d.dsDaoTao || []).map(function (d) {
            return [String(d.id), d.ten + (d.ngay ? ' (' + KyDanhGia.ngayVn(d.ngay) + ')' : '')];
        }));
    }

    function veBang() {
        var tb = $('khptBang');
        tb.textContent = '';
        var ds = st.d.dsMucTieu;
        $('khptMoTaDs').textContent = st.d.ky.dangMo ? 'Gồm cả mục tiêu chưa xong của kỳ trước.' : 'Kỳ đã đóng: vẫn cập nhật được tiến độ và kết quả.';
        if (!ds.length) tb.appendChild(el('tr', null, [el('td', { colspan: '7', class: 'text-muted', text: 'Chưa có mục tiêu nào.' })]));
        ds.forEach(function (m) {
            var bar = el('div', { class: 'kh-tien-do' }, [el('i', { style: 'width:' + m.tienDo + '%' })]);
            var daoTao = m.idDaoTao
                ? el('div', null, [m.tenDaoTao || 'Buổi đào tạo đã xoá', el('small', { class: 'd-block text-muted', text: m.daDuDaoTao ? 'Đã tham gia' : (m.trangThaiDaoTao || '') })])
                : document.createTextNode('—');
            tb.appendChild(el('tr', { class: quaHan(m) ? 'kh-tre' : '' }, [
                el('td', null, [
                    el('span', { class: 'nl-ten', text: m.noiDung }),
                    m.ketQuaMongDoi ? el('span', { class: 'nl-mo-ta', text: 'Mong đợi: ' + m.ketQuaMongDoi }) : null,
                    m.tenNangLuc ? el('span', { class: 'nl-mo-ta', text: 'Năng lực: ' + m.tenNangLuc }) : null,
                    m.tuKyTruoc ? el('span', { class: 'nl-mo-ta', text: 'Từ kỳ: ' + (m.tenKy || '') }) : null
                ]),
                el('td', { text: m.hanHoanThanh ? KyDanhGia.ngayVn(m.hanHoanThanh) : '—' }),
                el('td', null, [bar, el('small', { text: m.tienDo + '%' })]),
                el('td', null, [el('span', { class: 'tdg-badge ' + LOP_TT[m.trangThai], text: quaHan(m) ? 'Quá hạn' : TEN_TT[m.trangThai] })]),
                el('td', null, [daoTao]),
                el('td', { text: m.ketQuaThucTe || '—' }),
                el('td', null, [el('div', { class: 'ky-nut' }, [
                    el('button', { type: 'button', class: 'btn btn-sm btn-outline-secondary', 'data-sua': m.idMucTieu, text: 'Cập nhật' }),
                    m.suaNoiDung ? el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-xoa': m.idMucTieu, text: 'Xoá' }) : null
                ])])
            ]));
        });
    }

    // m = null: form thêm mới; có m: sửa. goiY: điền sẵn từ năng lực GAP âm
    function moForm(m, goiY) {
        st.sua = m;
        st.maGui = maGuiMoi();
        var khoaNd = !!m && !m.suaNoiDung;
        $('khptNoiDungMt').value = m ? m.noiDung : (goiY ? 'Nâng năng lực ' + goiY.ma + ' ' + goiY.ten + ' lên mức yêu cầu' : '');
        $('khptMongDoi').value = m ? (m.ketQuaMongDoi || '') : '';
        $('khptHan').value = m && m.hanHoanThanh ? m.hanHoanThanh : '';
        $('khptNangLuc').value = m && m.idNangLuc ? m.idNangLuc : (goiY ? goiY.idNangLuc : '');
        $('khptDaoTao').value = m && m.idDaoTao ? String(m.idDaoTao) : '';
        $('khptTienDoMt').value = m ? m.tienDo : 0;
        $('khptTrangThaiMt').value = m ? m.trangThai : 'doing';
        $('khptKetQua').value = m ? (m.ketQuaThucTe || '') : '';
        ['khptNoiDungMt', 'khptMongDoi', 'khptHan', 'khptNangLuc'].forEach(function (id) { $(id).disabled = khoaNd; });
        $('khptTieuDeForm').textContent = m ? 'Cập nhật mục tiêu' : 'Thêm mục tiêu';
        $('khptMoTaForm').textContent = khoaNd ? 'Mục tiêu của kỳ đã đóng: chỉ cập nhật tiến độ, trạng thái, đào tạo và kết quả.'
            : 'Mỗi mục tiêu là một việc cụ thể, có hạn và kết quả đo được.';
        $('khptHuySua').hidden = !m;
        $('khptLoi').textContent = '';
        // Kỳ đóng không thêm mới; chỉ hiện form khi bấm Cập nhật
        $('khptKhungForm').hidden = !m && !st.d.ky.dangMo;
    }

    function luu(e) {
        e.preventDefault();
        if (st.dangLuu) return;
        st.dangLuu = true;
        var nut = $('khptLuu');
        nut.disabled = true;
        $('khptLoi').textContent = '';
        goiApi(st.laCuaToi ? '/QLCongViec/DanhGia/KeHoachApi/LuuCuaToi' : '/QLCongViec/DanhGia/KeHoachApi/LuuQuanLy', {
            maNv: st.ma || null,
            idKy: st.d.ky.idKy,
            idMucTieu: st.sua ? st.sua.idMucTieu : null,
            maGui: st.sua ? null : st.maGui,
            noiDung: $('khptNoiDungMt').value,
            ketQuaMongDoi: $('khptMongDoi').value,
            hanHoanThanh: $('khptHan').value || null,
            tienDo: parseInt($('khptTienDoMt').value, 10) || 0,
            trangThai: $('khptTrangThaiMt').value,
            ketQuaThucTe: $('khptKetQua').value,
            idNangLuc: $('khptNangLuc').value || null,
            idDaoTao: $('khptDaoTao').value ? parseInt($('khptDaoTao').value, 10) : null
        })
            .then(function (kq) {
                Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1300, showConfirmButton: false });
                tai();
            })
            // Lỗi hiện tại chỗ, giữ nguyên phần đã nhập
            .catch(function (err) { $('khptLoi').textContent = err.message; })
            .finally(function () { st.dangLuu = false; nut.disabled = false; });
    }

    function xoa(nut) {
        Swal.fire({ icon: 'question', text: 'Xoá mục tiêu này?', showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ' }).then(function (r) {
            if (!r.isConfirmed) return;
            nut.disabled = true;
            goiApi(st.laCuaToi ? '/QLCongViec/DanhGia/KeHoachApi/XoaCuaToi' : '/QLCongViec/DanhGia/KeHoachApi/XoaQuanLy', {
                maNv: st.ma || null, idMucTieu: nut.getAttribute('data-xoa')
            })
                .then(function () { tai(); })
                .catch(function (e) { nut.disabled = false; Swal.fire({ icon: 'error', text: e.message }); });
        });
    }

    document.addEventListener('click', function (e) {
        var tao = e.target.closest('[data-tao]');
        if (tao) {
            var id = tao.getAttribute('data-tao');
            moForm(null, st.d.dsGoiY.filter(function (g) { return g.idNangLuc === id; })[0]);
            $('khptKhungForm').scrollIntoView({ behavior: 'smooth', block: 'center' });
            $('khptMongDoi').focus();
            return;
        }
        var sua = e.target.closest('[data-sua]');
        if (sua) {
            moForm(st.d.dsMucTieu.filter(function (m) { return m.idMucTieu === sua.getAttribute('data-sua'); })[0]);
            $('khptKhungForm').scrollIntoView({ behavior: 'smooth', block: 'center' });
            return;
        }
        var nutXoa = e.target.closest('[data-xoa]');
        if (nutXoa) { xoa(nutXoa); return; }
        if (e.target.closest('#khptHuySua')) moForm(null);
    });

    document.addEventListener('submit', function (e) {
        if (e.target.id === 'khptForm') luu(e);
    });

    function batDau() {
        st.ma = $('khpt').getAttribute('data-ma-nv') || '';
        st.laCuaToi = !st.ma;
        KyDanhGia.khoiTao('khptKy', tai);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', batDau);
    else batDau();
})();
