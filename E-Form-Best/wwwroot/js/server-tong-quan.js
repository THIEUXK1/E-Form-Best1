// Tổng quan Server và lưu trữ — /QLServer/TongQuan: thẻ số liệu theo công ty + bảng thiết bị cần chú ý.
// Số liệu hiện tại (không theo kỳ), lấy qua AJAX từ QLServerController.TongQuanDuLieu.
(function () {
    'use strict';

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function soTb(v) { return (Math.round((v || 0) * 100) / 100).toLocaleString('vi-VN'); }

    function chiSo(nhan, so, lop) {
        return '<div><div class="nhan">' + esc(nhan) + '</div><div class="so ' + (lop || '') + '">' + esc(so) + '</div></div>';
    }

    function veCongTy(ds, moDuoc, nguongDay) {
        var $khung = $('#tqCongTy');
        if (!ds.length) {
            $khung.html('<div class="col-12 text-muted small py-4 text-center">Không có công ty nào được xem.</div>');
            return;
        }
        $khung.html(ds.map(function (c) {
            var ten = moDuoc.indexOf(c.congTy) >= 0
                ? '<a class="sv-the-cty-ten" href="/QLServer/' + encodeURIComponent(c.congTy) + '">' + esc(c.congTy) + ' <i class="fas fa-arrow-right small"></i></a>'
                : '<span class="sv-the-cty-ten">' + esc(c.congTy) + '</span>';

            var tyLe = c.dungLuongTb > 0 ? c.daDungTb / c.dungLuongTb : null;
            var phanTram = tyLe === null ? null : Math.round(tyLe * 100);
            var mau = tyLe === null ? '' : tyLe >= nguongDay ? 'sv-thanh-day' : tyLe >= nguongDay - 0.15 ? 'sv-thanh-vua' : '';
            var luuTru = c.dungLuongTb > 0
                ? '<div class="small fw-bold mt-2 d-flex justify-content-between"><span><i class="fas fa-hard-drive me-1 text-warning"></i>Lưu trữ NAS/SAN</span>'
                    + '<span>' + soTb(c.daDungTb) + ' / ' + soTb(c.dungLuongTb) + ' TB · ' + phanTram + '%</span></div>'
                    + '<div class="sv-thanh"><div class="sv-thanh-dung ' + mau + '" style="width:' + Math.min(100, phanTram) + '%;"></div></div>'
                : '<div class="small text-muted mt-2">Chưa khai dung lượng NAS/SAN.</div>';

            return '<div class="col-12 col-md-6 col-xl-4"><div class="sv-the-cty">'
                + '<div class="sv-the-cty-dau">' + ten + '<span class="sv-the-cty-tong" title="Tổng thiết bị">' + c.tong + '</span></div>'
                + '<div class="sv-chi-so">'
                + chiSo('Online', c.online, 'text-success')
                + chiSo('Mất kết nối', c.offline, c.offline ? 'text-danger' : '')
                + chiSo('Không theo dõi', c.khongTheoDoi, 'text-muted')
                + chiSo('Mất KN / 7 ngày', c.matKetNoi7Ngay, c.matKetNoi7Ngay ? 'text-danger' : '')
                + chiSo('Phần cứng lỗi', c.sucKhoeLoi, c.sucKhoeLoi ? 'text-danger' : '')
                + chiSo('Phần cứng cảnh báo', c.sucKhoeCanhBao, c.sucKhoeCanhBao ? 'text-warning' : '')
                + '</div>'
                + c.theoLoai.map(function (l) {
                    return '<div class="sv-hang"><span>' + esc(l.loai) + '</span><span class="fw-bold">' + l.so + '</span></div>';
                }).join('')
                + '<div class="sv-hang"><span>Hết / sắp hết bảo hành</span><span class="fw-bold">'
                + '<span class="' + (c.hetBaoHanh ? 'text-danger' : '') + '">' + c.hetBaoHanh + '</span> / '
                + '<span class="' + (c.sapHetBaoHanh ? 'text-warning' : '') + '">' + c.sapHetBaoHanh + '</span></span></div>'
                + luuTru
                + '</div></div>';
        }).join(''));
    }

    function veChuY(ds) {
        var $body = $('#tqChuY');
        if (!ds.length) {
            $body.html('<tr><td colspan="6" class="ccdc-trangthai text-success py-4">Không có thiết bị nào cần chú ý.</td></tr>');
            return;
        }
        $body.html(ds.map(function (x, i) {
            return '<tr>'
                + '<td class="text-muted small">' + (i + 1) + '</td>'
                + '<td class="small fw-bold">' + esc(x.congTy) + '</td>'
                + '<td class="small">' + esc(x.loai) + '</td>'
                + '<td class="ccdc-ten" title="' + esc(x.tenServer) + '">' + esc(x.tenServer) + '</td>'
                + '<td class="small font-monospace">' + esc(x.diaChiIp) + '</td>'
                + '<td>' + x.lyDo.map(function (l) {
                    var nang = l === 'Mất kết nối' || l === 'Hết bảo hành' || /^Dung lượng/.test(l) || /^Lỗi:/.test(l);
                    return '<span class="sv-ly-do ' + (nang ? 'sv-ly-do-do' : '') + '">' + esc(l) + '</span>';
                }).join('') + '</td>'
                + '</tr>';
        }).join(''));
    }

    function tai() {
        var $btn = $('#tqTaiLai').prop('disabled', true);
        $('#tqLoi').addClass('d-none').text('');
        $.getJSON('/QLServer/TongQuan/DuLieu')
            .done(function (res) {
                if (!res.thanhCong) {
                    $('#tqLoi').removeClass('d-none').text(res.thongBao || 'Không tải được số liệu.');
                    return;
                }
                veCongTy(res.theoCongTy, res.moDuoc || [], res.nguongDay || 0.85);
                veChuY(res.canChuY || []);
            })
            .fail(function () { $('#tqLoi').removeClass('d-none').text('Lỗi kết nối máy chủ.'); })
            .always(function () { $btn.prop('disabled', false); });
    }

    $(function () {
        $('#tqTaiLai').on('click', tai);
        tai();
        setInterval(function () { if (!document.hidden) tai(); }, 120000);
    });
})();
