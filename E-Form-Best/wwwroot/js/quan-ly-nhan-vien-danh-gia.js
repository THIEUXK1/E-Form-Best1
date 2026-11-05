// Trang Quản lý nhân viên (/QLCongViec/DanhGia/NhanVien) — chuyển từ /manager/employees của app evaluation.
// Lọc/tìm làm ở client (danh sách vài chục người); thêm, ngừng, sửa hồ sơ đều AJAX, chỉ vẽ lại phần đổi.
(function () {
    'use strict';

    var TEN_TRANG_THAI = {
        completed: 'Hoàn thành',
        awaiting_manager: 'Chờ quản lý',
        awaiting_self: 'Chờ tự đánh giá',
        not_started: 'Chưa đánh giá'
    };
    var GHI_CHU_TRONG = {
        awaiting_self: 'Quản lý đã đánh giá, chờ tự đánh giá',
        awaiting_manager: 'Đã tự đánh giá, chờ quản lý',
        not_started: 'Chưa có đánh giá'
    };

    var st = { ds: [], hoSo: null, modal: null, dsCapMaTran: [] };

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

    function so(v) { var n = parseFloat(v); return isNaN(n) ? 0 : n; }
    function fmt(n) { return (Math.round(n * 10) / 10).toString(); }

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

    function lopXepLoai(x) {
        if (!x) return '';
        if (x.indexOf('Vượt') === 0) return 'vuot';
        if (x.indexOf('Đạt') === 0) return 'dat';
        if (x.indexOf('Cần') === 0) return 'can';
        return 'chua';
    }

    // ---------- Danh sách ----------
    function taiDanhSach() {
        goiApi(KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/DanhSach'))
            .then(function (kq) {
                st.ds = kq.duLieu || [];
                $('qlnvTrangThaiTai').hidden = true;
                veDanhSach();
            })
            .catch(function (e) {
                var o = $('qlnvTrangThaiTai');
                o.hidden = false;
                o.classList.add('tdg-error');
                o.textContent = e.message;
            });
    }

    function locDanhSach() {
        var tim = $('qlnvTim').value.trim().toLowerCase();
        var tt = $('qlnvTrangThai').value;
        var xl = $('qlnvXepLoai').value;
        return st.ds.filter(function (nv) {
            if (tim && [nv.hoTen, nv.maNv, nv.capBac, nv.tenNhomCongViec, nv.chucVu, nv.phongBan].join(' ').toLowerCase().indexOf(tim) < 0) return false;
            if (tt && nv.trangThai !== tt) return false;
            if (xl && nv.xepLoai !== xl) return false;
            return true;
        });
    }

    function veDanhSach() {
        var luoi = $('qlnvLuoi');
        luoi.textContent = '';
        var ds = locDanhSach();
        $('qlnvDem').textContent = 'Hiển thị ' + ds.length + '/' + st.ds.length + ' nhân viên';
        if (!ds.length) {
            luoi.appendChild(el('div', { class: 'tdg-empty', style: 'grid-column:1/-1' }, [
                el('div', { text: st.ds.length ? 'Không tìm thấy nhân viên phù hợp bộ lọc.' : 'Chưa có nhân viên nào trong hệ thống đánh giá.' }),
                el('small', { text: st.ds.length ? 'Thử đổi từ khoá hoặc bộ lọc.' : 'Nhập mã NV ở ô “Thêm” để đưa nhân viên vào danh sách.' })
            ]));
            return;
        }
        ds.forEach(function (nv) { luoi.appendChild(theNhanVien(nv)); });
    }

    function dong(nhan, giaTri) {
        return el('div', { class: 'tdg-nv-dong' }, [el('span', { text: nhan }), el('b', { text: giaTri })]);
    }

    // ngayVaoLam dạng "yyyy-MM-dd" (DateOnly từ HRM) → "dd/MM/yyyy (x năm y tháng)"
    function ngayVaoLam(s) {
        if (!s) return '—';
        var p = s.split('-'), nam = +p[0], thang = +p[1], ngay = +p[2];
        var nay = new Date();
        var soThang = (nay.getFullYear() - nam) * 12 + (nay.getMonth() + 1 - thang) - (nay.getDate() < ngay ? 1 : 0);
        var thamNien = soThang < 1 ? 'dưới 1 tháng'
            : (Math.floor(soThang / 12) ? Math.floor(soThang / 12) + ' năm ' : '') + (soThang % 12 ? (soThang % 12) + ' tháng' : '');
        return p[2] + '/' + p[1] + '/' + p[0] + ' (' + thamNien.trim() + ')';
    }

    function theNhanVien(nv) {
        var coDiem = nv.diemNangLuc !== null && nv.diemNangLuc !== undefined;
        var gap = so(nv.chenhLech);
        var phanDiem = coDiem
            ? el('div', { class: 'tdg-nv-diem' }, [
                dong('Part I / Part II', fmt(so(nv.diemKpi)) + ' / ' + fmt(so(nv.diemNangLuc))),
                dong('Tổng (100)', fmt(so(nv.diemThucTe))),
                el('div', { class: 'tdg-nv-dong' }, [el('span', { text: 'So với chuẩn 60' }),
                    el('span', { class: 'tdg-gap ' + (gap > 0 ? 'duong' : gap < 0 ? 'am' : 'bang'), text: (gap > 0 ? '+' : '') + fmt(gap) })]),
                nv.xepLoai ? el('div', { class: 'tdg-xl ' + lopXepLoai(nv.xepLoai), text: nv.xepLoai }) : null
            ])
            : el('div', { class: 'tdg-nv-trong' }, [el('i', { class: 'fa fa-user-o fa-2x' }), el('div', { text: GHI_CHU_TRONG[nv.trangThai] || 'Chưa có đánh giá' })]);

        return el('div', { class: 'tdg-nv', 'data-ma': nv.maNv }, [
            el('div', { class: 'tdg-nv-head' }, [
                el('div', null, [el('div', { class: 'tdg-nv-ten', text: nv.hoTen }), el('div', { class: 'tdg-nv-ma', text: nv.maNv + (nv.phongBan ? ' • ' + nv.phongBan : '') })]),
                el('span', { class: 'tdg-badge ' + nv.trangThai, text: TEN_TRANG_THAI[nv.trangThai] || nv.trangThai })
            ]),
            nv.dangLamViec === false ? el('div', { class: 'tdg-loi', text: 'HRM: đã nghỉ việc' }) : null,
            dong('Chức vụ', nv.chucVu || '—'),
            dong('Ngày vào làm', ngayVaoLam(nv.ngayVaoLam)),
            // Cấp bậc đánh giá lấy từ hồ sơ; hiện kèm cấp bậc HRM khi lệch để quản lý biết cần cập nhật hồ sơ
            dong('Cấp bậc', nv.capBac + (nv.coHoSo ? '' : ' (mặc định)')
                + (nv.capBacHrm && nv.capBacHrm !== nv.capBac ? ' • HRM: ' + nv.capBacHrm : '')),
            dong('Nhóm công việc', nv.tenNhomCongViec || 'Chưa gán'),
            dong('KPI giao / tiêu chí NL', nv.soKpi + ' / ' + nv.soTieuChi),
            phanDiem,
            el('div', { class: 'tdg-nv-nut' }, [
                el('a', { class: 'btn btn-sm btn-outline-secondary tdg-nut-rong', href: '/QLCongViec/DanhGia/ChiTiet/' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-eye' }), ' Xem chi tiết']),
                el('a', { class: 'btn btn-sm btn-primary tdg-btn-main', style: 'min-width:0', href: '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-pencil-square-o' }), ' Đánh giá']),
                el('button', { type: 'button', class: 'btn btn-sm btn-outline-primary', 'data-sua': nv.maNv }, [el('i', { class: 'fa fa-sliders' }), ' Hồ sơ & KPI']),
                el('a', { class: 'btn btn-sm btn-outline-primary', href: '/QLCongViec/DanhGia/NangLuc/' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-crosshairs' }), ' Năng lực']),
                el('a', { class: 'btn btn-sm btn-outline-primary', href: '/QLCongViec/DanhGia/KeHoach/' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-flag-checkered' }), ' Kế hoạch']),
                el('a', { class: 'btn btn-sm btn-outline-primary', href: '/QLCongViec/DanhGia/LoTrinh/' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-road' }), ' Lộ trình']),
                el('a', { class: 'btn btn-sm btn-outline-success', href: KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/Excel?maNv=' + encodeURIComponent(nv.maNv)) }, [el('i', { class: 'fa fa-file-excel-o' }), ' Excel']),
                el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-ngung': nv.maNv }, [el('i', { class: 'fa fa-ban' }), ' Ngừng đánh giá'])
            ])
        ]);
    }

    // ---------- Thêm / ngừng ----------
    function themNhanVien(e) {
        e.preventDefault();
        var o = $('qlnvMaThem');
        var ma = o.value.trim();
        if (!ma) { o.focus(); return; }
        var nut = e.target.querySelector('button');
        nut.disabled = true;
        goiApi('/QLCongViec/DanhGia/QuanLy/Them', { maNv: ma })
            .then(function (kq) {
                o.value = '';
                Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1600, showConfirmButton: false });
                return goiApi(KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/DanhSach')).then(function (k2) {
                    st.ds = k2.duLieu || [];
                    veDanhSach();
                    moHoSo(kq.duLieu.maNv);
                });
            })
            .catch(function (err) { Swal.fire({ icon: 'warning', text: err.message }); })
            .finally(function () { nut.disabled = false; });
    }

    function ngungDanhGia(nut) {
        var ma = nut.getAttribute('data-ngung');
        var nv = st.ds.find(function (x) { return x.maNv === ma; });
        Swal.fire({
            icon: 'warning',
            title: 'Ngừng đánh giá?',
            text: (nv ? nv.hoTen + ' (' + ma + ')' : ma) + ' sẽ ẩn khỏi danh sách. Phiếu đã có vẫn được giữ; thêm lại bằng mã NV để mở lại.',
            showCancelButton: true,
            confirmButtonText: 'Ngừng đánh giá',
            cancelButtonText: 'Huỷ',
            confirmButtonColor: '#dc2626'
        }).then(function (r) {
            if (!r.isConfirmed) return;
            nut.disabled = true;
            goiApi('/QLCongViec/DanhGia/QuanLy/Ngung', { maNv: ma })
                .then(function (kq) {
                    st.ds = st.ds.filter(function (x) { return x.maNv !== ma; });
                    veDanhSach();
                    Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1800, showConfirmButton: false });
                })
                .catch(function (err) { nut.disabled = false; Swal.fire({ icon: 'error', text: err.message }); });
        });
    }

    // ---------- Hồ sơ & KPI ----------
    function moHoSo(ma) {
        if (!st.modal) st.modal = new bootstrap.Modal($('qlnvModal'));
        st.hoSo = null;
        $('qlnvModalTen').textContent = ma;
        $('qlnvModalLoi').textContent = '';
        $('qlnvModalTai').hidden = false;
        $('qlnvModalTai').classList.remove('tdg-error');
        $('qlnvModalTai').textContent = 'Đang tải...';
        $('qlnvModalNoiDung').hidden = true;
        $('qlnvLuuHoSo').disabled = true;
        st.modal.show();

        goiApi('/QLCongViec/DanhGia/QuanLy/HoSo?maNv=' + encodeURIComponent(ma))
            .then(function (kq) {
                var d = kq.duLieu;
                st.hoSo = d;
                st.hoSo.chon = {};
                (d.dsKpiGiao || []).forEach(function (id) { st.hoSo.chon[id] = true; });
                $('qlnvModalTen').textContent = d.hoTen + ' (' + d.maNv + ')';

                var selCap = $('qlnvCapBac');
                selCap.textContent = '';
                var dsCap = (d.dsCapBac || []).slice();
                if (d.capBac && dsCap.indexOf(d.capBac) < 0) dsCap.push(d.capBac);
                dsCap.forEach(function (c) { selCap.appendChild(el('option', { value: c, text: c })); });
                selCap.value = d.capBac;
                st.dsCapMaTran = d.dsCapBac || [];
                dongThemCap();

                var selNhom = $('qlnvNhom');
                while (selNhom.options.length > 1) selNhom.remove(1);
                (d.dsNhomCongViec || []).forEach(function (n) { selNhom.appendChild(el('option', { value: n.id, text: n.ten })); });
                selNhom.value = d.idNhomCongViec || '';
                $('qlnvTatCaNhom').checked = !d.idNhomCongViec;

                veDsKpiChon();
                st.hoSo.dsTc = (d.dsTieuChi || []).map(function (t) { return { id: t.id, ten: t.ten, moTa: t.moTa || '', diemToiDa: t.diemToiDa }; });
                veDsTieuChi();
                $('qlnvModalTai').hidden = true;
                $('qlnvModalNoiDung').hidden = false;
                $('qlnvLuuHoSo').disabled = false;
            })
            .catch(function (e) {
                $('qlnvModalTai').classList.add('tdg-error');
                $('qlnvModalTai').textContent = e.message;
            });
    }

    // KPI mẫu của nhóm đang chọn + KPI đang giao (dù thuộc nhóm khác) để không “mất” khi đổi nhóm
    function veDsKpiChon() {
        var d = st.hoSo;
        if (!d) return;
        var nhom = $('qlnvNhom').value;
        var tatCa = $('qlnvTatCaNhom').checked || !nhom;
        var hop = $('qlnvDsKpi');
        hop.textContent = '';
        var ds = d.dsKpiMau.filter(function (k) { return tatCa || k.idNhom === nhom || d.chon[k.id]; });
        if (!ds.length) hop.appendChild(el('div', { class: 'tdg-empty', text: 'Nhóm công việc này chưa có KPI mẫu.' }));
        ds.forEach(function (k) {
            hop.appendChild(el('label', null, [
                el('input', { type: 'checkbox', class: 'form-check-input mt-1', 'data-kpi-chon': k.id, checked: !!d.chon[k.id] }),
                el('span', null, [k.congViec, k.kpiGoiY ? el('small', { text: k.kpiGoiY }) : null])
            ]));
        });
        $('qlnvSoKpi').textContent = Object.keys(d.chon).filter(function (id) { return d.chon[id]; }).length;
    }

    // ---------- Tiêu chí năng lực Part 2 (mẫu Excel 2025: tên, mô tả, điểm tối đa — tổng 50) ----------
    function veDsTieuChi() {
        var hop = $('qlnvDsTc');
        hop.textContent = '';
        if (!st.hoSo.dsTc.length) hop.appendChild(el('div', { class: 'tdg-empty', text: 'Chưa có tiêu chí. Bấm “Thêm tiêu chí” (vd 15 / 15 / 10 / 10 điểm).' }));
        st.hoSo.dsTc.forEach(function (t, i) {
            var moTa = el('textarea', { class: 'form-control', 'data-tc-truong': 'moTa', 'data-tc-i': i, placeholder: 'Mô tả / kiến thức, kỹ năng cần có', maxlength: 8000 });
            moTa.value = t.moTa || '';
            hop.appendChild(el('div', { class: 'tdg-hs-tc' }, [
                el('input', { type: 'text', class: 'form-control form-control-sm', 'data-tc-truong': 'ten', 'data-tc-i': i, value: t.ten || '', maxlength: 300, placeholder: 'Tên tiêu chí / nhiệm vụ' }),
                moTa,
                el('input', { type: 'number', class: 'form-control form-control-sm', 'data-tc-truong': 'diemToiDa', 'data-tc-i': i, min: 0.5, max: 50, step: 0.5, value: t.diemToiDa, title: 'Điểm tối đa' }),
                el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-tc-xoa': i, title: 'Bỏ tiêu chí' }, [el('i', { class: 'fa fa-trash' })])
            ]));
        });
        tinhTongTc();
    }

    function tinhTongTc() {
        var tong = st.hoSo.dsTc.reduce(function (s, t) { return s + so(t.diemToiDa); }, 0);
        var o = $('qlnvTongTc');
        o.textContent = fmt(tong);
        o.style.color = tong > 50 ? '#b91c1c' : '';
    }

    // ---------- Thêm nhanh cấp bậc (ô nhập ngay trong modal — SweetAlert trong modal Bootstrap bị giành focus) ----------
    function moThemCap() {
        var nguon = $('qlnvCapNguon');
        nguon.textContent = '';
        // Chỉ cấp bậc đã có ma trận mới sao chép được (cấp bậc lẻ của hồ sơ cũ thì không)
        (st.dsCapMaTran || []).forEach(function (c) { nguon.appendChild(el('option', { value: c, text: 'Sao chép từ ' + c })); });
        if ((st.dsCapMaTran || []).indexOf($('qlnvCapBac').value) >= 0) nguon.value = $('qlnvCapBac').value;
        $('qlnvCapMoi').value = '';
        $('qlnvCapLoi').textContent = '';
        $('qlnvThemCap').hidden = false;
        $('qlnvCapMoi').focus();
    }

    function dongThemCap() { $('qlnvThemCap').hidden = true; }

    function luuCapBac() {
        var ma = $('qlnvCapMoi').value.trim().toUpperCase();
        if (!ma) { $('qlnvCapLoi').textContent = 'Nhập mã cấp bậc.'; $('qlnvCapMoi').focus(); return; }
        var nut = $('qlnvLuuCap');
        nut.disabled = true;
        $('qlnvCapLoi').textContent = '';
        goiApi('/QLCongViec/DanhGia/QuanLy/ThemCapBac', { capBac: ma, saoChepTu: $('qlnvCapNguon').value })
            .then(function (kq) {
                var cap = kq.duLieu.capBac;
                var sel = $('qlnvCapBac');
                if (!sel.querySelector('option[value="' + CSS.escape(cap) + '"]')) sel.appendChild(el('option', { value: cap, text: cap }));
                if (st.dsCapMaTran.indexOf(cap) < 0) st.dsCapMaTran.push(cap);
                sel.value = cap;
                dongThemCap();
                Swal.fire({ icon: 'success', text: kq.thongBao + ' Bấm "Lưu" để gán cho nhân viên.', timer: 2200, showConfirmButton: false });
            })
            .catch(function (e) { $('qlnvCapLoi').textContent = e.message; })
            .finally(function () { nut.disabled = false; });
    }

    function luuHoSo() {
        var d = st.hoSo;
        if (!d) return;
        var tongTc = d.dsTc.reduce(function (s, t) { return s + so(t.diemToiDa); }, 0);
        if (d.dsTc.some(function (t) { return !String(t.ten || '').trim(); })) { $('qlnvModalLoi').textContent = 'Tiêu chí năng lực phải có tên.'; return; }
        if (tongTc > 50) { $('qlnvModalLoi').textContent = 'Tổng điểm tối đa tiêu chí năng lực vượt 50.'; return; }
        var nut = $('qlnvLuuHoSo');
        nut.disabled = true;
        $('qlnvModalLoi').textContent = '';
        goiApi('/QLCongViec/DanhGia/QuanLy/LuuHoSo', {
            maNv: d.maNv,
            capBac: $('qlnvCapBac').value,
            idNhomCongViec: $('qlnvNhom').value || null,
            dsKpiGiao: Object.keys(d.chon).filter(function (id) { return d.chon[id]; }),
            dsTieuChi: d.dsTc.map(function (t) { return { idTieuChi: t.id || null, ten: t.ten, moTa: t.moTa, diemToiDa: so(t.diemToiDa) }; })
        })
            .then(function (kq) {
                st.modal.hide();
                Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1500, showConfirmButton: false });
                return goiApi(KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/DanhSach')).then(function (k2) { st.ds = k2.duLieu || []; veDanhSach(); });
            })
            .catch(function (e) { $('qlnvModalLoi').textContent = e.message; })
            .finally(function () { nut.disabled = false; });
    }

    // ---------- Sự kiện ----------
    document.addEventListener('click', function (e) {
        var t = e.target;
        var sua = t.closest('[data-sua]');
        if (sua) { moHoSo(sua.getAttribute('data-sua')); return; }
        var ngung = t.closest('[data-ngung]');
        if (ngung) { ngungDanhGia(ngung); return; }
        if (t.closest('#qlnvMoThemCap')) { moThemCap(); return; }
        if (t.closest('#qlnvHuyCap')) { dongThemCap(); return; }
        if (t.closest('#qlnvLuuCap')) { luuCapBac(); return; }
        if (t.closest('#qlnvThemTc') && st.hoSo) {
            var conLai = 50 - st.hoSo.dsTc.reduce(function (s, x) { return s + so(x.diemToiDa); }, 0);
            st.hoSo.dsTc.push({ id: null, ten: '', moTa: '', diemToiDa: conLai > 0 ? Math.min(15, conLai) : 10 });
            veDsTieuChi();
            var o = document.querySelector('#qlnvDsTc [data-tc-i="' + (st.hoSo.dsTc.length - 1) + '"][data-tc-truong="ten"]');
            if (o) o.focus();
            return;
        }
        var xoaTc = t.closest('[data-tc-xoa]');
        if (xoaTc && st.hoSo) { st.hoSo.dsTc.splice(so(xoaTc.getAttribute('data-tc-xoa')), 1); veDsTieuChi(); return; }
        if (t.closest('#qlnvLuuHoSo')) luuHoSo();
    });

    // Enter trong ô mã cấp bậc mới = bấm "Thêm" (không để Enter lọt ra ngoài)
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && e.target.id === 'qlnvCapMoi') { e.preventDefault(); luuCapBac(); }
    });

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (t.matches('[data-kpi-chon]') && st.hoSo) {
            st.hoSo.chon[t.getAttribute('data-kpi-chon')] = t.checked;
            $('qlnvSoKpi').textContent = Object.keys(st.hoSo.chon).filter(function (id) { return st.hoSo.chon[id]; }).length;
            return;
        }
        if (t.id === 'qlnvNhom' || t.id === 'qlnvTatCaNhom') { veDsKpiChon(); return; }
        if (t.id === 'qlnvTrangThai' || t.id === 'qlnvXepLoai') veDanhSach();
    });

    document.addEventListener('input', function (e) {
        var t = e.target;
        if (t.id === 'qlnvTim') { veDanhSach(); return; }
        if (t.matches('[data-tc-truong]') && st.hoSo) {
            var tc = st.hoSo.dsTc[so(t.getAttribute('data-tc-i'))];
            if (!tc) return;
            tc[t.getAttribute('data-tc-truong')] = t.value;
            if (t.getAttribute('data-tc-truong') === 'diemToiDa') tinhTongTc();
        }
    });

    document.addEventListener('submit', function (e) {
        if (e.target.id === 'qlnvFormThem') themNhanVien(e);
    });

    function batDau() {
        KyDanhGia.khoiTao('qlnvKy', function () {
            $('qlnvExcelTatCa').href = KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/ExcelTatCa');
            taiDanhSach();
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', batDau);
    else batDau();
})();
