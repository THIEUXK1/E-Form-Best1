// Màn hình Server và lưu trữ — /QLServer/{công ty}: tab "Danh sách" (KK_Server) + tab "Lịch sử" online/offline.
// Toàn bộ dữ liệu lấy qua AJAX từ QLServerController, view chỉ dựng khung.
(function () {
    'use strict';

    var COT = 16;
    var congTy, chiXem, goc, nhom = '', nguongDay = 0.85;
    var modalSv, modalXoa, modalSk, idSkDangMo = 0, xDangMo = null, cheDoLs = 'ketnoi';
    var timerTimKiem = null, timerLs = null;
    // Danh sách lần tải gần nhất (đã lọc server) + bộ lọc loại client (bấm bảng nhóm) + biểu đồ xu hướng
    var dsSvHienTai = [], locLoai = '', bieuDo = null;
    var NHOM_TRONG = '\u0000'; // khoá cho thiết bị chưa khai loại

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

    // 7.2 -> "7,2"; bỏ số 0 thừa
    function soTb(v) { return (Math.round(v * 100) / 100).toLocaleString('vi-VN'); }

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

    function laLuuTru(loai) { return loai === 'NAS' || loai === 'SAN'; }

    function oLoai(loai) {
        var icon = laLuuTru(loai) ? 'fa-hard-drive' : loai === 'Máy ảo' ? 'fa-cloud' : 'fa-server';
        return '<td class="small"><span class="sv-loai ' + (laLuuTru(loai) ? 'sv-loai-luutru' : '') + '">'
            + '<i class="fas ' + icon + ' me-1"></i>' + esc(loai) + '</span></td>';
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
        return '<td class="small text-muted" title="Job ping chưa kiểm tới thiết bị này"><span class="cam-dot" style="background:#facc15;"></span>Chưa kiểm</td>';
    }

    // ---------------- Sức khoẻ phần cứng (SNMP) ----------------

    var TRANG_THAI_SK = {
        OK: { nhan: 'Tốt', lop: 'sv-sk-ok', icon: 'fa-circle-check' },
        CANH_BAO: { nhan: 'Cảnh báo', lop: 'sv-sk-canh-bao', icon: 'fa-triangle-exclamation' },
        LOI: { nhan: 'Lỗi', lop: 'sv-sk-loi', icon: 'fa-circle-xmark' },
        KHONG_DOC_DUOC: { nhan: 'Không đọc được', lop: 'sv-sk-xam', icon: 'fa-plug-circle-xmark' },
        KHONG_RO: { nhan: 'Không rõ', lop: 'sv-sk-xam', icon: 'fa-circle-question' }
    };

    function nhanSk(tt) { return TRANG_THAI_SK[tt] || TRANG_THAI_SK.KHONG_RO; }

    function oSucKhoe(x) {
        var sk = x.sucKhoe;
        if (!sk) {
            return '<td class="small text-muted sv-cot-sk"><button type="button" class="btn btn-link btn-sm p-0 text-muted btn-suc-khoe" title="Chưa có lần đọc SNMP nào">Chưa đọc</button></td>';
        }
        var n = nhanSk(sk.trangThai);
        var tieuDe = (sk.tomTat || n.nhan) + (sk.kiemTraLuc ? '\nĐọc lúc ' + gio(sk.kiemTraLuc) : '');
        return '<td class="small sv-cot-sk" title="' + esc(tieuDe) + '">'
            + '<button type="button" class="btn btn-sm sv-sk ' + n.lop + ' btn-suc-khoe"><i class="fas ' + n.icon + ' me-1"></i>' + esc(n.nhan) + '</button>'
            + (sk.tomTat && sk.trangThai !== 'OK' ? '<div class="sv-sk-tom-tat">' + esc(sk.tomTat) + '</div>' : '')
            + '</td>';
    }

    // Thứ tự nhóm trong bảng chi tiết; nhóm lạ xếp cuối
    var THU_TU_NHOM = ['Tổng thể', 'Nhật ký', 'Ổ cứng', 'RAID', 'Dung lượng', 'Phần mềm', 'RAM', 'CPU', 'Cổng mạng', 'Cổng host',
        'Quạt', 'Nguồn', 'Nhiệt độ', 'Điện áp', 'Tụ cache', 'Thiết bị', 'Khác'];
    var ICON_NHOM = {
        'Tổng thể': 'fa-heart-pulse', 'Ổ cứng': 'fa-hard-drive', 'RAID': 'fa-layer-group', 'Dung lượng': 'fa-database',
        'RAM': 'fa-memory', 'CPU': 'fa-microchip', 'Quạt': 'fa-fan', 'Nguồn': 'fa-plug', 'Nhiệt độ': 'fa-temperature-half',
        'Điện áp': 'fa-bolt', 'Tụ cache': 'fa-car-battery', 'Thiết bị': 'fa-gears', 'Phần mềm': 'fa-download',
        'Cổng mạng': 'fa-ethernet', 'Cổng host': 'fa-plug-circle-check', 'Nhật ký': 'fa-list-check'
    };

    // Giây -> "86 ngày 3 giờ"
    function thoiGianChay(giay) {
        if (!giay && giay !== 0) return '';
        return 'chạy liên tục ' + thoiLuong(giay);
    }

    function veSuKien(ds) {
        if (!ds || !ds.length) return '';
        var lop = { 'Lỗi': 'text-danger fw-bold', 'Cảnh báo': 'text-warning fw-bold' };
        return '<div class="sv-sk-nhom mt-3"><div class="sv-sk-nhom-dau"><span><i class="fas fa-scroll me-2"></i>Nhật ký thiết bị</span>'
            + '<span class="small text-muted">' + ds.length + ' sự kiện gần nhất</span></div>'
            + '<div class="sv-sk-su-kien">' + ds.map(function (s) {
                return '<div class="sv-sk-dong"><span class="text-nowrap text-muted small" style="width:110px;flex:0 0 auto;">' + esc(gio(s.thoiGian)) + '</span>'
                    + '<span class="small ' + (lop[s.mucDo] || 'text-muted') + '" style="width:70px;flex:0 0 auto;">' + esc(s.mucDo) + '</span>'
                    + '<span class="sv-sk-ten small" title="' + esc(s.noiDung) + '" style="white-space:normal;">' + esc(s.noiDung) + '</span></div>';
            }).join('') + '</div></div>';
    }

    function veChiTietMuc(ct) {
        return '<div class="sv-sk-chi-tiet d-none">' + Object.keys(ct).map(function (k) {
            return '<div><span>' + esc(k) + '</span><span class="fw-bold">' + esc(ct[k]) + '</span></div>';
        }).join('') + '</div>';
    }
    var MUC_DO = { LOI: 0, CANH_BAO: 1, KHONG_RO: 2, OK: 3 };

    // Thông tin tĩnh đọc qua SNMP (firmware, BIOS, vị trí, load, phiên kết nối...) — không mang trạng thái
    function veThongTinSnmp(tt) {
        var ds = tt ? Object.keys(tt) : [];
        if (!ds.length) return '';
        return '<div class="sv-sk-nhom mb-3"><div class="sv-sk-nhom-dau"><span><i class="fas fa-tower-broadcast me-2"></i>Thông tin đọc từ SNMP</span></div>'
            + '<div class="sv-tt-luoi">' + ds.map(function (k) {
                return '<div><span class="text-muted">' + esc(k) + '</span><span class="fw-semibold">' + esc(tt[k]) + '</span></div>';
            }).join('') + '</div></div>';
    }

    // RAID: mỗi bộ điều khiển một tiêu đề + bảng ổ logic (mức RAID, dung lượng, trạng thái)
    function veRaid(ds) {
        if (!ds || !ds.length) return '';
        return '<div class="sv-sk-nhom mb-3"><div class="sv-sk-nhom-dau"><span><i class="fas fa-layer-group me-2"></i>RAID</span></div>'
            + ds.map(function (r) {
                var nr = nhanSk(r.trangThai);
                return '<div class="mb-2"><span class="sv-sk-cham ' + nr.lop + ' me-1" title="' + esc(nr.nhan) + '"></span>'
                    + '<span class="fw-bold">RAID Controller: </span>' + esc(r.ten)
                    + (r.firmware ? '<span class="small text-muted ms-2">firmware ' + esc(r.firmware) + '</span>' : '') + '</div>'
                    + (r.dsO && r.dsO.length
                        ? '<div class="table-responsive"><table class="table table-sm small mb-2 align-middle"><thead><tr>'
                        + '<th>Logical Drive</th><th>RAID Level</th><th class="text-end">Dung lượng</th><th>Trạng thái</th></tr></thead><tbody>'
                        + r.dsO.map(function (o) {
                            var lop = o.trangThai === 'LOI' ? 'text-danger fw-bold' : o.trangThai === 'CANH_BAO' ? 'text-warning fw-bold' : '';
                            return '<tr><td>' + esc(o.ten) + '</td><td>' + esc(o.mucRaid || '') + '</td>'
                                + '<td class="text-end text-nowrap">' + esc(o.dungLuong || '') + '</td>'
                                + '<td class="' + lop + '">' + esc(o.moTa || nhanSk(o.trangThai).nhan) + '</td></tr>';
                        }).join('') + '</tbody></table></div>'
                        : '<div class="small text-muted mb-2">Chưa có ổ logic nào.</div>');
            }).join('') + '</div>';
    }

    // Khối "Thông tin thiết bị" đầu cửa sổ chi tiết: lấy từ dòng danh sách (đã có đủ trường), chỉ hiện ô có dữ liệu
    function veThongTin(x) {
        if (!x) return '';
        var ketNoi = !x.theoDoi ? (x.diaChiIp ? 'Không theo dõi (' + (x.tinhTrang || '') + ')' : 'Chưa khai IP')
            : x.ketNoi === 'UP' ? 'Online' + (x.doiTrangThaiLuc ? ' từ ' + gio(x.doiTrangThaiLuc) : '')
            : x.ketNoi === 'DOWN' ? 'Mất kết nối' + (x.doiTrangThaiLuc ? ' từ ' + gio(x.doiTrangThaiLuc) : '')
            : 'Chưa kiểm';
        var dungLuong = x.dungLuongTb ? (x.daDungTb != null ? soTb(x.daDungTb) + ' / ' : '') + soTb(x.dungLuongTb) + ' TB'
            + (x.daDungTb != null ? ' (' + Math.round(x.daDungTb / x.dungLuongTb * 100) + '%)' : '') : '';
        var o = [
            ['Loại', x.loai], ['Mã tài sản', x.maServer], ['Chức năng', x.chucNang],
            ['Kết nối', ketNoi], ['IP', x.diaChiIp], ['IP quản trị', x.ipQuanTri],
            ['Hãng / Model', [x.hangSanXuat, x.model].filter(Boolean).join(' / ')], ['Serial', x.serial],
            ['Hệ điều hành', x.heDieuHanh], ['CPU', x.cpu], ['RAM', x.ramGb ? x.ramGb + ' GB' : ''],
            ['Dung lượng', dungLuong], ['RAID', x.raid],
            ['Bộ phận', x.tenBoPhan], ['Vị trí', x.viTri], ['Tình trạng', x.tinhTrang],
            ['Ngày lắp đặt', dinhDangNgay(x.ngayLapDat)], ['Hạn bảo hành', dinhDangNgay(x.hanBaoHanh)]
        ].filter(function (d) { return d[1]; });
        return '<div class="sv-sk-nhom mb-3"><div class="sv-sk-nhom-dau"><span><i class="fas fa-circle-info me-2"></i>Thông tin thiết bị</span></div>'
            + '<div class="sv-tt-luoi">' + o.map(function (d) {
                return '<div><span class="text-muted">' + esc(d[0]) + '</span><span class="fw-semibold">' + esc(d[1]) + '</span></div>';
            }).join('') + '</div>'
            + (x.ghiChu ? '<div class="small mt-2"><span class="text-muted">Ghi chú: </span>' + esc(x.ghiChu) + '</div>' : '')
            + '</div>'
            + '<h6 class="fw-bold mb-2"><i class="fas fa-stethoscope me-1"></i>Sức khoẻ phần cứng (SNMP)</h6>';
    }

    function veSucKhoe(res) {
        var tb = res.thietBi, sk = res.sucKhoe;
        $('#skTieuDe').text('Chi tiết · ' + tb.tenServer);
        $('#skPhuDe').text([tb.loai, tb.diaChiIp, tb.ipQuanTri ? 'quản trị ' + tb.ipQuanTri : ''].filter(Boolean).join(' · '));
        var $nd = $('#skNoiDung');
        var thongTin = veThongTin(xDangMo);

        if (!sk) {
            $('#skThoiGian').text('');
            $nd.html(thongTin + '<div class="text-muted py-3">Chưa có lần đọc SNMP nào. '
                + (res.coCommunity ? 'Job đọc ' + Math.round(res.chuKyGiay / 60) + ' phút/lần — chờ lượt tiếp theo hoặc bấm "Kiểm tra ngay".'
                    : 'Chưa cấu hình community SNMP cho nhóm thiết bị này trong .env (ServerSnmp__Community__...).') + '</div>');
            return;
        }

        var n = nhanSk(sk.trangThai);
        $('#skThoiGian').text('Kiểm tra lúc ' + gio(sk.kiemTraLuc)
            + (sk.docDuocLuc && sk.docDuocLuc !== sk.kiemTraLuc ? ' · đọc được lần cuối ' + gio(sk.docDuocLuc) : '')
            + (sk.ipSnmp ? ' · IP ' + sk.ipSnmp : ''));

        var dl = null;
        try { dl = sk.duLieu ? JSON.parse(sk.duLieu) : null; } catch (e) { dl = null; }
        var dsMuc = dl && dl.dsMuc ? dl.dsMuc : [];

        var html = thongTin + '<div class="d-flex flex-wrap align-items-center gap-2 mb-3">'
            + '<span class="sv-sk sv-sk-lon ' + n.lop + '"><i class="fas ' + n.icon + ' me-1"></i>' + esc(n.nhan) + '</span>'
            + '<span class="fw-bold">' + esc(sk.tenHeThong || '') + '</span>'
            + '<span class="text-muted">' + esc([sk.nguon, sk.moTa].filter(Boolean).join(' · ')) + '</span>'
            + (dl && dl.thoiGianChay != null ? '<span class="badge bg-light text-dark border"><i class="fas fa-clock me-1"></i>' + esc(thoiGianChay(dl.thoiGianChay)) + '</span>' : '')
            + '</div>';
        if (sk.loi) html += '<div class="alert alert-warning py-2 small">' + esc(sk.loi) + (dsMuc.length ? ' — đang hiện số liệu lần đọc được gần nhất.' : '') + '</div>';
        if (dl && dl.ghiChu) html += '<div class="small text-muted mb-2"><i class="fas fa-circle-info me-1"></i>' + esc(dl.ghiChu) + '</div>';
        html += veThongTinSnmp(dl && dl.thongTin);
        var dsRaid = dl && dl.dsRaid ? dl.dsRaid : [];
        html += veRaid(dsRaid);

        // Gom theo nhóm, mục bất thường lên đầu; controller / ổ logic đã có bảng RAID riêng thì không lặp lại
        var daVeRaid = {};
        dsRaid.forEach(function (r) { daVeRaid[r.ten] = 1; (r.dsO || []).forEach(function (o) { daVeRaid[o.ten] = 1; }); });
        var theoNhom = {};
        dsMuc.forEach(function (m) {
            if (m.nhom === 'RAID' && daVeRaid[m.ten]) return;
            (theoNhom[m.nhom] = theoNhom[m.nhom] || []).push(m);
        });
        var dsNhom = Object.keys(theoNhom).sort(function (a, b) {
            var ia = THU_TU_NHOM.indexOf(a), ib = THU_TU_NHOM.indexOf(b);
            return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
        });

        html += '<div class="row g-3">' + dsNhom.map(function (ten) {
            var ds = theoNhom[ten].slice().sort(function (a, b) { return (MUC_DO[a.trangThai] || 0) - (MUC_DO[b.trangThai] || 0); });
            var soXau = ds.filter(function (m) { return m.trangThai === 'LOI' || m.trangThai === 'CANH_BAO'; }).length;
            return '<div class="col-12 col-lg-6"><div class="sv-sk-nhom">'
                + '<div class="sv-sk-nhom-dau"><span><i class="fas ' + (ICON_NHOM[ten] || 'fa-circle') + ' me-2"></i>' + esc(ten) + '</span>'
                + '<span class="small ' + (soXau ? 'text-danger fw-bold' : 'text-muted') + '">' + (soXau ? soXau + ' bất thường / ' : '') + ds.length + '</span></div>'
                + ds.map(function (m) {
                    var nm = nhanSk(m.trangThai);
                    var coCt = m.chiTiet && Object.keys(m.chiTiet).length;
                    // Mục có bảng phụ (SMART / IO từng ổ): bấm dòng để mở
                    return '<div class="sv-sk-muc' + (coCt ? ' sv-sk-mo-duoc' : '') + '"' + (coCt ? ' title="Bấm để xem SMART / IO"' : '') + '>'
                        + '<div class="sv-sk-dong"><span class="sv-sk-cham ' + nm.lop + '" title="' + esc(nm.nhan) + '"></span>'
                        + '<span class="sv-sk-ten" title="' + esc(m.ten) + '">' + (coCt ? '<i class="fas fa-angle-right sv-sk-mui me-1"></i>' : '') + esc(m.ten) + '</span>'
                        + '<span class="sv-sk-gt ' + (m.trangThai === 'LOI' ? 'text-danger fw-bold' : m.trangThai === 'CANH_BAO' ? 'text-warning fw-bold' : '') + '">'
                        + esc(m.giaTri || nm.nhan) + '</span></div>'
                        + (coCt ? veChiTietMuc(m.chiTiet) : '')
                        + '</div>';
                }).join('')
                + '</div></div>';
        }).join('') + '</div>';

        html += veSuKien(dl && dl.dsSuKien);
        if (!dsMuc.length && !sk.loi) html += '<div class="text-muted">Thiết bị trả lời SNMP nhưng không có mục nào đọc được.</div>';
        $nd.html(html);
    }

    function moSucKhoe(x) {
        idSkDangMo = x.idServer;
        xDangMo = x;
        $('#skTieuDe').text('Chi tiết · ' + x.tenServer);
        $('#skPhuDe, #skThoiGian').text('');
        $('#skLoi').addClass('d-none').text('');
        $('#skNoiDung').html('<div class="text-muted py-3">Đang tải...</div>');
        modalSk.show();

        $.getJSON(goc + '/SucKhoe/' + x.idServer)
            .done(function (res) {
                if (!res.thanhCong) { $('#skLoi').removeClass('d-none').text(res.thongBao || 'Không tải được.'); $('#skNoiDung').html(''); return; }
                veSucKhoe(res);
            })
            .fail(function () { $('#skLoi').removeClass('d-none').text('Lỗi kết nối máy chủ.'); });
    }

    function kiemTraSucKhoe() {
        if (!idSkDangMo) return;
        var $btn = $('#btnKiemTraSk').prop('disabled', true);
        var nhanCu = $btn.html();
        $btn.html('<span class="spinner-border spinner-border-sm me-1"></span> Đang đọc SNMP...');
        $('#skLoi').addClass('d-none').text('');

        $.post(goc + '/SucKhoe/' + idSkDangMo + '/KiemTra')
            .done(function (res) {
                if (!res.thanhCong) { $('#skLoi').removeClass('d-none').text(res.thongBao || 'Không đọc được.'); return; }
                veSucKhoe(res);
                taiDanhSach();
            })
            .fail(function () { $('#skLoi').removeClass('d-none').text('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false).html(nhanCu); });
    }

    // Dung lượng: thanh % đã dùng, vượt ngưỡng thì đỏ
    function oDungLuong(x) {
        if (!x.dungLuongTb) return '<td class="small text-muted">' + (x.raid ? esc(x.raid) : '') + '</td>';
        var tong = x.dungLuongTb, dung = x.daDungTb;
        var raid = x.raid ? ' · ' + x.raid : '';
        if (dung === null || dung === undefined) {
            return '<td class="small" title="' + esc(soTb(tong) + ' TB' + raid) + '">' + esc(soTb(tong)) + ' TB'
                + (x.raid ? ' <span class="text-muted">' + esc(raid) + '</span>' : '') + '</td>';
        }
        var tyLe = dung / tong;
        var phanTram = Math.round(tyLe * 100);
        var mau = tyLe >= nguongDay ? 'sv-thanh-day' : tyLe >= nguongDay - 0.15 ? 'sv-thanh-vua' : '';
        var tieuDe = soTb(dung) + ' / ' + soTb(tong) + ' TB (' + phanTram + '%)' + raid;
        return '<td class="small" title="' + esc(tieuDe) + '">'
            + '<div class="d-flex justify-content-between"><span>' + esc(soTb(dung)) + ' / ' + esc(soTb(tong)) + ' TB</span>'
            + '<span class="fw-bold ' + (tyLe >= nguongDay ? 'text-danger' : '') + '">' + phanTram + '%</span></div>'
            + '<div class="sv-thanh"><div class="sv-thanh-dung ' + mau + '" style="width:' + Math.min(100, phanTram) + '%;"></div></div>'
            + '</td>';
    }

    // Hạn bảo hành đã qua thì tô đỏ, còn ≤ 90 ngày tô vàng
    function oHanBaoHanh(v) {
        if (!v) return '<td></td>';
        var homNay = new Date();
        var han = chuanHoaNgay(v);
        var sau90 = new Date(); sau90.setDate(sau90.getDate() + 90);
        var hetHan = han < ngayInput(homNay);
        var sapHet = !hetHan && han <= ngayInput(sau90);
        return '<td class="small ' + (hetHan ? 'text-danger fw-bold' : sapHet ? 'text-warning fw-bold' : '') + '" title="'
            + (hetHan ? 'Đã hết bảo hành' : sapHet ? 'Sắp hết bảo hành' : '') + '">' + dinhDangNgay(v) + '</td>';
    }

    // ---------------- Danh sách ----------------

    function thamSoLoc() {
        return {
            tuKhoa: $('#filterTuKhoa').val(),
            nhom: nhom,
            loai: $('#filterLoai').val(),
            idBoPhan: $('#filterBoPhan').val(),
            tinhTrang: $('#filterTinhTrang').val(),
            ketNoi: $('#filterKetNoi').val()
        };
    }

    function taiDanhSach() {
        var $body = $('#svTableBody');
        dongTrangThai($body, COT, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/DanhSach', thamSoLoc())
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, COT, res.thongBao || 'Không tải được dữ liệu.', 'text-danger');
                    return;
                }
                nguongDay = res.nguongDay || nguongDay;
                dsSvHienTai = res.duLieu || [];
                // Loại đang lọc không còn trong kết quả mới -> bỏ lọc để bảng không rỗng oan
                if (locLoai && !dsSvHienTai.some(function (x) { return khoaLoai(x) === locLoai; })) locLoai = '';
                veNhom(dsSvHienTai);
                veBang(locTheoLoai(dsSvHienTai));
                $('#svTong').text(res.tongDong);
                $('#demTatCa').text(res.tongDong);
                $('#demServer').text(res.tongServer);
                $('#demLuuTru').text(res.tongLuuTru);
                $('#tongOnline').text(res.tongOnline);
                $('#tongOffline').text(res.tongOffline);
                $('#badgeMatKetNoi').text(res.tongOffline).toggleClass('d-none', !res.tongOffline);
                veTinhTrangPing(res.kiemTraGanNhat, res.chuKyGiay, res.duLieu);
                danhDauOLoc();
            })
            .fail(function () { dongTrangThai($body, COT, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    // Lần ping gần nhất quá 3 chu kỳ: job nền không chạy ở máy chủ này -> trạng thái đang hiện là cũ
    function veTinhTrangPing(kiemTra, chuKyGiay, ds) {
        var $canhBao = $('#svCanhBaoPing');
        var m = /T(\d{2}):(\d{2})/.exec(kiemTra || '');
        $('#svLanCuoi').text(m ? m[1] + ':' + m[2] : '–');
        $('#kiemTraGanNhat').text(kiemTra ? gio(kiemTra).substring(6) + (chuKyGiay ? ' · chu kỳ ' + Math.round(chuKyGiay / 60) + ' phút' : '') : 'chưa kiểm lần nào');
        var coTheoDoi = (ds || []).some(function (x) { return x.theoDoi; });
        var cu = kiemTra && (Date.now() - new Date(kiemTra).getTime()) > 3 * chuKyGiay * 1000;
        var noiDung = !coTheoDoi ? ''
            : !kiemTra ? 'Chưa có lần ping nào — job theo dõi server chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (Server:GhiLichSu).'
            : cu ? 'Lần ping gần nhất lúc ' + gio(kiemTra) + ' — trạng thái kết nối có thể đã cũ (job theo dõi server đang tắt hoặc lỗi).'
            : '';
        $canhBao.text(noiDung).toggleClass('d-none', !noiDung);
    }

    function khoaLoai(x) { return x.loai ? x.loai : NHOM_TRONG; }

    // Lọc client theo loại đang chọn ở bảng nhóm (chồng lên bộ lọc server)
    function locTheoLoai(ds) {
        return locLoai ? (ds || []).filter(function (x) { return khoaLoai(x) === locLoai; }) : (ds || []);
    }

    // Khung "Nhóm theo loại": đếm trên toàn danh sách đã tải, bấm 1 dòng = lọc bảng theo loại đó
    function veNhom(ds) {
        var nhomDs = {};
        (ds || []).forEach(function (x) {
            var k = khoaLoai(x);
            var n = nhomDs[k] || (nhomDs[k] = { khoa: k, ten: x.loai || '(Chưa rõ loại)', so: 0, up: 0, down: 0 });
            n.so++;
            if (x.ketNoi === 'UP') n.up++;
            if (x.ketNoi === 'DOWN') n.down++;
        });
        // Loại có thiết bị mất kết nối lên đầu, rồi theo tên; "chưa rõ loại" luôn cuối
        var ds2 = Object.keys(nhomDs).map(function (k) { return nhomDs[k]; }).sort(function (a, b) {
            if ((a.khoa === NHOM_TRONG) !== (b.khoa === NHOM_TRONG)) return a.khoa === NHOM_TRONG ? 1 : -1;
            return (b.down > 0) - (a.down > 0) || a.ten.localeCompare(b.ten);
        });

        $('#svNhomBody').html(!ds2.length
            ? '<tr><td colspan="4" class="text-center text-muted py-3">Chưa có thiết bị nào.</td></tr>'
            : ds2.map(function (n) {
                return '<tr data-nhom="' + esc(n.khoa) + '" style="cursor:pointer;"' + (n.khoa === locLoai ? ' class="cam-dang-loc"' : '') + '>'
                    + '<td class="fw-bold' + (n.khoa === NHOM_TRONG ? ' text-muted fst-italic' : '') + '">' + esc(n.ten) + '</td>'
                    + '<td class="text-end">' + n.so + '</td>'
                    + '<td class="text-end text-success">' + n.up + '</td>'
                    + '<td class="text-end ' + (n.down ? 'text-danger fw-bold' : 'text-muted') + '">' + n.down + '</td>'
                    + '</tr>';
            }).join(''));
    }

    // Tô viền ô số liệu + dòng nhóm khớp bộ lọc hiện tại
    function danhDauOLoc() {
        var ketNoi = $('#filterKetNoi').val();
        $('.cam-tile-loc').each(function () { $(this).toggleClass('cam-tile-chon', String($(this).data('loc') || '') === ketNoi); });
        $('#svNhomBody tr').each(function () { $(this).toggleClass('cam-dang-loc', !!locLoai && String($(this).data('nhom')) === locLoai); });
    }

    // ---------------- Xu hướng (biểu đồ) ----------------

    function veBieuDo(diem) {
        if (typeof Chart === 'undefined') return; // CDN Chart.js không tải được thì bỏ biểu đồ, phần khác vẫn chạy
        var soGio = +$('#svKhoangXuHuong').val();
        var nhan = diem.map(function (d) { var g = gio(d.thoiGian); return soGio > 24 ? g.substring(0, 11) : g.substring(0, 5); });
        var up = diem.map(function (d) { return d.up; });
        var down = diem.map(function (d) { return d.down; });

        if (bieuDo) {
            bieuDo.data.labels = nhan;
            bieuDo.data.datasets[0].data = up;
            bieuDo.data.datasets[1].data = down;
            bieuDo.update('none');
            return;
        }

        bieuDo = new Chart(document.getElementById('svBieuDo'), {
            type: 'line',
            data: {
                labels: nhan,
                datasets: [
                    { label: 'Online', data: up, borderColor: '#16a34a', backgroundColor: 'rgba(22,163,74,.08)', fill: true, pointRadius: 0, tension: .25, yAxisID: 'y' },
                    { label: 'Mất kết nối', data: down, borderColor: '#dc2626', backgroundColor: 'rgba(220,38,38,.08)', fill: true, pointRadius: 0, tension: .25, yAxisID: 'y1' }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
                scales: {
                    x: { ticks: { maxTicksLimit: 8, autoSkip: true } },
                    // Hai trục riêng: online nhiều, mất kết nối vài cái, chung trục thì đường đỏ dẹt sát đáy
                    y: { position: 'left', title: { display: true, text: 'Online' }, ticks: { precision: 0 } },
                    y1: { position: 'right', beginAtZero: true, grid: { drawOnChartArea: false }, title: { display: true, text: 'Mất kết nối' }, ticks: { precision: 0 } }
                }
            }
        });
    }

    function taiXuHuong() {
        $.getJSON(goc + '/XuHuong', { gio: $('#svKhoangXuHuong').val() })
            .done(function (res) { if (res.thanhCong) veBieuDo(res.duLieu || []); });
    }

    function veBang(data) {
        var $body = $('#svTableBody');
        if (!data || data.length === 0) {
            dongTrangThai($body, COT, 'Chưa có thiết bị nào khớp bộ lọc.', 'text-muted');
            return;
        }

        $body.html(data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            var cpuRam = [x.cpu, x.ramGb ? x.ramGb + ' GB RAM' : ''].filter(Boolean).join(' · ');
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + oKetNoi(x)
                + oSucKhoe(x)
                + oLoai(x.loai)
                + '<td class="ccdc-ten" title="' + esc(x.tenServer + (x.maServer ? ' · ' + x.maServer : '')) + '">' + esc(x.tenServer)
                + (x.maServer ? '<div class="small text-muted fw-normal">' + esc(x.maServer) + '</div>' : '') + '</td>'
                + o(x.chucNang, 'small')
                + o(x.diaChiIp, 'small font-monospace')
                + o(x.ipQuanTri, 'small font-monospace text-muted')
                + o(hangModel, 'small text-muted')
                + o(x.heDieuHanh, 'small')
                + o(cpuRam, 'small text-muted')
                + oDungLuong(x)
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
        $body.find('tr').each(function (i) { $(this).data('sv', data[i]); });
    }

    // ---------------- Thêm / sửa / xoá ----------------

    function anLoi($khung) { $khung.addClass('d-none').text(''); }
    function hienLoi($khung, noiDung) { $khung.removeClass('d-none').text(noiDung); }

    function moModalThem() {
        $('#formSv')[0].reset();
        $('#IdServer').val(0);
        // Đang lọc nhóm Lưu trữ thì mặc định loại NAS cho đỡ chọn lại
        if (nhom === 'luutru') $('#Loai').val('NAS');
        $('#modalSvTitle').text('Thêm thiết bị ' + congTy);
        datLaiSnmp('');
        anLoi($('#svLoi'));
        modalSv.show();
    }

    var TRUONG = ['MaServer', 'TenServer', 'ChucNang', 'DiaChiIp', 'IpQuanTri', 'HangSanXuat', 'Model', 'Serial',
        'HeDieuHanh', 'Cpu', 'RamGb', 'DungLuongTb', 'DaDungTb', 'Raid', 'ViTri', 'GhiChu'];

    function moModalSua(x) {
        $('#IdServer').val(x.idServer);
        TRUONG.forEach(function (t) {
            var v = x[t.charAt(0).toLowerCase() + t.slice(1)];
            $('#' + t).val(v === null || v === undefined ? '' : v);
        });
        $('#Loai').val(x.loai);
        $('#IdboPhan').val(x.idboPhan || '');
        $('#TinhTrang').val(x.tinhTrang || $('#TinhTrang option:first').val());
        $('#NgayLapDat').val(chuanHoaNgay(x.ngayLapDat));
        $('#HanBaoHanh').val(chuanHoaNgay(x.hanBaoHanh));
        $('#modalSvTitle').text('Sửa ' + x.loai + ' ' + congTy);
        datLaiSnmp(x.ipQuanTri || x.diaChiIp || '');
        anLoi($('#svLoi'));
        modalSv.show();
    }

    // ---------------- SNMP: kiểm tra kết nối ----------------

    function datLaiSnmp(ip) {
        $('#snmpIp').val(ip);
        $('#snmpCommunity').val('');
        $('#snmpKetQua').addClass('d-none').text('');
    }

    // Đọc SNMP rồi điền form (chưa lưu): ô nào SNMP trả được thì thay, tên chỉ điền khi đang trống.
    // iLO / iDRAC là IP quản trị, còn lại là IP máy — chỉ điền IP khi ô đó trống.
    function kiemTraSnmp() {
        var $kq = $('#snmpKetQua');
        var ip = $('#snmpIp').val().trim() || $('#IpQuanTri').val().trim() || $('#DiaChiIp').val().trim();
        if (!ip) { $kq.removeClass('d-none text-success').addClass('text-danger').text('Nhập IP hoặc hostname.'); return; }
        var $btn = $('#btnKiemTraSnmp').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i> Đang đọc...');
        $kq.addClass('d-none');

        $.ajax({ url: goc + '/KiemTraSnmp', method: 'POST', timeout: 100000,
            data: { ipHoacTen: ip, community: $('#snmpCommunity').val(), loai: $('#Loai').val() } })
            .done(function (res) {
                if (!res.thanhCong) { $kq.removeClass('d-none text-success').addClass('text-danger').text(res.thongBao || 'Không đọc được SNMP.'); return; }
                var d = res.duLieu, daDien = [];
                function dien(id, v, nhan) { if (v !== null && v !== undefined && v !== '') { $('#' + id).val(v); daDien.push(nhan); } }
                var laQuanTri = d.nguon === 'HPE ProLiant' || d.nguon === 'Dell iDRAC';
                var oIp = laQuanTri ? 'IpQuanTri' : 'DiaChiIp';
                if (!$('#' + oIp).val().trim()) dien(oIp, d.ip, laQuanTri ? 'IP quản trị' : 'IP');
                if (!$('#TenServer').val().trim()) dien('TenServer', d.tenHeThong, 'tên');
                dien('HangSanXuat', d.hang, 'hãng');
                dien('Model', d.model, 'model');
                dien('Serial', d.serial, 'serial');
                dien('HeDieuHanh', d.heDieuHanh, 'hệ điều hành');
                dien('Cpu', d.cpu, 'CPU');
                dien('RamGb', d.ramGb, 'RAM');
                dien('Raid', d.raid, 'RAID');
                dien('DungLuongTb', d.dungLuongTb, 'dung lượng');
                dien('DaDungTb', d.daDungTb, 'đã dùng');
                $kq.removeClass('d-none text-danger').addClass('text-success')
                    .text(res.thongBao + (d.tenHeThong ? ' ' + d.tenHeThong + '.' : '')
                        + (daDien.length ? ' Đã điền: ' + daDien.join(', ') + '. Bấm Lưu để ghi.' : ' Thiết bị không trả thêm thông tin tài sản.'));
            })
            .fail(function () { $kq.removeClass('d-none text-success').addClass('text-danger').text('Lỗi kết nối máy chủ hoặc quá thời gian chờ.'); })
            .always(function () { $btn.prop('disabled', false).html('<i class="fas fa-plug me-1"></i> Kiểm tra kết nối'); });
    }

    function luuSv() {
        var $loi = $('#svLoi');
        if (!$('#TenServer').val().trim()) {
            hienLoi($loi, 'Vui lòng nhập tên thiết bị.');
            $('#TenServer').focus();
            return;
        }
        var tong = parseFloat($('#DungLuongTb').val()), dung = parseFloat($('#DaDungTb').val());
        if (!isNaN(tong) && !isNaN(dung) && dung > tong) {
            hienLoi($loi, 'Dung lượng đã dùng lớn hơn tổng dung lượng.');
            $('#DaDungTb').focus();
            return;
        }

        anLoi($loi);
        var $btn = $('#btnLuuSv').prop('disabled', true); // chặn double-submit tạo bản ghi trùng

        $.post(goc + '/Luu', $('#formSv').serialize())
            .done(function (res) {
                if (res.thanhCong) {
                    modalSv.hide();
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
        var $btn = $('#btnXacNhanXoaSv').prop('disabled', true);

        $.post(goc + '/Xoa', { id: $('#xoaIdServer').val(), lyDo: $('#xoaLyDo').val() })
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
        if (cheDoLs === 'suckhoe') { taiLichSuSucKhoe(); return; }
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
                $('#lsSoThietBi').text(res.soThietBi);
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
                        + o(x.tenServer, 'ccdc-ten')
                        + o(x.diaChiIp, 'small font-monospace')
                        + '<td class="small">' + (x.thoiLuongGiay ? esc(thoiLuong(x.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 6, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    function taiLichSuSucKhoe() {
        var $body = $('#lsSkBody');
        dongTrangThai($body, 5, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/SucKhoe/LichSu', { tuNgay: $('#lsTuNgay').val(), denNgay: $('#lsDenNgay').val(), tuKhoa: $('#lsTuKhoa').val() })
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, 5, res.thongBao || 'Không tải được lịch sử.', 'text-danger');
                    return;
                }
                $('#lsSkTong').text(res.tong);
                $('#lsSkGhiChu').text(res.biCat ? '(chỉ hiện ' + res.tong + ' sự kiện mới nhất)' : '');
                if (!res.duLieu.length) {
                    dongTrangThai($body, 5, 'Không có thay đổi sức khoẻ phần cứng nào trong khoảng này.', 'text-muted');
                    return;
                }
                $body.html(res.duLieu.map(function (x, i) {
                    var tu = nhanSk(x.tuTrangThai), sang = nhanSk(x.sangTrangThai);
                    return '<tr>'
                        + '<td class="text-muted small">' + (i + 1) + '</td>'
                        + '<td class="small text-nowrap">' + esc(gio(x.thoiGian)) + '</td>'
                        + o(x.tenServer, 'ccdc-ten')
                        + '<td class="small"><span class="sv-sk ' + tu.lop + '">' + esc(tu.nhan) + '</span>'
                        + ' <i class="fas fa-arrow-right text-muted mx-1"></i> <span class="sv-sk ' + sang.lop + '">' + esc(sang.nhan) + '</span></td>'
                        + o(x.tomTat, 'small')
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 5, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    $(function () {
        var trang = document.getElementById('svTrang');
        congTy = trang.getAttribute('data-cong-ty');
        chiXem = trang.getAttribute('data-chi-xem') === '1';
        goc = '/QLServer/' + encodeURIComponent(congTy);

        if (!chiXem) {
            modalSv = new bootstrap.Modal(document.getElementById('modalSv'));
            modalXoa = new bootstrap.Modal(document.getElementById('modalXoaSv'));
            $('#btnThemSv').on('click', moModalThem);
            $('#btnLuuSv').on('click', luuSv);
            $('#btnXacNhanXoaSv').on('click', xacNhanXoa);
            $('#btnKiemTraSnmp').on('click', kiemTraSnmp);
            $('#snmpIp, #snmpCommunity').on('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); kiemTraSnmp(); } });
            // Enter trong form không được submit đồng bộ (reload trang)
            $('#formSv').on('submit', function (e) { e.preventDefault(); luuSv(); });
        }

        $(document).on('click', '#paneDanhSach [data-sv-nhom]', function () {
            nhom = $(this).attr('data-sv-nhom');
            $('#paneDanhSach [data-sv-nhom]').removeClass('active');
            $(this).addClass('active');
            taiDanhSach();
        });

        $('#btnTaiLai').on('click', function () { taiDanhSach(); taiXuHuong(); });
        $('#filterLoai, #filterBoPhan, #filterTinhTrang, #filterKetNoi').on('change', taiDanhSach);

        // Bấm ô số liệu Online / Mất kết nối = bật/tắt lọc theo kết nối
        $(document).on('click', '.cam-tile-loc', function () {
            var loc = String($(this).data('loc') || '');
            $('#filterKetNoi').val($(this).hasClass('cam-tile-chon') ? '' : loc);
            taiDanhSach();
        });
        // Bấm 1 dòng bảng nhóm = bật/tắt lọc client theo loại (không gọi lại server)
        $(document).on('click', '#svNhomBody tr[data-nhom]', function () {
            var k = String($(this).data('nhom'));
            locLoai = (locLoai === k) ? '' : k;
            veBang(locTheoLoai(dsSvHienTai));
            danhDauOLoc();
        });
        $('#svKhoangXuHuong').on('change', taiXuHuong);
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

        modalSk = new bootstrap.Modal(document.getElementById('modalSucKhoe'));
        // Bấm vào dòng (hoặc ô Sức khoẻ) mở cửa sổ chi tiết; nút Sửa / Xoá giữ việc riêng của nó
        $(document).on('click', '#svTableBody tr', function (e) {
            if ($(e.target).closest('.btn-sua, .btn-xoa, a').length || window.getSelection().toString()) return;
            var x = $(this).data('sv');
            if (x) moSucKhoe(x);
        });
        // Từ cửa sổ chi tiết chuyển sang sửa
        $('#btnSuaTuChiTiet').on('click', function () {
            if (!xDangMo || chiXem) return;
            var x = xDangMo;
            $('#modalSucKhoe').one('hidden.bs.modal', function () { moModalSua(x); });
            modalSk.hide();
        });
        $('#btnKiemTraSk').on('click', kiemTraSucKhoe);
        $(document).on('click', '#skNoiDung .sv-sk-mo-duoc', function () {
            $(this).toggleClass('mo').find('.sv-sk-chi-tiet').toggleClass('d-none');
        });

        $(document).on('click', '#paneLichSu [data-ls-che-do]', function () {
            cheDoLs = $(this).attr('data-ls-che-do');
            $('#paneLichSu [data-ls-che-do]').removeClass('active');
            $(this).addClass('active');
            $('#lsKhungKetNoi').toggleClass('d-none', cheDoLs !== 'ketnoi');
            $('#lsKhungSucKhoe').toggleClass('d-none', cheDoLs !== 'suckhoe');
            // Lọc lên/xuống chỉ áp cho lịch sử kết nối
            $('#lsLoai').toggleClass('d-none', cheDoLs !== 'ketnoi');
            taiLichSu();
        });

        $(document).on('click', '#svTableBody .btn-sua', function () {
            moModalSua($(this).closest('tr').data('sv'));
        });
        $(document).on('click', '#svTableBody .btn-xoa', function () {
            var x = $(this).closest('tr').data('sv');
            $('#xoaIdServer').val(x.idServer);
            $('#xoaTenServer').text(x.tenServer);
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
        taiXuHuong();
        // Trạng thái do job nền ping 2 phút/lần: tự làm mới danh sách theo nhịp đó khi tab đang mở
        setInterval(function () {
            if (!document.hidden && $('#paneDanhSach').hasClass('active') && !$('.modal.show').length) { taiDanhSach(); taiXuHuong(); }
        }, 120000);
    });
})();
