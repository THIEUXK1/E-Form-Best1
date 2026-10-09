// /QLSwitch/TongQuan — báo cáo tổng quan Switch 3 công ty (hiện tại + theo kỳ), in và xuất Excel.
// Cùng bố cục với camera-tong-quan.js, dùng chung CSS camera-tong-quan.css.
(function () {
    'use strict';

    var dangTai = false;
    var phien = 0;      // tăng mỗi lần khởi tạo lại (đổi tab): kết quả của lượt tải cũ bị bỏ
    var urlTrang = function (cty) { return '/QLSwitch/TongQuan' + (cty ? '?congTy=' + encodeURIComponent(cty) : ''); };
    var moDuoc = [];   // công ty người xem mở được trang chi tiết (AdminIT chỉ có tổng quan)

    function $id(id) { return document.getElementById(id); }

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function so(v) { return v === null || v === undefined ? '–' : Number(v).toLocaleString('vi-VN'); }
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
    function swPhamVi() {
        var cty = congTy();
        var ten = cty ? 'Báo cáo Switch ' + cty : 'Tổng quan Switch';
        $id('bcTieuDe').textContent = ten;
        document.title = ten;
        var dsTen = Array.prototype.map.call($id('bcCongTy').options, function (o) { return o.value; })
            .filter(function (v) { return v; });
        $id('bcInTen').textContent = 'Tình trạng Switch ' + (cty || dsTen.join(' · '));
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

        fetch('/QLSwitch/TongQuan/DuLieu?' + thamSo(congTy()), { credentials: 'same-origin' })
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
        var kyTruoc = BaoCaoSoSanh.kyTruoc(bc);
        $id('bcKyIn').textContent = 'Kỳ ' + ngay(bc.tuNgay) + ' – ' + ngay(bc.denNgay) + ' (so với ' + kyTruoc + ') · lập lúc ' + gio(bc.taoLuc);
        $id('bcKyTruoc').textContent = '(' + kyTruoc + ')';

        var canhBao = $id('bcCanhBao');
        canhBao.textContent = BaoCaoSoSanh.canhBaoLichSu(bc, gio);
        canhBao.classList.toggle('d-none', !canhBao.textContent);

        veCards(bc.congTy);
        BaoCaoSoSanh.veBang($id('bcBangSoSanh'), bc.congTy, 'soSwitchBiMat');
        veTongHop(bc.congTy);
        veBoPhan(bc.boPhan || []);
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

    // % hoạt động hiện tại tính trên switch đã ping (bỏ switch chưa kiểm lần nào)
    function tyLeHienTai(c) {
        var daKiem = c.hoatDong + c.matKetNoi;
        return daKiem ? 100 * c.hoatDong / daKiem : null;
    }

    function veCards(ds) {
        var tong = { tongTaiSan: 0, tong: 0, hoatDong: 0, matKetNoi: 0, soLanMat: 0, giayMat: 0 };
        ds.forEach(function (c) {
            Object.keys(tong).forEach(function (k) { tong[k] += c[k]; });
        });

        var html = '';
        if (ds.length > 1) {
            html += '<div class="bc-card bc-card-tong">'
                + '<div class="bc-card-dau"><span class="bc-card-ten"><i class="fas fa-layer-group me-1"></i>Toàn bộ</span>'
                + '<span class="small text-muted">' + ds.length + ' công ty</span></div>'
                + veVong(tyLeHienTai(tong))
                + '<div class="bc-card-so">'
                + oSo('Tổng switch', so(tong.tongTaiSan)) + oSo('Hoạt động', so(tong.hoatDong), 'bc-xanh') + oSo('Mất KN', so(tong.matKetNoi), 'bc-do')
                + oSo('Lần mất KN (kỳ)', so(tong.soLanMat)) + oSo('Giờ mất (kỳ)', gioMat(tong.giayMat))
                + '</div></div>';
        }

        ds.forEach(function (c) {
            var laLink = moDuoc.indexOf(c.congTy) >= 0;
            var the = laLink ? 'a' : 'div';
            html += (laLink
                    ? '<a class="bc-card" href="/QLSwitch/' + encodeURIComponent(c.congTy) + '" title="Mở trang switch ' + esc(c.congTy) + '">'
                    : '<div class="bc-card">')
                + '<div class="bc-card-dau"><span class="bc-card-ten"><i class="fas fa-building me-1"></i>' + esc(c.congTy) + '</span>'
                + '<span class="small text-muted">' + (c.kiemTraLuc ? 'ping ' + esc(gio(c.kiemTraLuc)) : '') + '</span></div>';
            if (!c.tongTaiSan) {
                html += '<div class="text-muted small py-3">Chưa có switch nào.</div></' + the + '>';
                return;
            }
            html += veVong(tyLeHienTai(c))
                + '<div class="bc-card-so">'
                + oSo('Tổng switch', so(c.tongTaiSan)) + oSo('Hoạt động', so(c.hoatDong), 'bc-xanh') + oSo('Mất KN', so(c.matKetNoi), 'bc-do')
                + oSo('Chưa có IP', so(c.chuaKhaiIp), c.chuaKhaiIp ? 'bc-do' : '') + oSo('Hết BH', so(c.hetBaoHanh), c.hetBaoHanh ? 'bc-do' : '')
                + '</div>'
                + '<div class="bc-card-ky">Trong kỳ: <b>' + so(c.soLanMat) + '</b> lần mất KN · <b>' + so(c.soSwitchBiMat) + '</b> switch · '
                + '<b>' + gioMat(c.giayMat) + '</b> · hoạt động <b style="color:' + mauTyLe(c.tyLeTrongKy) + '">' + phanTram(c.tyLeTrongKy) + '</b>'
                + '<br>So kỳ trước: lần mất ' + chenh(c.soLanMat, c.soLanMatTruoc, true) + ' · hoạt động ' + chenh(c.tyLeTrongKy, c.tyLeTruoc, false, 'diem') + '</div>'
                + '</' + the + '>';
        });
        $id('bcCards').innerHTML = html;
    }

    function veTongHop(ds) {
        $id('bcBangTongHop').innerHTML = ds.map(function (c) {
            var t = tyLeHienTai(c);
            return '<tr><td class="fw-bold">' + esc(c.congTy) + '</td>'
                + '<td class="text-end">' + so(c.tongTaiSan) + '</td>'
                + '<td class="text-end">' + so(c.tong) + '</td>'
                + '<td class="text-end bc-xanh">' + so(c.hoatDong) + '</td>'
                + '<td class="text-end bc-do">' + so(c.matKetNoi) + '</td>'
                + '<td class="text-end">' + phanTram(t === null ? null : Math.round(t * 10) / 10) + '</td>'
                + '<td class="text-end">' + so(c.chuaKhaiIp) + '</td>'
                + '<td class="text-end">' + so(c.hetBaoHanh) + '</td>'
                + '<td class="text-end">' + so(c.soLanMat) + '</td>'
                + '<td class="text-end">' + so(c.soSwitchBiMat) + '</td>'
                + '<td class="text-end">' + gioMat(c.giayMat) + '</td>'
                + '<td class="text-end fw-bold" style="color:' + mauTyLe(c.tyLeTrongKy) + '">' + phanTram(c.tyLeTrongKy) + '</td></tr>';
        }).join('');
    }

    // Mỗi bộ phận một dòng, % hoạt động vẽ thành thanh ngang (in ra vẫn giữ màu)
    function veBoPhan(ds) {
        $id('bcBangBoPhan').innerHTML = ds.length ? ds.map(function (d) {
            var daKiem = d.hoatDong + d.matKetNoi;
            var t = daKiem ? 100 * d.hoatDong / daKiem : null;
            return '<tr><td>' + esc(d.congTy) + '</td>'
                + '<td>' + esc(d.boPhan) + '</td>'
                + '<td class="text-end">' + so(d.tong) + '</td>'
                + '<td class="text-end bc-xanh">' + so(d.hoatDong) + '</td>'
                + '<td class="text-end ' + (d.matKetNoi ? 'bc-do fw-bold' : '') + '">' + so(d.matKetNoi) + '</td>'
                + '<td><div class="bc-thanh"><div class="bc-thanh-day" style="width:' + (t || 0) + '%;background:' + mauTyLe(t) + '"></div>'
                + '<span class="bc-thanh-so">' + phanTram(t === null ? null : Math.round(t * 10) / 10) + '</span></div></td>'
                + '<td class="text-end">' + so(d.soLanMat) + '</td>'
                + '<td class="text-end">' + chenh(d.soLanMat, d.soLanMatTruoc, true) + '</td>'
                + '<td class="text-end">' + gioMat(d.giayMat) + '</td></tr>';
        }).join('') : '<tr><td colspan="9" class="text-center text-muted py-3">Chưa có switch nào đang theo dõi.</td></tr>';
    }

    function veDangMat(ds) {
        $id('bcDemDangMat').textContent = ds.length;
        var bayGio = Date.now();
        $id('bcBangDangMat').innerHTML = ds.length ? ds.map(function (a) {
            var matTu = a.matTu ? new Date(a.matTu).getTime() : null;
            return '<tr><td>' + esc(a.congTy) + '</td>'
                + '<td>' + esc(a.ten) + '</td>'
                + '<td class="font-monospace">' + esc(a.ip) + '</td>'
                + '<td>' + esc(a.boPhan) + '</td>'
                + '<td class="text-muted">' + esc(a.viTri) + '</td>'
                + '<td class="text-nowrap">' + (a.matTu ? esc(gio(a.matTu)) : '<span class="text-muted">Không rõ</span>') + '</td>'
                + '<td class="text-nowrap bc-do">' + (matTu ? esc(thoiLuong((bayGio - matTu) / 1000)) : '') + '</td></tr>';
        }).join('') : '<tr><td colspan="7" class="text-center text-muted py-3">Không có switch nào đang mất kết nối.</td></tr>';
    }

    function veMatNhieu(ds) {
        $id('bcBangMatNhieu').innerHTML = ds.length ? ds.map(function (a) {
            return '<tr><td>' + esc(a.congTy) + '</td>'
                + '<td>' + esc(a.ten) + (a.matTu ? ' <span class="badge bg-danger-subtle text-danger">đang mất</span>' : '') + '</td>'
                + '<td class="font-monospace">' + esc(a.ip) + '</td>'
                + '<td class="text-end fw-bold">' + so(a.soLan) + '</td>'
                + '<td class="text-end text-muted">' + so(a.soLanTruoc) + '</td>'
                + '<td class="text-end">' + gioMat(a.giayMat) + '</td></tr>';
        }).join('') : '<tr><td colspan="6" class="text-center text-muted py-3">Không có switch nào mất kết nối trong kỳ.</td></tr>';
    }

    // Gắn sự kiện lên khung báo cáo đang có trong DOM rồi tải số liệu. Trang "Báo cáo định kì" gọi lại mỗi lần
    // nạp tab (khung cũ bị thay nên listener cũ đi theo); tuyChon.giuKy = giữ kỳ/phạm vi đã chép từ tab trước.
    function khoiTao(tuyChon) {
        tuyChon = tuyChon || {};
        phien++;
        dangTai = false;
        if (tuyChon.urlTrang) urlTrang = tuyChon.urlTrang;
        if (!tuyChon.giuKy) datKy('7');
        swPhamVi();
        $id('bcCongTy').addEventListener('change', function () { swPhamVi(); tai(); });
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
            window.location.href = '/QLSwitch/TongQuan/XuatExcel?' + thamSo(muc.getAttribute('data-cong-ty') || '');
        });
        tai();
    }

    window.BaoCaoTongQuan = window.BaoCaoTongQuan || {};
    window.BaoCaoTongQuan.switch = khoiTao;

    document.addEventListener('DOMContentLoaded', function () {
        // Trên trang "Báo cáo định kì" để bao-cao-dinh-ky.js khởi tạo theo tab
        if (!document.getElementById('bcDinhKy')) khoiTao();
    });
})();
