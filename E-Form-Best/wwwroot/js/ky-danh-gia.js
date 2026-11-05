// Ô chọn kỳ đánh giá dùng chung cho các trang /QLCongViec/DanhGia/*.
// KyDanhGia.khoiTao(idHop, cb): vẽ ô chọn vào #idHop, gọi cb(ky) lần đầu và mỗi khi đổi kỳ.
// Kỳ đang xem nhớ theo tab (sessionStorage) để chuyển trang không phải chọn lại.
(function () {
    'use strict';

    var KHOA_NHO = 'dgKyDangXem';
    var st = { ds: [], ky: null };

    function docNho() { try { return sessionStorage.getItem(KHOA_NHO) || ''; } catch (e) { return ''; } }
    function ghiNho(id) { try { sessionStorage.setItem(KHOA_NHO, id); } catch (e) { /* trình duyệt chặn lưu trữ: chỉ mất phần nhớ kỳ */ } }

    function ngayVn(s) {
        var p = String(s || '').split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : '';
    }

    function veGhiChu(hop) {
        var o = hop.querySelector('.ky-dg-ghi-chu');
        var k = st.ky;
        o.textContent = !k ? ''
            : k.dangMo ? (k.hanNop ? 'Hạn nộp ' + ngayVn(k.hanNop) : 'Đang mở')
            : k.thangCu ? 'Đã đóng • thang điểm cũ, chỉ xem' : 'Đã đóng • chỉ xem';
        o.className = 'ky-dg-ghi-chu ' + (k && k.dangMo ? 'mo' : 'dong');
    }

    function khoiTao(idHop, cb) {
        var hop = document.getElementById(idHop);
        hop.textContent = '';
        var nhan = document.createElement('label');
        nhan.textContent = 'Kỳ đánh giá';
        nhan.htmlFor = idHop + 'Chon';
        var sel = document.createElement('select');
        sel.id = idHop + 'Chon';
        sel.className = 'form-select form-select-sm';
        var ghiChu = document.createElement('span');
        ghiChu.className = 'ky-dg-ghi-chu';
        hop.appendChild(nhan);
        hop.appendChild(sel);
        hop.appendChild(ghiChu);

        return fetch('/QLCongViec/DanhGia/DsKy', { headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được danh sách kỳ.');
                st.ds = kq.duLieu || [];
                if (!st.ds.length) throw new Error('Chưa có kỳ đánh giá nào.');
                st.ds.forEach(function (k) {
                    var o = document.createElement('option');
                    o.value = k.idKy;
                    o.textContent = k.ten + (k.dangMo ? ' (đang mở)' : '');
                    sel.appendChild(o);
                });
                var nho = docNho();
                st.ky = st.ds.filter(function (k) { return k.idKy === nho; })[0] || st.ds[0];
                sel.value = st.ky.idKy;
                veGhiChu(hop);
                sel.addEventListener('change', function () {
                    st.ky = st.ds.filter(function (k) { return k.idKy === sel.value; })[0];
                    ghiNho(sel.value);
                    veGhiChu(hop);
                    cb(st.ky);
                });
                cb(st.ky);
            })
            .catch(function (e) {
                ghiChu.className = 'ky-dg-ghi-chu dong';
                ghiChu.textContent = e.message;
            });
    }

    window.KyDanhGia = {
        khoiTao: khoiTao,
        id: function () { return st.ky ? st.ky.idKy : ''; },
        // Thêm tham số kỳ vào url API / link tải
        thamSo: function (url) {
            return url + (url.indexOf('?') >= 0 ? '&' : '?') + 'idKy=' + encodeURIComponent(st.ky ? st.ky.idKy : '');
        },
        ngayVn: ngayVn
    };
})();
