// /QLCamera — tab "Giám sát": dữ liệu sống của hệ thống BPVN Camera ISAPI (10.0.60.238:3005),
// đọc qua QLCameraController (server giữ tài khoản, trình duyệt không gọi thẳng hệ thống kia).
// Tên trường giữ nguyên snake_case như API gốc trả về.
(function () {
    'use strict';

    var dsCamera = [];          // toàn bộ camera của lần tải gần nhất, lọc trạng thái/NVR/khu/từ khoá ở client
    var dsGhiChu = {};          // ghi chú người dùng (KK_CameraGhiChu), khoá "nvrIp|kenh"
    var dangSuaGhiChu = false;  // đang gõ ghi chú thì tự làm mới không được vẽ lại bảng (mất chữ đang gõ)

    function khoaGhiChu(c) { return c.nvr_ip + '|' + c.cam_id; }

    // Camera ghim lên đầu: sở thích riêng từng máy nên lưu localStorage của trình duyệt, không lên DB.
    // Giá trị là mốc thời gian ghim để giữ thứ tự ghim trước đứng trước. Trình duyệt chặn storage
    // (chế độ ẩn danh...) thì vẫn ghim được trong phiên, chỉ là tải lại trang sẽ mất.
    var KHOA_GHIM = 'qlcamera.ghim';
    var dsGhim = (function () {
        try { return JSON.parse(localStorage.getItem(KHOA_GHIM) || '{}') || {}; } catch (e) { return {}; }
    })();

    function luuGhim() {
        try { localStorage.setItem(KHOA_GHIM, JSON.stringify(dsGhim)); } catch (e) { /* bỏ qua */ }
    }

    function doiGhim(cam) {
        var k = khoaGhiChu(cam);
        if (dsGhim[k]) delete dsGhim[k]; else dsGhim[k] = Date.now();
        luuGhim();
        veCamera();
    }
    var bieuDo = null;
    var timerTuLamMoi = null;
    var timerTimKiem = null;
    var dangTai = false;

    function escapeHtml(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function hai(n) { return n < 10 ? '0' + n : String(n); }

    // API trả giờ địa phương không kèm múi giờ ("2026-10-06T13:59:11") nên new Date hiểu đúng giờ máy
    function ngayGio(v, coGiay) {
        if (!v) return '';
        var d = new Date(v);
        if (isNaN(d.getTime())) return escapeHtml(v);
        return hai(d.getDate()) + '/' + hai(d.getMonth() + 1) + ' ' + hai(d.getHours()) + ':' + hai(d.getMinutes())
            + (coGiay ? ':' + hai(d.getSeconds()) : '');
    }

    function khoangThoiGian(tu) {
        if (!tu) return '';
        var phut = Math.floor((Date.now() - new Date(tu).getTime()) / 60000);
        if (isNaN(phut) || phut < 0) return '';
        if (phut < 60) return phut + ' phút';
        var gio = Math.floor(phut / 60);
        if (gio < 24) return gio + ' giờ ' + (phut % 60) + ' phút';
        return Math.floor(gio / 24) + ' ngày ' + (gio % 24) + ' giờ';
    }

    function oTrangThai(status) {
        if (status === 'UP') return '<span class="text-success fw-bold"><span class="cam-dot" style="background:#22c55e;"></span>Hoạt động</span>';
        if (status === 'DOWN') return '<span class="text-danger fw-bold"><span class="cam-dot" style="background:#ef4444;"></span>Mất kết nối</span>';
        return '<span class="text-muted"><span class="cam-dot" style="background:#cbd5e1;"></span>' + escapeHtml(status || 'Chưa rõ') + '</span>';
    }

    function o(giaTri, className) {
        var s = escapeHtml(giaTri);
        return '<td class="' + (className || '') + '" title="' + s + '">' + s + '</td>';
    }

    function hienLoi(noiDung) {
        if (noiDung) $('#gsLoi').removeClass('d-none').text(noiDung);
        else $('#gsLoi').addClass('d-none').text('');
    }

    // $.getJSON kèm kiểm thanhCong; lỗi gom về một chỗ báo trên đầu tab
    function lay(url, thamSo) {
        return $.getJSON(url, thamSo || {}).then(function (res) {
            if (!res.thanhCong) return $.Deferred().reject(res.thongBao || 'Không tải được dữ liệu giám sát.').promise();
            return res.duLieu;
        }, function () {
            return $.Deferred().reject('Lỗi kết nối máy chủ E-Form.').promise();
        });
    }

    // ---------------- Tổng quan ----------------
    function veTongQuan(s) {
        $('#gsTong').text(s.total);
        $('#gsUp').text(s.up);
        $('#gsDown').text(s.down);
        $('#gsTyLe').text(s.total ? ((s.down * 100 / s.total).toFixed(1) + '% tổng camera') : '');
        $('#gsNvr').text(s.nvrs_ready + ' / ' + s.nvr_total);
        $('#gsNvrLoi').text(s.nvr_errors ? s.nvr_errors + ' đầu ghi lỗi' : 'Không có đầu ghi lỗi')
            .toggleClass('text-danger fw-bold', !!s.nvr_errors);
        $('#gsLoaiTru').text(s.excluded_count + ' / ' + s.watchlist_count);
        $('#gsLanCuoi').text(ngayGio(s.last_poll_at, true));
        $('#gsLanCuoiTrangThai').text(s.last_poll_status === 'ok' ? 'Thành công' : (s.last_poll_status || ''))
            .toggleClass('text-danger fw-bold', s.last_poll_status && s.last_poll_status !== 'ok');
        $('#badgeMatKetNoi').text(s.down).toggleClass('d-none', !s.down);
    }

    // ---------------- Đầu ghi ----------------
    function veDauGhi(nvrs) {
        var dangLoc = $('#gsNvrLoc').val();
        var soDown = {};
        dsCamera.forEach(function (c) {
            if (c.status === 'DOWN' && !c.excluded) soDown[c.nvr_ip] = (soDown[c.nvr_ip] || 0) + 1;
        });

        if (!nvrs.length) {
            $('#gsNvrBody').html('<tr><td colspan="5" class="text-center text-muted py-3">Chưa có đầu ghi nào.</td></tr>');
            return;
        }

        var html = nvrs.map(function (n) {
            var sanSang = n.readiness === 'READY' && !n.last_error;
            var down = soDown[n.nvr_ip] || 0;
            return '<tr data-ip="' + escapeHtml(n.nvr_ip) + '"' + (n.nvr_ip === dangLoc ? ' class="cam-dang-loc"' : '') + '>'
                + '<td title="' + escapeHtml(n.nvr_ip) + '"><div class="fw-bold">' + escapeHtml(n.nvr_name) + '</div>'
                + '<div class="text-muted font-monospace" style="font-size:11px;">' + escapeHtml(n.nvr_ip) + '</div></td>'
                + '<td>' + escapeHtml(n.zone) + '</td>'
                + '<td class="text-end">' + escapeHtml(n.channel_count) + '</td>'
                + '<td class="text-end ' + (down ? 'text-danger fw-bold' : 'text-muted') + '">' + down + '</td>'
                + '<td title="' + escapeHtml(n.last_error || ('Poll lúc ' + ngayGio(n.last_poll_at, true))) + '">'
                + (sanSang ? '<span class="badge rounded-pill bg-success-subtle text-success-emphasis">Sẵn sàng</span>'
                           : '<span class="badge rounded-pill bg-danger">' + escapeHtml(n.readiness || 'Lỗi') + '</span>')
                + '</td></tr>';
        }).join('');
        $('#gsNvrBody').html(html);

        // Dropdown NVR + khu vực dựng theo dữ liệu thật, giữ lựa chọn đang có
        var $nvr = $('#gsNvrLoc'), $khu = $('#gsKhuVuc');
        var khuDangChon = $khu.val();
        $nvr.find('option:not(:first)').remove();
        nvrs.forEach(function (n) {
            $nvr.append($('<option>').val(n.nvr_ip).text(n.nvr_name + ' (' + n.nvr_ip + ')'));
        });
        $nvr.val(dangLoc);

        var dsKhu = [];
        nvrs.forEach(function (n) { if (n.zone && dsKhu.indexOf(n.zone) < 0) dsKhu.push(n.zone); });
        dsKhu.sort();
        $khu.find('option:not(:first)').remove();
        dsKhu.forEach(function (k) { $khu.append($('<option>').val(k).text(k)); });
        $khu.val(khuDangChon);
    }

    // ---------------- Danh sách camera ----------------
    function veCamera() {
        var tuKhoa = ($('#gsTuKhoa').val() || '').trim().toLowerCase();
        var trangThai = $('#gsTrangThai').val();
        var nvrIp = $('#gsNvrLoc').val();
        var khuVuc = $('#gsKhuVuc').val();

        var loc = dsCamera.filter(function (c) {
            if (trangThai && c.status !== trangThai) return false;
            if (nvrIp && c.nvr_ip !== nvrIp) return false;
            if (khuVuc && c.zone !== khuVuc) return false;
            if (tuKhoa) {
                var gc = dsGhiChu[khoaGhiChu(c)];
                var chuoi = [c.name, c.ip, c.nvr_name, c.nvr_ip, c.zone, c.note, gc ? gc.ghiChu : ''].join(' ').toLowerCase();
                if (chuoi.indexOf(tuKhoa) < 0) return false;
            }
            return true;
        });

        if ($('#gsChiGhim').is(':checked')) loc = loc.filter(function (c) { return !!dsGhim[khoaGhiChu(c)]; });

        // Camera đã ghim lên đầu (theo thứ tự ghim); sau đó camera mất kết nối, mất lâu nhất trước;
        // còn lại theo đầu ghi + kênh
        loc.sort(function (a, b) {
            var ga = dsGhim[khoaGhiChu(a)], gb = dsGhim[khoaGhiChu(b)];
            if (!!ga !== !!gb) return ga ? -1 : 1;
            if (ga && gb && ga !== gb) return ga - gb;
            var da = a.status === 'DOWN' ? 0 : 1, db = b.status === 'DOWN' ? 0 : 1;
            if (da !== db) return da - db;
            if (da === 0) return String(a.down_since_at || '').localeCompare(String(b.down_since_at || ''));
            return String(a.nvr_name).localeCompare(String(b.nvr_name)) || (Number(a.cam_id) - Number(b.cam_id));
        });

        var soGhim = Object.keys(dsGhim).length;
        $('#gsDemCamera').text('Hiển thị ' + loc.length + ' / ' + dsCamera.length + ' camera'
            + (soGhim ? ' · đã ghim ' + soGhim : ''));

        if (!loc.length) {
            $('#gsCameraBody').html('<tr><td colspan="10" class="ccdc-trangthai text-muted py-4">Không có camera nào khớp bộ lọc.</td></tr>');
            return;
        }

        var html = loc.map(function (c, i) {
            var ghim = !!dsGhim[khoaGhiChu(c)];
            var lop = (c.status === 'DOWN' ? 'cam-down ' : '') + (c.excluded ? 'cam-loai-tru ' : '') + (ghim ? 'cam-da-ghim' : '');
            var ten = escapeHtml(c.name)
                + (c.is_watchlist ? '<i class="fas fa-star cam-sao" title="Camera chú ý' + (c.note ? ': ' + escapeHtml(c.note) : '') + '"></i>' : '')
                + (c.excluded ? ' <span class="badge bg-secondary">Loại trừ</span>' : '');
            var matKetNoi = c.status === 'DOWN' && c.down_since_at
                ? '<span class="text-danger fw-bold">' + khoangThoiGian(c.down_since_at) + '</span>'
                  + '<div class="text-muted" style="font-size:11px;">từ ' + ngayGio(c.down_since_at) + '</div>'
                : '';
            return '<tr class="' + lop + '" title="Bấm để xem trực tiếp">'
                + '<td class="text-muted small text-nowrap">'
                + '<button type="button" class="cam-ghim' + (ghim ? ' cam-ghim-bat' : '') + '" title="'
                + (ghim ? 'Bỏ ghim' : 'Ghim lên đầu danh sách (chỉ trên máy này)') + '"><i class="fas fa-thumbtack"></i></button>'
                + (i + 1) + '</td>'
                + '<td class="small">' + oTrangThai(c.status) + '</td>'
                + '<td class="ccdc-ten" title="' + escapeHtml(c.name) + '">' + ten + '</td>'
                + o(c.ip, 'small font-monospace')
                + o(c.nvr_name + ' (' + c.nvr_ip + ')', 'small')
                + o(c.zone, 'small')
                + '<td class="ccdc-num small">' + escapeHtml(c.cam_id) + '</td>'
                + '<td class="small">' + matKetNoi + '</td>'
                + '<td class="small text-muted">' + ngayGio(c.status_changed_at) + '</td>'
                + oGhiChu(dsGhiChu[khoaGhiChu(c)])
                + '</tr>';
        }).join('');
        var $body = $('#gsCameraBody').html(html);
        $body.find('tr').each(function (i) { $(this).data('cam', loc[i]); });
    }

    // ---------------- Ghi chú (sửa tại chỗ) ----------------
    function oGhiChu(gc) {
        if (!gc || !gc.ghiChu) return '<td class="small cam-ghi-chu"><span class="cam-ghi-chu-trong">+ ghi chú</span></td>';
        var title = gc.ghiChu + (gc.nguoiCapNhat ? '\n— ' + gc.nguoiCapNhat + ', ' + ngayGio(gc.ngayCapNhat) : '');
        return '<td class="small cam-ghi-chu" title="' + escapeHtml(title) + '">' + escapeHtml(gc.ghiChu) + '</td>';
    }

    function taiGhiChu() {
        // Lỗi ghi chú không được làm hỏng phần giám sát: lỗi thì coi như chưa có ghi chú nào
        return lay('/QLCamera/GhiChu/DanhSach').then(function (ds) {
            var map = {};
            (ds || []).forEach(function (g) { map[g.nvrIp + '|' + g.kenh] = g; });
            dsGhiChu = map;
            veMau();
        }, function () { return $.Deferred().resolve().promise(); });
    }

    /**
     * Gửi ghi chú lên server, cập nhật bộ nhớ dsGhiChu. Dùng chung cho ô trong bảng và cửa sổ xem.
     * Trả promise: resolve khi lưu được, reject(thongBao) khi lỗi.
     */
    function guiGhiChu(cam, moi) {
        return $.post('/QLCamera/GhiChu/Luu', { nvrIp: cam.nvr_ip, kenh: cam.cam_id, tenCamera: cam.name, ghiChu: moi })
            .then(function (res) {
                if (!res.thanhCong) return $.Deferred().reject(res.thongBao || 'Không lưu được ghi chú.').promise();
                var k = khoaGhiChu(cam);
                if (res.duLieu && res.duLieu.ghiChu) {
                    dsGhiChu[k] = { nvrIp: cam.nvr_ip, kenh: cam.cam_id, ghiChu: res.duLieu.ghiChu,
                        nguoiCapNhat: res.duLieu.nguoiCapNhat, ngayCapNhat: res.duLieu.ngayCapNhat };
                } else {
                    delete dsGhiChu[k];
                }
                veMau(); // nhóm "Đã từng điền" đổi theo
            }, function () {
                return $.Deferred().reject('Lỗi kết nối máy chủ, ghi chú chưa được lưu.').promise();
            });
    }

    // Vẽ lại đúng ô ghi chú của một camera trong bảng (không vẽ lại cả bảng)
    function capNhatOGhiChu(cam) {
        var k = khoaGhiChu(cam);
        $('#gsCameraBody tr').each(function () {
            var c = $(this).data('cam');
            if (c && khoaGhiChu(c) === k) $(this).find('td.cam-ghi-chu').replaceWith(oGhiChu(dsGhiChu[k]));
        });
    }

    function batDauSuaGhiChu($td) {
        if ($td.find('input').length) return;
        var cam = $td.closest('tr').data('cam');
        var gc = dsGhiChu[khoaGhiChu(cam)];
        var cu = gc ? gc.ghiChu : '';
        dangSuaGhiChu = true;

        // Nút ▾ mở danh sách "Có sẵn" + "Đã từng điền"; chọn một câu là lưu luôn
        var $nut = $('<button type="button" class="btn btn-outline-secondary dropdown-toggle" tabindex="-1" title="Chọn ghi chú có sẵn / đã từng điền">');
        var $menu = $('<ul class="dropdown-menu cam-ds-mau">');
        var $input = $('<input type="text" class="form-control" maxlength="1000">')
            .val(cu).attr('placeholder', 'Enter để lưu, Esc để huỷ');
        $td.empty().append($('<div class="input-group input-group-sm">').append($nut, $menu, $input));
        veMenuGoiY($menu, false);

        // Bảng nằm trong khung cuộn (overflow) -> menu định vị "fixed" để không bị cắt mất
        var dropdown = new bootstrap.Dropdown($nut[0], {
            popperConfig: function (macDinh) { return $.extend({}, macDinh, { strategy: 'fixed' }); }
        });
        // Giữ focus ở ô nhập khi bấm nút/menu, không thì blur sẽ lưu và đóng ô trước khi kịp chọn
        $nut.add($menu).on('mousedown', function (e) { e.preventDefault(); });
        $menu.on('click', 'a.cam-goi-y', function (e) {
            e.preventDefault();
            $input.val($(this).find('.cam-mau-chu').text());
            dropdown.hide();
            luu();
        });

        $input.trigger('focus');

        var xong = false;
        function ketThuc() { xong = true; dangSuaGhiChu = false; }
        function traLai() { dropdown.dispose(); $td.replaceWith(oGhiChu(dsGhiChu[khoaGhiChu(cam)])); }

        function luu() {
            if (xong) return;
            var moi = $input.val().trim();
            if (moi === (cu || '')) { ketThuc(); traLai(); return; }

            xong = true;
            $input.prop('disabled', true);
            guiGhiChu(cam, moi)
                .done(function () { ketThuc(); traLai(); })
                .fail(function (loi) {
                    // Giữ nguyên chữ đang gõ để người dùng sửa lại, không mất dữ liệu đã nhập
                    alert(loi);
                    xong = false;
                    $input.prop('disabled', false).trigger('focus');
                });
        }

        $input.on('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); luu(); }
            else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); ketThuc(); traLai(); }
        });
        $input.on('blur', luu);
    }

    // ---------------- Ghi chú có sẵn (dùng chung mọi người) ----------------
    var dsMau = [];

    function taiMau() {
        return lay('/QLCamera/GhiChuMau/DanhSach').done(function (ds) {
            dsMau = ds || [];
            veMau();
        });
    }

    // Các ghi chú đang gán cho camera (bỏ trùng, bỏ câu đã nằm trong danh sách có sẵn)
    function ghiChuDaDien() {
        var coSan = {}, kq = [], daGap = {};
        dsMau.forEach(function (m) { coSan[m.noiDung] = true; });
        Object.keys(dsGhiChu).forEach(function (k) {
            var t = dsGhiChu[k].ghiChu;
            if (t && !coSan[t] && !daGap[t]) { daGap[t] = true; kq.push(t); }
        });
        return kq.sort(function (a, b) { return a.localeCompare(b, 'vi'); });
    }

    /**
     * Vẽ menu gợi ý: nhóm "Có sẵn" (KK_CameraGhiChuMau) + nhóm "Đã từng điền" (ghi chú đang gán cho camera).
     * choXoaMau = true thì mỗi câu có sẵn kèm nút × để bỏ khỏi danh sách (chỉ ở cửa sổ xem).
     */
    function veMenuGoiY($menu, choXoaMau) {
        $menu.empty();
        var daDien = ghiChuDaDien();

        if (!dsMau.length && !daDien.length) {
            $menu.append('<li><span class="dropdown-item-text small text-muted">Chưa có ghi chú nào để chọn — gõ rồi bấm <b>+</b> trong cửa sổ xem camera để lưu thành có sẵn</span></li>');
            return;
        }

        function mucChon(noiDung, idMau) {
            var $a = $('<a href="#" class="dropdown-item small cam-goi-y">');
            if (idMau) $a.attr('data-id', idMau);
            $a.append($('<span class="cam-mau-chu">').text(noiDung));
            if (choXoaMau && idMau) $a.append('<span class="cam-mau-xoa" title="Bỏ khỏi danh sách có sẵn"><i class="fas fa-xmark"></i></span>');
            return $('<li>').append($a);
        }

        if (dsMau.length) {
            $menu.append('<li><h6 class="dropdown-header">Có sẵn</h6></li>');
            dsMau.forEach(function (m) { $menu.append(mucChon(m.noiDung, m.idMau)); });
        }
        if (daDien.length) {
            if (dsMau.length) $menu.append('<li><hr class="dropdown-divider"></li>');
            $menu.append('<li><h6 class="dropdown-header">Đã từng điền</h6></li>');
            daDien.forEach(function (t) { $menu.append(mucChon(t, null)); });
        }
    }

    function veMau() {
        veMenuGoiY($('#xemDsMau'), true);
    }

    function thongBaoXem(noiDung, loi) {
        var $o = $('#xemGhiChu');
        $o.attr('title', noiDung || '');
        if (loi) { alert(noiDung); return; }
        $o.addClass('cam-da-luu');
        setTimeout(function () { $o.removeClass('cam-da-luu'); }, 1200);
    }

    function luuGhiChuXem() {
        if (!xem.cam) return;
        var $btn = $('#xemLuuGhiChu').prop('disabled', true);
        var cam = xem.cam;
        guiGhiChu(cam, $('#xemGhiChu').val().trim())
            .done(function () { capNhatOGhiChu(cam); thongBaoXem('Đã lưu'); })
            .fail(function (loi) { thongBaoXem(loi, true); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function themMau() {
        var noiDung = $('#xemGhiChu').val().trim();
        if (!noiDung) { alert('Gõ nội dung ghi chú trước rồi bấm +.'); $('#xemGhiChu').trigger('focus'); return; }

        var $btn = $('#xemThemMau').prop('disabled', true);
        $.post('/QLCamera/GhiChuMau/Them', { noiDung: noiDung })
            .done(function (res) {
                if (!res.thanhCong) { alert(res.thongBao || 'Không thêm được.'); return; }
                taiMau();
                // Bấm + cũng lưu luôn ghi chú cho camera đang xem, đỡ phải bấm Lưu thêm lần nữa
                luuGhiChuXem();
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    function xoaMau(id, noiDung) {
        if (!confirm('Bỏ "' + noiDung + '" khỏi danh sách ghi chú có sẵn?\n(Ghi chú đã gán cho camera vẫn giữ nguyên.)')) return;
        $.post('/QLCamera/GhiChuMau/Xoa', { id: id })
            .done(function (res) {
                if (!res.thanhCong) { alert(res.thongBao || 'Không xoá được.'); return; }
                taiMau();
            })
            .fail(function () { alert('Lỗi kết nối máy chủ.'); });
    }

    // ---------------- Xem trực tiếp ----------------
    // Ưu tiên video (fMP4 H.264 qua go2rtc, camera H.265 được chuyển mã ở server). Video vẫn không lên
    // (đầu ghi lỗi, go2rtc tắt...) thì tự chuyển sang chế độ ảnh cập nhật mỗi giây.
    var xem = { cam: null, cheDo: null, timerAnh: null, timerCho: null, timerBam: null, modal: null };

    function urlXem(loai, cam) {
        return '/QLCamera/Xem/' + loai + '?nvrIp=' + encodeURIComponent(cam.nvr_ip)
            + '&kenh=' + encodeURIComponent(cam.cam_id) + '&t=' + Date.now();
    }

    function nhanXem(noiDung, dangPhat) {
        $('#xemNhan').text(noiDung).toggleClass('cam-xem-live', !!dangPhat);
    }

    function dungXem() {
        clearTimeout(xem.timerAnh);
        clearTimeout(xem.timerCho);
        clearInterval(xem.timerBam);
        var video = document.getElementById('xemVideo');
        // Gỡ src + load() để trình duyệt đóng kết nối, server mới biết mà ngắt luồng go2rtc
        video.onplaying = video.onerror = null;
        video.removeAttribute('src');
        video.load();
        document.getElementById('xemAnh').onload = document.getElementById('xemAnh').onerror = null;
        thuHoiAnhLuu();
    }

    function cheDoVideo() {
        dungXem();
        xem.cheDo = 'video';
        datNutCheDo('#xemCheDoVideo');

        var video = document.getElementById('xemVideo');
        var anh = document.getElementById('xemAnh');
        $(video).hide();
        $(anh).show().attr('src', urlXem('AnhChup', xem.cam)); // ảnh tĩnh che chỗ trong lúc chờ video
        nhanXem('Đang mở video...');

        video.onplaying = function () {
            clearTimeout(xem.timerCho);
            $(anh).hide();
            $(video).show();
            nhanXem('● TRỰC TIẾP', true);

            // fMP4 qua HTTP không tự đuổi theo thời gian thực: mạng chậm một nhịp là trễ dồn mãi.
            // Trễ quá 1,5 giây so với khung mới nhất đã về thì nhảy tới sát đó.
            clearInterval(xem.timerBam);
            xem.timerBam = setInterval(function () {
                if (!video.buffered.length) return;
                var cuoi = video.buffered.end(video.buffered.length - 1);
                if (cuoi - video.currentTime > 1.5) video.currentTime = cuoi - 0.3;
            }, 2000);
        };
        video.onerror = function () { chuyenSangAnh('Trình duyệt không phát được luồng video này'); };
        xem.timerCho = setTimeout(function () {
            if (video.paused || video.readyState < 2) chuyenSangAnh('Video không lên sau 15 giây');
        }, 15000);   // camera H.265 cần ~3 giây chuyển mã trước khi có hình

        video.src = urlXem('Video', xem.cam);
    }

    function chuyenSangAnh(lyDo) {
        cheDoAnh();
        nhanXem('Ảnh 1 giây/lần — ' + lyDo);
    }

    function cheDoAnh() {
        dungXem();
        xem.cheDo = 'anh';
        datNutCheDo('#xemCheDoAnh');
        $('#xemVideo').hide();

        var anh = document.getElementById('xemAnh');
        $(anh).show();
        nhanXem('Ảnh 1 giây/lần');

        // Tải ảnh kế tiếp chỉ sau khi ảnh trước về xong, mạng chậm thì tự giãn nhịp chứ không dồn request
        var loiLienTiep = 0;
        anh.onload = function () {
            loiLienTiep = 0;
            xem.timerAnh = setTimeout(taiAnh, 1000);
        };
        anh.onerror = function () {
            loiLienTiep++;
            // Lỗi ngay lần đầu thì hiện ảnh lưu sẵn luôn: máy chủ không thông mạng tới đầu ghi thì
            // mỗi lần thử lại đều phải chờ hết timeout, người xem nhìn khung đen rất lâu
            if (loiLienTiep === 1) { hienAnhLuu(); return; }
            nhanXem(loiLienTiep >= 3 ? 'Không lấy được ảnh từ đầu ghi, chưa có ảnh lưu' : 'Đang thử lại...');
            xem.timerAnh = setTimeout(taiAnh, loiLienTiep >= 3 ? 5000 : 1500);
        };
        function taiAnh() {
            if (xem.cheDo === 'anh') anh.src = urlXem('AnhChup', xem.cam);
        }

        // fetch thay vì gán src để đọc được header X-Chup-Luc (giờ chụp ảnh lưu)
        function hienAnhLuu() {
            var cam = xem.cam;
            fetch(urlXem('AnhLuu', cam), { credentials: 'same-origin' })
                .then(function (res) {
                    if (!res.ok) throw new Error('HTTP ' + res.status);
                    var luc = res.headers.get('X-Chup-Luc');
                    return res.blob().then(function (b) { return { blob: b, luc: luc }; });
                })
                .then(function (kq) {
                    if (xem.cheDo !== 'anh' || xem.cam !== cam) return;
                    anh.onload = anh.onerror = null;
                    thuHoiAnhLuu();
                    xem.urlAnhLuu = URL.createObjectURL(kq.blob);
                    anh.src = xem.urlAnhLuu;
                    nhanXem('Ảnh lưu lúc ' + dinhDangGioChup(kq.luc) + ' — máy chủ không kết nối trực tiếp được tới đầu ghi');
                    xem.timerAnh = setTimeout(thuLaiTrucTiep, 60000);
                })
                .catch(function () {
                    if (xem.cheDo !== 'anh' || xem.cam !== cam) return;
                    nhanXem('Đang thử lại...');
                    xem.timerAnh = setTimeout(taiAnh, 1500);
                });
        }

        // Thử ảnh trực tiếp bằng Image ẩn, được thì mới quay lại chế độ ảnh 1 giây/lần,
        // không thì giữ nguyên ảnh lưu đang hiện (gán thẳng vào #xemAnh sẽ ra ảnh vỡ khi lỗi)
        function thuLaiTrucTiep() {
            if (xem.cheDo !== 'anh') return;
            var cam = xem.cam;
            var thu = new Image();
            thu.onload = function () { if (xem.cheDo === 'anh' && xem.cam === cam) cheDoAnh(); };
            thu.onerror = function () { if (xem.cheDo === 'anh' && xem.cam === cam) xem.timerAnh = setTimeout(thuLaiTrucTiep, 60000); };
            thu.src = urlXem('AnhChup', cam);
        }

        taiAnh();
    }

    function datNutCheDo(nut) {
        $('#xemCheDoVideo, #xemCheDoAnh, #xemCheDoAnhLuu').removeClass('active');
        $(nut).addClass('active');
    }

    // Chế độ "Ảnh lưu sẵn": ảnh do job CameraAnhLuuWorker chụp theo giờ định kỳ. Không gọi đầu ghi,
    // nên mở được cả khi máy chủ không kết nối được dải camera. 60 giây kiểm lại một lần xem có ảnh mới.
    function cheDoAnhLuu() {
        dungXem();
        xem.cheDo = 'anhluu';
        datNutCheDo('#xemCheDoAnhLuu');
        $('#xemVideo').hide();

        var anh = document.getElementById('xemAnh');
        var cam = xem.cam;
        $(anh).show();
        nhanXem('Đang tải ảnh lưu...');

        function tai() {
            if (xem.cheDo !== 'anhluu' || xem.cam !== cam) return;
            fetch(urlXem('AnhLuu', cam), { credentials: 'same-origin' })
                .then(function (res) {
                    if (res.status === 404) return null;
                    if (!res.ok) throw new Error('HTTP ' + res.status);
                    var luc = res.headers.get('X-Chup-Luc');
                    return res.blob().then(function (b) { return { blob: b, luc: luc }; });
                })
                .then(function (kq) {
                    if (xem.cheDo !== 'anhluu' || xem.cam !== cam) return;
                    if (!kq) {
                        anh.removeAttribute('src');
                        nhanXem('Chưa có ảnh lưu cho camera này');
                    } else {
                        thuHoiAnhLuu();
                        xem.urlAnhLuu = URL.createObjectURL(kq.blob);
                        anh.src = xem.urlAnhLuu;
                        nhanXem('Ảnh lưu lúc ' + dinhDangGioChup(kq.luc));
                    }
                    xem.timerAnh = setTimeout(tai, 60000);
                })
                .catch(function () {
                    if (xem.cheDo !== 'anhluu' || xem.cam !== cam) return;
                    nhanXem('Không đọc được ảnh lưu, đang thử lại...');
                    xem.timerAnh = setTimeout(tai, 10000);
                });
        }
        tai();
    }

    function thuHoiAnhLuu() {
        if (xem.urlAnhLuu) { URL.revokeObjectURL(xem.urlAnhLuu); xem.urlAnhLuu = null; }
    }

    // "2026-10-06T15:30:00" -> "15:30 06/10/2026"
    function dinhDangGioChup(luc) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(luc || '');
        return m ? m[4] + ':' + m[5] + ' ' + m[3] + '/' + m[2] + '/' + m[1] : '(không rõ giờ)';
    }

    function moXem(cam) {
        if (!cam) return;
        xem.cam = cam;
        $('#xemTen').text(cam.name);
        $('#xemThongTin').text('IP ' + (cam.ip || '?') + ' · Đầu ghi ' + cam.nvr_name + ' (' + cam.nvr_ip + ') · kênh '
            + cam.cam_id + ' · ' + (cam.status === 'UP' ? 'Hoạt động' : cam.status === 'DOWN' ? 'Mất kết nối' : (cam.status || '')));
        $('#xemTaiAnh').attr('href', urlXem('AnhChup', cam));
        var gc = dsGhiChu[khoaGhiChu(cam)];
        $('#xemGhiChu').val(gc ? gc.ghiChu : '').removeClass('cam-da-luu');
        xem.modal.show();
        // Mặc định ảnh 1 giây/lần: nhẹ cho đầu ghi và máy chủ (không mở RTSP, không chuyển mã);
        // cần video thì người dùng tự bấm nút "Video"
        cheDoAnh();
    }

    // ---------------- Xu hướng ----------------
    function veBieuDo(runs) {
        if (typeof Chart === 'undefined') return; // CDN Chart.js không tải được thì bỏ biểu đồ, phần khác vẫn chạy

        runs = runs.slice().sort(function (a, b) { return String(a.finished_at).localeCompare(String(b.finished_at)); });
        var nhan = runs.map(function (r) { return ngayGio(r.finished_at); });
        var up = runs.map(function (r) { return r.up_count; });
        var down = runs.map(function (r) { return r.down_count; });

        if (bieuDo) {
            bieuDo.data.labels = nhan;
            bieuDo.data.datasets[0].data = up;
            bieuDo.data.datasets[1].data = down;
            bieuDo.update('none');
            return;
        }

        bieuDo = new Chart(document.getElementById('gsBieuDo'), {
            type: 'line',
            data: {
                labels: nhan,
                datasets: [
                    { label: 'Hoạt động', data: up, borderColor: '#16a34a', backgroundColor: 'rgba(22,163,74,.08)', fill: true, pointRadius: 0, tension: .25, yAxisID: 'y' },
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
                    // Hai trục riêng: ~300 camera hoạt động và ~20 mất kết nối, chung một trục thì đường đỏ dẹt sát đáy
                    y: { position: 'left', title: { display: true, text: 'Hoạt động' } },
                    y1: { position: 'right', beginAtZero: true, grid: { drawOnChartArea: false }, title: { display: true, text: 'Mất kết nối' } }
                }
            }
        });
    }

    function taiXuHuong() {
        lay('/QLCamera/GiamSat/LichSu', { soLan: $('#gsKhoangXuHuong').val() })
            .done(function (d) { veBieuDo(d.runs || []); })
            .fail(hienLoi);
    }

    // ---------------- Tải tất cả ----------------
    function taiTatCa() {
        if (dangTai) return;
        dangTai = true;
        var $btn = $('#gsBtnTaiLai').prop('disabled', true);

        var pTongQuan = lay('/QLCamera/GiamSat/TongQuan');
        var pCamera = lay('/QLCamera/GiamSat/Camera', {
            gomDaLoaiTru: $('#gsGomLoaiTru').is(':checked'),
            chiCanChuY: $('#gsChiChuY').is(':checked')
        });
        var pDauGhi = lay('/QLCamera/GiamSat/DauGhi');

        $.when(pTongQuan, pCamera, pDauGhi, taiGhiChu())
            .done(function (tongQuan, camera, dauGhi) {
                hienLoi(null);
                veTongQuan(tongQuan);
                // Đang gõ ghi chú thì giữ nguyên bảng, lần làm mới sau sẽ vẽ lại
                if (dangSuaGhiChu) return;
                dsCamera = camera.cameras || [];
                veDauGhi(dauGhi.nvrs || []);
                veCamera();
            })
            .fail(function (loi) {
                hienLoi(loi);
                if (!dsCamera.length) {
                    $('#gsCameraBody').html('<tr><td colspan="10" class="ccdc-trangthai text-danger py-4">Không tải được dữ liệu giám sát.</td></tr>');
                }
            })
            .always(function () { dangTai = false; $btn.prop('disabled', false); });

        taiXuHuong();
    }

    function datTuLamMoi() {
        clearInterval(timerTuLamMoi);
        if (!$('#gsTuLamMoi').is(':checked')) return;
        timerTuLamMoi = setInterval(function () {
            // Không gọi khi tab trình duyệt bị ẩn hoặc người dùng đang ở tab khác của trang
            if (document.hidden || !$('#paneGiamSat').hasClass('active')) return;
            taiTatCa();
        }, 60000);
    }

    $(function () {
        $('#gsBtnTaiLai').on('click', taiTatCa);
        $('#gsChiChuY, #gsGomLoaiTru').on('change', taiTatCa);   // hai bộ lọc này lọc ở phía API
        $('#gsTrangThai, #gsKhuVuc').on('change', veCamera);
        $('#gsNvrLoc').on('change', function () {
            veCamera();
            var ip = $(this).val();
            $('#gsNvrBody tr').each(function () { $(this).toggleClass('cam-dang-loc', !!ip && $(this).data('ip') === ip); });
        });
        $('#gsTuKhoa').on('input', function () {
            clearTimeout(timerTimKiem);
            timerTimKiem = setTimeout(veCamera, 250);
        });
        $('#gsKhoangXuHuong').on('change', taiXuHuong);
        $('#gsTuLamMoi').on('change', datTuLamMoi);

        // Bấm dòng đầu ghi = lọc camera theo đầu ghi đó, bấm lại để bỏ lọc
        $('#gsNvrBody').on('click', 'tr[data-ip]', function () {
            var ip = String($(this).data('ip'));
            $('#gsNvrLoc').val($('#gsNvrLoc').val() === ip ? '' : ip).trigger('change');
        });

        xem.modal = new bootstrap.Modal(document.getElementById('modalXemCamera'));
        // Ô ghi chú: sửa tại chỗ, chặn không cho lan lên dòng (dòng bấm = mở xem trực tiếp)
        $('#gsCameraBody').on('click', 'button.cam-ghim', function (e) {
            e.stopPropagation();   // dòng bấm = mở xem trực tiếp, nút ghim thì không
            doiGhim($(this).closest('tr').data('cam'));
        });
        $('#gsChiGhim').on('change', veCamera);
        $('#gsCameraBody').on('click', 'td.cam-ghi-chu', function (e) {
            e.stopPropagation();
            batDauSuaGhiChu($(this));
        });
        $('#gsCameraBody').on('click', 'tr', function () { moXem($(this).data('cam')); });
        $('#xemCheDoVideo').on('click', cheDoVideo);
        $('#xemCheDoAnh').on('click', cheDoAnh);
        $('#xemCheDoAnhLuu').on('click', cheDoAnhLuu);
        $('#xemToanManHinh').on('click', function () {
            var khung = document.getElementById('xemKhung');
            if (khung.requestFullscreen) khung.requestFullscreen();
        });
        // Ghi chú trong cửa sổ xem + danh sách có sẵn
        $('#xemLuuGhiChu').on('click', luuGhiChuXem);
        $('#xemThemMau').on('click', themMau);
        $('#xemGhiChu').on('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); luuGhiChuXem(); }
        });
        $('#xemDsMau').on('click', 'a.dropdown-item', function (e) {
            e.preventDefault();
            var $a = $(this);
            var noiDung = $a.find('.cam-mau-chu').text();
            if ($(e.target).closest('.cam-mau-xoa').length) { xoaMau($a.data('id'), noiDung); return; }
            // Chọn câu có sẵn = điền vào ô và lưu luôn, không phải gõ hay bấm Lưu
            $('#xemGhiChu').val(noiDung);
            bootstrap.Dropdown.getOrCreateInstance($('#xemDsMau').prev('[data-bs-toggle="dropdown"]')[0]).hide();
            luuGhiChuXem();
        });
        taiMau();

        $('#xemThoatToanManHinh').on('click', function () {
            if (document.fullscreenElement && document.exitFullscreen) document.exitFullscreen();
        });
        $('#modalXemCamera').on('hidden.bs.modal', function () { dungXem(); xem.cheDo = null; });

        taiTatCa();
        datTuLamMoi();
    });
})();
