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

    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + (className || '') + '" title="' + s + '">' + s + '</td>';
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

    function taiDanhSach() {
        var thamSo = {
            tuKhoa: $('#filterTuKhoa').val(),
            boPhan: $('#filterBoPhan').val(),
            model: $('#filterModel').val(),
            trangThai: $('#filterTrangThai').val()
        };

        $('#mayinTableBody').html('<tr><td colspan="16" class="text-center text-muted py-4">Đang tải dữ liệu...</td></tr>');

        $.getJSON('/QLMayIn/GetDanhSach', thamSo)
            .done(function (res) {
                if (!res.thanhCong) {
                    $('#mayinTableBody').html('<tr><td colspan="16" class="text-center text-danger py-4">'
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
            })
            .fail(function () {
                $('#mayinTableBody').html('<tr><td colspan="16" class="text-center text-danger py-4">Lỗi kết nối máy chủ.</td></tr>');
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
            $('#mayinTableBody').html('<tr><td colspan="16" class="text-center text-muted py-4">Không có máy in nào khớp bộ lọc.</td></tr>');
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
                : dinhDangSo(x.counterTong) + '<div class="text-muted" style="font-size:0.7rem;">' + escapeHtml(x.ngayChiSo) + '</div>';

            var nutDoc = x.diaChiIp
                ? '<button class="btn btn-sm btn-outline-primary rounded-pill px-2 js-doc" data-id="' + x.idMayIn + '" title="Đọc chỉ số từ máy"><i class="fa fa-rotate"></i></button>'
                : '<button class="btn btn-sm btn-outline-warning rounded-pill px-2 js-tay" data-id="' + x.idMayIn + '" title="Nhập chỉ số tay"><i class="fa fa-keyboard"></i></button>';

            return '<tr class="js-dong-may" data-id="' + x.idMayIn + '" tabindex="0">'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + o(x.boPhan, 'small fw-bold')
                + o(x.model, 'small')
                + o(x.serial, 'small')
                + o(x.tenHangDoi, 'small')
                + '<td class="small">' + ip + '</td>'
                + o(x.viTri, 'small')
                + '<td class="mayin-cot-so small">' + chiSo + '</td>'
                + '<td class="mayin-cot-so small fw-bold">' + dinhDangSo(x.trangInHomNay) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(x.trangIn7Ngay) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(x.trangIn30Ngay) + '</td>'
                + '<td>' + oNhomVatTu(x.toner, x.vatTu, 'TONER') + '</td>'
                + '<td>' + oNhomVatTu(x.drum, x.vatTu, 'DRUM') + '</td>'
                + '<td class="mayin-trangthai">' + nhanTrangThai(x.trangThai) + '</td>'
                + '<td class="small text-muted" title="' + escapeHtml(x.ketQuaDocCuoi) + '">' + escapeHtml(x.lanDocCuoi || '—') + '</td>'
                + '<td class="text-end text-nowrap">'
                + nutDoc
                + ' <a href="/QLMayIn/May/' + x.idMayIn + '" target="_blank" rel="noopener" class="btn btn-sm btn-outline-secondary rounded-pill px-2" title="Mở trang chi tiết ở tab mới"><i class="fa fa-chart-line"></i></a>'
                + ' <button class="btn btn-sm btn-outline-secondary rounded-pill px-2 js-sua" data-id="' + x.idMayIn + '" title="Sửa thông tin"><i class="fa fa-pen"></i></button>'
                + '</td>'
                + '</tr>';
        }).join('');

        $('#mayinTableBody').html(html);
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
                    $nut.closest('tr').find('td').eq(14)
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

        $(document).on('click', '.js-tay', function () {
            var $tr = $(this).closest('tr');
            $('#tayId').val($(this).data('id'));
            $('#tayThongTin').text($tr.find('td').eq(2).text() + ' — serial ' + $tr.find('td').eq(3).text());
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
        $('#btnXuatExcel').on('click', function () {
            var thamSo = $.param({
                tuKhoa: $('#filterTuKhoa').val() || '',
                boPhan: $('#filterBoPhan').val() || '',
                model: $('#filterModel').val() || ''
            });

            var $nut = $(this);
            var chuCu = $nut.html();
            $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang xuất...');

            var khung = document.createElement('iframe');
            khung.style.display = 'none';
            khung.src = '/QLMayIn/XuatExcel?' + thamSo;
            document.body.appendChild(khung);

            // Không có sự kiện nào báo "đã tải xong" cho iframe tải file, nên mở khoá nút theo thời gian
            setTimeout(function () {
                $nut.prop('disabled', false).html(chuCu);
                if (khung.parentNode) khung.parentNode.removeChild(khung);
            }, 4000);
        });
    });
})();
