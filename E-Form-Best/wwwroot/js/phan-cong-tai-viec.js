// Ma trận phân công (/QLCongViec/DanhGia/PhanCong). Bấm ô đổi vai trò tại chỗ; server trả lại ma trận để cảnh báo luôn khớp.
(function () {
    'use strict';

    var VONG = ['', 'chinh', 'du_phong_1', 'du_phong_2'];
    var NHAN_VT = { chinh: 'C', du_phong_1: 'D1', du_phong_2: 'D2' };
    var NHAN_CB = {
        overload: ['awaiting_self', 'Overload'],
        mot_nguoi: ['awaiting_self', 'Chỉ 1 người nắm'],
        chua_co_nguoi: ['awaiting_self', 'Chưa có người'],
        khong_chinh: ['awaiting_self', 'Không có người chính'],
        khong_du_phong: ['awaiting_manager', 'Không có dự phòng']
    };
    var st = { d: null, don: {}, duocSua: false, sua: null, dangLuu: false };

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

    function nap(d) {
        st.d = d;
        $('pcvTong').textContent = String(d.dsDauViec.length);
        $('pcvOverload').textContent = String(d.soOverload);
        $('pcvMotNguoi').textContent = String(d.soMotNguoi);
        $('pcvKhongDp').textContent = String(d.soKhongDuPhong);
        $('pcvChuaCo').textContent = String(d.soChuaCoNguoi);
        $('pcvChuaChuan').textContent = String(d.soChuaCoChuan);
        veNguoi();
        veMaTran();
        var ds = $('pcvDsNhom');
        if (ds) {
            ds.textContent = '';
            nhomCua(d.dsDauViec).forEach(function (n) { ds.appendChild(el('option', { value: n })); });
        }
    }

    function nhomCua(ds) {
        var kq = [];
        ds.forEach(function (x) { if (kq.indexOf(x.nhom) < 0) kq.push(x.nhom); });
        return kq;
    }

    function veNguoi() {
        var body = $('pcvBangNguoi');
        body.textContent = '';
        st.d.dsNguoi.filter(function (n) { return n.soChinh + n.soDuPhong > 0; }).forEach(function (n) {
            var don = st.don[n.maNv.toUpperCase()];
            var chuDon = !don ? '…' : !don.laNguoiHoTro ? 'Không nhận đơn' : don.hoanTat + ' / ' + don.duocGiao + ' hoàn tất';
            body.appendChild(el('tr', null, [
                el('td', null, [el('b', { text: n.hoTen }), ' ', el('small', { text: n.maNv }), !n.conTrongDanhSach ? el('span', { class: 'tdg-badge awaiting_self', text: 'Cần bàn giao' }) : null]),
                el('td', { text: String(n.soChinh) }),
                el('td', { text: String(n.soDuPhong) }),
                el('td', { text: String(n.soOverload) }),
                el('td', { text: String(n.soChiMinhNam) }),
                el('td', { text: chuDon })
            ]));
        });
    }

    function veMaTran() {
        var chiCanhBao = $('pcvChiCanhBao').checked;
        var chiCoViec = $('pcvChiCoViec').checked;
        var dsNguoi = st.d.dsNguoi.filter(function (n) { return !chiCoViec || n.soChinh + n.soDuPhong > 0; });

        var dau = $('pcvDau');
        dau.textContent = '';
        var hang = [el('th', { class: 'pcv-ten', text: 'Đầu việc' }), el('th', { text: 'Tải / người' })];
        dsNguoi.forEach(function (n) { hang.push(el('th', { class: 'pcv-nguoi', title: n.maNv, text: n.hoTen })); });
        hang.push(el('th', { text: 'Cảnh báo' }));
        dau.appendChild(el('tr', null, hang));

        var than = $('pcvThan');
        than.textContent = '';
        var soCot = dsNguoi.length + 3;
        var dsViec = st.d.dsDauViec.filter(function (v) { return !chiCanhBao || v.dsCanhBao.length > 0; });
        if (!dsViec.length) {
            than.appendChild(el('tr', null, [el('td', { colspan: String(soCot), class: 'tdg-empty', text: 'Không có đầu việc phù hợp.' })]));
            return;
        }
        nhomCua(dsViec).forEach(function (nhom) {
            than.appendChild(el('tr', { class: 'pcv-nhom' }, [el('td', { colspan: String(soCot), text: nhom })]));
            dsViec.filter(function (v) { return v.nhom === nhom; }).forEach(function (v) { than.appendChild(veDong(v, dsNguoi)); });
        });
    }

    function chuTai(v) {
        if (v.taiMoiNguoi === null) return v.soLuong === null ? '—' : v.soLuong + ' (chưa có người chính)';
        return v.taiMoiNguoi + (v.chuanMoiNguoi ? ' / chuẩn ' + v.chuanMoiNguoi : '');
    }

    function veDong(v, dsNguoi) {
        var ten = el('td', { class: 'pcv-ten', title: v.moTa || '' }, [
            st.duocSua ? el('button', { type: 'button', class: 'pcv-sua', 'data-sua': v.idDauViec, text: v.ten }) : el('span', { text: v.ten }),
            v.tenEn ? el('small', { text: v.tenEn }) : null
        ]);
        var cot = [ten, el('td', { class: 'pcv-tai', text: chuTai(v) })];
        dsNguoi.forEach(function (n) {
            var g = v.dsGan.filter(function (x) { return x.maNv.toUpperCase() === n.maNv.toUpperCase(); })[0];
            var vt = g ? g.vaiTro : '';
            var nhan = NHAN_VT[vt] || '';
            var lop = 'pcv-o' + (vt === 'chinh' ? ' chinh' : vt ? ' du-phong' : '');
            cot.push(el('td', { class: 'pcv-giua' }, [st.duocSua
                ? el('button', { type: 'button', class: lop, 'data-viec': v.idDauViec, 'data-ma': n.maNv, 'data-vt': vt, 'aria-label': n.hoTen + ': ' + (nhan || 'chưa giao'), text: nhan })
                : el('span', { class: lop, text: nhan })]));
        });
        cot.push(el('td', null, v.dsCanhBao.map(function (c) {
            var nhan = NHAN_CB[c] || ['not_started', c];
            return el('span', { class: 'tdg-badge ' + nhan[0], text: nhan[1] });
        })));
        return el('tr', null, cot);
    }

    function doiO(nut) {
        if (st.dangLuu) return;
        var hienTai = nut.getAttribute('data-vt');
        var moi = VONG[(VONG.indexOf(hienTai) + 1) % VONG.length];
        st.dangLuu = true;
        nut.disabled = true;
        $('pcvLoi').textContent = '';
        goiApi('/QLCongViec/DanhGia/PhanCongApi/LuuO', { idDauViec: nut.getAttribute('data-viec'), maNv: nut.getAttribute('data-ma'), vaiTro: moi })
            .then(function (kq) { nap(kq.duLieu); })
            .catch(function (e) { $('pcvLoi').textContent = e.message; nut.disabled = false; })
            .finally(function () { st.dangLuu = false; });
    }

    function moForm(v) {
        st.sua = v ? v.idDauViec : null;
        $('pcvKhungForm').hidden = false;
        $('pcvTieuDeForm').textContent = v ? 'Sửa đầu việc' : 'Thêm đầu việc';
        $('pcvNhom').value = v ? v.nhom : '';
        $('pcvTen').value = v ? v.ten : '';
        $('pcvTenEn').value = v ? v.tenEn || '' : '';
        $('pcvSoLuong').value = v && v.soLuong !== null ? v.soLuong : '';
        $('pcvChuan').value = v && v.chuanMoiNguoi !== null ? v.chuanMoiNguoi : '';
        $('pcvMoTa').value = v ? v.moTa || '' : '';
        $('pcvXoa').hidden = !v;
        $('pcvLoiForm').textContent = '';
        $('pcvKhungForm').scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    function so(id) {
        var v = $(id).value;
        return v === '' ? null : Number(v);
    }

    function luuForm(e) {
        e.preventDefault();
        if (st.dangLuu) return;
        st.dangLuu = true;
        var nut = $('pcvLuu');
        nut.disabled = true;
        $('pcvLoiForm').textContent = '';
        goiApi('/QLCongViec/DanhGia/PhanCongApi/LuuDauViec', {
            idDauViec: st.sua, nhom: $('pcvNhom').value, ten: $('pcvTen').value, tenEn: $('pcvTenEn').value,
            moTa: $('pcvMoTa').value, soLuong: so('pcvSoLuong'), chuanMoiNguoi: so('pcvChuan')
        })
            .then(function (kq) { nap(kq.duLieu); $('pcvKhungForm').hidden = true; })
            .catch(function (ex) { $('pcvLoiForm').textContent = ex.message; })
            .finally(function () { st.dangLuu = false; nut.disabled = false; });
    }

    function xoa() {
        var id = st.sua;
        Swal.fire({
            icon: 'warning', text: 'Xoá đầu việc này? Người đang giữ sẽ được gỡ khỏi đầu việc.',
            showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ'
        }).then(function (r) {
            if (!r.isConfirmed) return;
            var nut = $('pcvXoa');
            nut.disabled = true;
            goiApi('/QLCongViec/DanhGia/PhanCongApi/XoaDauViec', { idDauViec: id })
                .then(function (kq) { nap(kq.duLieu); $('pcvKhungForm').hidden = true; })
                .catch(function (ex) { $('pcvLoiForm').textContent = ex.message; })
                .finally(function () { nut.disabled = false; });
        });
    }

    function taiDon() {
        goiApi('/QLCongViec/DanhGia/PhanCongApi/DonTheoNguoi')
            .then(function (kq) {
                kq.duLieu.forEach(function (x) { st.don[x.maNv.toUpperCase()] = x; });
                veNguoi();
            })
            .catch(function () {
                // Thiếu số đơn không ảnh hưởng ma trận: để nguyên cột, ghi rõ lỗi tại chỗ
                $('pcvLoi').textContent = 'Không tải được số đơn IT.';
            });
    }

    document.addEventListener('DOMContentLoaded', function () {
        st.duocSua = $('pcv').getAttribute('data-duoc-sua') === '1';
        $('pcvChiCanhBao').addEventListener('change', veMaTran);
        $('pcvChiCoViec').addEventListener('change', veMaTran);
        if (st.duocSua) $('pcvForm').addEventListener('submit', luuForm);
        var tt = $('pcvTrangThai');
        goiApi('/QLCongViec/DanhGia/PhanCongApi/MaTran')
            .then(function (kq) {
                nap(kq.duLieu);
                tt.hidden = true;
                $('pcvNoiDung').hidden = false;
                taiDon();
            })
            .catch(function (e) {
                tt.textContent = e.message;
                tt.classList.add('tdg-error');
            });
    });

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!t.closest) return;
        var o = t.closest('button.pcv-o');
        if (o) { doiO(o); return; }
        var sua = t.closest('[data-sua]');
        if (sua) {
            moForm(st.d.dsDauViec.filter(function (v) { return v.idDauViec === sua.getAttribute('data-sua'); })[0]);
            return;
        }
        if (t.closest('#pcvThem')) { moForm(null); return; }
        if (t.closest('#pcvHuy')) { $('pcvKhungForm').hidden = true; return; }
        if (t.closest('#pcvXoa')) xoa();
    });
})();
