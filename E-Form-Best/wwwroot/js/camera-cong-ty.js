/**
 * Trang camera PFVN / MEGA (/QLCamera/{congTy}): danh sách kênh + ảnh lưu sẵn.
 * Không có hệ thống giám sát như BPVN — dữ liệu là "_kenh.json" và ảnh do script chụp trên máy bên đó
 * (tools/chup-anh-camera-pfvn.ps1) đẩy về, nên trạng thái online là trạng thái lúc chụp ảnh gần nhất.
 */
(function () {
    'use strict';

    var khung = document.getElementById('camCongTy');
    if (!khung) return;
    var congTy = khung.getAttribute('data-cong-ty');

    var dsKenh = [];
    var modal = null;
    var urlAnhDangXem = null;

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // "2026-10-07T11:25:19" -> "11:25 07/10/2026"
    function ngayGio(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(s || '');
        return m ? m[4] + ':' + m[5] + ' ' + m[3] + '/' + m[2] + '/' + m[1] : '';
    }

    function tuBanGhi(k) { return k.ketQua === 'PLAYBACK' || k.ketQua === 'BOQUA'; }

    function hienLoi(thongBao) {
        $('#ctLoi').toggleClass('d-none', !thongBao).text(thongBao || '');
    }

    function taiDuLieu() {
        var $btn = $('#ctBtnTaiLai').prop('disabled', true);
        fetch('/QLCamera/' + encodeURIComponent(congTy) + '/DanhSach', { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (kq) {
                if (!kq.thanhCong) { hienLoi(kq.thongBao || 'Không tải được dữ liệu camera.'); return; }
                hienLoi(null);
                dsKenh = kq.duLieu.kenh || [];
                var coDuLieu = !!kq.duLieu.capNhat || dsKenh.length > 0;
                $('#ctChuaKetNoi').toggleClass('d-none', coDuLieu);
                $('#ctNoiDung').toggleClass('d-none', !coDuLieu);
                if (!coDuLieu) return;

                $('#ctCapNhat').text(ngayGio(kq.duLieu.capNhat) || '–');
                veSoLieu();
                veDsDauGhi();
                veBang();
            })
            .catch(function () { hienLoi('Lỗi kết nối máy chủ E-Form.'); })
            .finally(function () { $btn.prop('disabled', false); });
    }

    function veSoLieu() {
        var up = dsKenh.filter(function (k) { return k.online; }).length;
        var dauGhi = {};
        dsKenh.forEach(function (k) { dauGhi[k.nvr] = true; });
        $('#ctTong').text(dsKenh.length);
        $('#ctUp').text(up);
        $('#ctDown').text(dsKenh.length - up);
        $('#ctChuaAnh').text(dsKenh.filter(function (k) { return !k.anhLuc; }).length);
        $('#ctSoDauGhi').text(Object.keys(dauGhi).length);
    }

    function veDsDauGhi() {
        var $sel = $('#ctDauGhi');
        var dangChon = $sel.val();
        var dauGhi = [];
        dsKenh.forEach(function (k) { if (dauGhi.indexOf(k.nvr) < 0) dauGhi.push(k.nvr); });
        $sel.find('option:not(:first)').remove();
        dauGhi.forEach(function (ip) { $sel.append($('<option>').val(ip).text(ip)); });
        $sel.val(dauGhi.indexOf(dangChon) >= 0 ? dangChon : '');
    }

    function locDs() {
        var tuKhoa = ($('#ctTuKhoa').val() || '').trim().toLowerCase();
        var trangThai = $('#ctTrangThai').val();
        var dauGhi = $('#ctDauGhi').val();
        return dsKenh.filter(function (k) {
            if (dauGhi && k.nvr !== dauGhi) return false;
            if (trangThai === 'UP' && !k.online) return false;
            if (trangThai === 'DOWN' && k.online) return false;
            if (trangThai === 'CHUA_ANH' && k.anhLuc) return false;
            if (!tuKhoa) return true;
            return [k.ten, k.ipCamera, k.nvr, String(k.kenh)].join(' ').toLowerCase().indexOf(tuKhoa) >= 0;
        });
    }

    function veBang() {
        var ds = locDs();
        $('#ctDem').text('Hiển thị ' + ds.length + ' / ' + dsKenh.length + ' camera');
        var dangLoc = $('#ctTrangThai').val() || '';
        $('#camCongTy .cam-tile-loc').each(function () {
            $(this).toggleClass('cam-tile-chon', ($(this).attr('data-loc') || '') === dangLoc);
        });

        if (!ds.length) {
            $('#ctBody').html('<tr><td colspan="7" class="ccdc-trangthai text-muted py-4">Không có camera nào khớp bộ lọc.</td></tr>');
            return;
        }

        var html = ds.map(function (k, i) {
            var trangThai = k.online
                ? '<span class="text-success fw-bold"><span class="cam-dot" style="background:#22c55e;"></span>Hoạt động</span>'
                : '<span class="text-danger fw-bold"><span class="cam-dot" style="background:#ef4444;"></span>Mất kết nối</span>';
            var ten = escapeHtml(k.ten || ('Kênh ' + k.kenh))
                + (!k.anhLuc ? '<i class="fas fa-circle-exclamation cam-chua-anh" title="Chưa có ảnh lưu: chụp trực tiếp và lấy từ playback đầu ghi đều không được"></i>' : '');
            var anh = k.anhLuc
                ? escapeHtml(ngayGio(k.anhLuc)) + (tuBanGhi(k) ? ' <span class="badge bg-secondary" title="Camera không chụp trực tiếp được, ảnh là khung hình cuối trong bản ghi của đầu ghi">từ bản ghi</span>' : '')
                : '<span class="text-muted">Chưa có ảnh</span>';
            return '<tr class="' + (k.online ? '' : 'cam-down') + '" data-i="' + i + '" title="Bấm để xem ảnh lưu">'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + '<td class="small">' + trangThai + '</td>'
                + '<td class="ccdc-ten" title="' + escapeHtml(k.ten) + '">' + ten + '</td>'
                + '<td class="small font-monospace">' + escapeHtml(k.ipCamera) + '</td>'
                + '<td class="small font-monospace">' + escapeHtml(k.nvr) + '</td>'
                + '<td class="ccdc-num small">' + escapeHtml(k.kenh) + '</td>'
                + '<td class="small">' + anh + '</td>'
                + '</tr>';
        }).join('');
        var $body = $('#ctBody').html(html);
        $body.find('tr').each(function () { $(this).data('kenh', ds[+$(this).attr('data-i')]); });
    }

    function thuHoiAnh() {
        if (urlAnhDangXem) { URL.revokeObjectURL(urlAnhDangXem); urlAnhDangXem = null; }
    }

    function moXem(k) {
        if (!k) return;
        var anh = document.getElementById('ctXemAnh');
        thuHoiAnh();
        anh.removeAttribute('src');
        $('#ctXemTen').text(k.ten || ('Kênh ' + k.kenh));
        $('#ctXemThongTin').text('IP ' + (k.ipCamera || '?') + ' · Đầu ghi ' + k.nvr + ' · kênh ' + k.kenh
            + ' · ' + (k.online ? 'Hoạt động' : 'Mất kết nối'));
        $('#ctXemNhan').text(k.anhLuc ? 'Đang tải ảnh...' : 'Chưa có ảnh lưu cho camera này');
        modal.show();
        if (!k.anhLuc) return;

        fetch('/QLCamera/' + encodeURIComponent(congTy) + '/AnhLuu?nvrIp=' + encodeURIComponent(k.nvr)
            + '&kenh=' + encodeURIComponent(k.kenh) + '&t=' + Date.now(), { credentials: 'same-origin' })
            .then(function (res) {
                if (res.status === 404) return null;
                if (!res.ok) throw new Error('HTTP ' + res.status);
                var luc = res.headers.get('X-Chup-Luc');
                return res.blob().then(function (b) { return { blob: b, luc: luc }; });
            })
            .then(function (kq) {
                if (!kq) { $('#ctXemNhan').text('Chưa có ảnh lưu cho camera này'); return; }
                thuHoiAnh();
                urlAnhDangXem = URL.createObjectURL(kq.blob);
                anh.src = urlAnhDangXem;
                $('#ctXemNhan').text((tuBanGhi(k) ? 'Khung hình ghi cuối lúc ' : 'Ảnh lưu lúc ') + (ngayGio(kq.luc) || '(không rõ giờ)'));
            })
            .catch(function () { $('#ctXemNhan').text('Không tải được ảnh lưu'); });
    }

    $(function () {
        modal = new bootstrap.Modal(document.getElementById('modalXemAnhLuu'));
        document.getElementById('modalXemAnhLuu').addEventListener('hidden.bs.modal', thuHoiAnh);

        $('#ctTuKhoa').on('input', veBang);
        $('#ctTrangThai, #ctDauGhi').on('change', veBang);
        $('#ctBtnTaiLai').on('click', taiDuLieu);
        $(document).on('click', '#camCongTy .cam-tile-loc', function () {
            $('#ctTrangThai').val($(this).attr('data-loc') || '');
            veBang();
        });
        $('#ctBody').on('click', 'tr', function () { moXem($(this).data('kenh')); });

        taiDuLieu();
    });
})();
