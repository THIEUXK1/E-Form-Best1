// Trang Tự đánh giá hiệu suất (/QLCongViec/DanhGia/TuDanhGia) theo mẫu "IT_Biểu đánh giá nhân viên 2025.xlsx".
// 5 bước: Thông tin → Part 1 (KPI, mức 1–5) → Part 2 (tiêu chí năng lực) → Part 3 (kế hoạch) → Hoàn thành.
// Lưu nháp mỗi lần bấm "Tiếp tục" từ bước 2 trở đi, nên đóng tab giữa chừng không mất phần đã nhập.
(function () {
    'use strict';

    var SO_BUOC = 5;
    var MUC = [1, 2, 3, 4, 5];
    var TEN_MUC = { 5: 'Excellent', 4: 'Good', 3: 'Standard', 2: 'Poor', 1: 'Very Poor' };
    // Khớp DuoiFileHopLe ở DanhGiaController
    var DUOI_HOP_LE = '.jpg,.jpeg,.png,.webp,.gif,.bmp,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.rar,.7z';
    var P3 = { diemManh: 'tdgDiemManh', diemYeu: 'tdgDiemYeu', loTrinh: 'tdgLoTrinh', keHoach: 'tdgKeHoach' };

    var st = {
        buoc: 1,
        duLieu: null,
        dsKpi: [],
        dsTieuChi: [],
        khoa: false,
        dangLuu: false
    };

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
    function fmt(n) { return (Math.round(n * 100) / 100).toString(); }
    function coGiaTri(v) { return v !== null && v !== undefined && v !== ''; }

    function goiApi(url, opt) {
        return fetch(url, opt).then(function (res) {
            if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
            if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
            return res.json();
        }).then(function (kq) {
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Thao tác thất bại.');
            return kq;
        });
    }

    function baoLoi(msg) { $('tdgLoi').textContent = msg || ''; }

    // ---------- Tải dữ liệu ----------
    function taiDuLieu() {
        var tt = $('tdgTrangThai');
        tt.hidden = false;
        tt.classList.remove('tdg-error');
        tt.textContent = 'Đang tải dữ liệu đánh giá...';
        $('tdgNoiDung').hidden = true;
        goiApi(KyDanhGia.thamSo('/QLCongViec/DanhGia/DuLieu'), { headers: { 'Accept': 'application/json' } })
            .then(function (kq) {
                var d = kq.duLieu;
                st.duLieu = d;
                st.dsKpi = d.dsKpi || [];
                st.dsTieuChi = d.dsTieuChi || [];
                st.khoa = !d.ky.dangMo || !!(d.phieu && d.phieu.trangThai === 'completed');
                veThongTin();
                veKpi();
                veTieuChi();
                veP3();
                SoLieuKpi.ve('tdgSoLieu', KyDanhGia.thamSo('/QLCongViec/DanhGia/SoLieuCuaToi'));
                KyXacNhan.ve('tdgKyXacNhan', { url: KyDanhGia.thamSo('/QLCongViec/DanhGia/KyXacNhanApi/CuaToi'), idKy: d.ky.idKy });
                $('tdgTrangThai').hidden = true;
                $('tdgNoiDung').hidden = false;
                chuyenBuoc(1);
            })
            .catch(function (e) {
                var tt = $('tdgTrangThai');
                tt.classList.add('tdg-error');
                tt.textContent = e.message;
            });
    }

    // ---------- Bước 1 ----------
    function veThongTin() {
        var t = st.duLieu.thongTin || {};
        $('tdgHoTen').value = t.hoTen || '';
        $('tdgMaNv').value = t.maNv || '';
        $('tdgChucVu').value = t.chucVu || '';
        $('tdgPhongBan').value = t.phongBan || '';
        $('tdgDaKhoa').hidden = !st.khoa;
        $('tdgDaKhoaChu').textContent = st.duLieu.ky.dangMo ? 'Phiếu đã được quản lý đánh giá xong — chỉ xem, không sửa được nữa.'
            : 'Kỳ “' + st.duLieu.ky.ten + '” đã đóng — chỉ xem.';
    }

    // ---------- Bước 2: Part 1 ----------
    function veKpi() {
        var hop = $('tdgDsKpi');
        hop.textContent = '';
        if (!st.dsKpi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Chưa có nhiệm vụ / KPI nào được phân công cho bạn.' }),
                el('small', { text: 'Có thể bấm “Tiếp tục” để sang Part 2. Liên hệ quản lý để được giao nhiệm vụ.' })
            ]));
        }
        st.dsKpi.forEach(function (k, i) { hop.appendChild(theKpi(k, i)); });
        tinhP1();
    }

    function nutMuc(thuocTinh, id, giaTri, khoa) {
        var hop = el('div', { class: 'tdg-muc' });
        MUC.forEach(function (m) {
            hop.appendChild(el('button', { type: 'button', title: TEN_MUC[m], 'data-muc': m, 'data-muc-cho': id, 'data-muc-loai': thuocTinh,
                class: m === giaTri ? 'chon' : '', disabled: khoa, text: String(m) }));
        });
        return hop;
    }

    function theKpi(k, i) {
        var id = k.idKpiMau;
        return el('div', { class: 'tdg-kpi', 'data-kpi': id }, [
            el('div', { class: 'tdg-kpi-head' }, [el('span', { text: k.congViec }), el('span', { class: 'tdg-kpi-stt', text: '#' + (i + 1) })]),
            el('div', { class: 'tdg-grid2' }, [
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Key Performance Index — Hiệu suất' }),
                    taoTextarea('chiSoKpi', k.chiSoKpi, k.kpiGoiY)]),
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Result — Kết quả' }),
                    taoTextarea('ketQua', k.ketQua, k.ketQuaGoiY)])
            ]),
            el('div', { class: 'tdg-kpi-row tdg-kpi-row3' }, [
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Weight — Trọng số' }),
                    el('input', { type: 'number', class: 'form-control', value: fmt(so(k.trongSo)), disabled: true })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Self-Evaluation — Mức' }),
                    nutMuc('tu', id, k.mucTuDanhGia, st.khoa),
                    el('div', { class: 'tdg-muc-diem', 'data-diem-kpi': id })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Minh chứng (ảnh / tài liệu)' }), vungAnh(k)])
            ])
        ]);
    }

    function taoTextarea(truong, giaTri, goiY) {
        var t = el('textarea', { class: 'form-control', 'data-truong': truong, placeholder: goiY || '', disabled: st.khoa });
        t.value = giaTri || '';
        return t;
    }

    function vungAnh(k) {
        var hop = el('div', { class: 'tdg-anh', 'data-anh': k.idKpiMau });
        var url = KyDanhGia.thamSo('/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?t=' + Date.now());
        if (k.coAnh) {
            if (k.laAnh) hop.appendChild(el('img', { src: url, alt: 'Ảnh minh chứng KPI', 'data-mo-anh': url }));
            else hop.appendChild(el('a', { href: url, target: '_blank', rel: 'noopener', class: 'btn btn-sm btn-outline-primary' },
                [el('i', { class: 'fa fa-file-text-o' }), ' Xem tài liệu minh chứng']));
            if (!st.khoa) hop.appendChild(el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', 'data-xoa-anh': k.idKpiMau, text: 'Gỡ' }));
        } else if (st.khoa) {
            hop.appendChild(el('span', { class: 'text-muted', text: 'Không có minh chứng' }));
        } else {
            // Ô dán: bấm vào ô rồi Ctrl+V (ảnh chụp màn hình hoặc file copy từ Explorer), kéo-thả, hoặc bấm để chọn file
            var input = el('input', { type: 'file', hidden: true, accept: DUOI_HOP_LE, 'data-tai-anh': k.idKpiMau });
            hop.appendChild(el('div', { class: 'tdg-dan', tabindex: 0, 'data-dan-file': k.idKpiMau }, [
                el('i', { class: 'fa fa-paperclip' }),
                el('span', { text: ' Bấm để chọn file, hoặc bấm vào đây rồi Ctrl+V / kéo-thả' })
            ]));
            hop.appendChild(input);
            hop.appendChild(el('small', { class: 'text-muted', text: 'Ảnh, PDF, Word, Excel, PowerPoint, TXT, ZIP — tối đa 15MB' }));
        }
        return hop;
    }

    function veLaiAnh(idKpi) {
        var k = st.dsKpi.find(function (x) { return x.idKpiMau === idKpi; });
        var cu = document.querySelector('[data-anh="' + idKpi + '"]');
        if (k && cu) cu.replaceWith(vungAnh(k));
    }

    function diemP1() {
        return st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo) * so(k.mucTuDanhGia) / 5; }, 0);
    }

    function tinhP1() {
        var tong = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0);
        $('tdgTongTrongSo').textContent = fmt(tong);
        st.dsKpi.forEach(function (k) {
            var o = document.querySelector('[data-diem-kpi="' + k.idKpiMau + '"]');
            if (!o) return;
            o.textContent = '';
            o.appendChild(document.createTextNode('Score: '));
            o.appendChild(el('b', { text: coGiaTri(k.mucTuDanhGia) ? fmt(so(k.trongSo) * k.mucTuDanhGia / 5) : '—' }));
        });
        $('tdgDiemP1').textContent = fmt(diemP1());
    }

    function datMuc(id, m) {
        var k = st.dsKpi.find(function (x) { return x.idKpiMau === id; });
        if (!k) return;
        k.mucTuDanhGia = m;
        document.querySelectorAll('[data-muc-cho="' + id + '"]').forEach(function (b) { b.classList.toggle('chon', so(b.getAttribute('data-muc')) === m); });
        tinhP1();
    }

    // Đọc lại chữ đang nhập trên DOM vào st.dsKpi (mức đã cập nhật ngay khi bấm)
    function docKpi() {
        st.dsKpi.forEach(function (k) {
            var the = document.querySelector('.tdg-kpi[data-kpi="' + k.idKpiMau + '"]');
            if (!the) return;
            k.chiSoKpi = the.querySelector('[data-truong="chiSoKpi"]').value;
            k.ketQua = the.querySelector('[data-truong="ketQua"]').value;
        });
    }

    function taiAnh(idKpi, file) {
        if (!file || st.khoa) return;
        var hop = document.querySelector('[data-anh="' + idKpi + '"]');
        var o = hop && hop.querySelector('[data-dan-file]');
        if (!o || o.classList.contains('dang-tai')) return;
        var duoi = (file.name.match(/\.[^.]+$/) || [''])[0].toLowerCase();
        if (DUOI_HOP_LE.split(',').indexOf(duoi) < 0) { Swal.fire({ icon: 'warning', text: 'Định dạng ' + (duoi || 'này') + ' không được nhận.' }); return; }
        if (file.size > 15 * 1024 * 1024) { Swal.fire({ icon: 'warning', text: 'File vượt quá 15MB.' }); return; }
        var fd = new FormData();
        fd.append('file', file);
        o.classList.add('dang-tai');
        var nhan = o.querySelector('span');
        var cu = nhan.textContent;
        nhan.textContent = ' Đang tải ' + file.name + '...';
        goiApi('/QLCongViec/DanhGia/AnhKpi/' + idKpi, { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token() } })
            .then(function (kq) {
                var k = st.dsKpi.find(function (x) { return x.idKpiMau === idKpi; });
                if (k) { k.coAnh = true; k.laAnh = !!(kq.duLieu && kq.duLieu.laAnh); }
                veLaiAnh(idKpi);
            })
            .catch(function (e) {
                o.classList.remove('dang-tai');
                nhan.textContent = cu;
                Swal.fire({ icon: 'error', text: e.message });
            });
    }

    // Ảnh chụp màn hình dán vào thường tên "image.png" -> đặt tên rõ hơn, server vẫn tự sinh tên lưu
    function fileTuClipboard(dt) {
        if (!dt) return null;
        var f = dt.files && dt.files[0];
        if (!f && dt.items) {
            for (var i = 0; i < dt.items.length; i++) {
                if (dt.items[i].kind === 'file') { f = dt.items[i].getAsFile(); break; }
            }
        }
        if (f && !/\.[^.]+$/.test(f.name || '')) {
            var duoi = (f.type.split('/')[1] || 'png').replace('jpeg', 'jpg');
            f = new File([f], 'dan-' + Date.now() + '.' + duoi, { type: f.type });
        }
        return f;
    }

    function goAnh(nut) {
        var idKpi = nut.getAttribute('data-xoa-anh');
        nut.disabled = true;
        goiApi('/QLCongViec/DanhGia/XoaAnhKpi/' + idKpi, { method: 'POST', headers: { 'RequestVerificationToken': token() } })
            .then(function () {
                var k = st.dsKpi.find(function (x) { return x.idKpiMau === idKpi; });
                if (k) k.coAnh = false;
                veLaiAnh(idKpi);
            })
            .catch(function (e) { nut.disabled = false; Swal.fire({ icon: 'error', text: e.message }); });
    }

    // ---------- Bước 3: Part 2 ----------
    function veTieuChi() {
        var hop = $('tdgDsTieuChi');
        hop.textContent = '';
        if (!st.dsTieuChi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Chưa có tiêu chí năng lực nào cho bạn.' }),
                el('small', { text: 'Quản lý đặt tiêu chí ở “Hồ sơ & KPI”. Có thể bấm “Tiếp tục” sang Part 3.' })
            ]));
        }
        st.dsTieuChi.forEach(function (t) {
            var o = el('input', { type: 'number', class: 'form-control form-control-sm', min: 0, max: t.diemToiDa, step: 0.5,
                'data-tc-diem': t.idTieuChi, value: coGiaTri(t.diem) ? fmt(so(t.diem)) : '', disabled: st.khoa });
            var tomTat = el('textarea', { class: 'form-control', 'data-tc-tomtat': t.idTieuChi, maxlength: 8000,
                placeholder: 'Tóm tắt và một vài ví dụ cụ thể...', disabled: st.khoa });
            tomTat.value = t.tomTat || '';
            hop.appendChild(el('div', { class: 'tdg-tc' }, [
                el('div', { class: 'tdg-tc-head' }, [el('span', { text: t.ten }), el('span', { class: 'tdg-tc-max', text: 'Tối đa ' + fmt(so(t.diemToiDa)) + ' điểm' })]),
                t.moTa ? el('div', { class: 'tdg-tc-mota', text: t.moTa }) : null,
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Summary and some examples — Tóm tắt / ví dụ' }), tomTat]),
                el('div', { class: 'tdg-tc-diem' }, [el('span', { text: 'Self-Evaluation:' }), o, el('span', { class: 'text-muted', text: '/ ' + fmt(so(t.diemToiDa)) })])
            ]));
        });
        $('tdgToiDaP2').textContent = fmt(st.dsTieuChi.reduce(function (s, t) { return s + so(t.diemToiDa); }, 0) || 50);
        tinhP2();
    }

    function diemP2() { return st.dsTieuChi.reduce(function (s, t) { return s + so(t.diem); }, 0); }
    function tinhP2() { $('tdgDiemP2').textContent = fmt(diemP2()); }

    function docTieuChi() {
        st.dsTieuChi.forEach(function (t) {
            var o = document.querySelector('[data-tc-diem="' + t.idTieuChi + '"]');
            var tt = document.querySelector('[data-tc-tomtat="' + t.idTieuChi + '"]');
            if (o) t.diem = o.value === '' ? null : so(o.value);
            if (tt) t.tomTat = tt.value;
        });
    }

    // ---------- Bước 4: Part 3 ----------
    function veP3() {
        var p = st.duLieu.phieu || {};
        Object.keys(P3).forEach(function (k) { var o = $(P3[k]); o.value = p[k] || ''; o.disabled = st.khoa; });
    }

    // ---------- Bước 5 ----------
    function xepLoai(chenh) {
        if (chenh >= 20) return 'Vượt chuẩn';
        if (chenh >= 0) return 'Đạt chuẩn';
        if (chenh >= -20) return 'Cần cải thiện';
        return 'Chưa đạt';
    }

    function veTomTat() {
        var p1 = diemP1(), p2 = diemP2(), tong = p1 + p2;
        $('tdgTkP1').textContent = fmt(p1) + ' / 50';
        $('tdgTkP2').textContent = fmt(p2) + ' / 50';
        $('tdgTkTong').textContent = fmt(tong);
        $('tdgXepLoai').textContent = xepLoai(tong - 60);
        var p = st.duLieu.phieu;
        $('tdgDaNop').hidden = !(p && p.ngayNop);
        if (p && p.ngayNop) $('tdgNgayNop').textContent = 'lúc ' + new Date(p.ngayNop).toLocaleString('vi-VN');

        var thieu = [];
        var soKpiThieu = st.dsKpi.filter(function (k) { return !coGiaTri(k.mucTuDanhGia); }).length;
        var soTcThieu = st.dsTieuChi.filter(function (t) { return !coGiaTri(t.diem); }).length;
        if (soKpiThieu) thieu.push(soKpiThieu + ' nhiệm vụ Part 1 chưa chọn mức');
        if (soTcThieu) thieu.push(soTcThieu + ' tiêu chí Part 2 chưa chấm');
        $('tdgThieu').hidden = !thieu.length || st.khoa;
        $('tdgThieuChu').textContent = 'Cần hoàn tất trước khi gửi: ' + thieu.join('; ') + '.';
    }

    // ---------- Điều hướng bước + lưu ----------
    function chuyenBuoc(b) {
        st.buoc = b;
        baoLoi('');
        document.querySelectorAll('#tdg [data-panel]').forEach(function (p) { p.hidden = so(p.getAttribute('data-panel')) !== b; });
        document.querySelectorAll('#tdgSteps li').forEach(function (li) {
            var n = so(li.getAttribute('data-buoc'));
            li.classList.toggle('dat', n <= b);
            li.classList.toggle('qua', n < b);
        });
        if (b === SO_BUOC) veTomTat();
        $('tdgQuayLai').disabled = b === 1;
        var nut = $('tdgTiepTuc');
        nut.textContent = '';
        if (b === SO_BUOC) {
            nut.appendChild(document.createTextNode(st.khoa ? 'Đã khoá ' : 'Hoàn thành '));
            nut.appendChild(el('i', { class: st.khoa ? 'fa fa-lock' : 'fa fa-paper-plane' }));
            nut.disabled = st.khoa;
        } else {
            nut.appendChild(document.createTextNode('Tiếp tục '));
            nut.appendChild(el('i', { class: 'fa fa-arrow-right' }));
            nut.disabled = false;
        }
        // Khi trang bị khoá cuộn thì window không cuộn được: đưa vùng nội dung về đầu
        var vung = document.querySelector('.tdg-cuon');
        if (vung && vung.scrollHeight > vung.clientHeight) vung.scrollTo({ top: 0, behavior: 'smooth' });
        else window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function docTatCa() { docKpi(); docTieuChi(); }

    function goiLuu(hoanThanh) {
        docTatCa();
        var body = {
            dsKpi: st.dsKpi.map(function (k) {
                return { idKpiMau: k.idKpiMau, chiSoKpi: k.chiSoKpi, ketQua: k.ketQua, mucTuDanhGia: coGiaTri(k.mucTuDanhGia) ? k.mucTuDanhGia : null };
            }),
            dsTieuChi: st.dsTieuChi.map(function (t) { return { idTieuChi: t.idTieuChi, tomTat: t.tomTat, diem: t.diem }; }),
            idKy: st.duLieu.ky.idKy,
            hoanThanh: !!hoanThanh
        };
        Object.keys(P3).forEach(function (k) { body[k] = $(P3[k]).value; });
        return goiApi('/QLCongViec/DanhGia/Luu', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify(body)
        }).then(function (kq) {
            if (kq.duLieu) st.duLieu.phieu = kq.duLieu;
            return kq;
        });
    }

    function tiepTuc() {
        if (st.dangLuu) return;
        var b = st.buoc;
        // Bước 1 chỉ xem; phiếu đã khoá thì không lưu nữa
        if (b === 1 || st.khoa) { if (b < SO_BUOC) chuyenBuoc(b + 1); return; }

        if (b === 3) {
            docTieuChi();
            var sai = st.dsTieuChi.filter(function (t) { return coGiaTri(t.diem) && (t.diem < 0 || t.diem > so(t.diemToiDa)); })[0];
            if (sai) { baoLoi('Điểm “' + sai.ten + '” phải trong khoảng 0–' + fmt(so(sai.diemToiDa)) + '.'); return; }
        }

        var nut = $('tdgTiepTuc');
        var noiDungCu = Array.prototype.slice.call(nut.childNodes);
        st.dangLuu = true;
        nut.disabled = true;
        nut.textContent = '';
        nut.appendChild(el('i', { class: 'fa fa-spinner fa-spin' }));
        nut.appendChild(document.createTextNode(' Đang lưu...'));
        baoLoi('');

        goiLuu(b === SO_BUOC)
            .then(function (kq) {
                st.dangLuu = false;
                if (b === SO_BUOC) {
                    chuyenBuoc(SO_BUOC);
                    Swal.fire({ icon: 'success', title: kq.thongBao, text: 'Dữ liệu đã được chuyển cho quản lý xem xét.', timer: 2500, showConfirmButton: false });
                } else {
                    chuyenBuoc(b + 1);
                }
            })
            .catch(function (e) {
                // Giữ nguyên dữ liệu đã nhập, chỉ báo lỗi tại chỗ
                st.dangLuu = false;
                nut.textContent = '';
                noiDungCu.forEach(function (n) { nut.appendChild(n); });
                nut.disabled = false;
                baoLoi(e.message);
            });
    }

    // ---------- Sự kiện (delegation trên document) ----------
    document.addEventListener('click', function (e) {
        var t = e.target;
        if (t.closest('#tdgTiepTuc')) { e.preventDefault(); tiepTuc(); return; }
        if (t.closest('#tdgQuayLai')) { e.preventDefault(); if (st.buoc > 1 && !st.dangLuu) { docTatCa(); chuyenBuoc(st.buoc - 1); } return; }
        var muc = t.closest('#tdg [data-muc]');
        if (muc && !st.khoa) { datMuc(muc.getAttribute('data-muc-cho'), so(muc.getAttribute('data-muc'))); return; }
        var xoa = t.closest('[data-xoa-anh]');
        if (xoa) { goAnh(xoa); return; }
        var dan = t.closest('[data-dan-file]');
        if (dan && !dan.classList.contains('dang-tai')) {
            var chon = dan.parentNode.querySelector('[data-tai-anh]');
            if (chon) chon.click();
            return;
        }
        var anh = t.closest('[data-mo-anh]');
        if (anh) window.open(anh.getAttribute('data-mo-anh'), '_blank', 'noopener');
    });

    document.addEventListener('input', function (e) {
        if (e.target.matches('[data-tc-diem]')) { docTieuChi(); tinhP2(); }
    });

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (t.matches('[data-tai-anh]')) {
            var f = t.files && t.files[0];
            t.value = '';
            taiAnh(t.getAttribute('data-tai-anh'), f);
        }
    });

    // Ctrl+V khi ô dán đang được chọn (focus)
    document.addEventListener('paste', function (e) {
        var o = e.target.closest && e.target.closest('[data-dan-file]');
        if (!o) return;
        var f = fileTuClipboard(e.clipboardData);
        if (!f) { Swal.fire({ icon: 'info', text: 'Clipboard không có ảnh hoặc file. Hãy chụp màn hình hoặc copy file rồi dán lại.' }); return; }
        e.preventDefault();
        taiAnh(o.getAttribute('data-dan-file'), f);
    });

    document.addEventListener('dragover', function (e) {
        var o = e.target.closest && e.target.closest('[data-dan-file]');
        if (!o) return;
        e.preventDefault();
        o.classList.add('keo');
    });
    document.addEventListener('dragleave', function (e) {
        var o = e.target.closest && e.target.closest('[data-dan-file]');
        if (o) o.classList.remove('keo');
    });
    document.addEventListener('drop', function (e) {
        var o = e.target.closest && e.target.closest('[data-dan-file]');
        if (!o) return;
        e.preventDefault();
        o.classList.remove('keo');
        taiAnh(o.getAttribute('data-dan-file'), fileTuClipboard(e.dataTransfer));
    });

    function batDau() { KyDanhGia.khoiTao('tdgKy', taiDuLieu); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', batDau);
    else batDau();
})();
