// /BaoCaoDinhKy — gom các báo cáo định kì thành tab trên một trang. 3 kiểu tab (data-kieu):
//  - tong-quan: Camera / AP / Switch — nạp partial _TongQuanNoiDung qua AJAX vào #bcDinhKy rồi giao cho
//    camera|ap|switch-tong-quan.js khởi tạo; kỳ và phạm vi công ty đang chọn được chép sang tab mới.
//  - thiet-bi: số liệu /QLKiemKe/GetKkThietBis vẽ bằng kiemke-tong-quan.js (cùng tab Tổng quan của trang Quản lý Thiết bị).
//  - iframe: trang thống kê sẵn có mở với ?nhung=1 (script inline, trùng id giữa các trang nên phải tách tài liệu).
(function () {
    'use strict';

    var luotNap = 0;          // bấm tab liên tiếp: chỉ lượt cuối được vẽ
    var boLocTongQuan = null; // bộ lọc lần cuối của nhóm tab tong-quan (giữ khi đi qua tab khác rồi quay lại)
    var daNapThietBi = false;

    function $id(id) { return document.getElementById(id); }

    function urlTrang(loai) {
        return function (cty) {
            return '/BaoCaoDinhKy?loai=' + encodeURIComponent(loai) + (cty ? '&congTy=' + encodeURIComponent(cty) : '');
        };
    }

    function doiUrl(loai) {
        try { history.replaceState(null, '', urlTrang(loai)('')); } catch (e) { /* chỉ mất bookmark đúng tab */ }
    }

    // Lấy bộ lọc của tab tong-quan đang hiện (chưa có thì null)
    function layBoLoc() {
        if (!$id('bcKy')) return null;
        return { ky: $id('bcKy').value, tu: $id('bcTuNgay').value, den: $id('bcDenNgay').value, congTy: $id('bcCongTy').value };
    }

    function chonCongTy(cty) {
        var el = $id('bcCongTy');
        if (!el || !cty) return;
        var co = Array.prototype.some.call(el.options, function (o) { return o.value === cty; });
        if (co) el.value = cty;
    }

    function hienLoi(khung, noiDung) {
        var div = document.createElement('div');
        div.className = 'alert alert-danger m-3';
        div.textContent = noiDung;
        khung.replaceChildren(div);
    }

    function hienPane(id) {
        document.querySelectorAll('.bc-dinh-ky .bc-pane').forEach(function (p) {
            p.classList.toggle('d-none', p.id !== id);
        });
    }

    function napTongQuan(nut, loai, congTyBanDau) {
        var khung = $id('bcDinhKy');
        var boLoc = layBoLoc() || boLocTongQuan;
        var luot = ++luotNap;
        hienPane('bcDinhKy');
        khung.setAttribute('aria-busy', 'true');

        fetch(nut.getAttribute('data-url'), { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (res) {
                if (!res.ok) throw new Error(res.status === 403 ? 'Bạn không có quyền xem báo cáo này.' : 'HTTP ' + res.status);
                return res.text();
            })
            .then(function (html) {
                if (luot !== luotNap) return;
                khung.innerHTML = html;
                if (boLoc) {
                    $id('bcKy').value = boLoc.ky;
                    $id('bcTuNgay').value = boLoc.tu;
                    $id('bcDenNgay').value = boLoc.den;
                    chonCongTy(boLoc.congTy);
                } else {
                    chonCongTy(congTyBanDau);
                }
                var khoiTao = window.BaoCaoTongQuan && window.BaoCaoTongQuan[loai];
                if (!khoiTao) throw new Error('Thiếu script báo cáo ' + loai + '.');
                khoiTao({ giuKy: !!boLoc, urlTrang: urlTrang(loai) });
            })
            .catch(function (e) {
                if (luot === luotNap) hienLoi(khung, 'Không tải được báo cáo: ' + e.message);
            })
            .then(function () {
                if (luot === luotNap) khung.removeAttribute('aria-busy');
            });
    }

    function napThietBi(nut) {
        hienPane('bcPaneThietBi');
        if (daNapThietBi) return;
        daNapThietBi = true;
        var goc = $id('tqTongQuan');

        fetch(nut.getAttribute('data-url'), { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (res) {
                if (!res || !res.success) throw new Error((res && res.message) || 'máy chủ báo lỗi');
                if (!window.TongQuanKiemKe) throw new Error('thiếu script kiemke-tong-quan.js');
                window.TongQuanKiemKe.render(res.data || []);
            })
            .catch(function (e) {
                daNapThietBi = false;   // lỗi thì lần mở tab sau thử lại
                hienLoi(goc, 'Không tải được số liệu thiết bị: ' + e.message);
            });
    }

    // Iframe cùng origin nên đọc được chiều cao nội dung; trang con vẽ biểu đồ/đổi bộ lọc thì khung tự giãn theo
    function canChieuCao(khung) {
        var doc;
        try { doc = khung.contentDocument; } catch (e) { return; }
        if (!doc || !doc.body) return;
        // Đo body chứ không đo documentElement: html luôn cao bằng khung nên nội dung co lại thì khung không co theo
        var dat = function () {
            var cao = Math.ceil(doc.body.getBoundingClientRect().height);
            if (cao > 0) khung.style.height = cao + 'px';
        };
        dat();
        if (window.ResizeObserver) new ResizeObserver(dat).observe(doc.body);
    }

    function napIframe(nut, loai) {
        var pane = $id('bcPaneIframe');
        hienPane('bcPaneIframe');
        var khung = null;
        Array.prototype.forEach.call(pane.children, function (f) {
            var laTab = f.getAttribute('data-loai') === loai;
            f.classList.toggle('d-none', !laTab);
            if (laTab) khung = f;
        });
        if (khung) return;

        khung = document.createElement('iframe');
        khung.className = 'bc-iframe';
        khung.setAttribute('data-loai', loai);
        khung.title = nut.getAttribute('data-ten') || loai;
        khung.addEventListener('load', function () { canChieuCao(khung); });
        khung.src = nut.getAttribute('data-url');
        pane.appendChild(khung);
    }

    // ---------- "So với kỳ trước" của tab Thiết bị / Đơn IT / Công việc (/BaoCaoDinhKy/SoSanh) ----------
    var loaiSoSanh = null;  // tab đang hiện khối so sánh
    var luotSs = 0;         // đổi tab/kỳ liên tiếp: chỉ lượt cuối được vẽ
    var daDatKySs = false;

    function hienLoiSs(noiDung) {
        var el = $id('bcSsLoi');
        el.textContent = noiDung || '';
        el.classList.toggle('d-none', !noiDung);
    }

    function datKySs(loai) {
        var ky = BaoCaoSoSanh.kyTheoLoai(loai);
        if (!ky) return;   // tuỳ chọn: giữ ngày đang nhập
        $id('bcSsTuNgay').value = ngayInput(ky.tu);
        $id('bcSsDenNgay').value = ngayInput(ky.den);
    }

    function napSoSanh(loai) {
        loaiSoSanh = loai;
        if (!daDatKySs) {
            daDatKySs = true;
            // Lần đầu: theo kỳ đã chọn ở Camera/AP/Switch nếu có, không thì 7 ngày (cùng mặc định các tab kia)
            var boLoc = layBoLoc() || boLocTongQuan;
            if (boLoc && boLoc.tu && boLoc.den) {
                $id('bcSsKy').value = boLoc.ky;
                $id('bcSsTuNgay').value = boLoc.tu;
                $id('bcSsDenNgay').value = boLoc.den;
            } else {
                $id('bcSsKy').value = '7';
                datKySs('7');
            }
        }

        var luot = ++luotSs;
        var nut = $id('bcSsXem');
        nut.disabled = true;
        hienLoiSs('');
        var url = '/BaoCaoDinhKy/SoSanh?loai=' + encodeURIComponent(loai)
            + '&tuNgay=' + encodeURIComponent($id('bcSsTuNgay').value) + '&denNgay=' + encodeURIComponent($id('bcSsDenNgay').value);

        fetch(url, { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (res) {
                if (luot !== luotSs) return;
                if (!res.thanhCong) { hienLoiSs(res.thongBao || 'Không tải được số liệu so sánh.'); return; }
                veSoSanhKhac(loai, res.duLieu);
            })
            .catch(function (e) { if (luot === luotSs) hienLoiSs('Lỗi kết nối máy chủ (' + e.message + ').'); })
            .then(function () { if (luot === luotSs) nut.disabled = false; });
    }

    var esc = function (v) { return BaoCaoSoSanh.esc(v); };
    var so = function (v) { return BaoCaoSoSanh.so(v); };
    var chenh = function (nay, truoc, tangLaXau, kieu) { return BaoCaoSoSanh.chenh(nay, truoc, tangLaXau, kieu); };
    function gioTuPhut(phut) {
        return phut === null || phut === undefined ? '–' : (phut / 60).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' giờ';
    }

    // Một dòng: tên | kỳ này | kỳ trước | thay đổi (tangLaXau: true/false/null = trung tính)
    function dongSs(ten, nay, truoc, tangLaXau, laTong) {
        return '<tr' + (laTong ? ' class="table-light fw-bold"' : '') + '><td>' + esc(ten) + '</td>'
            + '<td class="text-end fw-bold">' + so(nay) + '</td><td class="text-end text-muted">' + so(truoc) + '</td>'
            + '<td class="text-end">' + chenh(nay, truoc, tangLaXau) + '</td></tr>';
    }

    function khungBang(tieuDe, cotTen, than) {
        return '<div class="fw-bold small text-muted mb-1">' + esc(tieuDe) + '</div>'
            + '<div class="table-responsive bc-cuon"><table class="table table-sm table-bordered align-middle mb-0 small bc-bang">'
            + '<thead><tr><th>' + esc(cotTen) + '</th><th class="text-end">Kỳ này</th><th class="text-end">Kỳ trước</th><th class="text-end">Thay đổi</th></tr></thead>'
            + '<tbody>' + (than || '<tr><td colspan="4" class="text-center text-muted py-3">Không có dữ liệu.</td></tr>') + '</tbody></table></div>';
    }

    // Ghép danh sách {ten, soLuong} của 2 kỳ theo tên (tên chỉ có ở kỳ trước vẫn giữ), xếp theo kỳ này giảm dần
    function ghepTheoTen(nay, truoc, gioiHan) {
        var ds = {};
        (nay || []).forEach(function (x) { ds[x.ten] = { ten: x.ten, nay: x.soLuong, truoc: 0 }; });
        (truoc || []).forEach(function (x) { (ds[x.ten] = ds[x.ten] || { ten: x.ten, nay: 0, truoc: 0 }).truoc = x.soLuong; });
        return Object.keys(ds).map(function (k) { return ds[k]; })
            .sort(function (a, b) { return b.nay - a.nay || b.truoc - a.truoc; })
            .slice(0, gioiHan);
    }

    // Tăng là xấu với trạng thái tồn đọng/hủy, là tốt với hoàn tất; còn lại trung tính
    var XAU_KHI_TANG = { 'CHỜ QL': true, 'HOÀN TẤT': false, 'HỦY': true };

    function veDon(d) {
        var nay = d.nay, truoc = d.truoc;
        var demTruoc = {};
        truoc.theoTrangThai.forEach(function (x) { demTruoc[x.ten] = x.soLuong; });
        var trangThai = dongSs('Tổng đơn', nay.tong, truoc.tong, null, true)
            + nay.theoTrangThai.map(function (x) {
                return dongSs(x.ten, x.soLuong, demTruoc[x.ten] || 0, XAU_KHI_TANG.hasOwnProperty(x.ten) ? XAU_KHI_TANG[x.ten] : null);
            }).join('')
            + (nay.quaHan !== null && nay.quaHan !== undefined ? dongSs('Đang quá hạn', nay.quaHan, truoc.quaHan, true) : '');

        var html = '<div class="row g-3">'
            + '<div class="col-xl-4">' + khungBang('Theo trạng thái', 'Trạng thái', trangThai) + '</div>'
            + '<div class="col-xl-4">' + khungBang('Theo danh mục (top 10)', 'Danh mục',
                ghepTheoTen(nay.theoDanhMuc, truoc.theoDanhMuc, 10).map(function (x) { return dongSs(x.ten, x.nay, x.truoc, null); }).join('')) + '</div>'
            + '<div class="col-xl-4">' + khungBang('Theo bộ phận yêu cầu (top 10)', 'Bộ phận',
                ghepTheoTen(nay.theoBoPhan, truoc.theoBoPhan, 10).map(function (x) { return dongSs(x.ten, x.nay, x.truoc, null); }).join('')) + '</div>'
            + '</div>';

        // Người hỗ trợ: số đơn + thời gian xử lý trung bình (tăng là chậm hơn → xấu)
        var nguoi = {};
        nay.theoNguoiHoTro.forEach(function (x) { nguoi[x.ten] = { ten: x.ten, nay: x, truoc: null }; });
        truoc.theoNguoiHoTro.forEach(function (x) { (nguoi[x.ten] = nguoi[x.ten] || { ten: x.ten, nay: null, truoc: null }).truoc = x; });
        var dsNguoi = Object.keys(nguoi).map(function (k) { return nguoi[k]; })
            .sort(function (a, b) { return ((b.nay && b.nay.tong) || 0) - ((a.nay && a.nay.tong) || 0); });
        html += '<div class="fw-bold small text-muted mb-1 mt-3">Theo người hỗ trợ (người được gán sau cùng)</div>'
            + '<div class="table-responsive bc-cuon"><table class="table table-sm table-bordered align-middle mb-0 small bc-bang"><thead>'
            + '<tr><th rowspan="2">Người hỗ trợ</th><th colspan="3" class="text-center">Số đơn</th><th colspan="3" class="text-center">TB xử lý</th></tr>'
            + '<tr><th class="text-end">Kỳ này</th><th class="text-end">Kỳ trước</th><th class="text-end">Thay đổi</th>'
            + '<th class="text-end">Kỳ này</th><th class="text-end">Kỳ trước</th><th class="text-end">Thay đổi</th></tr></thead><tbody>'
            + (dsNguoi.length ? dsNguoi.map(function (n) {
                var a = n.nay ? n.nay.tong : 0, b = n.truoc ? n.truoc.tong : 0;
                var pa = n.nay ? n.nay.phutTrungBinh : null, pb = n.truoc ? n.truoc.phutTrungBinh : null;
                return '<tr><td>' + esc(n.ten) + '</td>'
                    + '<td class="text-end fw-bold">' + so(a) + '</td><td class="text-end text-muted">' + so(b) + '</td>'
                    + '<td class="text-end">' + chenh(a, b, null) + '</td>'
                    + '<td class="text-end">' + gioTuPhut(pa) + '</td><td class="text-end text-muted">' + gioTuPhut(pb) + '</td>'
                    + '<td class="text-end">' + chenh(pa === null ? null : pa * 60, pb === null ? null : pb * 60, true, 'gio') + '</td></tr>';
            }).join('') : '<tr><td colspan="7" class="text-center text-muted py-3">Không có dữ liệu.</td></tr>')
            + '</tbody></table></div>'
            + '<div class="small text-muted mt-2">Tính các đơn có thời điểm quản lý duyệt trong kỳ. Kỳ này chưa hết thì kỳ trước cũng chỉ tính tới cùng mốc. '
            + 'Trạng thái, quá hạn của cả 2 kỳ là trạng thái hiện tại của các đơn đó.</div>';
        return html;
    }

    // ---- Bản quyền Windows / Office / Cần cài Office / trạng thái: từ bản chụp hằng ngày (KK_ThietBiChotNgay) ----
    var banQuyenGanNhat = null;   // giữ để đổi ô công ty thì vẽ lại, không gọi lại máy chủ
    var CHI_TIEU = [
        ['win', 'Bản quyền Windows (máy tính)'], ['office', 'Bản quyền Office (máy tính)'],
        ['can_cai_office', 'Cần cài Office (máy tính)'], ['trang_thai', 'Trạng thái thiết bị']
    ];
    var THU_TU_GIA_TRI = ['Có bản quyền', 'Chưa có bản quyền', 'Không xác định', 'Trống / Chưa rõ', 'Cần cài', 'Không cần',
        'Chưa trả lời', 'Hoạt động', 'Hỏng', 'Bảo trì', 'Kho IT', 'Khác'];
    // true = tăng là xấu, false = tăng là tốt, không có = trung tính
    var XAU_KHI_TANG_TB = {
        'Có bản quyền': false, 'Chưa có bản quyền': true, 'Không xác định': true, 'Trống / Chưa rõ': true,
        'Chưa trả lời': true, 'Hoạt động': false, 'Hỏng': true
    };

    function veBanQuyen() {
        var bq = banQuyenGanNhat;
        var khung = $id('bcSsBanQuyen');
        if (!khung || !bq) return;
        var cty = ($id('bcSsBqCongTy') || {}).value || '';
        var dem = function (ds, chiTieu, giaTri) {
            if (!ds) return null;
            return ds.filter(function (x) { return x.chiTieu === chiTieu && x.giaTri === giaTri && (!cty || x.congTy === cty); })
                .reduce(function (t, x) { return t + x.soLuong; }, 0);
        };
        var tatCa = bq.nay.concat(bq.truoc || []);
        khung.innerHTML = '<div class="row g-3">' + CHI_TIEU.map(function (ct) {
            var dsGiaTri = tatCa.filter(function (x) { return x.chiTieu === ct[0]; }).map(function (x) { return x.giaTri; })
                .filter(function (v, i, a) { return a.indexOf(v) === i; })
                .sort(function (a, b) {
                    var ia = THU_TU_GIA_TRI.indexOf(a), ib = THU_TU_GIA_TRI.indexOf(b);
                    return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib) || a.localeCompare(b);
                });
            return '<div class="col-xl-3 col-md-6">' + khungBang(ct[1], 'Giá trị', dsGiaTri.map(function (gt) {
                return dongSs(gt, dem(bq.nay, ct[0], gt), dem(bq.truoc, ct[0], gt),
                    XAU_KHI_TANG_TB.hasOwnProperty(gt) ? XAU_KHI_TANG_TB[gt] : null);
            }).join('')) + '</div>';
        }).join('') + '</div>';
    }

    function khungBanQuyen(d) {
        banQuyenGanNhat = d.banQuyen;
        var html = '<div class="fw-bold small text-muted mb-1 mt-3"><i class="fas fa-key me-1"></i> Bản quyền · Office · Trạng thái</div>';
        if (d.banQuyenLoi) return html + '<div class="alert alert-warning py-2 small mb-0">' + esc(d.banQuyenLoi) + '</div>';
        var bq = d.banQuyen;
        if (!bq) return html;

        var dsCongTy = bq.nay.concat(bq.truoc || []).map(function (x) { return x.congTy; })
            .filter(function (v, i, a) { return a.indexOf(v) === i; }).sort();
        var nguonNay = bq.nayLaHienTai ? 'số hiện tại' : bq.ngayNay ? 'bản chụp ' + BaoCaoSoSanh.ngay(bq.ngayNay) : 'không có bản chụp trong kỳ';
        var nguonTruoc = bq.ngayTruoc ? 'bản chụp ' + BaoCaoSoSanh.ngay(bq.ngayTruoc)
            : bq.chupTu ? 'chưa có bản chụp trong kỳ trước (bắt đầu chụp từ ' + BaoCaoSoSanh.ngay(bq.chupTu) + ')'
            : 'chưa có bản chụp nào — job chụp mỗi ngày, có số kỳ trước sau khi đủ 1 kỳ';
        html += '<div class="d-flex flex-wrap align-items-center gap-2 mb-2">'
            + '<select id="bcSsBqCongTy" class="form-select form-select-sm w-auto" title="Công ty"><option value="">Toàn bộ</option>'
            + dsCongTy.map(function (c) { return '<option value="' + esc(c) + '">' + esc(c) + '</option>'; }).join('') + '</select>'
            + '<span class="small text-muted">Kỳ này: ' + esc(nguonNay) + ' · Kỳ trước: ' + esc(nguonTruoc) + '</span></div>'
            + (bq.ngayTruoc ? '' : '<div class="alert alert-info py-2 small">Chưa có số kỳ trước để so sánh — '
                + 'hệ thống mới bắt đầu chụp số liệu bản quyền/Office mỗi ngày.</div>')
            + '<div id="bcSsBanQuyen"></div>';
        return html;
    }

    function veThietBi(d) {
        var truoc = {};
        d.truoc.forEach(function (x) { truoc[x.congTy] = x; });
        var dong = d.nay.map(function (x) { return { ten: x.congTy, nay: x, truoc: truoc[x.congTy] || { themMoi: 0, daXoa: 0, daKiem: 0 } }; });
        if (dong.length > 1) {
            var cong = function (ds) {
                return ds.reduce(function (t, x) { t.themMoi += x.themMoi; t.daXoa += x.daXoa; t.daKiem += x.daKiem; return t; }, { themMoi: 0, daXoa: 0, daKiem: 0 });
            };
            dong.push({ ten: 'Toàn bộ', laTong: true, nay: cong(d.nay), truoc: cong(d.truoc) });
        }
        var o = function (a, b, tangLaXau) {
            return '<td class="text-end fw-bold">' + so(a) + '</td><td class="text-end text-muted">' + so(b) + '</td><td class="text-end">' + chenh(a, b, tangLaXau) + '</td>';
        };
        return khungBanQuyen(d)
            + '<div class="fw-bold small text-muted mb-1 mt-3"><i class="fas fa-arrows-rotate me-1"></i> Biến động trong kỳ</div>'
            + '<div class="table-responsive"><table class="table table-sm table-bordered align-middle mb-0 small bc-bang"><thead>'
            + '<tr><th rowspan="2">Công ty</th><th colspan="3" class="text-center">Thêm mới</th><th colspan="3" class="text-center">Xoá</th>'
            + '<th colspan="3" class="text-center">Kiểm kê</th></tr><tr>'
            + new Array(4).join('<th class="text-end">Kỳ này</th><th class="text-end">Kỳ trước</th><th class="text-end">Thay đổi</th>')
            + '</tr></thead><tbody>'
            + (dong.length ? dong.map(function (x) {
                return '<tr' + (x.laTong ? ' class="table-light fw-bold"' : '') + '><td>' + esc(x.ten) + '</td>'
                    + o(x.nay.themMoi, x.truoc.themMoi, null) + o(x.nay.daXoa, x.truoc.daXoa, null) + o(x.nay.daKiem, x.truoc.daKiem, false) + '</tr>';
            }).join('') : '<tr><td colspan="10" class="text-center text-muted py-3">Không có thiết bị nào thêm, xoá hay kiểm kê trong 2 kỳ.</td></tr>')
            + '</tbody></table></div>'
            + '<div class="small text-muted mt-2">Chỉ so được các số có mốc thời gian (ngày tạo, ngày xoá, lần kiểm kê). '
            + 'Kiểm kê tính theo lần kiểm gần nhất của mỗi máy — máy kiểm lại ở kỳ này không còn tính cho kỳ trước. '
            + 'Bản quyền/Office/trạng thái so theo bản chụp số liệu mỗi ngày.</div>';
    }

    function veSoSanhKhac(loai, d) {
        $id('bcSsKyTruoc').textContent = '(' + BaoCaoSoSanh.ngay(d.tuNgay) + ' – ' + BaoCaoSoSanh.ngay(d.denNgay)
            + ' so với ' + BaoCaoSoSanh.ngay(d.kyTruocTu) + ' – ' + BaoCaoSoSanh.ngay(d.kyTruocDen) + ')';
        $id('bcSsNoiDung').innerHTML = loai === 'thiet-bi' ? veThietBi(d) : veDon(d);
        if (loai === 'thiet-bi') veBanQuyen();
    }

    function moTab(nut, congTyBanDau) {
        var loai = nut.getAttribute('data-loai');
        var kieu = nut.getAttribute('data-kieu');

        document.querySelectorAll('.bc-tab').forEach(function (t) {
            var dangChon = t === nut;
            t.classList.toggle('active', dangChon);
            t.setAttribute('aria-selected', dangChon ? 'true' : 'false');
        });

        // Rời nhóm tong-quan: nhớ bộ lọc để lúc quay lại vẫn đúng kỳ/phạm vi
        if (kieu !== 'tong-quan') {
            boLocTongQuan = layBoLoc() || boLocTongQuan;
            ++luotNap;          // bỏ lượt nạp tong-quan còn dở
            $id('bcDinhKy').removeAttribute('aria-busy');
            doiUrl(loai);
        }

        // Khối so sánh kỳ trước chỉ cho 3 tab không phải Camera/AP/Switch (3 tab đó đã có sẵn trong nội dung)
        $id('bcSoSanhKhac').classList.toggle('d-none', kieu === 'tong-quan');
        if (kieu !== 'tong-quan') {
            ++luotSs;   // bỏ lượt so sánh của tab trước còn dở
            $id('bcSsNoiDung').innerHTML = '<div class="text-center text-muted py-3">Đang tải...</div>';
            napSoSanh(loai);
        }

        if (kieu === 'tong-quan') napTongQuan(nut, loai, congTyBanDau);
        else if (kieu === 'thiet-bi') napThietBi(nut);
        else napIframe(nut, loai);
    }

    document.addEventListener('change', function (e) {
        if (e.target.id === 'bcSsBqCongTy') {
            veBanQuyen();
        } else if (e.target.id === 'bcSsKy') {
            datKySs(e.target.value);
            if (e.target.value !== 'tuy-chon' && loaiSoSanh) napSoSanh(loaiSoSanh);
        } else if (e.target.id === 'bcSsTuNgay' || e.target.id === 'bcSsDenNgay') {
            $id('bcSsKy').value = 'tuy-chon';
        }
    });

    // yyyy-MM-dd theo giờ máy (toISOString lệch ngày do UTC)
    function ngayInput(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }

    // Excel tổng hợp: kỳ + phạm vi theo bộ lọc Camera/AP/Switch đang chọn (hoặc lần chọn cuối), chưa có thì 7 ngày gần nhất.
    // Tải file là điều hướng hợp lệ (trang giữ nguyên); file lớn mất vài giây nên khoá nút một lúc tránh bấm lặp.
    function xuatTongHop(nut) {
        var boLoc = layBoLoc() || boLocTongQuan;
        var den = new Date(), tu = new Date();
        tu.setDate(tu.getDate() - 6);
        var tuNgay = (boLoc && boLoc.tu) || ngayInput(tu);
        var denNgay = (boLoc && boLoc.den) || ngayInput(den);
        var cty = (boLoc && boLoc.congTy) || '';

        nut.disabled = true;
        var cu = nut.innerHTML;
        nut.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Đang tạo file...';
        setTimeout(function () { nut.disabled = false; nut.innerHTML = cu; }, 8000);

        window.location.href = '/BaoCaoDinhKy/XuatExcel?tuNgay=' + encodeURIComponent(tuNgay) + '&denNgay=' + encodeURIComponent(denNgay)
            + (cty ? '&congTy=' + encodeURIComponent(cty) : '');
    }

    document.addEventListener('click', function (e) {
        var xuat = e.target.closest('#bcXuatTongHop');
        if (xuat) { e.preventDefault(); xuatTongHop(xuat); return; }
        if (e.target.closest('#bcSsXem')) { e.preventDefault(); if (loaiSoSanh) napSoSanh(loaiSoSanh); return; }

        var nut = e.target.closest('.bc-tab');
        if (!nut || nut.classList.contains('active')) return;
        e.preventDefault();
        moTab(nut);
    });

    document.addEventListener('DOMContentLoaded', function () {
        var khung = $id('bcDinhKy');
        if (!khung) return;
        var nut = document.querySelector('.bc-tab[data-loai="' + khung.getAttribute('data-loai') + '"]')
            || document.querySelector('.bc-tab');
        moTab(nut, khung.getAttribute('data-cong-ty'));
    });
})();
