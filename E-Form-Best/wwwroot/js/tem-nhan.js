// Lõi tem nhãn dùng chung cho trang thiết kế tem và nút "In tem" ở danh sách tủ điện thoại.
// Định dạng mẫu = 1 trang (Sheet) của file .tnl app TemNhan desktop, để nhập/xuất qua lại được:
// toạ độ, kích thước tính bằng mm từ góc trên-trái nhãn; trường trộn {{Tên cột}}, {{NGÀY}}, {{GIỜ}}, {{SỐ:001}}.
// Mọi style vẽ ra đều inline để chép nguyên HTML sang iframe in mà không cần CSS của trang.
(function (w) {
    'use strict';

    var PX_MM = 96 / 25.4;
    var KHOA_CAI_DAT_IN = 'tem-nhan-cai-dat-in';

    // Các trường của tủ điện thoại chèn được vào mẫu
    var TRUONG_TU = [
        { ten: 'Bộ phận', khoa: 'tenBoPhan' },
        { ten: 'Tên tủ', khoa: 'tenTu' },
        { ten: 'Vị trí', khoa: 'viTri' },
        { ten: 'Mã NV', khoa: 'maNhanVien' },
        { ten: 'Người sử dụng', khoa: 'nguoiSuDung' }
    ];

    // Giống danh sách khổ mặc định của app TemNhan
    var KHO_CO_SAN = [
        ['50 × 30 mm', 50, 30], ['40 × 30 mm', 40, 30], ['35 × 22 mm', 35, 22],
        ['60 × 40 mm', 60, 40], ['70 × 50 mm', 70, 50], ['100 × 50 mm', 100, 50],
        ['100 × 100 mm', 100, 100], ['100 × 150 mm (vận đơn)', 100, 150],
        ['Băng 12 mm × 40 mm', 40, 12], ['Băng 24 mm × 58,7 mm', 58.7, 24]
    ];

    var PHONG_CHU = ['Arial', 'Tahoma', 'Verdana', 'Segoe UI', 'Calibri', 'Times New Roman', 'Courier New'];

    function so(v, macDinh, min, max) {
        var n = typeof v === 'number' ? v : parseFloat(v);
        if (!isFinite(n)) n = macDinh;
        if (min != null && n < min) n = min;
        if (max != null && n > max) n = max;
        return Math.round(n * 100) / 100;
    }

    function chon(v, ds, macDinh) { return ds.indexOf(v) >= 0 ? v : macDinh; }

    // Bỏ dấu, viết hoa, bỏ khoảng trắng: "Tên tủ" → "TENTU" — gõ hoa/thường, có/không dấu đều khớp
    function boDau(s) {
        return String(s == null ? '' : s).normalize('NFD').replace(/[\u0300-\u036f]/g, '')
            .replace(/đ/g, 'd').replace(/Đ/g, 'D').toUpperCase().replace(/\s+/g, '');
    }

    function dinhDangNgay(d, mau) {
        function p(n, l) { return String(n).padStart(l || 2, '0'); }
        return mau.replace(/yyyy|yy|MM|dd|HH|mm|ss/g, function (t) {
            switch (t) {
                case 'yyyy': return String(d.getFullYear());
                case 'yy': return p(d.getFullYear() % 100);
                case 'MM': return p(d.getMonth() + 1);
                case 'dd': return p(d.getDate());
                case 'HH': return p(d.getHours());
                case 'mm': return p(d.getMinutes());
                default: return p(d.getSeconds());
            }
        });
    }

    // Dòng dữ liệu để trộn từ 1 tủ điện thoại (kết quả API DanhSach)
    function taoDong(tu) {
        var dong = {};
        TRUONG_TU.forEach(function (t) { dong[boDau(t.ten)] = tu ? (tu[t.khoa] || '') : ''; });
        dong[boDau('Mã nhân viên')] = dong[boDau('Mã NV')];
        dong[boDau('Vị trí tủ')] = dong[boDau('Vị trí')];
        return dong;
    }

    // Thay {{...}} bằng dữ liệu; trường lạ giữ nguyên để người thiết kế thấy gõ sai
    function tron(text, dong, stt, now) {
        return String(text == null ? '' : text).replace(/\{\{(.+?)\}\}/g, function (m, ten) {
            ten = ten.trim();
            var k = boDau(ten);
            // Ưu tiên đúng tên cột (Excel có thể có "MA TU" và "MÃ TỦ" cùng lúc), rồi mới tới tên bỏ dấu
            if (dong && Object.prototype.hasOwnProperty.call(dong, '=' + ten)) return dong['=' + ten] == null ? '' : String(dong['=' + ten]);
            if (dong && Object.prototype.hasOwnProperty.call(dong, k)) return dong[k] == null ? '' : String(dong[k]);
            var i = ten.indexOf(':');
            var khoa = boDau(i < 0 ? ten : ten.slice(0, i));
            var arg = i < 0 ? null : ten.slice(i + 1);
            if (khoa === 'NGAY') return dinhDangNgay(now || new Date(), arg || 'dd/MM/yyyy');
            if (khoa === 'GIO') return dinhDangNgay(now || new Date(), arg || 'HH:mm');
            if (khoa === 'SO') {
                var a = (arg || '1').trim();
                if (/^\d+$/.test(a)) return String(parseInt(a, 10) + (stt || 0)).padStart(a.length, '0');
            }
            return m;
        });
    }

    function macDinhDoiTuong(type) {
        var chung = { type: type, X: 0, Y: 0, W: 20, H: 8, Rotation: 0, GroupId: null, Locked: false, Printable: true };
        var rieng = {
            text: { Text: 'Chữ', FontFamily: 'Arial', FontSizePt: 12, Bold: false, Italic: false, HAlign: 'Center', VAlign: 'Center', ShrinkToFit: true, Wrap: false },
            frame: { StrokeMm: 0.3, CornerMm: 0, Filled: false },
            line: { StrokeMm: 0.3, Orientation: 'Horizontal' },
            barcode: { Kind: 'Code128', Data: '123456789', ShowText: true, TextSizePt: 7 },
            image: { ImageData: '', KeepAspect: true, Monochrome: false }
        }[type] || {};
        return Object.assign(chung, rieng);
    }

    function chuanHoaDoiTuong(el) {
        var type = ['text', 'frame', 'line', 'barcode', 'image'].indexOf(el && el.type) >= 0 ? el.type : null;
        if (!type) return null;
        var o = Object.assign(macDinhDoiTuong(type), el);
        o.X = so(o.X, 0, -1000, 1000); o.Y = so(o.Y, 0, -1000, 1000);
        o.W = so(o.W, 20, 0.5, 1000); o.H = so(o.H, 8, 0.5, 1000);
        o.Rotation = ((Math.round(so(o.Rotation, 0) / 90) * 90) % 360 + 360) % 360;
        o.Locked = !!o.Locked; o.Printable = o.Printable !== false;
        if (type === 'text') {
            o.Text = o.Text == null ? '' : String(o.Text);
            o.FontFamily = o.FontFamily ? String(o.FontFamily) : 'Arial';
            o.FontSizePt = so(o.FontSizePt, 12, 2, 400);
            o.Bold = !!o.Bold; o.Italic = !!o.Italic; o.ShrinkToFit = !!o.ShrinkToFit; o.Wrap = !!o.Wrap;
            o.HAlign = chon(o.HAlign, ['Left', 'Center', 'Right'], 'Center');
            o.VAlign = chon(o.VAlign, ['Top', 'Center', 'Bottom'], 'Center');
        } else if (type === 'frame') {
            o.StrokeMm = so(o.StrokeMm, 0.3, 0, 20); o.CornerMm = so(o.CornerMm, 0, 0, 100); o.Filled = !!o.Filled;
        } else if (type === 'line') {
            o.StrokeMm = so(o.StrokeMm, 0.3, 0.05, 20);
            o.Orientation = chon(o.Orientation, ['Horizontal', 'Vertical'], 'Horizontal');
        } else if (type === 'barcode') {
            o.Kind = chon(o.Kind, ['Code128', 'Code39', 'Ean13', 'QrCode', 'DataMatrix'], 'Code128');
            o.Data = o.Data == null ? '' : String(o.Data);
            o.ShowText = !!o.ShowText; o.TextSizePt = so(o.TextSizePt, 7, 3, 72);
        } else if (type === 'image') {
            o.ImageData = o.ImageData ? String(o.ImageData) : '';
            o.KeepAspect = o.KeepAspect !== false; o.Monochrome = !!o.Monochrome;
        }
        return o;
    }

    function taoTrang() {
        return { WidthMm: 50, HeightMm: 30, MarginMm: 1, AutoLength: false, Name: 'Trang 1', PaperColor: '#FFFFFF', Elements: [], DataSourcePath: null, DataSheet: null };
    }

    function chuanHoaTrang(t) {
        var o = Object.assign(taoTrang(), t || {});
        o.WidthMm = so(o.WidthMm, 50, 5, 1000); o.HeightMm = so(o.HeightMm, 30, 5, 1000);
        o.MarginMm = so(o.MarginMm, 0, 0, 50); o.AutoLength = !!o.AutoLength;
        o.Name = o.Name ? String(o.Name) : 'Trang 1';
        o.PaperColor = /^#[0-9a-f]{6}$/i.test(o.PaperColor || '') ? String(o.PaperColor).toUpperCase() : '#FFFFFF';
        o.Elements = (Array.isArray(o.Elements) ? o.Elements : []).map(chuanHoaDoiTuong).filter(Boolean);
        return o;
    }

    // Mẫu gợi ý cho tủ điện thoại khi chưa có mẫu nào
    function taoTrangMacDinh() {
        var t = taoTrang();
        function chu(text, x, y, ww, h, co, dam) {
            return chuanHoaDoiTuong({ type: 'text', Text: text, X: x, Y: y, W: ww, H: h, FontSizePt: co, Bold: !!dam, HAlign: 'Left' });
        }
        t.Elements = [
            chu('{{Tên tủ}}', 1.5, 1.5, 29, 11, 22, true),
            chu('{{Người sử dụng}}', 1.5, 13, 29, 7, 12),
            chu('{{Mã NV}}', 1.5, 20, 29, 4.5, 9),
            chu('{{Bộ phận}}', 1.5, 24.5, 29, 4, 8),
            chuanHoaDoiTuong({ type: 'barcode', Kind: 'QrCode', Data: '{{Tên tủ}} | {{Mã NV}} | {{Người sử dụng}}', X: 31.5, Y: 7, W: 17, H: 17 })
        ];
        return t;
    }

    // Đọc file .tnl (dự án nhiều trang) hoặc JSON 1 trang → mảng trang
    function docTnl(text) {
        var goc = JSON.parse(text);
        var ds = goc && Array.isArray(goc.Sheets) ? goc.Sheets : (goc && Array.isArray(goc.Elements) ? [goc] : null);
        if (!ds || !ds.length) throw new Error('File không phải mẫu tem .tnl.');
        return ds.map(chuanHoaTrang);
    }

    // Mẫu lưu trên máy chủ và file .tnl cùng một dạng: { Sheets: [trang, ...] } (nhiều trang như app TemNhan)
    function xuatDuAn(dsTrang, thut) {
        return JSON.stringify({ Sheets: dsTrang, DataSourcePath: null, DataSheet: null }, null, thut ? 2 : 0);
    }

    function xuatTnl(trangHoacDs) {
        return xuatDuAn(Array.isArray(trangHoacDs) ? trangHoacDs : [trangHoacDs], true);
    }

    // Xuất 1 nhãn ra PNG (300 dpi) — vẽ HTML của nhãn vào SVG foreignObject rồi chép sang canvas
    function xuatPng(trang, dong) {
        return new Promise(function (resolve, reject) {
            var kho = taoDiv('position:fixed;left:-30000px;top:0;visibility:hidden;');
            document.body.appendChild(kho);
            var kq = veNhan(trang, { dong: dong, stt: 0, now: new Date() });
            kho.appendChild(kq.el);
            thuNhoChu(kq.el);
            var html = kq.el.outerHTML;
            document.body.removeChild(kho);
            var rongPx = hieuRong(trang) * PX_MM, caoPx = trang.HeightMm * PX_MM, tl = 300 / 96;
            var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + rongPx + '" height="' + caoPx + '">' +
                '<foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml">' + html + '</div></foreignObject></svg>';
            var img = new Image();
            img.onload = function () {
                var c = document.createElement('canvas');
                c.width = Math.round(rongPx * tl); c.height = Math.round(caoPx * tl);
                var g = c.getContext('2d');
                g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);
                g.scale(tl, tl);
                g.drawImage(img, 0, 0);
                c.toBlob(function (b) { if (b) resolve(b); else reject(new Error('Trình duyệt không xuất được ảnh.')); }, 'image/png');
            };
            img.onerror = function () { reject(new Error('Trình duyệt không vẽ được nhãn ra ảnh.')); };
            img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
        });
    }

    // "Độ dài tự động" như bỏ Fix Length của P-touch: rộng = mép phải đối tượng xa nhất + lề
    function hieuRong(trang) {
        if (!trang.AutoLength || !trang.Elements.length) return trang.WidthMm;
        var xa = 0;
        trang.Elements.forEach(function (e) { xa = Math.max(xa, e.X + e.W); });
        return so(xa + trang.MarginMm, trang.WidthMm, 5, 1000);
    }

    function ganStyle(el, css) { el.setAttribute('style', css); return el; }
    function taoDiv(css) { return ganStyle(document.createElement('div'), css || ''); }

    var DA_DAT_UTF8 = false;

    function veMaVach(e, data, hop, loi) {
        var la2D = e.Kind === 'QrCode' || e.Kind === 'DataMatrix';
        if (!data) { loi.push('Mã vạch không có dữ liệu'); return false; }
        if (la2D) {
            if (!w.qrcode) { loi.push('Chưa tải được thư viện QR'); return false; }
            if (!DA_DAT_UTF8 && w.qrcode.stringToBytesFuncs && w.qrcode.stringToBytesFuncs['UTF-8']) {
                w.qrcode.stringToBytes = w.qrcode.stringToBytesFuncs['UTF-8'];
                DA_DAT_UTF8 = true;
            }
            try {
                var q = w.qrcode(0, 'M');
                q.addData(data);
                q.make();
                hop.innerHTML = q.createSvgTag({ cellSize: 1, margin: 0, scalable: true });
                var svgQr = hop.firstElementChild;
                svgQr.setAttribute('preserveAspectRatio', 'xMidYMid meet');
                ganStyle(svgQr, 'display:block;width:100%;height:100%;');
                return true;
            } catch (ex) {
                loi.push('QR quá dài'); return false;
            }
        }
        if (!w.JsBarcode) { loi.push('Chưa tải được thư viện mã vạch'); return false; }
        var hopLe = true;
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        try {
            w.JsBarcode(svg, data, {
                format: { Code128: 'CODE128', Code39: 'CODE39', Ean13: 'EAN13' }[e.Kind],
                displayValue: false, margin: 0, height: 50, width: 2, background: 'transparent',
                valid: function (v) { hopLe = v; }
            });
        } catch (ex) { hopLe = false; }
        if (!hopLe) { loi.push('Dữ liệu "' + data + '" không hợp lệ cho ' + e.Kind); return false; }
        svg.setAttribute('preserveAspectRatio', 'none');
        ganStyle(svg, 'display:block;width:100%;flex:1 1 auto;min-height:0;');
        var cot = taoDiv('display:flex;flex-direction:column;width:100%;height:100%;');
        cot.appendChild(svg);
        if (e.ShowText) {
            var chu = taoDiv('flex:0 0 auto;font-family:Arial;font-size:' + e.TextSizePt + 'pt;line-height:1.1;text-align:center;white-space:nowrap;overflow:hidden;');
            chu.textContent = data;
            cot.appendChild(chu);
        }
        hop.appendChild(cot);
        return true;
    }

    function veNoiDung(e, hop, ctx, loi) {
        if (e.type === 'text') {
            var ngang = { Left: 'flex-start', Center: 'center', Right: 'flex-end' }[e.HAlign];
            var doc = { Top: 'flex-start', Center: 'center', Bottom: 'flex-end' }[e.VAlign];
            ganStyle(hop, hop.getAttribute('style') + 'display:flex;overflow:hidden;justify-content:' + ngang + ';align-items:' + doc + ';');
            var sp = document.createElement('span');
            ganStyle(sp, 'font-family:"' + e.FontFamily.replace(/"/g, '') + '";font-size:' + e.FontSizePt + 'pt;line-height:1.15;' +
                'font-weight:' + (e.Bold ? '700' : '400') + ';font-style:' + (e.Italic ? 'italic' : 'normal') + ';' +
                'text-align:' + e.HAlign.toLowerCase() + ';' +
                (e.Wrap ? 'white-space:pre-wrap;overflow-wrap:anywhere;max-width:100%;' : 'white-space:pre;flex-shrink:0;'));
            sp.textContent = tron(e.Text, ctx.dong, ctx.stt, ctx.now);
            if (e.ShrinkToFit) { sp.setAttribute('data-thu-nho', '1'); sp.setAttribute('data-co', e.FontSizePt); }
            hop.appendChild(sp);
        } else if (e.type === 'frame') {
            hop.appendChild(taoDiv('box-sizing:border-box;width:100%;height:100%;border:' + e.StrokeMm + 'mm solid #000;' +
                'border-radius:' + e.CornerMm + 'mm;background:' + (e.Filled ? '#000' : 'transparent') + ';'));
        } else if (e.type === 'line') {
            var ngangLine = e.Orientation === 'Horizontal';
            var vach = taoDiv('display:flex;width:100%;height:100%;align-items:center;justify-content:center;');
            vach.appendChild(taoDiv('background:#000;' + (ngangLine ? 'width:100%;height:' + e.StrokeMm + 'mm;' : 'height:100%;width:' + e.StrokeMm + 'mm;')));
            hop.appendChild(vach);
        } else if (e.type === 'barcode') {
            var loiMa = [];
            if (!veMaVach(e, tron(e.Data, ctx.dong, ctx.stt, ctx.now), hop, loiMa)) {
                loi.push(loiMa[0]);
                // Ô đỏ để người thiết kế thấy ngay; khi in thì bị chặn ở inNhan, không ra bản in
                var o = taoDiv('box-sizing:border-box;width:100%;height:100%;border:1px dashed #dc2626;background:rgba(220,38,38,.12);' +
                    'color:#dc2626;font:7pt Arial;display:flex;align-items:center;justify-content:center;text-align:center;overflow:hidden;');
                o.textContent = loiMa[0];
                hop.appendChild(o);
            }
        } else if (e.type === 'image') {
            if (!e.ImageData) return;
            var img = document.createElement('img');
            img.alt = '';
            img.src = e.ImageData.indexOf('data:') === 0 ? e.ImageData : 'data:image/png;base64,' + e.ImageData;
            ganStyle(img, 'display:block;width:100%;height:100%;object-fit:' + (e.KeepAspect ? 'contain' : 'fill') + ';' +
                (e.Monochrome ? 'filter:grayscale(1) contrast(1.4);' : ''));
            hop.appendChild(img);
        }
    }

    // Vẽ 1 nhãn. ctx: { dong, stt, now, thietKe } — thietKe: hiện màu giấy + đối tượng không in (mờ).
    // Trả { el, loi: [] }; gọi thuNhoChu(el) SAU khi đã gắn el vào trang để đo được chữ.
    function veNhan(trang, ctx) {
        ctx = ctx || {};
        var rong = hieuRong(trang);
        var goc = taoDiv('position:relative;overflow:hidden;width:' + rong + 'mm;height:' + trang.HeightMm + 'mm;' +
            'background:' + (ctx.thietKe ? trang.PaperColor : '#fff') + ';color:#000;font-family:Arial;');
        var loi = [];
        trang.Elements.forEach(function (e, i) {
            if (!e.Printable && !ctx.thietKe) return;
            var o = taoDiv('position:absolute;left:' + e.X + 'mm;top:' + e.Y + 'mm;width:' + e.W + 'mm;height:' + e.H + 'mm;' +
                (!e.Printable ? 'opacity:.4;' : ''));
            o.setAttribute('data-i', i);
            var r = e.Rotation;
            var hop = (r === 90 || r === 270)
                ? taoDiv('position:absolute;width:' + e.H + 'mm;height:' + e.W + 'mm;left:' + so((e.W - e.H) / 2) + 'mm;top:' + so((e.H - e.W) / 2) + 'mm;transform:rotate(' + r + 'deg);')
                : taoDiv('position:absolute;left:0;top:0;width:100%;height:100%;' + (r === 180 ? 'transform:rotate(180deg);' : ''));
            o.appendChild(hop);
            var loiDt = [];
            veNoiDung(e, hop, ctx, loiDt);
            if (loiDt.length) loi.push({ i: i, thongBao: loiDt[0] });
            goc.appendChild(o);
        });
        return { el: goc, loi: loi };
    }

    // Tự thu nhỏ cỡ chữ cho vừa khung (ShrinkToFit) — phải đo trên DOM đã gắn vào trang
    function thuNhoChu(goc) {
        goc.querySelectorAll('[data-thu-nho]').forEach(function (sp) {
            var hop = sp.parentNode;
            var co = parseFloat(sp.getAttribute('data-co')) || 12;
            sp.style.fontSize = co + 'pt';
            var lan = 0;
            while (lan < 80 && co > 2 && (sp.offsetWidth > hop.clientWidth + 0.5 || sp.offsetHeight > hop.clientHeight + 0.5)) {
                co *= 0.94;
                sp.style.fontSize = co.toFixed(2) + 'pt';
                lan++;
            }
        });
    }

    // ===== Tem in Agent (app chạy nền trên máy in tem, TemNhan.Agent) =====
    // Trình duyệt không đọc được máy in / khổ tem của driver, nên hỏi agent ở 127.0.0.1. Không có agent thì in qua trình duyệt.

    var AGENT_URL = 'http://127.0.0.1:47123';

    async function goiAgent(duongDan, body, msCho) {
        var ctrl = new AbortController();
        var hen = setTimeout(function () { ctrl.abort(); }, msCho || 5000);
        try {
            var res = await fetch(AGENT_URL + duongDan, body
                ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), signal: ctrl.signal }
                : { signal: ctrl.signal });
            if (!res.ok) {
                var loi = null;
                try { loi = await res.json(); } catch (ex) { /* không phải JSON */ }
                throw new Error((loi && loi.error) || ('Tem in Agent trả lỗi ' + res.status));
            }
            return await res.json();
        } catch (ex) {
            if (ex.name === 'AbortError') throw new Error('Tem in Agent không trả lời.');
            throw ex;
        } finally {
            clearTimeout(hen);
        }
    }

    // Tên các trường {{...}} có trong mẫu → giá trị của dòng, để agent (LabelRenderer của TemNhan) trộn đúng tên.
    // Trường có sẵn (NGÀY, GIỜ, SỐ) không gửi: agent tự tính theo lúc in và số thứ tự tem.
    function dongChoAgent(trang, dong) {
        var kq = {};
        if (!dong) return kq;
        trang.Elements.forEach(function (e) {
            var text = e.type === 'text' ? e.Text : e.type === 'barcode' ? e.Data : '';
            (text || '').replace(/\{\{(.+?)\}\}/g, function (m, ten) {
                var t = ten.trim(), k = boDau(t);
                var v = Object.prototype.hasOwnProperty.call(dong, '=' + t) ? dong['=' + t] : Object.prototype.hasOwnProperty.call(dong, k) ? dong[k] : undefined;
                if (v !== undefined) kq[t] = v == null ? '' : String(v);
                return m;
            });
        });
        return kq;
    }

    // Vẽ thử mọi nhãn để bắt mã vạch lỗi trước khi in (giống app: tem lỗi không được in)
    function kiemTraLoi(trang, dsDong) {
        var loi = [];
        (dsDong.length ? dsDong : [null]).forEach(function (dong, i) {
            if (loi.length >= 5) return;
            var kq = veNhan(trang, { dong: dong, stt: i, now: new Date() });
            if (kq.loi.length) loi.push('Nhãn ' + (i + 1) + ': ' + kq.loi[0].thongBao);
        });
        return loi;
    }

    var agent = {
        // null nếu máy này chưa cài / chưa chạy agent
        kiemTra: async function () {
            try { return await goiAgent('/api/ping', null, 1500); } catch (ex) { return null; }
        },
        mayIn: async function () { return (await goiAgent('/api/printers')).printers || []; },
        khoTem: async function (may) { return (await goiAgent('/api/media?printer=' + encodeURIComponent(may), null, 10000)).media || []; },
        // Tuỳ chọn driver có hỗ trợ: { autoCut, halfCut, chainPrint, mirror, quality } (agent ≥ 1.0.1; bản cũ trả null)
        tinhNang: async function (may) {
            try { return await goiAgent('/api/features?printer=' + encodeURIComponent(may), null, 10000); } catch (ex) { return null; }
        },
        // Máy in có đang lỗi / ngoại tuyến không (null = bình thường)
        tinhTrangMay: async function (may) { return (await goiAgent('/api/status?printer=' + encodeURIComponent(may), null, 10000)).problem; },
        // In thẳng qua driver (không hiện hộp thoại). Trả { pages, warning }.
        inNhan: async function (may, trang, dsDong, opts) {
            opts = opts || {};
            var loi = kiemTraLoi(trang, dsDong);
            if (loi.length) throw new Error('Không in vì mã vạch lỗi:\n' + loi.join('\n'));
            return goiAgent('/api/print', {
                printer: may,
                sheet: trang,
                rows: dsDong.map(function (d) { return dongChoAgent(trang, d); }),
                copies: Math.max(1, Math.round(opts.soBan || 1)),
                rotate: !!opts.xoay,
                offsetXmm: so(opts.lechX, 0),
                offsetYmm: so(opts.lechY, 0),
                // null = để mặc định của driver; chỉ gửi khi người dùng đã chọn
                autoCut: opts.tuCat == null ? null : !!opts.tuCat,
                halfCut: opts.catNua == null ? null : !!opts.catNua,
                chainPrint: opts.noiTiep == null ? null : !!opts.noiTiep,
                mirror: opts.inGuong == null ? null : !!opts.inGuong,
                highQuality: opts.chatLuongCao == null ? null : !!opts.chatLuongCao
            }, 120000);
        }
    };

    // Tải bộ cài agent (máy chủ lấy từ file server). Không lấy được thì trả về đường dẫn thư mục để mở bằng Explorer.
    agent.taiBoCai = async function () {
        var res = await fetch('/FormHR/QuanLyTuDienThoai/TemInAgent/KiemTra');
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
        var kq = await res.json();
        if (!kq.thanhCong) {
            var e = new Error(kq.thongBao || 'Không lấy được bộ cài.');
            e.thuMuc = kq.duLieu && kq.duLieu.thuMuc;
            throw e;
        }
        var a = document.createElement('a');
        a.href = '/FormHR/QuanLyTuDienThoai/TemInAgent/Tai';
        a.download = 'TemNhanAgent_CaiDat.msi';
        document.body.appendChild(a);
        a.click();
        a.remove();
        return kq.duLieu;
    };

    // Khổ tem của driver có khớp khổ nhãn không (băng cuộn: chỉ cần bề ngang băng = 1 cạnh nhãn)
    function khopKho(m, rong, cao) {
        var sai = 1.5;
        if (m.heightMm == null) return Math.abs(m.widthMm - cao) < sai || Math.abs(m.widthMm - rong) < sai;
        return (Math.abs(m.widthMm - rong) < sai && Math.abs(m.heightMm - cao) < sai) ||
            (Math.abs(m.widthMm - cao) < sai && Math.abs(m.heightMm - rong) < sai);
    }

    function docCaiDatIn() {
        var c = { soBan: 1, lechX: 0, lechY: 0, xoay: false, mayIn: '' };
        try { Object.assign(c, JSON.parse(localStorage.getItem(KHOA_CAI_DAT_IN) || '{}')); } catch (ex) { /* trình duyệt chặn bộ nhớ: dùng mặc định */ }
        c.soBan = Math.round(so(c.soBan, 1, 1, 50)); c.lechX = so(c.lechX, 0, -20, 20); c.lechY = so(c.lechY, 0, -20, 20); c.xoay = !!c.xoay;
        c.mayIn = c.mayIn ? String(c.mayIn) : '';
        return c;
    }

    function luuCaiDatIn(c) {
        try { localStorage.setItem(KHOA_CAI_DAT_IN, JSON.stringify(c)); } catch (ex) { /* không lưu được thì thôi, lần sau nhập lại */ }
    }

    // In danh sách nhãn qua hộp thoại in của trình duyệt: mỗi nhãn 1 trang đúng khổ (@page), không lề.
    // opts: { soBan, lechX, lechY, xoay } — xoay 90° cho máy in băng nạp theo chiều dọc (giống app TemNhan).
    function inNhan(trang, dsDong, opts) {
        return new Promise(function (resolve, reject) {
            opts = opts || {};
            var soBan = Math.max(1, Math.round(opts.soBan || 1));
            var lechX = so(opts.lechX, 0), lechY = so(opts.lechY, 0);
            var rong = hieuRong(trang), cao = trang.HeightMm;
            var giayW = opts.xoay ? cao : rong, giayH = opts.xoay ? rong : cao;
            var now = new Date();

            // Vẽ ngoài màn hình trong chính trang này để đo thu nhỏ chữ và dùng thư viện mã vạch đã nạp
            var kho = taoDiv('position:fixed;left:-30000px;top:0;visibility:hidden;');
            document.body.appendChild(kho);
            var cacTrang = [], loi = [], stt = 0;
            try {
                (dsDong.length ? dsDong : [null]).forEach(function (dong, iDong) {
                    for (var b = 0; b < soBan; b++) {
                        var kq = veNhan(trang, { dong: dong, stt: stt, now: now });
                        stt++;
                        kho.appendChild(kq.el);
                        thuNhoChu(kq.el);
                        if (kq.loi.length && loi.length < 5) loi.push('Nhãn ' + (iDong + 1) + ': ' + kq.loi[0].thongBao);
                        cacTrang.push(kq.el.outerHTML);
                        kho.removeChild(kq.el);
                    }
                });
            } finally {
                document.body.removeChild(kho);
            }
            if (loi.length) { reject(new Error('Không in vì mã vạch lỗi:\n' + loi.join('\n'))); return; }

            var bienDoi = 'translate(' + so(lechX + (opts.xoay ? cao : 0)) + 'mm,' + lechY + 'mm)' + (opts.xoay ? ' rotate(90deg)' : '');
            var html = '<!doctype html><html><head><meta charset="utf-8"><title>In tem</title><style>' +
                '@page{size:' + giayW + 'mm ' + giayH + 'mm;margin:0}' +
                'html,body{margin:0;padding:0;-webkit-print-color-adjust:exact;print-color-adjust:exact}' +
                '.trang{position:relative;overflow:hidden;width:' + giayW + 'mm;height:' + giayH + 'mm;break-after:page;page-break-after:always}' +
                '.trang:last-child{break-after:auto;page-break-after:auto}' +
                '.dat{position:absolute;left:0;top:0;transform-origin:0 0;transform:' + bienDoi + '}' +
                '</style></head><body>' +
                cacTrang.map(function (h) { return '<div class="trang"><div class="dat">' + h + '</div></div>'; }).join('') +
                '</body></html>';

            var f = document.createElement('iframe');
            f.setAttribute('aria-hidden', 'true');
            ganStyle(f, 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;');
            var daDon = false;
            function don() { if (!daDon) { daDon = true; setTimeout(function () { f.remove(); }, 500); } }
            f.onload = function () {
                try {
                    var cs = f.contentWindow;
                    cs.addEventListener('afterprint', don);
                    cs.focus();
                    cs.print();
                    setTimeout(don, 60000);
                    resolve(cacTrang.length);
                } catch (ex) { don(); reject(ex); }
            };
            f.srcdoc = html;
            document.body.appendChild(f);
        });
    }

    w.TemNhan = {
        PX_MM: PX_MM,
        TRUONG_TU: TRUONG_TU,
        KHO_CO_SAN: KHO_CO_SAN,
        PHONG_CHU: PHONG_CHU,
        so: so,
        boDau: boDau,
        taoDong: taoDong,
        tron: tron,
        macDinhDoiTuong: macDinhDoiTuong,
        chuanHoaDoiTuong: chuanHoaDoiTuong,
        taoTrang: taoTrang,
        taoTrangMacDinh: taoTrangMacDinh,
        chuanHoaTrang: chuanHoaTrang,
        docTnl: docTnl,
        xuatTnl: xuatTnl,
        xuatDuAn: xuatDuAn,
        xuatPng: xuatPng,
        hieuRong: hieuRong,
        veNhan: veNhan,
        thuNhoChu: thuNhoChu,
        docCaiDatIn: docCaiDatIn,
        luuCaiDatIn: luuCaiDatIn,
        inNhan: inNhan,
        agent: agent,
        khopKho: khopKho
    };
})(window);
