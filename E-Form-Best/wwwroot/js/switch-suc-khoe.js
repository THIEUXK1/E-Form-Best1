// Tab "Thông tin thiết bị" trang /QLSwitch/{công ty}: sức khoẻ phần cứng switch lõi / WLC (CPU, RAM, nhiệt độ,
// quạt, nguồn, stack, cổng) do SwitchSucKhoeWorker đọc SNMP và lưu KK_SwitchSucKhoe. View chỉ dựng khung.
(function () {
    'use strict';

    var goc, chiXem, daTai = false, timer = null, dangTai = false;

    var NHAN = { OK: 'Tốt', CANH_BAO: 'Cảnh báo', LOI: 'Lỗi', KHONG_DOC_DUOC: 'Không đọc được', CHUA_DOC: 'Chưa đọc', KHONG_RO: 'Không rõ' };
    var LOP = { OK: 'sv-sk-ok', CANH_BAO: 'sv-sk-canh-bao', LOI: 'sv-sk-loi' };
    var THU_TU_NHOM = ['Stack', 'CPU', 'Bộ nhớ', 'Nguồn', 'Quạt', 'Nhiệt độ'];
    var ICON_NHOM = { 'Stack': 'fa-layer-group', 'CPU': 'fa-microchip', 'Bộ nhớ': 'fa-memory', 'Nguồn': 'fa-plug', 'Quạt': 'fa-fan', 'Nhiệt độ': 'fa-temperature-half' };
    // Nhóm nhiều dòng (4500 có ~40 cảm biến nhiệt) chỉ hiện chừng này dòng, còn lại bấm "xem thêm"
    var TOI_DA_DONG = 6;

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function lop(tt) { return LOP[tt] || 'sv-sk-xam'; }

    // "2026-11-10T09:21:07" -> "09:21 10/11/2026"
    function gio(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(s || '');
        return m ? m[4] + ':' + m[5] + ' ' + m[3] + '/' + m[2] + '/' + m[1] : '';
    }

    function thoiGianChay(giay) {
        if (!giay && giay !== 0) return '';
        var ngay = Math.floor(giay / 86400), h = Math.floor((giay % 86400) / 3600), p = Math.floor((giay % 3600) / 60);
        return ngay > 0 ? ngay + ' ngày ' + h + ' giờ' : h > 0 ? h + ' giờ ' + p + ' phút' : p + ' phút';
    }

    function so(n) { return (n || 0).toLocaleString('vi-VN'); }

    function thanh(pt, tt) {
        if (pt === null || pt === undefined) return '';
        var l = tt === 'LOI' ? ' sv-thanh-day' : tt === 'CANH_BAO' ? ' sv-thanh-vua' : '';
        return '<div class="sv-thanh"><div class="sv-thanh-dung' + l + '" style="width:' + Math.max(0, Math.min(100, pt)) + '%"></div></div>';
    }

    function docJson(s) {
        if (!s) return null;
        try { return JSON.parse(s); } catch (e) { return null; }
    }

    // ---------- Ô số liệu tóm tắt đầu thẻ ----------
    function oChiSo(nhan, giaTri, phu, tt, pt) {
        return '<div class="sk-o"><div class="sk-o-nhan">' + esc(nhan) + '</div>' +
            '<div class="sk-o-so ' + (tt ? 'sk-so-' + lop(tt) : '') + '">' + giaTri + '</div>' +
            (phu ? '<div class="sk-o-phu">' + phu + '</div>' : '') + thanh(pt, tt) + '</div>';
    }

    function nangNhat(ds) {
        // Mục có % cao nhất (CPU / RAM / nhiệt độ so ngưỡng) để đưa lên ô tóm tắt
        var m = null;
        ds.forEach(function (x) { if (x.phanTram !== null && x.phanTram !== undefined && (!m || x.phanTram > m.phanTram)) m = x; });
        return m;
    }

    function tomTatO(dl) {
        var ds = dl.dsMuc || [];
        var cpu = nangNhat(ds.filter(function (x) { return x.nhom === 'CPU'; }));
        var ram = nangNhat(ds.filter(function (x) { return x.nhom === 'Bộ nhớ'; }));
        var nd = ds.filter(function (x) { return x.nhom === 'Nhiệt độ'; });
        // Cảm biến sát ngưỡng nhất (chip ASIC 79°C/120 an toàn hơn khí vào 50°C/56); không có ngưỡng thì theo số độ
        var ndMax = null;
        nd.forEach(function (x) {
            var v = parseInt(x.giaTri, 10), k = x.phanTram !== null && x.phanTram !== undefined ? x.phanTram : v;
            if (!isNaN(v) && (!ndMax || k > ndMax.k)) ndMax = { v: v, k: k, x: x };
        });
        var html = '';
        if (cpu) html += oChiSo('CPU (5 phút)', cpu.phanTram + '%', esc(cpu.ten), cpu.trangThai, cpu.phanTram);
        if (ram) html += oChiSo('RAM', ram.phanTram + '%', esc(ram.ten), ram.trangThai, ram.phanTram);
        if (ndMax) {
            var ttNd = nd.some(function (x) { return x.trangThai === 'LOI'; }) ? 'LOI'
                : nd.some(function (x) { return x.trangThai === 'CANH_BAO'; }) ? 'CANH_BAO' : 'OK';
            html += oChiSo('Nhiệt độ sát ngưỡng nhất', ndMax.v + '°C', esc(ndMax.x.ten + (ndMax.x.phanTram ? ' · ' + ndMax.x.phanTram + '% ngưỡng' : '')), ttNd, ndMax.x.phanTram);
        }
        var c = dl.cong || {};
        if (c.tong) {
            html += oChiSo('Cổng up', so(c.up) + '<span class="sk-o-tong">/' + so(c.tong) + '</span>',
                'Down ' + so(c.down) + ' · Tắt ' + so(c.tat), null, null);
        }
        if (dl.soAp !== null && dl.soAp !== undefined) html += oChiSo('Access point', so(dl.soAp), 'đang join', null, null);
        if (dl.soClient !== null && dl.soClient !== undefined) html += oChiSo('Client Wi-Fi', so(dl.soClient), 'đang kết nối', null, null);
        return html ? '<div class="sk-o-hang">' + html + '</div>' : '';
    }

    // ---------- Nhóm mục (CPU, RAM, nguồn, quạt, nhiệt độ, stack) ----------
    function khoiNhom(ten, ds) {
        if (!ds.length) return '';
        // Mục bất thường lên đầu để không bị giấu sau "xem thêm"
        var hang = { LOI: 0, CANH_BAO: 1, OK: 2 };
        var sx = ds.slice().sort(function (a, b) { return (hang[a.trangThai] ?? 3) - (hang[b.trangThai] ?? 3); });
        var soXau = ds.filter(function (x) { return x.trangThai === 'LOI' || x.trangThai === 'CANH_BAO'; }).length;
        var soTot = ds.filter(function (x) { return x.trangThai === 'OK'; }).length;
        var dem = soXau > 0 ? '<span class="sv-sk sv-sk-' + (ds.some(function (x) { return x.trangThai === 'LOI'; }) ? 'loi' : 'canh-bao') + '">' + soXau + ' bất thường</span>'
            : '<span class="small text-muted fw-normal">' + soTot + '/' + ds.length + ' tốt</span>';

        var dong = sx.map(function (x, i) {
            return '<div class="sv-sk-dong' + (i >= TOI_DA_DONG ? ' sk-an d-none' : '') + '">' +
                '<span class="sv-sk-cham ' + lop(x.trangThai) + '" title="' + esc(NHAN[x.trangThai] || x.trangThai) + '"></span>' +
                '<span class="sv-sk-ten" title="' + esc(x.ten) + '">' + esc(x.ten) + '</span>' +
                '<span class="sv-sk-gt">' + esc(x.giaTri) + '</span></div>';
        }).join('');
        if (sx.length > TOI_DA_DONG)
            dong += '<button type="button" class="btn btn-link btn-sm p-0 sk-xem-them">Xem thêm ' + (sx.length - TOI_DA_DONG) + ' dòng</button>';

        return '<div class="col-md-6 col-xxl-4"><div class="sv-sk-nhom">' +
            '<div class="sv-sk-nhom-dau"><span><i class="fas ' + (ICON_NHOM[ten] || 'fa-circle') + ' me-1 text-muted"></i>' + esc(ten) + '</span>' + dem + '</div>' +
            dong + '</div></div>';
    }

    function bangCongLoi(ds) {
        if (!ds || !ds.length) return '';
        var dangTang = ds.filter(function (x) { return (x.tangVao || 0) + (x.tangRa || 0) > 0; }).length;
        var dong = ds.slice(0, 15).map(function (x) {
            var tang = (x.tangVao || 0) + (x.tangRa || 0);
            var tangChu = x.tangVao === null || x.tangVao === undefined ? '<span class="text-muted">—</span>'
                : tang > 0 ? '<span class="text-danger fw-bold">+' + so(x.tangVao) + ' / +' + so(x.tangRa) + '</span>' : '<span class="text-muted">0</span>';
            return '<tr' + (tang > 0 ? ' class="table-warning"' : '') + '><td class="fw-semibold">' + esc(x.ten) + '</td>' +
                '<td class="text-truncate" style="max-width:220px;" title="' + esc(x.moTa) + '">' + esc(x.moTa) + '</td>' +
                '<td class="text-end">' + so(x.loiVao) + '</td><td class="text-end">' + so(x.loiRa) + '</td><td class="text-end">' + tangChu + '</td></tr>';
        }).join('');
        return '<details class="sk-cong-loi mt-2"' + (dangTang ? ' open' : '') + '><summary class="small fw-bold">' +
            '<i class="fas fa-triangle-exclamation me-1 text-warning"></i>Cổng có bộ đếm lỗi: ' + ds.length +
            (dangTang ? ' · <span class="text-danger">' + dangTang + ' cổng lỗi đang tăng</span>' : ' · không cổng nào tăng so với lần đọc trước') +
            '</summary><div class="table-responsive"><table class="table table-sm mb-0 small"><thead><tr>' +
            '<th>Cổng</th><th>Mô tả</th><th class="text-end">Lỗi vào</th><th class="text-end">Lỗi ra</th><th class="text-end">Tăng (vào / ra)</th>' +
            '</tr></thead><tbody>' + dong + '</tbody></table></div>' +
            '<div class="small text-muted">Bộ đếm cộng dồn từ lúc khởi động / clear counters; "Tăng" so với lần đọc trước.' +
            (ds.length > 15 ? ' Hiện 15/' + ds.length + ' cổng.' : '') + '</div></details>';
    }

    function theThietBi(x) {
        var dl = docJson(x.duLieu);
        var tt = x.trangThai;
        var dsMuc = (dl && dl.dsMuc) || [];
        var tieuDe = esc(x.tenHienThi || x.tenSwitch);
        var phu = [x.tenHienThi ? x.tenSwitch : null, x.diaChiIp, [x.hangSanXuat, x.model].filter(Boolean).join(' '), x.phienBan ? 'FW ' + x.phienBan : null]
            .filter(Boolean).map(esc).join(' · ');

        var trenCung = '<div class="d-flex flex-wrap align-items-start gap-2 mb-2">' +
            '<div class="me-auto" style="min-width:0;"><div class="sk-ten">' + tieuDe +
            ' <span class="sv-sk sv-sk-lon ' + lop(tt) + ' ms-1">' + esc(NHAN[tt] || tt) + '</span>' +
            (x.ketNoi === 'DOWN' ? ' <span class="sv-sk sv-sk-loi ms-1" title="Theo job ping">Mất ping</span>' : '') + '</div>' +
            '<div class="small text-muted text-truncate" title="' + phu + '">' + phu + '</div>' +
            '<div class="small text-muted">' +
            (dl && dl.thoiGianChay ? '<i class="fas fa-power-off me-1"></i>Chạy liên tục ' + thoiGianChay(dl.thoiGianChay) + ' · ' : '') +
            (x.docDuocLuc ? 'Đọc lúc ' + gio(x.docDuocLuc) : 'Chưa đọc được lần nào') +
            (x.kiemTraLuc && x.kiemTraLuc !== x.docDuocLuc && x.docDuocLuc ? ' · <span class="text-danger">lần thử ' + gio(x.kiemTraLuc) + ' lỗi</span>' : '') +
            '</div></div>' +
            '<div class="d-flex gap-1">' +
            '<button type="button" class="btn btn-sm btn-outline-primary sk-doc-ngay" data-id="' + x.idSwitch + '" title="Đọc SNMP lại ngay trên máy chủ web này">' +
            '<i class="fas fa-rotate me-1"></i>Đọc ngay</button>' +
            (chiXem ? '' : '<button type="button" class="btn btn-sm btn-light border sk-bo" data-id="' + x.idSwitch + '" data-ten="' + tieuDe + '" title="Bỏ theo dõi"><i class="fas fa-xmark"></i></button>') +
            '</div></div>';

        var canhBao = '';
        if (x.loi && tt === 'KHONG_DOC_DUOC') canhBao = '<div class="alert alert-danger py-1 px-2 small mb-2">' + esc(x.loi) + (dl ? ' Số liệu bên dưới là lần đọc được gần nhất.' : '') + '</div>';
        else if (x.tomTat && (tt === 'LOI' || tt === 'CANH_BAO')) canhBao = '<div class="alert alert-' + (tt === 'LOI' ? 'danger' : 'warning') + ' py-1 px-2 small mb-2">' + esc(x.tomTat) + '</div>';

        var than = '';
        if (dl) {
            var nhom = {};
            dsMuc.forEach(function (m) { (nhom[m.nhom] = nhom[m.nhom] || []).push(m); });
            var khoi = THU_TU_NHOM.map(function (n) { return khoiNhom(n, nhom[n] || []); }).join('');
            than = tomTatO(dl) + (khoi ? '<div class="row g-2">' + khoi + '</div>' : '<div class="small text-muted">Thiết bị không trả MIB sức khoẻ Cisco (CPU / nhiệt độ / quạt / nguồn).</div>') +
                bangCongLoi(dl.dsCongLoi);
        } else if (tt === 'CHUA_DOC') {
            than = '<div class="small text-muted py-3 text-center">Chưa có số liệu — chờ lượt đọc kế tiếp hoặc bấm "Đọc ngay".</div>';
        }

        return '<div class="col-12"><div class="sk-the sk-the-' + lop(tt) + '" data-id="' + x.idSwitch + '">' + trenCung + canhBao + than + '</div></div>';
    }

    function veDanhSach(res) {
        var ds = res.duLieu || [];
        var $ds = $('#skDanhSach');
        if (!ds.length) {
            $ds.html('<div class="col-12 text-center text-muted py-4">Chưa có thiết bị nào được theo dõi' +
                (chiXem ? '.' : ' — chọn switch ở ô phía trên rồi bấm "Theo dõi".') + '</div>');
        } else {
            $ds.html(ds.map(theThietBi).join(''));
        }

        var dem = { OK: 0, CANH_BAO: 0, LOI: 0, KHONG_DOC_DUOC: 0 };
        ds.forEach(function (x) { if (dem[x.trangThai] !== undefined) dem[x.trangThai]++; });
        $('#skTongHop').html('Thiết bị: ' + ds.length +
            ' · <span class="text-success">Tốt: ' + dem.OK + '</span>' +
            ' · <span class="text-warning">Cảnh báo: ' + dem.CANH_BAO + '</span>' +
            ' · <span class="text-danger">Lỗi: ' + (dem.LOI + dem.KHONG_DOC_DUOC) + '</span>');
        $('#skGhiChu').text('Tự đọc SNMP mỗi ' + Math.round((res.chuKyGiay || 300) / 60) + ' phút' +
            (res.mayNayDoc ? '' : ' (do máy chủ khác đọc)') + '.');
        var soXau = dem.LOI + dem.KHONG_DOC_DUOC;
        $('#badgeSkLoi').text(soXau).toggleClass('d-none', soXau === 0);

        var $chon = $('#skChonSwitch');
        if ($chon.length && res.dsChon) {
            var cu = $chon.val();
            $chon.html('<option value="">-- Chọn switch để theo dõi --</option>' + res.dsChon.map(function (s) {
                return '<option value="' + s.idSwitch + '">' + esc(s.tenSwitch + ' · ' + s.diaChiIp + (s.model ? ' · ' + s.model : '')) + '</option>';
            }).join(''));
            if (cu) $chon.val(cu);
        }
    }

    function taiDanhSach() {
        if (dangTai) return;
        dangTai = true;
        $.ajax({ url: goc + '/SucKhoe', cache: false })
            .done(function (res) {
                if (!res || !res.thanhCong) {
                    $('#skLoi').text((res && res.thongBao) || 'Không tải được dữ liệu.').removeClass('d-none');
                    $('#skDanhSach').html('');
                    return;
                }
                $('#skLoi').addClass('d-none');
                veDanhSach(res);
            })
            .fail(function () { $('#skLoi').text('Không kết nối được máy chủ, thử tải lại.').removeClass('d-none'); })
            .always(function () { dangTai = false; });
    }

    function taiLichSu() {
        $.ajax({ url: goc + '/SucKhoe/LichSu', cache: false })
            .done(function (res) {
                var $b = $('#skLsBody');
                if (!res || !res.thanhCong) {
                    $b.html('<tr><td colspan="4" class="ccdc-trangthai text-danger py-3">' + esc((res && res.thongBao) || 'Không tải được nhật ký.') + '</td></tr>');
                    return;
                }
                if (!res.duLieu.length) {
                    $b.html('<tr><td colspan="4" class="ccdc-trangthai text-muted py-3">Chưa có lần đổi trạng thái nào.</td></tr>');
                    return;
                }
                $b.html(res.duLieu.map(function (x) {
                    return '<tr><td>' + gio(x.thoiGian) + '</td><td>' + esc(x.tenSwitch) + '</td><td>' +
                        '<span class="sv-sk ' + lop(x.tuTrangThai) + '">' + esc(NHAN[x.tuTrangThai] || x.tuTrangThai || '—') + '</span>' +
                        ' <i class="fas fa-arrow-right mx-1 text-muted"></i> ' +
                        '<span class="sv-sk ' + lop(x.sangTrangThai) + '">' + esc(NHAN[x.sangTrangThai] || x.sangTrangThai) + '</span></td>' +
                        '<td class="small" style="white-space:normal;">' + esc(x.tomTat) + '</td></tr>';
                }).join(''));
            })
            .fail(function () { $('#skLsBody').html('<tr><td colspan="4" class="ccdc-trangthai text-danger py-3">Không kết nối được máy chủ.</td></tr>'); });
    }

    function taiTatCa() { taiDanhSach(); taiLichSu(); }

    function baoLoiThe($nut, thongBao) {
        var $the = $nut.closest('.sk-the');
        $the.find('.sk-thong-bao').remove();
        $('<div class="alert alert-danger py-1 px-2 small mb-2 sk-thong-bao"></div>').text(thongBao).insertAfter($the.children().first());
    }

    $(function () {
        var $trang = $('#swTrang');
        if (!$trang.length || !$('#paneSucKhoe').length) return;
        goc = '/QLSwitch/' + $trang.data('congTy');
        chiXem = !!$trang.data('chiXem');

        // Tải sẵn 1 lần để badge lỗi hiện ngay cả khi chưa mở tab; mở tab thì tự làm mới mỗi phút
        taiDanhSach();
        $('#tabSucKhoeBtn').on('shown.bs.tab', function () {
            if (!daTai) { daTai = true; taiLichSu(); } else taiTatCa();
            clearInterval(timer);
            timer = setInterval(function () { if (!document.hidden) taiTatCa(); }, 60000);
        }).on('hidden.bs.tab', function () { clearInterval(timer); });

        $('#skBtnTaiLai').on('click', taiTatCa);

        $(document).on('click', '#paneSucKhoe .sk-xem-them', function () {
            var $nhom = $(this).closest('.sv-sk-nhom');
            var mo = $nhom.find('.sk-an').first().hasClass('d-none');
            $nhom.find('.sk-an').toggleClass('d-none', !mo);
            $(this).text(mo ? 'Thu gọn' : 'Xem thêm ' + $nhom.find('.sk-an').length + ' dòng');
        });

        $(document).on('click', '#paneSucKhoe .sk-doc-ngay', function () {
            var $nut = $(this), html = $nut.html();
            $nut.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span>Đang đọc...');
            $.ajax({ url: goc + '/SucKhoe/DocNgay', method: 'POST', data: { idSwitch: $nut.data('id') }, timeout: 90000 })
                .done(function (res) {
                    if (res && res.thanhCong) { taiTatCa(); return; }
                    baoLoiThe($nut, (res && res.thongBao) || 'Đọc thất bại.');
                })
                .fail(function () { baoLoiThe($nut, 'Không kết nối được máy chủ hoặc đọc quá lâu.'); })
                .always(function () { $nut.prop('disabled', false).html(html); });
        });

        $(document).on('click', '#skBtnThem', function () {
            var $nut = $(this), id = $('#skChonSwitch').val();
            if (!id) { $('#skChonSwitch').trigger('focus'); return; }
            $nut.prop('disabled', true);
            $.post(goc + '/SucKhoe/Them', { idSwitch: id, tenHienThi: $('#skTenHienThi').val() })
                .done(function (res) {
                    if (res && res.thanhCong) { $('#skTenHienThi').val(''); $('#skChonSwitch').val(''); taiDanhSach(); return; }
                    $('#skLoi').text((res && res.thongBao) || 'Không thêm được.').removeClass('d-none');
                })
                .fail(function () { $('#skLoi').text('Không kết nối được máy chủ.').removeClass('d-none'); })
                .always(function () { $nut.prop('disabled', false); });
        });

        $(document).on('click', '#paneSucKhoe .sk-bo', function () {
            var $nut = $(this);
            if (!confirm('Bỏ theo dõi sức khoẻ "' + $nut.data('ten') + '"? Nhật ký cũ vẫn giữ.')) return;
            $nut.prop('disabled', true);
            $.post(goc + '/SucKhoe/BoTheoDoi', { idSwitch: $nut.data('id') })
                .done(function (res) {
                    if (res && res.thanhCong) { $nut.closest('.col-12').remove(); taiDanhSach(); return; }
                    baoLoiThe($nut, (res && res.thongBao) || 'Không bỏ được.');
                    $nut.prop('disabled', false);
                })
                .fail(function () { baoLoiThe($nut, 'Không kết nối được máy chủ.'); $nut.prop('disabled', false); });
        });
    });
})();
