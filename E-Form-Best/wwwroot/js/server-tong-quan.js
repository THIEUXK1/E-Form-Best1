// /QLServer/TongQuan — báo cáo Server & lưu trữ 3 công ty (hiện tại + theo kỳ), in và xuất Excel.
// Cùng bố cục với switch-tong-quan.js, dùng chung CSS camera-tong-quan.css; phần riêng (dung lượng, phần cứng) ở server-quanly.css.
(function () {
    'use strict';

    var dangTai = false;
    var phien = 0;      // tăng mỗi lần khởi tạo lại (đổi tab): kết quả của lượt tải cũ bị bỏ
    var urlTrang = function (cty) { return '/QLServer/TongQuan' + (cty ? '?congTy=' + encodeURIComponent(cty) : ''); };
    var moDuoc = [];   // công ty người xem mở được trang chi tiết (AdminIT chỉ có tổng quan)
    var nguongDay = 0.85;

    function $id(id) { return document.getElementById(id); }

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function so(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN'); }
    function soTb(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 }); }
    function phanTram(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + '%'; }

    // yyyy-MM-dd theo giờ máy (không dùng toISOString: lệch ngày do UTC)
    function ngayInput(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }
    function gio(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(s || '');
        return m ? m[4] + ':' + m[5] + ' ' + m[3] + '/' + m[2] + '/' + m[1] : '';
    }
    function ngay(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s || '');
        return m ? m[3] + '/' + m[2] + '/' + m[1] : '';
    }
    function thoiLuong(giay) {
        giay = Math.max(0, Math.round(giay || 0));
        var d = Math.floor(giay / 86400), h = Math.floor(giay % 86400 / 3600), p = Math.floor(giay % 3600 / 60);
        if (d > 0) return d + ' ngày' + (h ? ' ' + h + ' giờ' : '');
        if (h > 0) return h + ' giờ' + (p ? ' ' + p + ' phút' : '');
        return p + ' phút';
    }
    function gioMat(giay) { return giay ? (giay / 3600).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' giờ' : '0'; }
    function mauTyLe(t) { return t === null || t === undefined ? '#94a3b8' : t >= 98 ? '#16a34a' : t >= 90 ? '#d97706' : '#dc2626'; }
    // Màu thanh dung lượng: đỏ từ ngưỡng đầy, vàng trước ngưỡng 15 điểm
    function lopDay(tyLe) { return tyLe === null ? '' : tyLe >= nguongDay ? 'sv-thanh-day' : tyLe >= nguongDay - 0.15 ? 'sv-thanh-vua' : ''; }

    function datKy(loai) {
        var homNay = new Date(); homNay.setHours(0, 0, 0, 0);
        var tu, den = new Date(homNay);
        var tuan = BaoCaoSoSanh.kyTuan(loai, homNay);
        if (tuan) {
            tu = tuan.tu; den = tuan.den;
        } else if (loai === '7' || loai === '30') {
            tu = new Date(homNay); tu.setDate(tu.getDate() - (Number(loai) - 1));
        } else if (loai === 'thang-nay') {
            tu = new Date(homNay.getFullYear(), homNay.getMonth(), 1);
        } else if (loai === 'thang-truoc') {
            tu = new Date(homNay.getFullYear(), homNay.getMonth() - 1, 1);
            den = new Date(homNay.getFullYear(), homNay.getMonth(), 0);
        } else {
            return; // tuỳ chọn: giữ ngày đang nhập
        }
        $id('bcTuNgay').value = ngayInput(tu);
        $id('bcDenNgay').value = ngayInput(den);
    }

    function congTy() { return $id('bcCongTy').value || ''; }

    // Đổi phạm vi: cập nhật tiêu đề + URL (bookmark/gửi link được) rồi tải lại số liệu, không reload trang
    function svPhamVi() {
        var cty = congTy();
        var ten = cty ? 'Báo cáo Server và lưu trữ ' + cty : 'Tổng quan Server và lưu trữ';
        $id('bcTieuDe').textContent = ten;
        document.title = ten;
        var dsTen = Array.prototype.map.call($id('bcCongTy').options, function (o) { return o.value; })
            .filter(function (v) { return v; });
        $id('bcInTen').textContent = 'Tình trạng Server & lưu trữ ' + (cty || dsTen.join(' · '));
        $id('bcKhungTongHop').classList.toggle('d-none', !!cty);
        try {
            history.replaceState(null, '', urlTrang(cty));
        } catch (e) { /* trình duyệt chặn đổi URL: vẫn xem được, chỉ không bookmark đúng phạm vi */ }
    }

    function thamSo(cty) {
        return 'tuNgay=' + encodeURIComponent($id('bcTuNgay').value) + '&denNgay=' + encodeURIComponent($id('bcDenNgay').value)
            + (cty ? '&congTy=' + encodeURIComponent(cty) : '');
    }

    function hienLoi(noiDung) {
        var el = $id('bcLoi');
        el.textContent = noiDung || '';
        el.classList.toggle('d-none', !noiDung);
    }

    function tai() {
        if (dangTai) return;
        dangTai = true;
        var p = phien;
        var nut = $id('bcXem');
        nut.disabled = true;
        hienLoi('');

        fetch('/QLServer/TongQuan/DuLieu?' + thamSo(congTy()), { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (res) {
                if (p !== phien) return;
                if (!res.thanhCong) { hienLoi(res.thongBao || 'Không tải được báo cáo.'); return; }
                moDuoc = res.moDuoc || [];
                ve(res.duLieu);
            })
            .catch(function (e) { hienLoi('Lỗi kết nối máy chủ (' + e.message + ').'); })
            .then(function () { if (p === phien) { dangTai = false; nut.disabled = false; } });
    }

    function ve(bc) {
        nguongDay = bc.nguongDay || 0.85;
        $id('bcNguongDay').textContent = Math.round(nguongDay * 100);
        var kyTruoc = BaoCaoSoSanh.kyTruoc(bc);
        $id('bcKyIn').textContent = 'Kỳ ' + ngay(bc.tuNgay) + ' – ' + ngay(bc.denNgay) + ' (so với ' + kyTruoc + ') · lập lúc ' + gio(bc.taoLuc);
        $id('bcKyTruoc').textContent = '(' + kyTruoc + ')';

        var canhBao = $id('bcCanhBao');
        canhBao.textContent = BaoCaoSoSanh.canhBaoLichSu(bc, gio);
        canhBao.classList.toggle('d-none', !canhBao.textContent);

        veCards(bc.congTy);
        BaoCaoSoSanh.veBang($id('bcBangSoSanh'), bc.congTy, 'soSwitchBiMat');
        veTongHop(bc.congTy);
        veLoai(bc.theoLoai || []);
        veLuuTru(bc.luuTru || [], bc.congTy);
        veChuY(bc.canChuY || []);
        veDangMat(bc.dangMat || []);
        veMatNhieu(bc.matNhieu || []);
    }

    // ▲/▼ so với kỳ trước (bao-cao-so-sanh.js)
    function chenh(nay, truoc, tangLaXau, kieu) { return BaoCaoSoSanh.chenh(nay, truoc, tangLaXau, kieu); }

    function oSo(nhan, giaTri, lop) {
        return '<div class="bc-o"><div class="bc-o-nhan">' + esc(nhan) + '</div><div class="bc-o-so ' + (lop || '') + '">' + esc(giaTri) + '</div></div>';
    }

    // Vòng % hoạt động hiện tại (conic-gradient, không cần thư viện biểu đồ, in ra vẫn có màu)
    function veVong(tyLe) {
        var t = tyLe === null ? 0 : Math.max(0, Math.min(100, tyLe));
        var mau = mauTyLe(tyLe);
        return '<div class="bc-vong" style="background: conic-gradient(' + mau + ' ' + t + '%, #fee2e2 0);">'
            + '<div class="bc-vong-trong"><div class="bc-vong-so" style="color:' + mau + '">' + phanTram(tyLe === null ? null : Math.round(tyLe * 10) / 10) + '</div>'
            + '<div class="bc-vong-nhan">hoạt động</div></div></div>';
    }

    // % hoạt động hiện tại tính trên thiết bị đã ping (bỏ thiết bị chưa kiểm lần nào)
    function tyLeHienTai(c) {
        var daKiem = c.hoatDong + c.matKetNoi;
        return daKiem ? 100 * c.hoatDong / daKiem : null;
    }

    // Thanh dung lượng NAS/SAN trong thẻ công ty
    function veDungLuong(c) {
        if (!(c.dungLuongTb > 0)) return '<div class="sv-bc-luu-tru text-muted">Chưa khai dung lượng NAS/SAN.</div>';
        var tyLe = c.daDungTb / c.dungLuongTb;
        var pt = Math.round(tyLe * 100);
        return '<div class="sv-bc-luu-tru"><div class="d-flex justify-content-between">'
            + '<span><i class="fas fa-hard-drive me-1 text-warning"></i>Lưu trữ NAS/SAN</span>'
            + '<span class="fw-bold">' + soTb(c.daDungTb) + ' / ' + soTb(c.dungLuongTb) + ' TB · ' + pt + '%</span></div>'
            + '<div class="sv-thanh"><div class="sv-thanh-dung ' + lopDay(tyLe) + '" style="width:' + Math.min(100, pt) + '%;"></div></div></div>';
    }

    // Chip phần cứng (SNMP): lỗi / cảnh báo / OK
    function veSucKhoe(c) {
        return '<div class="sv-bc-chip">'
            + '<span class="sv-sk ' + (c.sucKhoeLoi ? 'sv-sk-loi' : 'sv-sk-xam') + '" title="Phần cứng lỗi (SNMP)"><i class="fas fa-circle-xmark me-1"></i>' + so(c.sucKhoeLoi) + ' lỗi</span>'
            + '<span class="sv-sk ' + (c.sucKhoeCanhBao ? 'sv-sk-canh-bao' : 'sv-sk-xam') + '" title="Phần cứng cảnh báo (SNMP)"><i class="fas fa-triangle-exclamation me-1"></i>' + so(c.sucKhoeCanhBao) + ' cảnh báo</span>'
            + '<span class="sv-sk sv-sk-ok" title="Phần cứng bình thường (SNMP)"><i class="fas fa-circle-check me-1"></i>' + so(c.sucKhoeOk) + ' OK</span>'
            + '</div>';
    }

    function veCards(ds) {
        var khoa = ['tongTaiSan', 'tong', 'soServer', 'soLuuTru', 'hoatDong', 'matKetNoi', 'soLanMat', 'giayMat',
            'sucKhoeLoi', 'sucKhoeCanhBao', 'sucKhoeOk', 'hetBaoHanh', 'sapHetBaoHanh', 'dungLuongTb', 'daDungTb'];
        var tong = {};
        khoa.forEach(function (k) { tong[k] = 0; });
        ds.forEach(function (c) { khoa.forEach(function (k) { tong[k] += c[k] || 0; }); });

        var html = '';
        if (ds.length > 1) {
            html += '<div class="bc-card bc-card-tong">'
                + '<div class="bc-card-dau"><span class="bc-card-ten"><i class="fas fa-layer-group me-1"></i>Toàn bộ</span>'
                + '<span class="small text-muted">' + ds.length + ' công ty</span></div>'
                + veVong(tyLeHienTai(tong))
                + '<div class="bc-card-so">'
                + oSo('Tổng thiết bị', so(tong.tongTaiSan)) + oSo('Hoạt động', so(tong.hoatDong), 'bc-xanh') + oSo('Mất KN', so(tong.matKetNoi), 'bc-do')
                + oSo('Lần mất KN (kỳ)', so(tong.soLanMat)) + oSo('Giờ mất (kỳ)', gioMat(tong.giayMat))
                + '</div>'
                + veSucKhoe(tong)
                + veDungLuong(tong)
                + '</div>';
        }

        ds.forEach(function (c) {
            var laLink = moDuoc.indexOf(c.congTy) >= 0;
            var the = laLink ? 'a' : 'div';
            html += (laLink
                    ? '<a class="bc-card" href="/QLServer/' + encodeURIComponent(c.congTy) + '" target="_top" title="Mở trang server ' + esc(c.congTy) + '">'
                    : '<div class="bc-card">')
                + '<div class="bc-card-dau"><span class="bc-card-ten"><i class="fas fa-building me-1"></i>' + esc(c.congTy) + '</span>'
                + '<span class="small text-muted">' + (c.kiemTraLuc ? 'ping ' + esc(gio(c.kiemTraLuc)) : '') + '</span></div>';
            if (!c.tongTaiSan) {
                html += '<div class="text-muted small py-3">Chưa có thiết bị nào.</div></' + the + '>';
                return;
            }
            html += veVong(tyLeHienTai(c))
                + '<div class="bc-card-so">'
                + oSo('Server / VM', so(c.soServer)) + oSo('NAS / SAN', so(c.soLuuTru))
                + oSo('Hoạt động', so(c.hoatDong), 'bc-xanh') + oSo('Mất KN', so(c.matKetNoi), c.matKetNoi ? 'bc-do' : '')
                + oSo('Hết / sắp hết BH', so(c.hetBaoHanh) + ' / ' + so(c.sapHetBaoHanh), c.hetBaoHanh ? 'bc-do' : '')
                + '</div>'
                + veSucKhoe(c)
                + veDungLuong(c)
                + '<div class="bc-card-ky">Trong kỳ: <b>' + so(c.soLanMat) + '</b> lần mất KN · <b>' + so(c.soSwitchBiMat) + '</b> thiết bị · '
                + '<b>' + gioMat(c.giayMat) + '</b> · hoạt động <b style="color:' + mauTyLe(c.tyLeTrongKy) + '">' + phanTram(c.tyLeTrongKy) + '</b>'
                + ' · <b>' + so(c.suCoPhanCung) + '</b> sự cố phần cứng'
                + '<br>So kỳ trước: lần mất ' + chenh(c.soLanMat, c.soLanMatTruoc, true) + ' · hoạt động ' + chenh(c.tyLeTrongKy, c.tyLeTruoc, false, 'diem')
                + ' · sự cố PC ' + chenh(c.suCoPhanCung, c.suCoPhanCungTruoc, true) + '</div>'
                + '</' + the + '>';
        });
        $id('bcCards').innerHTML = html;
    }

    function veTongHop(ds) {
        $id('bcBangTongHop').innerHTML = ds.map(function (c) {
            var t = tyLeHienTai(c);
            return '<tr><td class="fw-bold">' + esc(c.congTy) + '</td>'
                + '<td class="text-end">' + so(c.tongTaiSan) + '</td>'
                + '<td class="text-end">' + so(c.soServer) + '</td>'
                + '<td class="text-end">' + so(c.soLuuTru) + '</td>'
                + '<td class="text-end bc-xanh">' + so(c.hoatDong) + '</td>'
                + '<td class="text-end bc-do">' + so(c.matKetNoi) + '</td>'
                + '<td class="text-end">' + phanTram(t === null ? null : Math.round(t * 10) / 10) + '</td>'
                + '<td class="text-end ' + (c.sucKhoeLoi ? 'bc-do fw-bold' : '') + '">' + so(c.sucKhoeLoi) + '</td>'
                + '<td class="text-end ' + (c.sucKhoeCanhBao ? 'text-warning fw-bold' : '') + '">' + so(c.sucKhoeCanhBao) + '</td>'
                + '<td class="text-end">' + so(c.hetBaoHanh) + ' / ' + so(c.sapHetBaoHanh) + '</td>'
                + '<td class="text-end">' + so(c.soLanMat) + '</td>'
                + '<td class="text-end">' + gioMat(c.giayMat) + '</td>'
                + '<td class="text-end">' + so(c.suCoPhanCung) + '</td>'
                + '<td class="text-end fw-bold" style="color:' + mauTyLe(c.tyLeTrongKy) + '">' + phanTram(c.tyLeTrongKy) + '</td></tr>';
        }).join('');
    }

    // Mỗi loại (Server / Máy ảo / NAS / SAN) một dòng, % hoạt động vẽ thành thanh ngang (in ra vẫn giữ màu)
    function veLoai(ds) {
        $id('bcBangLoai').innerHTML = ds.length ? ds.map(function (d) {
            var daKiem = d.hoatDong + d.matKetNoi;
            var t = daKiem ? 100 * d.hoatDong / daKiem : null;
            var luuTru = d.loai === 'NAS' || d.loai === 'SAN';
            return '<tr><td>' + esc(d.congTy) + '</td>'
                + '<td><span class="sv-loai ' + (luuTru ? 'sv-loai-luutru' : '') + '">' + esc(d.loai) + '</span></td>'
                + '<td class="text-end">' + so(d.tongTaiSan) + '</td>'
                + '<td class="text-end bc-xanh">' + so(d.hoatDong) + '</td>'
                + '<td class="text-end ' + (d.matKetNoi ? 'bc-do fw-bold' : '') + '">' + so(d.matKetNoi) + '</td>'
                + '<td><div class="bc-thanh"><div class="bc-thanh-day" style="width:' + (t || 0) + '%;background:' + mauTyLe(t) + '"></div>'
                + '<span class="bc-thanh-so">' + phanTram(t === null ? null : Math.round(t * 10) / 10) + '</span></div></td>'
                + '<td class="text-end ' + (d.sucKhoeLoi ? 'bc-do fw-bold' : '') + '">' + so(d.sucKhoeLoi) + '</td>'
                + '<td class="text-end">' + so(d.soLanMat) + '</td>'
                + '<td class="text-end">' + chenh(d.soLanMat, d.soLanMatTruoc, true) + '</td></tr>';
        }).join('') : '<tr><td colspan="9" class="text-center text-muted py-3">Chưa có thiết bị nào.</td></tr>';
    }

    function veLuuTru(ds, dsCty) {
        var tongDung = 0, tongDa = 0;
        dsCty.forEach(function (c) { tongDung += c.dungLuongTb || 0; tongDa += c.daDungTb || 0; });
        $id('bcLuuTruTong').textContent = tongDung > 0
            ? '— NAS/SAN đã dùng ' + soTb(tongDa) + ' / ' + soTb(tongDung) + ' TB (' + Math.round(100 * tongDa / tongDung) + '%), còn trống ' + soTb(tongDung - tongDa) + ' TB'
            : '';
        $id('bcBangLuuTru').innerHTML = ds.length ? ds.map(function (x) {
            var coSo = x.daDungTb !== null && x.daDungTb !== undefined;
            var tyLe = coSo ? x.daDungTb / x.dungLuongTb : null;
            var pt = tyLe === null ? null : Math.round(tyLe * 100);
            var mat = x.ketNoi === 'DOWN' ? ' <span class="badge bg-danger-subtle text-danger">mất KN</span>' : '';
            return '<tr><td>' + esc(x.congTy) + '</td>'
                + '<td>' + esc(x.ten) + mat + '</td>'
                + '<td>' + esc(x.loai) + '</td>'
                + '<td class="text-muted">' + esc(x.raid) + '</td>'
                + '<td class="text-end text-nowrap">' + soTb(x.dungLuongTb) + '</td>'
                + '<td>' + (coSo
                    ? '<div class="bc-thanh"><div class="bc-thanh-day" style="width:' + Math.min(100, pt) + '%;background:'
                        + (tyLe >= nguongDay ? '#dc2626' : tyLe >= nguongDay - 0.15 ? '#d97706' : '#16a34a') + '"></div>'
                        + '<span class="bc-thanh-so">' + soTb(x.daDungTb) + ' TB · ' + pt + '%</span></div>'
                    : '<span class="text-muted">Chưa khai đã dùng</span>') + '</td></tr>';
        }).join('') : '<tr><td colspan="6" class="text-center text-muted py-3">Chưa có thiết bị nào khai dung lượng.</td></tr>';
    }

    function veChuY(ds) {
        $id('bcDemChuY').textContent = ds.length;
        $id('bcBangChuY').innerHTML = ds.length ? ds.map(function (x) {
            return '<tr><td>' + esc(x.congTy) + '</td>'
                + '<td>' + esc(x.ten) + '</td>'
                + '<td>' + esc(x.loai) + '</td>'
                + '<td class="font-monospace">' + esc(x.ip) + '</td>'
                + '<td>' + x.lyDo.map(function (l) {
                    var nang = l === 'Mất kết nối' || l === 'Hết bảo hành' || /^Dung lượng/.test(l) || /^Lỗi:/.test(l);
                    return '<span class="sv-ly-do ' + (nang ? 'sv-ly-do-do' : '') + '">' + esc(l) + '</span>';
                }).join('') + '</td></tr>';
        }).join('') : '<tr><td colspan="5" class="text-center text-success py-3">Không có thiết bị nào cần chú ý.</td></tr>';
    }

    function veDangMat(ds) {
        $id('bcDemDangMat').textContent = ds.length;
        var bayGio = Date.now();
        $id('bcBangDangMat').innerHTML = ds.length ? ds.map(function (a) {
            var matTu = a.matTu ? new Date(a.matTu).getTime() : null;
            return '<tr><td>' + esc(a.congTy) + '</td>'
                + '<td>' + esc(a.ten) + '</td>'
                + '<td>' + esc(a.loai) + '</td>'
                + '<td class="font-monospace">' + esc(a.ip) + '</td>'
                + '<td class="text-muted">' + esc(a.chucNang) + '</td>'
                + '<td class="text-nowrap">' + (a.matTu ? esc(gio(a.matTu)) : '<span class="text-muted">Không rõ</span>') + '</td>'
                + '<td class="text-nowrap bc-do">' + (matTu ? esc(thoiLuong((bayGio - matTu) / 1000)) : '') + '</td></tr>';
        }).join('') : '<tr><td colspan="7" class="text-center text-muted py-3">Không có thiết bị nào đang mất kết nối.</td></tr>';
    }

    function veMatNhieu(ds) {
        $id('bcBangMatNhieu').innerHTML = ds.length ? ds.map(function (a) {
            return '<tr><td>' + esc(a.congTy) + '</td>'
                + '<td>' + esc(a.ten) + (a.matTu ? ' <span class="badge bg-danger-subtle text-danger">đang mất</span>' : '') + '</td>'
                + '<td class="font-monospace">' + esc(a.ip) + '</td>'
                + '<td class="text-end fw-bold">' + so(a.soLan) + '</td>'
                + '<td class="text-end text-muted">' + so(a.soLanTruoc) + '</td>'
                + '<td class="text-end">' + gioMat(a.giayMat) + '</td></tr>';
        }).join('') : '<tr><td colspan="6" class="text-center text-muted py-3">Không có thiết bị nào mất kết nối trong kỳ.</td></tr>';
    }

    // Gắn sự kiện lên khung báo cáo đang có trong DOM rồi tải số liệu. Trang "Báo cáo định kì" gọi lại mỗi lần
    // nạp tab (khung cũ bị thay nên listener cũ đi theo); tuyChon.giuKy = giữ kỳ/phạm vi đã chép từ tab trước.
    function khoiTao(tuyChon) {
        tuyChon = tuyChon || {};
        phien++;
        dangTai = false;
        if (tuyChon.urlTrang) urlTrang = tuyChon.urlTrang;
        if (!tuyChon.giuKy) datKy('7');
        svPhamVi();
        $id('bcCongTy').addEventListener('change', function () { svPhamVi(); tai(); });
        $id('bcKy').addEventListener('change', function () { datKy(this.value); if (this.value !== 'tuy-chon') tai(); });
        ['bcTuNgay', 'bcDenNgay'].forEach(function (id) {
            $id(id).addEventListener('change', function () { $id('bcKy').value = 'tuy-chon'; });
        });
        $id('bcXem').addEventListener('click', function (e) { e.preventDefault(); tai(); });
        $id('bcIn').addEventListener('click', function (e) { e.preventDefault(); window.print(); });

        // Tải file là điều hướng hợp lệ (trình duyệt giữ nguyên trang, chỉ tải file về)
        $id('bcExcel').parentElement.addEventListener('click', function (e) {
            var muc = e.target.closest('.bc-xuat-excel');
            if (!muc) return;
            e.preventDefault();
            window.location.href = '/QLServer/TongQuan/XuatExcel?' + thamSo(muc.getAttribute('data-cong-ty') || '');
        });
        tai();
    }

    window.BaoCaoTongQuan = window.BaoCaoTongQuan || {};
    window.BaoCaoTongQuan.server = khoiTao;

    document.addEventListener('DOMContentLoaded', function () {
        // Trên trang "Báo cáo định kì" để bao-cao-dinh-ky.js khởi tạo theo tab
        if (!document.getElementById('bcDinhKy')) khoiTao();
    });
})();
