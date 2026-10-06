// /QLCamera — tab "Lịch sử": camera đổi trạng thái Hoạt động ⇄ Mất kết nối (KK_CameraLichSu,
// do job nền CameraLichSuWorker ghi). Chỉ có dữ liệu từ lúc job bắt đầu chạy.
(function () {
    'use strict';

    var COT = 9;
    var daTai = false;
    var timerTimKiem = null;

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function hai(n) { return n < 10 ? '0' + n : String(n); }

    function ngayGio(v) {
        if (!v) return '';
        var d = new Date(v);
        if (isNaN(d.getTime())) return escapeHtml(v);
        return hai(d.getDate()) + '/' + hai(d.getMonth() + 1) + '/' + d.getFullYear()
            + ' ' + hai(d.getHours()) + ':' + hai(d.getMinutes()) + ':' + hai(d.getSeconds());
    }

    function ngayInput(d) { return d.getFullYear() + '-' + hai(d.getMonth() + 1) + '-' + hai(d.getDate()); }

    function thoiLuong(giay) {
        if (giay === null || giay === undefined) return '';
        var phut = Math.floor(giay / 60);
        if (phut < 1) return giay + ' giây';
        if (phut < 60) return phut + ' phút';
        var gio = Math.floor(phut / 60);
        if (gio < 24) return gio + ' giờ ' + (phut % 60) + ' phút';
        return Math.floor(gio / 24) + ' ngày ' + (gio % 24) + ' giờ';
    }

    function tenTrangThai(s) {
        if (s === 'UP') return 'Hoạt động';
        if (s === 'DOWN') return 'Mất kết nối';
        return s || '?';
    }

    function oSuKien(x) {
        var mau = x.sangTrangThai === 'DOWN' ? 'bg-danger' : x.sangTrangThai === 'UP' ? 'bg-success' : 'bg-secondary';
        var mui = x.sangTrangThai === 'DOWN' ? 'fa-arrow-down' : 'fa-arrow-up';
        return '<td class="small"><span class="badge rounded-pill ' + mau + '"><i class="fas ' + mui + ' me-1"></i>'
            + escapeHtml(tenTrangThai(x.sangTrangThai)) + '</span>'
            + '<div class="text-muted" style="font-size:11px;">từ ' + escapeHtml(tenTrangThai(x.tuTrangThai)) + '</div></td>';
    }

    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + (className || '') + '" title="' + s + '">' + s + '</td>';
    }

    function dongTrangThai(noiDung, className) {
        $('#lsBody').html('<tr><td colspan="' + COT + '" class="ccdc-trangthai ' + className + ' py-4">' + escapeHtml(noiDung) + '</td></tr>');
    }

    function taiDanhSach() {
        dongTrangThai('Đang tải dữ liệu...', 'text-muted');
        var $btn = $('#lsBtnTaiLai').prop('disabled', true);

        $.getJSON('/QLCamera/LichSu/DanhSach', {
            tuNgay: $('#lsTuNgay').val(),
            denNgay: $('#lsDenNgay').val(),
            loai: $('#lsLoai').val(),
            nvrIp: $('#lsNvr').val(),
            tuKhoa: $('#lsTuKhoa').val()
        })
            .done(function (res) {
                if (!res.thanhCong) { dongTrangThai(res.thongBao || 'Không tải được lịch sử.', 'text-danger'); return; }

                var t = res.tongHop || {};
                $('#lsTong').text(t.tong || 0);
                $('#lsDown').text(t.matKetNoi || 0);
                $('#lsUp').text(t.hoatDongLai || 0);
                $('#lsSoCamera').text(t.soCamera || 0);

                var ghiChu = res.batDauGhi
                    ? 'Có dữ liệu từ ' + ngayGio(res.batDauGhi)
                    : 'Chưa ghi nhận sự kiện nào — lịch sử bắt đầu từ khi hệ thống chạy, kiểm tra mỗi 2 phút';
                if (res.biCat) ghiChu += ' · Chỉ hiện ' + res.toiDa + ' sự kiện mới nhất, thu hẹp khoảng ngày để xem hết';
                $('#lsGhiChu').text(ghiChu);

                var ds = res.duLieu || [];
                if (!ds.length) { dongTrangThai('Không có sự kiện nào trong khoảng đã chọn.', 'text-muted'); return; }

                $('#lsBody').html(ds.map(function (x, i) {
                    return '<tr>'
                        + '<td class="text-muted small">' + (i + 1) + '</td>'
                        + '<td class="small">' + ngayGio(x.thoiGian) + '</td>'
                        + oSuKien(x)
                        + o(x.tenCamera, 'ccdc-ten')
                        + o(x.ipCamera, 'small font-monospace')
                        + o((x.tenDauGhi || '') + ' (' + x.nvrIp + ')', 'small')
                        + o(x.khuVuc, 'small')
                        + '<td class="ccdc-num small">' + escapeHtml(x.kenh) + '</td>'
                        + '<td class="small">' + (x.sangTrangThai === 'UP' ? escapeHtml(thoiLuong(x.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai('Lỗi kết nối máy chủ.', 'text-danger'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    // Danh sách đầu ghi cho bộ lọc lấy từ hệ thống giám sát (cùng nguồn với tab Giám sát)
    function taiDauGhi() {
        $.getJSON('/QLCamera/GiamSat/DauGhi').done(function (res) {
            if (!res.thanhCong || !res.duLieu || !res.duLieu.nvrs) return;
            var $s = $('#lsNvr');
            res.duLieu.nvrs.forEach(function (n) {
                $s.append($('<option>').val(n.nvr_ip).text(n.nvr_name + ' (' + n.nvr_ip + ')'));
            });
        });
    }

    $(function () {
        var homNay = new Date();
        var truoc = new Date();
        truoc.setDate(homNay.getDate() - 6);
        $('#lsTuNgay').val(ngayInput(truoc));
        $('#lsDenNgay').val(ngayInput(homNay));

        $('#lsTuNgay, #lsDenNgay, #lsLoai, #lsNvr').on('change', taiDanhSach);
        $('#lsBtnTaiLai').on('click', taiDanhSach);
        $('#lsTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(taiDanhSach, 350);
        });

        $('#tabLichSuBtn').on('shown.bs.tab', function () {
            if (daTai) { taiDanhSach(); return; }   // mở lại tab thì lấy sự kiện mới nhất
            daTai = true;
            taiDauGhi();
            taiDanhSach();
        });
    });
})();
