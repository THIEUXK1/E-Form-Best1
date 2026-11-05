// Trang chấm năng lực dùng chung: /QLCongViec/DanhGia/NangLucCuaToi (nhân viên tự chấm) và
// /QLCongViec/DanhGia/NangLuc/{maNv} (quản lý chấm). data-ma-nv rỗng = chế độ tự chấm.
(function () {
    'use strict';

    var MAU_QL = '#16a34a', MAU_TU = '#2563eb', MAU_YC = '#6b7280';
    var st = { ma: '', laTuCham: true, d: null, khoa: false, dangLuu: false, bieuDo: null };

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

    function coGT(v) { return v !== null && v !== undefined && v !== ''; }
    function fmt(n) { return (Math.round(n * 100) / 100).toString(); }
    function fmtGap(n) { return (n > 0 ? '+' : '') + fmt(n); }
    function lop(gap) { return gap >= 1 ? 'vuot' : gap >= 0 ? 'dat' : gap >= -1 ? 'cai' : 'chua'; }

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

    function tai() {
        var tt = $('cnlTrangThai');
        tt.hidden = false;
        tt.classList.remove('tdg-error');
        tt.textContent = 'Đang tải dữ liệu...';
        $('cnlNoiDung').hidden = true;
        var url = st.laTuCham ? '/QLCongViec/DanhGia/NangLucApi/CuaToi'
            : '/QLCongViec/DanhGia/NangLucApi/NhanVien?maNv=' + encodeURIComponent(st.ma);
        goiApi(KyDanhGia.thamSo(url), { headers: { 'Accept': 'application/json' } })
            .then(function (kq) {
                nap(kq.duLieu);
                tt.hidden = true;
                $('cnlNoiDung').hidden = false;
            })
            .catch(function (e) {
                tt.classList.add('tdg-error');
                tt.textContent = e.message;
            });
    }

    function nap(d) {
        st.d = d;
        st.khoa = !d.ky.dangMo || (st.laTuCham ? d.daChot : d.laChinhMinh);
        veThongTin();
        veBang();
        veBieuDo();
    }

    function veThongTin() {
        var d = st.d, k = d.ketQua;
        $('cnlTen').textContent = d.hoTen || d.maNv;
        $('cnlMa').textContent = 'Employee ID: ' + d.maNv;
        $('cnlNhom').textContent = k.tenNhomCongViec || 'Chưa gán';
        $('cnlCapBac').textContent = k.capBac || 'Chưa gán';
        $('cnlDaCham').textContent = k.soDaCham + '/' + k.dsNangLuc.length;
        $('cnlThang').textContent = fmt(d.thangToiDa);
        var o = $('cnlGap');
        o.textContent = '';
        o.appendChild(el('span', { class: 'nl-o ' + (coGT(k.tbGap) ? lop(k.tbGap) : ''), text: coGT(k.tbGap) ? fmtGap(k.tbGap) : '—' }));
        $('cnlXepLoai').textContent = k.xepLoai || (d.ky.thangCu ? '—' : st.laTuCham && d.ky.dangMo ? 'Chờ quản lý chấm xong' : 'Chưa chấm');

        var tb = !d.ky.dangMo ? (d.ky.thangCu ? 'Kỳ này chấm theo thang cũ nên không tính GAP và xếp loại, số liệu chỉ để tham khảo.' : 'Kỳ này đã đóng, chỉ xem.')
            : st.laTuCham && d.daChot ? 'Phiếu đã được quản lý đánh giá xong, không sửa được nữa.'
            : !st.laTuCham && d.laChinhMinh ? 'Đây là năng lực của chính bạn — chỉ xem, không tự chấm phiếu quản lý.'
            : '';
        $('cnlThongBao').hidden = !tb;
        $('cnlThongBaoChu').textContent = tb;
        $('cnlThanhLuu').hidden = st.khoa || !k.dsNangLuc.length;
        $('cnlLoi').textContent = '';
    }

    function gapDong(nl, oDiem) {
        if (st.d.ky.thangCu) return null;
        // Tự chấm khi chưa thấy điểm quản lý: GAP tính theo điểm tự chấm để nhân viên tự đối chiếu
        var diem = st.laTuCham ? (coGT(nl.diemQuanLy) ? nl.diemQuanLy : oDiem) : oDiem;
        return coGT(diem) ? parseFloat(diem) - nl.yeuCau : null;
    }

    function veOGap(o, gap) {
        o.textContent = '';
        o.appendChild(el('span', { class: 'nl-o ' + (gap === null ? '' : lop(gap)), text: gap === null ? '—' : fmtGap(gap) }));
    }

    function oNhap(nl, giaTri) {
        var o = el('input', { type: 'number', class: 'form-control form-control-sm', min: 0, max: st.d.thangToiDa, step: 1,
            'data-nl': nl.idNangLuc, 'aria-label': 'Điểm ' + nl.ten });
        o.value = coGT(giaTri) ? fmt(giaTri) : '';
        return o;
    }

    function veBang() {
        var ds = st.d.ketQua.dsNangLuc;
        var tb = $('cnlBang');
        tb.textContent = '';
        $('cnlBangBoc').hidden = !ds.length;
        var trong = $('cnlTrong');
        trong.hidden = ds.length > 0;
        trong.textContent = st.d.ky.dangMo ? 'Chưa có khung năng lực — cần gán cấp bậc và nhóm công việc ở “Hồ sơ & KPI”.' : 'Không có điểm năng lực trong kỳ này.';
        var nhomTruoc = null;
        ds.forEach(function (nl) {
            if (nl.tenNhom !== nhomTruoc) {
                nhomTruoc = nl.tenNhom;
                tb.appendChild(el('tr', { class: 'nl-dong-nhom' }, [el('td', { colspan: '6', text: nl.tenNhom })]));
            }
            var moTa = nl.moTa ? el('span', { class: 'nl-mo-ta', text: nl.moTa }) : null;
            var oGap = el('td', { 'data-gap': nl.idNangLuc });
            var nhapTu = st.laTuCham && !st.khoa, nhapQl = !st.laTuCham && !st.khoa;
            tb.appendChild(el('tr', null, [
                el('td', { text: nl.ma }),
                el('td', null, [el('span', { class: 'nl-ten', text: nl.ten }), moTa]),
                el('td', { text: fmt(nl.yeuCau) }),
                nhapTu ? el('td', null, [oNhap(nl, nl.diemTu)]) : el('td', { text: coGT(nl.diemTu) ? fmt(nl.diemTu) : '—' }),
                nhapQl ? el('td', null, [oNhap(nl, nl.diemQuanLy)]) : el('td', { text: coGT(nl.diemQuanLy) ? fmt(nl.diemQuanLy) : '—' }),
                oGap
            ]));
            veOGap(oGap, gapDong(nl, st.laTuCham ? nl.diemTu : nl.diemQuanLy));
        });
    }

    function veBieuDo() {
        if (st.bieuDo) { st.bieuDo.destroy(); st.bieuDo = null; }
        var ds = st.d.ketQua.theoNhom || [];
        var co = ds.length > 0 && !!window.Chart;
        $('cnlRadar').hidden = !co;
        $('cnlRadarTrong').hidden = co;
        if (!co) return;
        var bo = function (nhan, mau, nen, truong) {
            return { label: nhan, data: ds.map(function (x) { return coGT(x[truong]) ? x[truong] : null; }), borderColor: mau, backgroundColor: nen,
                borderWidth: 3, pointRadius: 4, pointBackgroundColor: '#fff', pointBorderColor: mau, spanGaps: true };
        };
        var tron = ds.length >= 3;
        var thang = st.d.thangToiDa;
        st.bieuDo = new Chart($('cnlRadar'), {
            type: tron ? 'radar' : 'bar',
            data: {
                labels: ds.map(function (x) { return x.tenNhom; }),
                datasets: [
                    bo('Quản lý chấm', MAU_QL, tron ? 'rgba(22,163,74,0.12)' : MAU_QL, 'thucTe'),
                    bo('Tự chấm', MAU_TU, tron ? 'rgba(37,99,235,0.08)' : MAU_TU, 'tuCham'),
                    bo('Yêu cầu', MAU_YC, tron ? 'rgba(107,114,128,0.05)' : MAU_YC, 'yeuCau')
                ]
            },
            options: {
                maintainAspectRatio: false,
                scales: tron
                    ? { r: { min: 0, max: thang, ticks: { stepSize: 2, backdropColor: 'transparent', color: '#94a3b8' }, pointLabels: { font: { size: 12 }, color: '#475569' } } }
                    : { y: { min: 0, max: thang } },
                plugins: { legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8, padding: 14 } } }
            }
        });
    }

    function luu() {
        if (st.dangLuu || st.khoa) return;
        var dsDiem = [];
        document.querySelectorAll('#cnlBang [data-nl]').forEach(function (o) {
            dsDiem.push({ idNangLuc: o.getAttribute('data-nl'), diem: o.value === '' ? null : parseFloat(o.value) });
        });
        st.dangLuu = true;
        var nut = $('cnlLuu');
        nut.disabled = true;
        $('cnlLoi').textContent = '';
        goiApi(st.laTuCham ? '/QLCongViec/DanhGia/NangLucApi/LuuTuCham' : '/QLCongViec/DanhGia/NangLucApi/LuuQuanLy', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify({ maNv: st.ma || null, idKy: st.d.ky.idKy, dsDiem: dsDiem })
        })
            .then(function (kq) {
                nap(kq.duLieu);
                Swal.fire({ icon: 'success', text: kq.thongBao, timer: 1400, showConfirmButton: false });
            })
            // Lỗi hiện tại chỗ, giữ nguyên số đã nhập
            .catch(function (e) { $('cnlLoi').textContent = e.message; })
            .finally(function () { st.dangLuu = false; nut.disabled = false; });
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('#cnlLuu')) luu();
    });

    document.addEventListener('input', function (e) {
        var t = e.target;
        if (!t.matches('#cnlBang [data-nl]')) return;
        var id = t.getAttribute('data-nl');
        var nl = st.d.ketQua.dsNangLuc.filter(function (x) { return x.idNangLuc === id; })[0];
        var o = document.querySelector('#cnlBang [data-gap="' + CSS.escape(id) + '"]');
        if (nl && o) veOGap(o, gapDong(nl, t.value));
    });

    function batDau() {
        st.ma = $('cnl').getAttribute('data-ma-nv') || '';
        st.laTuCham = !st.ma;
        KyDanhGia.khoiTao('cnlKy', tai);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', batDau);
    else batDau();
})();
