// Trang QA "Check xuất hàng" — tab Bảng khiếu nại: lưới kiểu Excel trên QA_KhieuNai.
// Toàn bộ dòng (theo bộ lọc trên thanh công cụ) tải 1 lần về trình duyệt; lọc từng cột (ô gõ + hộp ▼ kiểu Excel),
// sắp xếp và phân trang làm trên dữ liệu, chỉ vẽ dòng của trang đang xem (nhẹ DOM dù vài nghìn dòng).
// Ô sửa trực tiếp (contenteditable / input / select), rời ô hoặc Enter là lưu riêng ô đó qua
// POST /QAForm/CheckXuatHang/KhieuNai/{id}/Sua; nhập mã lot thì server tự điền cột hệ thống (MES/WMS) và trả lại cả dòng.
// Phím: Enter / Shift+Enter xuống / lên, Tab sang ô kế, Alt+Enter xuống dòng trong ô, Esc huỷ sửa.
(function () {
    var the = document.getElementById('qa-kn-the');
    if (!the) return;
    var formLoc = document.getElementById('qa-kn-loc');
    var thead = document.getElementById('qa-kn-thead');
    var tbody = document.getElementById('qa-kn-tbody');
    var thongBao = document.getElementById('qa-kn-thongbao');
    var nutThem = document.getElementById('qa-kn-them');
    var oNhap = document.getElementById('qa-kn-nhap');
    var nutXuat = document.getElementById('qa-kn-xuat');
    var goiYChungLoai = document.getElementById('qa-kn-goiy-chungloai');
    var phanTrang = document.getElementById('qa-kn-phantrang');
    var trangTt = document.getElementById('qa-kn-trang-tt');
    var trangNut = document.getElementById('qa-kn-trang-nut');
    var trangCo = document.getElementById('qa-kn-trang-co');
    var nutXoaLoc = document.getElementById('qa-kn-xoa-loc');

    var dsCot = [];          // mô tả cột server gửi (khoa, tieuDe, kieu, tuDong, vang…)
    var cotTheoKhoa = {};
    var duLieu = {};         // id -> dòng
    var thuTu = [];          // id theo thứ tự server (ngày mới nhất trước)
    var moiThem = new Set(); // dòng vừa thêm: luôn hiện (kể cả khi đang lọc) để gõ tiếp
    var tongDong = 0;
    var COT_DAI = 1000;      // cột chuỗi dài hơn mức này thì cho xuống dòng, ô rộng hơn
    var TRONG = '(Trống)';
    var TRAN_HIEN = 1000;    // số giá trị tối đa liệt kê trong hộp lọc

    var locChu = {};         // khoa -> chuỗi gõ ở ô lọc đầu cột (chứa, không phân biệt hoa thường)
    var locGiaTri = {};      // khoa -> Set giá trị được hiện (hộp ▼)
    var sapXep = null;       // { khoa, tang }
    var trang = 1;
    var coTrang = 100;
    var dsHien = [];         // id sau lọc + sắp xếp

    // ---------- Tab ----------
    function chonTab(tab) {
        document.querySelectorAll('[data-tab]').forEach(function (el) { el.hidden = el.getAttribute('data-tab') !== tab; });
        document.querySelectorAll('[data-chon-tab]').forEach(function (b) {
            b.classList.toggle('active', b.getAttribute('data-chon-tab') === tab);
        });
        try { localStorage.setItem('qa-check-xuat-hang-tab', tab); } catch (e) { /* trình duyệt chặn lưu: bỏ qua */ }
        var o = document.getElementById('qa-xh-ma');
        if (o && tab === 'tra') o.focus();
    }
    document.addEventListener('click', function (ev) {
        var b = ev.target.closest('[data-chon-tab]');
        if (b) chonTab(b.getAttribute('data-chon-tab'));
    });
    var tabNho = 'kn';
    try { tabNho = localStorage.getItem('qa-check-xuat-hang-tab') || 'kn'; } catch (e) { /* bỏ qua */ }
    chonTab(tabNho === 'tra' ? 'tra' : 'kn');
    try { coTrang = parseInt(localStorage.getItem('qa-kn-co-trang'), 10) || 100; } catch (e) { /* bỏ qua */ }
    trangCo.value = String(coTrang);

    // ---------- Tiện ích ----------
    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function bao(chu, kieu) {
        thongBao.textContent = chu || '';
        thongBao.className = 'qa-mes-thongbao' + (kieu ? ' ' + kieu : '');
    }

    async function goi(url, tuyChon) {
        var res = await fetch(url, tuyChon);
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
        return res.json();
    }

    function postJson(url, body) {
        return goi(url, {
            method: 'POST',
            headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify(body || {})
        });
    }

    // Giá trị server -> chữ hiển thị trong ô
    function hienThi(cot, v) {
        if (v === null || v === undefined) return '';
        if (cot.kieu === 'So') return Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 });
        if (cot.kieu === 'Ngay') { var p = String(v).split('-'); return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : v; }
        return String(v);
    }

    // Giá trị dùng cho lọc theo danh sách (1 dòng, rỗng = "(Trống)")
    function giaTriLoc(cot, d) {
        var s = hienThi(cot, d[cot.khoa]).replace(/\s+/g, ' ').trim();
        return s === '' ? TRONG : s;
    }

    function layGiaTriO(o) {
        if (o.tagName === 'SELECT' || o.tagName === 'INPUT') return o.value.trim();
        return (o.innerText || '').replace(/ /g, ' ').trim();
    }

    function ganGiaTriO(o, chu) {
        if (o.tagName === 'SELECT' || o.tagName === 'INPUT') o.value = chu;
        else o.textContent = chu;
        o.setAttribute('data-goc', chu);
    }

    function locHienTai() {
        var p = new URLSearchParams(new FormData(formLoc));
        Array.from(p.keys()).forEach(function (k) { if (!p.get(k)) p.delete(k); });
        return p;
    }

    function boDau(s) { return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase(); }

    // ---------- Lọc / sắp xếp trên dữ liệu ----------
    // Dòng qua mọi bộ lọc, trừ bộ lọc danh sách của cột boQua (để hộp ▼ của cột đó liệt kê theo các cột khác — như Excel)
    function quaLoc(d, boQua) {
        for (var k in locChu) {
            var cot = cotTheoKhoa[k];
            if (boDau(hienThi(cot, d[k])).indexOf(locChu[k]) < 0) return false;
        }
        for (var k2 in locGiaTri) {
            if (k2 === boQua) continue;
            if (!locGiaTri[k2].has(giaTriLoc(cotTheoKhoa[k2], d))) return false;
        }
        return true;
    }

    function soSanhGiaTri(cot) {
        return function (a, b) {
            var x = a[cot.khoa], y = b[cot.khoa];
            var xr = x === null || x === undefined || x === '', yr = y === null || y === undefined || y === '';
            if (xr || yr) return xr === yr ? 0 : xr ? 1 : -1;   // ô trống luôn cuối
            if (cot.kieu === 'So') return Number(x) - Number(y);
            if (cot.kieu === 'Ngay') return String(x) < String(y) ? -1 : String(x) > String(y) ? 1 : 0;
            return String(x).localeCompare(String(y), 'vi', { numeric: true });
        };
    }

    function tinhDsHien() {
        var ds = thuTu.filter(function (id) { return moiThem.has(id) || quaLoc(duLieu[id], null); });
        if (sapXep) {
            var ss = soSanhGiaTri(cotTheoKhoa[sapXep.khoa]);
            var tang = sapXep.tang;
            ds.sort(function (a, b) {
                var r = ss(duLieu[a], duLieu[b]);
                // ô trống luôn cuối dù sắp tăng hay giảm
                var x = duLieu[a][sapXep.khoa], y = duLieu[b][sapXep.khoa];
                if (x === null || x === undefined || x === '' || y === null || y === undefined || y === '') return r;
                return tang ? r : -r;
            });
        }
        // Dòng vừa thêm lên đầu
        if (moiThem.size) ds.sort(function (a, b) { return (moiThem.has(b) ? 1 : 0) - (moiThem.has(a) ? 1 : 0); });
        dsHien = ds;
    }

    function dangLoc() { return Object.keys(locChu).length > 0 || Object.keys(locGiaTri).length > 0; }

    function capNhatNutLoc() {
        thead.querySelectorAll('th[data-k]').forEach(function (th) {
            var k = th.getAttribute('data-k');
            var nut = th.querySelector('.qa-loc-nut');
            if (nut) nut.classList.toggle('dang-loc', !!locGiaTri[k] || (sapXep && sapXep.khoa === k));
            var o = th.querySelector('.qa-kn-loc-o');
            if (o) o.classList.toggle('dang-loc', !!locChu[k]);
        });
        nutXoaLoc.hidden = !dangLoc() && !sapXep;
    }

    // Tính lại danh sách + vẽ trang; giuTrang=false về trang 1
    function apDung(giuTrang) {
        tinhDsHien();
        var soTrang = Math.max(1, Math.ceil(dsHien.length / coTrang));
        if (!giuTrang) trang = 1;
        trang = Math.min(Math.max(1, trang), soTrang);
        veTrang();
        veThongKe();
        capNhatNutLoc();
    }

    // ---------- Vẽ ----------
    // Độ rộng cột (px) theo kiểu dữ liệu như bảng Excel; bảng dùng table-layout: fixed nên chữ dài cắt "…"
    var RONG_RIENG = { maLot: 128, po: 130, mauKhach: 170, khoRong: 140, chungLoai: 130, trangThai: 86, maNvQa: 110, qaPhuTrach: 130, carNo: 110 };
    function doRong(c) {
        if (RONG_RIENG[c.khoa]) return RONG_RIENG[c.khoa];
        if (c.kieu === 'Ngay') return 96;
        if (c.kieu === 'So') return 92;
        if (c.doDai >= COT_DAI) return 230;
        if (c.doDai > 100) return 160;
        return 96;
    }

    function veTieuDe() {
        var bang = thead.closest('table');
        var cg = bang.querySelector('colgroup') || bang.insertBefore(document.createElement('colgroup'), bang.firstChild);
        var tong = 44 + 62;
        var dsCol = [44, 62].concat(dsCot.map(doRong)).map(function (w) {
            var col = document.createElement('col');
            col.style.width = w + 'px';
            return col;
        });
        dsCot.forEach(function (c) { tong += doRong(c); });
        cg.replaceChildren.apply(cg, dsCol);
        bang.style.width = tong + 'px';

        var tr = document.createElement('tr');
        var thSo = document.createElement('th');
        thSo.className = 'qa-kn-co-dinh qa-kn-stt';
        thSo.textContent = '#';
        tr.appendChild(thSo);
        var thNut = document.createElement('th');
        thNut.className = 'qa-kn-co-dinh qa-kn-nut-cot';
        thNut.title = 'Lấy lại từ MES / xoá dòng';
        thNut.innerHTML = '<i class="fa fa-cog"></i>';
        tr.appendChild(thNut);
        dsCot.forEach(function (c) {
            var th = document.createElement('th');
            th.setAttribute('data-k', c.khoa);
            th.className = (c.vang ? 'qa-kn-vang' : 'qa-kn-xanh') + (c.tuDong ? ' qa-kn-tudong' : '') + (c.doDai >= COT_DAI ? ' qa-kn-dai' : '');
            // Tiêu đề 2 dòng: tiếng Trung (đậm) + tiếng Việt; cột Excel + "tự điền" đưa vào tooltip / biểu tượng ⚡
            var phan = String(c.tieuDe).split('\n');
            var chu = document.createElement('span');
            chu.className = 'qa-kn-td';
            var b = document.createElement('b');
            b.textContent = phan[0];
            if (c.tuDong) {
                var set = document.createElement('i');
                set.className = 'fa fa-bolt qa-kn-set';
                b.appendChild(set);
            }
            var vi = document.createElement('small');
            vi.textContent = phan.slice(1).join(' ') || ' ';
            chu.append(b, vi);
            var ma = document.createDocumentFragment();
            th.title = 'Cột ' + c.cotExcel + ': ' + phan.join(' / ') + (c.tuDong ? '\n⚡ Tự điền từ NH-MES / NH-WMS khi nhập mã lot (vẫn sửa tay được)' : '');
            // Ô lọc đầu cột: gõ là lọc "chứa" (không dấu); nút ▼ mở hộp lọc danh sách + sắp xếp kiểu Excel
            var loc = document.createElement('div');
            loc.className = 'qa-kn-loc';
            var o = document.createElement('input');
            o.type = 'search';
            o.size = 1;   // không để ô lọc tự kéo giãn cột
            o.className = 'qa-kn-loc-o';
            o.placeholder = 'Lọc…';
            o.setAttribute('data-loc-k', c.khoa);
            var nut = document.createElement('button');
            nut.type = 'button';
            nut.className = 'qa-loc-nut';
            nut.title = 'Lọc / sắp xếp cột này';
            nut.setAttribute('data-hop-k', c.khoa);
            nut.innerHTML = '<i class="fa fa-caret-down"></i>';
            loc.append(o, nut);
            th.append(chu, ma, loc);
            tr.appendChild(th);
        });
        thead.replaceChildren(tr);
    }

    function taoO(c, d) {
        var td = document.createElement('td');
        td.setAttribute('data-k', c.khoa);
        if (c.tuDong) td.className = 'qa-kn-tudong';
        var o;
        if (c.kieu === 'TrangThai') {
            o = document.createElement('select');
            o.className = 'qa-kn-tt';
            ['pending', 'close'].forEach(function (t) {
                var op = document.createElement('option');
                op.value = t;
                op.textContent = t;
                o.appendChild(op);
            });
        } else if (c.khoa === 'chungLoai') {
            o = document.createElement('input');
            o.type = 'text';
            o.className = 'qa-kn-input';
            o.setAttribute('list', 'qa-kn-goiy-chungloai');
            o.maxLength = c.doDai;
        } else {
            o = document.createElement('div');
            o.className = 'qa-kn-o' + (c.doDai >= COT_DAI ? ' qa-kn-dai' : '') + (c.kieu === 'So' ? ' qa-kn-so' : '');
            o.contentEditable = 'plaintext-only';
            if (o.contentEditable !== 'plaintext-only') o.contentEditable = 'true';
            o.spellcheck = false;
        }
        o.setAttribute('data-o', '1');
        o.setAttribute('data-k', c.khoa);
        ganGiaTriO(o, hienThi(c, d[c.khoa]));
        td.appendChild(o);
        return td;
    }

    function taoDong(d, stt) {
        var tr = document.createElement('tr');
        tr.setAttribute('data-id', d.id);
        var tdSo = document.createElement('td');
        tdSo.className = 'qa-kn-co-dinh qa-kn-stt';
        tdSo.textContent = stt;
        tr.appendChild(tdSo);
        var tdNut = document.createElement('td');
        tdNut.className = 'qa-kn-co-dinh qa-kn-nut-cot';
        tdNut.innerHTML = '<button type="button" class="qa-kn-nut" data-dien-lai title="Lấy lại cột hệ thống từ NH-MES/NH-WMS theo mã lot"><i class="fa fa-refresh"></i></button>' +
            '<button type="button" class="qa-kn-nut xoa" data-xoa title="Xoá dòng"><i class="fa fa-trash-o"></i></button>';
        tr.appendChild(tdNut);
        dsCot.forEach(function (c) { tr.appendChild(taoO(c, d)); });
        capNhatDong(tr, d);
        return tr;
    }

    // Đổ lại giá trị dòng từ server (trừ ô đang được gõ), tô trạng thái
    function capNhatDong(tr, d) {
        duLieu[d.id] = d;
        tr.classList.toggle('qa-kn-close', d.trangThai === 'close');
        tr.title = 'Sửa lần cuối: ' + (d.nguoiSua || '') + ' ' + (d.ngaySua || '') +
            (d.dienTuDongLuc ? ' · Tự điền MES/WMS lúc ' + d.dienTuDongLuc : '');
        tr.querySelectorAll('[data-o]').forEach(function (o) {
            // Ô đang gõ dở thì giữ chữ người dùng; ô chỉ đang được chọn (chưa sửa) vẫn đổ giá trị mới
            if (o === document.activeElement && layGiaTriO(o) !== (o.getAttribute('data-goc') || '')) return;
            var c = cotTheoKhoa[o.getAttribute('data-k')];
            ganGiaTriO(o, hienThi(c, d[c.khoa]));
        });
    }

    function dongTrong(chu) {
        var tr = document.createElement('tr');
        var td = document.createElement('td');
        td.colSpan = dsCot.length + 2 || 1;
        td.className = 'qa-mes-trong';
        // Bảng rộng hơn màn hình: chữ dính mép trái vùng nhìn thấy thay vì canh giữa cả bảng
        var span = document.createElement('span');
        span.textContent = chu;
        td.appendChild(span);
        tr.appendChild(td);
        tbody.replaceChildren(tr);
    }

    function veTrang() {
        var dau = (trang - 1) * coTrang;
        var ds = dsHien.slice(dau, dau + coTrang);
        if (!ds.length) {
            dongTrong(thuTu.length ? 'Không có dòng nào khớp bộ lọc cột.' : 'Không có dòng khiếu nại nào. Bấm "Thêm dòng" hoặc "Đồng bộ thư mục QA".');
        } else {
            var frag = document.createDocumentFragment();
            ds.forEach(function (id, i) { frag.appendChild(taoDong(duLieu[id], dau + i + 1)); });
            tbody.replaceChildren(frag);
        }
        the.querySelector('.qa-kn-wrap').scrollTop = 0;
        vePhanTrang();
    }

    function nutTrang(nhan, so, hienTai, tat) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'qa-kn-trang' + (hienTai ? ' active' : '');
        b.textContent = nhan;
        b.disabled = !!tat;
        if (!tat && !hienTai) b.setAttribute('data-trang', so);
        return b;
    }

    function vePhanTrang() {
        var tong = dsHien.length;
        var soTrang = Math.max(1, Math.ceil(tong / coTrang));
        phanTrang.hidden = tong === 0;
        var dau = tong ? (trang - 1) * coTrang + 1 : 0;
        trangTt.textContent = 'Dòng ' + dau.toLocaleString('vi-VN') + '–' + Math.min(trang * coTrang, tong).toLocaleString('vi-VN') +
            ' / ' + tong.toLocaleString('vi-VN') + (dangLoc() ? ' (đang lọc từ ' + thuTu.length.toLocaleString('vi-VN') + ')' : '');
        var frag = document.createDocumentFragment();
        frag.appendChild(nutTrang('«', 1, false, trang === 1));
        frag.appendChild(nutTrang('‹', trang - 1, false, trang === 1));
        var tu = Math.max(1, trang - 2), den = Math.min(soTrang, tu + 4);
        tu = Math.max(1, den - 4);
        for (var p = tu; p <= den; p++) frag.appendChild(nutTrang(String(p), p, p === trang, false));
        frag.appendChild(nutTrang('›', trang + 1, false, trang === soTrang));
        frag.appendChild(nutTrang('»', soTrang, false, trang === soTrang));
        trangNut.replaceChildren(frag);
    }

    trangNut.addEventListener('click', function (ev) {
        var b = ev.target.closest('[data-trang]');
        if (!b) return;
        trang = parseInt(b.getAttribute('data-trang'), 10);
        veTrang();
    });
    trangCo.addEventListener('change', function () {
        coTrang = parseInt(trangCo.value, 10) || 100;
        try { localStorage.setItem('qa-kn-co-trang', String(coTrang)); } catch (e) { /* bỏ qua */ }
        apDung(false);
    });

    // Số liệu theo các dòng đang qua bộ lọc (giống thanh trạng thái Excel)
    function veThongKe() {
        var ds = dsHien.map(function (id) { return duLieu[id]; });
        var pending = ds.filter(function (d) { return d.trangThai !== 'close'; }).length;
        var slKn = ds.reduce(function (t, d) { return t + (Number(d.slKhieuNai) || 0); }, 0);
        window.qaTheSoLieu('qa-kn-thongke', [
            ['fa-list', dangLoc() ? 'Dòng (đang lọc)' : 'Dòng khiếu nại', ds.length.toLocaleString('vi-VN') + (tongDong > thuTu.length ? ' / ' + tongDong.toLocaleString('vi-VN') : ''), ''],
            ['fa-hourglass-half', 'pending', pending.toLocaleString('vi-VN'), 'cam'],
            ['fa-check-circle', 'close', (ds.length - pending).toLocaleString('vi-VN'), 'xanh'],
            ['fa-arrows-h', 'SL khiếu nại (Y)', slKn.toLocaleString('vi-VN', { maximumFractionDigits: 2 }), '']
        ]);
    }

    async function taiDs() {
        bao('Đang tải bảng khiếu nại…');
        try {
            var kq = await goi('/QAForm/CheckXuatHang/KhieuNai?' + locHienTai().toString(), { headers: { 'Accept': 'application/json' } });
            if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); dongTrong(kq.thongBao); return; }
            var dl = kq.duLieu;
            if (!dsCot.length) {
                dsCot = dl.cot;
                dsCot.forEach(function (c) { cotTheoKhoa[c.khoa] = c; });
                veTieuDe();
            }
            duLieu = {};
            moiThem.clear();
            thuTu = dl.ds.map(function (d) { duLieu[d.id] = d; return d.id; });
            tongDong = dl.tong;
            goiYChungLoai.replaceChildren.apply(goiYChungLoai, (dl.goiYChungLoai || []).map(function (s) {
                var op = document.createElement('option');
                op.value = s;
                return op;
            }));
            apDung(false);
            bao(dl.tong > dl.ds.length ? 'Đang có ' + dl.ds.length + '/' + dl.tong + ' dòng — lọc theo ngày ở trên để xem phần còn lại.' : '');
            nutXuat.href = '/QAForm/CheckXuatHang/KhieuNai/XuatExcel?' + locHienTai().toString();
        } catch (e) {
            bao('Không tải được bảng khiếu nại: ' + e.message, 'loi');
        }
    }

    // ---------- Ô lọc đầu cột ----------
    var henLoc = null;
    thead.addEventListener('input', function (ev) {
        var o = ev.target.closest('[data-loc-k]');
        if (!o) return;
        var k = o.getAttribute('data-loc-k');
        var s = boDau(o.value.trim());
        if (s) locChu[k] = s; else delete locChu[k];
        clearTimeout(henLoc);
        henLoc = setTimeout(function () { apDung(false); }, 200);
    });

    nutXoaLoc.addEventListener('click', function () {
        locChu = {};
        locGiaTri = {};
        sapXep = null;
        thead.querySelectorAll('[data-loc-k]').forEach(function (o) { o.value = ''; });
        dongHop();
        apDung(false);
    });

    // ---------- Hộp lọc ▼ kiểu Excel (giao diện dùng chung lớp CSS của qa-loc-cot.js) ----------
    var hop = null;

    function dongHop() {
        if (!hop) return;
        hop.el.remove();
        hop = null;
    }

    function zoomBody() { return parseFloat(getComputedStyle(document.body).zoom) || 1; }

    function moHop(nutMo, k) {
        dongHop();
        var cot = cotTheoKhoa[k];
        var dem = new Map();
        thuTu.forEach(function (id) {
            var d = duLieu[id];
            if (!quaLoc(d, k)) return;
            var v = giaTriLoc(cot, d);
            dem.set(v, (dem.get(v) || 0) + 1);
        });
        var dsGiaTri = Array.from(dem.keys());
        // Sắp danh sách giá trị theo kiểu cột (ngày/số theo giá trị thật)
        var khoaSx = function (v) {
            if (cot.kieu === 'Ngay') { var p = v.split('/'); return p.length === 3 ? p[2] + p[1] + p[0] : v; }
            return v;
        };
        dsGiaTri.sort(function (a, b) {
            if (a === TRONG) return 1;
            if (b === TRONG) return -1;
            if (cot.kieu === 'So') return parseFloat(a.replace(/\./g, '').replace(',', '.')) - parseFloat(b.replace(/\./g, '').replace(',', '.'));
            if (cot.kieu === 'Ngay') return khoaSx(b) < khoaSx(a) ? -1 : 1;   // ngày mới nhất trước
            return a.localeCompare(b, 'vi', { numeric: true });
        });
        var dangChon = locGiaTri[k] ? new Set(locGiaTri[k]) : new Set(dsGiaTri);

        var el = document.createElement('div');
        el.className = 'qa-loc-hop';
        el.innerHTML =
            '<div class="qa-loc-sx">' +
            '<button type="button" data-sx="tang"><i class="fa fa-sort-amount-asc"></i> Sắp xếp tăng dần (A → Z)</button>' +
            '<button type="button" data-sx="giam"><i class="fa fa-sort-amount-desc"></i> Sắp xếp giảm dần (Z → A)</button>' +
            '</div>' +
            '<button type="button" class="qa-loc-xoa-cot"><i class="fa fa-times"></i> Xoá lọc + sắp xếp cột này</button>' +
            '<input type="search" class="form-control form-control-sm qa-loc-tim" placeholder="Tìm...">' +
            '<div class="qa-loc-ds"></div>' +
            '<div class="qa-loc-nut-duoi">' +
            '<button type="button" class="btn btn-sm btn-success qa-loc-ok">OK</button>' +
            '<button type="button" class="btn btn-sm btn-outline-secondary qa-loc-huy">Huỷ</button>' +
            '</div>';
        var dsEl = el.querySelector('.qa-loc-ds');
        var tim = el.querySelector('.qa-loc-tim');

        function dongChon(text, checked, lop) {
            var lb = document.createElement('label');
            if (lop) lb.className = lop;
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.className = 'form-check-input';
            cb.checked = checked;
            lb.appendChild(cb);
            lb.appendChild(document.createTextNode(' ' + text));
            return { lb: lb, cb: cb };
        }

        function dsKhopTim() {
            var q = boDau(tim.value.trim());
            return q ? dsGiaTri.filter(function (v) { return boDau(v).indexOf(q) >= 0; }) : dsGiaTri;
        }

        function ve() {
            var khop = dsKhopTim();
            var frag = document.createDocumentFragment();
            var tatCa = dongChon('(Chọn tất cả' + (tim.value.trim() ? ' kết quả tìm' : '') + ')',
                khop.length > 0 && khop.every(function (v) { return dangChon.has(v); }), 'qa-loc-tatca');
            tatCa.cb.addEventListener('change', function () {
                khop.forEach(function (v) { if (tatCa.cb.checked) dangChon.add(v); else dangChon.delete(v); });
                ve();
            });
            frag.appendChild(tatCa.lb);
            khop.slice(0, TRAN_HIEN).forEach(function (v) {
                var d = dongChon(v + '  (' + dem.get(v).toLocaleString('vi-VN') + ')', dangChon.has(v));
                d.cb.addEventListener('change', function () {
                    if (d.cb.checked) dangChon.add(v); else dangChon.delete(v);
                    tatCa.cb.checked = khop.every(function (x) { return dangChon.has(x); });
                });
                frag.appendChild(d.lb);
            });
            if (khop.length > TRAN_HIEN || khop.length === 0) {
                var them = document.createElement('div');
                them.className = 'qa-loc-them';
                them.textContent = khop.length ? 'Còn ' + (khop.length - TRAN_HIEN).toLocaleString('vi-VN') + ' giá trị — gõ ô Tìm để thu hẹp.' : 'Không có giá trị khớp.';
                frag.appendChild(them);
            }
            dsEl.replaceChildren(frag);
        }

        function ok() {
            // Đang gõ tìm: như Excel, chỉ giữ các giá trị khớp tìm và đang tích
            var khop = new Set(dsKhopTim());
            var chon = dsGiaTri.filter(function (v) { return dangChon.has(v) && (!tim.value.trim() || khop.has(v)); });
            if (chon.length === dsGiaTri.length) delete locGiaTri[k];
            else locGiaTri[k] = new Set(chon);
            dongHop();
            apDung(false);
        }

        tim.addEventListener('input', ve);
        tim.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); ok(); } });
        el.querySelector('.qa-loc-ok').addEventListener('click', ok);
        el.querySelector('.qa-loc-huy').addEventListener('click', dongHop);
        el.querySelector('.qa-loc-xoa-cot').addEventListener('click', function () {
            delete locGiaTri[k];
            if (sapXep && sapXep.khoa === k) sapXep = null;
            dongHop();
            apDung(false);
        });
        el.querySelectorAll('[data-sx]').forEach(function (b) {
            b.addEventListener('click', function () {
                sapXep = { khoa: k, tang: b.getAttribute('data-sx') === 'tang' };
                dongHop();
                apDung(false);
            });
        });
        ve();

        // Đặt hộp dưới nút; body có zoom (menu % kích thước) nên toạ độ màn hình phải chia zoom
        document.body.appendChild(el);
        var z = zoomBody();
        var r = nutMo.getBoundingClientRect();
        var rong = el.offsetWidth;
        el.style.left = Math.max(8, Math.min(r.left / z, (window.innerWidth / z) - rong - 8)) + 'px';
        el.style.top = (r.bottom / z + 2) + 'px';
        el.style.maxHeight = Math.max(220, window.innerHeight / z - r.bottom / z - 16) + 'px';
        hop = { k: k, el: el };
        tim.focus();
    }

    thead.addEventListener('click', function (ev) {
        var nut = ev.target.closest('[data-hop-k]');
        if (!nut) return;
        ev.stopPropagation();
        var k = nut.getAttribute('data-hop-k');
        if (hop && hop.k === k) { dongHop(); return; }
        moHop(nut, k);
    });
    document.addEventListener('mousedown', function (e) {
        if (hop && !e.target.closest('.qa-loc-hop') && !e.target.closest('[data-hop-k]')) dongHop();
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') dongHop(); });
    window.addEventListener('resize', dongHop);
    the.querySelector('.qa-kn-wrap').addEventListener('scroll', dongHop);

    // ---------- Lưu ô ----------
    async function luuO(o) {
        var tr = o.closest('tr[data-id]');
        if (!tr) return;
        var moi = layGiaTriO(o);
        if (moi === (o.getAttribute('data-goc') || '')) { o.classList.remove('loi'); return; }
        var id = tr.getAttribute('data-id');
        var khoa = o.getAttribute('data-k');
        o.classList.add('dang-luu');
        o.classList.remove('loi');
        if (khoa === 'maLot' && moi) bao('Đang lấy thông tin lot ' + moi + ' từ NH-MES / NH-WMS…');
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/Sua', { truong: khoa, giaTri: moi });
            if (!kq.thanhCong) {
                // Giữ chữ người dùng đã gõ, đánh dấu lỗi để sửa lại
                o.classList.add('loi');
                o.title = kq.thongBao;
                bao(kq.thongBao, 'loi');
                return;
            }
            o.title = '';
            o.setAttribute('data-goc', moi);
            // Đổ lại cả dòng (giá trị đã chuẩn hoá + cột tự điền khi đổi mã lot); ô đang gõ thì giữ nguyên.
            // Không lọc/vẽ lại ngay để dòng không "nhảy" mất khi đang sửa — bấm lọc lại sẽ áp giá trị mới.
            if (kq.duLieu) capNhatDong(tr, kq.duLieu);
            veThongKe();
            if (kq.thongBao) bao(kq.thongBao, 'loi');
            else if (khoa === 'maLot' && moi) bao('Đã điền thông tin lot ' + moi + ' từ NH-MES / NH-WMS.', 'ok');
            else bao('');
        } catch (e) {
            o.classList.add('loi');
            bao('Không lưu được: ' + e.message, 'loi');
        } finally {
            o.classList.remove('dang-luu');
        }
    }

    tbody.addEventListener('focusout', function (ev) {
        var o = ev.target.closest('[data-o]');
        if (o && o.tagName !== 'SELECT') luuO(o);
    });
    tbody.addEventListener('change', function (ev) {
        var o = ev.target.closest('select[data-o]');
        if (o) luuO(o);
    });

    // Paste vào ô contenteditable: chỉ lấy chữ thuần
    tbody.addEventListener('paste', function (ev) {
        var o = ev.target.closest('div[data-o]');
        if (!o || o.contentEditable === 'plaintext-only') return;
        ev.preventDefault();
        document.execCommand('insertText', false, (ev.clipboardData || window.clipboardData).getData('text'));
    });

    function oCungCot(o, buoc) {
        var tr = o.closest('tr[data-id]');
        var khoa = o.getAttribute('data-k');
        var dich = buoc > 0 ? tr.nextElementSibling : tr.previousElementSibling;
        return dich ? dich.querySelector('[data-o][data-k="' + khoa + '"]') : null;
    }

    function dat(o) {
        if (!o) return;
        o.focus();
        if (o.tagName === 'DIV') {
            var r = document.createRange();
            r.selectNodeContents(o);
            var s = window.getSelection();
            s.removeAllRanges();
            s.addRange(r);
        } else if (o.select) {
            o.select();
        }
    }

    tbody.addEventListener('keydown', function (ev) {
        var o = ev.target.closest('[data-o]');
        if (!o) return;
        if (ev.key === 'Escape') {
            ganGiaTriO(o, o.getAttribute('data-goc') || '');
            o.classList.remove('loi');
            o.blur();
            return;
        }
        if (ev.key === 'Enter' && ev.altKey && o.tagName === 'DIV') {
            ev.preventDefault();
            document.execCommand('insertLineBreak');
            return;
        }
        if (ev.key === 'Enter' || (o.tagName !== 'SELECT' && (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') && !ev.altKey)) {
            // Ô nhiều dòng: mũi tên giữ di chuyển con trỏ trong ô
            if (ev.key !== 'Enter' && o.classList.contains('qa-kn-dai')) return;
            ev.preventDefault();
            var len = ev.key === 'ArrowUp' || (ev.key === 'Enter' && ev.shiftKey);
            dat(oCungCot(o, len ? -1 : 1) || (o.blur(), null));
        }
    });

    // ---------- Nút dòng ----------
    tbody.addEventListener('click', async function (ev) {
        var nut = ev.target.closest('[data-dien-lai], [data-xoa]');
        if (!nut) return;
        var tr = nut.closest('tr[data-id]');
        var id = tr.getAttribute('data-id');
        var d = duLieu[id] || {};
        if (nut.hasAttribute('data-xoa')) {
            if (!confirm('Xoá dòng khiếu nại ' + (d.maLot || '') + ' (' + (hienThi({ kieu: 'Ngay' }, d.ngayKhieuNai) || 'chưa có ngày') + ')?')) return;
        }
        nut.disabled = true;
        var icon = nut.innerHTML;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i>';
        try {
            if (nut.hasAttribute('data-xoa')) {
                var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/Xoa');
                if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); return; }
                var idSo = parseInt(id, 10);
                thuTu = thuTu.filter(function (x) { return x !== idSo; });
                delete duLieu[id];
                moiThem.delete(idSo);
                tongDong = Math.max(0, tongDong - 1);
                apDung(true);
                bao(kq.thongBao, 'ok');
            } else {
                bao('Đang lấy lại thông tin lot ' + (d.maLot || '') + '…');
                var kq2 = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/DienLai');
                if (kq2.duLieu) capNhatDong(tr, kq2.duLieu);
                bao(kq2.thanhCong ? 'Đã cập nhật lot ' + (d.maLot || '') + ' từ NH-MES / NH-WMS.' : kq2.thongBao, kq2.thanhCong ? 'ok' : 'loi');
                veThongKe();
            }
        } catch (e) {
            bao('Lỗi: ' + e.message, 'loi');
        } finally {
            if (nut.isConnected) { nut.disabled = false; nut.innerHTML = icon; }
        }
    });

    // ---------- Thêm dòng ----------
    function guid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        return 'k' + Date.now().toString(36) + Math.random().toString(36).slice(2, 12);
    }

    nutThem.addEventListener('click', async function () {
        nutThem.disabled = true;
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/Them', { khoaTao: guid() });
            if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); return; }
            var d = kq.duLieu;
            if (!duLieu[d.id]) { thuTu.unshift(d.id); tongDong++; }
            duLieu[d.id] = d;
            // Dòng mới luôn hiện ở đầu trang 1 (kể cả khi đang lọc cột) để nhập tiếp
            moiThem.add(d.id);
            apDung(false);
            var tr = tbody.querySelector('tr[data-id="' + d.id + '"]');
            if (tr) dat(tr.querySelector('[data-o][data-k="maLot"]'));
            bao('Đã thêm dòng — nhập mã lot để tự điền nhà máy, khách cuối, PO, mã hàng, màu, SL xuất.', 'ok');
        } catch (e) {
            bao('Không thêm được dòng: ' + e.message, 'loi');
        } finally {
            nutThem.disabled = false;
        }
    });

    // ---------- Nhập Excel ----------
    // Chọn được nhiều file (vd cả 12 tháng): gửi lần lượt từng file theo tên, gộp kết quả; file lỗi không chặn file sau
    oNhap.addEventListener('change', async function () {
        var dsTep = Array.from(oNhap.files || []).sort(function (a, b) { return a.name.localeCompare(b.name); });
        if (!dsTep.length) return;
        var ketQua = [], coLoi = false;
        for (var i = 0; i < dsTep.length; i++) {
            var tep = dsTep[i];
            bao('Đang nhập ' + (dsTep.length > 1 ? '(' + (i + 1) + '/' + dsTep.length + ') ' : '') + tep.name + '…');
            var fd = new FormData();
            fd.append('tep', tep);
            try {
                var kq = await goi('/QAForm/CheckXuatHang/KhieuNai/NhapExcel', {
                    method: 'POST', headers: { 'Accept': 'application/json', 'RequestVerificationToken': token() }, body: fd
                });
                if (!kq.thanhCong) coLoi = true;
                ketQua.push(tep.name + ': ' + kq.thongBao);
            } catch (e) {
                coLoi = true;
                ketQua.push(tep.name + ': lỗi ' + e.message);
            }
        }
        oNhap.value = '';
        await taiDs();
        bao(ketQua.join(' · '), coLoi ? 'loi' : 'ok');
    });

    // Đồng bộ thư mục QA trên file server: server đọc từng file và nhập (dòng mới thêm, dòng đã có điền ô trống)
    var nutDongBo = document.getElementById('qa-kn-dongbo');
    nutDongBo.addEventListener('click', async function () {
        nutDongBo.disabled = true;
        var icon = nutDongBo.innerHTML;
        nutDongBo.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang đồng bộ…';
        bao('Đang đọc các file khiếu nại trong thư mục QA…');
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/DongBoThuMuc');
            await taiDs();
            bao((kq.thongBao || '').split('\n').join(' · '), kq.thanhCong ? 'ok' : 'loi');
        } catch (e) {
            bao('Không đồng bộ được: ' + e.message, 'loi');
        } finally {
            nutDongBo.disabled = false;
            nutDongBo.innerHTML = icon;
        }
    });

    formLoc.addEventListener('submit', function (ev) {
        ev.preventDefault();
        taiDs();
    });

    taiDs();
})();
