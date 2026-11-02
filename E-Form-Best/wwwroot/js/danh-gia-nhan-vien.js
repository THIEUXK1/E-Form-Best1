// Trang Đánh giá nhân viên (/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=...) — chấm theo mẫu "IT_Biểu đánh giá nhân viên 2025.xlsx":
// Part 1 trọng số (tổng 50) + mức 1–5, Part 2 điểm từng tiêu chí, Part 3 nhận xét. Hiện song song phần nhân viên tự khai
// để quản lý so sánh; đổi nhân viên không tải lại trang, chỉ đổi ?maNv trên URL.
(function () {
    'use strict';

    var SO_BUOC = 5;
    var MUC = [1, 2, 3, 4, 5];
    var TEN_MUC = { 5: 'Excellent', 4: 'Good', 3: 'Standard', 2: 'Poor', 1: 'Very Poor' };

    var st = { buoc: 1, ma: '', d: null, dsKpi: [], dsTieuChi: [], chiXem: false, dangLuu: false, ds: [] };

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

    function baoLoi(m) { $('dgnvLoi').textContent = m || ''; }

    function trangThaiTai(msg, loi) {
        var o = $('dgnvTrangThai');
        o.hidden = !msg;
        o.classList.toggle('tdg-error', !!loi);
        o.textContent = '';
        if (msg && !loi) o.appendChild(el('i', { class: 'fa fa-spinner fa-spin' }));
        if (msg) o.appendChild(document.createTextNode(' ' + msg));
    }

    // ---------- Chọn nhân viên ----------
    function taiDsNhanVien() {
        var maUrl = new URLSearchParams(location.search).get('maNv') || '';
        goiApi('/QLCongViec/DanhGia/QuanLy/DanhSach')
            .then(function (kq) {
                var sel = $('dgnvChon');
                sel.textContent = '';
                sel.appendChild(el('option', { value: '', text: '— Chọn nhân viên để đánh giá —' }));
                st.ds = kq.duLieu || [];
                st.ds.forEach(function (nv) {
                    sel.appendChild(el('option', { value: nv.maNv, text: nhanOption(nv) }));
                });
                veDsCho();
                if (maUrl) {
                    sel.value = maUrl;
                    if (sel.value) taiNhanVien(maUrl, false);
                    else trangThaiTai(maUrl + ' không có trong danh sách đánh giá.', true);
                }
            })
            .catch(function (e) { trangThaiTai(e.message, true); });
    }

    var NHAN_TT = { completed: 'Hoàn thành', awaiting_manager: 'Chờ quản lý', awaiting_self: 'Chờ tự đánh giá', not_started: 'Chưa đánh giá' };

    function nhanOption(nv) { return nv.maNv + ' — ' + nv.hoTen + ' (' + (NHAN_TT[nv.trangThai] || '') + ')'; }

    // Danh sách người quản lý chưa gửi đánh giá (Chờ quản lý + Chưa đánh giá); chỉ hiện khi chưa chọn nhân viên
    function veDsCho() {
        var cho = st.ds.filter(function (nv) { return !nv.daQuanLyDanhGia; })
            .sort(function (a, b) {
                var ua = a.trangThai === 'awaiting_manager' ? 0 : 1, ub = b.trangThai === 'awaiting_manager' ? 0 : 1;
                return ua - ub || String(a.maNv).localeCompare(String(b.maNv));
            });
        $('dgnvSoCho').textContent = cho.length;
        var tb = $('dgnvDsCho');
        tb.textContent = '';
        if (!cho.length) {
            tb.appendChild(el('tr', null, [el('td', { colspan: '4', class: 'tdg-cho-trong', text: 'Không còn nhân viên nào cần quản lý đánh giá.' })]));
        }
        cho.forEach(function (nv) {
            tb.appendChild(el('tr', { 'data-chon-nv': nv.maNv }, [
                el('td', null, [el('b', { text: nv.maNv }), ' — ' + (nv.hoTen || '')]),
                el('td', { text: nv.phongBan || '' }),
                el('td', null, [el('span', { class: 'tdg-badge ' + nv.trangThai, text: NHAN_TT[nv.trangThai] || '' })]),
                el('td', null, [el('span', { class: 'btn btn-sm btn-primary' }, [el('i', { class: 'fa fa-pencil-square-o' }), ' Đánh giá'])])
            ]));
        });
        $('dgnvCho').hidden = !!st.ma;
    }

    // Cập nhật trạng thái 1 người trong danh sách sau khi lưu / mở lại phiếu (không gọi lại API danh sách)
    function capNhatTrangThaiDs() {
        var nv = st.ds.filter(function (x) { return x.maNv === st.ma; })[0];
        if (!nv || !st.d) return;
        var daTu = !!(st.d.phieuTu && st.d.phieuTu.ngayNop), daQl = !!(st.d.phieuQuanLy && st.d.phieuQuanLy.ngayNop);
        nv.daTuDanhGia = daTu;
        nv.daQuanLyDanhGia = daQl;
        nv.trangThai = daTu && daQl ? 'completed' : daTu ? 'awaiting_manager' : daQl ? 'awaiting_self' : 'not_started';
        var opt = $('dgnvChon').querySelector('option[value="' + CSS.escape(nv.maNv) + '"]');
        if (opt) opt.textContent = nhanOption(nv);
        veDsCho();
    }

    function taiNhanVien(ma, doiUrl) {
        st.ma = ma;
        $('dgnvNoiDung').hidden = true;
        $('dgnvCho').hidden = !!ma;
        if (!ma) {
            trangThaiTai('');
            if (doiUrl) history.replaceState(null, '', '/QLCongViec/DanhGia/DanhGiaNhanVien');
            return;
        }
        if (doiUrl) history.replaceState(null, '', '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(ma));
        trangThaiTai('Đang tải dữ liệu đánh giá...');
        goiApi('/QLCongViec/DanhGia/QuanLy/DuLieuDanhGia?maNv=' + encodeURIComponent(ma))
            .then(function (kq) {
                if (st.ma !== ma) return; // đã chọn người khác trong lúc chờ
                st.chiXem = !!kq.laChinhMinh;
                napDuLieu(kq.duLieu);
                trangThaiTai('');
                $('dgnvNoiDung').hidden = false;
                chuyenBuoc(1);
            })
            .catch(function (e) { trangThaiTai(e.message, true); });
    }

    function napDuLieu(d) {
        st.d = d;
        st.dsKpi = d.dsKpi || [];
        st.dsTieuChi = d.dsTieuChi || [];
        veThongTin();
        veKpi();
        veTieuChi();
        veP3();
    }

    // ---------- Bước 1 ----------
    function veThongTin() {
        var d = st.d, t = d.thongTin || {};
        $('dgnvMa').value = d.maNv;
        $('dgnvTen').value = t.hoTen || '';
        $('dgnvChucVu').value = t.chucVu || '';
        $('dgnvPhongBan').value = t.phongBan || '';
        $('dgnvExcel').href = '/QLCongViec/DanhGia/QuanLy/Excel?maNv=' + encodeURIComponent(d.maNv);

        var tu = d.phieuTu, ql = d.phieuQuanLy;
        var hop = $('dgnvTinhTrangTu');
        hop.textContent = '';
        hop.appendChild(el('b', { text: 'Tình trạng' }));
        hop.appendChild(el('ul', null, [
            el('li', { text: tu && tu.ngayNop ? 'Nhân viên đã gửi tự đánh giá lúc ' + new Date(tu.ngayNop).toLocaleString('vi-VN') : (tu ? 'Nhân viên đang tự đánh giá (chưa gửi)' : 'Nhân viên chưa tự đánh giá') }),
            el('li', { text: ql && ql.ngayNop ? 'Quản lý đã gửi đánh giá lúc ' + new Date(ql.ngayNop).toLocaleString('vi-VN') + (ql.tenNguoiDanhGia ? ' (' + ql.tenNguoiDanhGia + ')' : '') : (ql ? 'Quản lý đã lưu nháp, chưa gửi' : 'Quản lý chưa đánh giá') }),
            el('li', { text: st.dsKpi.length + ' nhiệm vụ Part 1 • ' + st.dsTieuChi.length + ' tiêu chí Part 2' })
        ]));

        var cb = $('dgnvCanhBao');
        cb.hidden = !st.chiXem;
        if (st.chiXem) $('dgnvCanhBaoChu').textContent = 'Đây là phiếu của chính bạn — chỉ xem, không tự chấm phiếu quản lý.';
    }

    // ---------- Bước 2: Part 1 ----------
    function veKpi() {
        var hop = $('dgnvDsKpi');
        hop.textContent = '';
        if (!st.dsKpi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Nhân viên chưa được giao nhiệm vụ / KPI.' }),
                el('small', { text: 'Giao KPI ở trang Quản lý nhân viên → “Hồ sơ & KPI”.' })
            ]));
        }
        st.dsKpi.forEach(function (k, i) { hop.appendChild(theKpi(k, i)); });
        tinhP1();
    }

    function nutMuc(id, giaTri) {
        var hop = el('div', { class: 'tdg-muc' });
        MUC.forEach(function (m) {
            hop.appendChild(el('button', { type: 'button', title: TEN_MUC[m], 'data-muc': m, 'data-muc-cho': id,
                class: m === giaTri ? 'chon' : '', disabled: st.chiXem, text: String(m) }));
        });
        return hop;
    }

    function theKpi(k, i) {
        var urlMc = '/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?maNv=' + encodeURIComponent(st.ma) + '&t=' + Date.now();
        var anh = !k.coAnh ? el('span', { class: 'text-muted', text: 'Nhân viên chưa tải minh chứng' })
            : k.laAnh ? el('img', { src: urlMc, alt: 'Ảnh minh chứng', 'data-mo-anh': '1' })
            : el('a', { href: urlMc, target: '_blank', rel: 'noopener', class: 'btn btn-sm btn-outline-primary', text: 'Xem tài liệu minh chứng' });
        return el('div', { class: 'tdg-kpi', 'data-kpi': k.idKpiMau }, [
            el('div', { class: 'tdg-kpi-head' }, [el('span', { text: k.congViec }), el('span', { class: 'tdg-kpi-stt', text: '#' + (i + 1) })]),
            el('div', { class: 'tdg-grid2' }, [
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Key Performance Index (nhân viên khai)' }), el('div', { class: 'tdg-chi-xem', text: k.chiSoKpi || '—' })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Result (nhân viên khai)' }), el('div', { class: 'tdg-chi-xem', text: k.ketQua || '—' })])
            ]),
            el('div', { class: 'tdg-kpi-row tdg-kpi-row3' }, [
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Weight — Trọng số' }),
                    el('input', { type: 'number', class: 'form-control', min: 0, max: 50, step: 1, 'data-truong': 'trongSo', value: fmt(so(k.trongSo)), disabled: st.chiXem })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Supervisor — Mức quản lý' }), nutMuc(k.idKpiMau, k.mucQuanLy),
                    el('div', { class: 'tdg-muc-diem', 'data-diem-kpi': k.idKpiMau })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'NV tự đánh giá: ' + (coGiaTri(k.mucTuDanhGia) ? 'mức ' + k.mucTuDanhGia + ' (' + TEN_MUC[k.mucTuDanhGia] + ')' : 'chưa chọn') }),
                    el('div', { class: 'tdg-anh' }, [anh])])
            ])
        ]);
    }

    function docKpi() {
        st.dsKpi.forEach(function (k) {
            var o = document.querySelector('.tdg-kpi[data-kpi="' + k.idKpiMau + '"] [data-truong="trongSo"]');
            if (o) k.trongSo = so(o.value);
        });
    }

    function diemKpi(truong) {
        return st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo) * so(k[truong]) / 5; }, 0);
    }

    function tinhP1() {
        docKpi();
        var tong = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0);
        var b = $('dgnvTongTrongSo');
        b.textContent = fmt(tong);
        b.classList.toggle('vuot', st.dsKpi.length > 0 && Math.abs(tong - 50) > 0.05);
        st.dsKpi.forEach(function (k) {
            var o = document.querySelector('[data-diem-kpi="' + k.idKpiMau + '"]');
            if (!o) return;
            o.textContent = '';
            o.appendChild(document.createTextNode('Score: '));
            o.appendChild(el('b', { text: coGiaTri(k.mucQuanLy) ? fmt(so(k.trongSo) * k.mucQuanLy / 5) : '—' }));
            if (coGiaTri(k.mucTuDanhGia)) o.appendChild(document.createTextNode(' • NV: ' + fmt(so(k.trongSo) * k.mucTuDanhGia / 5)));
        });
        $('dgnvDiemP1').textContent = fmt(diemKpi('mucQuanLy'));
    }

    function datMuc(id, m) {
        var k = st.dsKpi.find(function (x) { return x.idKpiMau === id; });
        if (!k) return;
        k.mucQuanLy = m;
        document.querySelectorAll('#dgnv [data-muc-cho="' + id + '"]').forEach(function (b) { b.classList.toggle('chon', so(b.getAttribute('data-muc')) === m); });
        tinhP1();
    }

    // ---------- Bước 3: Part 2 ----------
    function veTieuChi() {
        var hop = $('dgnvDsTieuChi');
        hop.textContent = '';
        if (!st.dsTieuChi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Nhân viên chưa có tiêu chí năng lực.' }),
                el('small', { text: 'Thêm tiêu chí (tên, mô tả, điểm tối đa — tổng 50) ở trang Quản lý nhân viên → “Hồ sơ & KPI”.' })
            ]));
        }
        st.dsTieuChi.forEach(function (t) {
            hop.appendChild(el('div', { class: 'tdg-tc' }, [
                el('div', { class: 'tdg-tc-head' }, [el('span', { text: t.ten }), el('span', { class: 'tdg-tc-max', text: 'Tối đa ' + fmt(so(t.diemToiDa)) + ' điểm' })]),
                t.moTa ? el('div', { class: 'tdg-tc-mota', text: t.moTa }) : null,
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Summary and some examples (nhân viên ghi)' }), el('div', { class: 'tdg-chi-xem', text: t.tomTat || '—' })]),
                el('div', { class: 'tdg-tc-diem' }, [
                    el('span', { text: 'Supervisor:' }),
                    el('input', { type: 'number', class: 'form-control form-control-sm', min: 0, max: t.diemToiDa, step: 0.5, 'data-tc-diem': t.idTieuChi,
                        value: coGiaTri(t.diemQuanLy) ? fmt(so(t.diemQuanLy)) : '', disabled: st.chiXem }),
                    el('span', { class: 'text-muted', text: '/ ' + fmt(so(t.diemToiDa)) }),
                    el('span', { class: 'tdg-nl-tu', text: 'NV tự chấm: ' + (coGiaTri(t.diemTu) ? fmt(so(t.diemTu)) : '—') })
                ])
            ]));
        });
        $('dgnvToiDaP2').textContent = fmt(st.dsTieuChi.reduce(function (s, t) { return s + so(t.diemToiDa); }, 0) || 50);
        tinhP2();
    }

    function docTieuChi() {
        st.dsTieuChi.forEach(function (t) {
            var o = document.querySelector('#dgnv [data-tc-diem="' + t.idTieuChi + '"]');
            if (o) t.diemQuanLy = o.value === '' ? null : so(o.value);
        });
    }

    function diemTc(truong) { return st.dsTieuChi.reduce(function (s, t) { return s + so(t[truong]); }, 0); }
    function tinhP2() { $('dgnvDiemP2').textContent = fmt(diemTc('diemQuanLy')); }

    // ---------- Bước 4: Part 3 ----------
    function veP3() {
        var tu = st.d.phieuTu || {}, ql = st.d.phieuQuanLy || {};
        [['dgnvDiemManh', 'diemManh'], ['dgnvDiemYeu', 'diemYeu'], ['dgnvLoTrinh', 'loTrinh'], ['dgnvKeHoach', 'keHoach']].forEach(function (x) {
            $(x[0]).textContent = tu[x[1]] || '—';
        });
        var nx = $('dgnvNhanXet');
        nx.value = ql.nhanXet || '';
        nx.disabled = st.chiXem;
    }

    // ---------- Bước 5 ----------
    function xepLoai(chenh) {
        if (chenh >= 20) return 'Vượt chuẩn';
        if (chenh >= 0) return 'Đạt chuẩn';
        if (chenh >= -20) return 'Cần cải thiện';
        return 'Chưa đạt';
    }

    function veSoSanh() {
        docKpi();
        docTieuChi();
        var tu = st.d.phieuTu;
        var p1Ql = diemKpi('mucQuanLy'), p2Ql = diemTc('diemQuanLy');
        var p1Tu = diemKpi('mucTuDanhGia'), p2Tu = diemTc('diemTu');
        var dongs = [
            ['Part I (50)', tu ? fmt(p1Tu) : '—', fmt(p1Ql)],
            ['Part II (50)', tu ? fmt(p2Tu) : '—', fmt(p2Ql)],
            ['Total (100)', tu ? fmt(p1Tu + p2Tu) : '—', fmt(p1Ql + p2Ql)],
            ['So với chuẩn 60', tu ? fmt(p1Tu + p2Tu - 60) : '—', fmt(p1Ql + p2Ql - 60)],
            ['Xếp loại', tu ? xepLoai(p1Tu + p2Tu - 60) : '—', xepLoai(p1Ql + p2Ql - 60)]
        ];
        var body = $('dgnvBangSoSanh');
        body.textContent = '';
        dongs.forEach(function (r) { body.appendChild(el('tr', null, r.map(function (c) { return el('td', { text: c }); }))); });
        var ql = st.d.phieuQuanLy;
        $('dgnvDaChot').hidden = !(ql && ql.trangThai === 'completed');
        $('dgnvMoLai').disabled = st.chiXem;

        var thieu = loiHoanThanh();
        $('dgnvThieu').hidden = !thieu || st.chiXem;
        $('dgnvThieuChu').textContent = thieu ? 'Cần hoàn tất trước khi gửi: ' + thieu + '.' : '';
    }

    // Cùng điều kiện server kiểm khi "Hoàn thành" — báo sớm cho quản lý
    function loiHoanThanh() {
        var loi = [];
        var tong = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0);
        if (!st.dsKpi.length) loi.push('chưa giao nhiệm vụ Part 1');
        else if (Math.abs(tong - 50) > 0.05) loi.push('tổng trọng số Part 1 phải bằng 50 (hiện ' + fmt(tong) + ')');
        var thieuMuc = st.dsKpi.filter(function (k) { return !coGiaTri(k.mucQuanLy); }).length;
        if (thieuMuc) loi.push(thieuMuc + ' nhiệm vụ chưa chọn mức');
        if (!st.dsTieuChi.length) loi.push('chưa có tiêu chí Part 2');
        var thieuTc = st.dsTieuChi.filter(function (t) { return !coGiaTri(t.diemQuanLy); }).length;
        if (thieuTc) loi.push(thieuTc + ' tiêu chí chưa chấm');
        return loi.join('; ');
    }

    // ---------- Điều hướng + lưu ----------
    function chuyenBuoc(b) {
        st.buoc = b;
        baoLoi('');
        document.querySelectorAll('#dgnv [data-panel]').forEach(function (p) { p.hidden = so(p.getAttribute('data-panel')) !== b; });
        document.querySelectorAll('#dgnvSteps li').forEach(function (li) {
            var n = so(li.getAttribute('data-buoc'));
            li.classList.toggle('dat', n <= b);
            li.classList.toggle('qua', n < b);
        });
        if (b === SO_BUOC) veSoSanh();
        $('dgnvQuayLai').disabled = b === 1;
        var nut = $('dgnvTiepTuc');
        nut.textContent = '';
        if (b === SO_BUOC) {
            nut.appendChild(document.createTextNode('Hoàn thành '));
            nut.appendChild(el('i', { class: 'fa fa-paper-plane' }));
            nut.disabled = st.chiXem;
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
        docTieuChi();
        return goiApi('/QLCongViec/DanhGia/QuanLy/LuuDanhGia', {
            maNv: st.ma,
            dsKpi: st.dsKpi.map(function (k) { return { idKpiMau: k.idKpiMau, trongSo: so(k.trongSo), mucQuanLy: coGiaTri(k.mucQuanLy) ? k.mucQuanLy : null }; }),
            dsTieuChi: st.dsTieuChi.map(function (t) { return { idTieuChi: t.idTieuChi, diem: t.diemQuanLy }; }),
            nhanXet: $('dgnvNhanXet').value,
            hoanThanh: !!hoanThanh
        });
    }

    function tiepTuc() {
        if (st.dangLuu) return;
        var b = st.buoc;
        if (b === 1 || st.chiXem) { if (b < SO_BUOC) chuyenBuoc(b + 1); return; }

        if (b === 2) {
            docKpi();
            var tongTs = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0);
            if (tongTs > 50.05) { baoLoi('Tổng trọng số Part 1 vượt quá 50.'); return; }
        }
        if (b === 3) {
            docTieuChi();
            var sai = st.dsTieuChi.filter(function (t) { return coGiaTri(t.diemQuanLy) && (t.diemQuanLy < 0 || t.diemQuanLy > so(t.diemToiDa)); })[0];
            if (sai) { baoLoi('Điểm “' + sai.ten + '” phải trong khoảng 0–' + fmt(so(sai.diemToiDa)) + '.'); return; }
        }

        var nut = $('dgnvTiepTuc');
        var cu = Array.prototype.slice.call(nut.childNodes);
        st.dangLuu = true;
        nut.disabled = true;
        nut.textContent = '';
        nut.appendChild(el('i', { class: 'fa fa-spinner fa-spin' }));
        nut.appendChild(document.createTextNode(' Đang lưu...'));
        baoLoi('');

        goiLuu(b === SO_BUOC)
            .then(function (kq) {
                st.dangLuu = false;
                // Giữ nguyên điểm đang nhập trên màn hình, chỉ cập nhật trạng thái phiếu từ server
                st.d.phieuTu = kq.duLieu.phieuTu;
                st.d.phieuQuanLy = kq.duLieu.phieuQuanLy;
                veThongTin();
                capNhatTrangThaiDs();
                if (b === SO_BUOC) {
                    chuyenBuoc(SO_BUOC);
                    Swal.fire({ icon: 'success', title: kq.thongBao, timer: 2000, showConfirmButton: false });
                } else {
                    chuyenBuoc(b + 1);
                }
            })
            .catch(function (e) {
                st.dangLuu = false;
                nut.textContent = '';
                cu.forEach(function (n) { nut.appendChild(n); });
                nut.disabled = false;
                baoLoi(e.message);
            });
    }

    function moLai() {
        Swal.fire({
            icon: 'question',
            text: 'Mở lại phiếu? Nhân viên sẽ sửa được tự đánh giá; đánh giá của quản lý chuyển về nháp (điểm vẫn giữ).',
            showCancelButton: true, confirmButtonText: 'Mở lại', cancelButtonText: 'Huỷ'
        }).then(function (r) {
            if (!r.isConfirmed) return;
            var nut = $('dgnvMoLai');
            nut.disabled = true;
            goiApi('/QLCongViec/DanhGia/QuanLy/MoLai', { maNv: st.ma })
                .then(function (kq) {
                    st.d.phieuTu = kq.duLieu.phieuTu;
                    st.d.phieuQuanLy = kq.duLieu.phieuQuanLy;
                    veThongTin();
                    veSoSanh();
                    capNhatTrangThaiDs();
                    Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1800, showConfirmButton: false });
                })
                .catch(function (e) { Swal.fire({ icon: 'error', text: e.message }); })
                .finally(function () { nut.disabled = false; });
        });
    }

    // ---------- Sự kiện ----------
    document.addEventListener('click', function (e) {
        var t = e.target;
        if (t.closest('#dgnvTiepTuc')) { e.preventDefault(); tiepTuc(); return; }
        if (t.closest('#dgnvQuayLai')) { e.preventDefault(); if (st.buoc > 1 && !st.dangLuu) { docKpi(); docTieuChi(); chuyenBuoc(st.buoc - 1); } return; }
        if (t.closest('#dgnvMoLai')) { moLai(); return; }
        var chonNv = t.closest('[data-chon-nv]');
        if (chonNv) { var ma = chonNv.getAttribute('data-chon-nv'); $('dgnvChon').value = ma; taiNhanVien(ma, true); return; }
        var muc = t.closest('#dgnv [data-muc]');
        if (muc && !st.chiXem) { datMuc(muc.getAttribute('data-muc-cho'), so(muc.getAttribute('data-muc'))); return; }
        var anh = t.closest('[data-mo-anh]');
        if (anh) window.open(anh.getAttribute('src'), '_blank', 'noopener');
    });

    document.addEventListener('input', function (e) {
        var t = e.target;
        if (t.matches('#dgnvDsKpi [data-truong="trongSo"]')) { tinhP1(); return; }
        if (t.matches('#dgnv [data-tc-diem]')) { docTieuChi(); tinhP2(); }
    });

    document.addEventListener('change', function (e) {
        if (e.target.id === 'dgnvChon') taiNhanVien(e.target.value, true);
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', taiDsNhanVien);
    else taiDsNhanVien();
})();
