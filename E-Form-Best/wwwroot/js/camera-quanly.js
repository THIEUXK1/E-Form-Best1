// Màn hình Quản lý camera — /QLCamera, tab "Tài sản camera" (bảng KK_Camera nhập tay)
// Toàn bộ dữ liệu lấy qua AJAX từ QLCameraController, view chỉ dựng khung.
(function () {
    'use strict';

    var modalCamera, modalXoa;
    var timerTimKiem = null;
    var COT = 14;

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // API trả DateOnly dạng "2026-10-06" — cắt phần giờ nếu có để hợp input type=date
    function chuanHoaNgay(v) {
        if (!v) return '';
        return String(v).substring(0, 10);
    }

    function dinhDangNgay(v) {
        var s = chuanHoaNgay(v);
        if (!s) return '';
        var p = s.split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : s;
    }

    // Ô bị cắt bằng ellipsis nên gắn title để rê chuột xem đủ nội dung
    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + className + '" title="' + s + '">' + s + '</td>';
    }

    function dongTrangThai(noiDung, className) {
        $('#cameraTableBody').html('<tr><td colspan="' + COT + '" class="ccdc-trangthai ' + className + ' py-4">'
            + escapeHtml(noiDung) + '</td></tr>');
    }

    function mauTinhTrang(tt) {
        switch (tt) {
            case 'Đang hoạt động': return 'background:#dcfce7; color:#166534;';
            case 'Hư hỏng': return 'background:#fee2e2; color:#991b1b;';
            case 'Ngừng sử dụng': return 'background:#e2e8f0; color:#475569;';
            default: return 'background:#dbeafe; color:#1e40af;'; // Trong kho
        }
    }

    function oKetNoi(online) {
        if (online === true) return '<td class="small text-success fw-bold"><span class="cam-dot" style="background:#22c55e;"></span>Online</td>';
        if (online === false) return '<td class="small text-danger fw-bold"><span class="cam-dot" style="background:#ef4444;"></span>Mất KN</td>';
        return '<td class="small text-muted"><span class="cam-dot" style="background:#cbd5e1;"></span>Chưa có IP</td>';
    }

    // Hạn bảo hành đã qua thì tô đỏ để thấy ngay camera nào hết bảo hành
    function oHanBaoHanh(v) {
        if (!v) return '<td></td>';
        var hetHan = chuanHoaNgay(v) < new Date().toISOString().substring(0, 10);
        return '<td class="small ' + (hetHan ? 'text-danger fw-bold' : '') + '" title="' + (hetHan ? 'Đã hết bảo hành' : '') + '">'
            + dinhDangNgay(v) + '</td>';
    }

    // Bộ phận chỉ hiện những dòng thuộc công ty đang chọn; để trống công ty thì hiện tất cả
    function locBoPhanTheoCongTy($selectCongTy, $selectBoPhan) {
        var idCongTy = $selectCongTy.val();
        var dangChon = $selectBoPhan.val();
        var conHopLe = false;

        $selectBoPhan.find('option').each(function () {
            var $op = $(this);
            if (!$op.val()) return;
            var hop = !idCongTy || String($op.data('congty')) === String(idCongTy);
            $op.prop('hidden', !hop).prop('disabled', !hop);
            if (hop && $op.val() === dangChon) conHopLe = true;
        });

        if (!conHopLe) $selectBoPhan.val('');
    }

    function taiDanhSach() {
        var thamSo = {
            tuKhoa: $('#filterTuKhoa').val(),
            idCongTy: $('#filterCongTy').val(),
            idBoPhan: $('#filterBoPhan').val(),
            tinhTrang: $('#filterTinhTrang').val(),
            trucTuyen: $('#filterTrucTuyen').val()
        };

        dongTrangThai('Đang tải dữ liệu và kiểm tra kết nối...', 'text-muted');

        $.getJSON('/QLCamera/GetDanhSach', thamSo)
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai(res.thongBao || 'Không tải được dữ liệu.', 'text-danger');
                    return;
                }
                veBang(res.duLieu);
                $('#tongDong').text(res.tongDong);
                $('#tongOnline').text(res.tongOnline);
                $('#tongOffline').text(res.tongOffline);
            })
            .fail(function () { dongTrangThai('Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    function veBang(data) {
        if (!data || data.length === 0) {
            dongTrangThai('Chưa có camera nào khớp bộ lọc.', 'text-muted');
            return;
        }

        var html = data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + oKetNoi(x.online)
                + o(x.maCamera, 'small')
                + o(x.tenCamera, 'ccdc-ten')
                + o(x.diaChiIp, 'small font-monospace')
                + o(x.dauGhi, 'small')
                + '<td class="ccdc-num small">' + escapeHtml(x.kenh) + '</td>'
                + o(hangModel, 'small text-muted')
                + o(x.tenCongTy, 'small')
                + o(x.tenBoPhan, 'small')
                + o(x.viTri, 'small text-muted')
                + '<td><span class="badge rounded-pill" style="' + mauTinhTrang(x.tinhTrang) + '">'
                + escapeHtml(x.tinhTrang || 'Chưa rõ') + '</span></td>'
                + oHanBaoHanh(x.hanBaoHanh)
                + '<td class="ccdc-thaotac">'
                + '<button type="button" class="btn btn-sm btn-light border me-1 btn-sua" title="Sửa"><i class="fas fa-pen text-primary"></i></button>'
                + '<button type="button" class="btn btn-sm btn-light border btn-xoa" title="Xoá"><i class="fas fa-trash text-danger"></i></button>'
                + '</td>'
                + '</tr>';
        }).join('');

        var $body = $('#cameraTableBody').html(html);

        // Gắn dữ liệu bằng .data() thay vì nhét JSON vào thuộc tính HTML
        $body.find('tr').each(function (i) {
            $(this).data('camera', data[i]);
        });
    }

    function anLoi($khung) { $khung.addClass('d-none').text(''); }
    function hienLoi($khung, noiDung) { $khung.removeClass('d-none').text(noiDung); }

    function moModalThem() {
        $('#formCamera')[0].reset();
        $('#IdCamera').val(0);
        $('#modalCameraTitle').text('Thêm camera');
        anLoi($('#cameraLoi'));
        locBoPhanTheoCongTy($('#IdcongTy'), $('#IdboPhan'));
        modalCamera.show();
    }

    function moModalSua(x) {
        $('#IdCamera').val(x.idCamera);
        $('#MaCamera').val(x.maCamera || '');
        $('#TenCamera').val(x.tenCamera || '');
        $('#DiaChiIp').val(x.diaChiIp || '');
        $('#DauGhi').val(x.dauGhi || '');
        $('#Kenh').val(x.kenh === null ? '' : x.kenh);
        $('#HangSanXuat').val(x.hangSanXuat || '');
        $('#Model').val(x.model || '');
        $('#Serial').val(x.serial || '');
        $('#IdcongTy').val(x.idcongTy || '');
        locBoPhanTheoCongTy($('#IdcongTy'), $('#IdboPhan'));
        $('#IdboPhan').val(x.idboPhan || '');
        $('#ViTri').val(x.viTri || '');
        $('#TinhTrang').val(x.tinhTrang || $('#TinhTrang option:first').val());
        $('#NgayLapDat').val(chuanHoaNgay(x.ngayLapDat));
        $('#HanBaoHanh').val(chuanHoaNgay(x.hanBaoHanh));
        $('#GhiChu').val(x.ghiChu || '');
        $('#modalCameraTitle').text('Sửa camera');
        anLoi($('#cameraLoi'));
        modalCamera.show();
    }

    function luuCamera() {
        var $loi = $('#cameraLoi');
        if (!$('#TenCamera').val().trim()) {
            hienLoi($loi, 'Vui lòng nhập tên camera.');
            $('#TenCamera').focus();
            return;
        }

        anLoi($loi);
        var $btn = $('#btnLuuCamera').prop('disabled', true); // chặn double-submit tạo bản ghi trùng

        $.post('/QLCamera/Save', $('#formCamera').serialize())
            .done(function (res) {
                if (res.thanhCong) {
                    modalCamera.hide();
                    taiDanhSach();
                } else {
                    hienLoi($loi, res.thongBao || 'Lưu không thành công.');
                }
            })
            .fail(function () { hienLoi($loi, 'Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function xacNhanXoa() {
        var $loi = $('#xoaLoi');
        anLoi($loi);
        var $btn = $('#btnXacNhanXoaCamera').prop('disabled', true);

        $.post('/QLCamera/Delete', { id: $('#xoaIdCamera').val(), lyDo: $('#xoaLyDo').val() })
            .done(function (res) {
                if (res.thanhCong) {
                    modalXoa.hide();
                    taiDanhSach();
                } else {
                    hienLoi($loi, res.thongBao || 'Xoá không thành công.');
                }
            })
            .fail(function () { hienLoi($loi, 'Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    $(function () {
        modalCamera = new bootstrap.Modal(document.getElementById('modalCamera'));
        modalXoa = new bootstrap.Modal(document.getElementById('modalXoaCamera'));

        $('#btnThemCamera').on('click', moModalThem);
        $('#btnLuuCamera').on('click', luuCamera);
        $('#btnXacNhanXoaCamera').on('click', xacNhanXoa);
        $('#btnTaiLai').on('click', taiDanhSach);

        // Enter trong form không được submit đồng bộ (reload trang)
        $('#formCamera').on('submit', function (e) { e.preventDefault(); luuCamera(); });

        $('#IdcongTy').on('change', function () { locBoPhanTheoCongTy($(this), $('#IdboPhan')); });
        $('#filterCongTy').on('change', function () {
            locBoPhanTheoCongTy($(this), $('#filterBoPhan'));
            taiDanhSach();
        });
        $('#filterBoPhan, #filterTinhTrang, #filterTrucTuyen').on('change', taiDanhSach);

        // Gõ tới đâu lọc tới đó, hoãn 350ms cho đỡ dội request
        $('#filterTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(taiDanhSach, 350);
        });

        $('#cameraTableBody').on('click', '.btn-sua', function () {
            moModalSua($(this).closest('tr').data('camera'));
        });

        $('#cameraTableBody').on('click', '.btn-xoa', function () {
            var x = $(this).closest('tr').data('camera');
            $('#xoaIdCamera').val(x.idCamera);
            $('#xoaTenCamera').text(x.tenCamera);
            $('#xoaLyDo').val('');
            anLoi($('#xoaLoi'));
            modalXoa.show();
        });

        // Tab tài sản không phải tab mặc định: chỉ tải (và ping) khi người dùng mở lần đầu
        var daTai = false;
        $('#tabTaiSanBtn').on('shown.bs.tab', function () {
            if (daTai) return;
            daTai = true;
            taiDanhSach();
        });
    });
})();
