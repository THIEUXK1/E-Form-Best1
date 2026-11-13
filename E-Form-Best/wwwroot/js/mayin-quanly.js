// Màn hình Quản lý máy in — /QLMayIn
// View chỉ dựng khung, toàn bộ dữ liệu lấy qua AJAX từ QLMayInController.
// Token chống CSRF do antiforgery-ajax.js tự gắn cho mọi request khác GET.
(function () {
    'use strict';

    var modalMayIn, modalChiSoTay;
    var timerTimKiem = null;

    // Dữ liệu vừa tải về, giữ lại để lọc nhanh bằng thẻ số mà không phải gọi lại máy chủ
    var duLieuGoc = [];
    var locNhanh = '';
    var dangXemLuuTru = false; // true = bảng đang hiện các máy đã cất vào Lưu trữ

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function dinhDangSo(v) {
        if (v === null || v === undefined || v === '') return '—';
        return Number(v).toLocaleString('vi-VN');
    }

    // Dòng tách In / Copy / Scan dưới số tổng. Số tổng = In + Copy (Scan không tốn giấy nên
    // không cộng vào). Máy đời cũ / nhập tay không có cột tách thì bỏ dòng, chỉ còn số tổng.
    function oTach(t) {
        if (!t) return '';
        var dong = [['In', t.in], ['Copy', t.copy], ['Scan', t.scan]]
            .filter(function (p) { return p[1] !== null && p[1] !== undefined; })
            .map(function (p) {
                return '<div><span class="mayin-tach-nhan">' + p[0] + '</span> ' + dinhDangSo(p[1]) + '</div>';
            });
        return dong.length ? '<div class="mayin-tach">' + dong.join('') + '</div>' : '';
    }

    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + (className || '') + '" title="' + s + '">' + s + '</td>';
    }

    // Ô gộp 2 cột thành 2 dòng (bảng 16 cột cũ phải kéo ngang). Mỗi dòng tự cắt "…", rê chuột xem đủ.
    function dong(html, title, className) {
        return '<div class="mayin-dong ' + (className || '') + '"' + (title ? ' title="' + title + '"' : '') + '>' + html + '</div>';
    }

    function dongChu(giaTri, className) {
        var s = escapeHtml(giaTri);
        return dong(s || '<span class="text-muted">—</span>', s, className);
    }

    function nhanTrangThai(tt) {
        switch (tt) {
            case 'TamDung': return '<span class="badge rounded-pill bg-warning-subtle text-warning-emphasis">Tạm dừng</span>';
            case 'BaoPhe': return '<span class="badge rounded-pill bg-secondary-subtle text-secondary-emphasis">Báo phế</span>';
            default: return '<span class="badge rounded-pill bg-success-subtle text-success-emphasis">Hoạt động</span>';
        }
    }

    // Thanh mực/trống: xanh > 50%, vàng 21-50%, đỏ ≤ 20%
    function thanhMuc(phanTram) {
        if (phanTram === null || phanTram === undefined) return '<span class="text-muted small">—</span>';
        var lop = phanTram > 50 ? 'muc-du' : (phanTram > 20 ? 'muc-vua' : 'muc-can');
        return '<div class="d-flex align-items-center gap-2">'
            + '<div class="mayin-thanh"><span class="' + lop + '" style="width:' + Math.max(0, Math.min(100, phanTram)) + '%"></span></div>'
            + '<span class="small text-muted">' + phanTram + '%</span>'
            + '</div>';
    }

    // Máy in màu báo TONER_C/M/Y/K, DRUM_C/M/Y/K. Cột trong bảng hẹp nên hiện thanh của màu THẤP
    // NHẤT (số máy chủ đã tính sẵn) kèm dãy chấm màu, rê chuột ra phần trăm từng màu.
    var MAU_VAT_TU = { C: '#0891b2', M: '#db2777', Y: '#ca8a04', K: '#334155' };
    var TEN_VAT_TU = { C: 'Xanh (C)', M: 'Hồng (M)', Y: 'Vàng (Y)', K: 'Đen (K)' };

    // Hộp mực liền trống (DocuPrint 3105/3205): server gắn dòng LIENTRONG_x, trống thay cùng hộp mực
    function coLienTrong(dsVatTu) {
        return (dsVatTu || []).some(function (v) { return (v.ten || '').toUpperCase().indexOf('LIENTRONG') === 0; });
    }

    function oNhomVatTu(phanTram, dsVatTu, tienTo) {
        var nhom = (dsVatTu || []).filter(function (v) {
            return (v.ten || '').toUpperCase().indexOf(tienTo) === 0;
        });

        if (nhom.length <= 1) return thanhMuc(phanTram);

        var cham = nhom.map(function (v) {
            var k = /_([CMYK])$/.exec(v.ten || '');
            k = k ? k[1] : null;
            var nhan = (k ? TEN_VAT_TU[k] : v.ten) + ' ' + v.phanTram + '%';
            return '<span class="mayin-cham-muc" title="' + escapeHtml(nhan) + '" style="background:'
                + (k ? MAU_VAT_TU[k] : '#64748b') + '; opacity:' + (v.phanTram <= 20 ? '1' : '.55') + ';"></span>';
        }).join('');

        return thanhMuc(phanTram) + '<div class="d-flex gap-1 mt-1">' + cham + '</div>';
    }

    /// Chạy một lệnh đồng bộ hàng loạt: khoá cả hai nút (máy chủ chạy tuần tự qua vài chục máy,
    /// bấm chồng lên nhau chỉ làm nghẽn), hiện kết quả tại chỗ rồi tải lại danh sách.
    function dongBoTatCa($nut, duongDan, chuDangChay, chuBanDau) {
        var $cacNut = $('#btnDocTatCa, #btnNapLichSuTatCa');
        var batDau = Date.now();

        $cacNut.prop('disabled', true);
        $nut.html('<i class="fa fa-spinner fa-spin me-1"></i> ' + chuDangChay);
        $('#kqDongBo').removeClass('text-success text-danger').addClass('text-muted')
            .text('Đang chạy, mỗi máy mất vài giây — đừng đóng trang.');

        $.post(duongDan)
            .done(function (res) {
                var giay = Math.round((Date.now() - batDau) / 1000);
                $('#kqDongBo')
                    .removeClass('text-muted text-success text-danger')
                    .addClass(res.thanhCong ? 'text-success' : 'text-danger')
                    .text((res.thongBao || '') + ' (' + giay + ' giây)');

                taiDanhSach();
            })
            .fail(function () {
                $('#kqDongBo').removeClass('text-muted text-success').addClass('text-danger')
                    .text('Lỗi kết nối máy chủ.');
            })
            .always(function () {
                $cacNut.prop('disabled', false);
                $nut.html(chuBanDau);
            });
    }

    // Hiện nút làm hàng loạt kèm số máy đã tích; ở mục Lưu trữ nút đổi thành "Đưa về bảng chính"
    function capNhatDaChon() {
        var soChon = $('.js-chon-may:checked').length;
        var tong = $('.js-chon-may').length;
        $('#soDaChon').text(soChon);
        $('.js-chu-da-chon').text(dangXemLuuTru ? 'Đưa về bảng chính' : 'Cất vào Lưu trữ');
        $('#btnLuuTruDaChon').toggleClass('d-none', soChon === 0)
            .toggleClass('btn-warning', !dangXemLuuTru).toggleClass('btn-success', dangXemLuuTru);
        $('#chonTatCa').prop('checked', tong > 0 && soChon === tong).prop('indeterminate', soChon > 0 && soChon < tong);
    }

    function taiDanhSach() {
        var thamSo = {
            tuKhoa: $('#filterTuKhoa').val(),
            boPhan: $('#filterBoPhan').val(),
            model: $('#filterModel').val(),
            trangThai: $('#filterTrangThai').val(),
            luuTru: dangXemLuuTru
        };

        $('#mayinTableBody').html('<tr><td colspan="13" class="text-center text-muted py-4">Đang tải dữ liệu...</td></tr>');

        $.getJSON('/QLMayIn/GetDanhSach', thamSo)
            .done(function (res) {
                if (!res.thanhCong) {
                    $('#mayinTableBody').html('<tr><td colspan="13" class="text-center text-danger py-4">'
                        + escapeHtml(res.thongBao || 'Không tải được dữ liệu') + '</td></tr>');
                    return;
                }
                duLieuGoc = res.duLieu || [];
                veBang(apDungLocNhanh(duLieuGoc));
                $('#soTongMay').text(dinhDangSo(res.tomTat.tongMay));
                $('#soDangChay').text(dinhDangSo(res.tomTat.dangChay));
                $('#soTheoDoi').text(dinhDangSo(res.tomTat.theoDoiTuDong));
                $('#soDangBat').text(dinhDangSo(res.tomTat.dangBat));
                $('#soTonerThap').text(dinhDangSo(res.tomTat.tonerThap));
                $('#soTrangInHomNay').text(dinhDangSo(res.tomTat.trangInHomNay));
                $('#soTrangIn30').text(dinhDangSo(res.tomTat.trangIn30Ngay));
                $('#soLuuTru').text(dinhDangSo(res.tomTat.soLuuTru));
            })
            .fail(function () {
                $('#mayinTableBody').html('<tr><td colspan="13" class="text-center text-danger py-4">Lỗi kết nối máy chủ.</td></tr>');
            });
    }

    // Lọc nhanh theo thẻ số: mọi tiêu chí đều có sẵn trong dữ liệu đã tải nên lọc ngay tại chỗ,
    // không gọi lại máy chủ. Riêng "Đang hoạt động" là bộ lọc thật của truy vấn nên đẩy sang ô chọn.
    function apDungLocNhanh(duLieu) {
        switch (locNhanh) {
            case 'dangBat': return duLieu.filter(function (x) { return x.online; });
            case 'theoDoi': return duLieu.filter(function (x) { return x.theoDoiTuDong; });
            case 'tonerThap': return duLieu.filter(function (x) { return x.toner !== null && x.toner !== undefined && x.toner <= 20; });
            case 'coInHomNay': return duLieu.filter(function (x) { return x.trangInHomNay > 0; });
            case 'coIn30Ngay': return duLieu.filter(function (x) { return x.trangIn30Ngay > 0; });
            default: return duLieu;
        }
    }

    function datLocNhanh(loc) {
        if (loc === 'tatCa') {
            locNhanh = '';
            $('#filterTrangThai').val('');
            $('.mayin-the-so').removeClass('dang-loc');
            taiDanhSach();
            return;
        }

        if (loc === 'hoatDong') {
            locNhanh = '';
            $('.mayin-the-so').removeClass('dang-loc');
            $('.mayin-the-so[data-loc="hoatDong"]').addClass('dang-loc');
            $('#filterTrangThai').val('HoatDong');
            taiDanhSach();
            return;
        }

        // Bấm lại đúng thẻ đang chọn thì bỏ lọc
        locNhanh = (locNhanh === loc) ? '' : loc;
        $('.mayin-the-so').removeClass('dang-loc');
        if (locNhanh) $('.mayin-the-so[data-loc="' + locNhanh + '"]').addClass('dang-loc');
        veBang(apDungLocNhanh(duLieuGoc));
    }

    function veBang(duLieu) {

        if (!duLieu || duLieu.length === 0) {
            $('#mayinTableBody').html('<tr><td colspan="13" class="text-center text-muted py-4">Không có máy in nào khớp bộ lọc.</td></tr>');
            return;
        }

        var html = duLieu.map(function (x, i) {
            // Chấm trạng thái mạng: xanh = ping được, xám = có IP nhưng im, rỗng = không nối mạng
            var ip = x.diaChiIp
                ? '<span class="mayin-cham ' + (x.online ? 'cham-song' : 'cham-tat') + '" title="'
                  + (x.online ? 'Đang bật' : 'Không phản hồi ping') + '"></span>' + escapeHtml(x.diaChiIp)
                : '<span class="text-muted small">USB / không IP</span>';

            var chiSo = x.counterTong === null || x.counterTong === undefined
                ? '<span class="text-muted small">chưa có</span>'
                : dinhDangSo(x.counterTong) + oTach(x.tachChiSo)
                  + '<div class="text-muted" style="font-size:0.7rem;">' + escapeHtml(x.ngayChiSo) + '</div>';

            var nutDoc = x.diaChiIp
                ? '<button class="btn btn-sm btn-outline-primary rounded-pill px-2 js-doc" data-id="' + x.idMayIn + '" title="Đọc chỉ số từ máy"><i class="fa fa-rotate"></i></button>'
                : '<button class="btn btn-sm btn-outline-warning rounded-pill px-2 js-tay" data-id="' + x.idMayIn + '" title="Nhập chỉ số tay"><i class="fa fa-keyboard"></i></button>';

            return '<tr class="js-dong-may" data-id="' + x.idMayIn + '" tabindex="0">'
                + '<td class="js-o-chon" style="cursor:pointer;"><input type="checkbox" class="form-check-input js-chon-may" value="' + x.idMayIn + '" title="Chọn máy #' + x.idMayIn + '"></td>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                // Loại máy do server xác định (đồng hồ màu → danh sách IT khai tay → mực C/M/Y)
                + '<td>' + (x.laMau
                        ? '<span class="mayin-loai mayin-loai-mau" title="Máy in màu">Màu</span>'
                        : '<span class="mayin-loai mayin-loai-dt" title="Máy in đen trắng">Đ/T</span>')
                + '</td>'
                // Mã #id trong DB để đối chiếu nhanh với bảng MayIn khi tra lỗi / soạn script
                + '<td class="small">' + dongChu(x.boPhan, 'fw-bold') + dongChu(x.viTri, 'text-muted')
                    + '<div class="text-muted" style="font-size:.7rem;">#' + x.idMayIn + '</div></td>'
                + '<td class="small">' + dongChu(x.model, 'js-model') + dongChu(x.serial, 'text-muted js-serial') + '</td>'
                + '<td class="small">' + dongChu(x.tenHangDoi) + dong(ip) + '</td>'
                + '<td class="mayin-cot-so small">' + chiSo + '</td>'
                + '<td class="mayin-cot-so small"><span class="fw-bold">' + dinhDangSo(x.trangInHomNay) + '</span>' + oTach(x.tachHomNay) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(x.trangIn7Ngay) + oTach(x.tach7Ngay) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(x.trangIn30Ngay) + oTach(x.tach30Ngay) + '</td>'
                + '<td>'
                + '<div class="mayin-vt"><span class="mayin-vt-nhan">Mực</span><div class="mayin-vt-o">' + oNhomVatTu(x.toner, x.vatTu, 'TONER') + '</div></div>'
                + '<div class="mayin-vt"><span class="mayin-vt-nhan">Trống</span><div class="mayin-vt-o">'
                    + ((x.drum === null || x.drum === undefined) && coLienTrong(x.vatTu)
                        ? '<span class="small text-muted" title="Trống liền trong hộp mực, thay cùng hộp mực">liền mực</span>'
                        : oNhomVatTu(x.drum, x.vatTu, 'DRUM'))
                    + '</div></div>'
                + '</td>'
                + '<td class="mayin-trangthai">' + nhanTrangThai(x.trangThai)
                + dong(escapeHtml(x.lanDocCuoi || '—'), escapeHtml(x.ketQuaDocCuoi), 'small text-muted js-doc-luc') + '</td>'
                + '<td class="text-end text-nowrap">'
                + nutDoc
                + ' <a href="/QLMayIn/May/' + x.idMayIn + '" target="_blank" rel="noopener" class="btn btn-sm btn-outline-secondary rounded-pill px-2" title="Mở trang chi tiết ở tab mới"><i class="fa fa-chart-line"></i></a>'
                + ' <button class="btn btn-sm btn-outline-secondary rounded-pill px-2 js-sua" data-id="' + x.idMayIn + '" title="Sửa thông tin"><i class="fa fa-pen"></i></button>'
                + (dangXemLuuTru
                    ? ' <button class="btn btn-sm btn-outline-success rounded-pill px-2 js-luu-tru" data-id="' + x.idMayIn + '" data-cat="false" title="Đưa về bảng chính'
                        + (x.ngayLuuTru ? ' (cất lúc ' + escapeHtml(x.ngayLuuTru) + (x.nguoiLuuTru ? ' bởi ' + escapeHtml(x.nguoiLuuTru) : '') + ')' : '')
                        + '"><i class="fa fa-rotate-left"></i></button>'
                    : ' <button class="btn btn-sm btn-outline-secondary rounded-pill px-2 js-luu-tru" data-id="' + x.idMayIn + '" data-cat="true" title="Cất vào Lưu trữ (ẩn khỏi bảng chính)"><i class="fa fa-box-archive"></i></button>')
                + '<div class="small js-kq-luu-tru"></div>'
                + '</td>'
                + '</tr>';
        }).join('');

        $('#mayinTableBody').html(html);
        capNhatDaChon();
    }

    // ---------- Đọc chỉ số ----------

    function docMotMay($nut, id) {
        var htmlCu = $nut.html();
        $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i>');

        $.post('/QLMayIn/DocChiSo/' + id)
            .done(function (res) {
                if (res.thanhCong) {
                    taiDanhSach();
                } else {
                    // Lỗi hiện tại chỗ, không nhảy alert che mất bảng
                    $nut.closest('tr').find('.js-doc-luc')
                        .html('<span class="text-danger small" title="' + escapeHtml(res.thongBao) + '">Lỗi đọc</span>');
                    $nut.prop('disabled', false).html(htmlCu);
                }
            })
            .fail(function () {
                $nut.prop('disabled', false).html(htmlCu);
            });
    }



    // ---------- Thêm / sửa ----------

    function moFormMay(may) {
        $('#mayTieuDe').text(may ? 'Sửa máy in' : 'Thêm máy in');
        $('#mayId').val(may ? may.idMayIn : 0);
        $('#mayBoPhan').val(may ? may.boPhan || '' : '');
        $('#mayModel').val(may ? may.model : '');
        $('#maySerial').val(may ? may.serial : '');
        $('#mayIp').val(may ? may.diaChiIp || '' : '');
        $('#mayTenHangDoi').val(may ? may.tenHangDoi || '' : '');
        $('#mayViTri').val(may ? may.viTri || '' : '');
        $('#mayTrangThai').val(may ? may.trangThai : 'HoatDong');
        $('#mayTheoDoi').prop('checked', may ? !!may.theoDoiTuDong : true);
        $('#mayGhiChu').val(may ? may.ghiChu || '' : '');
        modalMayIn.show();
    }

    function luuMay() {
        var $nut = $('#btnLuuMay');
        var duLieu = {
            idMayIn: parseInt($('#mayId').val(), 10) || 0,
            boPhan: $('#mayBoPhan').val(),
            model: $('#mayModel').val(),
            serial: $('#maySerial').val(),
            diaChiIp: $('#mayIp').val(),
            tenHangDoi: $('#mayTenHangDoi').val(),
            viTri: $('#mayViTri').val(),
            trangThai: $('#mayTrangThai').val(),
            theoDoiTuDong: $('#mayTheoDoi').is(':checked'),
            ghiChu: $('#mayGhiChu').val()
        };

        $nut.prop('disabled', true).text('Đang lưu...');

        $.ajax({
            url: '/QLMayIn/Luu',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(duLieu)
        })
            .done(function (res) {
                if (res.thanhCong) {
                    modalMayIn.hide();
                    taiDanhSach();
                } else {
                    alert(res.thongBao);
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $nut.prop('disabled', false).text('Lưu'); });
    }

    function luuChiSoTay() {
        var $nut = $('#btnLuuChiSoTay');
        var duLieu = {
            idMayIn: parseInt($('#tayId').val(), 10) || 0,
            ngayChot: $('#tayNgay').val() || null,
            counterTong: $('#tayCounter').val() === '' ? null : parseInt($('#tayCounter').val(), 10),
            tonerPhanTram: $('#tayToner').val() === '' ? null : parseInt($('#tayToner').val(), 10),
            ghiChu: $('#tayGhiChu').val()
        };

        $nut.prop('disabled', true).text('Đang lưu...');

        $.ajax({
            url: '/QLMayIn/NhapChiSoTay',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(duLieu)
        })
            .done(function (res) {
                if (res.thanhCong) {
                    modalChiSoTay.hide();
                    taiDanhSach();
                } else {
                    alert(res.thongBao);
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $nut.prop('disabled', false).text('Lưu chỉ số'); });
    }

    function layMayTheoId(id, tiepTuc) {
        $.getJSON('/QLMayIn/ChiTiet/' + id, function (res) {
            if (res.thanhCong) tiepTuc(res.may);
        });
    }

    $(function () {
        modalMayIn = new bootstrap.Modal(document.getElementById('modalMayIn'));
        modalChiSoTay = new bootstrap.Modal(document.getElementById('modalChiSoTay'));

        taiDanhSach();

        $('#filterTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(taiDanhSach, 350);
        });
        $('#filterBoPhan, #filterModel, #filterTrangThai').on('change', function () {
            // Đổi ô chọn trạng thái bằng tay thì thẻ "Đang hoạt động" phải sáng/tắt theo cho khớp
            $('.mayin-the-so[data-loc="hoatDong"]')
                .toggleClass('dang-loc', $('#filterTrangThai').val() === 'HoatDong');
            taiDanhSach();
        });

        // Trang mở lần đầu đang lọc HoatDong sẵn nên thẻ tương ứng phải sáng ngay
        $('.mayin-the-so[data-loc="hoatDong"]').toggleClass('dang-loc', $('#filterTrangThai').val() === 'HoatDong');

        // Bảng vẽ động nên bắt sự kiện bằng delegation trên document
        $(document).on('click', '.js-doc', function () {
            docMotMay($(this), $(this).data('id'));
        });

        // Bấm vào dòng thì mở trang chi tiết ở TAB MỚI, giữ nguyên bảng đang lọc ở tab này.
        // 'noopener' để tab con không tham chiếu ngược lại window.opener của tab cha.
        function moTrangChiTiet(id) {
            window.open('/QLMayIn/May/' + id, '_blank', 'noopener');
        }

        // Bấm đúng nút/liên kết trong cột thao tác thì không tính là bấm vào dòng
        $(document).on('click', '.js-dong-may', function (e) {
            if ($(e.target).closest('button, a, input, select').length) return;
            moTrangChiTiet($(this).data('id'));
        });

        $(document).on('keydown', '.js-dong-may', function (e) {
            if (e.key !== 'Enter') return;
            if ($(e.target).closest('button, a, input, select').length) return;
            moTrangChiTiet($(this).data('id'));
        });

        $(document).on('click', '.js-sua', function () {
            layMayTheoId($(this).data('id'), moFormMay);
        });

        // Cất máy vào Lưu trữ / đưa về bảng chính: thành công thì gỡ đúng dòng đó khỏi bảng
        // và chỉnh số đếm tại chỗ, không tải lại cả danh sách (tải lại phải ping lại cả trăm máy).
        $(document).on('click', '.js-luu-tru', function () {
            var $nut = $(this);
            var id = $nut.data('id');
            var cat = String($nut.data('cat')) === 'true';
            var htmlCu = $nut.html();
            var $kq = $nut.closest('td').find('.js-kq-luu-tru');

            $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i>');
            $kq.empty();

            $.post('/QLMayIn/LuuTru/' + id + '?cat=' + cat)
                .done(function (res) {
                    if (!res.thanhCong) {
                        $kq.addClass('text-danger').text(res.thongBao || 'Không lưu được');
                        $nut.prop('disabled', false).html(htmlCu);
                        return;
                    }
                    duLieuGoc = duLieuGoc.filter(function (x) { return x.idMayIn !== id; });
                    $nut.closest('tr').fadeOut(200, function () {
                        $(this).remove();
                        if (!$('#mayinTableBody tr.js-dong-may').length) veBang([]);
                    });
                    var soLuuTru = parseInt($('#soLuuTru').text().replace(/\D/g, ''), 10) || 0;
                    $('#soLuuTru').text(dinhDangSo(Math.max(0, soLuuTru + (cat ? 1 : -1))));
                })
                .fail(function () {
                    $kq.addClass('text-danger').text('Lỗi kết nối máy chủ');
                    $nut.prop('disabled', false).html(htmlCu);
                });
        });

        // ---------- Tích chọn nhiều máy → cất / đưa về một lượt ----------
        $(document).on('change', '.js-chon-may', capNhatDaChon);

        $('#chonTatCa').on('change', function () {
            $('.js-chon-may').prop('checked', this.checked);
            capNhatDaChon();
        });

        // Ô tích nằm trong dòng có click mở trang chi tiết: bấm trúng ô (kể cả lề quanh ô tích)
        // chỉ tích/bỏ tích, không mở trang
        $(document).on('click', '.js-o-chon', function (e) {
            e.stopPropagation();
            if (e.target.type !== 'checkbox') {
                var $o = $(this).find('.js-chon-may');
                $o.prop('checked', !$o.prop('checked'));
                capNhatDaChon();
            }
        });

        $('#btnLuuTruDaChon').on('click', function () {
            var $nut = $(this);
            var ids = $('.js-chon-may:checked').map(function () { return parseInt(this.value, 10); }).get();
            if (!ids.length) return;

            var cat = !dangXemLuuTru;
            var htmlCu = $nut.html();
            $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang lưu...');
            $('#kqDongBo').removeClass('text-danger text-success').empty();

            // traditional: gửi ids=1&ids=2 (ASP.NET không đọc kiểu ids[]=1 mặc định của jQuery)
            $.ajax({ url: '/QLMayIn/LuuTruNhieu', type: 'POST', traditional: true, data: { ids: ids, cat: cat } })
                .done(function (res) {
                    $nut.prop('disabled', false).html(htmlCu);
                    if (!res.thanhCong) {
                        $('#kqDongBo').addClass('text-danger').text(res.thongBao || 'Không lưu được');
                        return;
                    }
                    var daDoi = (res.duLieu && res.duLieu.ids) || ids;
                    duLieuGoc = duLieuGoc.filter(function (x) { return daDoi.indexOf(x.idMayIn) < 0; });
                    $('.js-chon-may:checked').closest('tr').remove();
                    if (!$('#mayinTableBody tr.js-dong-may').length) veBang([]);

                    var soLuuTru = parseInt($('#soLuuTru').text().replace(/\D/g, ''), 10) || 0;
                    $('#soLuuTru').text(dinhDangSo(Math.max(0, soLuuTru + (cat ? daDoi.length : -daDoi.length))));
                    $('#kqDongBo').addClass('text-success').text(res.thongBao);
                    capNhatDaChon();
                })
                .fail(function () {
                    $nut.prop('disabled', false).html(htmlCu);
                    $('#kqDongBo').addClass('text-danger').text('Lỗi kết nối máy chủ');
                });
        });

        $('#btnLuuTru').on('click', function () {
            dangXemLuuTru = !dangXemLuuTru;
            $(this).toggleClass('btn-secondary text-white', dangXemLuuTru).toggleClass('btn-outline-secondary', !dangXemLuuTru)
                .attr('title', dangXemLuuTru ? 'Quay lại bảng chính' : 'Xem các máy đã cất vào Lưu trữ');
            $(this).find('.js-chu-luu-tru').text(dangXemLuuTru ? 'Đang xem Lưu trữ' : 'Lưu trữ');
            taiDanhSach();
        });

        $(document).on('click', '.js-tay', function () {
            var $tr = $(this).closest('tr');
            $('#tayId').val($(this).data('id'));
            $('#tayThongTin').text($tr.find('.js-model').text() + ' — serial ' + $tr.find('.js-serial').text());
            $('#tayNgay').val(new Date().toISOString().substring(0, 10));
            $('#tayCounter').val('');
            $('#tayToner').val('');
            $('#tayGhiChu').val('');
            modalChiSoTay.show();
        });

        $(document).on('click', '.mayin-the-so[data-loc]', function () {
            datLocNhanh($(this).data('loc'));
        });

        // Thẻ số đóng vai nút bấm nên phải dùng được bằng bàn phím
        $(document).on('keydown', '.mayin-the-so[data-loc]', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                datLocNhanh($(this).data('loc'));
            }
        });

        $('#btnThemMay').on('click', function () { moFormMay(null); });
        $('#btnLuuMay').on('click', luuMay);
        $('#btnLuuChiSoTay').on('click', luuChiSoTay);

        $('#btnDocTatCa').on('click', function () {
            dongBoTatCa($(this), '/QLMayIn/DocTatCa',
                'Đang đọc từng máy...', '<i class="fa fa-rotate me-1"></i> Đọc chỉ số tất cả');
        });

        $('#btnNapLichSuTatCa').on('click', function () {
            dongBoTatCa($(this), '/QLMayIn/NapLichSuTatCa',
                'Đang nạp nhật ký lỗi...', '<i class="fa fa-clock-rotate-left me-1"></i> Nạp lịch sử tất cả');
        });

        // Xuất báo cáo theo bộ lọc đang chọn, TRỪ trạng thái: file xuất luôn có đủ cả máy Tạm dừng
        // và Báo phế (máy chủ tự gom thành nhóm riêng). Tải file là ngoại lệ được phép điều hướng,
        // nhưng vẫn mở ở tab ẩn để trang danh sách không bị rời đi.
        function taiFileXuat($nut, duongDan, themThamSo) {
            var thamSo = $.extend({
                tuKhoa: $('#filterTuKhoa').val() || '',
                boPhan: $('#filterBoPhan').val() || '',
                model: $('#filterModel').val() || ''
            }, themThamSo || {});

            var chuCu = $nut.html();
            $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang xuất...');

            var khung = document.createElement('iframe');
            khung.style.display = 'none';
            khung.src = duongDan + '?' + $.param(thamSo);
            document.body.appendChild(khung);

            // Không có sự kiện nào báo "đã tải xong" cho iframe tải file, nên mở khoá nút theo thời gian
            setTimeout(function () {
                $nut.prop('disabled', false).html(chuCu);
                if (khung.parentNode) khung.parentNode.removeChild(khung);
            }, 4000);
        }

        $('#btnXuatExcel').on('click', function () {
            taiFileXuat($(this), '/QLMayIn/XuatExcel');
        });

        // Bảng theo tháng: mỗi tháng hai cột đen trắng / màu, xuất trọn năm đang chọn
        $('#btnXuatTheoThang').on('click', function () {
            taiFileXuat($(this), '/QLMayIn/XuatExcelTheoThang', {
                nam: $('#filterNamXuat').val() || ''
            });
        });
    });
})();
