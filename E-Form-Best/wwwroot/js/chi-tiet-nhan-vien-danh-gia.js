// Trang Chi tiết nhân viên (/QLCongViec/DanhGia/ChiTiet/{maNv}) — chuyển từ /manager/employees/[id] của app evaluation.
// Chỉ đọc: dùng chung API DuLieuDanhGia với trang Đánh giá nhân viên. Biểu đồ Chart.js (CDN đã dùng ở các trang khác).
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

    function gapBadge(g) {
        return el('span', { class: 'tdg-gap ' + (g > 0 ? 'duong' : g < 0 ? 'am' : 'bang'), text: (g > 0 ? '+' : '') + fmt(g) });
    }

    // "Lập trình code / Script" → ["Lập trình code", "/ Script"]: Chart.js nhận mảng là nhiều dòng
    function tachDong(ten) {
        if (!ten || ten.length <= 18) return ten;
        var tu = ten.split(' '), d1 = [], d2 = [];
        tu.forEach(function (t) { (d1.join(' ').length + t.length < ten.length / 2 + 2 ? d1 : d2).push(t); });
        return d2.length ? [d1.join(' '), d2.join(' ')] : ten;
    }

    function chuanDiem(map) {
        var kq = {};
        Object.keys(map || {}).forEach(function (k) { kq[k.toLowerCase()] = so(map[k]); });
        return kq;
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
        var tu = d.phieuTu, ql = d.phieuQuanLy;
        var diemTu = chuanDiem(d.diemTu), diemQl = chuanDiem(d.diemQuanLy);
        var urlDanhGia = '/QLCongViec/DanhGia/DanhGiaNhanVien?maNv=' + encodeURIComponent(d.maNv);
        var urlHoSo = '/QLCongViec/DanhGia/NhanVien';

        // --- Thông tin ---
        $('ctnvTen').textContent = d.hoTen;
        $('ctnvMa').textContent = d.maNv + (d.phongBan ? ' • ' + d.phongBan : '');
        $('ctnvCapBac').textContent = d.hoSo.capBac + (d.hoSo.dayDu ? '' : ' (mặc định)');
        $('ctnvNhom').textContent = d.hoSo.tenNhomCongViec || 'Chưa gán';
        var daTu = !!(tu && tu.ngayNop), daQl = !!(ql && ql.ngayNop);
        var tt = daTu && daQl ? ['completed', 'Hoàn thành'] : daTu ? ['awaiting_manager', 'Chờ quản lý'] : daQl ? ['awaiting_self', 'Chờ tự đánh giá'] : ['not_started', 'Chưa đánh giá'];
        $('ctnvTrangThaiPhieu').appendChild(el('span', { class: 'tdg-badge ' + tt[0], text: tt[1] }));
        var chinh = daQl ? ql : (tu || ql);
        $('ctnvGap').appendChild(chinh && coGT(chinh.chenhLech) ? gapBadge(so(chinh.chenhLech)) : el('span', { class: 'text-muted', text: '—' }));
        $('ctnvXepLoai').appendChild(chinh && chinh.xepLoai ? el('span', { class: 'tdg-xl ' + lopXepLoai(chinh.xepLoai), text: chinh.xepLoai }) : el('span', { class: 'text-muted', text: 'Chưa đánh giá' }));
        $('ctnvDanhGia').href = urlDanhGia;
        $('ctnvExcel').href = '/QLCongViec/DanhGia/QuanLy/Excel?maNv=' + encodeURIComponent(d.maNv);

        // --- Dữ liệu còn thiếu, kèm chỗ sửa ---
        var thieu = [];
        if (!d.hoSo.dayDu) thieu.push(['Chưa gán đủ cấp bậc / nhóm công việc', 'Sửa hồ sơ', urlHoSo]);
        if (!tu) thieu.push(['Nhân viên chưa tự đánh giá', null, null]);
        else if (!daTu) thieu.push(['Nhân viên chưa gửi bản tự đánh giá', null, null]);
        if (!daQl) thieu.push(['Quản lý chưa gửi đánh giá', 'Đánh giá', urlDanhGia]);
        if (!(d.dsNhomNangLuc || []).length) thieu.push(['Chưa có ma trận năng lực cho cấp bậc ' + d.boLoc.capBac, null, null]);
        if (!(d.dsKpi || []).length) thieu.push(['Chưa được giao KPI', 'Giao KPI', urlHoSo]);
        var ds = $('ctnvDsThieu');
        thieu.forEach(function (t) {
            ds.appendChild(el('li', null, ['• ' + t[0], t[1] ? el('a', { class: 'btn btn-sm btn-outline-secondary', href: t[2], text: t[1] }) : null]));
        });
        $('ctnvThieu').hidden = !thieu.length;

        // --- Radar năng lực ---
        var nhan = [], yc = [], dTu = [], dQl = [];
        (d.dsNhomNangLuc || []).forEach(function (g) {
            g.dsNangLuc.forEach(function (n) {
                var id = n.idNangLuc.toLowerCase();
                // Nhãn trục là tên năng lực như app cũ; tên dài tách 2 dòng cho đỡ chồng chữ
                nhan.push(tachDong(n.ten));
                yc.push(so(n.diemYeuCau));
                // App cũ coi năng lực chưa chấm = 0 (đường vẫn khép kín)
                dTu.push(diemTu[id] || 0);
                dQl.push(diemQl[id] || 0);
            });
        });
        if (nhan.length && window.Chart) {
            var net = function (mau, nen) {
                return { borderColor: mau, backgroundColor: nen, borderWidth: 3, pointRadius: 4, pointBorderWidth: 2, pointBackgroundColor: '#fff', pointBorderColor: mau };
            };
            new Chart($('ctnvRadar'), {
                type: 'radar',
                data: {
                    labels: nhan,
                    // Thứ tự chú thích giống app cũ: Quản lý → Tự đánh giá → Điểm yêu cầu
                    datasets: [
                        Object.assign({ label: 'Quản lý đánh giá', data: dQl }, net(MAU_QL, 'rgba(5,150,105,0.10)')),
                        Object.assign({ label: 'Tự đánh giá', data: dTu }, net(MAU_TU, 'rgba(37,99,235,0.10)')),
                        Object.assign({ label: 'Điểm yêu cầu', data: yc }, net(MAU_YC, 'rgba(107,114,128,0.05)'))
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    scales: {
                        r: {
                            min: 0, max: 10,
                            ticks: { stepSize: 2, backdropColor: 'transparent', font: { size: 10 }, color: '#94a3b8' },
                            pointLabels: { font: { size: 12 }, color: '#475569' },
                            grid: { color: '#e5e7eb' },
                            angleLines: { color: '#e5e7eb' }
                        }
                    },
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', boxWidth: 8, padding: 16, font: { size: 12 } } },
                        tooltip: { callbacks: { title: function (it) { var l = it[0].label; return Array.isArray(l) ? l.join(' ') : l; } } }
                    }
                }
            });
        } else {
            $('ctnvRadar').replaceWith(el('div', { class: 'tdg-empty', text: 'Chưa có dữ liệu đồ thị' }));
        }

        // --- KPI ---
        var dsKpi = d.dsKpi || [];
        if (dsKpi.length && window.Chart) {
            new Chart($('ctnvKpiChart'), {
                type: 'bar',
                data: {
                    labels: dsKpi.map(function (k, i) { return 'KPI ' + (i + 1); }),
                    datasets: [
                        { label: 'Tự đánh giá', data: dsKpi.map(function (k) { return so(k.diemTuDanhGia); }), backgroundColor: MAU_TU },
                        { label: 'Quản lý', data: dsKpi.map(function (k) { return so(k.diemQuanLy); }), backgroundColor: MAU_QL }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    scales: { y: { beginAtZero: true, suggestedMax: 120 } },
                    plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { title: function (it) { return dsKpi[it[0].dataIndex].congViec; } } } }
                }
            });
        } else {
            $('ctnvKpiChart').replaceWith(el('div', { class: 'tdg-empty', text: 'Chưa có dữ liệu KPI' }));
        }

        var bang = $('ctnvBangKpi');
        if (!dsKpi.length) bang.appendChild(el('tr', null, [el('td', { colspan: 7, class: 'text-muted', text: 'Chưa có dữ liệu KPI chi tiết' })]));
        dsKpi.forEach(function (k) {
            var anh = k.coAnh
                ? el('img', { src: '/QLCongViec/DanhGia/AnhKpi/' + k.idKpiMau + '?maNv=' + encodeURIComponent(d.maNv), alt: 'Ảnh minh chứng', 'data-mo-anh': '1' })
                : document.createTextNode('—');
            bang.appendChild(el('tr', null, [
                el('td', { text: k.congViec }),
                el('td', { text: k.chiSoKpi || '—' }),
                el('td', { text: k.ketQua || '—' }),
                el('td', { text: fmt(so(k.trongSo)) }),
                el('td', { text: coGT(k.diemTuDanhGia) ? fmt(so(k.diemTuDanhGia)) : '—' }),
                el('td', { text: coGT(k.diemQuanLy) ? fmt(so(k.diemQuanLy)) : '—' }),
                el('td', null, [anh])
            ]));
        });

        // --- Tổng hợp ---
        var tong = $('ctnvTongHop');
        [['Điểm KPI', 'diemKpi'], ['Điểm năng lực', 'diemNangLuc'], ['Tổng yêu cầu', 'tongYeuCau'], ['Chênh lệch', 'chenhLech']].forEach(function (r) {
            tong.appendChild(el('tr', null, [el('td', { text: r[0] }),
                el('td', { text: tu && coGT(tu[r[1]]) ? fmt(so(tu[r[1]])) : '—' }),
                el('td', { text: ql && coGT(ql[r[1]]) ? fmt(so(ql[r[1]])) : '—' })]));
        });
        tong.appendChild(el('tr', null, [el('td', { text: 'Xếp loại' }), el('td', { text: (tu && tu.xepLoai) || '—' }), el('td', { text: (ql && ql.xepLoai) || '—' })]));
        tong.appendChild(el('tr', null, [el('td', { text: 'Tổng trọng số KPI' }), el('td', { colspan: 2, text: fmt(dsKpi.reduce(function (s, k) { return s + so(k.trongSo); }, 0)) })]));

        // --- Chi tiết tiêu chí ---
        var hop = $('ctnvNangLuc');
        if (!(d.dsNhomNangLuc || []).length) hop.appendChild(el('div', { class: 'tdg-empty', text: 'Chưa có dữ liệu đánh giá chi tiết.' }));
        (d.dsNhomNangLuc || []).forEach(function (g) {
            var than = el('div', { class: 'tdg-group-body' });
            g.dsNangLuc.forEach(function (n) {
                var id = n.idNangLuc.toLowerCase();
                var y = so(n.diemYeuCau);
                function o(lop, nhan, v) {
                    return el('div', { class: 'tdg-ct-o ' + lop }, [el('span', { text: nhan }), el('b', { text: coGT(v) ? fmt(v) : '--' }),
                        coGT(v) && lop ? el('div', null, [gapBadge(v - y)]) : null]);
                }
                than.appendChild(el('div', { class: 'tdg-ct-nl' }, [
                    el('div', { class: 'tdg-nl-ten', title: n.moTa || '' }, [el('b', { text: n.ma }), n.ten]),
                    el('div', { class: 'tdg-ct-ba' }, [o('', 'Yêu cầu', y), o('tu', 'Tự đánh giá', diemTu[id]), o('ql', 'Quản lý đánh giá', diemQl[id])])
                ]));
            });
            hop.appendChild(el('div', { class: 'tdg-group' }, [
                el('button', { type: 'button', class: 'tdg-group-head', 'data-mo-nhom': '1' }, [el('span', { text: g.ten }), el('i', { class: 'fa fa-chevron-up' })]),
                than
            ]));
        });
    }

    document.addEventListener('click', function (e) {
        var moNhom = e.target.closest('#ctnv [data-mo-nhom]');
        if (moNhom) { moNhom.parentNode.classList.toggle('dong'); return; }
        var anh = e.target.closest('#ctnv [data-mo-anh]');
        if (anh) window.open(anh.getAttribute('src'), '_blank', 'noopener');
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', tai);
    else tai();
})();
