// Trang Thống kê đánh giá (/QLCongViec/DanhGia/ThongKe).
// Dùng lại API danh sách của "Quản lý nhân viên" (vài chục người) nên gom nhóm, lọc, vẽ biểu đồ đều ở client.
(function () {
    'use strict';

    var TRANG_THAI = [
        { ma: 'completed', ten: 'Hoàn thành', mau: '#65a30d' },
        { ma: 'awaiting_manager', ten: 'Chờ quản lý', mau: '#3b82f6' },
        { ma: 'awaiting_self', ten: 'Chờ tự đánh giá', mau: '#f59e0b' },
        { ma: 'not_started', ten: 'Chưa đánh giá', mau: '#94a3b8' }
    ];
    var XEP_LOAI = [
        { ten: 'Vượt chuẩn', mau: '#16a34a' },
        { ten: 'Đạt chuẩn', mau: '#3b82f6' },
        { ten: 'Cần cải thiện', mau: '#f59e0b' },
        { ten: 'Chưa đạt', mau: '#dc2626' }
    ];
    var KHONG_RO = '(Chưa rõ)';

    var st = { ds: [], locBang: '', bd: {} };

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

    function coSo(v) { return v !== null && v !== undefined && !isNaN(parseFloat(v)); }
    function fmt(v) { return coSo(v) ? (Math.round(parseFloat(v) * 10) / 10).toString() : '--'; }
    function tb(arr) { return arr.length ? Math.round(arr.reduce(function (a, b) { return a + b; }, 0) / arr.length * 10) / 10 : 0; }
    function phongBan(nv) { return (nv.phongBan || '').trim() || KHONG_RO; }
    function nhom(nv) { return (nv.tenNhomCongViec || '').trim() || KHONG_RO; }
    function timTrangThai(ma) { return TRANG_THAI.find(function (t) { return t.ma === ma; }) || TRANG_THAI[3]; }
    function duoiChuan(nv) { return coSo(nv.chenhLech) && parseFloat(nv.chenhLech) < 0; }

    function baoLoi(msg) {
        $('tkdgLoiText').textContent = msg;
        $('tkdgLoi').hidden = !msg;
    }

    function napLuaChon(id, giaTri) {
        var sel = $(id), cu = sel.value;
        while (sel.options.length > 1) sel.remove(1);
        giaTri.forEach(function (v) { sel.appendChild(el('option', { value: v, text: v })); });
        if (giaTri.indexOf(cu) >= 0) sel.value = cu;
    }

    function duyNhat(fn) {
        var s = {};
        st.ds.forEach(function (nv) { s[fn(nv)] = true; });
        return Object.keys(s).sort(function (a, b) { return a.localeCompare(b, 'vi'); });
    }

    // Dữ liệu sau bộ lọc đầu trang (phòng ban / nhóm / cấp bậc) — dùng cho KPI và biểu đồ
    function dsLoc() {
        var pb = $('tkdgPhongBan').value, nh = $('tkdgNhom').value, cb = $('tkdgCapBac').value;
        return st.ds.filter(function (nv) {
            return (!pb || phongBan(nv) === pb) && (!nh || nhom(nv) === nh) && (!cb || nv.capBac === cb);
        });
    }

    function veBieuDo(key, canvasId, trongId, cauHinh, coDuLieu) {
        if (st.bd[key]) { st.bd[key].destroy(); st.bd[key] = null; }
        $(trongId).classList.toggle('hien', !coDuLieu);
        $(canvasId).style.visibility = coDuLieu ? 'visible' : 'hidden';
        if (coDuLieu) st.bd[key] = new Chart($(canvasId), cauHinh);
    }

    var nhanSo = {
        color: '#fff',
        font: { weight: 'bold', size: 11 },
        formatter: function (v) { return v ? v : ''; }
    };

    function veKpi(ds) {
        var dem = {};
        TRANG_THAI.forEach(function (t) { dem[t.ma] = 0; });
        ds.forEach(function (nv) { dem[timTrangThai(nv.trangThai).ma]++; });
        $('tkdgTong').textContent = ds.length;
        $('tkdgHoanThanh').textContent = dem.completed;
        $('tkdgTyLe').textContent = (ds.length ? Math.round(dem.completed / ds.length * 100) : 0) + '% hoàn thành';
        $('tkdgChoQl').textContent = dem.awaiting_manager;
        $('tkdgChoTu').textContent = dem.awaiting_self;
        $('tkdgChuaDg').textContent = dem.not_started;
        $('tkdgDuoiChuan').textContent = ds.filter(duoiChuan).length;
        return dem;
    }

    function veTrangThai(dem) {
        var data = TRANG_THAI.map(function (t) { return dem[t.ma]; });
        veBieuDo('trangThai', 'tkdgBdTrangThai', 'tkdgTrongTrangThai', {
            type: 'doughnut',
            data: { labels: TRANG_THAI.map(function (t) { return t.ten; }), datasets: [{ data: data, backgroundColor: TRANG_THAI.map(function (t) { return t.mau; }) }] },
            options: {
                maintainAspectRatio: false,
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } }, datalabels: nhanSo },
                onClick: function (_e, items) { if (items.length) chonLoc(TRANG_THAI[items[0].index].ma); }
            }
        }, data.some(function (x) { return x > 0; }));
    }

    function vePhongBan(ds) {
        var dsPb = [];
        ds.forEach(function (nv) { if (dsPb.indexOf(phongBan(nv)) < 0) dsPb.push(phongBan(nv)); });
        dsPb.sort(function (a, b) { return a.localeCompare(b, 'vi'); });
        var datasets = TRANG_THAI.map(function (t) {
            return {
                label: t.ten,
                backgroundColor: t.mau,
                data: dsPb.map(function (pb) { return ds.filter(function (nv) { return phongBan(nv) === pb && timTrangThai(nv.trangThai).ma === t.ma; }).length; })
            };
        });
        veBieuDo('phongBan', 'tkdgBdPhongBan', 'tkdgTrongPhongBan', {
            type: 'bar',
            data: { labels: dsPb, datasets: datasets },
            options: {
                maintainAspectRatio: false,
                scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } } },
                plugins: { legend: { position: 'top', labels: { boxWidth: 10, font: { size: 10 } } }, datalabels: nhanSo },
                onClick: function (_e, items) {
                    if (!items.length) return;
                    $('tkdgPhongBan').value = dsPb[items[0].index];
                    veTatCa();
                }
            }
        }, dsPb.length > 0);
    }

    function veXepLoai(ds) {
        var data = XEP_LOAI.map(function (x) { return ds.filter(function (nv) { return nv.xepLoai === x.ten; }).length; });
        veBieuDo('xepLoai', 'tkdgBdXepLoai', 'tkdgTrongXepLoai', {
            type: 'doughnut',
            data: { labels: XEP_LOAI.map(function (x) { return x.ten; }), datasets: [{ data: data, backgroundColor: XEP_LOAI.map(function (x) { return x.mau; }) }] },
            options: {
                maintainAspectRatio: false,
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } }, datalabels: nhanSo },
                onClick: function (_e, items) { if (items.length) chonLoc('xl:' + XEP_LOAI[items[0].index].ten); }
            }
        }, data.some(function (x) { return x > 0; }));
    }

    function veNhom(ds) {
        var coDiem = ds.filter(function (nv) { return coSo(nv.diemKpi) || coSo(nv.diemNangLuc); });
        var dsNhom = [];
        coDiem.forEach(function (nv) { if (dsNhom.indexOf(nhom(nv)) < 0) dsNhom.push(nhom(nv)); });
        dsNhom.sort(function (a, b) { return a.localeCompare(b, 'vi'); });
        function tbTheo(truong) {
            return dsNhom.map(function (n) {
                return tb(coDiem.filter(function (nv) { return nhom(nv) === n && coSo(nv[truong]); }).map(function (nv) { return parseFloat(nv[truong]); }));
            });
        }
        veBieuDo('nhom', 'tkdgBdNhom', 'tkdgTrongNhom', {
            type: 'bar',
            data: {
                labels: dsNhom,
                datasets: [
                    { label: 'Part I trung bình', backgroundColor: '#65a30d', data: tbTheo('diemKpi') },
                    { label: 'Part II trung bình', backgroundColor: '#8b5cf6', data: tbTheo('diemNangLuc') },
                    { label: 'Chênh lệch chuẩn 60 (TB)', backgroundColor: '#f97316', data: tbTheo('chenhLech') }
                ]
            },
            options: {
                maintainAspectRatio: false,
                scales: { y: { beginAtZero: true } },
                plugins: {
                    legend: { position: 'top', labels: { boxWidth: 10, font: { size: 10 } } },
                    datalabels: { anchor: 'end', align: 'top', color: '#334155', font: { weight: 'bold', size: 10 }, formatter: function (v) { return v; } }
                }
            }
        }, dsNhom.length > 0);
    }

    function ngay(v) {
        if (!v) return '';
        var d = new Date(v);
        return isNaN(d) ? '' : ('0' + d.getDate()).slice(-2) + '/' + ('0' + (d.getMonth() + 1)).slice(-2) + '/' + d.getFullYear();
    }

    // Mỗi cột một trạng thái chưa xong; chỉ "Chờ quản lý" có nút chấm ngay vì đó là việc của người xem trang
    function veChuaXong(ds) {
        var cot = [
            { ma: 'awaiting_manager', ul: 'tkdgDsChoQl', dem: 'tkdgDemChoQl', nut: true },
            { ma: 'awaiting_self', ul: 'tkdgDsChoTu', dem: 'tkdgDemChoTu' },
            { ma: 'not_started', ul: 'tkdgDsChuaDg', dem: 'tkdgDemChuaDg', nut: true }
        ];
        var tong = 0;
        cot.forEach(function (c) {
            var rows = ds.filter(function (nv) { return timTrangThai(nv.trangThai).ma === c.ma; })
                .sort(function (a, b) { return phongBan(a).localeCompare(phongBan(b), 'vi') || a.maNv.localeCompare(b.maNv); });
            tong += rows.length;
            $(c.dem).textContent = rows.length;
            var ul = $(c.ul);
            ul.textContent = '';
            if (!rows.length) { ul.appendChild(el('li', { class: 'trong', text: 'Không có nhân viên' })); return; }
            rows.forEach(function (nv) {
                var phu = [nv.maNv, nv.phongBan, nv.ngayCapNhat ? 'cập nhật ' + ngay(nv.ngayCapNhat) : ''].filter(Boolean).join(' · ');
                ul.appendChild(el('li', null, [
                    el('div', { class: 'ten' }, [
                        el('a', { href: '/QLCongViec/DanhGia/ChiTiet/' + encodeURIComponent(nv.maNv), text: nv.hoTen }),
                        el('div', { class: 'tkdg-phu', text: phu, title: phu })
                    ]),
                    c.nut ? el('a', { class: 'tkdg-btn-dg', href: '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(nv.maNv) }, [el('i', { class: 'fa fa-pencil-square-o' }), ' Đánh giá']) : null
                ]));
            });
        });
        $('tkdgChuaXongDem').textContent = '(' + tong + '/' + ds.length + ' người)';
    }

    function moTaLoc() {
        if (!st.locBang) return '';
        if (st.locBang === 'duoi_chuan') return '— Dưới chuẩn';
        if (st.locBang.indexOf('xl:') === 0) return '— Xếp loại: ' + st.locBang.slice(3);
        return '— ' + timTrangThai(st.locBang).ten;
    }

    function veBang(ds) {
        var q = $('tkdgTim').value.trim().toLowerCase();
        var loc = st.locBang;
        var rows = ds.filter(function (nv) {
            if (loc === 'duoi_chuan' && !duoiChuan(nv)) return false;
            if (loc.indexOf('xl:') === 0 && nv.xepLoai !== loc.slice(3)) return false;
            if (loc && loc !== 'duoi_chuan' && loc.indexOf('xl:') !== 0 && timTrangThai(nv.trangThai).ma !== loc) return false;
            if (!q) return true;
            return (nv.maNv + ' ' + nv.hoTen).toLowerCase().indexOf(q) >= 0;
        });
        // Dưới chuẩn nặng nhất lên đầu khi đang xem nhóm dưới chuẩn
        if (loc === 'duoi_chuan') rows.sort(function (a, b) { return parseFloat(a.chenhLech) - parseFloat(b.chenhLech); });

        var than = $('tkdgBangThan');
        than.textContent = '';
        rows.forEach(function (nv) {
            var tt = timTrangThai(nv.trangThai);
            var xl = XEP_LOAI.find(function (x) { return x.ten === nv.xepLoai; });
            var gap = coSo(nv.chenhLech) ? parseFloat(nv.chenhLech) : null;
            than.appendChild(el('tr', null, [
                el('td', null, [el('a', { href: '/QLCongViec/DanhGia/ChiTiet/' + encodeURIComponent(nv.maNv), text: nv.maNv })]),
                el('td', { text: nv.hoTen }),
                el('td', { text: nv.phongBan || '--' }),
                el('td', null, [nv.tenNhomCongViec || '--', el('div', { class: 'tkdg-phu', text: nv.capBac || '' })]),
                el('td', { class: 'so', text: fmt(nv.diemKpi) }),
                el('td', { class: 'so', text: fmt(nv.diemNangLuc) }),
                el('td', { class: 'so ' + (gap === null ? '' : gap < 0 ? 'tkdg-gap-am' : 'tkdg-gap-duong'), text: gap === null ? '--' : (gap > 0 ? '+' : '') + fmt(gap) }),
                el('td', null, [nv.xepLoai ? el('span', { class: 'tkdg-the', style: '--the-c:' + (xl ? xl.mau : '#94a3b8'), text: nv.xepLoai }) : '--']),
                el('td', null, [el('span', { class: 'tkdg-the', style: '--the-c:' + tt.mau, text: tt.ten })])
            ]));
        });
        $('tkdgBangTrong').hidden = rows.length > 0;
        $('tkdgMoTaLoc').textContent = moTaLoc() + ' (' + rows.length + ' người)';
        document.querySelectorAll('.tkdg-kpi').forEach(function (k) {
            k.classList.toggle('dang-chon', !!loc && k.getAttribute('data-loc') === loc);
        });
    }

    function veTatCa() {
        var ds = dsLoc();
        veTrangThai(veKpi(ds));
        vePhongBan(ds);
        veXepLoai(ds);
        veNhom(ds);
        veChuaXong(ds);
        veBang(ds);
    }

    // Bấm lại cùng một bộ lọc thì bỏ lọc
    function chonLoc(loc) {
        st.locBang = st.locBang === loc ? '' : loc;
        veBang(dsLoc());
    }

    function taiDuLieu() {
        $('tkdgDangTai').hidden = false;
        baoLoi('');
        fetch(KyDanhGia.thamSo('/QLCongViec/DanhGia/QuanLy/DanhSach'), { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được dữ liệu.');
                st.ds = kq.duLieu || [];
                napLuaChon('tkdgPhongBan', duyNhat(phongBan));
                napLuaChon('tkdgNhom', duyNhat(nhom));
                napLuaChon('tkdgCapBac', duyNhat(function (nv) { return nv.capBac || ''; }).filter(Boolean));
                veTatCa();
            })
            .catch(function (err) { baoLoi(err.message); })
            .then(function () { $('tkdgDangTai').hidden = true; });
    }

    document.addEventListener('change', function (e) {
        if (['tkdgPhongBan', 'tkdgNhom', 'tkdgCapBac'].indexOf(e.target.id) >= 0) veTatCa();
    });
    document.addEventListener('input', function (e) {
        if (e.target.id === 'tkdgTim') veBang(dsLoc());
    });
    document.addEventListener('click', function (e) {
        var kpi = e.target.closest('.tkdg-kpi');
        if (kpi) { chonLoc(kpi.getAttribute('data-loc')); return; }
        if (e.target.closest('#tkdgLamMoi')) {
            $('tkdgPhongBan').value = $('tkdgNhom').value = $('tkdgCapBac').value = $('tkdgTim').value = '';
            st.locBang = '';
            taiDuLieu();
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        if (window.Chart && window.ChartDataLabels) Chart.register(ChartDataLabels);
        KyDanhGia.khoiTao('tkdgKy', taiDuLieu);
    });
})();
