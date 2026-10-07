// /QLCamera — tab "Đầu ghi chi nhánh" (bảng KK_DauGhi, nhập tay, không lưu mật khẩu)
(function () {
    'use strict';

    // PFVN/MEGA dùng chung tab: đầu ghi lọc theo công ty của trang (server gán công ty khi thêm mới)
    var congTy = ($('section.content[data-cong-ty]').attr('data-cong-ty') || 'BPVN').toUpperCase();

    var modalDauGhi, modalXoa;
    var timerTimKiem = null;
    var daTai = false;
    var COT = 11;

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + (className || '') + '" title="' + s + '">' + s + '</td>';
    }

    // Ô IP bấm được, mở trang web đầu ghi ở tab mới: giữ nguyên nếu đã nhập sẵn http(s)://,
    // còn IP/tên miền trần thì ghép cổng web (443 → https). Giá trị lạ (có khoảng trắng, ghi chú...) để chữ thường.
    function oIp(giaTri, portWeb) {
        var s = $.trim(giaTri || '');
        var url = null;
        if (/^https?:\/\/[A-Za-z0-9.\-]+(:\d{1,5})?\/?$/i.test(s)) url = s;
        else if (/^[A-Za-z0-9.\-]+(:\d{1,5})?$/.test(s)) {
            var coCong = s.indexOf(':') > 0;
            var cong = coCong ? Number(s.split(':')[1]) : Number(portWeb);
            url = (cong === 443 ? 'https://' : 'http://') + s + (!coCong && cong && cong !== 80 && cong !== 443 ? ':' + cong : '');
        }
        if (!url) return o(giaTri, 'small font-monospace');
        var e = escapeHtml(s);
        return '<td class="small font-monospace" title="' + escapeHtml(url) + '"><a href="' + escapeHtml(url)
            + '" target="_blank" rel="noopener noreferrer">' + e + '</a></td>';
    }

    function dongTrangThai(noiDung, className) {
        $('#dgBody').html('<tr><td colspan="' + COT + '" class="ccdc-trangthai ' + className + ' py-4">' + escapeHtml(noiDung) + '</td></tr>');
    }

    function mauTrangThai(tt) {
        switch (tt) {
            case 'Đang dùng': return 'background:#dcfce7; color:#166534;';
            case 'Đã tháo': return 'background:#e2e8f0; color:#475569;';
            default: return 'background:#fee2e2; color:#991b1b;'; // Ngừng dùng
        }
    }

    function anLoi($khung) { $khung.addClass('d-none').text(''); }
    function hienLoi($khung, noiDung) { $khung.removeClass('d-none').text(noiDung); }

    function taiDanhSach() {
        dongTrangThai('Đang tải dữ liệu...', 'text-muted');

        $.getJSON('/QLCamera/DauGhi/GetDanhSach', { tuKhoa: $('#dgTuKhoa').val(), trangThai: $('#dgTrangThai').val(), congTy: congTy })
            .done(function (res) {
                if (!res.thanhCong) { dongTrangThai(res.thongBao || 'Không tải được dữ liệu.', 'text-danger'); return; }
                veBang(res.duLieu);
                $('#dgTongDong').text(res.tongDong);
                $('#dgDemCamera').text(res.tongCamera);
            })
            .fail(function () { dongTrangThai('Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    function veBang(data) {
        if (!data || !data.length) { dongTrangThai('Chưa có đầu ghi nào khớp bộ lọc.', 'text-muted'); return; }

        var html = data.map(function (x, i) {
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + o(x.diaDiem, 'ccdc-ten')
                + oIp(x.ipPublic, x.portWeb)
                + oIp(x.ipLocal, x.portWeb)
                + '<td class="ccdc-num small">' + escapeHtml(x.portSv) + '</td>'
                + '<td class="ccdc-num small">' + escapeHtml(x.portWeb) + '</td>'
                + '<td class="ccdc-num small">' + escapeHtml(x.tongCamera) + '</td>'
                + o(x.raid, 'small')
                + '<td><span class="badge rounded-pill" style="' + mauTrangThai(x.trangThai) + '">' + escapeHtml(x.trangThai || 'Chưa rõ') + '</span></td>'
                + o(x.ghiChu, 'small text-muted')
                + '<td class="ccdc-thaotac">'
                + '<button type="button" class="btn btn-sm btn-light border me-1 btn-sua-dg" title="Sửa"><i class="fas fa-pen text-primary"></i></button>'
                + '<button type="button" class="btn btn-sm btn-light border btn-xoa-dg" title="Xoá"><i class="fas fa-trash text-danger"></i></button>'
                + '</td></tr>';
        }).join('');

        var $body = $('#dgBody').html(html);
        $body.find('tr').each(function (i) { $(this).data('dauGhi', data[i]); });
    }

    function moModal(x) {
        $('#formDauGhi')[0].reset();
        $('#IdDauGhi').val(x ? x.idDauGhi : 0);
        if (x) {
            $('#dgDiaDiem').val(x.diaDiem || '');
            $('#dgTrangThaiSua').val(x.trangThai || $('#dgTrangThaiSua option:first').val());
            $('#dgIpPublic').val(x.ipPublic || '');
            $('#dgIpLocal').val(x.ipLocal || '');
            $('#dgPortSv').val(x.portSv === null ? '' : x.portSv);
            $('#dgPortWeb').val(x.portWeb === null ? '' : x.portWeb);
            $('#dgTongCamera').val(x.tongCamera === null ? '' : x.tongCamera);
            $('#dgRaid').val(x.raid || '');
            $('#dgGhiChu').val(x.ghiChu || '');
        }
        $('#modalDauGhiTitle').text(x ? 'Sửa đầu ghi' : 'Thêm đầu ghi');
        anLoi($('#dauGhiLoi'));
        modalDauGhi.show();
    }

    function luu() {
        var $loi = $('#dauGhiLoi');
        if (!$('#dgDiaDiem').val().trim()) {
            hienLoi($loi, 'Vui lòng nhập địa điểm.');
            $('#dgDiaDiem').focus();
            return;
        }

        anLoi($loi);
        var $btn = $('#btnLuuDauGhi').prop('disabled', true); // chặn double-submit

        $.post('/QLCamera/DauGhi/Save', $('#formDauGhi').serialize() + '&congTy=' + encodeURIComponent(congTy))
            .done(function (res) {
                if (res.thanhCong) { modalDauGhi.hide(); taiDanhSach(); }
                else hienLoi($loi, res.thongBao || 'Lưu không thành công.');
            })
            .fail(function () { hienLoi($loi, 'Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function xacNhanXoa() {
        var $loi = $('#xoaDauGhiLoi');
        anLoi($loi);
        var $btn = $('#btnXacNhanXoaDauGhi').prop('disabled', true);

        $.post('/QLCamera/DauGhi/Delete', { id: $('#xoaIdDauGhi').val(), lyDo: $('#xoaLyDoDauGhi').val(), congTy: congTy })
            .done(function (res) {
                if (res.thanhCong) { modalXoa.hide(); taiDanhSach(); }
                else hienLoi($loi, res.thongBao || 'Xoá không thành công.');
            })
            .fail(function () { hienLoi($loi, 'Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    $(function () {
        modalDauGhi = new bootstrap.Modal(document.getElementById('modalDauGhi'));
        modalXoa = new bootstrap.Modal(document.getElementById('modalXoaDauGhi'));

        $('#btnThemDauGhi').on('click', function () { moModal(null); });
        $('#btnLuuDauGhi').on('click', luu);
        $('#btnXacNhanXoaDauGhi').on('click', xacNhanXoa);
        $('#formDauGhi').on('submit', function (e) { e.preventDefault(); luu(); });

        $('#dgTrangThai').on('change', taiDanhSach);
        $('#dgTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(taiDanhSach, 350);
        });

        $('#dgBody').on('click', '.btn-sua-dg', function () {
            moModal($(this).closest('tr').data('dauGhi'));
        });
        $('#dgBody').on('click', '.btn-xoa-dg', function () {
            var x = $(this).closest('tr').data('dauGhi');
            $('#xoaIdDauGhi').val(x.idDauGhi);
            $('#xoaTenDauGhi').text(x.diaDiem + (x.ipLocal ? ' (' + x.ipLocal + ')' : ''));
            $('#xoaLyDoDauGhi').val('');
            anLoi($('#xoaDauGhiLoi'));
            modalXoa.show();
        });

        $('#tabDauGhiBtn').on('shown.bs.tab', function () {
            if (daTai) return;
            daTai = true;
            taiDanhSach();
        });
    });
})();
