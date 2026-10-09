// So sánh kỳ này với kỳ trước — dùng chung cho camera-/ap-/switch-tong-quan.js.
// Server trả sẵn số kỳ trước trong từng dòng (soLanMatTruoc, giayMatTruoc, tyLeTruoc, <truongBiMat>Truoc)
// và mốc kỳ trước (kyTruocTu, kyTruocDen); file này chỉ trình bày.
(function () {
    'use strict';

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }
    function so(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN'); }
    function phanTram(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + '%'; }
    function gioMat(giay) { return giay ? (giay / 3600).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' giờ' : '0'; }
    function mauTyLe(t) { return t === null || t === undefined ? '#94a3b8' : t >= 98 ? '#16a34a' : t >= 90 ? '#d97706' : '#dc2626'; }
    function ngay(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s || '');
        return m ? m[3] + '/' + m[2] + '/' + m[1] : '';
    }

    // Chênh lệch kỳ này so với kỳ trước: ▲/▼ kèm màu theo tốt/xấu (tangLaXau: số lần mất, giờ mất tăng là xấu;
    // null = không tốt không xấu, vd tổng số đơn → màu trung tính).
    // kieu: '' = số đếm, 'gio' = giây hiện thành giờ, 'diem' = điểm phần trăm. Trả HTML (giá trị đã esc).
    function chenh(nay, truoc, tangLaXau, kieu) {
        if (nay === null || nay === undefined || truoc === null || truoc === undefined) return '<span class="text-muted">–</span>';
        var d = nay - truoc;
        if (kieu === 'diem') d = Math.round(d * 100) / 100;
        if (!d) return '<span class="text-muted">=</span>';
        var giaTri = kieu === 'gio' ? gioMat(Math.abs(d))
            : kieu === 'diem' ? Math.abs(d).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + ' điểm'
            : Math.abs(d).toLocaleString('vi-VN');
        // Số đếm: kèm % thay đổi khi kỳ trước khác 0
        if (!kieu && truoc) giaTri += ' (' + (d > 0 ? '+' : '−') + Math.round(100 * Math.abs(d) / truoc) + '%)';
        var lop = tangLaXau === null || tangLaXau === undefined ? 'text-primary'
            : (tangLaXau ? d > 0 : d < 0) ? 'bc-do' : 'bc-xanh';
        return '<span class="fw-bold ' + lop + '">' + (d > 0 ? '▲ ' : '▼ ') + esc(giaTri) + '</span>';
    }

    function kyTruoc(bc) { return ngay(bc.kyTruocTu) + ' – ' + ngay(bc.kyTruocDen); }

    // Cảnh báo thiếu lịch sử: cho kỳ này, hoặc chỉ cho kỳ trước (phần so sánh chỉ để tham khảo). '' = đủ dữ liệu
    function canhBaoLichSu(bc, gio) {
        if (!bc.lichSuTu) return '';
        if (bc.lichSuTu > bc.tuNgay)
            return 'Lịch sử đổi trạng thái chỉ có từ ' + gio(bc.lichSuTu) + ' — số liệu "trong kỳ" trước mốc này chưa đầy đủ.';
        if (bc.lichSuTu > bc.kyTruocTu)
            return 'Lịch sử đổi trạng thái chỉ có từ ' + gio(bc.lichSuTu) + ' — kỳ trước (' + kyTruoc(bc) + ') chưa đủ dữ liệu, phần so sánh chỉ để tham khảo.';
        return '';
    }

    // Bảng so sánh theo công ty (+ dòng Toàn bộ khi nhiều công ty). truongBiMat: 'soCameraBiMat' / 'soApBiMat' / 'soSwitchBiMat'
    function veBang(tbody, ds, truongBiMat) {
        var ok = ds.filter(function (c) { return !c.loi; });
        var dong = ok.map(function (c) {
            return { congTy: c.congTy, soLanMat: c.soLanMat, soLanMatTruoc: c.soLanMatTruoc, biMat: c[truongBiMat], biMatTruoc: c[truongBiMat + 'Truoc'],
                giayMat: c.giayMat, giayMatTruoc: c.giayMatTruoc, tyLe: c.tyLeTrongKy, tyLeTruoc: c.tyLeTruoc, tong: c.tong };
        });
        if (dong.length > 1) {
            var t = { congTy: 'Toàn bộ', laTong: true, soLanMat: 0, soLanMatTruoc: 0, biMat: 0, biMatTruoc: 0, giayMat: 0, giayMatTruoc: 0 };
            var tyLe = 0, tyLeTruoc = 0, coTyLe = 0;
            dong.forEach(function (c) {
                ['soLanMat', 'soLanMatTruoc', 'biMat', 'biMatTruoc', 'giayMat', 'giayMatTruoc'].forEach(function (k) { t[k] += c[k] || 0; });
                // Cùng độ dài kỳ cho mọi công ty nên % gộp = trung bình có trọng số theo số thiết bị
                if (c.tyLe !== null && c.tyLeTruoc !== null && c.tong) {
                    tyLe += c.tyLe * c.tong; tyLeTruoc += c.tyLeTruoc * c.tong; coTyLe += c.tong;
                }
            });
            t.tyLe = coTyLe ? Math.round(100 * tyLe / coTyLe) / 100 : null;
            t.tyLeTruoc = coTyLe ? Math.round(100 * tyLeTruoc / coTyLe) / 100 : null;
            dong.push(t);
        }
        tbody.innerHTML = dong.length ? dong.map(function (c) {
            return '<tr' + (c.laTong ? ' class="table-light fw-bold"' : '') + '><td class="fw-bold">' + esc(c.congTy) + '</td>'
                + '<td class="text-end">' + so(c.soLanMat) + '</td><td class="text-end text-muted">' + so(c.soLanMatTruoc) + '</td>'
                + '<td class="text-end">' + chenh(c.soLanMat, c.soLanMatTruoc, true) + '</td>'
                + '<td class="text-end">' + so(c.biMat) + '</td><td class="text-end text-muted">' + so(c.biMatTruoc) + '</td>'
                + '<td class="text-end">' + chenh(c.biMat, c.biMatTruoc, true) + '</td>'
                + '<td class="text-end">' + gioMat(c.giayMat) + '</td><td class="text-end text-muted">' + gioMat(c.giayMatTruoc) + '</td>'
                + '<td class="text-end">' + chenh(c.giayMat, c.giayMatTruoc, true, 'gio') + '</td>'
                + '<td class="text-end" style="color:' + mauTyLe(c.tyLe) + '">' + phanTram(c.tyLe) + '</td>'
                + '<td class="text-end text-muted">' + phanTram(c.tyLeTruoc) + '</td>'
                + '<td class="text-end">' + chenh(c.tyLe, c.tyLeTruoc, false, 'diem') + '</td></tr>';
        }).join('') : '<tr><td colspan="13" class="text-center text-muted py-3">Chưa có dữ liệu để so sánh.</td></tr>';
    }

    // Kỳ "tuan-nay" / "tuan-truoc" (tuần bắt đầu thứ Hai); trả null nếu không phải kỳ tuần
    function kyTuan(loai, homNay) {
        if (loai !== 'tuan-nay' && loai !== 'tuan-truoc') return null;
        var tu = new Date(homNay); tu.setDate(tu.getDate() - (tu.getDay() + 6) % 7);
        var den = new Date(homNay);
        if (loai === 'tuan-truoc') {
            tu.setDate(tu.getDate() - 7);
            den = new Date(tu); den.setDate(den.getDate() + 6);
        }
        return { tu: tu, den: den };
    }

    // Kỳ theo giá trị ô chọn kỳ (tuan-nay, 7, 30, thang-nay...); 'tuy-chon' trả null (giữ ngày đang nhập)
    function kyTheoLoai(loai) {
        var homNay = new Date(); homNay.setHours(0, 0, 0, 0);
        var tuan = kyTuan(loai, homNay);
        if (tuan) return tuan;
        var tu, den = new Date(homNay);
        if (loai === '7' || loai === '30') {
            tu = new Date(homNay); tu.setDate(tu.getDate() - (Number(loai) - 1));
        } else if (loai === 'thang-nay') {
            tu = new Date(homNay.getFullYear(), homNay.getMonth(), 1);
        } else if (loai === 'thang-truoc') {
            tu = new Date(homNay.getFullYear(), homNay.getMonth() - 1, 1);
            den = new Date(homNay.getFullYear(), homNay.getMonth(), 0);
        } else {
            return null;
        }
        return { tu: tu, den: den };
    }

    window.BaoCaoSoSanh = {
        chenh: chenh, kyTruoc: kyTruoc, canhBaoLichSu: canhBaoLichSu, veBang: veBang, kyTuan: kyTuan, kyTheoLoai: kyTheoLoai,
        esc: esc, so: so, ngay: ngay
    };
})();
