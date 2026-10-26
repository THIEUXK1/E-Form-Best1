// Màn hình Quản lý Switch — /QLSwitch/{công ty}: tab "Danh sách switch" (KK_Switch) + tab "Lịch sử" online/offline.
// Toàn bộ dữ liệu lấy qua AJAX từ QLSwitchController, view chỉ dựng khung.
(function () {
    'use strict';

    var COT = 15;
    // BPVN có thêm cột SNMP (view chỉ dựng cột này cho BPVN) — COT tăng tương ứng lúc khởi tạo
    var congTy, chiXem, goc, coSnmp = false;
    var modalSw, modalXoa, modalDienSnmp;
    var timerTimKiem = null, timerLs = null;
    // Danh sách switch lần tải gần nhất (đã lọc ở server) + bộ lọc vai trò client (bấm bảng nhóm) + biểu đồ xu hướng
    var dsSwHienTai = [], locVaiTro = '', bieuDo = null;
    var NHOM_TRONG = '\u0000'; // khoá cho switch chưa khai vai trò

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

    // Ô SNMP (chỉ BPVN): số liệu hệ thống giám sát mạng poll ~10 phút/lần; ngưỡng CPU 80% / RAM 90% / 60 °C tô vàng
    function oSnmp(s) {
        if (!s) return '<td class="small text-muted" title="Hệ thống giám sát mạng chưa theo dõi IP này">–</td>';
        var docLuc = s.docLuc ? 'Đọc lúc ' + gio(s.docLuc) : '';
        if (s.cpu == null && s.ram == null && s.nhietDo == null) {
            var lyDo = s.loi || (s.trangThai === 'OFFLINE' ? 'Không đọc được SNMP' : 'Chưa có số liệu');
            return '<td class="small text-muted" title="' + esc(lyDo + (docLuc ? '\n' + docLuc : '')) + '">'
                + '<i class="fas fa-circle-question me-1"></i>' + esc(s.trangThai === 'OFFLINE' ? 'SNMP không trả lời' : 'Chưa có số liệu') + '</td>';
        }
        function so(v, donVi, nguong) {
            if (v == null) return '';
            var tron = Math.round(v);
            return '<span class="' + (v >= nguong ? 'text-warning fw-bold' : '') + '">' + tron + donVi + '</span>';
        }
        var dong1 = [s.cpu != null ? 'CPU ' + so(s.cpu, '%', 80) : '', s.ram != null ? 'RAM ' + so(s.ram, '%', 90) : '',
            s.nhietDo != null ? so(s.nhietDo, ' °C', 60) : ''].filter(Boolean).join(' · ');
        var dong2 = s.tongCong ? (s.congUp || 0) + ' / ' + s.tongCong + ' cổng up' : '';
        var tieuDe = [docLuc, s.thoiGianChayGiay ? 'Chạy liên tục ' + thoiLuong(s.thoiGianChayGiay) : ''].filter(Boolean).join('\n');
        return '<td class="small" title="' + esc(tieuDe) + '">' + dong1
            + (dong2 ? '<div class="text-muted" style="font-size:11px;">' + esc(dong2) + '</div>' : '') + '</td>';
    }

    // ---------------- Tra MAC ----------------

    function traMac() {
        var $nut = $('#btnTraMacTim'), $body = $('#traMacBody'), $loi = $('#traMacLoi');
        $loi.addClass('d-none').text('');
        $nut.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang tìm');

        $.getJSON(goc + '/TraMac', { q: $('#traMacQ').val() })
            .done(function (res) {
                if (!res.thanhCong) {
                    $loi.text(res.thongBao || 'Không tra được.').removeClass('d-none');
                    $body.empty();
                    return;
                }
                // Đếm số MAC khớp trên từng cổng (hiện ở title): cổng gom nhiều MAC khớp thường là đường trục
                var soMacTheoCong = {};
                res.duLieu.forEach(function (x) { var k = x.ipSwitch + '|' + x.soCong; soMacTheoCong[k] = (soMacTheoCong[k] || 0) + 1; });
                $('#traMacTomTat').text('Đoạn "' + res.doan + '": ' + res.tongDong + ' dòng'
                    + (res.tongDong > res.duLieu.length ? ' (hiện ' + res.duLieu.length + ' dòng đầu — gõ MAC dài hơn để thu hẹp)' : '') + '.');
                $body.html(res.duLieu.length ? res.duLieu.map(function (x) {
                    return '<tr>'
                        + '<td class="font-monospace">' + esc(x.mac) + '</td>'
                        + '<td class="fw-bold">' + esc(x.tenSwitch) + '</td>'
                        + '<td class="font-monospace text-muted">' + esc(x.ipSwitch) + '</td>'
                        + '<td title="' + esc('Cổng số ' + (x.soCong == null ? '?' : x.soCong) + ' · ' + soMacTheoCong[x.ipSwitch + '|' + x.soCong] + ' MAC khớp trên cổng này') + '">'
                        + esc(x.tenCong || ('Cổng ' + x.soCong)) + '</td>'
                        + '<td class="text-end">' + esc(x.vlan) + '</td>'
                        + '<td class="text-nowrap text-muted">' + esc(gio(x.thayLuc)) + '</td>'
                        + '</tr>';
                }).join('') : '<tr><td colspan="6" class="text-center text-muted py-3">Không thấy MAC này trên switch nào.</td></tr>');
            })
            .fail(function () { $loi.text('Lỗi kết nối máy chủ.').removeClass('d-none'); })
            .always(function () { $nut.prop('disabled', false).html('<i class="fas fa-magnifying-glass me-1"></i> Tìm'); });
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
                dsSwHienTai = res.duLieu || [];
                // Vai trò đang lọc không còn trong kết quả mới -> bỏ lọc để bảng không rỗng oan
                if (locVaiTro && !dsSwHienTai.some(function (x) { return khoaVaiTro(x) === locVaiTro; })) locVaiTro = '';
                veNhom(dsSwHienTai);
                veBang(locTheoVaiTro(dsSwHienTai));
                $('#swSnmpLoi').text(res.snmpLoi || '').toggleClass('d-none', !res.snmpLoi);
                $('#tongDong').text(res.tongDong);
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
        var $canhBao = $('#swCanhBaoPing');
        var m = /T(\d{2}):(\d{2})/.exec(kiemTra || '');
        $('#swLanCuoi').text(m ? m[1] + ':' + m[2] : '–');
        $('#kiemTraGanNhat').text(kiemTra ? gio(kiemTra).substring(6) + (chuKyGiay ? ' · chu kỳ ' + Math.round(chuKyGiay / 60) + ' phút' : '') : 'chưa kiểm lần nào');
        var coTheoDoi = (ds || []).some(function (x) { return x.theoDoi; });
        var cu = kiemTra && (Date.now() - new Date(kiemTra).getTime()) > 3 * chuKyGiay * 1000;
        var noiDung = !coTheoDoi ? ''
            : !kiemTra ? 'Chưa có lần ping nào — job theo dõi switch chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (Switch:GhiLichSu).'
            : cu ? 'Lần ping gần nhất lúc ' + gio(kiemTra) + ' — trạng thái kết nối có thể đã cũ (job theo dõi switch đang tắt hoặc lỗi).'
            : '';
        $canhBao.text(noiDung).toggleClass('d-none', !noiDung);
    }

    function khoaVaiTro(x) { return x.vaiTro ? x.vaiTro : NHOM_TRONG; }

    // Lọc client theo vai trò đang chọn ở bảng nhóm (chồng lên bộ lọc server)
    function locTheoVaiTro(ds) {
        return locVaiTro ? (ds || []).filter(function (x) { return khoaVaiTro(x) === locVaiTro; }) : (ds || []);
    }

    // Khung "Nhóm theo vai trò": đếm trên toàn danh sách đã tải, bấm 1 dòng = lọc bảng theo vai trò đó
    function veNhom(ds) {
        var nhom = {};
        (ds || []).forEach(function (x) {
            var k = khoaVaiTro(x);
            var n = nhom[k] || (nhom[k] = { khoa: k, ten: x.vaiTro || '(Chưa phân loại)', so: 0, up: 0, down: 0 });
            n.so++;
            if (x.ketNoi === 'UP') n.up++;
            if (x.ketNoi === 'DOWN') n.down++;
        });
        // Vai trò có switch mất kết nối lên đầu, rồi theo tên; "chưa phân loại" luôn cuối
        var ds2 = Object.keys(nhom).map(function (k) { return nhom[k]; }).sort(function (a, b) {
            if ((a.khoa === NHOM_TRONG) !== (b.khoa === NHOM_TRONG)) return a.khoa === NHOM_TRONG ? 1 : -1;
            return (b.down > 0) - (a.down > 0) || a.ten.localeCompare(b.ten);
        });

        $('#swNhomBody').html(!ds2.length
            ? '<tr><td colspan="4" class="text-center text-muted py-3">Chưa có switch nào.</td></tr>'
            : ds2.map(function (n) {
                return '<tr data-nhom="' + esc(n.khoa) + '" style="cursor:pointer;"' + (n.khoa === locVaiTro ? ' class="cam-dang-loc"' : '') + '>'
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
        $('#swNhomBody tr').each(function () { $(this).toggleClass('cam-dang-loc', !!locVaiTro && String($(this).data('nhom')) === locVaiTro); });
    }

    // ---------------- Xu hướng (biểu đồ) ----------------

    function veBieuDo(diem) {
        if (typeof Chart === 'undefined') return; // CDN Chart.js không tải được thì bỏ biểu đồ, phần khác vẫn chạy
        var soGio = +$('#swKhoangXuHuong').val();
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

        bieuDo = new Chart(document.getElementById('swBieuDo'), {
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
        $.getJSON(goc + '/XuHuong', { gio: $('#swKhoangXuHuong').val() })
            .done(function (res) { if (res.thanhCong) veBieuDo(res.duLieu || []); });
    }

    function veBang(data) {
        var $body = $('#swTableBody');
        if (!data || data.length === 0) {
            dongTrangThai($body, COT, 'Chưa có switch nào khớp bộ lọc.', 'text-muted');
            return;
        }

        $body.html(data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            return '<tr class="sw-dong" style="cursor:pointer;" title="Bấm để xem chi tiết">'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + oKetNoi(x)
                + (coSnmp ? oSnmp(x.snmp) : '')
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
        datLaiSnmp('');
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
        datLaiSnmp(x.diaChiIp || '');
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

    // ---------------- SNMP: kiểm tra kết nối / điền hàng loạt ----------------

    function datLaiSnmp(ip) {
        $('#snmpIp').val(ip);
        $('#snmpCommunity').val('');
        $('#snmpKetQua').addClass('d-none').text('');
    }

    // Đọc SNMP 1 switch rồi điền form (chưa lưu): ô nào SNMP trả được thì thay, tên chỉ điền khi đang trống
    function kiemTraSnmp() {
        var $kq = $('#snmpKetQua');
        var ip = $('#snmpIp').val().trim() || $('#DiaChiIp').val().trim();
        if (!ip) { $kq.removeClass('d-none text-success').addClass('text-danger').text('Nhập IP hoặc hostname.'); return; }
        var $btn = $('#btnKiemTraSnmp').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i> Đang đọc...');
        $kq.addClass('d-none');

        $.post(goc + '/KiemTraSnmp', { ipHoacTen: ip, community: $('#snmpCommunity').val() })
            .done(function (res) {
                if (!res.thanhCong) { $kq.removeClass('d-none text-success').addClass('text-danger').text(res.thongBao || 'Không đọc được SNMP.'); return; }
                var d = res.duLieu, daDien = [];
                function dien(id, v, nhan) { if (v !== null && v !== undefined && v !== '') { $('#' + id).val(v); daDien.push(nhan); } }
                dien('DiaChiIp', d.ip, 'IP');
                if (!$('#TenSwitch').val().trim()) dien('TenSwitch', d.tenHeThong, 'tên');
                dien('HangSanXuat', d.hang, 'hãng');
                dien('Model', d.model, 'model');
                dien('Serial', d.serial, 'serial');
                dien('PhienBan', d.firmware, 'firmware');
                dien('DiaChiMac', d.mac, 'MAC');
                dien('SoCong', d.soCong, 'số cổng');
                $kq.removeClass('d-none text-danger').addClass('text-success')
                    .text(res.thongBao + (d.tenHeThong ? ' ' + d.tenHeThong + '.' : '') + ' Đã điền: ' + daDien.join(', ') + '. Bấm Lưu để ghi.');
            })
            .fail(function () { $kq.removeClass('d-none text-success').addClass('text-danger').text('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false).html('<i class="fas fa-plug me-1"></i> Kiểm tra kết nối'); });
    }

    function dienTuSnmp() {
        var $kq = $('#dienSnmpKetQua').addClass('d-none');
        var $btn = $('#btnXacNhanDienSnmp').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i> Đang đọc...');
        $.ajax({ url: goc + '/DienTuSnmp', method: 'POST', data: { community: $('#dienSnmpCommunity').val() }, timeout: 180000 })
            .done(function (res) {
                $kq.removeClass('d-none alert-success alert-danger').addClass(res.thanhCong ? 'alert-success' : 'alert-danger')
                    .text(res.thongBao || (res.thanhCong ? 'Xong.' : 'Không thành công.'));
                if (res.thanhCong) taiDanhSach();
            })
            .fail(function () { $kq.removeClass('d-none alert-success').addClass('alert-danger').text('Lỗi kết nối máy chủ hoặc quá thời gian chờ.'); })
            .always(function () { $btn.prop('disabled', false).html('<i class="fas fa-play me-1"></i> Bắt đầu'); });
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


    // ---------------- Chi tiết 1 switch ----------------

    var swDangXem = null;

    function oChiTiet(nhan, giaTri, className) {
        return '<div class="col-6 col-md-4"><div class="text-muted" style="font-size:11px;">' + esc(nhan) + '</div>'
            + '<div class="' + (className || '') + '" style="word-break:break-word;">' + (giaTri === '' || giaTri == null ? '<span class="text-muted">–</span>' : giaTri) + '</div></div>';
    }

    function moChiTiet(x) {
        swDangXem = x;
        $('#ctTieuDe').text(x.tenSwitch || x.maSwitch || 'Chi tiết switch');

        // Dùng lại đúng ô trạng thái của bảng để hai nơi hiện giống nhau
        var $ketNoi = $(oKetNoi(x)), ketNoiHtml = $ketNoi.html() + ($ketNoi.attr('title') ? ' <span class="text-muted fw-normal">(' + esc($ketNoi.attr('title')) + ')</span>' : '');
        $('#ctTrangThai').html('<span class="' + esc($ketNoi.attr('class')) + '">' + ketNoiHtml + '</span>'
            + '<span class="badge rounded-pill" style="' + mauTinhTrang(x.tinhTrang) + '">' + esc(x.tinhTrang || 'Chưa rõ') + '</span>');

        var hetHan = x.hanBaoHanh && chuanHoaNgay(x.hanBaoHanh) < ngayInput(new Date());
        $('#ctThongTin').html([
            oChiTiet('Mã', esc(x.maSwitch)),
            oChiTiet('Tên switch', esc(x.tenSwitch), 'fw-bold'),
            oChiTiet('Vai trò', esc(x.vaiTro)),
            oChiTiet('Địa chỉ IP', esc(x.diaChiIp), 'font-monospace'),
            oChiTiet('Địa chỉ MAC', esc(x.diaChiMac), 'font-monospace'),
            oChiTiet('Số cổng', esc(x.soCong)),
            oChiTiet('Hãng sản xuất', esc(x.hangSanXuat)),
            oChiTiet('Model', esc(x.model)),
            oChiTiet('Serial', esc(x.serial), 'font-monospace'),
            oChiTiet('Firmware', esc(x.phienBan)),
            oChiTiet('Bộ phận', esc(x.tenBoPhan)),
            oChiTiet('Vị trí', esc(x.viTri)),
            oChiTiet('Ngày lắp đặt', esc(dinhDangNgay(x.ngayLapDat))),
            oChiTiet('Hạn bảo hành', esc(dinhDangNgay(x.hanBaoHanh)) + (hetHan ? ' <span class="text-danger fw-bold">(đã hết)</span>' : '')),
            oChiTiet('Ping gần nhất', esc(gio(x.kiemTraLuc))),
            '<div class="col-12"><div class="text-muted" style="font-size:11px;">Ghi chú</div><div style="white-space:pre-wrap; word-break:break-word;">'
                + (x.ghiChu ? esc(x.ghiChu) : '<span class="text-muted">–</span>') + '</div></div>'
        ].join(''));

        var s = x.snmp;
        $('#ctKhungSnmp').toggleClass('d-none', !coSnmp);
        if (coSnmp) {
            $('#ctSnmp').html(!s
                ? '<div class="col-12 text-muted">Hệ thống giám sát mạng chưa theo dõi IP này.</div>'
                : [
                    oChiTiet('Trạng thái', esc(s.trangThai) + (s.loi ? ' <span class="text-danger">· ' + esc(s.loi) + '</span>' : '')),
                    oChiTiet('CPU', s.cpu != null ? Math.round(s.cpu) + '%' : '', s.cpu >= 80 ? 'text-warning fw-bold' : ''),
                    oChiTiet('RAM', s.ram != null ? Math.round(s.ram) + '%' : '', s.ram >= 90 ? 'text-warning fw-bold' : ''),
                    oChiTiet('Nhiệt độ', s.nhietDo != null ? Math.round(s.nhietDo) + ' °C' : '', s.nhietDo >= 60 ? 'text-warning fw-bold' : ''),
                    oChiTiet('Cổng up', s.tongCong ? (s.congUp || 0) + ' / ' + s.tongCong : ''),
                    oChiTiet('Chạy liên tục', s.thoiGianChayGiay ? esc(thoiLuong(s.thoiGianChayGiay)) : ''),
                    oChiTiet('IP SNMP', esc(s.ip), 'font-monospace'),
                    oChiTiet('Đọc lúc', esc(gio(s.docLuc)))
                ].join(''));
        }

        taiLichSuChiTiet(x.idSwitch);
        bootstrap.Modal.getOrCreateInstance(document.getElementById('modalChiTietSw')).show();
    }

    function taiLichSuChiTiet(idSwitch) {
        var $body = $('#ctLsBody');
        $('#ctLsTomTat').text('');
        dongTrangThai($body, 3, 'Đang tải...', 'text-muted');
        var truoc = new Date(); truoc.setDate(truoc.getDate() - 29);

        $.getJSON(goc + '/LichSu', { idSwitch: idSwitch, tuNgay: ngayInput(truoc), denNgay: ngayInput(new Date()) })
            .done(function (res) {
                // Người dùng đã mở switch khác trong lúc chờ -> bỏ kết quả cũ
                if (!swDangXem || swDangXem.idSwitch !== idSwitch) return;
                if (!res.thanhCong) { dongTrangThai($body, 3, res.thongBao || 'Không tải được lịch sử.', 'text-danger'); return; }
                $('#ctLsTomTat').text('(' + res.soDown + ' lần mất kết nối)');
                if (!res.duLieu.length) { dongTrangThai($body, 3, 'Không có sự kiện đổi trạng thái nào trong 30 ngày.', 'text-muted'); return; }
                $body.html(res.duLieu.map(function (l) {
                    return '<tr>'
                        + '<td class="text-nowrap">' + esc(gio(l.thoiGian)) + '</td>'
                        + (l.sangTrangThai === 'DOWN'
                            ? '<td class="text-danger fw-bold"><i class="fas fa-arrow-down me-1"></i>Mất kết nối</td>'
                            : '<td class="text-success fw-bold"><i class="fas fa-arrow-up me-1"></i>Hoạt động lại</td>')
                        + '<td>' + (l.thoiLuongGiay ? esc(thoiLuong(l.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 3, 'Lỗi kết nối máy chủ.', 'text-danger'); });
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

    // ---------------- Máy chủ (quét dải IP) ----------------

    function oKetNoiMc(x) {
        var tu = x.doiTrangThaiLuc ? 'Từ ' + gio(x.doiTrangThaiLuc) : '';
        if (x.ketNoi === 'UP') return '<td class="small text-success fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#22c55e;"></span>Online</td>';
        var da = x.doiTrangThaiLuc ? thoiLuong((Date.now() - new Date(x.doiTrangThaiLuc).getTime()) / 1000) : '';
        return '<td class="small text-danger fw-bold" title="' + esc(tu) + '"><span class="cam-dot" style="background:#ef4444;"></span>Mất KN'
            + (da ? ' <span class="fw-normal">· ' + esc(da) + '</span>' : '') + '</td>';
    }

    function taiMayChu() {
        var $body = $('#mcBody');
        dongTrangThai($body, 7, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/MayChu/DanhSach', { tuKhoa: $('#mcTuKhoa').val(), ketNoi: $('#mcKetNoi').val(), khaiBao: $('#mcKhaiBao').val() })
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, 7, res.thongBao || 'Không tải được dữ liệu.', 'text-danger');
                    return;
                }
                $('#mcTong').text(res.tongDong);
                $('#mcOnline').text(res.tongOnline);
                $('#mcOffline').text(res.tongOffline);
                $('#mcChuaKhaiBao').text(res.tongChuaKhaiBao);
                $('#badgeMcMatKetNoi').text(res.tongOffline).toggleClass('d-none', !res.tongOffline);

                var kt = res.kiemTraGanNhat;
                var cu = kt && (Date.now() - new Date(kt).getTime()) > 3 * res.chuKyGiay * 1000;
                $('#mcKiemTra').text(kt ? '(quét lúc ' + gio(kt) + ', ' + Math.round(res.chuKyGiay / 60) + ' phút/lần)' : '');
                var canhBao = !res.tongDong ? 'Chưa có lần quét nào — job quét dải ' + res.daiMang + '.x chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (QuetIp:Bat).'
                    : cu ? 'Lần quét gần nhất lúc ' + gio(kt) + ' — trạng thái có thể đã cũ (job quét dải đang tắt hoặc lỗi).' : '';
                $('#mcCanhBao').text(canhBao).toggleClass('d-none', !canhBao);

                if (!res.duLieu.length) {
                    dongTrangThai($body, 7, res.tongDong ? 'Không có IP nào khớp bộ lọc.' : 'Chưa phát hiện IP nào trong dải.', 'text-muted');
                    return;
                }
                $body.html(res.duLieu.map(function (x, i) {
                    return '<tr>'
                        + '<td class="text-muted small">' + (i + 1) + '</td>'
                        + oKetNoiMc(x)
                        + o(x.diaChiIp, 'small font-monospace')
                        + o(x.tenMay, 'small')
                        + (x.khaiBao ? o(x.khaiBao, 'small')
                            : '<td class="small text-warning fw-bold" title="IP chưa có trong Switch / AP / thiết bị kiểm kê">Chưa khai báo</td>')
                        + '<td class="small text-nowrap">' + esc(gio(x.lanCuoiSong)) + '</td>'
                        + '<td class="small text-nowrap text-muted">' + esc(gio(x.lanDauThay)) + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 7, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    function taiMayChuLichSu() {
        var $body = $('#mcLsBody');
        dongTrangThai($body, 6, 'Đang tải dữ liệu...', 'text-muted');

        $.getJSON(goc + '/MayChu/LichSu', {
            tuNgay: $('#mcLsTuNgay').val(),
            denNgay: $('#mcLsDenNgay').val(),
            loai: $('#mcLsLoai').val(),
            tuKhoa: $('#mcLsTuKhoa').val()
        })
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, 6, res.thongBao || 'Không tải được lịch sử.', 'text-danger');
                    return;
                }
                $('#mcLsTong').text(res.tong);
                $('#mcLsDown').text(res.soDown);
                $('#mcLsUp').text(res.soUp);
                $('#mcLsSoMay').text(res.soMay);
                $('#mcLsGhiChu').text(res.biCat ? '(chỉ hiện ' + res.tong + ' sự kiện mới nhất, thu hẹp khoảng ngày để xem đủ)' : '');

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
                        + o(x.diaChiIp, 'small font-monospace')
                        + o(x.tenMay, 'small')
                        + '<td class="small">' + (x.thoiLuongGiay ? esc(thoiLuong(x.thoiLuongGiay)) : '') + '</td>'
                        + '</tr>';
                }).join(''));
            })
            .fail(function () { dongTrangThai($body, 6, 'Lỗi kết nối máy chủ.', 'text-danger'); });
    }

    function ganMayChu() {
        if (!document.getElementById('paneMayChu')) return; // công ty không cấu hình dải quét

        var timerMc = null, timerMcLs = null, daTaiMc = false, daTaiMcLs = false;
        var cheDoLs = function () { return !$('#mcKhungLs').hasClass('d-none'); };

        $('#mcLsTuNgay').val($('#lsTuNgay').val());
        $('#mcLsDenNgay').val($('#lsDenNgay').val());

        $('#tabMayChuBtn').on('shown.bs.tab', function () {
            if (!daTaiMc) { daTaiMc = true; taiMayChu(); }
        });
        $(document).on('click', '#paneMayChu [data-mc-che-do]', function () {
            var ls = $(this).attr('data-mc-che-do') === 'ls';
            $('#paneMayChu [data-mc-che-do]').removeClass('active');
            $(this).addClass('active');
            $('#mcKhungDs').toggleClass('d-none', ls);
            $('#mcKhungLs').toggleClass('d-none', !ls);
            if (ls && !daTaiMcLs) { daTaiMcLs = true; taiMayChuLichSu(); }
        });

        $('#mcKetNoi, #mcKhaiBao').on('change', taiMayChu);
        $('#mcBtnTaiLai').on('click', taiMayChu);
        $('#mcTuKhoa').on('input', function () {
            clearTimeout(timerMc);
            timerMc = setTimeout(taiMayChu, 350);
        });
        $('#mcLsTuNgay, #mcLsDenNgay, #mcLsLoai').on('change', taiMayChuLichSu);
        $('#mcLsBtnTaiLai').on('click', taiMayChuLichSu);
        $('#mcLsTuKhoa').on('input', function () {
            clearTimeout(timerMcLs);
            timerMcLs = setTimeout(taiMayChuLichSu, 350);
        });

        // Job quét 5 phút/lần: làm mới theo nhịp đó khi đang xem danh sách IP
        setInterval(function () {
            if (!document.hidden && $('#paneMayChu').hasClass('active') && !cheDoLs()) taiMayChu();
        }, 300000);
    }

    $(function () {
        var trang = document.getElementById('swTrang');
        congTy = trang.getAttribute('data-cong-ty');
        chiXem = trang.getAttribute('data-chi-xem') === '1';
        goc = '/QLSwitch/' + encodeURIComponent(congTy);
        coSnmp = congTy === 'BPVN';
        if (coSnmp) COT = 16;

        if (!chiXem) {
            modalSw = new bootstrap.Modal(document.getElementById('modalSw'));
            modalXoa = new bootstrap.Modal(document.getElementById('modalXoaSw'));
            $('#btnThemSw').on('click', moModalThem);
            $('#btnLuuSw').on('click', luuSw);
            $('#btnXacNhanXoaSw').on('click', xacNhanXoa);
            $('#btnKiemTraSnmp').on('click', kiemTraSnmp);
            $('#snmpIp, #snmpCommunity').on('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); kiemTraSnmp(); } });
            var elDienSnmp = document.getElementById('modalDienSnmp');
            if (elDienSnmp) {
                modalDienSnmp = new bootstrap.Modal(elDienSnmp);
                $('#btnDienSnmp').on('click', function () { $('#dienSnmpKetQua').addClass('d-none').text(''); modalDienSnmp.show(); });
                $('#btnXacNhanDienSnmp').on('click', dienTuSnmp);
            }
            // Enter trong form không được submit đồng bộ (reload trang)
            $('#formSw').on('submit', function (e) { e.preventDefault(); luuSw(); });
        }

        $('#btnTaiLai').on('click', function () { taiDanhSach(); taiXuHuong(); });

        // Bấm ô số liệu Online / Mất kết nối = bật/tắt lọc theo kết nối
        $(document).on('click', '.cam-tile-loc', function () {
            var loc = String($(this).data('loc') || '');
            $('#filterKetNoi').val($(this).hasClass('cam-tile-chon') ? '' : loc);
            taiDanhSach();
        });
        // Bấm 1 dòng bảng nhóm = bật/tắt lọc client theo vai trò (không gọi lại server)
        $(document).on('click', '#swNhomBody tr[data-nhom]', function () {
            var nhom = String($(this).data('nhom'));
            locVaiTro = (locVaiTro === nhom) ? '' : nhom;
            veBang(locTheoVaiTro(dsSwHienTai));
            danhDauOLoc();
        });
        $('#swKhoangXuHuong').on('change', taiXuHuong);

        // Tra MAC (chỉ BPVN — view chỉ dựng nút + modal cho BPVN)
        $(document).on('click', '#btnTraMac', function () {
            bootstrap.Modal.getOrCreateInstance(document.getElementById('modalTraMac')).show();
            setTimeout(function () { $('#traMacQ').trigger('focus'); }, 300);
        });
        $(document).on('submit', '#formTraMac', function (e) {
            e.preventDefault();
            traMac();
        });
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

        // Bấm vào dòng mở chi tiết; bỏ qua khi bấm nút thao tác hoặc đang bôi đen chữ để copy
        $(document).on('click', '#swTableBody tr.sw-dong', function (e) {
            if ($(e.target).closest('.ccdc-thaotac').length || String(window.getSelection() || '')) return;
            moChiTiet($(this).data('sw'));
        });
        $(document).on('click', '#btnCtSua', function () {
            if (!swDangXem) return;
            var x = swDangXem;
            bootstrap.Modal.getOrCreateInstance(document.getElementById('modalChiTietSw')).hide();
            moModalSua(x);
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

        ganMayChu();
        taiDanhSach();
        taiXuHuong();
        // Trạng thái do job nền ping 2 phút/lần: tự làm mới danh sách theo nhịp đó khi tab đang mở
        setInterval(function () {
            if (!document.hidden && $('#paneDanhSach').hasClass('active') && !$('.modal.show').length) { taiDanhSach(); taiXuHuong(); }
        }, 120000);
    });
})();
