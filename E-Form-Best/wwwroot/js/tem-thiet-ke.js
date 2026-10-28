// Trang thiết kế tem tủ điện thoại (/FormHR/QuanLyTuDienThoai/ThietKeTem) — làm theo app desktop TemNhan:
// menu, thanh nút lớn, bảng trái (máy in, khổ nhãn, lề, hướng, màu giấy, lưới), nhiều trang, thước mm, kéo thả/kéo khung chọn,
// bảng thuộc tính 3 tab (loại · Bố cục · Khác), dữ liệu tủ để trộn {{...}}, hộp thoại Mở / In, lưu mẫu lên máy chủ.
(function () {
    'use strict';

    var T = window.TemNhan;
    var URL_GOC = '/FormHR/QuanLyTuDienThoai';
    var BUOC_LUOI = 0.5;
    var ZOOM_MIN = 0.25, ZOOM_MAX = 16;
    var KHOA_NHAP = 'tem-thiet-ke-nhap';
    var KHOA_BO_CUC = 'tem-thiet-ke-bo-cuc';
    var KHOA_KHO = 'tem-thiet-ke-kho';
    var KHOA_GAN_DAY = 'tem-thiet-ke-gan-day';
    var KHOA_MUC = 'tem-thiet-ke-muc';

    var duAn = [T.taoTrangMacDinh()];   // các trang của mẫu (như Sheets của file .tnl)
    var iTrang = 0;
    var trang = duAn[0];                 // luôn = duAn[iTrang]
    var idMau = 0;
    var chonDs = [];                     // chỉ số đối tượng đang chọn trên trang hiện tại
    var tabTT = 'loai';                  // tab bảng thuộc tính: loai | bocuc | khac
    var zoom = 4;
    var dsTu = [], dsLoc = [], iDong = 0;
    var lichSu = [], lamLaiDs = [];
    var khoaCuoi = null, tgCuoi = 0;
    var daSua = false;
    var boNho = null;                    // đối tượng đã cắt/chép
    var keo = null;                      // đang kéo / đổi cỡ / kéo khung chọn
    var henVe = 0;
    var dsMayIn = [], khoMay = [], coAgent = null, henAgent = 0;

    function $(id) { return document.getElementById(id); }

    function layToken() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function thongBao(icon, text) {
        if (window.Swal) Swal.fire({ icon: icon, text: text, timer: icon === 'success' ? 1400 : undefined, showConfirmButton: icon !== 'success' });
        else alert(text);
    }

    async function hoi(text, nutDongY) {
        if (!window.Swal) return confirm(text);
        var kq = await Swal.fire({ icon: 'question', text: text, showCancelButton: true, confirmButtonText: nutDongY || 'Đồng ý', cancelButtonText: 'Huỷ' });
        return kq.isConfirmed;
    }

    async function nhapChu(tieuDe, giaTri) {
        if (!window.Swal) { var v = prompt(tieuDe, giaTri || ''); return v == null ? null : v.trim(); }
        var kq = await Swal.fire({ title: tieuDe, input: 'text', inputValue: giaTri || '', showCancelButton: true, confirmButtonText: 'OK', cancelButtonText: 'Huỷ', inputAttributes: { maxlength: 100 } });
        return kq.isConfirmed ? String(kq.value || '').trim() : null;
    }

    async function layJson(url) {
        var res = await fetch(url);
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
        var kq = await res.json();
        if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được dữ liệu.');
        return kq.duLieu;
    }

    async function goiPost(url, duLieu) {
        var fd = new FormData();
        Object.keys(duLieu).forEach(function (k) { fd.append(k, duLieu[k]); });
        var res = await fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': layToken() }, body: fd });
        if (!res.ok) throw new Error(res.status === 413 ? 'Mẫu tem quá lớn (thường do ảnh).' : 'Máy chủ trả lỗi ' + res.status);
        return res.json();
    }

    function trangThai(text) { $('tkTrangThai').textContent = text; }

    function docLs(khoa, macDinh) {
        try { var v = JSON.parse(localStorage.getItem(khoa) || 'null'); return v == null ? macDinh : v; } catch (ex) { return macDinh; }
    }

    function ghiLs(khoa, v) {
        try { if (v == null) localStorage.removeItem(khoa); else localStorage.setItem(khoa, JSON.stringify(v)); return true; } catch (ex) { return false; }
    }

    // ===== Hoàn tác (chụp cả mẫu nhiều trang) =====

    function chup() { return JSON.stringify({ d: duAn, i: iTrang }); }

    function khoiPhuc(s) {
        var o = JSON.parse(s);
        duAn = o.d.map(T.chuanHoaTrang);
        iTrang = Math.min(o.i, duAn.length - 1);
        trang = duAn[iTrang];
    }

    // Gọi TRƯỚC mỗi thay đổi. Cùng khoá trong 1,5 giây (gõ liên tục 1 ô) thì gộp thành 1 bước hoàn tác.
    function ghiTruoc(khoa) {
        var bayGio = Date.now();
        if (khoa && khoa === khoaCuoi && bayGio - tgCuoi < 1500) { tgCuoi = bayGio; return; }
        khoaCuoi = khoa || null; tgCuoi = bayGio;
        lichSu.push(chup());
        if (lichSu.length > 100) lichSu.shift();
        lamLaiDs = [];
        daSua = true;
    }

    function hoanTac() {
        if (!lichSu.length) return;
        lamLaiDs.push(chup());
        khoiPhuc(lichSu.pop());
        khoaCuoi = null; daSua = true;
        chonDs = chonDs.filter(function (i) { return i < trang.Elements.length; });
        veTatCa();
    }

    function lamLai() {
        if (!lamLaiDs.length) return;
        lichSu.push(chup());
        khoiPhuc(lamLaiDs.pop());
        khoaCuoi = null; daSua = true;
        chonDs = chonDs.filter(function (i) { return i < trang.Elements.length; });
        veTatCa();
    }

    // ===== Vẽ nhãn, thước, lớp chọn =====

    function tyLe() { return T.PX_MM * zoom; }

    function dongDangXem() { return dsLoc[iDong] ? taoDongTron(dsLoc[iDong]) : null; }

    function dongXem() { return $('tkXemDuLieu').checked ? dongDangXem() : null; }

    function veNhan() {
        var host = $('tkNhan');
        var kq = T.veNhan(trang, { dong: dongXem(), stt: 0, thietKe: true });
        host.replaceChildren(kq.el);
        T.thuNhoChu(kq.el);

        var rong = T.hieuRong(trang), s = tyLe();
        host.style.transform = 'scale(' + zoom + ')';
        $('tkKhung').style.width = (rong * s) + 'px';
        $('tkKhung').style.height = (trang.HeightMm * s) + 'px';
        $('tkKichNgang').textContent = so1(rong) + ' mm';
        $('tkKichDoc').setAttribute('data-chu', so1(trang.HeightMm) + ' mm');
        $('tkZoomNhan').textContent = Math.round(zoom * 100) + ' %';

        // Vạch lề chỉ để canh khi thiết kế
        if (trang.MarginMm > 0) {
            var le = document.createElement('div');
            le.className = 'tk-vach-le';
            le.style.inset = trang.MarginMm + 'mm';
            kq.el.appendChild(le);
        }
        if (kq.loi.length) trangThai('Cảnh báo: ' + kq.loi[0].thongBao + ' — tem lỗi sẽ không được in.');
        veLop();
        veThuoc();
        // Mọi thay đổi đều đi qua đây → ghi nháp (có hẹn giờ, chỉ ghi khi có sửa)
        if (daSua) ghiNhap();
    }

    function so1(v) { return String(Math.round(v * 10) / 10).replace('.', ','); }

    // Thước mm trên và trái như app: vạch theo zoom, tô vùng nhãn
    function veMotThuoc(cv, doc, goc, dai) {
        var dpr = window.devicePixelRatio || 1;
        var w = cv.clientWidth, h = cv.clientHeight;
        if (!w || !h) return;
        // Chỉ gán lại khi đổi cỡ (gán width/height xoá canvas và có thể kích ResizeObserver lặp)
        if (cv.width !== Math.round(w * dpr)) cv.width = Math.round(w * dpr);
        if (cv.height !== Math.round(h * dpr)) cv.height = Math.round(h * dpr);
        var g = cv.getContext('2d');
        g.setTransform(dpr, 0, 0, dpr, 0, 0);
        g.clearRect(0, 0, w, h);
        var s = tyLe(), canh = doc ? w : h, chieu = doc ? h : w;
        g.fillStyle = '#dbe7fb';
        if (doc) g.fillRect(0, goc, canh, dai * s); else g.fillRect(goc, 0, dai * s, canh);
        var lon = [1, 2, 5, 10, 20, 50, 100, 200, 500].find(function (b) { return b * s >= 48; }) || 1000;
        var nho = lon / 10 * s >= 4 ? lon / 10 : lon / 5 * s >= 4 ? lon / 5 : lon / 2;
        g.strokeStyle = '#9aa0a6'; g.fillStyle = '#5f6368';
        g.font = '9px Arial'; g.lineWidth = 1;
        var dau = Math.floor(-goc / s / nho) * nho, cuoi = (chieu - goc) / s;
        g.beginPath();
        for (var mm = dau; mm <= cuoi; mm += nho) {
            var v = Math.round(goc + mm * s) + 0.5;
            var laLon = Math.abs(mm / lon - Math.round(mm / lon)) < 1e-6;
            var laVua = !laLon && Math.abs(mm / (lon / 2) - Math.round(mm / (lon / 2))) < 1e-6;
            var dd = laLon ? canh * 0.55 : laVua ? canh * 0.35 : canh * 0.2;
            if (doc) { g.moveTo(canh - dd, v); g.lineTo(canh, v); } else { g.moveTo(v, canh - dd); g.lineTo(v, canh); }
            if (laLon) {
                var chu = String(Math.round(mm));
                if (doc) { g.save(); g.translate(9, v + 2); g.rotate(-Math.PI / 2); g.fillText(chu, 0, 0); g.restore(); }
                else g.fillText(chu, v + 2, 9);
            }
        }
        g.stroke();
    }

    function veThuoc() {
        var vung = $('tkVung'), khung = $('tkKhung');
        veMotThuoc($('tkThuocNgang'), false, khung.offsetLeft - vung.scrollLeft, T.hieuRong(trang));
        veMotThuoc($('tkThuocDoc'), true, khung.offsetTop - vung.scrollTop, trang.HeightMm);
    }

    function veLop() {
        var lop = $('tkLop'), s = tyLe();
        lop.replaceChildren();
        chonDs.forEach(function (i) {
            var e = trang.Elements[i];
            if (!e) return;
            var hop = document.createElement('div');
            hop.className = 'tk-chon' + (e.Locked ? ' tk-khoa' : '') + (e.GroupId ? ' tk-nhom-vien' : '');
            hop.style.left = (e.X * s) + 'px';
            hop.style.top = (e.Y * s) + 'px';
            hop.style.width = (e.W * s) + 'px';
            hop.style.height = (e.H * s) + 'px';
            if (chonDs.length === 1 && !e.Locked) {
                ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].forEach(function (h) {
                    var tay = document.createElement('span');
                    tay.className = 'tk-tay tk-tay-' + h;
                    tay.dataset.tay = h;
                    hop.appendChild(tay);
                });
            }
            lop.appendChild(hop);
        });
        if (keo && keo.kieu === 'khung' && keo.hop) {
            var k = document.createElement('div');
            k.className = 'tk-vung-chon';
            k.style.left = (keo.hop.x * s) + 'px';
            k.style.top = (keo.hop.y * s) + 'px';
            k.style.width = (keo.hop.w * s) + 'px';
            k.style.height = (keo.hop.h * s) + 'px';
            lop.appendChild(k);
        }
    }

    function veTabTrang() {
        var ds = $('tkTabDs');
        ds.replaceChildren();
        duAn.forEach(function (t, i) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'tk-tab' + (i === iTrang ? ' dang-mo' : '');
            b.dataset.trang = i;
            b.setAttribute('role', 'tab');
            b.setAttribute('aria-selected', i === iTrang);
            b.title = 'Bấm đúp để đổi tên';
            b.textContent = t.Name;
            ds.appendChild(b);
        });
    }

    // ===== Bảng trái =====

    function danhSachKho() {
        var ds = docLs(KHOA_KHO, null);
        return Array.isArray(ds) && ds.length
            ? ds
            : T.KHO_CO_SAN.map(function (k) { return { ten: k[0], w: k[1], h: k[2] }; });
    }

    function veDanhSachKho() {
        var sel = $('tkKho');
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = 'Chọn khổ…';
        sel.appendChild(op0);
        danhSachKho().forEach(function (k, i) {
            var op = document.createElement('option');
            op.value = i; op.textContent = k.ten;
            sel.appendChild(op);
        });
    }

    function veKhoNhan() {
        $('tkRong').value = trang.WidthMm;
        $('tkCao').value = trang.HeightMm;
        $('tkLe').value = trang.MarginMm;
        $('tkTuDai').checked = trang.AutoLength;
        $('tkRong').disabled = trang.AutoLength;
        document.querySelectorAll('input[name="tkHuong"]').forEach(function (r) {
            r.checked = r.value === (trang.WidthMm >= trang.HeightMm ? 'ngang' : 'doc');
        });
        $('tkMauGiay').value = trang.PaperColor.toLowerCase();
        var selMau = $('tkMauGiayChon');
        selMau.value = Array.prototype.some.call(selMau.options, function (o) { return o.value === trang.PaperColor; }) ? trang.PaperColor : '';
        var khop = danhSachKho().findIndex(function (k) { return k.w === trang.WidthMm && k.h === trang.HeightMm; });
        $('tkKho').value = khop >= 0 ? String(khop) : '';
        var khopMay = khoMay.findIndex(function (m) {
            return m.heightMm == null ? Math.abs(m.widthMm - trang.HeightMm) < 0.6 : (m.widthMm === trang.WidthMm && m.heightMm === trang.HeightMm);
        });
        $('tkKhoMay').value = khopMay >= 0 ? String(khopMay) : '';
    }

    function suaKhoNhan(o) {
        ghiTruoc('kho:' + (o.id || o.name));
        if (o.id === 'tkRong') trang.WidthMm = T.so(o.value, trang.WidthMm, 5, 1000);
        else if (o.id === 'tkCao') trang.HeightMm = T.so(o.value, trang.HeightMm, 5, 1000);
        else if (o.id === 'tkLe') trang.MarginMm = T.so(o.value, trang.MarginMm, 0, 50);
        else if (o.id === 'tkTuDai') trang.AutoLength = o.checked;
        else if (o.id === 'tkMauGiay') trang.PaperColor = o.value.toUpperCase();
        else if (o.id === 'tkMauGiayChon') {
            if (!o.value) { $('tkMauGiay').click(); return; }
            trang.PaperColor = o.value;
        } else if (o.name === 'tkHuong') {
            // Ngang: rộng ≥ cao; Dọc: ngược lại — đổi chiều giấy, giữ nguyên đối tượng như app
            var ngang = o.value === 'ngang';
            if ((trang.WidthMm >= trang.HeightMm) !== ngang) { var r = trang.WidthMm; trang.WidthMm = trang.HeightMm; trang.HeightMm = r; }
        } else if (o.id === 'tkKho') {
            var k = danhSachKho()[parseInt(o.value, 10)];
            if (!k) return;
            trang.WidthMm = k.w; trang.HeightMm = k.h;
        }
        veKhoNhan();
        veNhan();
        capNhatTen();
        if (o.id === 'tkKho' || o.name === 'tkHuong') zoomVua();
    }

    async function themKho() {
        var ten = await nhapChu('Tên khổ', so1(trang.WidthMm) + ' × ' + so1(trang.HeightMm) + ' mm');
        if (!ten) return;
        var ds = danhSachKho().filter(function (k) { return !(k.w === trang.WidthMm && k.h === trang.HeightMm); });
        ds.push({ ten: ten, w: trang.WidthMm, h: trang.HeightMm });
        ghiLs(KHOA_KHO, ds);
        veDanhSachKho(); veKhoNhan();
        trangThai('Đã lưu khổ "' + ten + '" vào danh sách (trong trình duyệt này).');
    }

    function xoaKho() {
        var i = parseInt($('tkKho').value, 10);
        if (!(i >= 0)) { trangThai('Chọn một khổ trong danh sách để xoá.'); return; }
        var ds = danhSachKho();
        var ten = ds[i].ten;
        ds.splice(i, 1);
        ghiLs(KHOA_KHO, ds.length ? ds : null);
        veDanhSachKho(); veKhoNhan();
        trangThai('Đã xoá khổ "' + ten + '" khỏi danh sách.');
    }

    // ===== Bảng thuộc tính (tab như app) =====

    function oNhap(nhan, tt, kieu, giaTri, thuocTinh) {
        var lab = document.createElement('label');
        lab.className = kieu === 'bool' ? 'tk-tt tk-tt-check' : 'tk-tt';
        var o;
        if (kieu === 'chon') {
            o = document.createElement('select');
            o.className = 'form-select form-select-sm';
            thuocTinh.ds.forEach(function (d) {
                var op = document.createElement('option');
                op.value = d[0]; op.textContent = d[1];
                o.appendChild(op);
            });
            o.value = String(giaTri);
        } else if (kieu === 'vanBan') {
            o = document.createElement('textarea');
            o.className = 'form-control form-control-sm';
            o.rows = 3;
            o.value = giaTri;
        } else {
            o = document.createElement('input');
            if (kieu === 'bool') { o.type = 'checkbox'; o.className = 'form-check-input'; o.checked = !!giaTri; }
            else if (kieu === 'so') { o.type = 'number'; o.className = 'form-control form-control-sm'; o.step = (thuocTinh && thuocTinh.buoc) || '0.1'; o.value = giaTri; }
            else { o.type = 'text'; o.className = 'form-control form-control-sm'; o.value = giaTri; }
        }
        o.dataset.tt = tt;
        o.dataset.kieu = kieu;
        var chu = document.createElement('span');
        chu.textContent = nhan;
        if (kieu === 'bool') { lab.appendChild(o); lab.appendChild(chu); }
        else { lab.appendChild(chu); lab.appendChild(o); }
        return lab;
    }

    function taoNut(chu, lenh, them) {
        var b = document.createElement('button');
        b.type = 'button';
        b.textContent = chu;
        b.dataset.lenh = lenh;
        Object.keys(them || {}).forEach(function (k) { if (k === 'title') b.title = them[k]; else if (k === 'disabled') b.disabled = them[k]; else b.dataset[k] = them[k]; });
        return b;
    }

    function taoGoiY(text) {
        var p = document.createElement('p');
        p.className = 'tk-goi-y';
        p.textContent = text;
        return p;
    }

    function taoTieuDe(text) {
        var d = document.createElement('div');
        d.className = 'tk-tieu-de';
        d.textContent = text;
        return d;
    }

    // Ô chọn "+ Ngày in / giờ in / số tự tăng…" chèn trường vào ô nội dung tại con trỏ
    function oChenTruong(ttDich) {
        var sel = document.createElement('select');
        sel.className = 'form-select form-select-sm mb-1';
        sel.dataset.chenVao = ttDich;
        [['', '+ Ngày in / giờ in / số tự tăng / dữ liệu tủ…'], ['{{NGÀY}}', 'Ngày in (dd/MM/yyyy)'], ['{{NGÀY:dd/MM/yyyy HH:mm}}', 'Ngày giờ in'],
            ['{{GIỜ}}', 'Giờ in (HH:mm)'], ['{{SỐ:001}}', 'Số tự tăng 001, 002…']]
            .concat(T.TRUONG_TU.map(function (t) { return ['{{' + t.ten + '}}', 'Dữ liệu tủ: ' + t.ten]; }))
            .forEach(function (d) {
                var op = document.createElement('option');
                op.value = d[0]; op.textContent = d[1];
                sel.appendChild(op);
            });
        return sel;
    }

    function tenLoai(e) {
        if (e.type === 'barcode') return e.Kind === 'QrCode' || e.Kind === 'DataMatrix' ? 'Mã QR' : 'Mã vạch';
        return { text: 'Chữ', frame: 'Khung', line: 'Đường kẻ', image: 'Ảnh' }[e.type] || e.type;
    }

    function veThuocTinh() {
        var p = $('tkThuocTinh');
        p.replaceChildren();
        if (!chonDs.length) {
            var tr = document.createElement('div');
            tr.className = 'tk-trong-chon';
            var st = document.createElement('strong');
            st.textContent = 'Chưa chọn đối tượng';
            tr.appendChild(st);
            var p1 = document.createElement('p');
            p1.textContent = 'Thêm chữ, mã vạch, ảnh và hơn thế';
            tr.appendChild(p1);
            tr.appendChild(taoGoiY('Kéo khung trên vùng trống hoặc Ctrl + bấm để chọn nhiều đối tượng.'));
            tr.appendChild(taoGoiY('In hàng loạt: bấm "Dữ liệu", rồi kéo tên cột thả vào chữ / mã vạch.'));
            tr.appendChild(taoGoiY('Mở mẫu đã lưu bằng nút "Mở"; nhập file .tnl (app TemNhan) hoặc .lbx (P-touch Editor) qua Tệp > Nhập.'));
            p.appendChild(tr);
            return;
        }
        var mot = chonDs.length === 1 ? trang.Elements[chonDs[0]] : null;
        if (!mot && tabTT === 'loai') tabTT = 'bocuc';

        var tabs = document.createElement('div');
        tabs.className = 'tk-tabs';
        tabs.setAttribute('role', 'tablist');
        (mot ? [['loai', tenLoai(mot)]] : []).concat([['bocuc', 'Bố cục'], ['khac', 'Khác']]).forEach(function (t) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = t[0] === tabTT ? 'dang-mo' : '';
            b.dataset.tabTt = t[0];
            b.setAttribute('role', 'tab');
            b.setAttribute('aria-selected', t[0] === tabTT);
            b.textContent = t[1];
            tabs.appendChild(b);
        });
        p.appendChild(tabs);
        if (!mot) p.appendChild(taoGoiY('Đang chọn ' + chonDs.length + ' đối tượng.'));

        if (tabTT === 'loai' && mot) veTabLoai(p, mot);
        else if (tabTT === 'bocuc') veTabBoCuc(p, mot);
        else veTabKhac(p, mot);
    }

    function veTabLoai(p, e) {
        if (e.type === 'text') {
            p.appendChild(oNhap('Nội dung', 'Text', 'vanBan', e.Text));
            p.appendChild(oChenTruong('Text'));
            p.appendChild(taoGoiY('Gõ {{Tên cột}} để lấy dữ liệu tủ khi in hàng loạt.'));
            p.appendChild(oNhap('Phông chữ', 'FontFamily', 'chon', e.FontFamily, {
                ds: T.PHONG_CHU.concat(T.PHONG_CHU.indexOf(e.FontFamily) < 0 ? [e.FontFamily] : []).map(function (f) { return [f, f]; })
            }));
            var co = document.createElement('div');
            co.className = 'tk-tt-co';
            co.appendChild(oNhap('Cỡ chữ (pt)', 'FontSizePt', 'so', e.FontSizePt, { buoc: '0.5' }));
            var b = taoNut('B', 'bat', { tt: 'Bold', title: 'Đậm' });
            b.className = 'tk-nut-bat mb-2' + (e.Bold ? ' bat' : '');
            var it = taoNut('I', 'bat', { tt: 'Italic', title: 'Nghiêng' });
            it.className = 'tk-nut-bat mb-2' + (e.Italic ? ' bat' : '');
            it.style.fontStyle = 'italic';
            co.appendChild(b); co.appendChild(it);
            p.appendChild(co);
            var hai = document.createElement('div');
            hai.className = 'tk-tt-luoi';
            hai.appendChild(oNhap('Căn ngang', 'HAlign', 'chon', e.HAlign, { ds: [['Left', 'Trái'], ['Center', 'Giữa'], ['Right', 'Phải']] }));
            hai.appendChild(oNhap('Căn dọc', 'VAlign', 'chon', e.VAlign, { ds: [['Top', 'Trên'], ['Center', 'Giữa'], ['Bottom', 'Dưới']] }));
            p.appendChild(hai);
            p.appendChild(oNhap('Tự thu nhỏ cho vừa khung', 'ShrinkToFit', 'bool', e.ShrinkToFit));
            p.appendChild(oNhap('Tự xuống dòng', 'Wrap', 'bool', e.Wrap));
        } else if (e.type === 'frame') {
            p.appendChild(oNhap('Độ dày viền (mm)', 'StrokeMm', 'so', e.StrokeMm));
            p.appendChild(oNhap('Bo góc (mm)', 'CornerMm', 'so', e.CornerMm));
            p.appendChild(oNhap('Tô đen', 'Filled', 'bool', e.Filled));
        } else if (e.type === 'line') {
            p.appendChild(oNhap('Độ dày (mm)', 'StrokeMm', 'so', e.StrokeMm));
            p.appendChild(oNhap('Hướng', 'Orientation', 'chon', e.Orientation, { ds: [['Horizontal', 'Ngang'], ['Vertical', 'Dọc']] }));
        } else if (e.type === 'barcode') {
            var ds = [['Code128', 'Code 128'], ['Code39', 'Code 39'], ['Ean13', 'EAN-13'], ['QrCode', 'Mã QR']];
            if (e.Kind === 'DataMatrix') ds.push(['DataMatrix', 'DataMatrix (web in thành QR)']);
            p.appendChild(oNhap('Loại mã', 'Kind', 'chon', e.Kind, { ds: ds }));
            p.appendChild(oNhap('Dữ liệu', 'Data', 'chu', e.Data));
            p.appendChild(oChenTruong('Data'));
            if (e.Kind !== 'QrCode' && e.Kind !== 'DataMatrix') {
                p.appendChild(oNhap('Hiện chữ dưới mã vạch', 'ShowText', 'bool', e.ShowText));
                p.appendChild(oNhap('Cỡ chữ (pt)', 'TextSizePt', 'so', e.TextSizePt, { buoc: '0.5' }));
            }
        } else if (e.type === 'image') {
            p.appendChild(oNhap('Giữ tỉ lệ ảnh', 'KeepAspect', 'bool', e.KeepAspect));
            p.appendChild(oNhap('Chuyển trắng đen (xám)', 'Monochrome', 'bool', e.Monochrome));
            var nut = taoNut('Thay ảnh khác…', 'thayAnh');
            nut.className = 'btn btn-sm btn-light border w-100 mt-1';
            p.appendChild(nut);
        }
    }

    function veTabBoCuc(p, mot) {
        if (mot) {
            var luoi = document.createElement('div');
            luoi.className = 'tk-tt-luoi';
            luoi.appendChild(oNhap('X (mm)', 'X', 'so', mot.X));
            luoi.appendChild(oNhap('Y (mm)', 'Y', 'so', mot.Y));
            luoi.appendChild(oNhap('Rộng (mm)', 'W', 'so', mot.W));
            luoi.appendChild(oNhap('Cao (mm)', 'H', 'so', mot.H));
            p.appendChild(luoi);
        }
        p.appendChild(taoTieuDe('Xoay'));
        var xoayH = document.createElement('div');
        xoayH.className = 'tk-nut-hang';
        xoayH.appendChild(taoNut('↺ 90°', 'xoay', { goc: '-90', title: 'Xoay 90° sang trái' }));
        xoayH.appendChild(taoNut('↻ 90°', 'xoay', { goc: '90', title: 'Xoay 90° sang phải' }));
        p.appendChild(xoayH);
        p.appendChild(taoTieuDe('Căn chỉnh'));
        var can = document.createElement('div');
        can.className = 'tk-nut-luoi';
        [['⇤ Trái', 'trai'], ['↔ Giữa', 'giuaNgang'], ['Phải ⇥', 'phai'], ['⤒ Trên', 'tren'], ['↕ Giữa', 'giuaDoc'], ['Dưới ⤓', 'duoi']].forEach(function (c) {
            can.appendChild(taoNut(c[0], 'can', { huong: c[1] }));
        });
        p.appendChild(can);
        var deu = document.createElement('div');
        deu.className = 'tk-nut-hang';
        deu.appendChild(taoNut('Đều ngang', 'phanBo', { truc: 'ngang', title: 'Phân bố đều theo chiều ngang (≥ 3 đối tượng)', disabled: chonDs.length < 3 }));
        deu.appendChild(taoNut('Đều dọc', 'phanBo', { truc: 'doc', title: 'Phân bố đều theo chiều dọc (≥ 3 đối tượng)', disabled: chonDs.length < 3 }));
        p.appendChild(deu);
        var co = document.createElement('div');
        co.className = 'tk-nut-luoi';
        co.appendChild(taoNut('Vừa vùng in', 'vuaVungIn'));
        co.appendChild(taoNut('To hơn', 'doiCo', { kieu: 'to' }));
        co.appendChild(taoNut('Nhỏ hơn', 'doiCo', { kieu: 'nho' }));
        p.appendChild(co);
    }

    function veTabKhac(p, mot) {
        var tatCaKhoa = doiTuongChon().every(function (e) { return e.Locked; });
        var tatCaIn = doiTuongChon().every(function (e) { return e.Printable; });
        p.appendChild(oNhap('Khoá vị trí (không kéo / đổi cỡ bằng chuột)', 'Locked', 'bool', tatCaKhoa));
        p.appendChild(oNhap('In đối tượng này', 'Printable', 'bool', tatCaIn));
        p.appendChild(taoGoiY('Bỏ tích: chỉ hiện khi thiết kế (ghi chú, khung căn chỉnh…), không in ra.'));
        p.appendChild(taoTieuDe('Nhóm'));
        var nhom = document.createElement('div');
        nhom.className = 'tk-nut-hang';
        nhom.appendChild(taoNut('Nhóm (Ctrl+G)', 'nhom', { disabled: chonDs.length < 2 }));
        nhom.appendChild(taoNut('Bỏ nhóm', 'boNhom', { disabled: !doiTuongChon().some(function (e) { return e.GroupId; }) }));
        p.appendChild(nhom);
        p.appendChild(taoGoiY('Các đối tượng đã nhóm được chọn và di chuyển cùng nhau.'));
        p.appendChild(taoTieuDe('Sao chép'));
        var chep = document.createElement('div');
        chep.className = 'tk-nut-hang';
        chep.appendChild(taoNut('Cắt', 'cat'));
        chep.appendChild(taoNut('Chép', 'chep'));
        chep.appendChild(taoNut('Dán', 'dan', { disabled: !boNho }));
        p.appendChild(chep);
        p.appendChild(taoTieuDe('Thứ tự'));
        var lop = document.createElement('div');
        lop.className = 'tk-nut-luoi';
        lop.appendChild(taoNut('Lên trên cùng', 'lop', { buoc: 'tren' }));
        lop.appendChild(taoNut('Lên 1 lớp', 'lop', { buoc: 'len' }));
        lop.appendChild(taoNut('Xuống 1 lớp', 'lop', { buoc: 'xuong' }));
        lop.appendChild(taoNut('Xuống dưới', 'lop', { buoc: 'duoi' }));
        p.appendChild(lop);
        if (mot) {
            var xoa = taoNut('Xoá đối tượng', 'xoa');
            xoa.className = 'btn btn-sm btn-outline-danger w-100';
            p.appendChild(xoa);
        }
    }

    function capNhatNut() {
        document.querySelectorAll('[data-lenh="hoanTac"]').forEach(function (b) { b.disabled = !lichSu.length; });
        document.querySelectorAll('[data-lenh="lamLai"]').forEach(function (b) { b.disabled = !lamLaiDs.length; });
        document.querySelectorAll('[data-can-chon]').forEach(function (b) { b.disabled = !chonDs.length; });
        document.querySelectorAll('[data-lenh="dan"]').forEach(function (b) { b.disabled = !boNho; });
        document.querySelectorAll('[data-lenh="xoaMau"]').forEach(function (b) { b.disabled = !idMau; });
        document.querySelectorAll('[data-lenh="xoaTrang"]').forEach(function (b) { b.disabled = duAn.length < 2; });
        document.querySelectorAll('[data-lenh="trangTruoc"]').forEach(function (b) { b.disabled = iTrang === 0; });
        document.querySelectorAll('[data-lenh="trangSau"]').forEach(function (b) { b.disabled = iTrang >= duAn.length - 1; });
        var tr = $('tkTrang'), hep = manHep.matches;
        var bat = {
            batLuoi: $('tkBatLuoi').checked,
            xemDuLieu: $('tkXemDuLieu').checked,
            banTrai: hep ? tr.classList.contains('mo-trai') : !tr.classList.contains('an-trai'),
            banPhai: hep ? tr.classList.contains('mo-phai') : !tr.classList.contains('an-phai'),
            banDuLieu: tr.classList.contains('mo-du-lieu')
        };
        Object.keys(bat).forEach(function (l) {
            document.querySelectorAll('[data-lenh="' + l + '"]').forEach(function (b) {
                b.classList.toggle('bat', bat[l]);
                if (b.closest('.tk-menu-ds')) b.setAttribute('aria-checked', bat[l]);
            });
        });
    }

    function capNhatTen() {
        var ten = $('tkTenMau').value.trim();
        var o = $('tkTenMauHien');
        o.textContent = (ten || 'Mẫu mới (chưa lưu)') + (duAn.length > 1 ? ' — ' + duAn.length + ' trang' : '');
        o.classList.toggle('chua-luu', daSua);
        o.title = daSua ? 'Có thay đổi chưa lưu' : 'Mẫu đang mở';
        veChonMau();
    }

    function veTatCa() {
        veTabTrang();
        veKhoNhan();
        veNhan();
        veThuocTinh();
        capNhatNut();
        capNhatTen();
    }

    // Vẽ lại sau khi sửa thuộc tính: giữ nguyên bảng thuộc tính để ô đang gõ không mất con trỏ
    function veSauSua() {
        veNhan();
        capNhatNut();
        capNhatTen();
    }

    // Chọn 1 đối tượng trong nhóm là chọn cả nhóm (như app)
    function moRongNhom(ds) {
        var nhom = {};
        ds.forEach(function (i) { var g = trang.Elements[i] && trang.Elements[i].GroupId; if (g) nhom[g] = true; });
        var kq = ds.slice();
        trang.Elements.forEach(function (e, i) { if (e.GroupId && nhom[e.GroupId] && kq.indexOf(i) < 0) kq.push(i); });
        return kq;
    }

    function chonLai(ds) {
        chonDs = moRongNhom(ds);
        if (chonDs.length !== 1 && tabTT === 'loai') tabTT = 'bocuc';
        if (chonDs.length === 1) tabTT = 'loai';
        veLop();
        veThuocTinh();
        capNhatNut();
        trangThai(chonDs.length ? 'Đã chọn ' + chonDs.length + ' đối tượng' : 'Sẵn sàng');
    }

    // ===== Thao tác đối tượng =====

    function batLuoi(v, e) {
        if (!$('tkBatLuoi').checked || (e && e.altKey)) return T.so(v);
        return Math.round(v / BUOC_LUOI) * BUOC_LUOI;
    }

    function doiTuongChon() { return chonDs.map(function (i) { return trang.Elements[i]; }).filter(Boolean); }

    function vungIn() {
        var le = trang.MarginMm, rong = T.hieuRong(trang);
        return { x: le, y: le, w: rong - 2 * le, h: trang.HeightMm - 2 * le };
    }

    function themDoiTuong(o) {
        ghiTruoc();
        trang.Elements.push(T.chuanHoaDoiTuong(o));
        chonDs = [trang.Elements.length - 1];
        tabTT = 'loai';
        veTatCa();
    }

    function chen(loai, viTri) {
        var v = vungIn();
        var x = viTri ? viTri.x : v.x + 1, y = viTri ? viTri.y : v.y + 1;
        if (loai === 'image') { $('tkFileAnh').dataset.thay = ''; $('tkFileAnh').click(); return; }
        if (loai === 'text' || loai === 'ngay' || loai === 'so') {
            themDoiTuong({ type: 'text', Text: loai === 'ngay' ? '{{NGÀY}}' : loai === 'so' ? '{{SỐ:001}}' : 'Chữ', X: x, Y: y, W: Math.max(5, Math.min(30, v.w - 2)), H: Math.max(3, Math.min(8, v.h - 2)) });
        } else if (loai === 'frame') {
            themDoiTuong({ type: 'frame', X: v.x, Y: v.y, W: v.w, H: v.h });
        } else if (loai === 'line') {
            themDoiTuong({ type: 'line', X: x, Y: trang.HeightMm / 2 - 0.5, W: Math.max(5, v.w - 2), H: 1 });
        } else if (loai === 'barcode') {
            themDoiTuong({ type: 'barcode', Kind: 'Code128', Data: '{{Tên tủ}}', X: x, Y: y, W: Math.max(10, Math.min(35, v.w - 2)), H: Math.max(5, Math.min(12, v.h - 2)) });
        } else if (loai === 'qr') {
            var c = Math.max(5, Math.min(18, v.h - 2, v.w - 2));
            themDoiTuong({ type: 'barcode', Kind: 'QrCode', Data: '{{Tên tủ}}', X: x, Y: y, W: c, H: c });
        }
    }

    function xoaChon() {
        if (!chonDs.length) return;
        ghiTruoc();
        trang.Elements = trang.Elements.filter(function (e, i) { return chonDs.indexOf(i) < 0; });
        chonDs = [];
        veTatCa();
    }

    function nhanBan(dsGoc, lech) {
        if (!dsGoc.length) return;
        ghiTruoc();
        var moi = [], doiNhom = {};
        dsGoc.forEach(function (e) {
            var o = JSON.parse(JSON.stringify(e));
            o.X = T.so(o.X + lech); o.Y = T.so(o.Y + lech); o.Locked = false;
            // Bản sao của 1 nhóm thành nhóm mới, không dính nhóm gốc
            if (o.GroupId) o.GroupId = doiNhom[o.GroupId] || (doiNhom[o.GroupId] = 'g' + Date.now().toString(36) + Math.random().toString(36).slice(2, 6));
            trang.Elements.push(T.chuanHoaDoiTuong(o));
            moi.push(trang.Elements.length - 1);
        });
        chonDs = moi;
        veTatCa();
    }

    function chep() { if (chonDs.length) { boNho = JSON.parse(JSON.stringify(doiTuongChon())); capNhatNut(); trangThai('Đã chép ' + boNho.length + ' đối tượng.'); } }
    function cat() { if (chonDs.length) { chep(); xoaChon(); } }
    function dan() { if (boNho) nhanBan(boNho, 1); }

    function canChinh(huong) {
        var ds = doiTuongChon();
        if (!ds.length) return;
        ghiTruoc();
        var v = vungIn();
        // 1 đối tượng / 1 nhóm: căn theo vùng in của nhãn; nhiều đối tượng: căn theo khung bao của nhóm chọn
        var trai = v.x, tren = v.y, phai = v.x + v.w, duoi = v.y + v.h;
        var motKhoi = ds.length === 1 || ds.every(function (e) { return e.GroupId && e.GroupId === ds[0].GroupId; });
        if (!motKhoi) {
            trai = Math.min.apply(null, ds.map(function (e) { return e.X; }));
            tren = Math.min.apply(null, ds.map(function (e) { return e.Y; }));
            phai = Math.max.apply(null, ds.map(function (e) { return e.X + e.W; }));
            duoi = Math.max.apply(null, ds.map(function (e) { return e.Y + e.H; }));
            ds.forEach(function (e) { canMot(e, huong, trai, tren, phai, duoi); });
        } else {
            // Nhóm: dời cả khối theo khung bao của nhóm, giữ vị trí tương đối
            var bx = Math.min.apply(null, ds.map(function (e) { return e.X; })), by = Math.min.apply(null, ds.map(function (e) { return e.Y; }));
            var bw = Math.max.apply(null, ds.map(function (e) { return e.X + e.W; })) - bx, bh = Math.max.apply(null, ds.map(function (e) { return e.Y + e.H; })) - by;
            var khoi = { X: bx, Y: by, W: bw, H: bh };
            canMot(khoi, huong, trai, tren, phai, duoi);
            ds.forEach(function (e) { if (!e.Locked) { e.X = T.so(e.X + khoi.X - bx); e.Y = T.so(e.Y + khoi.Y - by); } });
        }
        veTatCa();
    }

    function canMot(e, huong, trai, tren, phai, duoi) {
        if (e.Locked) return;
        if (huong === 'trai') e.X = T.so(trai);
        else if (huong === 'giuaNgang') e.X = T.so((trai + phai - e.W) / 2);
        else if (huong === 'phai') e.X = T.so(phai - e.W);
        else if (huong === 'tren') e.Y = T.so(tren);
        else if (huong === 'giuaDoc') e.Y = T.so((tren + duoi - e.H) / 2);
        else if (huong === 'duoi') e.Y = T.so(duoi - e.H);
    }

    // Phân bố đều (≥ 3 đối tượng): giữ 2 đối tượng ngoài cùng, chia đều khoảng hở ở giữa
    function phanBo(truc) {
        var ds = doiTuongChon().filter(function (e) { return !e.Locked; });
        if (ds.length < 3) { trangThai('Cần chọn ít nhất 3 đối tượng để phân bố đều.'); return; }
        ghiTruoc();
        var ngang = truc === 'ngang';
        ds.sort(function (a, b) { return ngang ? a.X - b.X : a.Y - b.Y; });
        var dau = ngang ? ds[0].X : ds[0].Y;
        var cuoi = ngang ? ds[ds.length - 1].X + ds[ds.length - 1].W : ds[ds.length - 1].Y + ds[ds.length - 1].H;
        var tong = ds.reduce(function (s, e) { return s + (ngang ? e.W : e.H); }, 0);
        var ho = (cuoi - dau - tong) / (ds.length - 1), vt = dau;
        ds.forEach(function (e) {
            if (ngang) { e.X = T.so(vt); vt += e.W + ho; } else { e.Y = T.so(vt); vt += e.H + ho; }
        });
        veTatCa();
    }

    // Xoay 90°: đổi luôn rộng/cao quanh tâm để chữ xoay dọc vẫn đủ chiều dài (như app TemNhan)
    function xoay(goc) {
        var ds = doiTuongChon();
        if (!ds.length) return;
        ghiTruoc();
        ds.forEach(function (e) {
            var cx = e.X + e.W / 2, cy = e.Y + e.H / 2, w = e.W;
            e.W = e.H; e.H = w;
            e.X = T.so(cx - e.W / 2); e.Y = T.so(cy - e.H / 2);
            e.Rotation = ((e.Rotation + goc) % 360 + 360) % 360;
        });
        veTatCa();
    }

    function doiLop(buoc) {
        if (!chonDs.length) return;
        ghiTruoc();
        var ds = trang.Elements, laChon = ds.map(function (e, i) { return chonDs.indexOf(i) >= 0; });
        var chonEl = doiTuongChon();
        if (buoc === 'tren' || buoc === 'duoi') {
            var conLai = ds.filter(function (e, i) { return !laChon[i]; });
            trang.Elements = buoc === 'tren' ? conLai.concat(chonEl) : chonEl.concat(conLai);
        } else if (buoc === 'len') {
            for (var i = ds.length - 2; i >= 0; i--) {
                if (laChon[i] && !laChon[i + 1]) { var t = ds[i]; ds[i] = ds[i + 1]; ds[i + 1] = t; laChon[i] = false; laChon[i + 1] = true; }
            }
        } else {
            for (var j = 1; j < ds.length; j++) {
                if (laChon[j] && !laChon[j - 1]) { var u = ds[j]; ds[j] = ds[j - 1]; ds[j - 1] = u; laChon[j] = false; laChon[j - 1] = true; }
            }
        }
        chonDs = chonEl.map(function (e) { return trang.Elements.indexOf(e); });
        veTatCa();
    }

    function dich(dx, dy) {
        var ds = doiTuongChon().filter(function (e) { return !e.Locked; });
        if (!ds.length) return;
        ghiTruoc('dich');
        ds.forEach(function (e) { e.X = T.so(e.X + dx); e.Y = T.so(e.Y + dy); });
        veNhan();
        if (tabTT === 'bocuc') veThuocTinh();
    }

    function vuaVungIn() {
        var ds = doiTuongChon().filter(function (e) { return !e.Locked; });
        if (!ds.length) return;
        ghiTruoc();
        var v = vungIn();
        ds.forEach(function (e) { e.X = T.so(v.x); e.Y = T.so(v.y); e.W = T.so(Math.max(0.5, v.w)); e.H = T.so(Math.max(0.5, v.h)); });
        veTatCa();
    }

    function doiCo(kieu) {
        var ds = doiTuongChon().filter(function (e) { return !e.Locked; });
        if (!ds.length) return;
        ghiTruoc();
        var v = vungIn();
        ds.forEach(function (e) {
            var cx = e.X + e.W / 2, cy = e.Y + e.H / 2;
            if (kieu === 'to' || kieu === 'nho') {
                var k = kieu === 'to' ? 1.1 : 1 / 1.1;
                e.W = T.so(Math.max(0.5, e.W * k)); e.H = T.so(Math.max(0.5, e.H * k));
                e.X = T.so(cx - e.W / 2); e.Y = T.so(cy - e.H / 2);
            } else if (kieu === 'toiDa') {
                e.X = T.so(v.x); e.Y = T.so(v.y); e.W = T.so(v.w); e.H = T.so(v.h);
            } else {
                var r = Math.min(v.w / e.W, v.h / e.H);
                e.W = T.so(e.W * r); e.H = T.so(e.H * r);
                e.X = T.so(v.x + (v.w - e.W) / 2); e.Y = T.so(v.y + (v.h - e.H) / 2);
            }
        });
        veTatCa();
    }

    function nhom() {
        if (chonDs.length < 2) { trangThai('Chọn từ 2 đối tượng trở lên để nhóm.'); return; }
        ghiTruoc();
        var g = 'g' + Date.now().toString(36);
        doiTuongChon().forEach(function (e) { e.GroupId = g; });
        veTatCa();
        trangThai('Đã nhóm ' + chonDs.length + ' đối tượng.');
    }

    function boNhom() {
        if (!doiTuongChon().some(function (e) { return e.GroupId; })) return;
        ghiTruoc();
        doiTuongChon().forEach(function (e) { e.GroupId = null; });
        veTatCa();
        trangThai('Đã bỏ nhóm.');
    }

    // Chèn trường vào chữ / mã vạch (đối tượng i hoặc đang chọn); không có thì tạo ô chữ mới tại viTri
    function chenTruong(the, i, viTri) {
        var e = i != null ? trang.Elements[i] : (chonDs.length === 1 ? trang.Elements[chonDs[0]] : null);
        if (e && e.type === 'text') {
            ghiTruoc();
            e.Text = (e.Text === 'Chữ' || !e.Text) ? the : e.Text + ' ' + the;
        } else if (e && e.type === 'barcode') {
            ghiTruoc();
            e.Data = the;
        } else {
            var v = vungIn();
            themDoiTuong({ type: 'text', Text: the, X: viTri ? batLuoi(viTri.x) : v.x + 1, Y: viTri ? batLuoi(viTri.y) : v.y + 1, W: Math.max(5, Math.min(30, v.w - 2)), H: Math.max(3, Math.min(7, v.h - 2)), HAlign: 'Left' });
            return;
        }
        chonDs = [trang.Elements.indexOf(e)];
        tabTT = 'loai';
        veSauSua();
        veThuocTinh();
    }

    function suaThuocTinh(o) {
        var tt = o.dataset.tt, kieu = o.dataset.kieu, v;
        if (kieu === 'bool') v = o.checked;
        else if (kieu === 'so') { v = parseFloat(o.value); if (!isFinite(v)) return; }
        else if (tt === 'Rotation') v = parseInt(o.value, 10);
        else v = o.value;
        // Khoá / In ra áp cho mọi đối tượng đang chọn; các thuộc tính khác chỉ khi chọn 1
        var dsI = (tt === 'Locked' || tt === 'Printable') ? chonDs : (chonDs.length === 1 ? chonDs : []);
        if (!dsI.length) return;
        ghiTruoc('tt:' + dsI.join(',') + ':' + tt);
        dsI.forEach(function (i) {
            var e = trang.Elements[i];
            e[tt] = v;
            trang.Elements[i] = T.chuanHoaDoiTuong(e);
        });
        veSauSua();
        if (tt === 'Kind') veThuocTinh(); // đổi loại mã làm đổi tab + các ô hiện ra
    }

    function batTatThuocTinh(tt) {
        var e = chonDs.length === 1 ? trang.Elements[chonDs[0]] : null;
        if (!e) return;
        ghiTruoc();
        e[tt] = !e[tt];
        veSauSua();
        veThuocTinh();
    }

    // ===== Trang (tab) =====

    function chuyenTrang(i) {
        if (i < 0 || i >= duAn.length || i === iTrang) return;
        iTrang = i;
        trang = duAn[i];
        chonDs = [];
        veTatCa();
        zoomVua();
    }

    function themTrang() {
        ghiTruoc();
        var t = T.taoTrang();
        t.WidthMm = trang.WidthMm; t.HeightMm = trang.HeightMm; t.MarginMm = trang.MarginMm; t.PaperColor = trang.PaperColor;
        t.Name = tenTrangMoi();
        duAn.push(t);
        iTrang = duAn.length - 1; trang = t; chonDs = [];
        veTatCa();
    }

    function tenTrangMoi() {
        var n = duAn.length + 1;
        while (duAn.some(function (t) { return t.Name === 'Trang ' + n; })) n++;
        return 'Trang ' + n;
    }

    function nhanBanTrang() {
        ghiTruoc();
        var t = T.chuanHoaTrang(JSON.parse(JSON.stringify(trang)));
        t.Name = trang.Name + ' (bản sao)';
        duAn.splice(iTrang + 1, 0, t);
        iTrang++; trang = t; chonDs = [];
        veTatCa();
    }

    async function xoaTrang() {
        if (duAn.length < 2) return;
        if (!await hoi('Xoá trang "' + trang.Name + '"?', 'Xoá')) return;
        ghiTruoc();
        duAn.splice(iTrang, 1);
        iTrang = Math.min(iTrang, duAn.length - 1); trang = duAn[iTrang]; chonDs = [];
        veTatCa();
    }

    async function doiTenTrang(i) {
        var t = duAn[i == null ? iTrang : i];
        var ten = await nhapChu('Tên trang', t.Name);
        if (!ten || ten === t.Name) return;
        ghiTruoc();
        t.Name = ten;
        veTabTrang();
        capNhatTen();
        if (daSua) ghiNhap();
    }

    // ===== Chuột: chọn, kéo, đổi cỡ, kéo khung chọn =====

    // clientX và getBoundingClientRect tính theo px màn hình; khi hệ thống zoom (body{zoom:60%}) px màn hình ≠ px CSS
    // của nhãn → quy về px CSS bằng tỉ lệ rect / offsetWidth, nếu không thì bấm lệch và khung chọn vẽ sai chỗ.
    function heSoManHinh(el) {
        var r = el.getBoundingClientRect();
        return el.offsetWidth ? r.width / el.offsetWidth : zoomHeThong();
    }

    function viTriMm(ev) {
        var khung = $('tkKhung'), r = khung.getBoundingClientRect(), s = tyLe(), k = heSoManHinh(khung) || 1;
        return { x: (ev.clientX - r.left) / k / s, y: (ev.clientY - r.top) / k / s };
    }

    function timDoiTuong(p) {
        var dung = 3 / tyLe(); // nới 3px để bấm trúng đường kẻ mảnh
        for (var i = trang.Elements.length - 1; i >= 0; i--) {
            var e = trang.Elements[i];
            if (p.x >= e.X - dung && p.x <= e.X + e.W + dung && p.y >= e.Y - dung && p.y <= e.Y + e.H + dung) return i;
        }
        return -1;
    }

    function batDauKeo(ev) {
        if (ev.button !== 0) return;
        var vung = $('tkVung');
        // Bấm vào thanh cuộn của vùng vẽ thì để trình duyệt cuộn, không bắt đầu kéo
        if (ev.target === vung && (ev.offsetX > vung.clientWidth || ev.offsetY > vung.clientHeight)) return;
        var p = viTriMm(ev);
        var them = ev.ctrlKey || ev.metaKey || ev.shiftKey;
        var tay = ev.target.closest('[data-tay]');
        if (tay && chonDs.length === 1) {
            var e0 = trang.Elements[chonDs[0]];
            keo = { kieu: 'co', tay: tay.dataset.tay, p0: p, goc: { X: e0.X, Y: e0.Y, W: e0.W, H: e0.H }, truoc: chup(), daDoi: false };
        } else {
            var i = timDoiTuong(p);
            if (i < 0) {
                // Vùng trống: kéo khung chọn (giữ Ctrl/Shift để cộng thêm vào lựa chọn cũ)
                keo = { kieu: 'khung', p0: p, cu: them ? chonDs.slice() : [], hop: null, daDoi: false };
                if (!them) chonLai([]);
            } else if (them) {
                var nhomI = moRongNhom([i]);
                var dangChon = chonDs.indexOf(i) >= 0;
                chonLai(dangChon ? chonDs.filter(function (k) { return nhomI.indexOf(k) < 0; }) : chonDs.concat(nhomI));
                return;
            } else {
                if (chonDs.indexOf(i) < 0) chonLai([i]);
                keo = {
                    kieu: 'di', p0: p, truoc: chup(), daDoi: false,
                    goc: chonDs.map(function (k) { var e = trang.Elements[k]; return { i: k, X: e.X, Y: e.Y }; })
                };
            }
        }
        $('tkVung').setPointerCapture(ev.pointerId);
        // Kéo ra ngoài vùng vẽ không được bôi đen chữ ở bảng bên cạnh
        document.documentElement.classList.add('tk-dang-keo');
        ev.preventDefault();
    }

    function dangKeo(ev) {
        var p = viTriMm(ev);
        $('tkToaDo').textContent = 'X ' + so1(p.x) + ' · Y ' + so1(p.y) + ' mm';
        if (!keo) return;
        var dx = p.x - keo.p0.x, dy = p.y - keo.p0.y;
        if (!keo.daDoi && Math.abs(dx) * tyLe() < 3 && Math.abs(dy) * tyLe() < 3) return;
        keo.daDoi = true;
        if (keo.kieu === 'khung') {
            keo.hop = { x: Math.min(p.x, keo.p0.x), y: Math.min(p.y, keo.p0.y), w: Math.abs(dx), h: Math.abs(dy) };
            var h = keo.hop;
            var trong = trang.Elements.map(function (e, i) { return i; }).filter(function (i) {
                var e = trang.Elements[i];
                return e.X < h.x + h.w && e.X + e.W > h.x && e.Y < h.y + h.h && e.Y + e.H > h.y;
            });
            chonDs = moRongNhom(keo.cu.concat(trong.filter(function (i) { return keo.cu.indexOf(i) < 0; })));
            veLop();
            return;
        }
        if (keo.kieu === 'di') {
            keo.goc.forEach(function (g) {
                var e = trang.Elements[g.i];
                if (e.Locked) return;
                e.X = batLuoi(g.X + dx, ev);
                e.Y = batLuoi(g.Y + dy, ev);
            });
        } else {
            var e = trang.Elements[chonDs[0]], g = keo.goc, t = keo.tay;
            var trai = g.X, tren = g.Y, phai = g.X + g.W, duoi = g.Y + g.H;
            if (t.indexOf('w') >= 0) trai = Math.min(batLuoi(g.X + dx, ev), phai - 0.5);
            if (t.indexOf('e') >= 0) phai = Math.max(batLuoi(g.X + g.W + dx, ev), trai + 0.5);
            if (t.indexOf('n') >= 0) tren = Math.min(batLuoi(g.Y + dy, ev), duoi - 0.5);
            if (t.indexOf('s') >= 0) duoi = Math.max(batLuoi(g.Y + g.H + dy, ev), tren + 0.5);
            e.X = T.so(trai); e.Y = T.so(tren); e.W = T.so(phai - trai); e.H = T.so(duoi - tren);
        }
        if (!henVe) henVe = requestAnimationFrame(function () { henVe = 0; veNhan(); });
    }

    function ketThucKeo(ev) {
        document.documentElement.classList.remove('tk-dang-keo');
        if (!keo) return;
        var k = keo;
        keo = null;
        try { $('tkVung').releasePointerCapture(ev.pointerId); } catch (ex) { /* đã nhả */ }
        if (k.kieu === 'khung') { chonLai(chonDs); return; }
        if (k.daDoi) {
            lichSu.push(k.truoc);
            if (lichSu.length > 100) lichSu.shift();
            lamLaiDs = []; khoaCuoi = null; daSua = true;
        }
        veNhan();
        if (tabTT === 'bocuc') veThuocTinh();
        capNhatNut();
        capNhatTen();
    }

    // ===== Bố cục thích nghi =====
    // Khung rộng: hai bảng là cột, bật/tắt được (nhớ trong trình duyệt). Khung hẹp: các bảng là ngăn kéo, mở đè lên vùng vẽ.
    // Đo bề rộng thật của #tkTrang (không theo cửa sổ): laptop 1366px còn menu trái của trang thì 2 cột 214+250px
    // chiếm gần hết, vùng vẽ tem chỉ còn ~600px → zoom tem lên là phải cuộn.

    var NGUONG_HEP = 1080;               // ≈ 2 cột + vùng vẽ tối thiểu ~600px
    var manHep = { matches: false };

    function capNhatHep(lanDau) {
        var tr = $('tkTrang');
        var hep = tr.clientWidth > 0 && tr.clientWidth < NGUONG_HEP;
        if (hep === manHep.matches) return;
        manHep.matches = hep;
        tr.classList.toggle('tk-hep', hep);
        if (lanDau === true) return; // lúc khởi tạo tem chưa vẽ, zoomVua chạy sau trong init
        dongNganKeo();
        zoomVua();
    }

    function batTatBan(ben) {
        var tr = $('tkTrang');
        if (ben === 'du-lieu') {
            var mo = !tr.classList.contains('mo-du-lieu');
            if (manHep.matches) tr.classList.remove('mo-trai', 'mo-phai');
            tr.classList.toggle('mo-du-lieu', mo);
            if (mo && nguonDl === 'tu' && !dsTu.length) taiDsTu();
        } else if (manHep.matches) {
            var moBan = !tr.classList.contains('mo-' + ben);
            tr.classList.remove('mo-trai', 'mo-phai', 'mo-du-lieu'); // mỗi lần chỉ mở 1 ngăn kéo
            tr.classList.toggle('mo-' + ben, moBan);
        } else {
            tr.classList.toggle('an-' + ben);
            ghiLs(KHOA_BO_CUC, { anTrai: tr.classList.contains('an-trai'), anPhai: tr.classList.contains('an-phai') });
        }
        capNhatNut();
        requestAnimationFrame(function () {
            // Vùng vẽ hẹp lại làm nhãn tràn → thu cho vừa; không thì chỉ vẽ lại thước
            var v = $('tkVung'), k = $('tkKhung');
            if (!manHep.matches && (k.offsetWidth + 60 > v.clientWidth || k.offsetHeight + 90 > v.clientHeight)) zoomVua(); else veThuoc();
        });
    }

    function dongNganKeo() {
        $('tkTrang').classList.remove('mo-trai', 'mo-phai');
        if (manHep.matches) $('tkTrang').classList.remove('mo-du-lieu');
        capNhatNut();
    }

    // Khung thiết kế luôn kéo tới sát đáy cửa sổ, mọi cỡ màn và mọi mức zoom của hệ thống:
    // khoá cuộn trang rồi đo khoảng trống thật từ mép trên khung tới đáy (CSS calc(100vh - 110px) đoán sai khi header đổi cao / zoom).
    var CACH_DAY = 4;                    // px màn hình chừa dưới đáy cho thấy viền bo

    function zoomHeThong() {
        var z = parseFloat(getComputedStyle(document.body).zoom);
        if (/%$/.test(getComputedStyle(document.body).zoom)) z = z / 100;
        return z > 0 ? z : 1;
    }

    function coDinhChieuCao() {
        var tr = $('tkTrang');
        document.documentElement.classList.add('tk-khoa');
        window.scrollTo(0, 0);
        var z = zoomHeThong();
        var r = tr.getBoundingClientRect();
        // Tuỳ trình duyệt, rect trả theo px màn hình (đã nhân zoom) hoặc px CSS: so với offsetHeight để quy về px màn hình
        var k = tr.offsetHeight ? r.height / tr.offsetHeight : z;
        var tren = r.top * (k ? z / k : 1);
        // px gán vào bị body{zoom} nhân thêm → chia lại
        tr.style.height = Math.max(360, Math.floor((window.innerHeight - tren - CACH_DAY) / z)) + 'px';
        tr.style.flex = '0 0 auto';      // chuỗi flex của fixed-page-layout không được co/giãn lại chiều cao đã tính
    }

    function khoiTaoBoCuc() {
        coDinhChieuCao();
        var b = docLs(KHOA_BO_CUC, {});
        $('tkTrang').classList.toggle('an-trai', !!b.anTrai);
        $('tkTrang').classList.toggle('an-phai', !!b.anPhai);
        // Trạng thái mở/đóng từng mục bảng trái
        var muc = docLs(KHOA_MUC, {});
        document.querySelectorAll('.tk-muc[data-muc]').forEach(function (d) {
            if (muc[d.dataset.muc] === false) d.open = false;
            d.addEventListener('toggle', function () {
                var m = docLs(KHOA_MUC, {});
                m[d.dataset.muc] = d.open;
                ghiLs(KHOA_MUC, m);
            });
        });
        capNhatHep(true);
        var henCo = 0;
        window.addEventListener('resize', function () {
            clearTimeout(henCo);
            henCo = setTimeout(function () { coDinhChieuCao(); capNhatHep(); }, 200);
        });
        if (window.ResizeObserver) {
            // Vùng vẽ đổi cỡ (ẩn/hiện bảng, kéo cửa sổ) → vẽ lại thước
            new ResizeObserver(function () { veThuoc(); }).observe($('tkVung'));
            // Khung đổi bề rộng cả khi cửa sổ không đổi (thu/mở menu trái của trang) → xét lại cột / ngăn kéo
            new ResizeObserver(function () { capNhatHep(); }).observe($('tkTrang'));
        }
        // Đổi % ở menu zoom của hệ thống (gán body.style.zoom) → tính lại chiều cao ngay
        if (window.MutationObserver) {
            new MutationObserver(function () { coDinhChieuCao(); capNhatHep(); zoomVua(); })
                .observe(document.body, { attributes: true, attributeFilter: ['style'] });
        }
        // Header / fixed-page-layout dựng xong muộn hơn script này → đo lại vài lần
        window.addEventListener('load', coDinhChieuCao);
        [300, 1000, 2500].forEach(function (ms) { setTimeout(coDinhChieuCao, ms); });
    }

    // ===== Zoom =====

    function datZoom(z) {
        zoom = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, z));
        veNhan();
    }

    function zoomVua() {
        var vung = $('tkVung');
        var z = Math.min((vung.clientWidth - 70) / (T.hieuRong(trang) * T.PX_MM), (vung.clientHeight - 110) / (trang.HeightMm * T.PX_MM));
        datZoom(isFinite(z) && z > 0 ? z : 4);
    }

    // ===== Nháp trong trình duyệt: F5 / lỡ đóng tab không mất tem đang thiết kế =====

    var henNhap = 0;
    var nhapLoi = false;          // không ghi được nháp (bộ nhớ đầy/bị chặn) thì mới cảnh báo khi rời trang

    function giaTriNhap() { return { idMau: idMau, tenMau: $('tkTenMau').value, duAn: duAn, iTrang: iTrang, luc: Date.now() }; }

    function ghiNhap() {
        clearTimeout(henNhap);
        henNhap = setTimeout(function () {
            if (daSua) nhapLoi = !ghiLs(KHOA_NHAP, giaTriNhap());
        }, 400);
    }

    function xoaNhap() {
        clearTimeout(henNhap);
        ghiLs(KHOA_NHAP, null);
    }

    function docNhap() {
        var n = docLs(KHOA_NHAP, null);
        if (n && !n.duAn && n.trang) n.duAn = [n.trang]; // nháp bản cũ chỉ có 1 trang
        return n && Array.isArray(n.duAn) && n.duAn.length ? n : null;
    }

    // Ghi mẫu đang mở lên địa chỉ trang (?mau=5) để F5 / gửi link mở lại đúng mẫu
    function ghiDiaChi() {
        var url = URL_GOC + '/ThietKeTem' + (idMau ? '?mau=' + idMau : '');
        if (location.pathname + location.search !== url) history.replaceState(null, '', url);
    }

    function napDuAn(dsTrang, id, ten, iMo) {
        duAn = dsTrang.length ? dsTrang : [T.taoTrangMacDinh()];
        iTrang = Math.min(iMo || 0, duAn.length - 1);
        trang = duAn[iTrang];
        idMau = id || 0;
        $('tkTenMau').value = ten || '';
        chonDs = []; lichSu = []; lamLaiDs = []; khoaCuoi = null; daSua = false;
        xoaNhap();
        ghiDiaChi();
        veTatCa();
        zoomVua();
    }

    // ===== Mẫu trên máy chủ: mở / lưu / xoá / gần đây =====

    function themGanDay(id, ten) {
        if (!id) return;
        var ds = docLs(KHOA_GAN_DAY, []).filter(function (m) { return m.id !== id; });
        ds.unshift({ id: id, ten: ten });
        ghiLs(KHOA_GAN_DAY, ds.slice(0, 8));
        veGanDay();
    }

    function veGanDay() {
        var hop = $('tkGanDay');
        hop.replaceChildren();
        var ds = docLs(KHOA_GAN_DAY, []);
        if (!ds.length) {
            var b0 = document.createElement('button');
            b0.type = 'button'; b0.disabled = true;
            b0.innerHTML = '<span>(chưa có)</span>';
            hop.appendChild(b0);
            return;
        }
        ds.forEach(function (m) {
            var b = document.createElement('button');
            b.type = 'button';
            b.dataset.lenh = 'moGanDay';
            b.dataset.id = m.id;
            var sp = document.createElement('span');
            sp.textContent = m.ten;
            b.appendChild(sp);
            hop.appendChild(b);
        });
    }

    async function moMau(id) {
        try {
            var m = await layJson(URL_GOC + '/MauTem/' + id);
            napDuAn(T.docTnl(m.noiDung), m.idMau, m.tenMau);
            themGanDay(m.idMau, m.tenMau);
            trangThai('Đã mở mẫu "' + m.tenMau + '"' + (duAn.length > 1 ? ' (' + duAn.length + ' trang)' : ''));
        } catch (ex) {
            // Mẫu gần đây đã bị xoá → bỏ khỏi danh sách
            ghiLs(KHOA_GAN_DAY, docLs(KHOA_GAN_DAY, []).filter(function (x) { return x.id !== id; }));
            veGanDay();
            thongBao('error', ex.message);
        }
    }

    // ===== Ô "Chọn mẫu": các mẫu đã lưu trên hệ thống (DB) — máy nào cũng thấy như nhau =====

    var dsMauLuu = [];           // [{idMau, tenMau, ngayCapNhat}] từ DB

    async function napDsTenMau() {
        try {
            dsMauLuu = await layJson(URL_GOC + '/MauTem');
        } catch (ex) {
            dsMauLuu = [];
        }
        veChonMau();
        return dsMauLuu;
    }

    function veChonMau() {
        var s = $('tkChonMau');
        if (!s) return;
        s.replaceChildren();
        var dau = document.createElement('option');
        dau.value = '';
        dau.textContent = idMau ? '— Mẫu mới —' : (dsMauLuu.length ? '— Chưa lưu (chọn mẫu để mở) —' : '— Chưa có mẫu nào trên hệ thống —');
        s.appendChild(dau);
        dsMauLuu.forEach(function (m) {
            var o = document.createElement('option');
            o.value = m.idMau;
            o.textContent = m.tenMau + (m.idMau === idMau && daSua ? ' (đang sửa, chưa lưu)' : '');
            s.appendChild(o);
        });
        s.value = idMau ? String(idMau) : '';
    }

    async function chonMauTuDs(s) {
        var id = parseInt(s.value, 10) || 0;
        if (id === idMau) return;
        if (daSua && !await hoi('Bỏ các thay đổi chưa lưu?', 'Bỏ')) { veChonMau(); return; }
        if (id) await moMau(id);
        else napDuAn([T.taoTrangMacDinh()], 0, '');
        veChonMau();
    }

    // Máy chưa có nháp / chưa chỉ định mẫu → mở mẫu cập nhật gần nhất trên hệ thống thay vì tem trắng
    async function moMauMoiNhat() {
        var ds = await napDsTenMau();
        if (!ds.length) return;
        var moiNhat = ds.slice().sort(function (a, b) { return new Date(b.ngayCapNhat) - new Date(a.ngayCapNhat); })[0];
        await moMau(moiNhat.idMau);
        veChonMau();
    }

    async function moHopMo() {
        var ds = $('tkDsMau');
        ds.replaceChildren();
        var cho = document.createElement('div');
        cho.className = 'text-muted small p-2';
        cho.textContent = 'Đang tải…';
        ds.appendChild(cho);
        $('tkTimMau').value = '';
        bootstrap.Modal.getOrCreateInstance($('tkModalMo')).show();
        try {
            var dsMau = await layJson(URL_GOC + '/MauTem');
            ds.replaceChildren();
            if (!dsMau.length) {
                cho.textContent = 'Chưa có mẫu nào trên hệ thống. Thiết kế rồi bấm Lưu.';
                ds.appendChild(cho);
                return;
            }
            dsMau.forEach(function (m) {
                var b = document.createElement('button');
                b.type = 'button';
                b.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center' + (m.idMau === idMau ? ' active' : '');
                b.dataset.lenh = 'moMauChon';
                b.dataset.id = m.idMau;
                b.dataset.ten = m.tenMau.toLowerCase();
                var ten = document.createElement('span');
                ten.textContent = m.tenMau;
                var ngay = document.createElement('small');
                ngay.className = 'text-muted ms-2';
                ngay.textContent = new Date(m.ngayCapNhat).toLocaleString('vi-VN');
                b.appendChild(ten); b.appendChild(ngay);
                ds.appendChild(b);
            });
        } catch (ex) {
            cho.textContent = ex.message;
            ds.replaceChildren(cho);
        }
    }

    async function luu(thanhMauMoi) {
        var ten = $('tkTenMau').value.trim();
        if (thanhMauMoi || !idMau) {
            ten = await nhapChu('Tên mẫu tem', thanhMauMoi && ten ? ten + ' (bản sao)' : ten);
            if (!ten) return;
        }
        if (!ten) { thongBao('warning', 'Vui lòng nhập tên mẫu tem.'); $('tkTenMau').focus(); return; }
        var nuts = document.querySelectorAll('[data-lenh="luu"], [data-lenh="luuThanh"]');
        nuts.forEach(function (n) { n.disabled = true; });
        try {
            var kq = await goiPost(URL_GOC + '/MauTem/Luu', { idMau: thanhMauMoi ? 0 : idMau, tenMau: ten, noiDung: T.xuatDuAn(duAn) });
            if (!kq.thanhCong) { thongBao('error', kq.thongBao || 'Lưu thất bại.'); return; }
            idMau = kq.duLieu.idMau;
            $('tkTenMau').value = kq.duLieu.tenMau;
            daSua = false;
            napDsTenMau();
            xoaNhap();
            ghiDiaChi();
            themGanDay(idMau, kq.duLieu.tenMau);
            capNhatNut(); capNhatTen();
            trangThai('Đã lưu mẫu "' + kq.duLieu.tenMau + '".');
            thongBao('success', kq.thongBao);
        } catch (ex) {
            thongBao('error', ex.message);
        } finally {
            nuts.forEach(function (n) { n.disabled = false; });
        }
    }

    async function xoaMau() {
        if (!idMau) return;
        if (!await hoi('Xoá mẫu tem "' + $('tkTenMau').value + '"?', 'Xoá')) return;
        try {
            var kq = await goiPost(URL_GOC + '/MauTem/Xoa', { idMau: idMau });
            if (!kq.thanhCong) { thongBao('error', kq.thongBao || 'Xoá thất bại.'); return; }
            ghiLs(KHOA_GAN_DAY, docLs(KHOA_GAN_DAY, []).filter(function (x) { return x.id !== idMau; }));
            veGanDay();
            napDuAn([T.taoTrangMacDinh()], 0, '');
            napDsTenMau();
            thongBao('success', kq.thongBao);
        } catch (ex) {
            thongBao('error', ex.message);
        }
    }

    async function moiMau() {
        if (daSua && !await hoi('Bỏ các thay đổi chưa lưu?', 'Bỏ')) return;
        napDuAn([T.taoTrangMacDinh()], 0, '');
        trangThai('Mẫu mới.');
    }

    // ===== File .tnl / PNG / PDF / ảnh =====

    // Nhập .tnl (app TemNhan) hoặc .lbx (P-touch Editor) — lbx chuyển sang cùng định dạng trang, lưu lại thành mẫu như thường
    async function nhapTnl(file) {
        try {
            var laLbx = /\.lbx$/i.test(file.name);
            var lbx = laLbx ? await T.docLbx(await file.arrayBuffer()) : null;
            var ds = laLbx ? lbx.dsTrang : T.docTnl(await file.text());
            napDuAn(ds, 0, file.name.replace(/\.[^.]+$/, ''), laLbx ? lbx.iMo : 0);
            daSua = true;
            ghiNhap();
            capNhatTen();
            trangThai('Đã nhập "' + file.name + '" (' + ds.length + ' trang)'
                + (laLbx && lbx.boQua ? ', bỏ qua ' + lbx.boQua + ' đối tượng chưa hỗ trợ (ảnh, bảng…)' : '')
                + ' — bấm Lưu để lưu thành mẫu trên hệ thống.');
        } catch (ex) {
            thongBao('error', 'Không đọc được file: ' + ex.message);
        }
    }

    function taiVe(blob, ten) {
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = ten;
        document.body.appendChild(a);
        a.click();
        setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
    }

    function tenFile() { return ($('tkTenMau').value.trim() || 'mau-tem').replace(/[\\/:*?"<>|]/g, '_'); }

    function xuatTnl() {
        taiVe(new Blob([T.xuatTnl(duAn)], { type: 'application/json' }), tenFile() + '.tnl');
    }

    async function xuatPng() {
        try {
            var b = await T.xuatPng(trang, dongDangXem());
            taiVe(b, tenFile() + (duAn.length > 1 ? '-' + trang.Name : '') + '.png');
            trangThai('Đã xuất ảnh PNG 300 dpi của nhãn đang xem.');
        } catch (ex) {
            thongBao('error', ex.message);
        }
    }

    async function xuatPdf() {
        trangThai('Trong hộp thoại in, chọn máy in "Lưu dưới dạng PDF" (Save as PDF).');
        try { await T.inNhan(trang, [dongDangXem()], { soBan: 1 }); } catch (ex) { thongBao('error', ex.message); }
    }

    // Thu nhỏ ảnh trước khi nhúng (tối đa 600 px) để mẫu lưu lên máy chủ không quá nặng
    function docAnh(file) {
        return new Promise(function (resolve, reject) {
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () {
                var r = Math.min(1, 600 / Math.max(img.naturalWidth, img.naturalHeight));
                var c = document.createElement('canvas');
                c.width = Math.max(1, Math.round(img.naturalWidth * r));
                c.height = Math.max(1, Math.round(img.naturalHeight * r));
                c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
                URL.revokeObjectURL(url);
                resolve({ base64: c.toDataURL('image/png').split(',')[1], w: c.width, h: c.height });
            };
            img.onerror = function () { URL.revokeObjectURL(url); reject(new Error('File không phải ảnh.')); };
            img.src = url;
        });
    }

    async function chenAnh(file) {
        try {
            var a = await docAnh(file);
            var thay = $('tkFileAnh').dataset.thay;
            if (thay !== '' && trang.Elements[+thay] && trang.Elements[+thay].type === 'image') {
                ghiTruoc();
                trang.Elements[+thay].ImageData = a.base64;
                veSauSua();
                return;
            }
            var v = vungIn();
            var toiDa = Math.max(3, Math.min(20, v.h - 2));
            var ww = a.w >= a.h ? toiDa : toiDa * a.w / a.h, hh = a.w >= a.h ? toiDa * a.h / a.w : toiDa;
            themDoiTuong({ type: 'image', ImageData: a.base64, X: v.x + 1, Y: v.y + 1, W: T.so(ww), H: T.so(hh) });
        } catch (ex) {
            thongBao('error', ex.message);
        }
    }

    // ===== Dữ liệu in (như Database của P-touch): Tủ điện thoại trên hệ thống hoặc file Excel/CSV (chọn sheet) =====
    // Mọi nguồn quy về: cotDl = [{ ten, lay(dong) }], dongDl = [{ __id, ... }]. File Excel chỉ đọc trong trình duyệt, không tải lên máy chủ.

    var XLSX_CDN = 'https://cdn.jsdelivr.net/npm/xlsx@0.18.5/dist/xlsx.full.min.js';
    var nguonDl = 'tu', cotDl = [], dongDl = [], wbDl = null, tenFileDl = '';
    var daChonDong = new Set();          // __id các bản ghi đã tích (in "Bản ghi đã chọn")
    // Tủ đã tích ở trang quản lý (?tu=1,2,3) → tích sẵn khi tải xong dữ liệu tủ.
    // Đọc ngay lúc nạp script vì ghiDiaChi() sẽ thay địa chỉ trang.
    var tuChonTuQl = (new URLSearchParams(location.search).get('tu') || '').split(',')
        .map(function (v) { return parseInt(v, 10); }).filter(function (v) { return v > 0; });

    function cotTu() {
        return T.TRUONG_TU.map(function (t) { return { ten: t.ten, lay: function (r) { return r[t.khoa]; } }; });
    }

    // Dòng để trộn {{...}}: tủ dùng T.taoDong (có tên gọi khác như "Mã nhân viên"); Excel lấy theo tên cột
    function taoDongTron(r) {
        if (!r) return null;
        if (nguonDl === 'tu') return T.taoDong(r);
        var d = {};
        cotDl.forEach(function (c) {
            var v = c.lay(r) == null ? '' : String(c.lay(r));
            d['=' + c.ten] = v;                                       // đúng tên cột
            if (!(T.boDau(c.ten) in d)) d[T.boDau(c.ten)] = v;          // tên bỏ dấu: cột đứng trước thắng
        });
        return d;
    }

    async function taiDsTu() {
        try {
            dsTu = await layJson(URL_GOC + '/DanhSach');
        } catch (ex) {
            dsTu = [];
            trangThai('Không tải được dữ liệu tủ: ' + ex.message);
        }
        if (nguonDl !== 'tu') return;
        apNguonTu();
    }

    function apNguonTu() {
        nguonDl = 'tu';
        cotDl = cotTu();
        dongDl = dsTu.map(function (t) { return Object.assign({ __id: 't' + t.idTu }, t); });
        daChonDong.clear();
        if (tuChonTuQl.length) {
            var coTu = new Set(dsTu.map(function (t) { return t.idTu; }));
            tuChonTuQl.forEach(function (id) { if (coTu.has(id)) daChonDong.add('t' + id); });
            tuChonTuQl = []; // chỉ áp 1 lần, tải lại dữ liệu sau đó không tích lại
            if (daChonDong.size) {
                $('tkTrang').classList.add('mo-du-lieu');
                $('tkXemDuLieu').checked = true;
                capNhatNut();
            }
        }
        var sel = $('tkLocBoPhan'), cu = sel.value;
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = 'Tất cả bộ phận';
        sel.appendChild(op0);
        dsTu.map(function (t) { return t.tenBoPhan; }).filter(function (v, i, a) { return v && a.indexOf(v) === i; }).sort()
            .forEach(function (bp) {
                var op = document.createElement('option');
                op.value = bp; op.textContent = bp;
                sel.appendChild(op);
            });
        sel.value = cu;
        sel.hidden = false;
        $('tkSheetDl').hidden = true;
        $('tkDlFile').hidden = true;
        $('tkNguonDl').value = 'tu';
        locDuLieu();
    }

    function napThuVienExcel() {
        if (window.XLSX) return Promise.resolve();
        return new Promise(function (ok, loi) {
            var s = document.createElement('script');
            s.src = XLSX_CDN;
            s.onload = function () { ok(); };
            s.onerror = function () { loi(new Error('Không tải được thư viện đọc Excel (cần Internet).')); };
            document.head.appendChild(s);
        });
    }

    async function moFileDl(file) {
        try {
            trangThai('Đang đọc "' + file.name + '"…');
            await napThuVienExcel();
            wbDl = XLSX.read(await file.arrayBuffer(), { type: 'array', cellDates: false });
            tenFileDl = file.name;
            var sel = $('tkSheetDl');
            sel.replaceChildren();
            wbDl.SheetNames.forEach(function (ten) {
                var op = document.createElement('option');
                op.value = ten; op.textContent = ten;
                sel.appendChild(op);
            });
            sel.hidden = wbDl.SheetNames.length < 1;
            var nut = $('tkDlFile');
            nut.replaceChildren();
            var ic = document.createElement('i');
            ic.className = 'fa fa-file-excel-o';
            nut.appendChild(ic);
            nut.appendChild(document.createTextNode(' ' + file.name));
            nut.title = 'Bấm để đổi file';
            nut.hidden = false;
            napSheet(wbDl.SheetNames[0]);
            if (!$('tkTrang').classList.contains('mo-du-lieu')) batTatBan('du-lieu');
        } catch (ex) {
            thongBao('error', 'Không đọc được file: ' + ex.message);
            if (!wbDl) apNguonTu();
        }
    }

    // Dòng đầu có chữ là tiêu đề cột; bỏ dòng trống; tối đa 5000 bản ghi
    function napSheet(ten) {
        var ws = wbDl.Sheets[ten];
        var ds = XLSX.utils.sheet_to_json(ws, { header: 1, raw: false, defval: '', blankrows: false });
        var iDau = ds.findIndex(function (r) { return r.some(function (v) { return String(v).trim() !== ''; }); });
        nguonDl = 'excel';
        $('tkNguonDl').value = 'excel';
        $('tkLocBoPhan').hidden = true;
        $('tkSheetDl').value = ten;
        daChonDong.clear();
        if (iDau < 0) { cotDl = []; dongDl = []; locDuLieu(); trangThai('Sheet "' + ten + '" trống.'); return; }
        var dau = ds[iDau];
        var soCot = ds.reduce(function (m, r) { return Math.max(m, r.length); }, dau.length);
        var daCo = {};
        cotDl = [];
        for (var j = 0; j < soCot; j++) {
            var t = String(dau[j] == null ? '' : dau[j]).trim() || ('Cột ' + (j + 1));
            while (daCo[t]) t += ' (' + (j + 1) + ')';   // chỉ đổi tên khi trùng y hệt; khác dấu thì vẫn phân biệt được
            daCo[t] = true;
            (function (jj, ten) { cotDl.push({ ten: ten, lay: function (r) { return r.o[jj]; } }); })(j, t);
        }
        dongDl = ds.slice(iDau + 1, iDau + 1 + 5000)
            .filter(function (r) { return r.some(function (v) { return String(v).trim() !== ''; }); })
            .map(function (r, i) { return { __id: 'e' + i, o: r }; });
        locDuLieu();
        trangThai('Đã nạp "' + tenFileDl + '" — sheet "' + ten + '": ' + dongDl.length + ' bản ghi, ' + cotDl.length + ' cột. Kéo tiêu đề cột thả vào nhãn.');
    }

    function locDuLieu() {
        var bp = nguonDl === 'tu' ? $('tkLocBoPhan').value : '', tk = $('tkTimTu').value.trim().toLowerCase();
        dsLoc = dongDl.filter(function (r) {
            if (bp && r.tenBoPhan !== bp) return false;
            if (!tk) return true;
            return cotDl.some(function (c) { var v = c.lay(r); return v != null && String(v).toLowerCase().indexOf(tk) >= 0; });
        });
        iDong = Math.min(iDong, Math.max(0, dsLoc.length - 1));
        veDuLieu();
        if ($('tkXemDuLieu').checked) veNhan();
    }

    function veDuLieu() {
        $('tkDongSo').textContent = (dsLoc.length ? iDong + 1 : 0) + ' / ' + dsLoc.length;
        var dau = $('tkDlDau'), than = $('tkDsDong');
        dau.replaceChildren(); than.replaceChildren();
        var trDau = document.createElement('tr');
        var thChon = document.createElement('th');
        thChon.className = 'tk-dl-o-chon';
        var cbHet = document.createElement('input');
        cbHet.type = 'checkbox'; cbHet.className = 'form-check-input'; cbHet.id = 'tkChonHetDong';
        cbHet.title = 'Chọn tất cả bản ghi đang hiện';
        var soChonHien = dsLoc.filter(function (r) { return daChonDong.has(r.__id); }).length;
        cbHet.checked = soChonHien > 0 && soChonHien === dsLoc.length;
        cbHet.indeterminate = soChonHien > 0 && soChonHien < dsLoc.length;
        thChon.appendChild(cbHet);
        trDau.appendChild(thChon);
        var thStt = document.createElement('th');
        thStt.textContent = 'STT';
        thStt.className = 'tk-dl-stt';
        trDau.appendChild(thStt);
        cotDl.forEach(function (c) {
            var th = document.createElement('th');
            th.textContent = c.ten;
            th.draggable = true;
            th.dataset.truong = c.ten;
            th.title = 'Kéo thả vào chữ / mã vạch trên nhãn (hoặc bấm để chèn {{' + c.ten + '}})';
            trDau.appendChild(th);
        });
        dau.appendChild(trDau);
        if (!dsLoc.length) {
            var tr0 = document.createElement('tr');
            var td0 = document.createElement('td');
            td0.colSpan = cotDl.length + 2;
            td0.className = 'tk-dl-trong';
            td0.textContent = nguonDl === 'excel' && !wbDl ? 'Chọn file Excel / CSV để làm dữ liệu in.'
                : dongDl.length ? 'Không có bản ghi khớp bộ lọc.' : (nguonDl === 'tu' ? 'Chưa có tủ nào.' : 'Sheet không có dữ liệu.');
            tr0.appendChild(td0);
            than.appendChild(tr0);
            return;
        }
        dsLoc.forEach(function (r, i) {
            var tr = document.createElement('tr');
            tr.dataset.dong = i;
            if (i === iDong) tr.className = 'dang-xem';
            var tdC = document.createElement('td');
            tdC.className = 'tk-dl-o-chon';
            var cb = document.createElement('input');
            cb.type = 'checkbox'; cb.className = 'form-check-input'; cb.dataset.chonId = r.__id;
            cb.checked = daChonDong.has(r.__id);
            cb.setAttribute('aria-label', 'Chọn bản ghi ' + (i + 1));
            tdC.appendChild(cb);
            tr.appendChild(tdC);
            var tdS = document.createElement('td');
            tdS.className = 'tk-dl-stt';
            tdS.textContent = i + 1;
            tr.appendChild(tdS);
            cotDl.forEach(function (c) {
                var td = document.createElement('td');
                var v = c.lay(r);
                td.textContent = v == null ? '' : v;
                tr.appendChild(td);
            });
            than.appendChild(tr);
        });
    }

    function chonDong(i) {
        if (!dsLoc.length) return;
        iDong = Math.max(0, Math.min(dsLoc.length - 1, i));
        $('tkDongSo').textContent = (iDong + 1) + ' / ' + dsLoc.length;
        $('tkDsDong').querySelectorAll('tr.dang-xem').forEach(function (tr) { tr.classList.remove('dang-xem'); });
        var tr = $('tkDsDong').querySelector('[data-dong="' + iDong + '"]');
        if (tr) { tr.classList.add('dang-xem'); tr.scrollIntoView({ block: 'nearest' }); }
        // Chọn bản ghi là muốn xem nó trên nhãn (như P-touch)
        if (!$('tkXemDuLieu').checked) { $('tkXemDuLieu').checked = true; capNhatNut(); }
        veNhan();
    }

    function chonBanGhi(id, chon) {
        if (chon) daChonDong.add(id); else daChonDong.delete(id);
        var soChonHien = dsLoc.filter(function (r) { return daChonDong.has(r.__id); }).length;
        var cb = $('tkChonHetDong');
        if (cb) { cb.checked = soChonHien > 0 && soChonHien === dsLoc.length; cb.indeterminate = soChonHien > 0 && soChonHien < dsLoc.length; }
        trangThai(daChonDong.size ? 'Đã chọn ' + daChonDong.size + ' bản ghi để in.' : 'Sẵn sàng');
    }

    function chonHetBanGhi(chon) {
        dsLoc.forEach(function (r) { if (chon) daChonDong.add(r.__id); else daChonDong.delete(r.__id); });
        veDuLieu();
        trangThai(daChonDong.size ? 'Đã chọn ' + daChonDong.size + ' bản ghi để in.' : 'Sẵn sàng');
    }

    // ===== Máy in + khổ tem của driver (qua Tem in Agent) =====

    var mayKiem = '';

    async function napMayIn(ping) {
        var sel = $('tkMayIn'), daChon = sel.value || T.docCaiDatIn().mayIn;
        dsMayIn = [];
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = 'Hộp thoại in của trình duyệt';
        sel.appendChild(op0);
        if (!ping) {
            $('tkAgent').textContent = 'Máy này chưa cài hỗ trợ in: in qua hộp thoại trình duyệt, không đọc được khổ tem của driver.';
            napKhoMay();
            return;
        }
        try {
            dsMayIn = await T.agent.mayIn();
            dsMayIn.forEach(function (m) {
                var op = document.createElement('option');
                op.value = m.name;
                op.textContent = m.name.replace(/^\\\\[^\\]+\\/, '') + (m.isTape ? ' (băng)' : '');
                op.title = m.name;
                sel.appendChild(op);
            });
            if (daChon && dsMayIn.some(function (m) { return m.name === daChon; })) sel.value = daChon;
            $('tkAgent').textContent = 'Tem in Agent ' + ping.version + ' (' + ping.machine + ') — ' + dsMayIn.length + ' máy in.';
            napKhoMay();
        } catch (ex) {
            $('tkAgent').textContent = ex.message;
        }
    }

    async function napKhoMay() {
        var sel = $('tkKhoMay'), may = $('tkMayIn').value;
        khoMay = [];
        mayKiem = may;
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = may ? 'Đang đọc khổ tem của driver…' : '-- Khổ tem của máy in --';
        sel.appendChild(op0);
        sel.disabled = true;
        if (!may) return;
        try {
            var ds = await T.agent.khoTem(may);
            if (mayKiem !== may) return; // đã đổi máy khác trong lúc chờ
            khoMay = ds;
            op0.textContent = ds.length ? 'Chọn khổ…' : '(driver không khai báo khổ)';
            ds.forEach(function (m, i) {
                var op = document.createElement('option');
                op.value = i; op.textContent = m.display;
                sel.appendChild(op);
            });
            sel.disabled = !ds.length;
            veKhoNhan();
        } catch (ex) {
            op0.textContent = '(không đọc được: ' + ex.message + ')';
        }
    }

    // Chọn khổ của driver: băng cuộn → chiều cao nhãn = bề ngang băng, giữ chiều dài; tem cắt sẵn → đúng rộng × cao
    function apKhoMay(o) {
        var m = khoMay[parseInt(o.value, 10)];
        if (!m) return;
        ghiTruoc();
        var c = T.docCaiDatIn();
        if (m.heightMm == null) {
            trang.HeightMm = T.so(m.widthMm, trang.HeightMm, 5, 1000);
            if (trang.WidthMm < trang.HeightMm) trang.WidthMm = T.so(trang.HeightMm * 2.5);
            // Máy băng nạp theo chiều dài nhãn → in phải xoay 90° (giống mặc định của app TemNhan)
            c.xoay = true;
            T.luuCaiDatIn(c);
            trangThai('Đã đặt chiều cao nhãn = băng ' + m.widthMm + ' mm; khi in sẽ xoay 90°.');
        } else {
            trang.WidthMm = T.so(m.widthMm, trang.WidthMm, 5, 1000);
            trang.HeightMm = T.so(m.heightMm, trang.HeightMm, 5, 1000);
            trangThai('Đã đặt khổ nhãn theo driver: ' + m.display);
        }
        veKhoNhan();
        veNhan();
        capNhatTen();
        zoomVua();
    }

    async function kiemKhoGiay() {
        var may = $('tkMayIn').value;
        if (!may) {
            thongBao('info', coAgent
                ? 'Chọn máy in ở mục "Máy in" trước khi kiểm tra khổ giấy.'
                : 'Cần cài Tem in Agent trên máy này để hỏi driver máy in (bấm "Tải bộ cài" trên thanh công cụ).');
            return;
        }
        try {
            var loi = await T.agent.tinhTrangMay(may);
            var ds = khoMay.length ? khoMay : await T.agent.khoTem(may);
            var rong = T.hieuRong(trang), cao = trang.HeightMm;
            var khop = ds.filter(function (m) { return T.khopKho(m, rong, cao); });
            var khoMau = so1(rong) + ' × ' + so1(cao) + ' mm';
            var tb = khop.length
                ? 'Driver có khổ "' + khop[0].display + '" khớp nhãn ' + khoMau + '.'
                : 'Driver không có khổ ' + khoMau + '. Tạo khổ đúng trong Printing Preferences của máy in hoặc chọn khổ có sẵn, nếu không tem có thể bị lệch.';
            thongBao(khop.length && !loi ? 'success' : 'warning', tb + (loi ? '\nMáy in: ' + loi + '.' : ''));
        } catch (ex) {
            thongBao('error', ex.message);
        }
    }

    // ===== Tình trạng hỗ trợ in trên máy này =====
    // Hỏi agent định kỳ: chưa có thì 5 s/lần (cài xong là tự nhận, khỏi tải lại trang), đã có thì 30 s/lần (biết khi agent bị tắt).

    function veTinhTrangAgent(ping) {
        var nut = $('tkTinhTrangAgent'), chu = nut.querySelector('span');
        nut.classList.remove('tk-agent-cho', 'tk-agent-co', 'tk-agent-khong');
        if (ping) {
            nut.classList.add('tk-agent-co');
            chu.textContent = 'Máy này đã cài hỗ trợ in';
            nut.title = 'Tem in Agent ' + ping.version + ' đang chạy trên máy ' + ping.machine +
                ': chọn được máy in, khổ tem của driver và in thẳng. Bấm để kiểm tra lại.';
        } else {
            nut.classList.add('tk-agent-khong');
            chu.textContent = 'Máy này chưa cài hỗ trợ in';
            nut.title = 'Chưa thấy Tem in Agent trên máy này (chưa cài hoặc đã tắt) nên đang in qua hộp thoại của trình duyệt. Bấm để tải bộ cài.';
        }
    }

    async function kiemTraAgent() {
        clearTimeout(henAgent);
        if (document.hidden) { henAgent = setTimeout(kiemTraAgent, 30000); return; } // tab ẩn thì khỏi hỏi
        var ping = await T.agent.kiemTra();
        var co = !!ping;
        veTinhTrangAgent(ping);
        if (co !== coAgent) {
            if (coAgent === false && co) trangThai('Đã nhận Tem in Agent trên máy này — chọn máy in ở mục "Máy in".');
            coAgent = co;
            await napMayIn(ping);
        }
        henAgent = setTimeout(kiemTraAgent, co ? 30000 : 5000);
    }

    async function taiAgent(nut) {
        if (nut) nut.disabled = true;
        try {
            var bc = await T.agent.taiBoCai();
            trangThai('Đang tải bộ cài TemNhanAgent_CaiDat.msi (' + bc.kichThuocMb + ' MB) — mở file để cài (không cần quyền admin), cài xong trang tự nhận.');
        } catch (ex) {
            thongBao('error', ex.message);
        } finally {
            if (nut) nut.disabled = false;
        }
    }

    // ===== Hộp thoại In (giống PrintWindow của app) =====

    function phamViIn() { return document.querySelector('input[name="tkInPhamVi"]:checked').value; }

    function dsDongIn() {
        var pv = phamViIn();
        if (pv === 'dangXem' || !dsLoc.length) return [dongDangXem()];
        if (pv === 'tatCa') return dsLoc.map(taoDongTron);
        if (pv === 'daChon') return dsLoc.filter(function (r) { return daChonDong.has(r.__id); }).map(taoDongTron);
        var tu = Math.max(1, parseInt($('tkInTu').value, 10) || 1), den = Math.min(dsLoc.length, parseInt($('tkInDen').value, 10) || dsLoc.length);
        return dsLoc.slice(tu - 1, Math.max(tu, den)).map(taoDongTron);
    }

    function soBanIn() { return Math.max(1, Math.min(50, parseInt($('tkInSoBan').value, 10) || 1)); }

    function capNhatSoNhan() {
        var soDong = dsDongIn().length;
        $('tkInSoNhan').textContent = soDong * soBanIn();
        $('tkInSoTatCa').textContent = dsLoc.length;
        $('tkInSoDaChon').textContent = dsLoc.filter(function (r) { return daChonDong.has(r.__id); }).length;
        $('tkInXemChu').textContent = '· ' + soDong + ' nhãn × ' + soBanIn() + ' bản · ' + trang.Name + ' ' +
            so1(T.hieuRong(trang)) + ' × ' + so1(trang.HeightMm) + ' mm' + ($('tkInXoay').checked ? ' · xoay 90°' : '');
    }

    // Mẫu có dùng trường dữ liệu không (ngoài NGÀY/GIỜ/SỐ) — không có thì mặc định chỉ in nhãn đang xem
    function coTruongDuLieu() {
        return trang.Elements.some(function (e) {
            var s = e.type === 'text' ? e.Text : e.type === 'barcode' ? e.Data : '';
            return /\{\{(.+?)\}\}/.test(s || '') && (s.match(/\{\{(.+?)\}\}/g) || []).some(function (m) {
                var k = T.boDau(m.slice(2, -2).split(':')[0]);
                return k !== 'NGAY' && k !== 'GIO' && k !== 'SO';
            });
        });
    }

    var henXemIn = 0;
    var XEM_TRUOC_TOI_DA = 200;

    // Danh sách xem trước như hộp thoại Print của P-touch: mỗi nhãn 1 ô, kèm "× số bản"
    function veXemIn() {
        clearTimeout(henXemIn);
        henXemIn = setTimeout(function () {
            var hop = $('tkInXem');
            hop.replaceChildren();
            var dsDong = dsDongIn(), soBan = soBanIn(), now = new Date();
            var rong = T.hieuRong(trang) * T.PX_MM, cao = trang.HeightMm * T.PX_MM;
            var z = Math.min(3, 240 / rong, 110 / cao);
            var loi = null;
            dsDong.slice(0, XEM_TRUOC_TOI_DA).forEach(function (dong, i) {
                var muc = document.createElement('div');
                muc.className = 'tk-in-muc';
                var so = document.createElement('span');
                so.className = 'tk-in-so';
                so.textContent = '× ' + soBan;
                muc.appendChild(so);
                var giay = document.createElement('div');
                giay.className = 'tk-in-xem-giay';
                giay.style.width = (rong * z) + 'px';
                giay.style.height = (cao * z) + 'px';
                var kq = T.veNhan(trang, { dong: dong, stt: i * soBan, now: now });
                kq.el.style.transformOrigin = '0 0';
                kq.el.style.transform = 'scale(' + z + ')';
                giay.appendChild(kq.el);
                muc.appendChild(giay);
                var stt = document.createElement('span');
                stt.className = 'tk-in-stt';
                stt.textContent = i + 1;
                muc.appendChild(stt);
                hop.appendChild(muc);
                T.thuNhoChu(kq.el);
                if (kq.loi.length && !loi) { loi = 'Nhãn ' + (i + 1) + ': ' + kq.loi[0].thongBao; muc.classList.add('co-loi'); }
            });
            if (dsDong.length > XEM_TRUOC_TOI_DA) {
                var them = document.createElement('p');
                them.className = 'tk-goi-y text-center';
                them.textContent = '… và ' + (dsDong.length - XEM_TRUOC_TOI_DA) + ' nhãn nữa (không hiện xem trước, vẫn được in).';
                hop.appendChild(them);
            }
            $('tkInLoi').textContent = loi ? 'Mã vạch lỗi — ' + loi + '. Sửa trước khi in.' : '';
            capNhatSoNhan();
        }, 120);
    }

    async function napTuyChonMay() {
        var may = $('tkInMay').value;
        var hop = $('tkInTuyChon');
        $('tkInGoiY').textContent = may
            ? 'In thẳng qua driver bằng Tem in Agent, không hiện hộp thoại.'
            : 'Trong hộp thoại in: chọn máy in, Lề: Không, Tỷ lệ: 100%.';
        hop.hidden = true;
        if (!may) return;
        var f = await T.agent.tinhNang(may);
        if ($('tkInMay').value !== may || !f) return;
        var co = false;
        hop.querySelectorAll('[data-tinh-nang]').forEach(function (l) {
            var ok = !!f[l.dataset.tinhNang];
            l.hidden = !ok;
            co = co || ok;
        });
        hop.hidden = !co;
    }

    // phamVi: ép phạm vi in (vd "dangXem" khi in 1 bản ghi từ menu chuột phải)
    function moIn(phamVi) {
        var c = T.docCaiDatIn();
        var sel = $('tkInMay');
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = 'Hộp thoại in của trình duyệt';
        sel.appendChild(op0);
        dsMayIn.forEach(function (m) {
            var op = document.createElement('option');
            op.value = m.name; op.textContent = m.name.replace(/^\\\\[^\\]+\\/, '') + (m.isTape ? ' (băng)' : '');
            sel.appendChild(op);
        });
        var may = $('tkMayIn').value || c.mayIn;
        sel.value = dsMayIn.some(function (m) { return m.name === may; }) ? may : '';
        $('tkInXoay').checked = c.xoay;
        $('tkInSoBan').value = c.soBan;
        $('tkInLechX').value = c.lechX;
        $('tkInLechY').value = c.lechY;
        $('tkInTuCat').checked = !!c.tuCat; $('tkInCatNua').checked = !!c.catNua; $('tkInNoiTiep').checked = !!c.noiTiep;
        $('tkInGuong').checked = !!c.inGuong; $('tkInChatLuong').checked = !!c.chatLuongCao;
        $('tkInTu').value = 1;
        $('tkInDen').value = Math.max(1, dsLoc.length);
        $('tkInTu').max = $('tkInDen').max = Math.max(1, dsLoc.length);
        var soDaChon = dsLoc.filter(function (r) { return daChonDong.has(r.__id); }).length;
        ['tatCa', 'khoang'].forEach(function (v) { document.querySelector('input[name="tkInPhamVi"][value="' + v + '"]').disabled = !dsLoc.length; });
        document.querySelector('input[name="tkInPhamVi"][value="daChon"]').disabled = !soDaChon;
        // Mặc định như P-touch: có bản ghi đã tích → in các bản ghi đó; mẫu dùng trường dữ liệu → tất cả bản ghi; còn lại → nhãn đang xem
        var pv = phamVi || (soDaChon ? 'daChon' : dsLoc.length && coTruongDuLieu() ? 'tatCa' : 'dangXem');
        document.querySelector('input[name="tkInPhamVi"][value="' + pv + '"]').checked = true;
        $('tkInLoi').textContent = '';
        bootstrap.Modal.getOrCreateInstance($('tkModalIn')).show();
        capNhatSoNhan();
        napTuyChonMay();
        veXemIn();
    }

    function docCaiDatInHop() {
        var c = T.docCaiDatIn();
        var hop = $('tkInTuyChon');
        function chonNeuCo(id, tn) { var l = hop.querySelector('[data-tinh-nang="' + tn + '"]'); return !hop.hidden && l && !l.hidden ? $(id).checked : null; }
        c.mayIn = $('tkInMay').value;
        c.xoay = $('tkInXoay').checked;
        c.soBan = Math.round(T.so($('tkInSoBan').value, 1, 1, 50));
        c.lechX = T.so($('tkInLechX').value, 0, -20, 20);
        c.lechY = T.so($('tkInLechY').value, 0, -20, 20);
        c.tuCat = chonNeuCo('tkInTuCat', 'autoCut');
        c.catNua = chonNeuCo('tkInCatNua', 'halfCut');
        c.noiTiep = chonNeuCo('tkInNoiTiep', 'chainPrint');
        c.inGuong = chonNeuCo('tkInGuong', 'mirror');
        c.chatLuongCao = chonNeuCo('tkInChatLuong', 'quality');
        return c;
    }

    async function inNgay() {
        var nut = $('tkBtnInNgay');
        var c = docCaiDatInHop();
        T.luuCaiDatIn(c);
        if (c.mayIn !== $('tkMayIn').value && dsMayIn.some(function (m) { return m.name === c.mayIn; })) { $('tkMayIn').value = c.mayIn; napKhoMay(); }
        var dsDong = dsDongIn();
        nut.disabled = true;
        $('tkInLoi').textContent = '';
        try {
            if (c.mayIn) {
                var kq = await T.agent.inNhan(c.mayIn, trang, dsDong, c);
                bootstrap.Modal.getOrCreateInstance($('tkModalIn')).hide();
                trangThai('Đã gửi ' + kq.pages + ' tem tới "' + c.mayIn + '".');
                if (kq.warning) thongBao('warning', kq.warning);
            } else {
                await T.inNhan(trang, dsDong, c);
                bootstrap.Modal.getOrCreateInstance($('tkModalIn')).hide();
            }
        } catch (ex) {
            $('tkInLoi').textContent = ex.message;
        } finally {
            nut.disabled = false;
        }
    }

    // ===== Menu chuột phải (như P-touch / app TemNhan) =====
    // Dùng lại kiểu .tk-menu-ds của thanh menu; mỗi mục mang data-lenh nên đi chung chayLenh.

    function mucMenu(chu, lenh, them, phim, tat) {
        var b = document.createElement('button');
        b.type = 'button';
        b.setAttribute('role', 'menuitem');
        b.dataset.lenh = lenh;
        Object.keys(them || {}).forEach(function (k) { b.dataset[k] = them[k]; });
        var sp = document.createElement('span');
        sp.textContent = chu;
        b.appendChild(sp);
        if (phim) { var kb = document.createElement('kbd'); kb.textContent = phim; b.appendChild(kb); }
        b.disabled = !!tat;
        return b;
    }

    function vachMenu() { return document.createElement('hr'); }

    function menuCon(chu, dsMuc) {
        var d = document.createElement('div');
        d.className = 'tk-menu-con';
        var b = document.createElement('button');
        b.type = 'button';
        b.setAttribute('aria-haspopup', 'true');
        var sp = document.createElement('span');
        sp.textContent = chu;
        b.appendChild(sp);
        var mui = document.createElement('i');
        mui.className = 'fa fa-caret-right';
        b.appendChild(mui);
        d.appendChild(b);
        var ds = document.createElement('div');
        ds.className = 'tk-menu-ds';
        ds.setAttribute('role', 'menu');
        dsMuc.filter(Boolean).forEach(function (m) { ds.appendChild(m); });
        d.appendChild(ds);
        return d;
    }

    function hienMenuPhai(ev, dsMuc) {
        var m = $('tkMenuPhai');
        m.replaceChildren();
        var ds = document.createElement('div');
        ds.className = 'tk-menu-ds';
        dsMuc.filter(Boolean).forEach(function (x) { ds.appendChild(x); });
        m.appendChild(ds);
        m.hidden = false;
        // Đặt tại con trỏ, đẩy vào trong màn hình nếu tràn; menu con mở sang trái khi sát mép phải
        // Menu nằm trong body bị zoom: left/top gán vào bị nhân zoom → đổi toạ độ con trỏ và cỡ cửa sổ sang px CSS
        var z = zoomHeThong();
        var x = ev.clientX / z, y = ev.clientY / z, rongMan = window.innerWidth / z, caoMan = window.innerHeight / z;
        var w = ds.offsetWidth, h = ds.offsetHeight;
        m.style.left = Math.max(4, Math.min(x, rongMan - w - 4)) + 'px';
        m.style.top = Math.max(4, Math.min(y, caoMan - h - 4)) + 'px';
        m.classList.toggle('mo-trai', x + w + 240 > rongMan);
    }

    function dongMenuPhai() { $('tkMenuPhai').hidden = true; }

    function menuVung(ev) {
        var p = viTriMm(ev), i = timDoiTuong(p);
        if (i >= 0 && chonDs.indexOf(i) < 0) chonLai([i]);
        else if (i < 0 && chonDs.length) chonLai([]);
        var x = String(T.so(p.x)), y = String(T.so(p.y));
        var ds;
        if (chonDs.length) {
            var dt = doiTuongChon();
            var khoaHet = dt.every(function (e) { return e.Locked; }), inHet = dt.every(function (e) { return e.Printable; });
            ds = [
                mucMenu('Cắt', 'cat', null, 'Ctrl+X'),
                mucMenu('Sao chép', 'chep', null, 'Ctrl+C'),
                mucMenu('Dán', 'dan', null, 'Ctrl+V', !boNho),
                mucMenu('Nhân bản', 'nhanBan', null, 'Ctrl+D'),
                mucMenu('Xoá', 'xoa', null, 'Del'),
                vachMenu(),
                menuCon('Thứ tự', [mucMenu('Lên trên cùng', 'lop', { buoc: 'tren' }), mucMenu('Lên một lớp', 'lop', { buoc: 'len' }),
                    mucMenu('Xuống một lớp', 'lop', { buoc: 'xuong' }), mucMenu('Xuống dưới cùng', 'lop', { buoc: 'duoi' })]),
                menuCon('Căn chỉnh', [mucMenu('Trái', 'can', { huong: 'trai' }), mucMenu('Giữa (ngang)', 'can', { huong: 'giuaNgang' }),
                    mucMenu('Phải', 'can', { huong: 'phai' }), mucMenu('Trên', 'can', { huong: 'tren' }),
                    mucMenu('Giữa (dọc)', 'can', { huong: 'giuaDoc' }), mucMenu('Dưới', 'can', { huong: 'duoi' }), vachMenu(),
                    mucMenu('Phân bố đều ngang', 'phanBo', { truc: 'ngang' }, null, chonDs.length < 3),
                    mucMenu('Phân bố đều dọc', 'phanBo', { truc: 'doc' }, null, chonDs.length < 3)]),
                menuCon('Xoay', [mucMenu('Xoay 90° trái', 'xoay', { goc: '-90' }), mucMenu('Xoay 90° phải', 'xoay', { goc: '90' })]),
                menuCon('Đổi cỡ', [mucMenu('To hơn', 'doiCo', { kieu: 'to' }), mucMenu('Nhỏ hơn', 'doiCo', { kieu: 'nho' }),
                    mucMenu('Tối đa', 'doiCo', { kieu: 'toiDa' }), mucMenu('Tối đa, giữ tỉ lệ', 'doiCo', { kieu: 'toiDaTiLe' }),
                    mucMenu('Vừa vùng in', 'vuaVungIn')]),
                vachMenu(),
                chonDs.length > 1 ? mucMenu('Nhóm', 'nhom', null, 'Ctrl+G') : null,
                dt.some(function (e) { return e.GroupId; }) ? mucMenu('Bỏ nhóm', 'boNhom', null, 'Ctrl+Shift+G') : null,
                mucMenu(khoaHet ? 'Mở khoá vị trí' : 'Khoá vị trí', 'khoaChon'),
                mucMenu(inHet ? 'Không in đối tượng này' : 'In đối tượng này', 'inChon'),
                vachMenu(),
                mucMenu('Thuộc tính…', 'thuocTinh')
            ];
        } else {
            ds = [
                mucMenu('Dán', 'dan', null, 'Ctrl+V', !boNho),
                mucMenu('Chọn tất cả', 'chonHet', null, 'Ctrl+A', !trang.Elements.length),
                vachMenu(),
                menuCon('Chèn tại đây', [['Chữ', 'text'], ['Khung', 'frame'], ['Đường kẻ', 'line'], ['Mã vạch', 'barcode'], ['Mã QR', 'qr'],
                    ['Ảnh / logo…', 'image'], ['Ngày giờ', 'ngay'], ['Số tự tăng', 'so']].map(function (a) { return mucMenu(a[0], 'chen', { loai: a[1], x: x, y: y }); })),
                vachMenu(),
                mucMenu('Trang mới', 'themTrang'),
                mucMenu('In…', 'in', null, 'Ctrl+P')
            ];
        }
        hienMenuPhai(ev, ds);
    }

    function menuTabTrang(ev, i) {
        chuyenTrang(i);
        hienMenuPhai(ev, [
            mucMenu('Đổi tên trang…', 'doiTenTrang'),
            mucMenu('Nhân bản trang', 'nhanBanTrang'),
            mucMenu('Xoá trang', 'xoaTrang', null, null, duAn.length < 2),
            vachMenu(),
            mucMenu('Trang mới', 'themTrang')
        ]);
    }

    function menuDongDuLieu(ev, i) {
        chonDong(i);
        var r = dsLoc[i], daChon = r && daChonDong.has(r.__id);
        var soDaChon = dsLoc.filter(function (x) { return daChonDong.has(x.__id); }).length;
        hienMenuPhai(ev, [
            mucMenu('In bản ghi này…', 'inDong'),
            mucMenu(daChon ? 'Bỏ chọn bản ghi này' : 'Chọn bản ghi này', 'chonDongCt', { id: r ? r.__id : '' }),
            vachMenu(),
            mucMenu('Chọn tất cả bản ghi', 'chonHetDong', null, null, !dsLoc.length),
            mucMenu('Bỏ chọn tất cả', 'boChonHetDong', null, null, !soDaChon),
            mucMenu('In các bản ghi đã chọn (' + soDaChon + ')…', 'inDaChon', null, null, !soDaChon),
            mucMenu('In tất cả bản ghi (' + dsLoc.length + ')…', 'inTatCa', null, null, !dsLoc.length)
        ]);
    }

    function menuCotDuLieu(ev, ten) {
        var e = chonDs.length === 1 ? trang.Elements[chonDs[0]] : null;
        hienMenuPhai(ev, [
            mucMenu(e && (e.type === 'text' || e.type === 'barcode') ? 'Chèn {{' + ten + '}} vào ' + tenLoai(e).toLowerCase() + ' đang chọn' : 'Tạo ô chữ {{' + ten + '}}', 'chenCot', { ten: ten })
        ]);
    }

    function batTatChon(tt) {
        var ds = doiTuongChon();
        if (!ds.length) return;
        ghiTruoc();
        var giaTri = !ds.every(function (e) { return e[tt]; });
        ds.forEach(function (e) { e[tt] = giaTri; });
        veTatCa();
        trangThai(tt === 'Locked' ? (giaTri ? 'Đã khoá vị trí.' : 'Đã mở khoá vị trí.') : (giaTri ? 'Đối tượng sẽ được in.' : 'Đối tượng chỉ hiện khi thiết kế, không in.'));
    }

    function moThuocTinh() {
        var tr = $('tkTrang');
        if (manHep.matches) { if (!tr.classList.contains('mo-phai')) batTatBan('phai'); }
        else if (tr.classList.contains('an-phai')) batTatBan('phai');
        if (chonDs.length === 1) { tabTT = 'loai'; veThuocTinh(); }
    }

    // ===== Menu =====

    function dongMenu() { document.querySelectorAll('.tk-menu-muc.mo').forEach(function (m) { m.classList.remove('mo'); }); }

    function phimTat() {
        var ds = [['Ctrl+O', 'Mở mẫu'], ['Ctrl+S', 'Lưu'], ['Ctrl+P', 'In'], ['Ctrl+Z / Ctrl+Y', 'Hoàn tác / Làm lại'],
            ['Ctrl+X / C / V', 'Cắt / Chép / Dán'], ['Ctrl+D', 'Nhân bản'], ['Ctrl+A', 'Chọn tất cả'], ['Del', 'Xoá'],
            ['Ctrl+G / Ctrl+Shift+G', 'Nhóm / Bỏ nhóm'], ['Mũi tên', 'Dịch 0,5 mm (Shift: 0,1 mm)'], ['Ctrl + lăn chuột', 'Phóng to / thu nhỏ'],
            ['Alt khi kéo', 'Tạm tắt bắt lưới'], ['Ctrl / Shift + bấm', 'Chọn thêm đối tượng'], ['Esc', 'Bỏ chọn']];
        var html = '<table class="table table-sm text-start mb-0">' + ds.map(function (d) { return '<tr><td><kbd>' + d[0] + '</kbd></td><td>' + d[1] + '</td></tr>'; }).join('') + '</table>';
        if (window.Swal) Swal.fire({ title: 'Phím tắt', html: html, confirmButtonText: 'Đóng' });
    }

    function gioiThieu() {
        thongBao('info', 'Thiết kế tem tủ điện thoại — bản web của app Tem in (THIEUXK). Mẫu lưu trên hệ thống, cùng định dạng file .tnl với app desktop. ' +
            'Cài Tem in Agent để chọn máy in, khổ tem của driver và in thẳng.');
    }

    // ===== Khởi tạo & sự kiện =====

    function khoiTao() {
        veDanhSachKho();
        var cot = $('tkCot');
        ['NGÀY', 'GIỜ', 'SỐ:001'].forEach(function (ten) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'tk-the';
            b.draggable = true;
            b.dataset.truong = ten;
            b.textContent = '{{' + ten + '}}';
            cot.appendChild(b);
        });
        khoiTaoBoCuc();
        veGanDay();
        veTatCa();
        zoomVua();
        moLaiKhiTai();
        taiDsTu();
        kiemTraAgent();
        // Quay lại tab (vd vừa cài xong bộ cài) → kiểm tra ngay, khỏi chờ lượt hỏi kế
        document.addEventListener('visibilitychange', function () { if (!document.hidden) kiemTraAgent(); });
    }

    // Mở lại khi vào trang / F5: ưu tiên nháp chưa lưu của đúng mẫu đó, rồi tới mẫu trên địa chỉ (?mau=)
    async function moLaiKhiTai() {
        var idUrl = parseInt(new URLSearchParams(location.search).get('mau'), 10) || 0;
        var nhap = docNhap();
        if (nhap && (!idUrl || nhap.idMau === idUrl)) {
            napDuAn(nhap.duAn.map(T.chuanHoaTrang), nhap.idMau, nhap.tenMau, nhap.iTrang);
            // napDuAn xoá nháp; vẫn coi là chưa lưu và ghi lại nháp
            daSua = true;
            ghiNhap();
            capNhatTen();
            trangThai('Đã khôi phục tem đang thiết kế chưa lưu' + (nhap.tenMau ? ' ("' + nhap.tenMau + '")' : '') + ' — bấm Lưu để lưu lên hệ thống.');
            return;
        }
        if (idUrl) { await moMau(idUrl); veChonMau(); return; }
        await moMauMoiNhat();
    }

    async function chayLenh(b) {
        var l = b.dataset.lenh;
        switch (l) {
            case 'moi': await moiMau(); break;
            case 'mo': await moHopMo(); break;
            case 'moMauChon':
            case 'moGanDay':
                if (daSua && !await hoi('Bỏ các thay đổi chưa lưu để mở mẫu khác?', 'Bỏ')) return;
                bootstrap.Modal.getOrCreateInstance($('tkModalMo')).hide();
                await moMau(parseInt(b.dataset.id, 10));
                break;
            case 'luu': await luu(false); break;
            case 'luuThanh': await luu(true); break;
            case 'xoaMau': await xoaMau(); break;
            case 'nhap':
                bootstrap.Modal.getOrCreateInstance($('tkModalMo')).hide();
                $('tkFileTnl').click();
                break;
            case 'xuat': xuatTnl(); break;
            case 'xuatPng': await xuatPng(); break;
            case 'xuatPdf': await xuatPdf(); break;
            case 'in': moIn(); break;
            case 'hoanTac': hoanTac(); break;
            case 'lamLai': lamLai(); break;
            case 'cat': cat(); break;
            case 'chep': chep(); break;
            case 'dan': dan(); break;
            case 'nhanBan': nhanBan(doiTuongChon(), 1); break;
            case 'xoa': xoaChon(); break;
            case 'chonHet': chonLai(trang.Elements.map(function (e, i) { return i; })); break;
            case 'doiTenTrang': await doiTenTrang(); break;
            case 'nhanBanTrang': nhanBanTrang(); break;
            case 'xoaTrang': await xoaTrang(); break;
            case 'themTrang': themTrang(); break;
            case 'trangTruoc': chuyenTrang(iTrang - 1); break;
            case 'trangSau': chuyenTrang(iTrang + 1); break;
            case 'zoom':
                if (b.dataset.muc === 'vua') zoomVua();
                else if (b.dataset.muc === '100') datZoom(1);
                else datZoom(zoom * (b.dataset.muc === '+' ? 1.25 : 0.8));
                break;
            case 'batLuoi': $('tkBatLuoi').checked = !$('tkBatLuoi').checked; capNhatNut(); break;
            case 'banTrai': batTatBan('trai'); break;
            case 'banPhai': batTatBan('phai'); break;
            case 'banDuLieu': batTatBan('du-lieu'); break;
            case 'dongBan': dongNganKeo(); break;
            case 'xemDuLieu': $('tkXemDuLieu').checked = !$('tkXemDuLieu').checked; veNhan(); capNhatNut(); break;
            case 'taiLaiDuLieu': await taiDsTu(); trangThai('Đã tải lại dữ liệu tủ (' + dsTu.length + ' tủ).'); break;
            case 'chen': chen(b.dataset.loai, b.dataset.x != null ? { x: parseFloat(b.dataset.x), y: parseFloat(b.dataset.y) } : null); break;
            case 'chenCot': chenTruong('{{' + b.dataset.ten + '}}'); break;
            case 'khoaChon': batTatChon('Locked'); break;
            case 'inChon': batTatChon('Printable'); break;
            case 'thuocTinh': moThuocTinh(); break;
            case 'chonFileDl': $('tkFileDl').click(); break;
            case 'inDong': moIn('dangXem'); break;
            case 'inDaChon': moIn('daChon'); break;
            case 'inTatCa': moIn('tatCa'); break;
            case 'chonDongCt': chonBanGhi(b.dataset.id, !daChonDong.has(b.dataset.id)); veDuLieu(); break;
            case 'chonHetDong': chonHetBanGhi(true); break;
            case 'boChonHetDong': chonHetBanGhi(false); break;
            case 'can': canChinh(b.dataset.huong); break;
            case 'phanBo': phanBo(b.dataset.truc); break;
            case 'vuaVungIn': vuaVungIn(); break;
            case 'xoay': xoay(parseInt(b.dataset.goc, 10)); break;
            case 'lop': doiLop(b.dataset.buoc); break;
            case 'doiCo': doiCo(b.dataset.kieu); break;
            case 'nhom': nhom(); break;
            case 'boNhom': boNhom(); break;
            case 'bat': batTatThuocTinh(b.dataset.tt); break;
            case 'thayAnh': $('tkFileAnh').dataset.thay = String(chonDs[0]); $('tkFileAnh').click(); break;
            case 'themKho': await themKho(); break;
            case 'xoaKho': xoaKho(); break;
            case 'khoMacDinh': ghiLs(KHOA_KHO, null); veDanhSachKho(); veKhoNhan(); trangThai('Đã khôi phục danh sách khổ mặc định.'); break;
            case 'kiemKhoGiay': await kiemKhoGiay(); break;
            case 'dongTruoc': chonDong(iDong - 1); break;
            case 'dongSau': chonDong(iDong + 1); break;
            case 'taiAgent': await taiAgent(b); break;
            case 'kiemAgent': if (coAgent === false) await taiAgent(document.querySelector('.tk-nut-tai')); else await kiemTraAgent(); break;
            case 'phimTat': phimTat(); break;
            case 'gioiThieu': gioiThieu(); break;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        khoiTao();
        napDsTenMau(); // moLaiKhiTai có thể nạp lại lần nữa — rẻ, giữ cho ô Chọn mẫu luôn có dữ liệu
        var vung = $('tkVung');
        vung.addEventListener('pointerdown', batDauKeo);
        vung.addEventListener('pointermove', dangKeo);
        vung.addEventListener('pointerup', ketThucKeo);
        vung.addEventListener('pointercancel', ketThucKeo);
        vung.addEventListener('scroll', function () { veThuoc(); dongMenuPhai(); });
        vung.addEventListener('wheel', function (ev) {
            if (!ev.ctrlKey) return;
            ev.preventDefault();
            datZoom(zoom * (ev.deltaY < 0 ? 1.15 : 1 / 1.15));
        }, { passive: false });
        // Kéo tên cột từ bảng Dữ liệu thả vào nhãn
        vung.addEventListener('dragover', function (ev) { if (ev.dataTransfer.types.indexOf('text/plain') >= 0) { ev.preventDefault(); $('tkKhung').classList.add('tk-tha'); } });
        vung.addEventListener('dragleave', function () { $('tkKhung').classList.remove('tk-tha'); });
        vung.addEventListener('drop', function (ev) {
            ev.preventDefault();
            $('tkKhung').classList.remove('tk-tha');
            var the = ev.dataTransfer.getData('text/plain');
            if (!/^\{\{.+\}\}$/.test(the)) return;
            var p = viTriMm(ev), i = timDoiTuong(p);
            chenTruong(the, i >= 0 && (trang.Elements[i].type === 'text' || trang.Elements[i].type === 'barcode') ? i : null, p);
        });
        $('tkModalIn').addEventListener('shown.bs.modal', veXemIn);
    });

    document.addEventListener('dragstart', function (ev) {
        var t = ev.target.closest && ev.target.closest('[data-truong]');
        if (t) { ev.dataTransfer.setData('text/plain', '{{' + t.dataset.truong + '}}'); ev.dataTransfer.effectAllowed = 'copy'; }
    });

    document.addEventListener('click', async function (ev) {
        if (!ev.target.closest('#tkMenuPhai .tk-menu-con > button')) dongMenuPhai();
        // Menu: bấm tiêu đề mở/đóng; bấm ra ngoài đóng
        var tieuDe = ev.target.closest('[data-menu]');
        if (tieuDe) {
            var muc = tieuDe.parentElement, dangMo = muc.classList.contains('mo');
            dongMenu();
            if (!dangMo) muc.classList.add('mo');
            return;
        }
        if (!ev.target.closest('.tk-menu-con > button')) dongMenu();

        var tab = ev.target.closest('[data-tab-tt]');
        if (tab) { tabTT = tab.dataset.tabTt; veThuocTinh(); return; }
        var tabTrang = ev.target.closest('[data-trang]');
        if (tabTrang) { chuyenTrang(parseInt(tabTrang.dataset.trang, 10)); return; }
        var b = ev.target.closest('[data-lenh]');
        if (b && !b.disabled) { await chayLenh(b); return; }
        var the = ev.target.closest('[data-truong]');
        if (the) { chenTruong('{{' + the.dataset.truong + '}}'); return; }
        var dong = ev.target.closest('#tkDsDong tr[data-dong]');
        if (dong && !ev.target.closest('input')) chonDong(parseInt(dong.dataset.dong, 10));
    });

    // Rê chuột sang tiêu đề menu khác khi đang mở một menu → chuyển luôn (như thanh menu Windows)
    document.addEventListener('mouseover', function (ev) {
        var tieuDe = ev.target.closest && ev.target.closest('[data-menu]');
        if (!tieuDe || !document.querySelector('.tk-menu-muc.mo') || tieuDe.parentElement.classList.contains('mo')) return;
        dongMenu();
        tieuDe.parentElement.classList.add('mo');
    });

    document.addEventListener('contextmenu', function (ev) {
        var t = ev.target;
        if (t.closest('input, textarea, select, #tkMenuPhai')) return; // ô nhập giữ menu của trình duyệt (dán, chính tả…)
        var tab = t.closest('[data-trang]'), dong = t.closest('#tkDsDong tr[data-dong]'), cot = t.closest('#tkDlDau th[data-truong]');
        if (t.closest('#tkVung')) { ev.preventDefault(); dongMenu(); menuVung(ev); }
        else if (tab) { ev.preventDefault(); menuTabTrang(ev, parseInt(tab.dataset.trang, 10)); }
        else if (dong) { ev.preventDefault(); menuDongDuLieu(ev, parseInt(dong.dataset.dong, 10)); }
        else if (cot) { ev.preventDefault(); menuCotDuLieu(ev, cot.dataset.truong); }
        else dongMenuPhai();
    });

    document.addEventListener('dblclick', function (ev) {
        var tabTrang = ev.target.closest('[data-trang]');
        if (tabTrang) doiTenTrang(parseInt(tabTrang.dataset.trang, 10));
    });

    document.addEventListener('input', function (ev) {
        var o = ev.target;
        if (o.closest('#tkThuocTinh') && o.dataset.tt && o.dataset.kieu !== 'bool' && o.tagName !== 'SELECT') { suaThuocTinh(o); return; }
        if (['tkRong', 'tkCao', 'tkLe', 'tkMauGiay'].indexOf(o.id) >= 0) { suaKhoNhan(o); return; }
        if (o.id === 'tkTenMau') { daSua = true; capNhatTen(); ghiNhap(); return; }
        if (o.id === 'tkTimTu') { locDuLieu(); return; }
        if (o.id === 'tkTimMau') {
            var tk = o.value.trim().toLowerCase();
            $('tkDsMau').querySelectorAll('[data-ten]').forEach(function (x) { x.hidden = tk && x.dataset.ten.indexOf(tk) < 0; });
            return;
        }
        if (['tkInSoBan', 'tkInTu', 'tkInDen'].indexOf(o.id) >= 0) veXemIn();
    });

    document.addEventListener('change', async function (ev) {
        var o = ev.target;
        if (o.dataset.chenVao) {
            // "+ Ngày in / giờ in…": chèn tại con trỏ của ô nội dung
            var v = o.value;
            o.value = '';
            var dich = $('tkThuocTinh').querySelector('[data-tt="' + o.dataset.chenVao + '"]');
            if (!v || !dich) return;
            var dau = dich.selectionStart != null ? dich.selectionStart : dich.value.length;
            var cuoi = dich.selectionEnd != null ? dich.selectionEnd : dau;
            dich.value = o.dataset.chenVao === 'Data' ? v : dich.value.slice(0, dau) + v + dich.value.slice(cuoi);
            suaThuocTinh(dich);
            dich.focus();
            return;
        }
        if (o.closest('#tkThuocTinh') && o.dataset.tt) {
            if (o.dataset.kieu === 'bool' || o.tagName === 'SELECT') suaThuocTinh(o);
            else if (o.dataset.kieu === 'so' && trang.Elements[chonDs[0]]) {
                // Rời ô số: hiện lại giá trị đã chuẩn hoá (vd nhập Cao 0 → 0.5); không dựng lại bảng để Tab vẫn sang ô kế
                o.value = trang.Elements[chonDs[0]][o.dataset.tt];
            }
            return;
        }
        if (['tkKho', 'tkTuDai', 'tkMauGiayChon'].indexOf(o.id) >= 0 || o.name === 'tkHuong') { suaKhoNhan(o); return; }
        if (['tkRong', 'tkCao', 'tkLe'].indexOf(o.id) >= 0) { veKhoNhan(); zoomVua(); return; }
        if (o.id === 'tkKhoMay') { apKhoMay(o); return; }
        if (o.id === 'tkMayIn') { var c = T.docCaiDatIn(); c.mayIn = o.value; T.luuCaiDatIn(c); napKhoMay(); return; }
        if (o.id === 'tkBatLuoi') { capNhatNut(); return; }
        if (o.id === 'tkXemDuLieu') { veNhan(); capNhatNut(); return; }
        if (o.id === 'tkLocBoPhan') { iDong = 0; locDuLieu(); return; }
        if (o.dataset.chonId) { chonBanGhi(o.dataset.chonId, o.checked); return; }
        if (o.id === 'tkChonHetDong') { chonHetBanGhi(o.checked); return; }
        if (o.id === 'tkNguonDl') {
            // Excel: chọn file (đã nạp rồi thì dùng lại); Tủ: quay về dữ liệu hệ thống
            if (o.value === 'excel') { if (wbDl) napSheet($('tkSheetDl').value || wbDl.SheetNames[0]); else { o.value = nguonDl; $('tkFileDl').click(); } }
            else { iDong = 0; if (dsTu.length) apNguonTu(); else taiDsTu().then(apNguonTu); }
            return;
        }
        if (o.id === 'tkSheetDl') { iDong = 0; napSheet(o.value); return; }
        if (o.id === 'tkFileDl' && o.files[0]) { var fDl = o.files[0]; o.value = ''; iDong = 0; moFileDl(fDl); return; }
        if (o.id === 'tkChonMau') { chonMauTuDs(o); return; }
        if (o.id === 'tkInMay') { napTuyChonMay(); return; }
        if (o.id === 'tkInXoay' || o.name === 'tkInPhamVi' || o.id === 'tkInTu' || o.id === 'tkInDen' || o.id === 'tkInSoBan') { veXemIn(); return; }
        if (o.id === 'tkFileTnl' && o.files[0]) {
            var f = o.files[0];
            o.value = '';
            if (daSua && !await hoi('Bỏ các thay đổi chưa lưu để nhập file?', 'Bỏ')) return;
            nhapTnl(f);
            return;
        }
        if (o.id === 'tkFileAnh' && o.files[0]) { var a = o.files[0]; o.value = ''; chenAnh(a); }
    });

    document.addEventListener('click', function (ev) {
        if (ev.target.id === 'tkBtnInNgay' || ev.target.closest('#tkBtnInNgay')) inNgay();
    });

    document.addEventListener('keydown', function (ev) {
        var ctrl = ev.ctrlKey || ev.metaKey;
        var k = ev.key.toLowerCase();
        var trongHop = document.querySelector('.modal.show, .swal2-container');
        if (ctrl && k === 's' && !trongHop) { ev.preventDefault(); luu(false); return; }
        if (ctrl && k === 'o' && !trongHop) { ev.preventDefault(); moHopMo(); return; }
        if (ctrl && k === 'p' && !trongHop) { ev.preventDefault(); moIn(); return; }
        if (k === 'escape') { dongMenu(); dongMenuPhai(); }
        var t = ev.target;
        if (t.closest('input, textarea, select, [contenteditable="true"]') || trongHop) return;
        if (ctrl && k === 'z' && !ev.shiftKey) { ev.preventDefault(); hoanTac(); }
        else if (ctrl && (k === 'y' || (k === 'z' && ev.shiftKey))) { ev.preventDefault(); lamLai(); }
        else if (ctrl && k === 'd') { ev.preventDefault(); nhanBan(doiTuongChon(), 1); }
        else if (ctrl && k === 'x') { ev.preventDefault(); cat(); }
        else if (ctrl && k === 'c') { chep(); }
        else if (ctrl && k === 'v') { if (boNho) { ev.preventDefault(); dan(); } }
        else if (ctrl && k === 'a') { ev.preventDefault(); chonLai(trang.Elements.map(function (e, i) { return i; })); }
        else if (ctrl && k === 'g') { ev.preventDefault(); if (ev.shiftKey) boNhom(); else nhom(); }
        else if (k === 'delete' || k === 'backspace') { if (chonDs.length) { ev.preventDefault(); xoaChon(); } }
        else if (k === 'escape') { dongNganKeo(); chonLai([]); }
        else if (k.indexOf('arrow') === 0 && chonDs.length) {
            ev.preventDefault();
            var b = ev.shiftKey ? 0.1 : BUOC_LUOI;
            dich(k === 'arrowleft' ? -b : k === 'arrowright' ? b : 0, k === 'arrowup' ? -b : k === 'arrowdown' ? b : 0);
        }
    });

    window.addEventListener('beforeunload', function (ev) {
        // Đã có nháp trong trình duyệt thì F5 không mất gì, khỏi hỏi; chỉ cảnh báo khi ghi nháp lỗi
        if (!daSua) return;
        clearTimeout(henNhap);
        nhapLoi = !ghiLs(KHOA_NHAP, giaTriNhap());
        if (nhapLoi) { ev.preventDefault(); ev.returnValue = ''; }
    });
})();
