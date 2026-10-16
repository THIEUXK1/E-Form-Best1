// Trang Tự đánh giá hiệu suất (/QLCongViec/DanhGia/TuDanhGia).
// 4 bước giống app evaluation cũ: Thông tin → KPI → Năng lực → Hoàn thành.
// Khác app cũ: lưu nháp mỗi lần bấm "Tiếp tục" ở bước KPI/Năng lực (app cũ chỉ lưu ở bước cuối
// dù hướng dẫn ghi "lưu tự động"), nên đóng tab giữa chừng không mất phần đã nhập.
(function () {
    'use strict';

    var SO_BUOC = 4;
    var DIEM_NHANH = [0, 2, 4, 6, 8, 10];
    // Khớp DuoiFileHopLe ở DanhGiaController
    var DUOI_HOP_LE = '.jpg,.jpeg,.png,.webp,.gif,.bmp,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.rar,.7z';

    var st = {
        buoc: 1,
        duLieu: null,
        diem: {},          // idNangLuc -> điểm tự chấm
        dsNhom: [],        // nhóm năng lực đang hiển thị theo bộ lọc
        dsKpi: [],
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
        goiApi('/QLCongViec/DanhGia/DuLieu', { headers: { 'Accept': 'application/json' } })
            .then(function (kq) {
                var d = kq.duLieu;
                st.duLieu = d;
                st.dsKpi = d.dsKpi || [];
                st.dsNhom = d.dsNhomNangLuc || [];
                st.diem = {};
                if (d.phieu && d.phieu.diemTheoNangLuc) {
                    Object.keys(d.phieu.diemTheoNangLuc).forEach(function (k) { st.diem[k.toLowerCase()] = so(d.phieu.diemTheoNangLuc[k]); });
                }
                st.khoa = !!(d.phieu && d.phieu.trangThai === 'completed');
                veThongTin();
                veBoLoc();
                veKpi();
                veNangLuc();
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
        var d = st.duLieu;
        $('tdgHoTen').value = d.hoTen || '';
        $('tdgMaNv').value = d.maNv || '';
        $('tdgPhongBan').value = d.phongBan || '';
        $('tdgCapBacHoSo').value = d.hoSo.capBac || '';
        $('tdgNhomHoSo').value = d.hoSo.tenNhomCongViec || 'Chưa cập nhật';
        $('tdgCanhBao').hidden = !!d.hoSo.dayDu;
        $('tdgDaKhoa').hidden = !st.khoa;
    }

    // ---------- Bước 2: KPI ----------
    function veKpi() {
        var hop = $('tdgDsKpi');
        hop.textContent = '';
        if (!st.dsKpi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Chưa có KPI nào được phân công cho bạn.' }),
                el('small', { text: 'Có thể bấm “Tiếp tục” để sang phần đánh giá năng lực. Liên hệ quản lý để được giao KPI.' })
            ]));
        }
        st.dsKpi.forEach(function (k, i) { hop.appendChild(theKpi(k, i)); });
        tinhTrongSo();
    }

    function theKpi(k, i) {
        var id = k.idKpiMau;
        return el('div', { class: 'tdg-kpi', 'data-kpi': id }, [
            el('div', { class: 'tdg-kpi-head' }, [el('span', { text: k.congViec }), el('span', { class: 'tdg-kpi-stt', text: '#' + (i + 1) })]),
            el('div', { class: 'tdg-grid2' }, [
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Chỉ số hiệu suất (KPI)' }),
                    taoTextarea('chiSoKpi', k.chiSoKpi, k.kpiGoiY)]),
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Kết quả' }),
                    taoTextarea('ketQua', k.ketQua, k.ketQuaGoiY)])
            ]),
            el('div', { class: 'tdg-kpi-row' }, [
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Trọng số (quản lý điền)' }),
                    el('input', { type: 'number', class: 'form-control', value: fmt(so(k.trongSo)), disabled: true })]),
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Tự đánh giá (0–120%)' }),
                    el('input', { type: 'number', class: 'form-control', min: 0, max: 120, step: 1, 'data-truong': 'diemTuDanhGia',
                        value: k.diemTuDanhGia === null || k.diemTuDanhGia === undefined ? '' : fmt(so(k.diemTuDanhGia)), disabled: st.khoa })]),
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
        var url = '/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?t=' + Date.now();
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

    function tinhTrongSo() {
        var tong = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0);
        $('tdgTongTrongSo').textContent = fmt(tong);
    }

    // Đọc lại giá trị đang nhập trên DOM vào st.dsKpi
    function docKpi() {
        st.dsKpi.forEach(function (k) {
            var the = document.querySelector('.tdg-kpi[data-kpi="' + k.idKpiMau + '"]');
            if (!the) return;
            k.chiSoKpi = the.querySelector('[data-truong="chiSoKpi"]').value;
            k.ketQua = the.querySelector('[data-truong="ketQua"]').value;
            var d = the.querySelector('[data-truong="diemTuDanhGia"]').value;
            k.diemTuDanhGia = d === '' ? null : so(d);
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

    // ---------- Bước 3: năng lực ----------
    function veBoLoc() {
        var d = st.duLieu;
        var selCap = $('tdgCapBac');
        selCap.textContent = '';
        var dsCap = (d.dsCapBac || []).slice();
        if (d.boLoc.capBac && dsCap.indexOf(d.boLoc.capBac) < 0) dsCap.push(d.boLoc.capBac);
        dsCap.forEach(function (c) { selCap.appendChild(el('option', { value: c, text: c })); });
        selCap.value = d.boLoc.capBac || '';

        var selNhom = $('tdgNhomCongViec');
        while (selNhom.options.length > 1) selNhom.remove(1);
        (d.dsNhomCongViec || []).forEach(function (n) { selNhom.appendChild(el('option', { value: n.id, text: n.ten })); });
        selNhom.value = d.boLoc.idNhomCongViec || '';

        selCap.disabled = selNhom.disabled = st.khoa;
    }

    function doiBoLoc() {
        var cap = $('tdgCapBac').value;
        var nhom = $('tdgNhomCongViec').value;
        var hop = $('tdgDsNhomNangLuc');
        hop.textContent = '';
        hop.appendChild(el('div', { class: 'tdg-loading' }, [el('i', { class: 'fa fa-spinner fa-spin' }), ' Đang tải danh sách năng lực...']));
        var url = '/QLCongViec/DanhGia/NangLuc?capBac=' + encodeURIComponent(cap) + (nhom ? '&idNhomCongViec=' + encodeURIComponent(nhom) : '');
        goiApi(url, { headers: { 'Accept': 'application/json' } })
            .then(function (kq) { st.dsNhom = kq.duLieu || []; veNangLuc(); })
            .catch(function (e) {
                hop.textContent = '';
                hop.appendChild(el('div', { class: 'tdg-empty', text: e.message }));
            });
    }

    function lopGap(g) { return g > 0 ? 'duong' : (g < 0 ? 'am' : 'bang'); }
    function chuGap(g) { return 'GAP: ' + (g > 0 ? '+' : '') + fmt(g); }

    function veNangLuc() {
        var hop = $('tdgDsNhomNangLuc');
        hop.textContent = '';
        if (!st.dsNhom.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Không có năng lực nào cho bộ lọc hiện tại.' }),
                el('small', { text: 'Thử đổi cấp bậc hoặc nhóm công việc.' })
            ]));
        }
        st.dsNhom.forEach(function (g) {
            var than = el('div', { class: 'tdg-group-body' });
            g.dsNangLuc.forEach(function (n) { than.appendChild(dongNangLuc(n)); });
            hop.appendChild(el('div', { class: 'tdg-group', 'data-nhom': g.idNhom }, [
                el('button', { type: 'button', class: 'tdg-group-head', 'data-mo-nhom': g.idNhom }, [
                    el('span', null, [g.ten + ' ', el('small', { 'data-tong-nhom': g.idNhom })]),
                    el('i', { class: 'fa fa-chevron-up' })
                ]),
                than
            ]));
            capNhatNhom(g);
        });
        capNhatTong();
    }

    function dongNangLuc(n) {
        var id = n.idNangLuc.toLowerCase();
        var diem = st.diem[id] || 0;
        var nhanh = el('div', { class: 'tdg-nhanh' });
        DIEM_NHANH.forEach(function (v) {
            nhanh.appendChild(el('button', { type: 'button', 'data-diem-nhanh': v, 'data-nl': id, class: v === diem ? 'chon' : '', disabled: st.khoa, text: String(v) }));
        });
        var gap = diem - so(n.diemYeuCau);
        return el('div', { class: 'tdg-nl', 'data-nl-dong': id }, [
            el('div', { class: 'tdg-nl-info' }, [
                el('div', { class: 'tdg-nl-ten', title: n.moTa || '' }, [el('b', { text: n.ma }), n.ten]),
                el('div', { class: 'tdg-nl-meta' }, ['Yêu cầu: ' + fmt(so(n.diemYeuCau)),
                    el('span', { class: 'tdg-gap ' + lopGap(gap), 'data-gap': id, text: chuGap(gap) })])
            ]),
            el('div', { class: 'tdg-nl-diem' }, [
                el('input', { type: 'number', class: 'form-control form-control-sm', min: 0, max: 10, step: 0.1, value: fmt(diem), 'data-nl': id, disabled: st.khoa }),
                nhanh
            ])
        ]);
    }

    function timNangLuc(id) {
        for (var i = 0; i < st.dsNhom.length; i++) {
            var n = st.dsNhom[i].dsNangLuc.find(function (x) { return x.idNangLuc.toLowerCase() === id; });
            if (n) return { nhom: st.dsNhom[i], nl: n };
        }
        return null;
    }

    function datDiem(id, v, tuO) {
        v = Math.max(0, Math.min(10, so(v)));
        st.diem[id] = v;
        var dong = document.querySelector('[data-nl-dong="' + id + '"]');
        var tim = timNangLuc(id);
        if (!dong || !tim) return;
        if (!tuO) dong.querySelector('input[data-nl]').value = fmt(v);
        dong.querySelectorAll('[data-diem-nhanh]').forEach(function (b) { b.classList.toggle('chon', so(b.getAttribute('data-diem-nhanh')) === v); });
        var gap = v - so(tim.nl.diemYeuCau);
        var o = dong.querySelector('[data-gap]');
        o.className = 'tdg-gap ' + lopGap(gap);
        o.textContent = chuGap(gap);
        capNhatNhom(tim.nhom);
        capNhatTong();
    }

    function capNhatNhom(g) {
        var thuc = 0, yc = 0;
        g.dsNangLuc.forEach(function (n) { thuc += st.diem[n.idNangLuc.toLowerCase()] || 0; yc += so(n.diemYeuCau); });
        var o = document.querySelector('[data-tong-nhom="' + g.idNhom + '"]');
        if (o) o.textContent = '• ' + g.dsNangLuc.length + ' năng lực • ' + fmt(thuc) + '/' + fmt(yc) + ' • ' + chuGap(thuc - yc);
    }

    function tongNangLuc() {
        var thuc = 0, yc = 0, soDaCham = 0;
        st.dsNhom.forEach(function (g) {
            g.dsNangLuc.forEach(function (n) {
                var id = n.idNangLuc.toLowerCase();
                thuc += st.diem[id] || 0;
                yc += so(n.diemYeuCau);
                if (st.diem[id] !== undefined) soDaCham++;
            });
        });
        return { thuc: thuc, yc: yc, soDaCham: soDaCham };
    }

    function capNhatTong() {
        var t = tongNangLuc();
        $('tdgTongNangLuc').textContent = 'Tổng điểm: ' + fmt(t.thuc) + ' / yêu cầu ' + fmt(t.yc) + ' — ' + chuGap(t.thuc - t.yc);
    }

    // ---------- Bước 4 ----------
    function xepLoai(gap) {
        if (gap >= 1) return 'Vượt chuẩn';
        if (gap >= 0) return 'Đạt chuẩn';
        if (gap >= -1) return 'Cần cải thiện';
        return 'Chưa đạt';
    }

    function veTomTat() {
        var t = tongNangLuc();
        var diemKpi = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo) * so(k.diemTuDanhGia) / 100; }, 0);
        var gap = diemKpi + t.thuc - t.yc;
        $('tdgSoKpi').textContent = st.dsKpi.length;
        $('tdgSoNangLuc').textContent = t.soDaCham;
        $('tdgDiemNangLuc').textContent = fmt(t.thuc) + ' / ' + fmt(t.yc);
        $('tdgGap').textContent = (gap > 0 ? '+' : '') + fmt(gap);
        $('tdgXepLoai').textContent = xepLoai(gap);
        var p = st.duLieu.phieu;
        $('tdgDaNop').hidden = !(p && p.ngayNop);
        if (p && p.ngayNop) $('tdgNgayNop').textContent = 'lúc ' + new Date(p.ngayNop).toLocaleString('vi-VN');
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
        if (b === 4) veTomTat();
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

    function goiLuu(hoanThanh) {
        docKpi();
        var dsNangLuc = [];
        st.dsNhom.forEach(function (g) {
            g.dsNangLuc.forEach(function (n) {
                var id = n.idNangLuc.toLowerCase();
                if (st.diem[id] !== undefined) dsNangLuc.push({ idNangLuc: n.idNangLuc, diem: st.diem[id] });
            });
        });
        var body = {
            capBac: $('tdgCapBac').value,
            idNhomCongViec: $('tdgNhomCongViec').value || null,
            dsNangLuc: dsNangLuc,
            dsKpi: st.dsKpi.map(function (k) {
                return { idKpiMau: k.idKpiMau, chiSoKpi: k.chiSoKpi, ketQua: k.ketQua, diemTuDanhGia: k.diemTuDanhGia };
            }),
            hoanThanh: !!hoanThanh
        };
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

        if (b === 2) {
            docKpi();
            var sai = st.dsKpi.some(function (k) { return k.diemTuDanhGia !== null && (k.diemTuDanhGia < 0 || k.diemTuDanhGia > 120); });
            if (sai) { baoLoi('Tự đánh giá KPI phải trong khoảng 0–120%.'); return; }
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
        if (t.closest('#tdgQuayLai')) { e.preventDefault(); if (st.buoc > 1 && !st.dangLuu) { docKpi(); chuyenBuoc(st.buoc - 1); } return; }
        var nhanh = t.closest('[data-diem-nhanh]');
        if (nhanh && !st.khoa) { datDiem(nhanh.getAttribute('data-nl'), nhanh.getAttribute('data-diem-nhanh')); return; }
        var moNhom = t.closest('[data-mo-nhom]');
        if (moNhom) { moNhom.parentNode.classList.toggle('dong'); return; }
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
        var o = e.target.closest('input[data-nl]');
        if (o && !st.khoa) datDiem(o.getAttribute('data-nl'), o.value, true);
    });

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (t.matches('input[data-nl]')) { datDiem(t.getAttribute('data-nl'), t.value); return; }
        if (t.matches('[data-tai-anh]')) {
            var f = t.files && t.files[0];
            t.value = '';
            taiAnh(t.getAttribute('data-tai-anh'), f);
            return;
        }
        if (t.id === 'tdgCapBac' || t.id === 'tdgNhomCongViec') doiBoLoc();
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

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', taiDuLieu);
    else taiDuLieu();
})();
