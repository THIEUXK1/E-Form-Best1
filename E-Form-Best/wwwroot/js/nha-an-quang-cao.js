// Tab "Đặt đồ ăn" (/NhaAn#datdoan): trưng bày danh sách món từ window.NHA_AN_DANH_SACH_MON (nha-an-mon.js).
// Chỉ hiển thị phía trình duyệt, không gọi server. DOM dựng bằng createElement + textContent.
(function () {
    'use strict';

    var duLieu = window.NHA_AN_DANH_SACH_MON;
    var khung = document.getElementById('na-qc-noi-dung');
    if (!duLieu || !khung) return;

    var trangThai = { loai: 'comSuat', tuKhoa: '', nhom: null };

    function el(tag, lop, chu) {
        var e = document.createElement(tag);
        if (lop) e.className = lop;
        if (chu != null) e.textContent = chu;
        return e;
    }
    // Bỏ dấu tiếng Việt để tìm "ga" ra "gà"
    function chuan(s) {
        return (s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').trim();
    }

    // Biểu tượng theo tên nhóm (đã bỏ dấu); không khớp thì dùng 🍽️
    var BIEU_TUONG = {
        'thit lon': '🥓', 'suon': '🍖', 'gio cha': '🌭', 'thit ga': '🍗', 'thit vit': '🦆', 'tom/ tep': '🦐', 'tom': '🦐',
        'ca': '🐟', 'thit bo': '🥩', 'bo': '🥩', 'trung': '🥚', 'dau': '🧈', 'khoai tay': '🥔', 'cu cai': '🥕',
        'bap cai': '🥬', 'ca tim': '🍆', 'bi do': '🎃', 'bi xanh': '🍈', 'gia do': '🌱', 'ngo': '🌽', 'dua chuot': '🥒',
        'mung toi': '🌿', 'rau can': '🌿', 'salad': '🥗', 'soup': '🍲', 'pho': '🍜', 'ngo/khoai chien': '🍟',
        'cu qua': '🥕', 'lac': '🥜', 'so': '🦪', 'nem/ cha': '🥟', 'cuon': '🌯', 'muc': '🦑', 'de': '🐐',
        'com/mi': '🍚', 'mon xao': '🥦', 'canh': '🍲', 'trang mieng': '🍉'
    };
    function bieuTuong(ten) {
        var k = chuan(ten);
        if (BIEU_TUONG[k]) return BIEU_TUONG[k];
        if (k.indexOf('cai') === 0) return '🥬';
        return '🍽️';
    }
    // Màu nền thẻ xoay vòng theo nhóm cho bắt mắt
    var MAU = ['#fff1e6', '#fdf2d8', '#e9f7ef', '#e8f1fd', '#fbe9f1', '#f0ecfd', '#e6f6f6', '#fdecea'];

    // Chuẩn hoá về danh sách nhóm phẳng: { ten, tenTrung, phan, mon[] }
    function dsNhom(loai) {
        if (loai === 'comSuat') return duLieu.comSuat.map(function (n) { return { ten: n.ten, tenTrung: n.tenTrung, mon: n.mon }; });
        var kq = [];
        duLieu.comKhach.forEach(function (p) {
            p.nhom.forEach(function (n) {
                kq.push({ ten: n.ten || p.ten, tenTrung: n.tenTrung || p.tenTrung, phan: p.ten, phanTrung: p.tenTrung, mon: n.mon });
            });
        });
        return kq;
    }

    function demMon(loai) { return dsNhom(loai).reduce(function (s, n) { return s + n.mon.length; }, 0); }

    function veSoLieu() {
        document.getElementById('na-qc-thang').textContent = 'Thực đơn tháng ' + duLieu.thang;
        var o = document.getElementById('na-qc-so-lieu');
        o.replaceChildren();
        [[demMon('comSuat'), 'món cơm suất'], [demMon('comKhach'), 'món cơm khách'], [duLieu.comSuat.length, 'nhóm nguyên liệu']]
            .forEach(function (x) {
                var d = el('div', 'na-qc-so');
                d.append(el('b', null, String(x[0])), el('span', null, x[1]));
                o.appendChild(d);
            });
    }

    function veChip(ds) {
        var chip = document.getElementById('na-qc-chip');
        chip.replaceChildren();
        var tatCa = el('button', 'na-qc-chip-nut' + (trangThai.nhom == null ? ' active' : ''), 'Tất cả');
        tatCa.type = 'button';
        tatCa.dataset.qcNhom = '';
        chip.appendChild(tatCa);
        ds.forEach(function (n, i) {
            var b = el('button', 'na-qc-chip-nut' + (trangThai.nhom === i ? ' active' : ''), bieuTuong(n.ten) + ' ' + n.ten);
            b.type = 'button';
            b.dataset.qcNhom = String(i);
            chip.appendChild(b);
        });
    }

    function veMon() {
        var ds = dsNhom(trangThai.loai);
        var tk = chuan(trangThai.tuKhoa);
        var frag = document.createDocumentFragment();
        var phanTruoc = null, soHien = 0;

        ds.forEach(function (n, i) {
            if (trangThai.nhom != null && trangThai.nhom !== i) return;
            var mon = n.mon.filter(function (m) {
                return !tk || chuan(m.vi).indexOf(tk) >= 0 || (m.zh || '').indexOf(trangThai.tuKhoa.trim()) >= 0 || chuan(n.ten).indexOf(tk) >= 0;
            });
            if (!mon.length) return;
            soHien += mon.length;

            // Cơm khách: tiêu đề phần (Khai vị, Món chính…) khi đổi phần
            if (n.phan && n.phan !== phanTruoc) {
                phanTruoc = n.phan;
                var tp = el('h2', 'na-qc-phan', n.phan);
                if (n.phanTrung) tp.appendChild(el('small', null, n.phanTrung));
                frag.appendChild(tp);
            }

            var muc = el('section', 'na-qc-nhom');
            var dau = el('div', 'na-qc-nhom-dau');
            dau.appendChild(el('span', 'na-qc-nhom-icon', bieuTuong(n.ten)));
            var ten = el('div', 'na-qc-nhom-ten', n.ten);
            if (n.tenTrung) ten.appendChild(el('small', null, n.tenTrung));
            dau.append(ten, el('span', 'na-qc-nhom-dem', mon.length + ' món'));
            muc.appendChild(dau);

            var luoi = el('div', 'na-qc-luoi');
            mon.forEach(function (m, j) {
                var the = el('article', 'na-qc-the');
                the.style.setProperty('--na-qc-nen', MAU[(i + j) % MAU.length]);
                the.appendChild(el('div', 'na-qc-the-anh', bieuTuong(n.ten)));
                var chu = el('div', 'na-qc-the-chu');
                chu.appendChild(el('div', 'na-qc-the-ten', m.vi));
                if (m.zh) chu.appendChild(el('div', 'na-qc-the-trung', m.zh));
                the.appendChild(chu);
                if (/^(canh|soup)/i.test(m.vi)) the.appendChild(el('span', 'na-qc-nhan-nho', 'Canh'));
                luoi.appendChild(the);
            });
            muc.appendChild(luoi);
            frag.appendChild(muc);
        });

        if (!soHien) frag.appendChild(el('div', 'na-trang-thai', 'Không tìm thấy món nào khớp "' + trangThai.tuKhoa + '".'));
        khung.replaceChildren(frag);
    }

    function veLai() { veChip(dsNhom(trangThai.loai)); veMon(); }

    document.addEventListener('click', function (e) {
        var loai = e.target.closest('[data-qc-loai]');
        if (loai) {
            trangThai.loai = loai.dataset.qcLoai;
            trangThai.nhom = null;
            document.querySelectorAll('[data-qc-loai]').forEach(function (b) { b.classList.toggle('active', b === loai); });
            veLai();
            return;
        }
        var nhom = e.target.closest('[data-qc-nhom]');
        if (nhom) {
            trangThai.nhom = nhom.dataset.qcNhom === '' ? null : +nhom.dataset.qcNhom;
            veLai();
        }
    });

    var henTim;
    document.getElementById('na-qc-tim').addEventListener('input', function (e) {
        clearTimeout(henTim);
        var gt = e.target.value;
        henTim = setTimeout(function () { trangThai.tuKhoa = gt; veMon(); }, 150);
    });

    veSoLieu();
    veLai();
})();
