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

    // AP ghim lên đầu: sở thích riêng từng máy nên lưu localStorage (giống trang camera), không lên DB.
    // Giá trị là mốc lúc ghim để AP ghim trước đứng trước. Trình duyệt chặn storage thì chỉ ghim trong phiên.
    var dsGhim = {};
    function khoaGhim() { return 'qlap.ghim.' + congTy; }
    function napGhim() {
        try { dsGhim = JSON.parse(localStorage.getItem(khoaGhim()) || '{}') || {}; } catch (e) { dsGhim = {}; }
    }
    function doiGhim(x) {
        if (dsGhim[x.idAp]) delete dsGhim[x.idAp]; else dsGhim[x.idAp] = Date.now();
        try { localStorage.setItem(khoaGhim(), JSON.stringify(dsGhim)); } catch (e) { /* bỏ qua */ }
        veBang();
    }

    // Dữ liệu lần tải gần nhất: tìm / bộ phận / tình trạng lọc ở server; kết nối / nhóm / ghim lọc tại chỗ
    // để ô số liệu và khung nhóm luôn đếm trên toàn bộ AP, bấm lọc không phải gọi lại server
    var dsAp = [];
    var NHOM_TRONG = '__trong';

    // Tham số Excel: gửi đủ bộ lọc đang chọn để file khớp đúng bảng đang xem
    function thamSoLoc() {
        return {
            tuKhoa: $('#filterTuKhoa').val(),
            idBoPhan: $('#filterBoPhan').val(),
            tinhTrang: $('#filterTinhTrang').val(),
            ketNoi: $('#filterKetNoi').val(),
            nhomAp: $('#filterNhom').val()
        };
    }

    function taiDanhSach() {
        var $body = $('#apTableBody');
        if (!dsAp.length) dongTrangThai($body, COT, 'Đang tải dữ liệu...', 'text-muted');
        var $btn = $('#btnTaiLai').prop('disabled', true);

        $.getJSON(goc + '/DanhSach', {
            tuKhoa: $('#filterTuKhoa').val(),
            idBoPhan: $('#filterBoPhan').val(),
            tinhTrang: $('#filterTinhTrang').val()
        })
            .done(function (res) {
                if (!res.thanhCong) {
                    dongTrangThai($body, COT, res.thongBao || 'Không tải được dữ liệu.', 'text-danger');
                    return;
                }
                dsAp = res.duLieu || [];
                veTongQuan(res);
                veNhom();
                veBang();
                $('#badgeMatKetNoi').text(res.tongOffline).toggleClass('d-none', !res.tongOffline);
                veTinhTrangPing(res.kiemTraGanNhat, res.chuKyGiay, dsAp);
            })
            .fail(function () { dongTrangThai($body, COT, 'Lỗi kết nối máy chủ.', 'text-danger'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function veTongQuan(res) {
        var theoDoi = dsAp.filter(function (x) { return x.theoDoi; }).length;
        var khong = dsAp.length - theoDoi;
        $('#apTongAp').text(dsAp.length);
        $('#apKhongTheoDoi').text(khong ? khong + ' AP không theo dõi' : 'đều đang theo dõi');
        $('#tongOnline').text(res.tongOnline);
        $('#apTiLeOnline').text(theoDoi ? Math.round(res.tongOnline * 100 / theoDoi) + '% AP theo dõi' : '');
        $('#tongOffline').text(res.tongOffline);

        // AP mất lâu nhất: thấy ngay cái cần đi kiểm trước
        var lauNhat = dsAp.filter(function (x) { return x.ketNoi === 'DOWN' && x.doiTrangThaiLuc; })
            .sort(function (a, b) { return String(a.doiTrangThaiLuc).localeCompare(String(b.doiTrangThaiLuc)); })[0];
        $('#apMatLauNhat').text(lauNhat ? 'lâu nhất: ' + lauNhat.tenAp + ' · ' + thoiLuong((Date.now() - new Date(lauNhat.doiTrangThaiLuc).getTime()) / 1000) : '')
            .attr('title', lauNhat ? lauNhat.tenAp : '');

        $('#tongClient').text(res.tongClient || 0);
        var dongNhat = dsAp.filter(function (x) { return x.ketNoi === 'UP' && x.soClient; })
            .sort(function (a, b) { return b.soClient - a.soClient; })[0];
        $('#apClientNhieuNhat').text(dongNhat ? 'đông nhất: ' + dongNhat.tenAp + ' (' + dongNhat.soClient + ')' : '')
            .attr('title', dongNhat ? dongNhat.tenAp : '');

        var m = /T(\d{2}):(\d{2})/.exec(res.kiemTraGanNhat || '');
        $('#apLanCuoi').text(m ? m[1] + ':' + m[2] : '–');
        $('#kiemTraGanNhat').text(res.kiemTraGanNhat ? gio(res.kiemTraGanNhat).substring(6) + ' · chu kỳ ' + Math.round(res.chuKyGiay / 60) + ' phút' : 'chưa kiểm lần nào');
    }

    function khoaNhom(x) { return x.nhomAp ? x.nhomAp : NHOM_TRONG; }

    // Khung "Nhóm AP": đếm theo AP group trên controller, bấm 1 dòng = lọc bảng theo nhóm đó
    function veNhom() {
        var nhom = {};
        dsAp.forEach(function (x) {
            var k = khoaNhom(x);
            var n = nhom[k] || (nhom[k] = { khoa: k, ten: x.nhomAp || '(Chưa có nhóm)', so: 0, up: 0, down: 0, client: 0 });
            n.so++;
            if (x.ketNoi === 'UP') { n.up++; n.client += x.soClient || 0; }
            if (x.ketNoi === 'DOWN') n.down++;
        });
        // Nhóm có AP mất kết nối lên đầu, sau đó theo tên; "chưa có nhóm" luôn cuối
        var ds = Object.keys(nhom).map(function (k) { return nhom[k]; }).sort(function (a, b) {
            if ((a.khoa === NHOM_TRONG) !== (b.khoa === NHOM_TRONG)) return a.khoa === NHOM_TRONG ? 1 : -1;
            return (b.down > 0) - (a.down > 0) || a.ten.localeCompare(b.ten);
        });

        var dangLoc = $('#filterNhom').val();
        $('#apNhomBody').html(!ds.length
            ? '<tr><td colspan="5" class="text-center text-muted py-3">Chưa có AP nào.</td></tr>'
            : ds.map(function (n) {
                return '<tr data-nhom="' + esc(n.khoa) + '"' + (n.khoa === dangLoc ? ' class="cam-dang-loc"' : '') + '>'
                    + '<td class="fw-bold' + (n.khoa === NHOM_TRONG ? ' text-muted fst-italic' : '') + '">' + esc(n.ten) + '</td>'
                    + '<td class="text-end">' + n.so + '</td>'
                    + '<td class="text-end text-success">' + n.up + '</td>'
                    + '<td class="text-end ' + (n.down ? 'text-danger fw-bold' : 'text-muted') + '">' + n.down + '</td>'
                    + '<td class="text-end text-primary">' + n.client + '</td>'
                    + '</tr>';
            }).join(''));

        // Dropdown nhóm dựng theo dữ liệu thật, giữ lựa chọn đang có
        var $sel = $('#filterNhom');
        $sel.find('option:not(:first)').remove();
        ds.forEach(function (n) { $sel.append($('<option>').val(n.khoa).text(n.ten + ' (' + n.so + ')')); });
        if (dangLoc && !nhom[dangLoc]) $sel.append($('<option>').val(dangLoc).text(dangLoc === NHOM_TRONG ? '(Chưa có nhóm)' : dangLoc));
        $sel.val(dangLoc);
    }

    function locTaiCho() {
        var ketNoi = $('#filterKetNoi').val(), nhomLoc = $('#filterNhom').val(), chiGhim = $('#filterChiGhim').is(':checked');
        var loc = dsAp.filter(function (x) {
            if (ketNoi === 'online' && x.ketNoi !== 'UP') return false;
            if (ketNoi === 'offline' && x.ketNoi !== 'DOWN') return false;
            if (nhomLoc && khoaNhom(x) !== nhomLoc) return false;
            if (chiGhim && !dsGhim[x.idAp]) return false;
            return true;
        });
        // AP đã ghim lên đầu theo thứ tự ghim; còn lại giữ thứ tự server (bộ phận / vị trí / tên)
        return loc.map(function (x, i) { return { x: x, i: i }; }).sort(function (a, b) {
            var ga = dsGhim[a.x.idAp], gb = dsGhim[b.x.idAp];
            if (ga && gb) return ga - gb;
            if (ga || gb) return ga ? -1 : 1;
            return a.i - b.i;
        }).map(function (p) { return p.x; });
    }

    // Tô viền ô số liệu khớp bộ lọc kết nối hiện tại (đổi từ dropdown cũng cập nhật theo)
    function danhDauOLoc() {
        var ketNoi = $('#filterKetNoi').val();
        $('.cam-tile-loc').each(function () { $(this).toggleClass('cam-tile-chon', String($(this).data('loc') || '') === ketNoi); });
        var nhomLoc = $('#filterNhom').val();
        $('#apNhomBody tr').each(function () { $(this).toggleClass('cam-dang-loc', !!nhomLoc && String($(this).data('nhom')) === nhomLoc); });
    }

    // Lần ping gần nhất quá 3 chu kỳ: job nền không chạy ở máy chủ này -> trạng thái đang hiện là cũ
    function veTinhTrangPing(kiemTra, chuKyGiay, ds) {
        var $canhBao = $('#apCanhBaoPing');
        var coTheoDoi = (ds || []).some(function (x) { return x.theoDoi; });
        var cu = kiemTra && (Date.now() - new Date(kiemTra).getTime()) > 3 * chuKyGiay * 1000;
        var noiDung = !coTheoDoi ? ''
            : !kiemTra ? 'Chưa có lần ping nào — job theo dõi AP chưa chạy (vài phút sau khi khởi động) hoặc đang tắt (AccessPoint:GhiLichSu).'
            : cu ? 'Lần ping gần nhất lúc ' + gio(kiemTra) + ' — trạng thái kết nối có thể đã cũ (job theo dõi AP đang tắt hoặc lỗi).'
            : '';
        $canhBao.text(noiDung).toggleClass('d-none', !noiDung);
    }

    function veBang() {
        var $body = $('#apTableBody');
        var data = locTaiCho();
        var soGhim = Object.keys(dsGhim).length;
        $('#apDem').text('Đang hiện ' + data.length + ' / ' + dsAp.length + ' AP' + (soGhim ? ' · đã ghim ' + soGhim : ''));
        danhDauOLoc();
        if (data.length === 0) {
            dongTrangThai($body, COT, dsAp.length ? 'Không có AP nào khớp bộ lọc.' : 'Chưa có AP nào khớp bộ lọc.', 'text-muted');
            return;
        }

        $body.html(data.map(function (x, i) {
            var hangModel = [x.hangSanXuat, x.model].filter(Boolean).join(' / ');
            var ghim = !!dsGhim[x.idAp];
            return '<tr class="' + (x.ketNoi === 'DOWN' ? 'cam-down ' : '') + (ghim ? 'cam-da-ghim' : '') + '">'
                + '<td class="text-muted small text-nowrap">'
                + '<button type="button" class="cam-ghim' + (ghim ? ' cam-ghim-bat' : '') + '" title="'
                + (ghim ? 'Bỏ ghim' : 'Ghim lên đầu danh sách (chỉ trên máy này)') + '"><i class="fas fa-thumbtack"></i></button>'
                + (i + 1) + '</td>'
                + oKetNoi(x)
                + o(x.maAp, 'small')
                + o(x.tenAp, 'ccdc-ten')
                + o(x.diaChiIp, 'small font-monospace')
                + o(x.diaChiMac, 'small font-monospace')
                + o(hangModel, 'small text-muted')
                // Số client chỉ có nghĩa khi AP đang online
                + o(x.ketNoi === 'UP' && x.soClient !== null && x.soClient !== undefined ? x.soClient : '', 'small text-end')
                + o(x.nhomAp, 'small text-muted')
                // SSID nhiều mạng cách dấu phẩy: thêm khoảng trắng để trình duyệt ngắt dòng đúng chỗ
                + o((x.ssid || '').split(',').map(function (t) { return t.trim(); }).filter(Boolean).join(', '), 'small ccdc-xuongdong')
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

    // ---------------- Xu hướng ----------------

    var bieuDo = null;

    function veBieuDo(diem) {
        if (typeof Chart === 'undefined') return; // CDN Chart.js không tải được thì bỏ biểu đồ, phần khác vẫn chạy
        var soGio = +$('#apKhoangXuHuong').val();
        // Khoảng ngắn chỉ cần giờ:phút; nhiều ngày thì kèm ngày/tháng
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

        bieuDo = new Chart(document.getElementById('apBieuDo'), {
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
                    // Hai trục riêng: số AP online lớn, mất kết nối vài cái, chung trục thì đường đỏ dẹt sát đáy
                    y: { position: 'left', title: { display: true, text: 'Online' }, ticks: { precision: 0 } },
                    y1: { position: 'right', beginAtZero: true, grid: { drawOnChartArea: false }, title: { display: true, text: 'Mất kết nối' }, ticks: { precision: 0 } }
                }
            }
        });
    }

    function taiXuHuong() {
        $.getJSON(goc + '/XuHuong', { gio: $('#apKhoangXuHuong').val() })
            .done(function (res) { if (res.thanhCong) veBieuDo(res.duLieu || []); });
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

        $('#btnTaiLai').on('click', function () { taiDanhSach(); taiXuHuong(); });
        $('#filterBoPhan, #filterTinhTrang').on('change', taiDanhSach);
        // Kết nối / nhóm / ghim lọc tại chỗ trên dữ liệu đã tải
        $('#filterKetNoi, #filterChiGhim').on('change', veBang);
        $('#filterNhom').on('change', veBang);
        $('#apKhoangXuHuong').on('change', taiXuHuong);

        // Bấm ô số liệu = lọc theo kết nối của ô đó; bấm lại ô đang chọn thì bỏ lọc
        $('.cam-tile-loc').on('click', function () {
            var loc = String($(this).data('loc') || '');
            $('#filterKetNoi').val($(this).hasClass('cam-tile-chon') ? '' : loc);
            veBang();
            var bang = document.getElementById('apTableBody');
            if (bang) bang.closest('.ccdc-table-wrap').scrollTop = 0;
        });
        // Bấm 1 nhóm trong khung Nhóm AP = lọc theo nhóm đó; bấm lại thì bỏ lọc
        $('#apNhomBody').on('click', 'tr[data-nhom]', function () {
            var nhom = String($(this).data('nhom'));
            $('#filterNhom').val($('#filterNhom').val() === nhom ? '' : nhom);
            veBang();
        });
        $('#apTableBody').on('click', 'button.cam-ghim', function () {
            doiGhim($(this).closest('tr').data('ap'));
        });
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

        napGhim();
        taiDanhSach();
        taiXuHuong();

        // Tự làm mới 60 giây (tắt được): bỏ qua khi tab trình duyệt ẩn, đang ở tab Lịch sử hoặc đang mở modal
        var timerTuLamMoi = null;
        function datTuLamMoi() {
            clearInterval(timerTuLamMoi);
            if (!$('#apTuLamMoi').is(':checked')) return;
            timerTuLamMoi = setInterval(function () {
                if (document.hidden || !$('#paneDanhSach').hasClass('active') || $('.modal.show').length) return;
                taiDanhSach();
                taiXuHuong();
            }, 60000);
        }
        $('#apTuLamMoi').on('change', datTuLamMoi);
        datTuLamMoi();
    });
})();
