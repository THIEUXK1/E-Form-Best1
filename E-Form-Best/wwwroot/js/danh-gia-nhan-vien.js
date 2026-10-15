// Trang Đánh giá nhân viên (/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=...) — chuyển từ /manager/evaluation.
// Khác app cũ: hiện song song phần nhân viên tự khai (KPI, ảnh minh chứng, điểm năng lực) để quản lý so sánh
// ("Xem chi tiết" của app cũ gộp vào đây); đổi nhân viên không tải lại trang, chỉ đổi ?maNv trên URL.
(function () {
    'use strict';

    var SO_BUOC = 4;
    var DIEM_NHANH = [0, 2, 4, 6, 8, 10];

    var st = { buoc: 1, ma: '', d: null, diem: {}, diemTu: {}, dsNhom: [], dsKpi: [], chiXem: false, dangLuu: false };

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

    function chuanDiem(map) {
        var kq = {};
        Object.keys(map || {}).forEach(function (k) { kq[k.toLowerCase()] = so(map[k]); });
        return kq;
    }

    // ---------- Chọn nhân viên ----------
    function taiDsNhanVien() {
        var maUrl = new URLSearchParams(location.search).get('maNv') || '';
        goiApi('/QLCongViec/DanhGia/QuanLy/DanhSach')
            .then(function (kq) {
                var sel = $('dgnvChon');
                sel.textContent = '';
                sel.appendChild(el('option', { value: '', text: '— Chọn nhân viên để đánh giá —' }));
                var nhan = { completed: 'Hoàn thành', awaiting_manager: 'Chờ quản lý', awaiting_self: 'Chờ tự đánh giá', not_started: 'Chưa đánh giá' };
                (kq.duLieu || []).forEach(function (nv) {
                    sel.appendChild(el('option', { value: nv.maNv, text: nv.maNv + ' — ' + nv.hoTen + ' (' + (nhan[nv.trangThai] || '') + ')' }));
                });
                if (maUrl) {
                    sel.value = maUrl;
                    if (sel.value) taiNhanVien(maUrl, false);
                    else trangThaiTai(maUrl + ' không có trong danh sách đánh giá.', true);
                }
            })
            .catch(function (e) { trangThaiTai(e.message, true); });
    }

    function taiNhanVien(ma, doiUrl) {
        st.ma = ma;
        $('dgnvNoiDung').hidden = true;
        if (!ma) { trangThaiTai(''); return; }
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
        st.dsNhom = d.dsNhomNangLuc || [];
        st.diem = chuanDiem(d.diemQuanLy);
        st.diemTu = chuanDiem(d.diemTu);
        veThongTin();
        veBoLoc();
        veKpi();
        veNangLuc();
    }

    // ---------- Bước 1 ----------
    function veThongTin() {
        var d = st.d;
        $('dgnvMa').value = d.maNv;
        $('dgnvTen').value = d.hoTen || '';
        $('dgnvPhongBan').value = d.phongBan || '';
        $('dgnvCapBacHoSo').value = d.hoSo.capBac + (d.hoSo.dayDu ? '' : ' (mặc định / chưa đủ hồ sơ)');
        $('dgnvNhomHoSo').value = d.hoSo.tenNhomCongViec || 'Chưa gán';
        $('dgnvExcel').href = '/QLCongViec/DanhGia/QuanLy/Excel?maNv=' + encodeURIComponent(d.maNv);

        var tu = d.phieuTu, ql = d.phieuQuanLy;
        var hop = $('dgnvTinhTrangTu');
        hop.textContent = '';
        hop.appendChild(el('b', { text: 'Tình trạng' }));
        hop.appendChild(el('ul', null, [
            el('li', { text: tu && tu.ngayNop ? 'Nhân viên đã gửi tự đánh giá lúc ' + new Date(tu.ngayNop).toLocaleString('vi-VN') : (tu ? 'Nhân viên đang tự đánh giá (chưa gửi)' : 'Nhân viên chưa tự đánh giá') }),
            el('li', { text: ql && ql.ngayNop ? 'Quản lý đã gửi đánh giá lúc ' + new Date(ql.ngayNop).toLocaleString('vi-VN') : (ql ? 'Quản lý đã lưu nháp, chưa gửi' : 'Quản lý chưa đánh giá') })
        ]));

        var cb = $('dgnvCanhBao');
        cb.hidden = !st.chiXem;
        if (st.chiXem) $('dgnvCanhBaoChu').textContent = 'Đây là phiếu của chính bạn — chỉ xem, không tự chấm phiếu quản lý.';
    }

    // ---------- Bước 2: KPI ----------
    function veKpi() {
        var hop = $('dgnvDsKpi');
        hop.textContent = '';
        if (!st.dsKpi.length) {
            hop.appendChild(el('div', { class: 'tdg-empty' }, [
                el('div', { text: 'Nhân viên chưa được giao KPI.' }),
                el('small', { text: 'Giao KPI ở trang Quản lý nhân viên → “Hồ sơ & KPI”.' })
            ]));
        }
        st.dsKpi.forEach(function (k, i) { hop.appendChild(theKpi(k, i)); });
        tinhTrongSo();
    }

    function theKpi(k, i) {
        var anh = k.coAnh
            ? el('img', { src: '/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?maNv=' + encodeURIComponent(st.ma) + '&t=' + Date.now(), alt: 'Ảnh minh chứng', 'data-mo-anh': '1' })
            : el('span', { class: 'text-muted', text: 'Nhân viên chưa tải ảnh' });
        return el('div', { class: 'tdg-kpi', 'data-kpi': k.idKpiMau }, [
            el('div', { class: 'tdg-kpi-head' }, [el('span', { text: k.congViec }), el('span', { class: 'tdg-kpi-stt', text: '#' + (i + 1) })]),
            el('div', { class: 'tdg-grid2' }, [
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Chỉ số KPI (nhân viên khai)' }), el('div', { class: 'tdg-chi-xem', text: k.chiSoKpi || '—' })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'Kết quả (nhân viên khai)' }), el('div', { class: 'tdg-chi-xem', text: k.ketQua || '—' })])
            ]),
            el('div', { class: 'tdg-kpi-row' }, [
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Trọng số' }),
                    el('input', { type: 'number', class: 'form-control', min: 0, max: 100, step: 1, 'data-truong': 'trongSo', value: fmt(so(k.trongSo)), disabled: st.chiXem })]),
                el('label', { class: 'tdg-field' }, [el('span', { text: 'Quản lý đánh giá (0–120%)' }),
                    el('input', { type: 'number', class: 'form-control', min: 0, max: 120, step: 1, 'data-truong': 'diemQuanLy', value: coGiaTri(k.diemQuanLy) ? fmt(so(k.diemQuanLy)) : '', disabled: st.chiXem })]),
                el('div', { class: 'tdg-field' }, [el('span', { text: 'NV tự đánh giá: ' + (coGiaTri(k.diemTuDanhGia) ? fmt(so(k.diemTuDanhGia)) + '%' : 'chưa nhập') }), el('div', { class: 'tdg-anh' }, [anh])])
            ])
        ]);
    }

    function docKpi() {
        st.dsKpi.forEach(function (k) {
            var the = document.querySelector('.tdg-kpi[data-kpi="' + k.idKpiMau + '"]');
            if (!the) return;
            k.trongSo = so(the.querySelector('[data-truong="trongSo"]').value);
            var v = the.querySelector('[data-truong="diemQuanLy"]').value;
            k.diemQuanLy = v === '' ? null : so(v);
        });
    }

    function tinhTrongSo() {
        var tong = 0;
        document.querySelectorAll('#dgnvDsKpi [data-truong="trongSo"]').forEach(function (o) { tong += so(o.value); });
        var b = $('dgnvTongTrongSo');
        b.textContent = fmt(tong);
        b.style.color = tong > 100 ? '#b91c1c' : '';
    }

    // ---------- Bước 3: năng lực ----------
    function veBoLoc() {
        var d = st.d;
        var selCap = $('dgnvCapBac');
        selCap.textContent = '';
        var ds = (d.dsCapBac || []).slice();
        if (d.boLoc.capBac && ds.indexOf(d.boLoc.capBac) < 0) ds.push(d.boLoc.capBac);
        ds.forEach(function (c) { selCap.appendChild(el('option', { value: c, text: c })); });
        selCap.value = d.boLoc.capBac || '';
        var selNhom = $('dgnvNhomCongViec');
        while (selNhom.options.length > 1) selNhom.remove(1);
        (d.dsNhomCongViec || []).forEach(function (n) { selNhom.appendChild(el('option', { value: n.id, text: n.ten })); });
        selNhom.value = d.boLoc.idNhomCongViec || '';
        selCap.disabled = selNhom.disabled = st.chiXem;
    }

    function doiBoLoc() {
        var cap = $('dgnvCapBac').value, nhom = $('dgnvNhomCongViec').value;
        var hop = $('dgnvDsNhomNangLuc');
        hop.textContent = '';
        hop.appendChild(el('div', { class: 'tdg-loading' }, [el('i', { class: 'fa fa-spinner fa-spin' }), ' Đang tải danh sách năng lực...']));
        goiApi('/QLCongViec/DanhGia/NangLuc?capBac=' + encodeURIComponent(cap) + (nhom ? '&idNhomCongViec=' + encodeURIComponent(nhom) : ''))
            .then(function (kq) { st.dsNhom = kq.duLieu || []; veNangLuc(); })
            .catch(function (e) { hop.textContent = ''; hop.appendChild(el('div', { class: 'tdg-empty', text: e.message })); });
    }

    function lopGap(g) { return g > 0 ? 'duong' : (g < 0 ? 'am' : 'bang'); }
    function chuGap(g) { return 'GAP: ' + (g > 0 ? '+' : '') + fmt(g); }

    function veNangLuc() {
        var hop = $('dgnvDsNhomNangLuc');
        hop.textContent = '';
        if (!st.dsNhom.length) hop.appendChild(el('div', { class: 'tdg-empty', text: 'Không có năng lực nào cho bộ lọc hiện tại.' }));
        st.dsNhom.forEach(function (g) {
            var than = el('div', { class: 'tdg-group-body' });
            g.dsNangLuc.forEach(function (n) { than.appendChild(dongNangLuc(n)); });
            hop.appendChild(el('div', { class: 'tdg-group' }, [
                el('button', { type: 'button', class: 'tdg-group-head', 'data-mo-nhom': '1' }, [
                    el('span', null, [g.ten + ' ', el('small', { 'data-tong-nhom': g.idNhom })]), el('i', { class: 'fa fa-chevron-up' })
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
            nhanh.appendChild(el('button', { type: 'button', 'data-diem-nhanh': v, 'data-nl': id, class: v === diem ? 'chon' : '', disabled: st.chiXem, text: String(v) }));
        });
        var gap = diem - so(n.diemYeuCau);
        return el('div', { class: 'tdg-nl', 'data-nl-dong': id }, [
            el('div', { class: 'tdg-nl-info' }, [
                el('div', { class: 'tdg-nl-ten', title: n.moTa || '' }, [el('b', { text: n.ma }), n.ten]),
                el('div', { class: 'tdg-nl-meta' }, ['Yêu cầu: ' + fmt(so(n.diemYeuCau)),
                    el('span', { class: 'tdg-nl-tu', text: 'NV tự chấm: ' + (st.diemTu[id] !== undefined ? fmt(st.diemTu[id]) : '—') }),
                    el('span', { class: 'tdg-gap ' + lopGap(gap), 'data-gap': id, text: chuGap(gap) })])
            ]),
            el('div', { class: 'tdg-nl-diem' }, [
                el('input', { type: 'number', class: 'form-control form-control-sm', min: 0, max: 10, step: 0.1, value: fmt(diem), 'data-nl': id, disabled: st.chiXem }),
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

    function tong(map) {
        var thuc = 0, yc = 0;
        st.dsNhom.forEach(function (g) { g.dsNangLuc.forEach(function (n) { thuc += map[n.idNangLuc.toLowerCase()] || 0; yc += so(n.diemYeuCau); }); });
        return { thuc: thuc, yc: yc };
    }

    function capNhatNhom(g) {
        var thuc = 0, yc = 0;
        g.dsNangLuc.forEach(function (n) { thuc += st.diem[n.idNangLuc.toLowerCase()] || 0; yc += so(n.diemYeuCau); });
        var o = document.querySelector('[data-tong-nhom="' + g.idNhom + '"]');
        if (o) o.textContent = '• ' + g.dsNangLuc.length + ' năng lực • ' + fmt(thuc) + '/' + fmt(yc) + ' • ' + chuGap(thuc - yc);
    }

    function capNhatTong() {
        var t = tong(st.diem);
        $('dgnvTongNangLuc').textContent = 'Quản lý chấm: ' + fmt(t.thuc) + ' / yêu cầu ' + fmt(t.yc) + ' — ' + chuGap(t.thuc - t.yc);
    }

    // ---------- Bước 4 ----------
    function xepLoai(gap) {
        if (gap >= 1) return 'Vượt chuẩn';
        if (gap >= 0) return 'Đạt chuẩn';
        if (gap >= -1) return 'Cần cải thiện';
        return 'Chưa đạt';
    }

    function veSoSanh() {
        docKpi();
        var tu = st.d.phieuTu;
        var tQl = tong(st.diem);
        var kpiQl = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo) * so(k.diemQuanLy) / 100; }, 0);
        var kpiTu = st.dsKpi.reduce(function (s, k) { return s + so(k.trongSo) * so(k.diemTuDanhGia) / 100; }, 0);
        var tTu = tong(st.diemTu);
        var gapQl = kpiQl + tQl.thuc - tQl.yc;
        var gapTu = kpiTu + tTu.thuc - tTu.yc;
        var dongs = [
            ['Điểm KPI', tu ? fmt(kpiTu) : '—', fmt(kpiQl)],
            ['Điểm năng lực', tu ? fmt(tTu.thuc) : '—', fmt(tQl.thuc)],
            ['Tổng yêu cầu', fmt(tQl.yc), fmt(tQl.yc)],
            ['GAP', tu ? fmt(gapTu) : '—', fmt(gapQl)],
            ['Xếp loại', tu ? xepLoai(gapTu) : '—', xepLoai(gapQl)]
        ];
        var body = $('dgnvBangSoSanh');
        body.textContent = '';
        dongs.forEach(function (r) { body.appendChild(el('tr', null, r.map(function (c) { return el('td', { text: c }); }))); });
        var ql = st.d.phieuQuanLy;
        $('dgnvDaChot').hidden = !(ql && ql.trangThai === 'completed');
        $('dgnvMoLai').disabled = st.chiXem;
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
        if (b === 4) veSoSanh();
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
        var dsNangLuc = [];
        st.dsNhom.forEach(function (g) {
            g.dsNangLuc.forEach(function (n) {
                var id = n.idNangLuc.toLowerCase();
                if (st.diem[id] !== undefined) dsNangLuc.push({ idNangLuc: n.idNangLuc, diem: st.diem[id] });
            });
        });
        return goiApi('/QLCongViec/DanhGia/QuanLy/LuuDanhGia', {
            maNv: st.ma,
            capBac: $('dgnvCapBac').value,
            idNhomCongViec: $('dgnvNhomCongViec').value || null,
            dsNangLuc: dsNangLuc,
            dsKpi: st.dsKpi.map(function (k) { return { idKpiMau: k.idKpiMau, trongSo: so(k.trongSo), diemQuanLy: k.diemQuanLy }; }),
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
            if (tongTs > 100) { baoLoi('Tổng trọng số KPI vượt quá 100.'); return; }
            if (st.dsKpi.some(function (k) { return coGiaTri(k.diemQuanLy) && (k.diemQuanLy < 0 || k.diemQuanLy > 120); })) {
                baoLoi('Quản lý đánh giá KPI phải trong khoảng 0–120%.');
                return;
            }
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
        if (t.closest('#dgnvQuayLai')) { e.preventDefault(); if (st.buoc > 1 && !st.dangLuu) { docKpi(); chuyenBuoc(st.buoc - 1); } return; }
        if (t.closest('#dgnvMoLai')) { moLai(); return; }
        var nhanh = t.closest('[data-diem-nhanh]');
        if (nhanh && !st.chiXem) { datDiem(nhanh.getAttribute('data-nl'), nhanh.getAttribute('data-diem-nhanh')); return; }
        var moNhom = t.closest('[data-mo-nhom]');
        if (moNhom) { moNhom.parentNode.classList.toggle('dong'); return; }
        var anh = t.closest('[data-mo-anh]');
        if (anh) window.open(anh.getAttribute('src'), '_blank', 'noopener');
    });

    document.addEventListener('input', function (e) {
        var t = e.target;
        if (t.matches('#dgnv input[data-nl]') && !st.chiXem) { datDiem(t.getAttribute('data-nl'), t.value, true); return; }
        if (t.matches('#dgnvDsKpi [data-truong="trongSo"]')) tinhTrongSo();
    });

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (t.matches('#dgnv input[data-nl]')) { datDiem(t.getAttribute('data-nl'), t.value); return; }
        if (t.id === 'dgnvChon') { taiNhanVien(t.value, true); return; }
        if (t.id === 'dgnvCapBac' || t.id === 'dgnvNhomCongViec') doiBoLoc();
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', taiDsNhanVien);
    else taiDsNhanVien();
})();
