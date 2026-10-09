// /QLCamera (BPVN/PFVN/MEGA) — tab "Lưới ảnh": mỗi camera 1 thẻ gồm ảnh lưu sẵn + thông tin.
// Không tự gọi API danh sách: nhận dữ liệu camera-giam-sat.js phát ra (sự kiện "camgs:duLieu") sau mỗi lần tải,
// bấm thẻ thì mở chung cửa sổ xem của tab Giám sát (window.camGiamSat).
(function () {
    'use strict';

    var duLieu = null;          // { dsCamera, dsGhiChu, dsAnhLuu, dsGhim } của lần tải gần nhất
    // Thẻ <img> đã tạo theo khoá "nvrIp|kenh": tự làm mới 60 giây vẽ lại lưới nhưng giữ nguyên ảnh,
    // không tải lại hàng trăm ảnh mỗi phút (ảnh lưu chỉ đổi 2 lần/ngày). Bấm "Tải lại" mới xoá.
    var anhDaTao = {};
    var timerTimKiem = null;
    var KHOA_CO = 'qlcamera.luoi.co';

    function khoa(c) { return c.nvr_ip + '|' + c.cam_id; }

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
        if (isNaN(d.getTime())) return String(v);
        return hai(d.getDate()) + '/' + hai(d.getMonth() + 1) + ' ' + hai(d.getHours()) + ':' + hai(d.getMinutes());
    }

    // Danh sách đầu ghi lấy từ chính dữ liệu camera, giữ lựa chọn đang chọn khi vẽ lại
    function veLocDauGhi(ds) {
        var $sel = $('#luoiNvr');
        var dangChon = $sel.val();
        var map = {};
        ds.forEach(function (c) { if (!map[c.nvr_ip]) map[c.nvr_ip] = c.nvr_name || c.nvr_ip; });
        var dsIp = Object.keys(map).sort(function (a, b) { return String(map[a]).localeCompare(String(map[b]), 'vi'); });
        $sel.find('option:not(:first)').remove();
        dsIp.forEach(function (ip) {
            $sel.append($('<option>').val(ip).text(map[ip] + (map[ip] !== ip ? ' (' + ip + ')' : '')));
        });
        if (dangChon && map[dangChon]) $sel.val(dangChon);
    }

    function taoAnh(c) {
        var k = khoa(c);
        if (anhDaTao[k]) return anhDaTao[k];

        var $o;
        // Biết chắc kênh chưa có ảnh lưu thì không gọi server
        if (duLieu.dsAnhLuu && !duLieu.dsAnhLuu[k]) {
            $o = $('<div class="cam-the-khong-anh"><i class="fas fa-image"></i>Chưa có ảnh lưu</div>');
        } else {
            $o = $('<img loading="lazy" decoding="async">').attr('alt', c.name || ('Kênh ' + c.cam_id));
            $o.on('error', function () {
                // Giữ chỗ thay ảnh lỗi để lần tự làm mới sau không gọi lại ảnh hỏng
                var $loi = $('<div class="cam-the-khong-anh"><i class="fas fa-image"></i>Không tải được ảnh</div>');
                $(this).replaceWith($loi);
                anhDaTao[k] = $loi;
            });
            $o.attr('src', window.camGiamSat.urlAnhLuu(c));
        }
        anhDaTao[k] = $o;
        return $o;
    }

    function ve() {
        var $khung = $('#luoiKhung');
        if (!duLieu) return;

        var tuKhoa = ($('#luoiTuKhoa').val() || '').trim().toLowerCase();
        var trangThai = $('#luoiTrangThai').val();
        var nvrIp = $('#luoiNvr').val();
        var chiGhim = $('#luoiChiGhim').is(':checked');
        var dsGhiChu = duLieu.dsGhiChu || {};
        var dsGhim = duLieu.dsGhim || {};
        var tatCa = duLieu.dsCamera || [];

        var loc = tatCa.filter(function (c) {
            if (trangThai && c.status !== trangThai) return false;
            if (nvrIp && c.nvr_ip !== nvrIp) return false;
            if (chiGhim && !dsGhim[khoa(c)]) return false;
            if (tuKhoa) {
                var gc = dsGhiChu[khoa(c)];
                var chuoi = [c.name, c.ip, c.nvr_name, c.nvr_ip, c.zone, gc ? gc.ghiChu : ''].join(' ').toLowerCase();
                if (chuoi.indexOf(tuKhoa) < 0) return false;
            }
            return true;
        });

        // Ghim lên đầu; còn lại giữ thứ tự cố định theo đầu ghi + kênh để vị trí ô không nhảy khi trạng thái đổi
        loc.sort(function (a, b) {
            var ga = dsGhim[khoa(a)], gb = dsGhim[khoa(b)];
            if (!!ga !== !!gb) return ga ? -1 : 1;
            if (ga && gb && ga !== gb) return ga - gb;
            return String(a.nvr_name).localeCompare(String(b.nvr_name), 'vi') || (Number(a.cam_id) - Number(b.cam_id));
        });

        var soDown = loc.filter(function (c) { return c.status === 'DOWN'; }).length;
        $('#luoiDem').text('Hiển thị ' + loc.length + ' / ' + tatCa.length + ' camera' + (soDown ? ' · ' + soDown + ' mất kết nối' : ''));

        // Tách ảnh ra trước khi xoá lưới để jQuery không gỡ sự kiện của thẻ <img> đang giữ lại
        Object.keys(anhDaTao).forEach(function (k) { anhDaTao[k].detach(); });

        if (!loc.length) {
            $khung.html('<div class="cam-luoi-trong text-muted">' + (tatCa.length ? 'Không có camera nào khớp bộ lọc.' : 'Chưa có camera.') + '</div>');
            return;
        }

        var html = loc.map(function (c) {
            var k = khoa(c);
            var ghim = !!dsGhim[k];
            var gc = dsGhiChu[k];
            var lop = (c.status === 'DOWN' ? ' cam-down' : '') + (ghim ? ' cam-da-ghim' : '');
            var nhan = c.status === 'UP' ? 'Hoạt động' : c.status === 'DOWN' ? 'Mất kết nối' : (c.status || 'Chưa rõ');
            var dongMat = c.status === 'DOWN' && c.down_since_at
                ? '<div class="cam-the-dong text-danger">Mất từ ' + escapeHtml(ngayGio(c.down_since_at)) + '</div>' : '';
            return '<div class="cam-the' + lop + '" data-k="' + escapeHtml(k) + '">'
                + '<div class="cam-the-anh">'
                + '<span class="cam-the-nhan' + (c.status === 'UP' || c.status === 'DOWN' ? '' : ' cam-the-nhan-khac') + '">' + escapeHtml(nhan) + '</span>'
                + '<button type="button" class="cam-ghim' + (ghim ? ' cam-ghim-bat' : '') + '" title="'
                + (ghim ? 'Bỏ ghim' : 'Ghim lên đầu (chỉ trên máy này)') + '"><i class="fas fa-thumbtack"></i></button>'
                + '</div>'
                + '<div class="cam-the-tt">'
                + '<div class="cam-the-ten" title="' + escapeHtml(c.name) + '">' + escapeHtml(c.name) + '</div>'
                + '<div class="cam-the-dong" title="IP camera">' + (c.ip ? '<i class="fas fa-network-wired me-1"></i>' + escapeHtml(c.ip) : '<span class="text-muted">Chưa có IP</span>') + '</div>'
                + '<div class="cam-the-dong cam-the-dong-phu" title="' + escapeHtml(c.nvr_name + ' (' + c.nvr_ip + ')') + '">'
                + '<i class="fas fa-server me-1"></i>' + escapeHtml(c.nvr_name) + ' · kênh ' + escapeHtml(c.cam_id)
                + (c.zone ? ' · ' + escapeHtml(c.zone) : '') + '</div>'
                + dongMat
                + (gc && gc.ghiChu ? '<div class="cam-the-ghi-chu" title="' + escapeHtml(gc.ghiChu) + '"><i class="fas fa-note-sticky me-1"></i>' + escapeHtml(gc.ghiChu) + '</div>' : '')
                + '</div></div>';
        }).join('');
        $khung.html(html);

        var theoKhoa = {};
        loc.forEach(function (c) { theoKhoa[khoa(c)] = c; });
        $khung.children('.cam-the').each(function () {
            var c = theoKhoa[$(this).attr('data-k')];
            $(this).data('cam', c).find('.cam-the-anh').prepend(taoAnh(c));
        });
    }

    function doiCo(co) {
        $('#luoiKhung').removeClass('cam-luoi-nho cam-luoi-vua cam-luoi-lon').addClass('cam-luoi-' + co);
        try { localStorage.setItem(KHOA_CO, co); } catch (e) { /* trình duyệt chặn storage: chỉ không nhớ cỡ ô */ }
    }

    $(document).on('camgs:duLieu', function (e, d) {
        duLieu = d;
        veLocDauGhi(d.dsCamera || []);
        ve();
    });

    $(function () {
        if (!document.getElementById('luoiKhung') || !window.camGiamSat) return;

        var co = null;
        try { co = localStorage.getItem(KHOA_CO); } catch (e) { co = null; }
        if (co === 'nho' || co === 'vua' || co === 'lon') $('#luoiCo').val(co);
        doiCo($('#luoiCo').val());

        $('#luoiCo').on('change', function () { doiCo($(this).val()); });
        $('#luoiTrangThai, #luoiNvr, #luoiChiGhim').on('change', ve);
        $('#luoiTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(ve, 200);
        });
        $('#luoiBtnTaiLai').on('click', function () {
            // Lấy lại cả ảnh: bỏ thẻ <img> đã giữ để lần vẽ sau tạo URL mới
            anhDaTao = {};
            window.camGiamSat.taiLai();
        });
        $(document).on('click', '#luoiKhung button.cam-ghim', function (e) {
            e.stopPropagation();
            window.camGiamSat.doiGhim($(this).closest('.cam-the').data('cam'));
        });
        $(document).on('click', '#luoiKhung .cam-the', function () {
            window.camGiamSat.moXem($(this).data('cam'));
        });
    });
})();
