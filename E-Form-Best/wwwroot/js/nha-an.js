// Phân hệ Nhà ăn (/NhaAn): bảng thông báo thực đơn hôm nay + cả tuần; quản lý sửa thực đơn.
// Mọi thao tác qua fetch, không tải lại trang; DOM dựng bằng createElement + textContent.
(function () {
    'use strict';

    var app = document.getElementById('na-app');
    if (!app) return;
    var laQuanLy = app.dataset.quanLy === '1';
    var THU = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];

    var trangThai = { tab: 'tuan', tuNgay: null, duLieu: null };

    // ---------- Tiện ích ----------
    function el(tag, lop, chu) {
        var e = document.createElement(tag);
        if (lop) e.className = lop;
        if (chu != null) e.textContent = chu;
        return e;
    }
    function icon(ten) { return el('i', 'fa ' + ten); }
    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }
    function docNgay(s) { var p = s.split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function vietNgay(d) {
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    function ddmm(s) { var p = s.split('-'); return p[2] + '/' + p[1]; }
    function ddmmyyyy(s) { var p = s.split('-'); return p[2] + '/' + p[1] + '/' + p[0]; }
    function congNgay(s, n) { var d = docNgay(s); d.setDate(d.getDate() + n); return vietNgay(d); }
    function thuCua(s) { return THU[docNgay(s).getDay()]; }

    var henToast;
    function toast(chu, loi) {
        var t = document.getElementById('na-toast');
        t.textContent = chu;
        t.classList.toggle('loi', !!loi);
        t.hidden = false;
        clearTimeout(henToast);
        henToast = setTimeout(function () { t.hidden = true; }, loi ? 5000 : 2500);
    }

    async function goiApi(url, duLieu) {
        var tuyChon = { headers: { 'Accept': 'application/json' } };
        if (duLieu !== undefined) {
            tuyChon.method = 'POST';
            tuyChon.headers['Content-Type'] = 'application/json';
            tuyChon.headers['RequestVerificationToken'] = token();
            tuyChon.body = JSON.stringify(duLieu);
        }
        var res;
        try { res = await fetch(url, tuyChon); }
        catch (e) { return { thanhCong: false, thongBao: 'Mất kết nối máy chủ, thử lại sau.' }; }
        if (!res.ok) return { thanhCong: false, thongBao: 'Máy chủ trả lỗi ' + res.status + '.' };
        try { return await res.json(); }
        catch (e) { return { thanhCong: false, thongBao: 'Phản hồi không hợp lệ (có thể phiên đăng nhập đã hết).' }; }
    }

    function khoaNut(nut, khoa) {
        if (!nut) return;
        nut.disabled = khoa;
        nut.classList.toggle('na-dang-gui', khoa);
    }

    function hienTrangThai(khung, chu, loi) {
        khung.replaceChildren(el('div', 'na-trang-thai' + (loi ? ' loi' : ''), chu));
    }

    function timThucDon(ngay, ca) {
        return trangThai.duLieu.thucDon.find(function (o) { return o.ngay === ngay && o.ca === ca; }) || null;
    }

    function dsMon(td, lop) {
        if (!td) return el('div', 'na-mon-trong', 'Chưa có thực đơn');
        var ul = el('ul', lop || 'na-mon');
        td.monAn.split('\n').forEach(function (m) { ul.appendChild(el('li', null, m)); });
        return ul;
    }

    // ---------- Tab ----------
    function chonTab(tab) {
        if (!document.querySelector('[data-pane="' + tab + '"]')) tab = 'tuan';
        trangThai.tab = tab;
        document.querySelectorAll('.na-tab').forEach(function (b) { b.classList.toggle('active', b.dataset.tab === tab); });
        document.querySelectorAll('.na-pane').forEach(function (p) { p.hidden = p.dataset.pane !== tab; });
        var tvQl = document.getElementById('na-tac-vu-quan-ly');
        if (tvQl) tvQl.hidden = tab !== 'quanly';
        // Thanh chọn tuần chỉ dùng cho Thực đơn / Sửa thực đơn
        document.getElementById('na-thanh-tuan').hidden = tab !== 'tuan' && tab !== 'quanly';
        if (history.replaceState) history.replaceState(null, '', tab === 'tuan' ? location.pathname : '#' + tab);
        ve();
    }

    // ---------- Tải tuần ----------
    async function taiTuan(tuNgay) {
        var luoi = document.getElementById('na-luoi-tuan');
        hienTrangThai(luoi, 'Đang tải thực đơn…');
        var kq = await goiApi('/NhaAn/Tuan' + (tuNgay ? '?tuNgay=' + encodeURIComponent(tuNgay) : ''));
        if (!kq.thanhCong) {
            hienTrangThai(luoi, kq.thongBao || 'Không tải được thực đơn.', true);
            return;
        }
        trangThai.duLieu = kq.duLieu;
        trangThai.tuNgay = kq.duLieu.tuNgay;
        capNhatNhanTuan();
        ve();
    }

    function capNhatNhanTuan() {
        var d = trangThai.duLieu;
        var hn = docNgay(d.homNay);
        var dauTuanHomNay = congNgay(d.homNay, -((hn.getDay() + 6) % 7));
        var lech = Math.round((docNgay(d.tuNgay) - docNgay(dauTuanHomNay)) / 86400000 / 7);
        var tieuDe = lech === 0 ? 'Tuần này' : lech === 1 ? 'Tuần sau' : lech === -1 ? 'Tuần trước' : 'Tuần ' + ddmm(d.tuNgay);
        document.getElementById('na-tuan-tieu-de').textContent = tieuDe;
        document.getElementById('na-tuan-khoang').textContent = ddmm(d.tuNgay) + ' – ' + ddmmyyyy(d.denNgay);
    }

    function ve() {
        if (!trangThai.duLieu) return;
        veHomNay();
        if (trangThai.tab === 'tuan') veLuoiTuan();
        if (trangThai.tab === 'quanly') veBangQuanLy();
    }

    // ---------- Hôm nay ----------
    function veHomNay() {
        var d = trangThai.duLieu;
        var khung = document.getElementById('na-hom-nay');
        // Cố định: luôn hiện thực đơn hôm nay dù đang xem tuần nào (server trả riêng thucDonHomNay)
        khung.hidden = trangThai.tab !== 'tuan';
        if (khung.hidden) return;

        var dau = el('div', 'na-hn-dau');
        var trai = el('div');
        trai.appendChild(el('div', 'na-hn-nhan', 'Thực đơn hôm nay'));
        trai.appendChild(el('div', 'na-hn-ngay', thuCua(d.homNay) + ', ' + ddmmyyyy(d.homNay)));
        dau.append(trai, el('div', 'na-hn-bieu-tuong', '🍲'));

        var luoi = el('div', 'na-hn-luoi');
        d.dsCa.forEach(function (ca) {
            var td = d.thucDonHomNay.find(function (x) { return x.ca === ca.ma; }) || null;
            var o = el('div', 'na-hn-bua' + (td ? '' : ' trong'));
            var tieuDe = el('div', 'na-hn-bua-ten');
            tieuDe.appendChild(el('span', null, ca.ten));
            if (ca.gioAn) tieuDe.appendChild(el('span', 'na-hn-gio', ca.gioAn));
            o.append(tieuDe, dsMon(td, 'na-hn-mon'));
            if (td && td.ghiChu) o.appendChild(el('div', 'na-ghi-chu-bua', td.ghiChu));
            luoi.appendChild(o);
        });
        khung.replaceChildren(dau, luoi);
    }

    // ---------- Cả tuần ----------
    function veLuoiTuan() {
        var d = trangThai.duLieu;
        var luoi = document.getElementById('na-luoi-tuan');
        var frag = document.createDocumentFragment();
        for (var i = 0; i < 7; i++) {
            var ngay = congNgay(d.tuNgay, i);
            var the = el('article', 'na-ngay');
            if (ngay === d.homNay) the.classList.add('hom-nay');
            if (ngay < d.homNay) the.classList.add('da-qua');

            var dau = el('div', 'na-ngay-dau');
            var thu = el('span', 'na-thu', thuCua(ngay));
            if (ngay === d.homNay) thu.appendChild(el('span', 'na-nhan-hom-nay', 'Hôm nay'));
            dau.append(thu, el('span', 'na-ngay-so', ddmm(ngay)));
            the.appendChild(dau);

            d.dsCa.forEach(function (ca) {
                var td = timThucDon(ngay, ca.ma);
                var bua = el('div', 'na-bua');
                var bd = el('div', 'na-bua-dau');
                bd.appendChild(el('span', 'na-bua-ten', ca.ten + (ca.gioAn ? ' · ' + ca.gioAn : '')));
                bua.append(bd, dsMon(td));
                if (td && td.ghiChu) bua.appendChild(el('div', 'na-ghi-chu-bua', td.ghiChu));
                the.appendChild(bua);
            });
            frag.appendChild(the);
        }
        luoi.replaceChildren(frag);
    }

    // ---------- Sửa thực đơn (quản lý) ----------
    function veBangQuanLy() {
        var d = trangThai.duLieu;
        var bang = document.getElementById('na-bang-quan-ly');
        if (!bang) return;
        var tr = el('tr');
        tr.appendChild(el('th', null, 'Bữa'));
        for (var i = 0; i < 7; i++) {
            var ngay = congNgay(d.tuNgay, i);
            tr.appendChild(el('th', ngay === d.homNay ? 'hom-nay' : null, thuCua(ngay) + ' · ' + ddmm(ngay)));
        }
        var thead = el('thead');
        thead.appendChild(tr);

        var tbody = el('tbody');
        d.dsCa.forEach(function (ca) {
            var hang = el('tr');
            var thCa = el('th', null, ca.ten);
            if (ca.gioAn) thCa.appendChild(el('small', null, ca.gioAn));
            hang.appendChild(thCa);
            for (var i = 0; i < 7; i++) hang.appendChild(veOSua(congNgay(d.tuNgay, i), ca.ma));
            tbody.appendChild(hang);
        });
        bang.replaceChildren(thead, tbody);
    }

    function veOSua(ngay, ca) {
        var td = timThucDon(ngay, ca);
        var o = el('td');
        var khung = el('div', 'na-o-sua');
        khung.dataset.ngay = ngay;
        khung.dataset.ca = ca;
        var ta = el('textarea');
        ta.name = 'monAn';
        ta.maxLength = 1000;
        ta.placeholder = 'Mỗi dòng một món…';
        ta.value = td ? td.monAn : '';
        var gc = el('input');
        gc.name = 'ghiChu';
        gc.maxLength = 500;
        gc.placeholder = 'Ghi chú (tuỳ chọn)';
        gc.value = td && td.ghiChu ? td.ghiChu : '';
        var chan = el('div', 'na-o-chan');
        chan.appendChild(el('span'));
        var nut = el('button', 'na-nut-nho na-nut-xanh');
        nut.type = 'button';
        nut.dataset.luuThucDon = '1';
        nut.append(icon('fa-save'), document.createTextNode(' Lưu'));
        chan.appendChild(nut);
        khung.append(ta, gc, chan);
        o.appendChild(khung);
        return o;
    }

    async function luuThucDon(nut) {
        var khung = nut.closest('.na-o-sua');
        var ngay = khung.dataset.ngay, ca = khung.dataset.ca;
        var cu = timThucDon(ngay, ca);
        var monAn = khung.querySelector('[name="monAn"]').value;
        var ghiChu = khung.querySelector('[name="ghiChu"]').value;
        if (!monAn.trim() && cu && !confirm('Xoá thực đơn ô này?')) return;
        var loiCu = khung.querySelector('.na-o-loi');
        if (loiCu) loiCu.remove();
        khoaNut(nut, true);
        var kq = await goiApi('/NhaAn/ThucDon', { ngay: ngay, ca: ca, monAn: monAn, ghiChu: ghiChu });
        khoaNut(nut, false);
        if (!kq.thanhCong) {
            khung.appendChild(el('div', 'na-o-loi', kq.thongBao));
            return;
        }
        // Đồng bộ dữ liệu đã chuẩn hoá (bỏ dòng trống) để tab Thực đơn hiển thị đúng
        var chuan = monAn.split('\n').map(function (x) { return x.trim(); }).filter(Boolean).join('\n');
        var ds = trangThai.duLieu.thucDon;
        if (cu) ds.splice(ds.indexOf(cu), 1);
        if (chuan) ds.push({ ngay: ngay, ca: ca, monAn: chuan, ghiChu: ghiChu.trim() || null });
        // Sửa đúng ngày hôm nay → cập nhật luôn thẻ "Hôm nay"
        if (ngay === trangThai.duLieu.homNay) {
            var hn = trangThai.duLieu.thucDonHomNay.filter(function (x) { return x.ca !== ca; });
            if (chuan) hn.push({ ca: ca, monAn: chuan, ghiChu: ghiChu.trim() || null });
            trangThai.duLieu.thucDonHomNay = hn;
        }
        khung.querySelector('[name="monAn"]').value = chuan;
        if (!chuan) khung.querySelector('[name="ghiChu"]').value = '';
        khung.classList.remove('da-doi');
        toast(kq.thongBao);
    }

    async function chepTuan(nut) {
        if (!confirm('Chép thực đơn tuần trước sang các ô còn trống của tuần này?')) return;
        khoaNut(nut, true);
        var kq = await goiApi('/NhaAn/SaoChepTuan', { tuNgay: trangThai.tuNgay });
        khoaNut(nut, false);
        toast(kq.thongBao, !kq.thanhCong);
        if (kq.thanhCong) taiTuan(trangThai.tuNgay);
    }

    // ---------- Sự kiện chung (delegation) ----------
    document.addEventListener('click', function (e) {
        var t = e.target;
        var tab = t.closest('.na-tab');
        if (tab) { chonTab(tab.dataset.tab); return; }
        var nutTuan = t.closest('[data-tuan]');
        if (nutTuan) {
            if (document.querySelector('.na-o-sua.da-doi') && !confirm('Còn ô thực đơn chưa lưu, vẫn chuyển tuần?')) return;
            var buoc = +nutTuan.dataset.tuan;
            taiTuan(buoc === 0 ? null : congNgay(trangThai.tuNgay, buoc * 7));
            return;
        }
        var nut;
        if ((nut = t.closest('[data-luu-thuc-don]'))) { luuThucDon(nut); return; }
        if ((nut = t.closest('#na-chep-tuan'))) { chepTuan(nut); return; }
    });

    document.addEventListener('input', function (e) {
        var khung = e.target.closest('.na-o-sua');
        if (khung) khung.classList.add('da-doi');
    });

    document.addEventListener('keydown', function (e) {
        // Ctrl+Enter trong ô thực đơn = lưu ô đó
        if (e.key === 'Enter' && e.ctrlKey) {
            var khung = e.target.closest('.na-o-sua');
            if (khung) luuThucDon(khung.querySelector('[data-luu-thuc-don]'));
        }
    });

    // Rời trang khi còn ô thực đơn chưa lưu → hỏi lại
    window.addEventListener('beforeunload', function (e) {
        if (document.querySelector('.na-o-sua.da-doi')) { e.preventDefault(); e.returnValue = ''; }
    });

    var tabDau = location.hash.replace('#', '');
    chonTab(tabDau === 'datdoan' || (laQuanLy && tabDau === 'quanly') ? tabDau : 'tuan');
    taiTuan(null);
})();
