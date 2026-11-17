// Tem dán thùng QA tự chọn: nhiều mẫu (khổ, phần nội dung, cỡ chữ/mã vạch/QR, cột bảng), xem trước trực tiếp.
// Mẫu lưu localStorage của máy in tem — không cần bảng DB, mỗi máy/máy in giữ khổ riêng của nó.
// In qua iframe ẩn với @page đúng khổ; sau khi dựng sẽ tự bớt dòng bảng / thu chữ mã thùng cho vừa tem.
(function (w) {
    'use strict';

    var KHOA = 'qa-thung-tem-mau-v2';
    var $ = function (id) { return document.getElementById(id); };

    var COT = {
        maPhoiMau: { ten: 'Mã phối màu', so: false, rong: 26 },
        maHang: { ten: 'Mã hàng', so: false, rong: 18 },
        maMau: { ten: 'Mã màu', so: false, rong: 15 },
        maKhachHang: { ten: 'Mã KH', so: false, rong: 14 },
        maKhachCuoi: { ten: 'KH cuối', so: false, rong: 14 },
        soPhieuNhuom: { ten: 'Phiếu nhuộm', so: false, rong: 18 },
        soPhieuDongGoi: { ten: 'Phiếu ĐG', so: false, rong: 18 },
        loai: { ten: 'Loại', so: false, rong: 8 },
        m: { ten: 'm', so: true, rong: 12, nguon: 'slDongGoi', le: 2 },
        kg: { ten: 'kg', so: true, rong: 12, nguon: 'klDongGoi', le: 2 },
        hop: { ten: 'Hộp', so: true, rong: 9, nguon: 'soHop', le: 0 }
    };

    var HIEN_DAY_DU = { tieuDe: true, ngayTao: true, maThung: true, maVach: true, qr: false, tong: true, khachHang: true, ghiChu: true, bang: true, nguoiTao: true, ngayIn: true };

    function mauMacDinh() {
        return [
            // Khổ tem đang dùng thực tế ở QA -> đứng đầu, là mẫu mặc định
            { id: 'm5', ten: 'Đầy đủ 65×95', rong: 65, cao: 95, le: 2.5, tieuDe: 'THÙNG QA', chuThem: '', hien: Object.assign({}, HIEN_DAY_DU, { nguoiTao: false }),
              coMa: 8, caoVach: 12, coQr: 18, coChu: 2.4, gop: true, cot: ['maPhoiMau', 'maMau', 'm', 'kg', 'hop'] },
            { id: 'm1', ten: 'Đầy đủ 100×100', rong: 100, cao: 100, le: 3, tieuDe: 'THÙNG QA', chuThem: '', hien: Object.assign({}, HIEN_DAY_DU),
              coMa: 10, caoVach: 14, coQr: 20, coChu: 2.7, gop: true, cot: ['maPhoiMau', 'maHang', 'maMau', 'm', 'kg', 'hop'] },
            { id: 'm2', ten: 'Đầy đủ 100×150', rong: 100, cao: 150, le: 3, tieuDe: 'THÙNG QA', chuThem: '', hien: Object.assign({}, HIEN_DAY_DU, { qr: true }),
              coMa: 11, caoVach: 16, coQr: 22, coChu: 2.8, gop: true, cot: ['maPhoiMau', 'maHang', 'maMau', 'm', 'kg', 'hop'] },
            { id: 'm3', ten: 'Gọn 100×50 (mã + mã vạch)', rong: 100, cao: 50, le: 3, tieuDe: 'THÙNG QA', chuThem: '',
              hien: { tieuDe: true, ngayTao: true, maThung: true, maVach: true, qr: false, tong: true, khachHang: false, ghiChu: false, bang: false, nguoiTao: false, ngayIn: false },
              coMa: 11, caoVach: 14, coQr: 20, coChu: 2.8, gop: true, cot: ['maPhoiMau', 'm', 'kg', 'hop'] },
            { id: 'm4', ten: 'Nhỏ 60×40 (mã + QR)', rong: 60, cao: 40, le: 2, tieuDe: 'THÙNG QA', chuThem: '',
              hien: { tieuDe: false, ngayTao: false, maThung: true, maVach: false, qr: true, tong: true, khachHang: false, ghiChu: false, bang: false, nguoiTao: false, ngayIn: false },
              coMa: 6, caoVach: 10, coQr: 22, coChu: 2.4, gop: true, cot: ['maPhoiMau', 'm', 'kg', 'hop'] }
        ];
    }

    // ===== Lưu trữ mẫu =====
    var kho = null;
    function napKho() {
        try { kho = JSON.parse(localStorage.getItem(KHOA) || 'null'); } catch (e) { kho = null; }
        if (!kho || !Array.isArray(kho.ds) || kho.ds.length === 0) kho = { chon: 'm5', ds: mauMacDinh(), macDinhDaCo: ['m1', 'm2', 'm3', 'm4', 'm5'] };
        // Máy đã lưu mẫu từ trước: bổ sung mẫu mặc định mới ra đời sau (chỉ 1 lần, xoá đi thì không tự thêm lại)
        var daCo = kho.macDinhDaCo || ['m1', 'm2', 'm3', 'm4'];
        var them = mauMacDinh().filter(function (m) { return daCo.indexOf(m.id) < 0 && !kho.ds.some(function (x) { return x.id === m.id; }); });
        if (them.length > 0 || !kho.macDinhDaCo) {
            them.forEach(function (m) { kho.ds.unshift(m); daCo.push(m.id); });
            if (them.some(function (m) { return m.id === 'm5'; })) kho.chon = 'm5';
            kho.macDinhDaCo = daCo;
            luuKho();
        }
        if (!kho.ds.some(function (m) { return m.id === kho.chon; })) kho.chon = kho.ds[0].id;
    }
    function luuKho() { try { localStorage.setItem(KHOA, JSON.stringify(kho)); } catch (e) { /* storage bị chặn: chỉ dùng trong phiên */ } }
    function mauDangChon() { return kho.ds.find(function (m) { return m.id === kho.chon; }) || kho.ds[0]; }

    // ===== Dựng HTML tem =====
    function esc(s) {
        return String(s === null || s === undefined ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    function so(n, le) { return (Number(n) || 0).toLocaleString('vi-VN', { maximumFractionDigits: le === undefined ? 2 : le }); }
    function ngay(s) {
        var d = s ? new Date(s) : new Date();
        return isNaN(d) ? '' : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }
    function duyNhat(dong, khoa) {
        var s = [];
        dong.forEach(function (d) { var v = String(d[khoa] || '').trim(); if (v && s.indexOf(v) < 0) s.push(v); });
        return s;
    }

    // gop=true: gộp các dòng trùng mọi cột chữ đã chọn, cộng dồn cột số
    function hangBang(dong, mau) {
        var cotChu = mau.cot.filter(function (c) { return COT[c] && !COT[c].so; });
        var cotSo = mau.cot.filter(function (c) { return COT[c] && COT[c].so; });
        var map = {}, ds = [];
        dong.forEach(function (d, i) {
            var k = mau.gop ? cotChu.map(function (c) { return d[c] || ''; }).join('|') : 'd' + i;
            if (!map[k]) {
                map[k] = {};
                cotChu.forEach(function (c) { map[k][c] = d[c]; });
                cotSo.forEach(function (c) { map[k][c] = 0; });
                ds.push(map[k]);
            }
            cotSo.forEach(function (c) { map[k][c] += Number(d[COT[c].nguon]) || 0; });
        });
        return ds;
    }

    function maVachSvg(ma) {
        if (typeof w.JsBarcode !== 'function' || !ma) return '';
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        try { w.JsBarcode(svg, ma, { format: 'CODE128', displayValue: false, margin: 0, height: 60, width: 2 }); } catch (e) { return ''; }
        // Cho SVG co theo khung tem thay vì giữ kích thước px
        svg.setAttribute('viewBox', '0 0 ' + parseFloat(svg.getAttribute('width')) + ' ' + parseFloat(svg.getAttribute('height')));
        svg.setAttribute('preserveAspectRatio', 'none');
        svg.removeAttribute('width'); svg.removeAttribute('height');
        svg.setAttribute('style', 'width:100%;height:100%;display:block');
        return svg.outerHTML;
    }

    function qrSvg(ma) {
        if (typeof w.qrcode !== 'function' || !ma) return '';
        try {
            var q = w.qrcode(0, 'M');
            q.addData(ma); q.make();
            var n = q.getModuleCount(), p = [];
            for (var r = 0; r < n; r++) for (var c = 0; c < n; c++) if (q.isDark(r, c)) p.push('M' + c + ' ' + r + 'h1v1h-1z');
            return '<svg viewBox="0 0 ' + n + ' ' + n + '" shape-rendering="crispEdges" style="width:100%;height:100%;display:block"><path d="' + p.join('') + '"/></svg>';
        } catch (e) { return ''; }
    }

    function htmlTem(thung, dong, mau) {
        var h = mau.hien, cc = mau.coChu;
        var tongM = 0, tongKg = 0, tongHop = 0;
        dong.forEach(function (d) { tongM += Number(d.slDongGoi) || 0; tongKg += Number(d.klDongGoi) || 0; tongHop += Number(d.soHop) || 0; });
        var p = [];

        if (h.tieuDe || h.ngayTao) {
            p.push('<div class="dau"><b>' + (h.tieuDe ? esc(mau.tieuDe) : '') + '</b><span>' + (h.ngayTao ? 'Ngày tạo: ' + esc(ngay(thung.ngayTao)) : '') + '</span></div>');
        }
        var trai = '';
        // Có tên thùng: 2 mã vạch — tên (chữ to) + mã vạch tên, rồi mã thùng (chữ nhỏ) + mã vạch mã. QR vẫn mã hoá mã thùng.
        if (thung.tenThung) {
            if (h.maThung) trai += '<div class="ma">' + esc(thung.tenThung) + '</div>';
            if (h.maVach) {
                var vachTen = maVachSvg(thung.tenThung);
                // CODE128 chỉ nhận ký tự ASCII: tên có dấu tiếng Việt thì không tạo được mã vạch tên
                trai += vachTen ? '<div class="vach">' + vachTen + '</div>'
                    : '<div class="dong1" style="text-align:center;font-style:italic">(tên có dấu — không tạo được mã vạch tên)</div>';
            }
            if (h.maThung) trai += '<div class="ma2">' + esc(thung.maThung) + '</div>';
            if (h.maVach) trai += '<div class="vach">' + maVachSvg(thung.maThung) + '</div>';
        } else {
            if (h.maThung) trai += '<div class="ma">' + esc(thung.maThung) + '</div>';
            if (h.maVach) trai += '<div class="vach">' + maVachSvg(thung.maThung) + '</div>';
        }
        if (trai || h.qr) {
            p.push('<div class="khoima">' + (trai ? '<div class="trai">' + trai + '</div>' : '')
                + (h.qr ? '<div class="qr">' + qrSvg(thung.maThung) + '</div>' : '') + '</div>');
        }
        if (h.tong) {
            p.push('<div class="tong"><span>' + dong.length + ' dòng</span><span>' + so(tongM) + ' m</span><span>' + so(tongKg) + ' kg</span><span>' + so(tongHop, 0) + ' hộp</span></div>');
        }
        if (h.khachHang) {
            var kh = duyNhat(dong, 'maKhachHang'), khc = duyNhat(dong, 'maKhachCuoi');
            if (kh.length || khc.length) p.push('<div class="dong1">KH: <b>' + esc(kh.join(', ')) + '</b>' + (khc.length ? ' · KH cuối: ' + esc(khc.join(', ')) : '') + '</div>');
        }
        if (h.ghiChu && thung.ghiChu) p.push('<div class="dong1">Ghi chú: ' + esc(thung.ghiChu) + '</div>');
        if (mau.chuThem) p.push('<div class="dong1 them">' + esc(mau.chuThem) + '</div>');

        var cot = mau.cot.filter(function (c) { return COT[c]; });
        if (h.bang && cot.length) {
            var tongRong = cot.reduce(function (s, c) { return s + COT[c].rong; }, 0);
            var hang = hangBang(dong, mau).map(function (r) {
                return '<tr>' + cot.map(function (c) {
                    return COT[c].so ? '<td class="s">' + so(r[c], COT[c].le) + '</td>' : '<td>' + esc(r[c]) + '</td>';
                }).join('') + '</tr>';
            }).join('');
            if (!hang) hang = '<tr><td colspan="' + cot.length + '" class="con">Thùng chưa có dòng nào</td></tr>';
            p.push('<table><colgroup>' + cot.map(function (c) { return '<col style="width:' + (COT[c].rong * 100 / tongRong).toFixed(1) + '%">'; }).join('') + '</colgroup>'
                + '<thead><tr>' + cot.map(function (c) { return '<th' + (COT[c].so ? ' class="s"' : '') + '>' + esc(COT[c].ten) + '</th>'; }).join('') + '</tr></thead>'
                + '<tbody data-cot="' + cot.length + '">' + hang + '</tbody></table>');
        }
        if (h.nguoiTao || h.ngayIn) {
            p.push('<div class="chan"><span>' + (h.nguoiTao ? 'Người tạo: ' + esc(thung.nguoiTao || '') : '') + '</span><span>' + (h.ngayIn ? 'In: ' + esc(ngay()) : '') + '</span></div>');
        }

        return '<!doctype html><html><head><meta charset="utf-8"><title>' + esc(thung.maThung) + '</title><style>'
            + '@page{size:' + mau.rong + 'mm ' + mau.cao + 'mm;margin:0}'
            + '*{box-sizing:border-box}html,body{margin:0;padding:0;background:#fff}'
            + 'body{font-family:Arial,Helvetica,sans-serif;color:#000;-webkit-print-color-adjust:exact;print-color-adjust:exact}'
            + '.tem{width:' + mau.rong + 'mm;height:' + mau.cao + 'mm;padding:' + mau.le + 'mm;overflow:hidden;display:flex;flex-direction:column;gap:1.2mm}'
            + '.dau{display:flex;justify-content:space-between;align-items:baseline;font-size:' + (cc * 1.1) + 'mm;border-bottom:.4mm solid #000;padding-bottom:.8mm}'
            + '.dau b{font-size:' + (cc * 1.35) + 'mm;letter-spacing:.3mm}'
            + '.khoima{display:flex;align-items:center;gap:2mm}.trai{flex:1;min-width:0}'
            + '.ma{font-size:' + mau.coMa + 'mm;font-weight:800;text-align:center;line-height:1.05;white-space:nowrap;overflow:hidden}'
            // Mã thùng khi tem có tên: nhỏ hơn tên nhưng vẫn đọc được từ xa
            + '.ma2{font-size:' + (mau.coMa * 0.65).toFixed(2) + 'mm;font-weight:700;text-align:center;line-height:1.1;white-space:nowrap;overflow:hidden;margin-top:1.2mm}'
            + '.vach{height:' + mau.caoVach + 'mm;margin:1mm 2mm 0}'
            + '.qr{width:' + mau.coQr + 'mm;height:' + mau.coQr + 'mm;flex:none' + (trai ? '' : ';margin:0 auto') + '}'
            + '.tong{display:flex;justify-content:space-between;gap:1mm;font-size:' + (cc * 1.25) + 'mm;font-weight:700;border:.4mm solid #000;padding:.8mm 1.5mm}'
            + '.dong1{font-size:' + (cc * 1.05) + 'mm;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.them{font-weight:700}'
            + 'table{width:100%;border-collapse:collapse;font-size:' + cc + 'mm;table-layout:fixed}'
            + 'th,td{border:.2mm solid #000;padding:.2mm .7mm;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;line-height:1.3}'
            + 'th{background:#e5e5e5;text-align:left}td.s,th.s{text-align:right}td.con{text-align:center;font-style:italic}'
            + '.chan{margin-top:auto;display:flex;justify-content:space-between;font-size:' + (cc * 0.95) + 'mm;padding-top:.8mm;border-top:.2mm solid #000}'
            + '</style></head><body><div class="tem">' + p.join('') + '</div></body></html>';
    }

    // Sau khi dựng: thu chữ mã thùng nếu tràn ngang, bớt dòng bảng nếu tràn dọc (ghi "… và N dòng khác")
    function canChinh(doc) {
        var ma = doc.querySelector('.ma');
        if (ma) {
            var co = parseFloat(doc.defaultView.getComputedStyle(ma).fontSize);
            while (ma.scrollWidth > ma.clientWidth + 1 && co > 6) { co -= 1; ma.style.fontSize = co + 'px'; }
        }
        var tem = doc.querySelector('.tem'), tbody = doc.querySelector('tbody');
        if (!tem || !tbody) return;
        var soCot = parseInt(tbody.dataset.cot, 10) || 1, bo = 0, conRow = null;
        while (tem.scrollHeight > tem.clientHeight + 1 && tbody.rows.length > (conRow ? 1 : 0)) {
            var cuoi = conRow ? tbody.rows[tbody.rows.length - 2] : tbody.rows[tbody.rows.length - 1];
            if (!cuoi) break;
            cuoi.remove(); bo++;
            if (!conRow) {
                conRow = doc.createElement('tr');
                var td = doc.createElement('td'); td.colSpan = soCot; td.className = 'con';
                conRow.appendChild(td); tbody.appendChild(conRow);
            }
            conRow.firstChild.textContent = '… và ' + bo + ' dòng khác (xem trên E-Form)';
        }
    }

    function ghiVaoIframe(f, html) {
        var doc = f.contentWindow.document;
        doc.open(); doc.write(html); doc.close();
        canChinh(doc);
        return doc;
    }

    function inTem(thung, dong, mau) {
        mau = mau || mauDangChon();
        var cu = $('qa-thung-tem-iframe');
        if (cu) cu.remove();
        var f = document.createElement('iframe');
        f.id = 'qa-thung-tem-iframe';
        f.setAttribute('aria-hidden', 'true');
        // Iframe phải có kích thước thật = khổ tem thì đo tràn mới đúng; đẩy ra ngoài màn hình
        f.style.cssText = 'position:fixed;left:-10000px;top:0;border:0;width:' + mau.rong + 'mm;height:' + mau.cao + 'mm';
        document.body.appendChild(f);
        ghiVaoIframe(f, htmlTem(thung, dong, mau));
        setTimeout(function () { f.contentWindow.focus(); f.contentWindow.print(); }, 250);
    }

    // ===== Hộp tuỳ chỉnh =====
    var nguonDuLieu = null; // hàm async trả {thung, dong} để xem trước
    var duLieuXem = null;
    var nhap = null; // bản nháp mẫu đang sửa trong hộp

    var DU_LIEU_MAU = {
        thung: { maThung: 'QA-261112-001', tenThung: 'A1', ngayTao: new Date().toISOString(), nguoiTao: 'V000000', ghiChu: 'Ví dụ ghi chú' },
        dong: [
            { maPhoiMau: 'PM-0001', maHang: 'H100', maMau: 'M01', maKhachHang: 'KH01', maKhachCuoi: 'NIKE', slDongGoi: 120.5, klDongGoi: 18.2, soHop: 2, loai: 'A', soPhieuNhuom: 'N-001', soPhieuDongGoi: 'DG-01' },
            { maPhoiMau: 'PM-0002', maHang: 'H101', maMau: 'M02', maKhachHang: 'KH01', maKhachCuoi: 'NIKE', slDongGoi: 80, klDongGoi: 11.4, soHop: 1, loai: 'A', soPhieuNhuom: 'N-002', soPhieuDongGoi: 'DG-02' }
        ]
    };

    function veDsMau() {
        [$('qa-thung-tem-mau'), $('qa-tem-mau')].forEach(function (sel) {
            if (!sel) return;
            var frag = document.createDocumentFragment();
            kho.ds.forEach(function (m) {
                var o = document.createElement('option');
                o.value = m.id;
                o.textContent = m.ten + ' (' + m.rong + '×' + m.cao + ')';
                frag.appendChild(o);
            });
            sel.replaceChildren(frag);
            sel.value = kho.chon;
        });
    }

    function doNhapLenForm() {
        var f = $('qa-tem-form');
        f.querySelectorAll('[data-k]').forEach(function (el) {
            var v = nhap[el.dataset.k];
            if (el.type === 'checkbox') el.checked = !!v; else el.value = v === undefined ? '' : v;
        });
        f.querySelectorAll('[data-hien]').forEach(function (el) { el.checked = !!nhap.hien[el.dataset.hien]; });
        f.querySelectorAll('[data-cot]').forEach(function (el) { el.checked = nhap.cot.indexOf(el.dataset.cot) >= 0; });
        $('qa-tem-kho').value = nhap.rong + 'x' + nhap.cao;
        if (!$('qa-tem-kho').value) $('qa-tem-kho').value = '';
    }

    function soTrongKhoang(v, mac, min, max) {
        var n = parseFloat(String(v).replace(',', '.'));
        if (!isFinite(n)) n = mac;
        return Math.min(max, Math.max(min, Math.round(n * 10) / 10));
    }

    function docFormVaoNhap() {
        var f = $('qa-tem-form');
        f.querySelectorAll('[data-k]').forEach(function (el) {
            var k = el.dataset.k;
            if (el.type === 'checkbox') nhap[k] = el.checked;
            else if (el.type === 'number') nhap[k] = soTrongKhoang(el.value, nhap[k], parseFloat(el.min), parseFloat(el.max));
            else nhap[k] = el.value;
        });
        f.querySelectorAll('[data-hien]').forEach(function (el) { nhap.hien[el.dataset.hien] = el.checked; });
        // Giữ thứ tự cột theo thứ tự ô tích trên form
        nhap.cot = [];
        f.querySelectorAll('[data-cot]').forEach(function (el) { if (el.checked) nhap.cot.push(el.dataset.cot); });
    }

    function veXemTruoc() {
        var f = $('qa-tem-xem'), khung = $('qa-tem-xem-khung');
        var pxMm = 96 / 25.4;
        var rongPx = nhap.rong * pxMm, caoPx = nhap.cao * pxMm;
        // Hộp chưa hiện xong thì chưa có bề rộng: lấy tạm 400px, sự kiện shown.bs.modal sẽ vẽ lại
        var tiLe = Math.max(0.1, Math.min(1.6, ((khung.clientWidth || 416) - 16) / rongPx, 420 / caoPx));
        f.style.width = rongPx + 'px';
        f.style.height = caoPx + 'px';
        f.style.transform = 'scale(' + tiLe + ')';
        khung.style.height = (caoPx * tiLe + 16) + 'px';
        var dl = duLieuXem || DU_LIEU_MAU;
        ghiVaoIframe(f, htmlTem(dl.thung, dl.dong, nhap));
        $('qa-tem-xem-ghichu').textContent = (duLieuXem ? 'Xem trước với thùng ' + dl.thung.maThung : 'Xem trước với dữ liệu mẫu')
            + ' · ' + nhap.rong + ' × ' + nhap.cao + ' mm';
    }

    function bao(text, loai) {
        var el = $('qa-tem-thongbao');
        el.textContent = text || '';
        el.className = 'qa-tem-thongbao' + (loai ? ' ' + loai : '');
        if (loai && window.qaThongBao) window.qaThongBao(text, loai);
    }

    function chonMauSua(id) {
        kho.chon = id;
        nhap = JSON.parse(JSON.stringify(mauDangChon()));
        nhap.hien = Object.assign({}, HIEN_DAY_DU, nhap.hien);
        doNhapLenForm();
        veXemTruoc();
    }

    async function moHop() {
        veDsMau();
        bao('');
        var modal = w.bootstrap.Modal.getOrCreateInstance($('qa-tem-modal'));
        modal.show();
        chonMauSua(kho.chon);
        if (nguonDuLieu) {
            try { duLieuXem = await nguonDuLieu(); } catch (e) { duLieuXem = null; }
            veXemTruoc();
        }
    }

    function ganSuKien() {
        var modalEl = $('qa-tem-modal');
        if (modalEl) modalEl.addEventListener('shown.bs.modal', function () { if (nhap) veXemTruoc(); });
        document.addEventListener('click', async function (e) {
            if (e.target.closest('#qa-thung-tem-tuychinh')) { moHop(); return; }
            var nut = e.target.closest('#qa-tem-modal [data-lenh]');
            if (!nut) return;
            var lenh = nut.dataset.lenh;
            docFormVaoNhap();
            if (lenh === 'luu' || lenh === 'luumoi') {
                if (!String(nhap.ten || '').trim()) { bao('Nhập tên mẫu.', 'loi'); return; }
                if (lenh === 'luumoi') {
                    nhap.id = 'm' + Date.now();
                    if (kho.ds.some(function (m) { return m.ten === nhap.ten; })) nhap.ten += ' (bản mới)';
                    kho.ds.push(JSON.parse(JSON.stringify(nhap)));
                } else {
                    var i = kho.ds.findIndex(function (m) { return m.id === nhap.id; });
                    kho.ds[i >= 0 ? i : kho.ds.length] = JSON.parse(JSON.stringify(nhap));
                }
                kho.chon = nhap.id;
                luuKho(); veDsMau(); doNhapLenForm();
                bao('Đã lưu mẫu "' + nhap.ten + '" trên máy này.', 'ok');
            } else if (lenh === 'xoa') {
                if (kho.ds.length <= 1) { bao('Phải còn ít nhất 1 mẫu.', 'loi'); return; }
                if (!(await qaXacNhan('Xoá mẫu tem "' + nhap.ten + '"?'))) return;
                kho.ds = kho.ds.filter(function (m) { return m.id !== nhap.id; });
                kho.chon = kho.ds[0].id;
                luuKho(); veDsMau(); chonMauSua(kho.chon);
                bao('Đã xoá mẫu.', 'ok');
            } else if (lenh === 'macdinh') {
                if (!(await qaXacNhan('Khôi phục 4 mẫu mặc định? Các mẫu tự tạo trên máy này sẽ mất.'))) return;
                kho = { chon: 'm5', ds: mauMacDinh(), macDinhDaCo: ['m1', 'm2', 'm3', 'm4', 'm5'] };
                luuKho(); veDsMau(); chonMauSua(kho.chon);
                bao('Đã khôi phục mẫu mặc định.', 'ok');
            } else if (lenh === 'inthu') {
                var dl = duLieuXem || DU_LIEU_MAU;
                inTem(dl.thung, dl.dong, nhap);
            }
        });

        function khiDoi(e) {
            var t = e.target;
            if (t.id === 'qa-thung-tem-mau') { kho.chon = t.value; luuKho(); return; }
            if (!t.closest || !t.closest('#qa-tem-modal')) return;
            if (t.id === 'qa-tem-mau') { chonMauSua(t.value); luuKho(); veDsMau(); bao(''); return; }
            if (t.id === 'qa-tem-kho') {
                if (!t.value) return;
                var k = t.value.split('x');
                $('qa-tem-rong').value = k[0]; $('qa-tem-cao').value = k[1];
            }
            docFormVaoNhap();
            if (t.id === 'qa-tem-rong' || t.id === 'qa-tem-cao') $('qa-tem-kho').value = nhap.rong + 'x' + nhap.cao;
            if (!$('qa-tem-kho').value) $('qa-tem-kho').value = '';
            veXemTruoc();
        }
        document.addEventListener('change', khiDoi);
        document.addEventListener('input', function (e) {
            if (e.target.closest && e.target.closest('#qa-tem-form') && e.target.type !== 'checkbox' && e.target.tagName !== 'SELECT') khiDoi(e);
        });
    }

    napKho();
    w.qaThungTem = {
        inTem: function (thung, dong) { inTem(thung, dong, mauDangChon()); },
        khoiTao: function (layDuLieu) { nguonDuLieu = layDuLieu; napKho(); veDsMau(); ganSuKien(); }
    };
})(window);
