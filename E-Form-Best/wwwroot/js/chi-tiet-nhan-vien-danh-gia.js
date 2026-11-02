// Trang Chi tiết nhân viên (/QLCongViec/DanhGia/ChiTiet/{maNv}) — chuyển từ /manager/employees/[id] của app evaluation.
// Chỉ đọc, bố cục theo mẫu Excel 2025 (Part 1/2/3): dùng chung API DuLieuDanhGia với trang Đánh giá nhân viên.
// Biểu đồ Chart.js (CDN đã dùng ở các trang khác).
(function () {
    'use strict';

    var MAU_YC = '#6b7280', MAU_TU = '#2563eb', MAU_QL = '#059669';

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

    function so(v) { var n = parseFloat(v); return isNaN(n) ? 0 : n; }
    function fmt(n) { return (Math.round(n * 10) / 10).toFixed(1); }
    function coGT(v) { return v !== null && v !== undefined; }

    function lopXepLoai(x) {
        if (!x) return '';
        if (x.indexOf('Vượt') === 0) return 'vuot';
        if (x.indexOf('Đạt') === 0) return 'dat';
        if (x.indexOf('Cần') === 0) return 'can';
        return 'chua';
    }

    // "Lập trình code / Script" → ["Lập trình code", "/ Script"]: Chart.js nhận mảng là nhiều dòng
    function tachDong(ten) {
        if (!ten || ten.length <= 18) return ten;
        var tu = ten.split(' '), d1 = [], d2 = [];
        tu.forEach(function (t) { (d1.join(' ').length + t.length < ten.length / 2 + 2 ? d1 : d2).push(t); });
        return d2.length ? [d1.join(' '), d2.join(' ')] : ten;
    }

    function tai() {
        var ma = $('ctnv').getAttribute('data-ma-nv');
        fetch('/QLCongViec/DanhGia/QuanLy/DuLieuDanhGia?maNv=' + encodeURIComponent(ma), { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được dữ liệu.');
                ve(kq.duLieu);
                $('ctnvTrangThai').hidden = true;
                $('ctnvNoiDung').hidden = false;
            })
            .catch(function (e) {
                var o = $('ctnvTrangThai');
                o.classList.add('tdg-error');
                o.textContent = e.message;
            });
    }

    function ve(d) {
        var tu = d.phieuTu, ql = d.phieuQuanLy, t = d.thongTin || {};
        var urlDanhGia = '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(d.maNv);
        var urlHoSo = '/QLCongViec/DanhGia/NhanVien';
        var dsKpi = d.dsKpi || [], dsTc = d.dsTieuChi || [];

        // --- Thông tin ---
        $('ctnvTen').textContent = t.hoTen || d.maNv;
        $('ctnvMa').textContent = 'Employee ID: ' + d.maNv;
        $('ctnvChucVu').textContent = t.chucVu || '—';
        $('ctnvPhongBan').textContent = t.phongBan || '—';
        var daTu = !!(tu && tu.ngayNop), daQl = !!(ql && ql.ngayNop);
        var tt = daTu && daQl ? ['completed', 'Hoàn thành'] : daTu ? ['awaiting_manager', 'Chờ quản lý'] : daQl ? ['awaiting_self', 'Chờ tự đánh giá'] : ['not_started', 'Chưa đánh giá'];
        $('ctnvTrangThaiPhieu').appendChild(el('span', { class: 'tdg-badge ' + tt[0], text: tt[1] }));
        var chinh = daQl ? ql : (tu || ql);
        $('ctnvTong').textContent = chinh && coGT(chinh.diemThucTe) ? fmt(so(chinh.diemThucTe)) : '—';
        $('ctnvXepLoai').appendChild(chinh && chinh.xepLoai ? el('span', { class: 'tdg-xl ' + lopXepLoai(chinh.xepLoai), text: chinh.xepLoai }) : el('span', { class: 'text-muted', text: 'Chưa đánh giá' }));
        $('ctnvDanhGia').href = urlDanhGia;
        $('ctnvExcel').href = '/QLCongViec/DanhGia/QuanLy/Excel?maNv=' + encodeURIComponent(d.maNv);

        // --- Dữ liệu còn thiếu, kèm chỗ sửa ---
        var thieu = [];
        if (!tu) thieu.push(['Nhân viên chưa tự đánh giá', null, null]);
        else if (!daTu) thieu.push(['Nhân viên chưa gửi bản tự đánh giá', null, null]);
        if (!daQl) thieu.push(['Quản lý chưa gửi đánh giá', 'Đánh giá', urlDanhGia]);
        if (!dsKpi.length) thieu.push(['Chưa được giao nhiệm vụ Part 1', 'Giao KPI', urlHoSo]);
        if (!dsTc.length) thieu.push(['Chưa có tiêu chí năng lực Part 2', 'Thêm tiêu chí', urlHoSo]);
        var ds = $('ctnvDsThieu');
        thieu.forEach(function (x) {
            ds.appendChild(el('li', null, ['• ' + x[0], x[1] ? el('a', { class: 'btn btn-sm btn-outline-secondary', href: x[2], text: x[1] }) : null]));
        });
        $('ctnvThieu').hidden = !thieu.length;

        // --- Part 2: % điểm tối đa từng tiêu chí (tiêu chí khác điểm tối đa nên quy về % để so được) ---
        var phanTram = function (v, max) { return coGT(v) && so(max) > 0 ? Math.round(so(v) / so(max) * 1000) / 10 : 0; };
        if (dsTc.length && window.Chart) {
            var net = function (mau, nen) {
                return { borderColor: mau, backgroundColor: nen, borderWidth: 3, pointRadius: 4, pointBorderWidth: 2, pointBackgroundColor: '#fff', pointBorderColor: mau };
            };
            new Chart($('ctnvRadar'), {
                type: dsTc.length >= 3 ? 'radar' : 'bar',
                data: {
                    labels: dsTc.map(function (x) { return tachDong(x.ten); }),
                    datasets: [
                        Object.assign({ label: 'Quản lý đánh giá', data: dsTc.map(function (x) { return phanTram(x.diemQuanLy, x.diemToiDa); }) }, net(MAU_QL, 'rgba(5,150,105,0.10)')),
                        Object.assign({ label: 'Tự đánh giá', data: dsTc.map(function (x) { return phanTram(x.diemTu, x.diemToiDa); }) }, net(MAU_TU, 'rgba(37,99,235,0.10)')),
                        Object.assign({ label: 'Chuẩn (60%)', data: dsTc.map(function () { return 60; }) }, net(MAU_YC, 'rgba(107,114,128,0.05)'))
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    scales: dsTc.length >= 3 ? {
                        r: {
                            min: 0, max: 100,
                            ticks: { stepSize: 20, backdropColor: 'transparent', font: { size: 10 }, color: '#94a3b8' },
                            pointLabels: { font: { size: 12 }, color: '#475569' },
                            grid: { color: '#e5e7eb' },
                            angleLines: { color: '#e5e7eb' }
                        }
                    } : { y: { min: 0, max: 100 } },
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', boxWidth: 8, padding: 16, font: { size: 12 } } },
                        tooltip: { callbacks: { title: function (it) { var l = it[0].label; return Array.isArray(l) ? l.join(' ') : l; }, label: function (it) { return it.dataset.label + ': ' + it.formattedValue + '%'; } } }
                    }
                }
            });
        } else {
            $('ctnvRadar').replaceWith(el('div', { class: 'tdg-empty', text: 'Chưa có dữ liệu đồ thị' }));
        }

        // --- Part 1 ---
        if (dsKpi.length && window.Chart) {
            new Chart($('ctnvKpiChart'), {
                type: 'bar',
                data: {
                    labels: dsKpi.map(function (k, i) { return 'NV ' + (i + 1); }),
                    datasets: [
                        { label: 'Tự đánh giá', data: dsKpi.map(function (k) { return so(k.mucTuDanhGia); }), backgroundColor: MAU_TU },
                        { label: 'Quản lý', data: dsKpi.map(function (k) { return so(k.mucQuanLy); }), backgroundColor: MAU_QL }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    scales: { y: { beginAtZero: true, max: 5, ticks: { stepSize: 1 } } },
                    plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { title: function (it) { return dsKpi[it[0].dataIndex].congViec; } } } }
                }
            });
        } else {
            $('ctnvKpiChart').replaceWith(el('div', { class: 'tdg-empty', text: 'Chưa có dữ liệu KPI' }));
        }

        var mucDiem = function (muc, trongSo) { return coGT(muc) ? muc + ' / ' + fmt(so(trongSo) * muc / 5) : '—'; };
        var bang = $('ctnvBangKpi');
        if (!dsKpi.length) bang.appendChild(el('tr', null, [el('td', { colspan: 7, class: 'text-muted', text: 'Chưa có nhiệm vụ Part 1' })]));
        dsKpi.forEach(function (k) {
            var urlMc = '/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?maNv=' + encodeURIComponent(d.maNv);
            var anh = !k.coAnh ? document.createTextNode('—')
                : k.laAnh ? el('img', { src: urlMc, alt: 'Ảnh minh chứng', 'data-mo-anh': '1' })
                : el('a', { href: urlMc, target: '_blank', rel: 'noopener', text: 'Tài liệu' });
            bang.appendChild(el('tr', null, [
                el('td', { text: k.congViec }),
                el('td', { text: k.chiSoKpi || '—' }),
                el('td', { text: k.ketQua || '—' }),
                el('td', { text: fmt(so(k.trongSo)) }),
                el('td', { text: mucDiem(k.mucTuDanhGia, k.trongSo) }),
                el('td', { text: mucDiem(k.mucQuanLy, k.trongSo) }),
                el('td', null, [anh])
            ]));
        });

        // --- Part 2 ---
        var bangTc = $('ctnvBangTc');
        if (!dsTc.length) bangTc.appendChild(el('tr', null, [el('td', { colspan: 5, class: 'text-muted', text: 'Chưa có tiêu chí năng lực' })]));
        dsTc.forEach(function (x) {
            bangTc.appendChild(el('tr', null, [
                el('td', { text: x.ten }),
                el('td', { text: x.tomTat || x.moTa || '—' }),
                el('td', { text: fmt(so(x.diemToiDa)) }),
                el('td', { text: coGT(x.diemTu) ? fmt(so(x.diemTu)) : '—' }),
                el('td', { text: coGT(x.diemQuanLy) ? fmt(so(x.diemQuanLy)) : '—' })
            ]));
        });

        // --- Summary ---
        var tong = $('ctnvTongHop');
        [['Part I (50)', 'diemKpi'], ['Part II (50)', 'diemNangLuc'], ['Total (100)', 'diemThucTe'], ['So với chuẩn 60', 'chenhLech']].forEach(function (r) {
            tong.appendChild(el('tr', null, [el('td', { text: r[0] }),
                el('td', { text: tu && coGT(tu[r[1]]) ? fmt(so(tu[r[1]])) : '—' }),
                el('td', { text: ql && coGT(ql[r[1]]) ? fmt(so(ql[r[1]])) : '—' })]));
        });
        tong.appendChild(el('tr', null, [el('td', { text: 'Xếp loại' }), el('td', { text: (tu && tu.xepLoai) || '—' }), el('td', { text: (ql && ql.xepLoai) || '—' })]));
        tong.appendChild(el('tr', null, [el('td', { text: 'Tổng trọng số Part 1' }), el('td', { colspan: 2, text: fmt(dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0)) + ' / 50' })]));

        // --- Part 3 ---
        var p3 = tu || {};
        [['ctnvDiemManh', 'diemManh'], ['ctnvDiemYeu', 'diemYeu'], ['ctnvLoTrinh', 'loTrinh'], ['ctnvKeHoach', 'keHoach']].forEach(function (x) {
            $(x[0]).textContent = p3[x[1]] || '—';
        });
        $('ctnvNhanXet').textContent = (ql && ql.nhanXet) || '—';
    }

    document.addEventListener('click', function (e) {
        var anh = e.target.closest('#ctnv [data-mo-anh]');
        if (anh) window.open(anh.getAttribute('src'), '_blank', 'noopener');
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', tai);
    else tai();
})();
