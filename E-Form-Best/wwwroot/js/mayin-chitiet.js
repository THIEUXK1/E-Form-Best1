// Trang chi tiết một máy in — /QLMayIn/May/{id}
// Khung do view dựng sẵn, số liệu lấy qua chính hai API mà màn hình danh sách đang dùng:
//   /QLMayIn/ChiTiet/{id}     → thông tin máy + lịch sử chỉ số + trang in theo ngày
//   /QLMayIn/QuetThietBi/{id} → ping, cổng, firmware, vật tư, mức dùng (bấm nút mới chạy)
(function () {
    'use strict';

    var bieuDo;
    var goc = document.getElementById('mayInGoc');
    var idDangXem = goc ? (parseInt(goc.getAttribute('data-id-may-in'), 10) || 0) : 0;

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

    // Thanh mực/trống: xanh > 50%, vàng 21-50%, đỏ ≤ 20%
    function thanhMuc(phanTram) {
        if (phanTram === null || phanTram === undefined) return '<span class="text-muted small">—</span>';
        var lop = phanTram > 50 ? 'muc-du' : (phanTram > 20 ? 'muc-vua' : 'muc-can');
        return '<div class="d-flex align-items-center gap-2">'
            + '<div class="mayin-thanh"><span class="' + lop + '" style="width:' + Math.max(0, Math.min(100, phanTram)) + '%"></span></div>'
            + '<span class="small text-muted">' + phanTram + '%</span>'
            + '</div>';
    }

    // Máy in màu báo TONER_C/M/Y/K và DRUM_C/M/Y/K. Vẽ đúng màu mực để nhìn phát biết màu nào sắp hết.
    var MAU_VAT_TU = { C: '#0891b2', M: '#db2777', Y: '#ca8a04', K: '#334155' };
    var TEN_VAT_TU = { C: 'Xanh (C)', M: 'Hồng (M)', Y: 'Vàng (Y)', K: 'Đen (K)' };

    function hauToMau(ten) {
        var m = /_([CMYK])$/.exec(ten || '');
        return m ? m[1] : null;
    }

    function thanhMucMau(phanTram, mau) {
        return '<div class="d-flex align-items-center gap-2">'
            + '<div class="mayin-thanh"><span style="width:' + Math.max(0, Math.min(100, phanTram)) + '%;'
            + 'background:' + mau + ';"></span></div>'
            + '<span class="small text-muted">' + phanTram + '%</span>'
            + '</div>';
    }

    /// Vẽ nhóm vật tư theo tiền tố TONER/DRUM: nhiều màu thì mỗi màu một thanh, còn lại (máy đen
    /// trắng, dòng chốt cũ chưa có vat_tu_json) thì lui về một thanh như trước.
    function veNhomVatTu(dsVatTu, tienTo, phanTramDonLe) {
        var nhom = (dsVatTu || []).filter(function (v) {
            return (v.ten || '').toUpperCase().indexOf(tienTo) === 0;
        });

        if (nhom.length <= 1) {
            return (phanTramDonLe === null || phanTramDonLe === undefined)
                ? '<span class="text-muted">máy không báo</span>'
                : thanhMuc(phanTramDonLe);
        }

        return nhom.map(function (v) {
            var k = hauToMau(v.ten);
            var nhan = k ? TEN_VAT_TU[k] : escapeHtml(v.ten);
            return '<div class="d-flex align-items-center gap-2 mb-1">'
                + '<span class="small text-muted" style="min-width:64px;">' + nhan + '</span>'
                + thanhMucMau(v.phanTram, k ? MAU_VAT_TU[k] : '#64748b')
                + '</div>';
        }).join('');
    }


    // ---------- Quét thiết bị qua IP ----------

    function khoiVuong(tieuDe, noiDung, lopThem) {
        return '<div class="mayin-khoi ' + (lopThem || '') + '">'
            + '<div class="mayin-khoi-tieude">' + tieuDe + '</div>'
            + '<div>' + noiDung + '</div>'
            + '</div>';
    }

    function dongThongTin(nhan, giaTri) {
        if (giaTri === null || giaTri === undefined || giaTri === '') return '';
        return '<div class="mayin-dong"><span>' + escapeHtml(nhan) + '</span><b>' + giaTri + '</b></div>';
    }

    function veMang(mang) {
        if (!mang) return '';

        var trangThai = mang.song
            ? '<span class="badge rounded-pill bg-success-subtle text-success-emphasis">Online</span>'
            : '<span class="badge rounded-pill bg-danger-subtle text-danger-emphasis">' + escapeHtml(mang.trangThai) + '</span>';

        var cong = (mang.cong || []).map(function (c) {
            var lop = c.mo ? 'bg-success-subtle text-success-emphasis' : 'bg-light text-muted';
            return '<span class="badge rounded-pill ' + lop + ' me-1 mb-1">' + c.cong + ' ' + escapeHtml(c.ten) + '</span>';
        }).join('');

        return khoiVuong('<i class="fa fa-network-wired me-1"></i> Mạng',
            dongThongTin('Ping', trangThai)
            + dongThongTin('Thời gian phản hồi', mang.thoiGianMs === null ? null : mang.thoiGianMs + ' ms')
            + dongThongTin('TTL', mang.ttl === null ? null : mang.ttl + (mang.soRouter ? ' (qua ' + mang.soRouter + ' router)' : ' (cùng lớp mạng)'))
            + '<div class="mt-2">' + (cong || '<span class="text-muted small">Không dò được cổng nào</span>') + '</div>');
    }

    function veThietBi(tb) {
        if (!tb) return '';

        var phienBan = (tb.phienBan || []).slice(0, 4).map(function (p) {
            return escapeHtml(p.ten) + ' ' + escapeHtml(p.soHieu);
        }).join('<br>');

        var oDia = (tb.oDia || []).map(function (o) {
            return escapeHtml(o.ten) + ': còn ' + dinhDangSo(o.trong) + '/' + dinhDangSo(o.tong) + ' ' + escapeHtml(o.donVi);
        }).join('<br>');

        return khoiVuong('<i class="fa fa-microchip me-1"></i> Thiết bị',
            dongThongTin('Tên máy', escapeHtml(tb.ten))
            + dongThongTin('Serial', escapeHtml(tb.serial))
            + dongThongTin('Hostname', escapeHtml(tb.hostName))
            + dongThongTin('Trạng thái', escapeHtml(tb.trangThai))
            + dongThongTin('Firmware hệ thống', escapeHtml(tb.phienBanHeThong))
            + dongThongTin('RAM', tb.ram === null ? null : dinhDangSo(tb.ram) + ' ' + escapeHtml(tb.ramDonVi))
            + dongThongTin('Vị trí cấu hình trên máy', escapeHtml(tb.viTriCauHinh) || '<span class="text-muted">chưa đặt</span>')
            + (phienBan ? '<div class="mt-2 small text-muted">' + phienBan + '</div>' : '')
            + (oDia ? '<div class="mt-2 small text-muted">' + oDia + '</div>' : ''));
    }

    function veCounter(counter) {
        if (!counter) return '';

        return khoiVuong('<i class="fa fa-gauge me-1"></i> Đồng hồ đếm (tổng đời máy)',
            dongThongTin('In', dinhDangSo(counter.inTong))
            + dongThongTin('Copy', dinhDangSo(counter.copyTong))
            + dongThongTin('Tổng in + copy', dinhDangSo(counter.tong))
            + dongThongTin('Scan', counter.scanTong === null ? null
                : dinhDangSo(counter.scanTong)
                + (counter.scanMau === null ? '' : ' (màu ' + dinhDangSo(counter.scanMau) + ' / đen trắng ' + dinhDangSo(counter.scanTrangDen) + ')')));
    }

    function veVatTu(vatTu) {
        if (!vatTu || vatTu.length === 0) return '';

        var noiDung = vatTu.map(function (v) {
            var k = hauToMau(v.ten);
            var muc = v.conLai === null
                ? '<span class="badge rounded-pill bg-light text-muted">' + escapeHtml(v.trangThai) + '</span>'
                : (k ? thanhMucMau(v.conLai, MAU_VAT_TU[k]) : thanhMuc(v.conLai));
            var themVao = v.trangConLai === null ? '' : ' <span class="text-muted small">≈ ' + dinhDangSo(v.trangConLai) + ' trang</span>';
            return '<div class="mayin-dong"><span>' + escapeHtml(v.ten) + '</span><span>' + muc + themVao + '</span></div>';
        }).join('');

        return khoiVuong('<i class="fa fa-droplet me-1"></i> Vật tư', noiDung);
    }

    function veKhay(khay) {
        if (!khay || khay.length === 0) return '';

        var noiDung = khay.map(function (k) {
            return dongThongTin(escapeHtml(k.ten),
                escapeHtml(k.khoGiay) + ' · ' + escapeHtml(k.loaiGiay))
                + '<div class="text-muted small mb-2">Hỗ trợ: ' + escapeHtml((k.khoHoTro || []).join(', ')) + '</div>';
        }).join('');

        return khoiVuong('<i class="fa fa-layer-group me-1"></i> Khay giấy', noiDung);
    }

    function veNguonDien(nd) {
        if (!nd) return '';

        function gio(phut) {
            if (phut === null || phut === undefined) return null;
            return dinhDangSo(Math.round(phut / 60)) + ' giờ';
        }

        return khoiVuong('<i class="fa fa-plug me-1"></i> Nguồn điện',
            dongThongTin('Máy in', escapeHtml(nd.mayIn))
            + dongThongTin('Máy quét', escapeHtml(nd.mayQuet))
            + dongThongTin('Thời gian chạy', gio(nd.phutChay))
            + dongThongTin('Chờ lệnh', gio(nd.phutChoLenh))
            + dongThongTin('Ngủ', gio(nd.phutNgu))
            + dongThongTin('Tắt', gio(nd.phutTat)));
    }

    // Xếp hạng mức tải theo % công suất khuyến nghị — cùng thang màu cho cả bảng lẫn câu kết luận
    function xepHangTai(phanTram) {
        if (phanTram === null || phanTram === undefined) return { lop: 'text-muted', chu: 'chưa đủ dữ liệu' };
        if (phanTram >= 100) return { lop: 'text-danger', chu: 'vượt mức khuyến nghị' };
        if (phanTram >= 70) return { lop: 'text-warning', chu: 'khá tải' };
        if (phanTram >= 30) return { lop: 'text-primary', chu: 'bình thường' };
        return { lop: 'text-success', chu: 'rất nhàn' };
    }

    function veMucDung(md, congSuat) {
        if (!md || !md.cacKhoang || md.cacKhoang.length === 0) return '';

        var dong = md.cacKhoang.map(function (k) {
            var hang = xepHangTai(k.phanTramKhuyenNghi);
            var phanTram = k.phanTramKhuyenNghi === null || k.phanTramKhuyenNghi === undefined
                ? '<span class="text-muted">—</span>'
                : '<span class="' + hang.lop + '">' + k.phanTramKhuyenNghi + '%</span>';

            return '<tr>'
                + '<td class="small fw-bold">' + escapeHtml(k.nhan) + '</td>'
                + '<td class="small text-muted">' + escapeHtml(k.tuNgay) + ' → ' + escapeHtml(k.denNgay)
                + ' <span class="text-muted">(' + k.soNgay + ' ngày)</span></td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(k.soTrang) + '</td>'
                + '<td class="mayin-cot-so small fw-bold">' + k.trangMoiNgay + '</td>'
                + '<td class="mayin-cot-so small">' + phanTram + '</td>'
                + '</tr>';
        }).join('');

        var bang = '<table class="table table-sm mayin-bang mb-1">'
            + '<thead><tr><th>Khoảng</th><th>Thời gian</th><th class="mayin-cot-so">Số trang</th>'
            + '<th class="mayin-cot-so">Trang/ngày</th><th class="mayin-cot-so">% khuyến nghị</th></tr></thead>'
            + '<tbody>' + dong + '</tbody></table>';

        // Câu kết luận bám khoảng dài hạn: khoảng ngắn dễ bị một đợt in lớn kéo lệch
        var ketLuan = '';
        var moc = md.cacKhoang.filter(function (k) { return k.nhan === 'Dài hạn'; })[0] || md.cacKhoang[0];
        if (congSuat && moc && moc.phanTramKhuyenNghi !== null && moc.phanTramKhuyenNghi !== undefined) {
            var h = xepHangTai(moc.phanTramKhuyenNghi);
            ketLuan = '<div class="mt-1 ' + h.lop + '" style="font-weight:700;">→ Đang chạy ở ~'
                + Math.round(moc.phanTramKhuyenNghi) + '% công suất khuyến nghị, ' + h.chu + '.</div>';
        }

        return khoiVuong('<i class="fa fa-chart-simple me-1"></i> Mức sử dụng',
            bang + ketLuan
            + '<div class="text-muted" style="font-size:0.72rem;">' + escapeHtml(md.ghiChu) + '</div>',
            'mayin-khoi-rong');
    }

    function veCongSuat(cs) {
        if (!cs) {
            return khoiVuong('<i class="fa fa-gauge-high me-1"></i> Công suất khuyến nghị',
                '<div class="text-muted small">Chưa khai báo thông số công suất cho dòng máy này trong '
                + '<code>appsettings.json</code> → <code>MayIn:SpecTheoModel</code>.</div>');
        }

        // Hãng không công bố đủ 3 chỉ tiêu cho mọi dòng máy, ô nào thiếu thì nói thẳng là
        // thiếu chứ không điền số đoán.
        function dongSpec(nhan, thang, ngay) {
            if (thang === null || thang === undefined) {
                return '<tr><td class="small">' + nhan + '</td>'
                    + '<td class="mayin-cot-so small text-muted" colspan="2">hãng không công bố</td></tr>';
            }
            return '<tr><td class="small">' + nhan + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(thang) + '</td>'
                + '<td class="mayin-cot-so small">' + (ngay === null || ngay === undefined ? '—' : '~' + dinhDangSo(ngay)) + '</td></tr>';
        }

        var bang = '<table class="table table-sm mayin-bang mb-1">'
            + '<thead><tr><th></th><th class="mayin-cot-so">Mỗi tháng</th><th class="mayin-cot-so">Mỗi ngày</th></tr></thead>'
            + '<tbody>'
            + dongSpec('Khuyến nghị', cs.ampvKhuyenNghi, cs.ngayKhuyenNghi)
            + dongSpec('Tối đa', cs.ampvToiDa, cs.ngayToiDa)
            + dongSpec('Duty cycle', cs.dutyCycle, null)
            + '</tbody></table>';

        var tuoiTho = '';
        if (cs.tuoiTho) {
            var pt = cs.phanTramTuoiTho;
            tuoiTho = dongThongTin('Tuổi thọ máy', dinhDangSo(cs.tuoiTho) + ' trang cả đời')
                + (pt === null || pt === undefined ? ''
                    : dongThongTin('Đã dùng', dinhDangSo(cs.daDung) + ' trang (' + pt + '% tuổi thọ)'));
        }

        return khoiVuong('<i class="fa fa-gauge-high me-1"></i> Công suất khuyến nghị (spec hãng)',
            (cs.ten ? '<div class="small text-muted mb-1">' + escapeHtml(cs.ten) + '</div>' : '')
            + bang + tuoiTho
            + '<div class="text-muted" style="font-size:0.72rem;">Quy đổi theo '
            + cs.soNgayLamViec + ' ngày làm việc mỗi tháng.'
            + (cs.nguon ? '<br>Nguồn số: ' + escapeHtml(cs.nguon) : '') + '</div>');
    }

    function veLichSuLoi(ds) {
        if (!ds || ds.length === 0) return '';

        var dong = ds.map(function (x) {
            return '<div class="mayin-dong"><span>' + escapeHtml(x.thoiDiem) + '</span>'
                + '<span><code>' + escapeHtml(x.maLoi) + '</code> <span class="text-muted small">@ ' + dinhDangSo(x.Volume || x.volume) + '</span></span></div>';
        }).join('');

        return khoiVuong('<i class="fa fa-triangle-exclamation me-1"></i> Lịch sử lỗi gần đây', dong);
    }

    function quetThietBi() {
        if (!idDangXem) return;

        var $nut = $('#btnQuetThietBi');
        $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang quét...');
        $('#ctQuet').html('<div class="text-muted small">Đang ping, dò cổng và đọc thông số từ máy in...</div>');

        $.getJSON('/QLMayIn/QuetThietBi/' + idDangXem)
            .done(function (res) {
                if (!res.thanhCong) {
                    $('#ctQuet').html('<div class="alert alert-warning py-2 mb-0 small">' + escapeHtml(res.thongBao) + '</div>');
                    return;
                }

                var d = res.duLieu;
                var html = '<div class="mayin-luoi-quet">'
                    + veMang(d.mang)
                    + veThietBi(d.thietBi)
                    + veCounter(d.counter)
                    + veVatTu(d.vatTu)
                    + veMucDung(d.mucDung, d.congSuat)
                    + veCongSuat(d.congSuat)
                    + veKhay(d.khay)
                    + veNguonDien(d.nguonDien)
                    + veLichSuLoi(d.lichSuLoi)
                    + '</div>';

                if (!d.hoTroApi) {
                    html = '<div class="alert alert-warning py-2 small">Máy trả lời ping nhưng không có API đọc thông số '
                        + '(dòng Fuji Xerox đời cũ chạy CentreWare, SNMP tắt) — chỉ xem được phần mạng.</div>' + html;
                }

                $('#ctQuet').html(html);
                $('#ctQuetThoiDiem').text('Quét lúc ' + res.thoiDiemQuet);
            })
            .fail(function () {
                $('#ctQuet').html('<div class="alert alert-danger py-2 mb-0 small">Lỗi kết nối máy chủ khi quét.</div>');
            })
            .always(function () {
                $nut.prop('disabled', false).html('<i class="fa fa-satellite-dish me-1"></i> Quét thiết bị qua IP');
            });
    }
    // ---------- Chi tiết ----------

    // Dữ liệu máy đang mở, dùng lại khi bấm sửa / nhập chỉ số tay mà không phải gọi lại máy chủ
    var mayDangXem = null;

    function theSo(mau, id, nhan, icon) {
        return '<div class="mayin-the-so" style="--st-c: ' + mau + ';">'
            + '<div class="so" id="' + id + '">—</div>'
            + '<div class="nhan"><i class="fa ' + icon + '" style="margin-right:5px;opacity:.85;"></i>' + nhan + '</div>'
            + '</div>';
    }

    function veTheSo(tk) {
        $('#ctTheSo').html(
            theSo('#475569', 'tsChiSo', 'Chỉ số hiện tại', 'fa-gauge')
            + theSo('#ea580c', 'tsHomNay', 'Trang in hôm nay', 'fa-calendar-day')
            + theSo('#2563eb', 'tsBay', 'Trang in 7 ngày', 'fa-calendar-week')
            + theSo('#7c3aed', 'tsBaMuoi', 'Trang in 30 ngày', 'fa-file-lines')
            + theSo('#0891b2', 'tsTrungBinh', 'Trung bình mỗi ngày', 'fa-chart-line')
            + theSo('#059669', 'tsSoLanDoc', 'Lần chốt đã lưu', 'fa-database')
        );

        $('#tsChiSo').text(dinhDangSo(tk.counterTong));
        $('#tsHomNay').text(dinhDangSo(tk.trangInHomNay));
        $('#tsBay').text(dinhDangSo(tk.trangIn7Ngay));
        $('#tsBaMuoi').text(dinhDangSo(tk.trangIn30Ngay));
        $('#tsTrungBinh').text(tk.trungBinhMoiNgay === null || tk.trungBinhMoiNgay === undefined ? '—' : tk.trungBinhMoiNgay);
        $('#tsSoLanDoc').text(dinhDangSo(tk.soLanDoc));
    }

    function nhanTrangThai(tt) {
        switch (tt) {
            case 'TamDung': return '<span class="badge rounded-pill bg-warning-subtle text-warning-emphasis">Tạm dừng</span>';
            case 'BaoPhe': return '<span class="badge rounded-pill bg-secondary-subtle text-secondary-emphasis">Báo phế</span>';
            default: return '<span class="badge rounded-pill bg-success-subtle text-success-emphasis">Đang hoạt động</span>';
        }
    }

    function trong(giaTri, khiTrong) {
        var s = escapeHtml(giaTri);
        return s ? s : '<span class="text-muted">' + (khiTrong || 'chưa có') + '</span>';
    }

    // Đồng hồ tách màu chỉ có ở máy đọc được qua /home/api/billing-counter. Máy đọc qua CentreWare
    // cũ hoặc PJL không trả hai số này — ẩn hẳn khối thay vì hiện "—" gây hiểu nhầm là máy in 0 tờ.
    function veTachMau(tk) {
        var coSo = tk.counterInMau !== null && tk.counterInMau !== undefined
                || tk.counterInDenTrang !== null && tk.counterInDenTrang !== undefined;
        if (!coSo) return '';

        function keo(nhan, tong, trongKy, mau) {
            var phu = (trongKy === null || trongKy === undefined)
                ? ''
                : ' <span class="text-muted small">(30 ngày: ' + dinhDangSo(trongKy) + ' tờ)</span>';
            return dongThongTin(nhan, '<span style="color:' + mau + '; font-weight:600;">'
                + dinhDangSo(tong) + '</span>' + phu);
        }

        return keo('Trong đó: in màu', tk.counterInMau, tk.trangMau30Ngay, '#af1e78')
             + keo('Trong đó: in đen trắng', tk.counterInDenTrang, tk.trangDenTrang30Ngay, '#404040');
    }

    function veHoSo(m, tk) {
        var danhTinh = dongThongTin('Tên máy in', trong(m.tenHangDoi, 'chưa có trên print server'))
            + dongThongTin('Model', trong(m.model))
            + dongThongTin('Serial', trong(m.serial))
            + dongThongTin('Bộ phận', trong(m.boPhan))
            + dongThongTin('Vị trí', trong(m.viTri));

        var ketNoi = dongThongTin('Địa chỉ IP', m.diaChiIp ? escapeHtml(m.diaChiIp) : '<span class="text-muted">cắm USB / không nối mạng</span>')
            + dongThongTin('Trạng thái', nhanTrangThai(m.trangThai))
            + dongThongTin('Đọc tự động', m.theoDoiTuDong
                ? '<span class="text-success">Có</span>'
                : '<span class="text-muted">Không</span>')
            + dongThongTin('Đọc lần cuối', trong(m.lanDocCuoi, 'chưa đọc lần nào'))
            + dongThongTin('Kết quả lần đọc', trong(m.ketQuaDocCuoi));

        var vatTu = dongThongTin('Mực (toner)', veNhomVatTu(tk.vatTu, 'TONER', tk.toner))
            + dongThongTin('Trống (drum)', veNhomVatTu(tk.vatTu, 'DRUM', tk.drum))
            + dongThongTin('Chỉ số in', dinhDangSo(tk.counterIn))
            + dongThongTin('Chỉ số copy', dinhDangSo(tk.counterCopy))
            + dongThongTin('Chỉ số scan', dinhDangSo(tk.counterScan))
            + veTachMau(tk)
            + dongThongTin('Chốt ngày', trong(tk.ngayChiSo) + (tk.nguonMoiNhat ? ' <span class="text-muted">(' + escapeHtml(tk.nguonMoiNhat) + ')</span>' : ''));

        var hoSo = dongThongTin('Ghi chú', trong(m.ghiChu, 'không có'))
            + dongThongTin('Ngày đưa vào danh mục', trong(m.ngayTao))
            + dongThongTin('Cập nhật gần nhất', trong(m.ngayCapNhat))
            + dongThongTin('Mã trong hệ thống', '#' + m.idMayIn);

        $('#ctHoSo').html(
            khoiVuong('<i class="fa fa-tag me-1"></i> Định danh', danhTinh)
            + khoiVuong('<i class="fa fa-network-wired me-1"></i> Kết nối & theo dõi', ketNoi)
            + khoiVuong('<i class="fa fa-droplet me-1"></i> Chỉ số & vật tư gần nhất', vatTu)
            + khoiVuong('<i class="fa fa-circle-info me-1"></i> Hồ sơ', hoSo)
        );
    }

    function moChiTiet(id) {
        idDangXem = id;
        $('#ctHoSo').html('<span class="text-muted small">Đang tải...</span>');
        $('#ctLichSu').empty();

        // Lấy cả năm: nhật ký lỗi nạp về có mốc cũ hàng tháng, cắt 30 ngày thì bảng chốt theo kỳ trống
        $.getJSON('/QLMayIn/ChiTiet/' + id, { soNgay: 365 })
            .done(function (res) {
                if (!res.thanhCong) {
                    $('#ctHoSo').html('<span class="text-danger small">' + escapeHtml(res.thongBao) + '</span>');
                    return;
                }

                var m = res.may;
                mayDangXem = m;

                document.title = 'Máy in - ' + (m.tenHangDoi || m.serial);
                $('#ctTieuDe').text(m.tenHangDoi || (m.model + ' ' + m.serial));
                $('#ctPhuDe').text([m.boPhan, m.viTri, m.diaChiIp || 'không có IP'].filter(Boolean).join(' · '));

                // Máy cắm USB không quét được qua mạng nên ẩn luôn nút cho khỏi bấm nhầm
                $('#btnQuetThietBi, #btnDocChiSo, #btnNapLichSu').toggle(!!m.diaChiIp);

                veTheSo(res.thongKe);
                veHoSo(m, res.thongKe);
                veBieuDo(res.theoNgay, res.ngayChotThang);
                veChotThang(res.chotThang, res.ngayChotThang);
                veLichSu(res.lichSu);
            })
            .fail(function () {
                $('#ctHoSo').html('<span class="text-danger small">Lỗi kết nối máy chủ.</span>');
            });
    }

    function veBieuDo(theoNgay, ngayChot) {
        var ctx = document.getElementById('ctBieuDo');
        if (!ctx || typeof Chart === 'undefined') return;

        if (bieuDo) bieuDo.destroy();

        bieuDo = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: theoNgay.map(function (x) { return x.ngay; }),
                datasets: [{
                    label: 'Tổng số trang đã in',
                    data: theoNgay.map(function (x) { return x.counterTong; }),
                    // Cột rơi đúng ngày chốt sổ tô cam cho dễ dóng với bảng "Chốt theo kỳ" bên dưới
                    backgroundColor: theoNgay.map(function (x) {
                        return parseInt(x.ngay.substring(0, 2), 10) === ngayChot ? '#ea580c' : '#0ea5e9';
                    })
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (item) {
                                return 'Đồng hồ tổng: ' + dinhDangSo(theoNgay[item.dataIndex].counterTong) + ' trang';
                            },
                            // Nhịp in giữa hai lần đọc; máy không đọc được vài ngày thì nói rõ là gộp
                            afterLabel: function (item) {
                                var m = theoNgay[item.dataIndex];
                                if (m.soTrang === null || m.soTrang === undefined) return 'Mốc đầu tiên, chưa có gì để so';

                                var dong = 'In thêm ' + dinhDangSo(m.soTrang) + ' trang kể từ lần đọc trước';
                                return m.soNgayCach > 1 ? [dong, 'Cách ' + m.soNgayCach + ' ngày'] : dong;
                            }
                        }
                    }
                },
                // Đồng hồ tổng là số lớn và chỉ tăng: ép trục về 0 thì cột nào cũng cao bằng nhau,
                // nhìn không ra máy in nhiều hay ít nên để Chart.js tự chọn khoảng.
                scales: { y: { beginAtZero: false } }
            }
        });
    }

    /// Bảng chốt theo kỳ: mỗi tháng một dòng, lấy chỉ số tại ngày chốt sổ của bộ phận IT.
    function veChotThang(chotThang, ngayChot) {
        $('#ctChotThangMoTa').text('chỉ số tại ngày ' + ngayChot + ' hằng tháng');

        if (!chotThang || chotThang.length === 0) {
            $('#ctChotThang').html('<tr><td colspan="9" class="text-center text-muted py-3">'
                + 'Chưa có kỳ nào được chốt. Mỗi tháng có ít nhất một lần đọc là bảng này tự có dòng.</td></tr>');
            return;
        }

        var html = chotThang.map(function (k) {
            // Đọc lệch ngày chốt thì nói rõ lệch mấy ngày, đừng để người dùng tưởng là số đúng ngày 20
            var ngay = escapeHtml(k.ngayDoc);
            if (!k.dungNgayChot) {
                var lech = k.soNgayLech > 0 ? 'sau ' + k.soNgayLech + ' ngày' : 'trước ' + Math.abs(k.soNgayLech) + ' ngày';
                ngay += ' <span class="badge rounded-pill bg-warning-subtle text-warning-emphasis" title="'
                    + 'Ngày chốt không đọc được nên lấy lần đọc gần nhất trong tháng">' + lech + '</span>';
            }

            return '<tr>'
                + '<td class="small fw-bold">' + escapeHtml(k.ky) + '</td>'
                + '<td class="small">' + ngay + '</td>'
                + '<td class="mayin-cot-so small fw-bold">' + dinhDangSo(k.counterTong) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(k.counterIn) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(k.counterCopy) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(k.trangInTrongKy) + '</td>'
                + '<td class="mayin-cot-so small" style="color:#af1e78;">' + dinhDangSo(k.trangMauTrongKy) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(k.trangDenTrangTrongKy) + '</td>'
                + '<td class="small text-muted">' + escapeHtml(k.nguon) + '</td>'
                + '</tr>';
        }).join('');

        $('#ctChotThang').html(html);
    }

    /// Ô mực/trống trong bảng lịch sử: hiện số thấp nhất, máy màu thì rê chuột ra đủ từng màu.
    function oNhomVatTu(phanTram, dsVatTu, tienTo) {
        if (phanTram === null || phanTram === undefined) return '<td class="mayin-cot-so small">—</td>';

        var nhom = (dsVatTu || []).filter(function (v) {
            return (v.ten || '').toUpperCase().indexOf(tienTo) === 0;
        });

        if (nhom.length <= 1) return '<td class="mayin-cot-so small">' + phanTram + '%</td>';

        var chiTiet = nhom.map(function (v) {
            var k = hauToMau(v.ten);
            return (k ? TEN_VAT_TU[k] : v.ten) + ' ' + v.phanTram + '%';
        }).join(' · ');

        return '<td class="mayin-cot-so small" title="' + escapeHtml(chiTiet) + '">'
            + phanTram + '% <i class="fa fa-palette text-muted" style="font-size:.7rem;"></i></td>';
    }

    function veLichSu(lichSu) {
        if (!lichSu || lichSu.length === 0) {
            $('#ctLichSu').html('<tr><td colspan="10" class="text-center text-muted py-3">Chưa có chỉ số nào.</td></tr>');
            return;
        }

        // Chênh lệch với lần chốt liền trước = số tờ in giữa hai lần đọc; bản ghi đầu tiên
        // không có mốc để trừ nên để trống thay vì ghi 0.
        var chenh = {};
        for (var i = 1; i < lichSu.length; i++) {
            var truoc = lichSu[i - 1], sau = lichSu[i];
            if (truoc.counterTong === null || sau.counterTong === null) continue;
            var d = sau.counterTong - truoc.counterTong;
            if (d >= 0) chenh[sau.ngay] = d;
        }

        var html = lichSu.slice().reverse().map(function (c) {
            return '<tr>'
                + '<td class="small">' + escapeHtml(c.ngay) + '</td>'
                + '<td class="small text-muted">' + escapeHtml(c.thoiDiem) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(c.counterIn) + '</td>'

                + '<td class="mayin-cot-so small">' + dinhDangSo(c.counterCopy) + '</td>'
                + '<td class="mayin-cot-so small">' + dinhDangSo(c.counterScan) + '</td>'
                + '<td class="mayin-cot-so small fw-bold">' + dinhDangSo(c.counterTong) + '</td>'
                + '<td class="mayin-cot-so small">' + (chenh[c.ngay] === undefined ? '—' : dinhDangSo(chenh[c.ngay])) + '</td>'
                + oNhomVatTu(c.tonerPhanTram, c.vatTu, 'TONER')
                + oNhomVatTu(c.drumPhanTram, c.vatTu, 'DRUM')
                + '<td class="small text-muted">' + escapeHtml(c.nguon) + '</td>'
                + '</tr>';
        }).join('');

        $('#ctLichSu').html(html);
    }

    // ---------- Đọc chỉ số ngay trên trang ----------

    function docChiSo() {
        var $nut = $('#btnDocChiSo');
        $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang đọc...');

        $.post('/QLMayIn/DocChiSo/' + idDangXem)
            .done(function (res) {
                $('#ctKetQuaDoc')
                    .removeClass('text-success text-danger')
                    .addClass(res.thanhCong ? 'text-success' : 'text-danger')
                    .text(res.thongBao);

                if (res.thanhCong) moChiTiet(idDangXem);
            })
            .fail(function () {
                $('#ctKetQuaDoc').removeClass('text-success').addClass('text-danger').text('Lỗi kết nối máy chủ.');
            })
            .always(function () {
                $nut.prop('disabled', false).html('<i class="fa fa-rotate me-1"></i> Đọc chỉ số ngay');
            });
    }


    /// Nạp chỉ số quá khứ từ nhật ký lỗi của máy. Máy giữ 40 mốc lỗi gần nhất, mỗi mốc có kèm
    /// số trang đã in lúc đó — đủ để dựng lại lịch sử trước ngày hệ thống bắt đầu theo dõi.
    function napLichSu() {
        var $nut = $('#btnNapLichSu');
        $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin me-1"></i> Đang nạp...');

        $.post('/QLMayIn/NapLichSu/' + idDangXem)
            .done(function (res) {
                $('#ctKetQuaDoc')
                    .removeClass('text-success text-danger')
                    .addClass(res.thanhCong ? 'text-success' : 'text-danger')
                    .text(res.thongBao);

                if (res.thanhCong) moChiTiet(idDangXem);
            })
            .fail(function () {
                $('#ctKetQuaDoc').removeClass('text-success').addClass('text-danger').text('Lỗi kết nối máy chủ.');
            })
            .always(function () {
                $nut.prop('disabled', false).html('<i class="fa fa-clock-rotate-left me-1"></i> Nạp lịch sử');
            });
    }


    // ---------- Sửa thông tin / nhập chỉ số tay ----------

    var modalMayIn, modalChiSoTay;

    function moFormSua() {
        if (!mayDangXem) return;
        var m = mayDangXem;

        $('#mayId').val(m.idMayIn);
        $('#mayBoPhan').val(m.boPhan || '');
        $('#mayModel').val(m.model || '');
        $('#maySerial').val(m.serial || '');
        $('#mayIp').val(m.diaChiIp || '');
        $('#mayTenHangDoi').val(m.tenHangDoi || '');
        $('#mayViTri').val(m.viTri || '');
        $('#mayTrangThai').val(m.trangThai || 'HoatDong');
        $('#mayTheoDoi').prop('checked', !!m.theoDoiTuDong);
        $('#mayGhiChu').val(m.ghiChu || '');
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

        $.ajax({ url: '/QLMayIn/Luu', type: 'POST', contentType: 'application/json', data: JSON.stringify(duLieu) })
            .done(function (res) {
                if (res.thanhCong) {
                    modalMayIn.hide();
                    moChiTiet(idDangXem);
                } else {
                    alert(res.thongBao);
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $nut.prop('disabled', false).text('Lưu'); });
    }

    function moFormChiSoTay() {
        if (!mayDangXem) return;

        $('#tayId').val(mayDangXem.idMayIn);
        $('#tayThongTin').text(mayDangXem.model + ' — serial ' + mayDangXem.serial);
        $('#tayNgay').val(new Date().toISOString().substring(0, 10));
        $('#tayCounter').val('');
        $('#tayToner').val('');
        $('#tayGhiChu').val('');
        modalChiSoTay.show();
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

        $.ajax({ url: '/QLMayIn/NhapChiSoTay', type: 'POST', contentType: 'application/json', data: JSON.stringify(duLieu) })
            .done(function (res) {
                if (res.thanhCong) {
                    modalChiSoTay.hide();
                    moChiTiet(idDangXem);
                } else {
                    alert(res.thongBao);
                }
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $nut.prop('disabled', false).text('Lưu chỉ số'); });
    }
    // Trình duyệt chỉ cho đóng tab do chính script mở ra. Người dùng mở thẳng đường dẫn
    // (bookmark, dán link) thì window.close() bị bỏ qua — khi đó quay về danh sách cho khỏi kẹt.
    function dongTab() {
        window.close();
        setTimeout(function () {
            if (!window.closed) window.location.href = '/QLMayIn';
        }, 200);
    }

    $(function () {
        if (!idDangXem) return;

        modalMayIn = new bootstrap.Modal(document.getElementById('modalMayIn'));
        modalChiSoTay = new bootstrap.Modal(document.getElementById('modalChiSoTay'));

        moChiTiet(idDangXem);

        // Trang chi tiết là nơi xem kỹ một máy nên quét luôn, khỏi bắt bấm thêm một nút.
        // Chờ một nhịp cho phần dữ liệu trong CSDL hiện ra trước, vì quét mất vài giây.
        setTimeout(quetThietBi, 400);

        $('#btnDongTab').on('click', dongTab);
        $('#btnDocChiSo').on('click', docChiSo);
        $('#btnQuetThietBi').on('click', quetThietBi);
        $('#btnNapLichSu').on('click', napLichSu);
        $('#btnSuaMay').on('click', moFormSua);
        $('#btnLuuMay').on('click', luuMay);
        $('#btnNhapTay').on('click', moFormChiSoTay);
        $('#btnLuuChiSoTay').on('click', luuChiSoTay);
    });

})();
