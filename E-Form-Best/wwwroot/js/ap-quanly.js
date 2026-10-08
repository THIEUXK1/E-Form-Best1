// Màn hình Quản lý AP Wi-Fi — /QLAP/{công ty}: tab "Danh sách AP" (KK_AccessPoint) + tab "Lịch sử" online/offline.
// Toàn bộ dữ liệu lấy qua AJAX từ QLAPController, view chỉ dựng khung.
(function () {
    'use strict';

    var COT = 15;
    var congTy, chiXem, goc;
    var modalAp, modalXoa;
    var timerTimKiem = null, timerLs = null;

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // API trả DateOnly dạng "2026-10-06" — cắt phần giờ nếu có để hợp input type=date
    function chuanHoaNgay(v) { return v ? String(v).substring(0, 10) : ''; }

    function dinhDangNgay(v) {
        var p = chuanHoaNgay(v).split('-');
        return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : '';
    }

    // "2026-10-08T14:21:07" -> "14:21 08/10/2026"
    function gio(s) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(s || '');
        return m ? m[4] + ':' + m[5] + ' ' + m[3] + '/' + m[2] + '/' + m[1] : '';
    }

    // yyyy-MM-dd theo giờ máy (không dùng toISOString: lệch ngày do UTC)
    function ngayInput(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }

    // Giây -> "3 ngày 4 giờ" / "2 giờ 15 phút" / "12 phút"
    function thoiLuong(giay) {
        giay = Math.max(0, Math.round(giay || 0));
        var d = Math.floor(giay / 86400), h = Math.floor(giay % 86400 / 3600), p = Math.floor(giay % 3600 / 60);
        if (d > 0) return d + ' ngày' + (h ? ' ' + h + ' giờ' : '');
        if (h > 0) return h + ' giờ' + (p ? ' ' + p + ' phút' : '');
        return p + ' phút';
    }

    // Ô bị cắt bằng ellipsis nên gắn title để rê chuột xem đủ nội dung
    function o(giaTri, className) {
        var s = esc(giaTri);
        return '<td class="' + className + '" title="' + s + '">' + s + '</td>';
    }

    function dongTrangThai($body, soCot, noiDung, className) {
        $body.html('<tr><td colspan="' + soCot + '" class="ccdc-trangthai ' + className + ' py-4">' + esc(noiDung) + '</td></tr>');
    }

    function mauTinhTrang(tt) {
        switch (tt) {
            case 'Đang hoạt động': return 'background:#dcfce7; color:#166534;';
            case 'Hư hỏng': return 'background:#fee2e2; color:#991b1b;';
            case 'Ngừng sử dụng': return 'background:#e2e8f0; color:#475569;';
            default: return 'background:#dbeafe; color:#1e40af;'; // Trong kho
        }
    }

    function oKetNoi(x) {
        if (!x.theoDoi) {
            var lyDo = x.diaChiIp ? 'Không ping vì tình trạng "' + (x.tinhTrang || '') + '"' : 'Chưa khai IP';
            return '<td class="small text-muted" title="' + esc(lyDo) + '"><span class="cam-dot" style="background:#cbd5e1;"></span>Không theo dõi</td>';
        }
        // Tooltip: từ lúc nào + nguồn trạng thái (controller hay ping) + trạng thái gốc controller trả, uptime
        var tu = [
            x.doiTrangThaiLuc ? 'Từ ' + gio(x.doiTrangThaiLuc) : '',
            x.nguonTrangThai === 'AC' ? 'Theo controller: ' + (x.trangThaiController || '?') : x.nguonTrangThai === 'PING' ? 'Theo ping' : '',
            x.nguonTrangThai === 'AC' && x.ketNoi === 'UP' && x.thoiGianChayGiay ? 'Chạy liên tục ' + thoiLuong(x.thoiGianChayGiay) : '',
            x.phienBan ? 'Firmware ' + x.phienBan : ''
        ].filter(Boolean).join(' · ');
        if (x.ketNoi === 'UP') return '<td class="small text-success fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#22c55e;"></span>Online</td>';
        if (x.ketNoi === 'DOWN') {
            var da = x.doiTrangThaiLuc ? thoiLuong((Date.now() - new Date(x.doiTrangThaiLuc).getTime()) / 1000) : '';
            // Controller vẫn thấy AP nhưng không "normal" (fault, idle...): ghi rõ trạng thái gốc
            var ttGoc = x.nguonTrangThai === 'AC' && x.trangThaiController && x.trangThaiController !== 'normal'
                ? ' <span class="fw-normal">(' + esc(x.trangThaiController) + ')</span>' : '';
            return '<td class="small text-danger fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#ef4444;"></span>Mất KN' + ttGoc
                + (da ? ' <span class="fw-normal">· ' + esc(da) + '</span>' : '') + '</td>';
        }
        return '<td class="small text-muted" title="Job ping chưa kiểm tới AP này"><span class="cam-dot" style="background:#facc15;"></span>Chưa kiểm</td>';
    }

    // Hạn bảo hành đã qua thì tô đỏ để thấy ngay AP nào hết bảo hành
    function oHanBaoHanh(v) {
        if (!v) return '<td></td>';
        var hetHan = chuanHoaNgay(v) < ngayInput(new Date());
        return '<td class="small ' + (hetHan ? 'text-danger fw-bold' : '') + '" title="' + (hetHan ? 'Đã hết bảo hành' : '') + '">'
            + dinhDangNgay(v) + '</td>';
    }

    // ---------------- Danh sách ----------------

    function thamSoLoc() {
        return {
            tuKhoa: $('#filterTuKhoa').val(),
            idBoPhan: $('#filterBoPhan').val(),
            tinhTrang: $('#filterTinhTrang').val(),
            ketNoi: $('#filterKetNoi').val()
        };
    }

    function taiDanhSach() {
        var $body = $('#apTableBody');
        dongTrangThai($body, COT, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/DanhSach', thamSoLoc())
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, COT, res.thongBao || 'Không tải được dữ liệu.', 'text-danger');
                    return;
                }
                veBang(res.duLieu);
                $('#tongDong').text(res.tongDong);
                $('#tongOnline').text(res.tongOnline);
                $('#tongOffline').text(res.tongOffline);
                $('#tongClient').text(res.tongClient || 0);
                $('#badgeMatKetNoi').text(res.tongOffline).toggleClass('d-none', !res.tongOffline);
                veTinhTrangPing(res.kiemTraGanNhat, res.chuKyGiay, res.duLieu);
            })
            .fail(function () { dongTrangThai($body, COT, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    // Lần ping gần nhất quá 3 chu kỳ: job nền không chạy ở máy chủ này -> trạng thái đang hiện là cũ
    function veTinhTrangPing(kiemTra, chuKyGiay, ds) {
        var $canhBao = $('#apCanhBaoPing');
        $('#kiemTraGanNhat').text(kiemTra ? '(kiểm lúc ' + gio(kiemTra) + ')' : '');
        var coTheoDoi = (ds || []).some(function (x) { return x.theoDoi; });
        var cu = kiemTra && (Date.now() - new Date(kiemTra).getTime()) > 3 * chuKyGiay * 1000;
        var noiDung = !coTheoDoi ? ''
            : !kiemTra ? 'Chưa có lần ping nào — job theo dõi AP chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (AccessPoint:GhiLichSu).'
            : cu ? 'Lần ping gần nhất lúc ' + gio(kiemTra) + ' — trạng thái kết nối có thể đã cũ (job theo dõi AP đang tắt hoặc lỗi).'
            : '';
        $canhBao.text(noiDung).toggleClass('d-none', !noiDung);
    }

    function veBang(data) {
        var $body = $('#apTableBody');
        if (!data || data.length === 0) {
            dongTrangThai($body, COT, 'Chưa có AP nào khớp bộ lọc.', 'text-muted');
            return;
        }

        $body.html(data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + oKetNoi(x)
                + o(x.maAp, 'small')
                + o(x.tenAp, 'ccdc-ten')
                + o(x.diaChiIp, 'small font-monospace')
                + o(x.diaChiMac, 'small font-monospace')
                + o(hangModel, 'small text-muted')
                // Số client chỉ có nghĩa khi AP đang online
                + o(x.ketNoi === 'UP' && x.soClient !== null && x.soClient !== undefined ? x.soClient : '', 'small text-end')
                + o(x.nhomAp, 'small text-muted')
                + o(x.ssid, 'small')
                + o(x.tenBoPhan, 'small')
                + o(x.viTri, 'small text-muted')
                + '<td><span class="badge rounded-pill" style="' + mauTinhTrang(x.tinhTrang) + '">'
                + esc(x.tinhTrang || 'Chưa rõ') + '</span></td>'
                + oHanBaoHanh(x.hanBaoHanh)
                + '<td class="ccdc-thaotac">'
                + (chiXem ? '' :
                    '<button type="button" class="btn btn-sm btn-light border me-1 btn-sua" title="Sửa"><i class="fas fa-pen text-primary"></i></button>'
                  + '<button type="button" class="btn btn-sm btn-light border btn-xoa" title="Xoá"><i class="fas fa-trash text-danger"></i></button>')
                + '</td>'
                + '</tr>';
        }).join(''));

        // Gắn dữ liệu bằng .data() thay vì nhét JSON vào thuộc tính HTML
        $body.find('tr').each(function (i) { $(this).data('ap', data[i]); });
    }

    // ---------------- Thêm / sửa / xoá ----------------

    function anLoi($khung) { $khung.addClass('d-none').text(''); }
    function hienLoi($khung, noiDung) { $khung.removeClass('d-none').text(noiDung); }

    function moModalThem() {
        $('#formAp')[0].reset();
        $('#IdAp').val(0);
        $('#modalApTitle').text('Thêm AP ' + congTy);
        anLoi($('#apLoi'));
        modalAp.show();
    }

    function moModalSua(x) {
        $('#IdAp').val(x.idAp);
        $('#MaAp').val(x.maAp || '');
        $('#TenAp').val(x.tenAp || '');
        $('#DiaChiIp').val(x.diaChiIp || '');
        $('#DiaChiMac').val(x.diaChiMac || '');
        $('#TenController').val(x.tenController || '');
        $('#HangSanXuat').val(x.hangSanXuat || '');
        $('#Model').val(x.model || '');
        $('#Serial').val(x.serial || '');
        $('#Ssid').val(x.ssid || '');
        $('#IdboPhan').val(x.idboPhan || '');
        $('#ViTri').val(x.viTri || '');
        $('#TinhTrang').val(x.tinhTrang || $('#TinhTrang option:first').val());
        $('#NgayLapDat').val(chuanHoaNgay(x.ngayLapDat));
        $('#HanBaoHanh').val(chuanHoaNgay(x.hanBaoHanh));
        $('#GhiChu').val(x.ghiChu || '');
        $('#modalApTitle').text('Sửa AP ' + congTy);
        anLoi($('#apLoi'));
        modalAp.show();
    }

    function luuAp() {
        var $loi = $('#apLoi');
        if (!$('#TenAp').val().trim()) {
            hienLoi($loi, 'Vui lòng nhập tên AP.');
            $('#TenAp').focus();
            return;
        }

        anLoi($loi);
        var $btn = $('#btnLuuAp').prop('disabled', true); // chặn double-submit tạo bản ghi trùng

        $.post(goc + '/Luu', $('#formAp').serialize())
            .done(function (res) {
                if (res.thanhCong) {
                    modalAp.hide();
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
        var $btn = $('#btnXacNhanXoaAp').prop('disabled', true);

        $.post(goc + '/Xoa', { id: $('#xoaIdAp').val(), lyDo: $('#xoaLyDo').val() })
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

    // Đọc ngay danh sách AP trên controller; kết quả báo tại chỗ bằng ô cảnh báo trên bảng
    function dongBoController() {
        var $btn = $('#btnDongBoController').prop('disabled', true);
        var $icon = $btn.find('i').addClass('fa-spin');
        var $thongBao = $('#apCanhBaoPing');

        $.post(goc + '/DongBoController')
            .done(function (res) {
                $thongBao.removeClass('d-none alert-warning alert-danger alert-success')
                    .addClass(res.thanhCong ? 'alert-success' : 'alert-danger')
                    .text(res.thongBao || (res.thanhCong ? 'Đã đồng bộ.' : 'Đồng bộ không thành công.'));
                if (res.thanhCong) taiDanhSach();
            })
            .fail(function () {
                $thongBao.removeClass('d-none alert-warning alert-success').addClass('alert-danger').text('Lỗi kết nối máy chủ.');
            })
            .always(function () { $icon.removeClass('fa-spin'); $btn.prop('disabled', false); });
    }

    // ---------------- Lịch sử ----------------

    function taiLichSu() {
        var $body = $('#lsBody');
        dongTrangThai($body, 6, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/LichSu', {
            tuNgay: $('#lsTuNgay').val(),
            denNgay: $('#lsDenNgay').val(),
            loai: $('#lsLoai').val(),
            tuKhoa: $('#lsTuKhoa').val()
        })
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, 6, res.thongBao || 'Không tải được lịch sử.', 'text-danger');
                    return;
                }
                $('#lsTong').text(res.tong);
                $('#lsDown').text(res.soDown);
                $('#lsUp').text(res.soUp);
                $('#lsSoAp').text(res.soAp);
                $('#lsGhiChu').text(res.biCat ? '(chỉ hiện ' + res.tong + ' sự kiện mới nhất, thu hẹp khoảng ngày để xem đủ)' : '');

                if (!res.duLieu.length) {
                    dongTrangThai($body, 6, 'Không có sự kiện đổi trạng thái nào trong khoảng này.', 'text-muted');
                    return;
                }
                $body.html(res.duLieu.map(function (x, i) {
                    var xuong = x.sangTrangThai === 'DOWN';
                    return '<tr>'
                        + '<td class="text-muted small">' + (i + 1) + '</td>'
                        + '<td class="small text-nowrap">' + esc(gio(x.thoiGian)) + '</td>'
                        + (xuong
                            ? '<td class="small text-danger fw-bold"><i class="fas fa-arrow-down me-1"></i>Mất kết nối</td>'
                            : '<td class="small text-success fw-bold"><i class="fas fa-arrow-up me-1"></i>Hoạt động lại</td>')
                        + o(x.tenAp, 'ccdc-ten')
                        + o(x.diaChiIp, 'small font-monospace')
                        + '<td class="small">' + (x.thoiLuongGiay ? esc(thoiLuong(x.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 6, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    $(function () {
        var trang = document.getElementById('apTrang');
        congTy = trang.getAttribute('data-cong-ty');
        chiXem = trang.getAttribute('data-chi-xem') === '1';
        goc = '/QLAP/' + encodeURIComponent(congTy);

        if (!chiXem) {
            modalAp = new bootstrap.Modal(document.getElementById('modalAp'));
            modalXoa = new bootstrap.Modal(document.getElementById('modalXoaAp'));
            $('#btnThemAp').on('click', moModalThem);
            $('#btnLuuAp').on('click', luuAp);
            $('#btnXacNhanXoaAp').on('click', xacNhanXoa);
            $('#btnDongBoController').on('click', dongBoController);
            // Enter trong form không được submit đồng bộ (reload trang)
            $('#formAp').on('submit', function (e) { e.preventDefault(); luuAp(); });
        }

        $('#btnTaiLai').on('click', taiDanhSach);
        $('#filterBoPhan, #filterTinhTrang, #filterKetNoi').on('change', taiDanhSach);
        // Gõ tới đâu lọc tới đó, hoãn 350ms cho đỡ dội request
        $('#filterTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(taiDanhSach, 350);
        });

        // Tải file là điều hướng hợp lệ (trình duyệt giữ nguyên trang, chỉ tải file về)
        $('#btnXuatExcel').on('click', function (e) {
            e.preventDefault();
            window.location.href = goc + '/XuatExcel?' + $.param(thamSoLoc());
        });

        $(document).on('click', '#apTableBody .btn-sua', function () {
            moModalSua($(this).closest('tr').data('ap'));
        });
        $(document).on('click', '#apTableBody .btn-xoa', function () {
            var x = $(this).closest('tr').data('ap');
            $('#xoaIdAp').val(x.idAp);
            $('#xoaTenAp').text(x.tenAp);
            $('#xoaLyDo').val('');
            anLoi($('#xoaLoi'));
            modalXoa.show();
        });

        // Lịch sử: mặc định 7 ngày gần nhất, chỉ tải khi mở tab lần đầu
        var homNay = new Date();
        var truoc = new Date(); truoc.setDate(truoc.getDate() - 6);
        $('#lsTuNgay').val(ngayInput(truoc));
        $('#lsDenNgay').val(ngayInput(homNay));
        var daTaiLs = false;
        $('#tabLichSuBtn').on('shown.bs.tab', function () {
            if (daTaiLs) return;
            daTaiLs = true;
            taiLichSu();
        });
        $('#lsTuNgay, #lsDenNgay, #lsLoai').on('change', taiLichSu);
        $('#lsBtnTaiLai').on('click', taiLichSu);
        $('#lsTuKhoa').on('input', function () {
            clearTimeout(timerLs);
            timerLs = setTimeout(taiLichSu, 350);
        });

        taiDanhSach();
        // Trạng thái do job nền ping 2 phút/lần: tự làm mới danh sách theo nhịp đó khi tab đang mở
        setInterval(function () {
            if (!document.hidden && $('#paneDanhSach').hasClass('active') && !$('.modal.show').length) taiDanhSach();
        }, 120000);
    });
})();
