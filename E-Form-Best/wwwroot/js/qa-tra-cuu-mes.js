// Trang QA tra cứu mã phối màu: gọi /QAForm/TraCuuMes/DuLieu (server tự gọi API NH-MES, gộp hết các trang),
// vẽ toàn bộ bảng 1 lần rồi phân trang phía trình duyệt (ẩn dòng ngoài trang bằng class); tích dòng -> Tạo thùng mới / Thêm vào thùng đang mở (POST /QAForm/Thung/BoVao). Không reload.
(function () {
    var $ = function (id) { return document.getElementById(id); };
    var SO_COT = 24;
    var maDangXem = '';   // mã phối màu (orderUcode) của kết quả đang hiện (dùng khi bỏ vào thùng)
    var locDangXem = null; // bộ lọc của kết quả đang hiện { ma, kh, hang }, null = chưa tra
    var dongHienTai = []; // dữ liệu dòng đang hiện, chỉ số = data-chi-so

    // ===== Phân trang =====
    // Vẫn vẽ đủ dòng vào tbody để lọc cột / sắp xếp kiểu Excel chạy trên toàn bộ kết quả và dòng đã tích
    // ở trang khác vẫn giữ; chỉ ẩn dòng ngoài trang bằng class qa-ngoai-trang (hidden là của lọc cột).
    var KHOA_CO_TRANG = 'qa-mes-co-trang';
    var trang = 1;
    var soDongLocTruoc = -1; // số dòng qua lọc lần trước: đổi bộ lọc thì về trang 1, vẽ lại cùng dữ liệu thì giữ trang
    var coTrang = (function () {
        var v = 0;
        try { v = parseInt(localStorage.getItem(KHOA_CO_TRANG), 10); } catch (e) { /* bỏ qua */ }
        return [50, 100, 200, 500].indexOf(v) >= 0 ? v : 100;
    })();
    var DONG_DANG_HIEN = '#qa-mes-tbody tr:not([hidden]):not(.qa-ngoai-trang)';

    function nutTrang(nhan, soTrang, tieuDe, dangXem, khoa) {
        var b = document.createElement('button');
        b.type = 'button';
        b.textContent = nhan;
        b.dataset.trang = soTrang;
        if (tieuDe) b.title = tieuDe;
        if (dangXem) b.className = 'dang-xem';
        b.disabled = !!(khoa || dangXem);
        return b;
    }

    function phanTrang() {
        var khung = $('qa-mes-phantrang');
        var ds = Array.prototype.filter.call($('qa-mes-tbody').rows, function (tr) { return tr.cells.length > 1; });
        var qua = ds.filter(function (tr) { return !tr.hidden; });
        var tong = qua.length;
        var soTrang = Math.max(1, Math.ceil(tong / coTrang));
        if (trang > soTrang) trang = soTrang;
        if (trang < 1) trang = 1;
        var dau = (trang - 1) * coTrang, cuoi = dau + coTrang;
        ds.forEach(function (tr) { tr.classList.remove('qa-ngoai-trang'); });
        qua.forEach(function (tr, i) { if (i < dau || i >= cuoi) tr.classList.add('qa-ngoai-trang'); });

        khung.hidden = ds.length === 0;
        $('qa-mes-trang-co').value = String(coTrang);
        $('qa-mes-trang-tt').textContent = tong === 0 ? 'Không có dòng nào'
            : 'Dòng ' + (dau + 1).toLocaleString('vi-VN') + '–' + Math.min(cuoi, tong).toLocaleString('vi-VN') + ' / ' + tong.toLocaleString('vi-VN')
              + ' · Trang ' + trang + '/' + soTrang;
        var nut = $('qa-mes-trang-nut');
        nut.replaceChildren();
        if (soTrang <= 1) return;
        nut.appendChild(nutTrang('«', 1, 'Trang đầu', false, trang === 1));
        nut.appendChild(nutTrang('‹', trang - 1, 'Trang trước', false, trang === 1));
        var tu = Math.max(1, trang - 2), den = Math.min(soTrang, trang + 2);
        function baCham() { var s = document.createElement('span'); s.className = 'ba-cham'; s.textContent = '…'; nut.appendChild(s); }
        if (tu > 1) { nut.appendChild(nutTrang('1', 1)); if (tu > 2) baCham(); }
        for (var t = tu; t <= den; t++) nut.appendChild(nutTrang(String(t), t, null, t === trang));
        if (den < soTrang) { if (den < soTrang - 1) baCham(); nut.appendChild(nutTrang(String(soTrang), soTrang)); }
        nut.appendChild(nutTrang('›', trang + 1, 'Trang sau', false, trang === soTrang));
        nut.appendChild(nutTrang('»', soTrang, 'Trang cuối', false, trang === soTrang));
    }

    function denTrang(t) {
        trang = t;
        phanTrang();
        capNhatChon();
        var w = document.querySelector('.qa-mes-bang-wrap');
        if (w) w.scrollTop = 0;
    }

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function thongBao(text, loai) {
        var el = $('qa-mes-thongbao');
        el.textContent = text || '';
        el.classList.toggle('loi', loai === 'loi');
        el.classList.toggle('ok', loai === 'ok');
    }

    function o(tr, giaTri, lop) {
        var td = document.createElement('td');
        td.textContent = giaTri === null || giaTri === undefined ? '' : giaTri;
        if (lop) td.className = lop;
        tr.appendChild(td);
        return td;
    }

    function ngay(s) { return s ? String(s).substring(0, 10) : ''; }

    function dongTrong(text) {
        var tr = document.createElement('tr');
        var td = document.createElement('td');
        td.colSpan = SO_COT;
        td.className = 'qa-mes-trong';
        td.textContent = text;
        tr.appendChild(td);
        return tr;
    }

    // Ô tích của dòng đang nằm trong thùng (tích để chuyển thùng / bỏ ra)
    function laTrongThung(cb) { return cb.dataset.trongThung === '1'; }

    // Khoá nhớ 1 dòng đã tích: dòng trong thùng theo id dòng thùng, dòng tự do theo dấu vân tay
    function khoaTich(cb) {
        var d = dongHienTai[cb.dataset.chiSo];
        return laTrongThung(cb) ? 'd:' + d.idDongThung : d.dauVanTay;
    }

    function dsDaChon() { return Array.prototype.slice.call(document.querySelectorAll('#qa-mes-tbody .qa-mes-chon-dong:checked')); }

    // Đếm dòng đã tích, đồng bộ ô "chọn tất cả" và bật/tắt nút thùng
    function capNhatChon() {
        // "Chọn tất cả" chỉ áp cho dòng chưa vào thùng — tránh vô tình kéo cả dòng đã đóng thùng
        // Chỉ tính dòng đang hiện (lọc cột kiểu Excel ẩn bớt dòng)
        var tatCa = document.querySelectorAll(DONG_DANG_HIEN + ' .qa-mes-chon-dong:not(:disabled):not([data-trong-thung])');
        var daChon = dsDaChon();
        var soTuDoChon = daChon.filter(function (cb) { return !laTrongThung(cb); }).length;
        var soTrongThung = daChon.length - soTuDoChon;
        var het = $('qa-mes-chon-het');
        het.disabled = tatCa.length === 0;
        het.checked = tatCa.length > 0 && soTuDoChon === tatCa.length;
        het.indeterminate = soTuDoChon > 0 && soTuDoChon < tatCa.length;

        var tongM = 0, tongKg = 0;
        daChon.forEach(function (cb) {
            var d = dongHienTai[cb.dataset.chiSo];
            tongM += Number(d.pbNum) || 0;
            tongKg += Number(d.pbNw) || 0;
        });
        $('qa-mes-dachon').textContent = daChon.length > 0
            ? 'Đã chọn ' + daChon.length.toLocaleString('vi-VN') + ' dòng · ' + tongM.toLocaleString('vi-VN') + ' m · ' + tongKg.toLocaleString('vi-VN', { maximumFractionDigits: 3 }) + ' kg'
                + (soTrongThung > 0 ? ' (⚠ ' + soTrongThung.toLocaleString('vi-VN') + ' dòng đang ở thùng)' : '')
            : '';
        $('qa-mes-dachon').classList.toggle('canh-bao', soTrongThung > 0);
        $('qa-thung-tao').disabled = daChon.length === 0;
        $('qa-thung-them').disabled = daChon.length === 0 || !$('qa-thung-chon').value;
        $('qa-thung-bora').disabled = soTrongThung === 0;

        document.querySelectorAll('#qa-mes-tbody tr').forEach(function (tr) {
            var cb = tr.querySelector('.qa-mes-chon-dong');
            var chon = !!(cb && cb.checked);
            tr.classList.toggle('qa-mes-dang-chon', chon && !laTrongThung(cb));
            tr.classList.toggle('qa-mes-chon-trong-thung', chon && laTrongThung(cb));
        });

        // Nhớ các dòng đang tích để F5 không mất (chỉ trong tab này)
        if (locDangXem) luuTich(daChon.map(khoaTich));
    }

    // tichLai: danh sách dấu vân tay cần tích lại sau khi vẽ (khôi phục sau F5)
    function veBang(dong, tichLai) {
        dongHienTai = dong || [];
        var tbody = $('qa-mes-tbody');
        if (dongHienTai.length === 0) {
            tbody.replaceChildren(dongTrong('Không tìm thấy dữ liệu cho mã phối màu này.'));
            phanTrang();
            capNhatChon();
            return;
        }
        // Dựng ngoài DOM rồi gắn 1 lần cho nhanh khi có hàng nghìn dòng
        var frag = document.createDocumentFragment();
        dongHienTai.forEach(function (d, i) {
            var tr = document.createElement('tr');
            if (d.pbLv && d.pbLv !== 'A') tr.classList.add('qa-mes-bat-thuong');
            if (d.maThung) tr.classList.add('qa-mes-da-vao-thung');

            var tdChon = document.createElement('td');
            tdChon.className = 'qa-mes-chon';
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.className = 'form-check-input qa-mes-chon-dong';
            cb.dataset.chiSo = i;
            // Dòng đã nằm trong thùng: vẫn tích được để chuyển thùng / bỏ ra (có hỏi xác nhận);
            // thùng đã đóng thì khoá, phải mở lại thùng trước
            if (d.maThung) {
                cb.dataset.trongThung = '1';
                if (d.thungDaDong) { cb.disabled = true; cb.title = 'Thùng ' + d.maThung + ' đã đóng — mở lại ở Danh sách thùng để chuyển/bỏ dòng'; }
                else cb.title = 'Đang ở thùng ' + d.maThung + ' — tích để chuyển thùng hoặc bỏ ra';
            }
            tdChon.appendChild(cb);
            tr.appendChild(tdChon);

            o(tr, i + 1, 'so');
            o(tr, d.maThung || '', d.maThung ? 'qa-mes-o-thung' : '');
            o(tr, d.viTri || '');
            o(tr, d.soBan, 'so');
            o(tr, d.endCustCode, 'qa-cot-phu');
            o(tr, d.orderUcode);
            o(tr, d.custCode);
            o(tr, d.artCode);
            o(tr, d.colorCode);
            o(tr, d.pmcNum, 'so');
            o(tr, d.pbNum, 'so');
            o(tr, d.pbNw, 'so qa-cot-phu');
            o(tr, d.remark, 'qa-cot-phu');
            o(tr, d.pbBoxnum, 'so');
            o(tr, d.pbLv);
            o(tr, d.pbQcdes, 'qa-cot-phu');
            o(tr, d.handleMethod, 'qa-cot-phu');
            o(tr, d.manuStep, 'qa-cot-phu');
            o(tr, d.clNo, 'qa-cot-phu');
            o(tr, ngay(d.getDate), 'qa-cot-phu');
            o(tr, ngay(d.pbDate));
            o(tr, d.packclNo, 'qa-cot-phu');
            o(tr, d.pbGh);
            frag.appendChild(tr);
        });
        tbody.replaceChildren(frag);
        if (tichLai && tichLai.length) {
            // Tích lại theo khoá (dấu vân tay có đếm vì nhiều dòng MES giống hệt; dòng trong thùng theo id)
            var conLai = {};
            tichLai.forEach(function (v) { conLai[v] = (conLai[v] || 0) + 1; });
            tbody.querySelectorAll('.qa-mes-chon-dong:not(:disabled)').forEach(function (cb) {
                var v = khoaTich(cb);
                if (conLai[v] > 0) { cb.checked = true; conLai[v]--; }
            });
        }
        phanTrang();
        capNhatChon();
    }

    // ===== Giữ kết quả khi F5 =====
    // Mã đang tra nằm trên địa chỉ (?ma=...) -> F5 / gửi link đều tự tra lại; dòng đã tích lưu ở sessionStorage.
    // Chỉ xoá khi tra mã khác hoặc bấm "Làm mới".
    // Để trống cả 3 ô = đang xem toàn bộ dữ liệu: nút Xuất Excel hỏi lại trước khi xuất (qa-xuat-excel.js đọc data-canh-bao)
    function datCanhBaoXuat(l) {
        var nut = document.querySelector('[data-xuat-excel="qa-mes-bang"]');
        if (!nut) return;
        if (l && !l.ma && !l.kh && !l.hang)
            nut.dataset.canhBao = 'Bạn đang xem TOÀN BỘ dữ liệu (không lọc mã phối màu / khách hàng / mã hàng).\n'
                + 'File Excel sẽ có {n} dòng, rất nhiều, có thể mất vài chục giây và máy chạy chậm.\n\n'
                + 'Vẫn xuất? (Muốn ít hơn: nhập điều kiện tra cứu hoặc lọc theo cột trước.)';
        else delete nut.dataset.canhBao;
    }

    var KHOA_TICH = 'qa-mes-tich';

    function khoaLoc(l) { return l ? [l.ma, l.kh, l.hang].join('|') : ''; }
    function docLoc() {
        return { ma: $('qa-mes-ma').value.trim(), kh: $('qa-mes-kh').value.trim(), hang: $('qa-mes-hang').value.trim() };
    }
    function ganLoc(l) {
        $('qa-mes-ma').value = l.ma || '';
        $('qa-mes-kh').value = l.kh || '';
        $('qa-mes-hang').value = l.hang || '';
    }
    function thamSoLoc(l) {
        return 'maPhoiMau=' + encodeURIComponent(l.ma) + '&maKhachHang=' + encodeURIComponent(l.kh) + '&maHang=' + encodeURIComponent(l.hang);
    }
    function luuTich(ds) {
        try { sessionStorage.setItem(KHOA_TICH, JSON.stringify({ loc: locDangXem, ds: ds })); } catch (e) { /* bỏ qua */ }
    }
    function docTich(loc) {
        try {
            var x = JSON.parse(sessionStorage.getItem(KHOA_TICH) || 'null');
            return x && x.loc && khoaLoc(x.loc) === khoaLoc(loc) ? x.ds : [];
        } catch (e) { return []; }
    }
    function xoaTich() {
        try { sessionStorage.removeItem(KHOA_TICH); } catch (e) { /* bỏ qua */ }
    }
    // Bộ lọc lên địa chỉ (?ma=&kh=&hang=) để F5 / gửi link tự tra lại
    function datMaLenDiaChi(l) {
        var url = new URL(window.location.href);
        [['ma', l && l.ma], ['kh', l && l.kh], ['hang', l && l.hang]].forEach(function (p) {
            if (p[1]) url.searchParams.set(p[0], p[1]); else url.searchParams.delete(p[0]);
        });
        history.replaceState(null, '', url.pathname + url.search);
    }

    async function layJson(url, tuyChon) {
        var res = await fetch(url, tuyChon || { headers: { 'Accept': 'application/json' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    // Nạp danh sách thùng đang mở cho hộp chọn "Thêm vào thùng"
    async function napThungMo(chonId) {
        try {
            var kq = await layJson('/QAForm/Thung/DanhSach?chiThungMo=true');
            var sel = $('qa-thung-chon');
            var giu = chonId ? String(chonId) : sel.value;
            sel.replaceChildren();
            var dau = document.createElement('option');
            dau.value = '';
            dau.textContent = kq.thanhCong ? '— Chọn thùng đang mở —' : '(không đọc được danh sách thùng)';
            sel.appendChild(dau);
            (kq.duLieu || []).forEach(function (t) {
                var op = document.createElement('option');
                op.value = t.idThung;
                op.textContent = t.maThung + ' · ' + t.soDong + ' dòng' + (t.ghiChu ? ' · ' + t.ghiChu : '');
                sel.appendChild(op);
            });
            sel.value = giu && sel.querySelector('option[value="' + giu + '"]') ? giu : '';
        } catch (e) { /* để trống hộp chọn, nút Tạo thùng mới vẫn dùng được */ }
        capNhatChon();
    }

    // giuTich = tích lại các dòng đã lưu (F5, tra lại cùng mã); false = bắt đầu chọn mới
    // lamMoi = true: nút "Cập nhật từ MES" — kéo thẳng MES và ghi đè bản đồng bộ trong DB
    var textTong = '';   // dòng tổng của lần tra gần nhất (chưa kèm phần "đang lọc")

    // Lọc cột kiểu Excel (qa-loc-cot.js): bỏ tích dòng vừa bị ẩn để không lỡ bỏ vào thùng, ghi số dòng đang hiện
    document.addEventListener('qa-loc-cot:doi', function (e) {
        if (e.target.id !== 'qa-mes-bang') return;
        document.querySelectorAll('#qa-mes-tbody tr[hidden] .qa-mes-chon-dong:checked').forEach(function (cb) { cb.checked = false; });
        if (e.detail.hien !== soDongLocTruoc) trang = 1;
        soDongLocTruoc = e.detail.hien;
        phanTrang();
        capNhatChon();
        var d = e.detail;
        $('qa-mes-tong').textContent = textTong + (window.qaLocCot && window.qaLocCot.dangLoc($('qa-mes-bang'))
            ? ' · Đang lọc cột: hiện ' + d.hien.toLocaleString('vi-VN') + '/' + d.tong.toLocaleString('vi-VN') + ' dòng' : '');
    });

    async function tra(loc, giuTich, lamMoi) {
        var tichLai = giuTich ? docTich(loc) : [];
        var nut = $(lamMoi ? 'qa-mes-capnhat' : 'qa-mes-nut');
        var nutKhac = $(lamMoi ? 'qa-mes-nut' : 'qa-mes-capnhat');
        nut.disabled = true;
        nutKhac.disabled = true;
        var nhanCu = nut.innerHTML;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> ' + (lamMoi ? (loc.ma ? 'Đang lấy từ MES...' : 'Đang đồng bộ 7 ngày...') : 'Đang tra...');
        thongBao('');
        try {
            var url = '/QAForm/TraCuuMes/' + (lamMoi ? 'LamMoi' : 'DuLieu') + '?' + thamSoLoc(loc);
            var kq = lamMoi
                ? await layJson(url, { method: 'POST', headers: { 'Accept': 'application/json', 'RequestVerificationToken': token() } })
                : await layJson(url);
            if (!kq.thanhCong) { thongBao(kq.thongBao || 'Tra cứu thất bại.', 'loi'); return; }
            var dl = kq.duLieu;
            if (kq.thongBao) thongBao(kq.thongBao, 'loi');
            else if (dl.dongBo) thongBao(dl.dongBo, 'ok');
            // Tra bộ lọc khác thì bỏ lọc cột cũ; tra lại cùng bộ lọc (sau khi bỏ vào thùng...) thì giữ
            if (khoaLoc(loc) !== khoaLoc(locDangXem)) {
                if (window.qaLocCot) window.qaLocCot.xoa($('qa-mes-bang'));
                trang = 1;
            }
            maDangXem = loc.ma;
            locDangXem = loc;
            datCanhBaoXuat(loc);
            datMaLenDiaChi(loc);
            veBang(dl.dong, tichLai);
            var soVaoThung = dl.dong.filter(function (d) { return d.maThung; }).length;
            var moTa = [loc.ma && 'phối màu "' + loc.ma + '"', loc.kh && 'khách "' + loc.kh + '"', loc.hang && 'mã hàng "' + loc.hang + '"'].filter(Boolean).join(', ');
            var text = 'Tổng ' + dl.tong.toLocaleString('vi-VN') + ' dòng' + (moTa ? ' cho ' + moTa : ' (tất cả mã)');
            if (soVaoThung > 0) text += ' — ' + soVaoThung.toLocaleString('vi-VN') + ' dòng đã vào thùng';
            if (dl.tong > dl.dong.length) text += ' — chỉ hiện ' + dl.dong.length.toLocaleString('vi-VN') + (loc.ma ? ' dòng đầu' : ' dòng đóng gói mới nhất') + ', nhập mã đầy đủ hơn để thu hẹp';
            if (dl.capNhatLuc) text += dl.nguon === 'DB' ? ' · dữ liệu đồng bộ lúc ' + dl.capNhatLuc : ' · vừa lấy từ MES lúc ' + dl.capNhatLuc;
            textTong = text;
            var bang = $('qa-mes-bang');
            if (window.qaLocCot && window.qaLocCot.dangLoc(bang)) {
                var hien = bang.querySelectorAll('#qa-mes-tbody tr:not([hidden])').length;
                text += ' · Đang lọc cột: hiện ' + hien.toLocaleString('vi-VN') + '/' + dl.dong.length.toLocaleString('vi-VN') + ' dòng';
            }
            $('qa-mes-tong').textContent = text;
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nut.disabled = false;
            nutKhac.disabled = false;
            nut.innerHTML = nhanCu;
        }
    }

    document.addEventListener('click', function (e) {
        if (!e.target.closest('#qa-mes-capnhat')) return;
        var loc = docLoc();
        if (loc.ma.length > 0 && loc.ma.length < 4) { thongBao('Mã phối màu cần ít nhất 4 ký tự (hoặc để trống để đồng bộ 7 ngày gần nhất).', 'loi'); return; }
        tra(loc, khoaLoc(loc) === khoaLoc(locDangXem), true);
    });

    // Bỏ các dòng đã tích vào thùng: idThung null = tạo thùng mới
    // Gom các dòng đang ở thùng theo mã thùng: { 'QA-...': soDong }
    function demTheoThung(dsCb) {
        var dem = {};
        dsCb.forEach(function (cb) { var m = dongHienTai[cb.dataset.chiSo].maThung; dem[m] = (dem[m] || 0) + 1; });
        return Object.keys(dem).map(function (m) { return '  • ' + m + ': ' + dem[m] + ' dòng'; }).join('\n');
    }

    async function guiPost(url, body) {
        return layJson(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify(body)
        });
    }

    function khoaNutThung(khoa) {
        ['qa-thung-tao', 'qa-thung-them', 'qa-thung-bora'].forEach(function (id) { if (khoa) $(id).disabled = true; });
    }

    async function boVaoThung(idThung, nut) {
        var chon = dsDaChon();
        if (chon.length === 0) return;
        var tuDo = chon.filter(function (cb) { return !laTrongThung(cb); });
        var trongThung = chon.filter(laTrongThung);

        // Có dòng đang ở thùng khác -> cảnh báo + xác nhận chuyển
        if (trongThung.length > 0) {
            var dich = idThung ? $('qa-thung-chon').selectedOptions[0].textContent.split(' · ')[0] : 'THÙNG MỚI';
            var tb = '⚠ ' + trongThung.length + ' dòng đã chọn ĐANG Ở THÙNG KHÁC và sẽ được CHUYỂN sang ' + dich + ':\n'
                + demTheoThung(trongThung) + '\n'
                + (tuDo.length > 0 ? '\nCùng ' + tuDo.length + ' dòng chưa vào thùng sẽ được thêm vào ' + dich + '.\n' : '')
                + '\nTiếp tục chuyển?';
            if (!confirm(tb)) return;
        }

        var nutKhac = [$('qa-thung-tao'), $('qa-thung-them'), $('qa-thung-bora')];
        nutKhac.forEach(function (b) { b.disabled = true; });
        var nhanCu = nut.innerHTML;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang lưu...';
        try {
            var kq = await guiPost('/QAForm/Thung/BoVao', {
                maPhoiMau: maDangXem,
                dsDauVanTay: tuDo.map(function (cb) { return dongHienTai[cb.dataset.chiSo].dauVanTay; }),
                dsIdDongChuyen: trongThung.map(function (cb) { return dongHienTai[cb.dataset.chiSo].idDongThung; }),
                idThung: idThung,
                ghiChu: idThung ? null : $('qa-thung-ghichu').value
            });
            if (!kq.thanhCong) { thongBao(kq.thongBao || 'Không lưu được.', 'loi'); return; }
            thongBao(kq.thongBao, 'ok');
            $('qa-thung-ghichu').value = '';
            // Tải lại kết quả để cột "Thùng" cập nhật + khoá các dòng vừa bỏ vào
            await napThungMo(kq.duLieu && kq.duLieu.idThung);
            await tra(locDangXem || docLoc(), false);
            thongBao(kq.thongBao, 'ok');
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nut.innerHTML = nhanCu;
            capNhatChon();
        }
    }

    // Bỏ các dòng đang ở thùng ra khỏi thùng (xoá mềm phía server, có lịch sử) — luôn hỏi xác nhận
    async function boRaKhoiThung(nut) {
        var chon = dsDaChon();
        var trongThung = chon.filter(laTrongThung);
        if (trongThung.length === 0) return;
        var boQua = chon.length - trongThung.length;
        var tb = '⚠ Bỏ ' + trongThung.length + ' dòng RA KHỎI THÙNG:\n' + demTheoThung(trongThung) + '\n\n'
            + 'Các dòng này sẽ trở lại trạng thái chưa vào thùng (lịch sử thùng vẫn ghi lại).'
            + (boQua > 0 ? '\n(' + boQua + ' dòng chưa vào thùng đang tích sẽ được bỏ qua.)' : '')
            + '\n\nXác nhận bỏ ra?';
        if (!confirm(tb)) return;

        khoaNutThung(true);
        var nhanCu = nut.innerHTML;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang bỏ...';
        try {
            var kq = await guiPost('/QAForm/Thung/BoRa', {
                dsIdDong: trongThung.map(function (cb) { return dongHienTai[cb.dataset.chiSo].idDongThung; })
            });
            if (!kq.thanhCong) { thongBao(kq.thongBao || 'Không bỏ ra được.', 'loi'); return; }
            await napThungMo();
            await tra(locDangXem || docLoc(), false);
            thongBao(kq.thongBao, 'ok');
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nut.innerHTML = nhanCu;
            capNhatChon();
        }
    }

    document.addEventListener('submit', function (e) {
        if (e.target.id !== 'qa-mes-form') return;
        e.preventDefault();
        var loc = docLoc();
        // Để trống cả 3 ô = xem tất cả (bản đồng bộ trong DB, mới nhất trước)
        if (loc.ma.length > 0 && loc.ma.length < 4) { thongBao('Mã phối màu cần ít nhất 4 ký tự (hoặc để trống).', 'loi'); return; }
        // Tra lại đúng bộ lọc đang xem thì giữ dòng đã tích, khác thì chọn lại từ đầu
        tra(loc, khoaLoc(loc) === khoaLoc(locDangXem));
    });

    // Nút "Làm mới": xoá kết quả, mã trên địa chỉ và các dòng đã tích
    document.addEventListener('click', function (e) {
        if (!e.target.closest('#qa-mes-lammoi')) return;
        maDangXem = '';
        locDangXem = null;
        datCanhBaoXuat(null);
        textTong = '';
        if (window.qaLocCot) window.qaLocCot.xoa($('qa-mes-bang'));
        dongHienTai = [];
        xoaTich();
        datMaLenDiaChi(null);
        ganLoc({});
        $('qa-thung-ghichu').value = '';
        $('qa-mes-tbody').replaceChildren(dongTrong('Chưa có dữ liệu.'));
        trang = 1;
        phanTrang();
        $('qa-mes-tong').textContent = 'Nhập mã phối màu / mã khách hàng / mã hàng rồi bấm Tra cứu (để trống cả 3 = xem tất cả).';
        thongBao('');
        capNhatChon();
        $('qa-mes-ma').focus();
    });

    document.addEventListener('change', function (e) {
        if (e.target.id === 'qa-mes-chon-het') {
            var chon = e.target.checked;
            document.querySelectorAll(DONG_DANG_HIEN + ' .qa-mes-chon-dong:not(:disabled):not([data-trong-thung])').forEach(function (cb) { cb.checked = chon; });
            capNhatChon();
        } else if (e.target.classList.contains('qa-mes-chon-dong') || e.target.id === 'qa-thung-chon') {
            capNhatChon();
        }
    });

    document.addEventListener('click', function (e) {
        if (e.target.closest('#qa-thung-tao')) { boVaoThung(null, $('qa-thung-tao')); return; }
        if (e.target.closest('#qa-thung-bora')) { boRaKhoiThung($('qa-thung-bora')); return; }
        if (e.target.closest('#qa-thung-them')) {
            var id = parseInt($('qa-thung-chon').value, 10);
            if (id) boVaoThung(id, $('qa-thung-them'));
            return;
        }
        // Tích bằng chuột đã xử lý ở mousedown (kéo bôi đen) -> huỷ lần đảo trạng thái mặc định của ô vuông,
        // nếu không ô vừa tích ở mousedown sẽ bị click đảo ngược lại. detail = 0 là phím Space: để mặc định.
        if (e.detail > 0 && e.target.classList.contains('qa-mes-chon-dong')) e.preventDefault();
    });

    // ===== Tích bằng kéo chuột (bôi đen) =====
    // Nhấn giữ trên 1 dòng rồi kéo qua các dòng khác: mọi dòng đi qua nhận cùng trạng thái với dòng đầu sau khi đảo
    // (dòng đầu chưa tích -> kéo để tích; đã tích -> kéo để bỏ tích). Shift + bấm: áp cho cả khoảng từ dòng bấm trước.
    var keo = null;          // { trangThai, dau, cuoi } khi đang kéo
    var chiSoNeo = null;     // dòng bấm gần nhất, làm mốc cho Shift + bấm
    var trangThaiNeo = true;
    var loaiNeo = false;     // dòng mốc có đang ở thùng không

    // Chỉ dòng đang hiện: kéo bôi đen không tích lan sang dòng đã bị lọc ẩn
    function dsDong() { return Array.prototype.slice.call(document.querySelectorAll(DONG_DANG_HIEN)); }
    function oTich(tr) { return tr ? tr.querySelector('.qa-mes-chon-dong') : null; }

    // Áp trạng thái cho mọi dòng trong khoảng [a, b] CÙNG LOẠI với dòng mốc: bắt đầu kéo ở dòng chưa vào thùng
    // thì chỉ tích dòng chưa vào thùng, bắt đầu ở dòng trong thùng thì chỉ tích dòng trong thùng (tránh lẫn vô tình)
    function apKhoang(a, b, trangThai, trongThung) {
        var ds = dsDong();
        for (var i = Math.min(a, b); i <= Math.max(a, b); i++) {
            var cb = oTich(ds[i]);
            if (cb && !cb.disabled && laTrongThung(cb) === trongThung) cb.checked = trangThai;
        }
    }

    document.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return;
        var tr = e.target.closest('#qa-mes-tbody tr');
        var cb = oTich(tr);
        if (!cb || cb.disabled) return;
        e.preventDefault(); // không bôi đen chữ khi kéo
        var ds = dsDong();
        var chiSo = ds.indexOf(tr);

        if (e.shiftKey && chiSoNeo !== null) {
            apKhoang(chiSoNeo, chiSo, trangThaiNeo, loaiNeo);
            capNhatChon();
            return;
        }
        var trangThai = !cb.checked;
        cb.checked = trangThai;
        chiSoNeo = chiSo;
        trangThaiNeo = trangThai;
        loaiNeo = laTrongThung(cb);
        keo = { trangThai: trangThai, trongThung: loaiNeo, dau: chiSo, cuoi: chiSo, banDau: ds.map(function (r) { var c = oTich(r); return c ? c.checked : false; }) };
        document.body.classList.add('qa-dang-keo');
        capNhatChon();
    });

    document.addEventListener('mouseover', function (e) {
        if (!keo) return;
        var tr = e.target.closest('#qa-mes-tbody tr');
        if (!tr) return;
        var ds = dsDong();
        var chiSo = ds.indexOf(tr);
        if (chiSo < 0 || chiSo === keo.cuoi) return;
        // Kéo lùi lại thì trả các dòng ra khỏi khoảng về trạng thái trước khi kéo
        var cu = [Math.min(keo.dau, keo.cuoi), Math.max(keo.dau, keo.cuoi)];
        for (var i = cu[0]; i <= cu[1]; i++) {
            var c = oTich(ds[i]);
            if (c && !c.disabled) c.checked = keo.banDau[i];
        }
        keo.cuoi = chiSo;
        apKhoang(keo.dau, keo.cuoi, keo.trangThai, keo.trongThung);
        capNhatChon();
    });

    // Kéo sát mép trên/dưới khung bảng thì tự cuộn để chọn tiếp các dòng bị khuất
    document.addEventListener('mousemove', function (e) {
        if (!keo) return;
        var khung = document.querySelector('.qa-mes-bang-wrap');
        var r = khung.getBoundingClientRect();
        if (e.clientY < r.top + 50) khung.scrollTop -= 20;
        else if (e.clientY > r.bottom - 30) khung.scrollTop += 20;
    });

    document.addEventListener('mouseup', function () {
        if (!keo) return;
        keo = null;
        document.body.classList.remove('qa-dang-keo');
    });

    document.addEventListener('click', function (e) {
        var b = e.target.closest('#qa-mes-trang-nut button[data-trang]');
        if (b && !b.disabled) denTrang(parseInt(b.dataset.trang, 10));
    });
    document.addEventListener('change', function (e) {
        if (e.target.id !== 'qa-mes-trang-co') return;
        var dongDau = (trang - 1) * coTrang; // giữ dòng đầu đang xem khi đổi cỡ trang
        coTrang = parseInt(e.target.value, 10) || 100;
        try { localStorage.setItem(KHOA_CO_TRANG, String(coTrang)); } catch (err) { /* bỏ qua */ }
        denTrang(Math.floor(dongDau / coTrang) + 1);
    });

    napThungMo();

    // ===== Gợi ý khi gõ: datalist nạp từ dữ liệu đồng bộ, lọc theo các ô còn lại =====
    var henGoiY = null, lanGoiY = 0;
    document.addEventListener('input', function (e) {
        var o = e.target.closest('[data-goi-y]');
        if (!o) return;
        clearTimeout(henGoiY);
        henGoiY = setTimeout(function () { napGoiY(o); }, 250);
    });
    // Bấm vào ô (chưa gõ gì) cũng hiện gợi ý theo các ô còn lại
    document.addEventListener('focusin', function (e) {
        var o = e.target.closest('[data-goi-y]');
        if (o) napGoiY(o);
    });
    async function napGoiY(o) {
        var lan = ++lanGoiY;
        var dl = $(o.getAttribute('list'));
        try {
            var kq = await layJson('/QAForm/TraCuuMes/GoiY?truong=' + o.dataset.goiY + '&q=' + encodeURIComponent(o.value.trim()) + '&' + thamSoLoc(docLoc()));
            if (lan !== lanGoiY || !kq.thanhCong) return; // đã có lần gõ mới hơn
            dl.replaceChildren();
            (kq.duLieu || []).forEach(function (v) {
                var op = document.createElement('option');
                op.value = v;
                dl.appendChild(op);
            });
        } catch (err) { /* không có gợi ý vẫn gõ tay được */ }
    }

    // Mở trang có ?ma=&kh=&hang= (F5, link gửi nhau) -> tự tra lại và tích lại các dòng đã chọn.
    // Bấm menu sang trang khác rồi quay lại (địa chỉ không còn bộ lọc) -> lấy bộ lọc đang xem lưu ở sessionStorage.
    var ts = new URLSearchParams(window.location.search);
    var locDau = { ma: (ts.get('ma') || '').trim(), kh: (ts.get('kh') || '').trim(), hang: (ts.get('hang') || '').trim() };
    if (!locDau.ma && !locDau.kh && !locDau.hang) {
        try { locDau = (JSON.parse(sessionStorage.getItem(KHOA_TICH) || 'null') || {}).loc || locDau; } catch (e) { /* giữ rỗng */ }
    }
    if ((locDau.ma && locDau.ma.length >= 4) || locDau.kh || locDau.hang) {
        ganLoc(locDau);
        tra(docLoc(), true);
    }
})();
