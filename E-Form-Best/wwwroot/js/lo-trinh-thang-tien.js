// Trang Lộ trình dùng chung: /QLCongViec/DanhGia/LoTrinhCuaToi (nhân viên chỉ xem), /QLCongViec/DanhGia/LoTrinh/{maNv} (quản lý).
// Mọi thao tác ghi trả lại toàn bộ lộ trình để vẽ lại tại chỗ, không tải lại trang.
(function () {
    'use strict';

    var st = { ma: '', laCuaToi: true, d: null, chon: null, maGui: null, dangLuu: false };

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

    function ngayVn(s) {
        var p = String(s || '').split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : '—';
    }

    function thoiGian(b) {
        var fmt = function (t) { return t >= 12 && t % 12 === 0 ? (t / 12) + ' năm' : t + ' tháng'; };
        if (b.thangToiThieu === b.thangToiDa) return fmt(b.thangToiThieu);
        return b.thangToiDa < 12 || b.thangToiDa === 12 && b.thangToiThieu < 12
            ? b.thangToiThieu + '–' + b.thangToiDa + ' tháng'
            : (b.thangToiThieu / 12) + '–' + (b.thangToiDa / 12) + ' năm';
    }

    function tai() {
        var tt = $('lttTrangThai');
        tt.hidden = false;
        tt.classList.remove('tdg-error');
        tt.textContent = 'Đang tải dữ liệu...';
        var url = st.laCuaToi ? '/QLCongViec/DanhGia/LoTrinhApi/CuaToi' : '/QLCongViec/DanhGia/LoTrinhApi/NhanVien?maNv=' + encodeURIComponent(st.ma);
        goiApi(url)
            .then(function (kq) {
                nap(kq.duLieu);
                tt.hidden = true;
                $('lttNoiDung').hidden = false;
            })
            .catch(function (e) {
                tt.textContent = e.message;
                tt.classList.add('tdg-error');
            });
    }

    function nap(d) {
        st.d = d;
        var hienTai = d.dsBac.filter(function (b) { return b.laHienTai; })[0] || null;
        if (!st.chon || !d.dsBac.some(function (b) { return b.maBac === st.chon; })) {
            st.chon = d.bacKeTiep || (hienTai && hienTai.maBac) || d.dsBac[0].maBac;
        }
        if (!st.laCuaToi) $('lttTieuDe').textContent = 'Lộ trình: ' + d.hoTen + ' (' + d.maNv + ')';

        $('lttBac').textContent = d.capBac ? d.capBac + (hienTai ? ' · ' + hienTai.chucDanh : '') : 'Chưa có';
        $('lttBacHrm').textContent = d.capBacHrm || '—';
        $('lttDaGiu').textContent = d.soThangGiu === null ? 'Chưa rõ' : d.soThangGiu + ' tháng (từ ' + ngayVn(d.ngayGiuBac) + ')';
        $('lttQuyDinh').textContent = hienTai ? thoiGian(hienTai) + (d.thoiGian === 'du' ? ' · đã đủ' : d.thoiGian === 'chua_du' ? ' · chưa đủ' : '') : '—';
        $('lttKeTiep').textContent = d.bacKeTiep || '—';

        var canhBao = [];
        if (!d.capBac) canhBao.push('Chưa có cấp bậc trong hồ sơ đánh giá, cần nhập ở “Hồ sơ & KPI”.');
        if (d.ngoaiLoTrinh) canhBao.push('Bậc ' + d.capBac + ' không có trong lộ trình 7 bậc.');
        if (d.capBac && d.capBacHrm && d.capBac !== d.capBacHrm) canhBao.push('Bậc trong hồ sơ đánh giá (' + d.capBac + ') khác HRM (' + d.capBacHrm + '). Trang này dùng bậc hồ sơ đánh giá.');
        $('lttCanhBao').hidden = canhBao.length === 0;
        $('lttCanhBaoChu').textContent = canhBao.join(' ');

        veDuong();
        veChiTiet();
        veForm(hienTai);
        veThi();
        veLichSu();
    }

    function veDuong() {
        var o = $('lttDuong');
        o.textContent = '';
        st.d.dsBac.forEach(function (b) {
            var lop = 'lt-bac' + (b.laHienTai ? ' hien-tai' : b.daQua ? ' da-qua' : '') + (b.maBac === st.chon ? ' dang-chon' : '');
            o.appendChild(el('li', null, [
                el('button', { type: 'button', class: lop, 'data-bac': b.maBac, 'aria-pressed': b.maBac === st.chon ? 'true' : 'false' }, [
                    el('b', { text: b.maBac }),
                    el('span', { text: b.chucDanh }),
                    el('small', { text: thoiGian(b) })
                ])
            ]));
        });
    }

    function veChiTiet() {
        var b = st.d.dsBac.filter(function (x) { return x.maBac === st.chon; })[0];
        var o = $('lttChiTiet');
        o.textContent = '';
        if (!b) return;
        o.appendChild(el('h3', { text: b.maBac + ' · ' + b.chucDanh + ' (' + b.chucDanhEn + ')' }));
        o.appendChild(el('p', { text: 'Thời gian giữ bậc: ' + thoiGian(b) }));
        o.appendChild(el('ul', null, b.dsKyNang.map(function (k) { return el('li', { text: k }); })));
    }

    function veForm(hienTai) {
        var ql = !st.laCuaToi;
        $('lttKhungNgay').hidden = !ql;
        $('lttKhungThi').hidden = !ql;
        if (!ql) return;
        $('lttNgayGiu').value = st.d.ngayGiuBac || '';
        $('lttNgayGiu').max = homNay();
        $('lttNgayThi').max = homNay();
        var chon = $('lttDeXuat');
        var cu = chon.value;
        chon.textContent = '';
        st.d.dsBac.filter(function (b) { return !hienTai || b.thuTu > hienTai.thuTu; }).forEach(function (b) {
            chon.appendChild(el('option', { value: b.maBac, text: b.maBac + ' · ' + b.chucDanh }));
        });
        chon.value = cu && Array.prototype.some.call(chon.options, function (x) { return x.value === cu; }) ? cu : (st.d.bacKeTiep || chon.value);
        $('lttLuuThi').disabled = chon.options.length === 0;
    }

    function veThi() {
        var body = $('lttBangThi');
        body.textContent = '';
        if (!st.d.dsThi.length) {
            body.appendChild(el('tr', null, [el('td', { colspan: '8', class: 'tdg-empty', text: 'Chưa có kết quả thi.' })]));
            return;
        }
        st.d.dsThi.forEach(function (t) {
            var trangThai = t.ketQua === 'fail' ? el('span', { class: 'tdg-badge awaiting_self', text: 'Không đạt' })
                : t.ngayLenBac ? el('span', { class: 'tdg-badge completed', text: 'Đã lên bậc ' + ngayVn(t.ngayLenBac) })
                : el('span', { class: 'tdg-badge awaiting_manager', text: 'Đạt, chờ xác nhận' });
            var nut = el('div', { class: 'd-flex gap-1 flex-wrap' }, [
                t.coTheLenBac ? el('button', { type: 'button', class: 'btn btn-sm btn-primary', 'data-len-bac': t.idThi, text: 'Xác nhận lên bậc' }) : null,
                t.coTheXoa ? el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-xoa-thi': t.idThi, text: 'Xoá' }) : null
            ]);
            body.appendChild(el('tr', null, [
                el('td', { text: ngayVn(t.ngayThi) }), el('td', { text: t.bacHienTai || '—' }), el('td', { text: t.bacDeXuat }),
                el('td', { text: t.diem === null ? '—' : String(t.diem) }), el('td', null, [trangThai]),
                el('td', { text: t.ghiChu || '' }), el('td', { text: t.tenNguoiGhi || '' }), el('td', null, [nut])
            ]));
        });
    }

    function veLichSu() {
        var body = $('lttBangLichSu');
        body.textContent = '';
        if (!st.d.dsLichSu.length) {
            body.appendChild(el('tr', null, [el('td', { colspan: '5', class: 'tdg-empty', text: 'Chưa có thay đổi bậc nào được ghi nhận.' })]));
            return;
        }
        st.d.dsLichSu.forEach(function (l) {
            body.appendChild(el('tr', null, [
                el('td', { text: ngayVn(l.ngayHieuLuc) }), el('td', { text: l.bacCu || '—' }), el('td', { text: l.bacMoi || '—' }),
                el('td', { text: l.ghiChu || '' }), el('td', { text: l.tenNguoi || '' })
            ]));
        });
    }

    function luu(url, body, nut, oLoi) {
        if (st.dangLuu) return Promise.resolve(false);
        st.dangLuu = true;
        nut.disabled = true;
        oLoi.textContent = '';
        return goiApi(url, body)
            .then(function (kq) { nap(kq.duLieu); return true; })
            .catch(function (e) { oLoi.textContent = e.message; return false; })
            .finally(function () { st.dangLuu = false; nut.disabled = false; });
    }

    function luuNgay(e) {
        e.preventDefault();
        luu('/QLCongViec/DanhGia/LoTrinhApi/LuuNgayGiuBac', { maNv: st.ma, ngay: $('lttNgayGiu').value || null }, $('lttLuuNgay'), $('lttLoiNgay'));
    }

    function luuThi(e) {
        e.preventDefault();
        // Giữ nguyên khoá gửi khi lỗi mạng để gửi lại không sinh dòng trùng
        if (!st.maGui) st.maGui = maGuiMoi();
        var diem = $('lttDiem').value;
        luu('/QLCongViec/DanhGia/LoTrinhApi/ThemThi', {
            maNv: st.ma, bacDeXuat: $('lttDeXuat').value, ngayThi: $('lttNgayThi').value || null,
            diem: diem === '' ? null : Number(diem), ketQua: $('lttKetQua').value,
            ghiChu: $('lttGhiChu').value, maGui: st.maGui
        }, $('lttLuuThi'), $('lttLoiThi')).then(function (xong) {
            if (!xong) return;
            st.maGui = null;
            ['lttNgayThi', 'lttDiem', 'lttKetQua', 'lttGhiChu'].forEach(function (id) { $(id).value = ''; });
        });
    }

    function lenBac(id, nut) {
        Swal.fire({
            icon: 'question',
            title: 'Xác nhận lên bậc?',
            text: 'Hồ sơ đánh giá đổi sang bậc mới ngay. Chuẩn năng lực và GAP của phiếu chưa chốt sẽ tính theo bậc mới. Không đổi lương hay HRM.',
            showCancelButton: true, confirmButtonText: 'Xác nhận', cancelButtonText: 'Huỷ'
        }).then(function (r) {
            if (!r.isConfirmed) return;
            luu('/QLCongViec/DanhGia/LoTrinhApi/XacNhanLenBac', { maNv: st.ma, idThi: id, ngayHieuLuc: homNay() }, nut, $('lttLoiBang'));
        });
    }

    function xoaThi(id, nut) {
        Swal.fire({
            icon: 'warning', text: 'Xoá kết quả thi này?',
            showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ'
        }).then(function (r) {
            if (!r.isConfirmed) return;
            luu('/QLCongViec/DanhGia/LoTrinhApi/XoaThi', { maNv: st.ma, idThi: id }, nut, $('lttLoiBang'));
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        st.ma = $('ltt').getAttribute('data-ma-nv') || '';
        st.laCuaToi = !st.ma;
        $('lttFormNgay').addEventListener('submit', luuNgay);
        $('lttFormThi').addEventListener('submit', luuThi);
        tai();
    });

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!t.closest) return;
        var bac = t.closest('[data-bac]');
        if (bac) { st.chon = bac.getAttribute('data-bac'); veDuong(); veChiTiet(); return; }
        var len = t.closest('[data-len-bac]');
        if (len) { lenBac(len.getAttribute('data-len-bac'), len); return; }
        var xoa = t.closest('[data-xoa-thi]');
        if (xoa) xoaThi(xoa.getAttribute('data-xoa-thi'), xoa);
    });
})();
