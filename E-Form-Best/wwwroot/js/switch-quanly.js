// Màn hình Quản lý Switch — /QLSwitch/{công ty}: tab "Danh sách switch" (KK_Switch) + tab "Lịch sử" online/offline.
// Toàn bộ dữ liệu lấy qua AJAX từ QLSwitchController, view chỉ dựng khung.
(function () {
    'use strict';

    var COT = 15;
    var congTy, chiXem, goc;
    var modalSw, modalXoa;
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
        var tu = x.doiTrangThaiLuc ? 'Từ ' + gio(x.doiTrangThaiLuc) : '';
        if (x.ketNoi === 'UP') return '<td class="small text-success fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#22c55e;"></span>Online</td>';
        if (x.ketNoi === 'DOWN') {
            var da = x.doiTrangThaiLuc ? thoiLuong((Date.now() - new Date(x.doiTrangThaiLuc).getTime()) / 1000) : '';
            return '<td class="small text-danger fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#ef4444;"></span>Mất KN'
                + (da ? ' <span class="fw-normal">· ' + esc(da) + '</span>' : '') + '</td>';
        }
        return '<td class="small text-muted" title="Job ping chưa kiểm tới switch này"><span class="cam-dot" style="background:#facc15;"></span>Chưa kiểm</td>';
    }

    // Hạn bảo hành đã qua thì tô đỏ để thấy ngay switch nào hết bảo hành
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
        var $body = $('#swTableBody');
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
                $('#badgeMatKetNoi').text(res.tongOffline).toggleClass('d-none', !res.tongOffline);
                veTinhTrangPing(res.kiemTraGanNhat, res.chuKyGiay, res.duLieu);
            })
            .fail(function () { dongTrangThai($body, COT, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    // Lần ping gần nhất quá 3 chu kỳ: job nền không chạy ở máy chủ này -> trạng thái đang hiện là cũ
    function veTinhTrangPing(kiemTra, chuKyGiay, ds) {
        var $canhBao = $('#swCanhBaoPing');
        $('#kiemTraGanNhat').text(kiemTra ? '(kiểm lúc ' + gio(kiemTra) + ')' : '');
        var coTheoDoi = (ds || []).some(function (x) { return x.theoDoi; });
        var cu = kiemTra && (Date.now() - new Date(kiemTra).getTime()) > 3 * chuKyGiay * 1000;
        var noiDung = !coTheoDoi ? ''
            : !kiemTra ? 'Chưa có lần ping nào — job theo dõi switch chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (Switch:GhiLichSu).'
            : cu ? 'Lần ping gần nhất lúc ' + gio(kiemTra) + ' — trạng thái kết nối có thể đã cũ (job theo dõi switch đang tắt hoặc lỗi).'
            : '';
        $canhBao.text(noiDung).toggleClass('d-none', !noiDung);
    }

    function veBang(data) {
        var $body = $('#swTableBody');
        if (!data || data.length === 0) {
            dongTrangThai($body, COT, 'Chưa có switch nào khớp bộ lọc.', 'text-muted');
            return;
        }

        $body.html(data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + oKetNoi(x)
                + o(x.maSwitch, 'small')
                + o(x.tenSwitch, 'ccdc-ten')
                + o(x.diaChiIp, 'small font-monospace')
                + o(x.diaChiMac, 'small font-monospace')
                + o(hangModel, 'small text-muted')
                // Số client chỉ có nghĩa khi switch đang online
                + o(x.soCong, 'small text-end')
                + o(x.vaiTro, 'small')
                + o(x.phienBan, 'small text-muted')
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
        $body.find('tr').each(function (i) { $(this).data('sw', data[i]); });
    }

    // ---------------- Thêm / sửa / xoá ----------------

    function anLoi($khung) { $khung.addClass('d-none').text(''); }
    function hienLoi($khung, noiDung) { $khung.removeClass('d-none').text(noiDung); }

    function moModalThem() {
        $('#formSw')[0].reset();
        $('#IdSwitch').val(0);
        $('#modalSwTitle').text('Thêm Switch ' + congTy);
        anLoi($('#swLoi'));
        modalSw.show();
    }

    function moModalSua(x) {
        $('#IdSwitch').val(x.idSwitch);
        $('#MaSwitch').val(x.maSwitch || '');
        $('#TenSwitch').val(x.tenSwitch || '');
        $('#DiaChiIp').val(x.diaChiIp || '');
        $('#DiaChiMac').val(x.diaChiMac || '');
        $('#VaiTro').val(x.vaiTro || '');
        $('#SoCong').val(x.soCong || '');
        $('#PhienBan').val(x.phienBan || '');
        $('#HangSanXuat').val(x.hangSanXuat || '');
        $('#Model').val(x.model || '');
        $('#Serial').val(x.serial || '');
        $('#IdboPhan').val(x.idboPhan || '');
        $('#ViTri').val(x.viTri || '');
        $('#TinhTrang').val(x.tinhTrang || $('#TinhTrang option:first').val());
        $('#NgayLapDat').val(chuanHoaNgay(x.ngayLapDat));
        $('#HanBaoHanh').val(chuanHoaNgay(x.hanBaoHanh));
        $('#GhiChu').val(x.ghiChu || '');
        $('#modalSwTitle').text('Sửa Switch ' + congTy);
        anLoi($('#swLoi'));
        modalSw.show();
    }

    function luuSw() {
        var $loi = $('#swLoi');
        if (!$('#TenSwitch').val().trim()) {
            hienLoi($loi, 'Vui lòng nhập tên switch.');
            $('#TenSwitch').focus();
            return;
        }

        anLoi($loi);
        var $btn = $('#btnLuuSw').prop('disabled', true); // chặn double-submit tạo bản ghi trùng

        $.post(goc + '/Luu', $('#formSw').serialize())
            .done(function (res) {
                if (res.thanhCong) {
                    modalSw.hide();
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
        var $btn = $('#btnXacNhanXoaSw').prop('disabled', true);

        $.post(goc + '/Xoa', { id: $('#xoaIdSwitch').val(), lyDo: $('#xoaLyDo').val() })
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
                $('#lsSoSwitch').text(res.soSwitch);
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
                        + o(x.tenSwitch, 'ccdc-ten')
                        + o(x.diaChiIp, 'small font-monospace')
                        + '<td class="small">' + (x.thoiLuongGiay ? esc(thoiLuong(x.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 6, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    $(function () {
        var trang = document.getElementById('swTrang');
        congTy = trang.getAttribute('data-cong-ty');
        chiXem = trang.getAttribute('data-chi-xem') === '1';
        goc = '/QLSwitch/' + encodeURIComponent(congTy);

        if (!chiXem) {
            modalSw = new bootstrap.Modal(document.getElementById('modalSw'));
            modalXoa = new bootstrap.Modal(document.getElementById('modalXoaSw'));
            $('#btnThemSw').on('click', moModalThem);
            $('#btnLuuSw').on('click', luuSw);
            $('#btnXacNhanXoaSw').on('click', xacNhanXoa);
            // Enter trong form không được submit đồng bộ (reload trang)
            $('#formSw').on('submit', function (e) { e.preventDefault(); luuSw(); });
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

        $(document).on('click', '#swTableBody .btn-sua', function () {
            moModalSua($(this).closest('tr').data('sw'));
        });
        $(document).on('click', '#swTableBody .btn-xoa', function () {
            var x = $(this).closest('tr').data('sw');
            $('#xoaIdSwitch').val(x.idSwitch);
            $('#xoaTenSwitch').text(x.tenSwitch);
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
