// Màn hình Quản lý Công cụ dụng cụ (CCDC) — /QLCCDC
// Toàn bộ dữ liệu lấy qua AJAX từ QLCCDCController, view chỉ dựng khung.
(function () {
    'use strict';

    var modalCCDC, modalXoa, modalChoMuon, modalPhieuMuon, modalNhanTra;
    var timerTimKiem = null;
    // Nhận trả mở được từ 2 chỗ: modal phiếu mượn của 1 CCDC, và màn hình "Đang cho mượn".
    // Nhớ nguồn gốc để sau khi trả xong nạp lại đúng bảng người dùng đang đứng.
    var nguonNhanTra = 'phieu';

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function dinhDangSo(v) {
        if (v === null || v === undefined || v === '') return '';
        return Number(v).toLocaleString('vi-VN');
    }

    // API trả DateOnly dạng "2026-08-25" hoặc null — cắt phần giờ nếu có để hợp input type=date
    function chuanHoaNgay(v) {
        if (!v) return '';
        return String(v).substring(0, 10);
    }

    // Cột datetime trả về dạng ISO "2026-08-25T14:03:00" — hiển thị dd/MM/yyyy HH:mm cho gọn
    function dinhDangNgayGio(v) {
        if (!v) return '';
        var d = new Date(v);
        if (isNaN(d.getTime())) return escapeHtml(v);
        var hai = function (n) { return n < 10 ? '0' + n : String(n); };
        return hai(d.getDate()) + '/' + hai(d.getMonth() + 1) + '/' + d.getFullYear()
            + ' ' + hai(d.getHours()) + ':' + hai(d.getMinutes());
    }

    // Ô bị cắt bằng ellipsis nên gắn title để người dùng rê chuột xem đủ nội dung
    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + className + '" title="' + s + '">' + s + '</td>';
    }

    // Nhãn hạn trả dùng chung cho cả modal phiếu mượn lẫn màn hình "Đang cho mượn"
    function nhanHanTra(p) {
        if (p.soNgayTre > 0) {
            return '<span class="badge rounded-pill bg-danger">Trễ ' + p.soNgayTre + ' ngày</span>';
        }
        if (!p.ngayHenTra) {
            return '<span class="text-muted small">Không hẹn</span>';
        }
        return '<span class="badge rounded-pill bg-success-subtle text-success-emphasis">Trong hạn</span>';
    }

    function mauTinhTrang(tt) {
        switch (tt) {
            case 'Đang sử dụng': return 'background:#dcfce7; color:#166534;';
            case 'Hư hỏng': return 'background:#fee2e2; color:#991b1b;';
            case 'Thanh lý': return 'background:#e2e8f0; color:#475569;';
            default: return 'background:#dbeafe; color:#1e40af;'; // Trong kho
        }
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
            tinhTrang: $('#filterTinhTrang').val()
        };

        $('#ccdcTableBody').html('<tr><td colspan="15" class="ccdc-trangthai text-muted py-4">Đang tải dữ liệu...</td></tr>');

        $.getJSON('/QLCCDC/GetDanhSach', thamSo)
            .done(function (res) {
                if (!res.success) {
                    $('#ccdcTableBody').html('<tr><td colspan="15" class="ccdc-trangthai text-danger py-4">'
                        + escapeHtml(res.message) + '</td></tr>');
                    return;
                }
                veBang(res.data);
                $('#tongDong').text(dinhDangSo(res.tongDong));
                $('#tongSoLuong').text(dinhDangSo(res.tongSoLuong));
                $('#tongDangMuon').text(dinhDangSo(res.tongDangMuon));
                $('#tongGiaTri').text(dinhDangSo(res.tongGiaTri));
            })
            .fail(function () {
                $('#ccdcTableBody').html('<tr><td colspan="15" class="ccdc-trangthai text-danger py-4">Lỗi kết nối máy chủ.</td></tr>');
            });
    }

    function veBang(data) {
        if (!data || data.length === 0) {
            $('#ccdcTableBody').html('<tr><td colspan="15" class="ccdc-trangthai text-muted py-4">Chưa có công cụ dụng cụ nào.</td></tr>');
            return;
        }

        var html = data.map(function (x, i) {
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + o(x.maCcdc, 'small')
                + o(x.tenCcdc, 'ccdc-ten')
                + o(x.loaiCcdc, 'small text-muted')
                + '<td class="ccdc-num">' + dinhDangSo(x.soLuong) + '</td>'
                + '<td class="ccdc-num ' + (x.dangMuon > 0 ? 'text-warning fw-bold' : 'text-muted') + '">'
                + dinhDangSo(x.dangMuon) + '</td>'
                + '<td class="ccdc-num ' + (x.conLai <= 0 ? 'text-danger fw-bold' : 'text-success fw-bold') + '">'
                + dinhDangSo(x.conLai) + '</td>'
                + o(x.donViTinh, 'small text-muted')
                + o(x.tenCongTy, 'small')
                + o(x.tenBoPhan, 'small')
                + o(x.nguoiQuanLy, 'small')
                + o(x.viTri, 'small text-muted')
                + '<td><span class="badge rounded-pill" style="' + mauTinhTrang(x.tinhTrang) + '">'
                + escapeHtml(x.tinhTrang || 'Chưa rõ') + '</span></td>'
                + '<td class="ccdc-num small">' + dinhDangSo(x.giaTri) + '</td>'
                + '<td class="ccdc-thaotac">'
                + '<button type="button" class="btn btn-sm btn-light border me-1 btn-cho-muon" title="Cho mượn"'
                + (x.conLai <= 0 ? ' disabled' : '') + '><i class="fas fa-hand-holding-hand" style="color:#0ea5e9;"></i></button>'
                + '<button type="button" class="btn btn-sm btn-light border me-1 btn-phieu-muon" title="Phiếu mượn / nhận trả"><i class="fas fa-clipboard-list" style="color:#7c3aed;"></i></button>'
                + '<button type="button" class="btn btn-sm btn-light border me-1 btn-sua" title="Sửa"><i class="fas fa-pen text-primary"></i></button>'
                + '<button type="button" class="btn btn-sm btn-light border btn-xoa" title="Xoá"><i class="fas fa-trash text-danger"></i></button>'
                + '</td>'
                + '</tr>';
        }).join('');

        var $body = $('#ccdcTableBody').html(html);

        // Gắn dữ liệu bằng .data() thay vì nhét JSON vào thuộc tính HTML
        $body.find('tr').each(function (i) {
            $(this).data('ccdc', data[i]);
        });
    }

    function moModalThem() {
        $('#formCCDC')[0].reset();
        $('#IdCcdc').val(0);
        $('#SoLuong').val(1);
        $('#modalCCDCTitle').html('<i class="fas fa-screwdriver-wrench me-2" style="color:#fb923c;"></i> Thêm công cụ dụng cụ');
        locBoPhanTheoCongTy($('#IdcongTy'), $('#IdboPhan'));
        modalCCDC.show();
    }

    function moModalSua(x) {
        $('#IdCcdc').val(x.idCcdc);
        $('#MaCcdc').val(x.maCcdc || '');
        $('#TenCcdc').val(x.tenCcdc || '');
        $('#LoaiCcdc').val(x.loaiCcdc || '');
        $('#SoLuong').val(x.soLuong);
        $('#DonViTinh').val(x.donViTinh || '');
        $('#IdcongTy').val(x.idcongTy || '');
        locBoPhanTheoCongTy($('#IdcongTy'), $('#IdboPhan'));
        $('#IdboPhan').val(x.idboPhan || '');
        $('#NguoiQuanLy').val(x.nguoiQuanLy || '');
        $('#ViTri').val(x.viTri || '');
        $('#TinhTrang').val(x.tinhTrang || 'Trong kho');
        $('#NgayMua').val(chuanHoaNgay(x.ngayMua));
        $('#HanBaoHanh').val(chuanHoaNgay(x.hanBaoHanh));
        $('#GiaTri').val(x.giaTri === null ? '' : x.giaTri);
        $('#GhiChu').val(x.ghiChu || '');
        $('#modalCCDCTitle').html('<i class="fas fa-pen me-2" style="color:#fb923c;"></i> Sửa công cụ dụng cụ');
        modalCCDC.show();
    }

    function luuCCDC() {
        if (!$('#TenCcdc').val().trim()) {
            alert('Vui lòng nhập tên công cụ dụng cụ.');
            $('#TenCcdc').focus();
            return;
        }

        var $btn = $('#btnLuuCCDC').prop('disabled', true); // chặn double-submit tạo bản ghi trùng

        $.post('/QLCCDC/Save', $('#formCCDC').serialize())
            .done(function (res) {
                if (res.success) {
                    modalCCDC.hide();
                    taiDanhSach();
                } else {
                    alert(res.message || 'Lưu không thành công.');
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function xacNhanXoa() {
        var $btn = $('#btnXacNhanXoaCCDC').prop('disabled', true);

        $.post('/QLCCDC/Delete', { id: $('#xoaIdCcdc').val(), lyDo: $('#xoaLyDo').val() })
            .done(function (res) {
                if (res.success) {
                    modalXoa.hide();
                    taiDanhSach();
                } else {
                    alert(res.message || 'Xoá không thành công.');
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    // ---------------- Cho mượn / nhận trả ----------------

    function moModalChoMuon(x) {
        $('#muonIdCcdc').val(x.idCcdc);
        $('#muonTenCcdc').text(x.tenCcdc + (x.maCcdc ? ' (' + x.maCcdc + ')' : ''));
        $('#muonTongSL').text(dinhDangSo(x.soLuong));
        $('#muonDangMuon').text(dinhDangSo(x.dangMuon));
        $('#muonConLai').text(dinhDangSo(x.conLai));
        $('#muonMaNhanVien, #muonHoTen, #muonBoPhan, #muonCongTy, #muonNgayHenTra, #muonGhiChu').val('');
        $('#muonSoLuong').val(1).attr('max', x.conLai);
        $('#muonKetQuaTraCuu').removeClass('text-danger text-success')
            .text('Nhập mã rồi rời chuột ra để tự lấy tên.');
        modalChoMuon.show();
    }

    function traCuuNhanVien() {
        var ma = $('#muonMaNhanVien').val().trim();
        if (!ma) return;

        $('#muonKetQuaTraCuu').removeClass('text-danger text-success').text('Đang tra cứu...');

        $.getJSON('/QLCCDC/TraCuuNhanVien', { maNv: ma })
            .done(function (res) {
                if (!res.success) {
                    $('#muonHoTen, #muonBoPhan, #muonCongTy').val('');
                    $('#muonKetQuaTraCuu').addClass('text-danger').text(res.message);
                    return;
                }
                $('#muonHoTen').val(res.data.hoTen || '');
                $('#muonBoPhan').val(res.data.phongBan || '');
                $('#muonCongTy').val(res.data.tenCongTy || '');
                // Vẫn cho mượn khi tài khoản đã nghỉ, nhưng phải cảnh báo rõ cho người lập phiếu
                var canhBao = (res.data.trangThai && res.data.trangThai !== 'Đang làm')
                    ? ' — CHÚ Ý: trạng thái "' + res.data.trangThai + '"' : '';
                $('#muonKetQuaTraCuu').addClass(canhBao ? 'text-danger' : 'text-success')
                    .text('Đã tìm thấy nhân viên.' + canhBao);
            })
            .fail(function () {
                $('#muonKetQuaTraCuu').addClass('text-danger').text('Lỗi kết nối máy chủ.');
            });
    }

    function xacNhanChoMuon() {
        var ma = $('#muonMaNhanVien').val().trim();
        if (!ma) {
            alert('Vui lòng nhập mã nhân viên người mượn.');
            $('#muonMaNhanVien').focus();
            return;
        }

        var $btn = $('#btnXacNhanChoMuon').prop('disabled', true);

        $.post('/QLCCDC/ChoMuon', {
            idCcdc: $('#muonIdCcdc').val(),
            maNhanVien: ma,
            soLuongMuon: $('#muonSoLuong').val(),
            ngayHenTra: $('#muonNgayHenTra').val(),
            ghiChu: $('#muonGhiChu').val()
        })
            .done(function (res) {
                if (res.success) {
                    modalChoMuon.hide();
                    taiDanhSach();
                } else {
                    alert(res.message || 'Không ghi nhận được.');
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function moModalPhieuMuon(x) {
        $('#phieuIdCcdc').val(x.idCcdc);
        $('#phieuTenCcdc').text(x.tenCcdc);
        $('#phieuXemTatCa').prop('checked', false);
        taiPhieuMuon();
        modalPhieuMuon.show();
    }

    function taiPhieuMuon() {
        var idCcdc = $('#phieuIdCcdc').val();
        $('#phieuMuonTableBody').html('<tr><td colspan="10" class="text-center text-muted py-3">Đang tải...</td></tr>');

        $.getJSON('/QLCCDC/GetPhieuMuon', { idCcdc: idCcdc, tatCa: $('#phieuXemTatCa').is(':checked') })
            .done(function (res) {
                if (!res.success) {
                    $('#phieuMuonTableBody').html('<tr><td colspan="10" class="text-center text-danger py-3">'
                        + escapeHtml(res.message) + '</td></tr>');
                    return;
                }
                if (!res.data.length) {
                    $('#phieuMuonTableBody').html('<tr><td colspan="10" class="text-center text-muted py-3">Không có phiếu nào.</td></tr>');
                    return;
                }

                var html = res.data.map(function (p) {
                    var daDong = !!p.ngayTra;
                    return '<tr class="' + (p.soNgayTre > 0 ? 'ccdc-qua-han' : '') + '">'
                        + '<td class="small fw-bold">' + escapeHtml(p.maNhanVien) + '</td>'
                        + '<td class="small">' + escapeHtml(p.hoTen) + '</td>'
                        + '<td class="small">' + escapeHtml(p.boPhan) + '</td>'
                        + '<td class="text-end">' + dinhDangSo(p.soLuongMuon) + '</td>'
                        + '<td class="text-end">' + dinhDangSo(p.soLuongDaTra) + '</td>'
                        + '<td class="text-end ' + (p.conNo > 0 ? 'text-danger fw-bold' : 'text-muted') + '">'
                        + dinhDangSo(p.conNo) + '</td>'
                        + '<td class="small">' + dinhDangNgayGio(p.ngayMuon) + '</td>'
                        + '<td class="small">' + (chuanHoaNgay(p.ngayHenTra) || '—')
                        + (p.soNgayTre > 0 ? ' <span class="badge rounded-pill bg-danger">Trễ ' + p.soNgayTre + ' ngày</span>' : '')
                        + '</td>'
                        + '<td class="small">' + (daDong ? dinhDangNgayGio(p.ngayTra) : '<span class="badge bg-warning text-dark">Đang mượn</span>') + '</td>'
                        + '<td class="text-center">'
                        + (daDong ? '<span class="text-muted small">Đã xong</span>'
                                  : '<button type="button" class="btn btn-sm btn-success btn-nhan-tra">Nhận trả</button>')
                        + '</td>'
                        + '</tr>';
                }).join('');

                var $body = $('#phieuMuonTableBody').html(html);
                $body.find('tr').each(function (i) { $(this).data('phieu', res.data[i]); });
            })
            .fail(function () {
                $('#phieuMuonTableBody').html('<tr><td colspan="10" class="text-center text-danger py-3">Lỗi kết nối máy chủ.</td></tr>');
            });
    }

    // ---------- Màn hình "Đang cho mượn": mọi phiếu còn nợ của mọi CCDC ----------
    function taiDangMuon() {
        var thamSo = {
            tuKhoa: $('#filterTuKhoa').val(),
            idCongTy: $('#filterCongTy').val(),
            idBoPhan: $('#filterBoPhan').val(),
            chiQuaHan: $('#dmChiQuaHan').is(':checked')
        };

        $('#dangMuonTableBody').html('<tr><td colspan="11" class="ccdc-trangthai text-muted py-4">Đang tải dữ liệu...</td></tr>');

        $.getJSON('/QLCCDC/GetDangMuon', thamSo)
            .done(function (res) {
                if (!res.success) {
                    $('#dangMuonTableBody').html('<tr><td colspan="11" class="ccdc-trangthai text-danger py-4">'
                        + escapeHtml(res.message) + '</td></tr>');
                    return;
                }

                $('#dmTongPhieu').text(dinhDangSo(res.tongPhieu));
                $('#dmTongConNo').text(dinhDangSo(res.tongConNo));
                $('#dmTongQuaHan').text(dinhDangSo(res.tongQuaHan));
                $('#badgeDangMuon').text(res.tongPhieu);
                $('#badgeQuaHan').text(res.tongQuaHan + ' quá hạn').toggleClass('d-none', res.tongQuaHan === 0);

                if (!res.data.length) {
                    $('#dangMuonTableBody').html('<tr><td colspan="11" class="ccdc-trangthai text-muted py-4">'
                        + ($('#dmChiQuaHan').is(':checked') ? 'Không có phiếu nào quá hạn.' : 'Không có ai đang mượn.')
                        + '</td></tr>');
                    return;
                }

                var html = res.data.map(function (p, i) {
                    return '<tr class="' + (p.soNgayTre > 0 ? 'ccdc-qua-han' : '') + '">'
                        + '<td class="text-muted small">' + (i + 1) + '</td>'
                        + o(p.maNhanVien, 'small fw-bold')
                        + o(p.hoTen, 'small')
                        + o(p.boPhan, 'small text-muted')
                        + o(p.maCcdc, 'small')
                        + o(p.tenCcdc, 'ccdc-ten')
                        + '<td class="ccdc-num text-danger fw-bold">' + dinhDangSo(p.conNo) + '</td>'
                        + '<td class="small">' + dinhDangNgayGio(p.ngayMuon) + '</td>'
                        + '<td class="small">' + (chuanHoaNgay(p.ngayHenTra) || '—') + '</td>'
                        + '<td>' + nhanHanTra(p) + '</td>'
                        + '<td class="ccdc-thaotac">'
                        + '<button type="button" class="btn btn-sm btn-success btn-nhan-tra-dm">Nhận trả</button>'
                        + '</td>'
                        + '</tr>';
                }).join('');

                var $body = $('#dangMuonTableBody').html(html);
                $body.find('tr').each(function (i) { $(this).data('phieu', res.data[i]); });
            })
            .fail(function () {
                $('#dangMuonTableBody').html('<tr><td colspan="11" class="ccdc-trangthai text-danger py-4">Lỗi kết nối máy chủ.</td></tr>');
            });
    }

    // Bộ lọc phía trên dùng chung cho 2 tab — chỉ nạp lại bảng đang hiển thị
    function locTheoTabDangXem() {
        if ($('#paneDangMuon').hasClass('active')) taiDangMuon(); else taiDanhSach();
    }

    function moModalNhanTra(p) {
        $('#traIdMuon').val(p.idMuon);
        $('#traThongTin').html('Phiếu <strong>#' + p.idMuon + '</strong> — '
            + escapeHtml(p.maNhanVien) + ' ' + escapeHtml(p.hoTen)
            + ' — còn nợ <strong>' + dinhDangSo(p.conNo) + '</strong>');
        $('#traSoLuong').val(p.conNo).attr('max', p.conNo);
        $('#traTinhTrang').val('Nguyên vẹn');
        $('#traGhiChu').val('');
        modalNhanTra.show();
    }

    function xacNhanNhanTra() {
        var $btn = $('#btnXacNhanNhanTra').prop('disabled', true);

        $.post('/QLCCDC/NhanTra', {
            idMuon: $('#traIdMuon').val(),
            soLuongTra: $('#traSoLuong').val(),
            tinhTrangKhiTra: $('#traTinhTrang').val(),
            ghiChu: $('#traGhiChu').val()
        })
            .done(function (res) {
                if (res.success) {
                    modalNhanTra.hide();
                    // Nạp lại đúng bảng người dùng đang đứng; bảng kho luôn phải nạp lại
                    // vì trả hỏng/mất có thể đã đổi tình trạng hoặc trừ số lượng CCDC.
                    if (nguonNhanTra === 'dangMuon') taiDangMuon(); else taiPhieuMuon();
                    taiDanhSach();
                    if (res.message) alert(res.message);
                } else {
                    alert(res.message || 'Không ghi nhận được.');
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    $(function () {
        modalCCDC = new bootstrap.Modal(document.getElementById('modalCCDC'));
        modalXoa = new bootstrap.Modal(document.getElementById('modalXoaCCDC'));
        modalChoMuon = new bootstrap.Modal(document.getElementById('modalChoMuon'));
        modalPhieuMuon = new bootstrap.Modal(document.getElementById('modalPhieuMuon'));
        modalNhanTra = new bootstrap.Modal(document.getElementById('modalNhanTra'));

        $('#btnTraCuuNhanVien').on('click', traCuuNhanVien);
        $('#muonMaNhanVien').on('blur', traCuuNhanVien).on('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); traCuuNhanVien(); }
        });
        $('#btnXacNhanChoMuon').on('click', xacNhanChoMuon);
        $('#btnXacNhanNhanTra').on('click', xacNhanNhanTra);
        $('#phieuXemTatCa').on('change', taiPhieuMuon);

        $('#ccdcTableBody').on('click', '.btn-cho-muon', function () {
            moModalChoMuon($(this).closest('tr').data('ccdc'));
        });
        $('#ccdcTableBody').on('click', '.btn-phieu-muon', function () {
            moModalPhieuMuon($(this).closest('tr').data('ccdc'));
        });
        $('#phieuMuonTableBody').on('click', '.btn-nhan-tra', function () {
            nguonNhanTra = 'phieu';
            moModalNhanTra($(this).closest('tr').data('phieu'));
        });

        $('#dangMuonTableBody').on('click', '.btn-nhan-tra-dm', function () {
            nguonNhanTra = 'dangMuon';
            moModalNhanTra($(this).closest('tr').data('phieu'));
        });

        $('#dmChiQuaHan').on('change', taiDangMuon);
        $('#tabDangMuonBtn').on('shown.bs.tab', taiDangMuon);

        $('#btnThemCCDC').on('click', moModalThem);
        $('#btnLuuCCDC').on('click', luuCCDC);
        $('#btnXacNhanXoaCCDC').on('click', xacNhanXoa);

        $('#IdcongTy').on('change', function () { locBoPhanTheoCongTy($(this), $('#IdboPhan')); });
        $('#filterCongTy').on('change', function () {
            locBoPhanTheoCongTy($(this), $('#filterBoPhan'));
            locTheoTabDangXem();
        });
        $('#filterBoPhan').on('change', locTheoTabDangXem);
        $('#filterTinhTrang').on('change', taiDanhSach);  // lọc tình trạng chỉ có nghĩa với bảng kho

        // Gõ tới đâu lọc tới đó, hoãn 350ms cho đỡ dội request
        $('#filterTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(locTheoTabDangXem, 350);
        });

        $('#ccdcTableBody').on('click', '.btn-sua', function () {
            moModalSua($(this).closest('tr').data('ccdc'));
        });

        $('#ccdcTableBody').on('click', '.btn-xoa', function () {
            var x = $(this).closest('tr').data('ccdc');
            $('#xoaIdCcdc').val(x.idCcdc);
            $('#xoaTenCcdc').text(x.tenCcdc);
            $('#xoaLyDo').val('');
            modalXoa.show();
        });

        taiDanhSach();
        // Nạp luôn lúc vào trang để badge "Đang cho mượn / quá hạn" có số ngay,
        // người dùng thấy được việc cần làm mà không phải bấm mở tab.
        taiDangMuon();
    });
})();
