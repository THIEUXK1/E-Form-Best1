// Lịch công việc: hiển thị mỗi đơn thành một thanh chạy từ ngày tạo tới hạn hoàn thành,
// tô màu theo mức độ gấp của hạn chót + panel bên phải liệt kê việc sắp/đã quá hạn.
(function () {
    'use strict';

    var calendar = null;
    var boLocHienTai = 'tatca';

    // Bảng màu dùng chung cho chip lọc / badge panel — khớp với màu server trả về
    var MAU_NHOM = {
        quahan: '#dc2626',
        sapden: '#f59e0b',
        condu: '#0891b2',
        khonghan: '#8b5cf6',
        hoantat: '#16a34a',
        huy: '#94a3b8'
    };

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : text;
        return div.innerHTML;
    }

    function mauTheoSoNgay(soNgay) {
        if (soNgay < 0) return MAU_NHOM.quahan;
        if (soNgay === 0) return '#ea580c';
        if (soNgay <= 3) return MAU_NHOM.sapden;
        if (soNgay <= 7) return '#0ea5e9';
        return MAU_NHOM.condu;
    }

    // ---- Bộ lọc theo tình trạng hạn -------------------------------------
    // Lọc bằng class CSS trên khung lịch, KHÔNG dùng ev.setProp: setProp làm FullCalendar
    // vẽ lại toàn bộ và bắn lại eventsSet -> lặp vô tận, gây đơ trang.
    function apDungBoLoc() {
        var wrapper = document.getElementById('calendarWrapper');
        if (!wrapper) return;
        wrapper.className = wrapper.className.replace(/\blcv-loc-\S+/g, '').trim();
        if (boLocHienTai !== 'tatca') wrapper.classList.add('lcv-loc-' + boLocHienTai);
    }

    // ---- Nháy đỏ việc đang bị cảnh báo -----------------------------------
    // Danh sách cảnh báo (đã lọc theo quyền của từng tài khoản) do canh-bao-sap-qua-han.js
    // ở layout giữ. Đánh dấu bằng class trên DOM thay vì sửa event của FullCalendar —
    // sửa event sẽ làm lịch vẽ lại và bắn lại eventsSet.
    function apDungNhayDo() {
        if (window.CvSapQuaHan && window.CvSapQuaHan.apDung) window.CvSapQuaHan.apDung();
    }

    function laViecCanhBao(id) {
        return !!(window.CvSapQuaHan && window.CvSapQuaHan.coTrongCanhBao
            && window.CvSapQuaHan.coTrongCanhBao(id));
    }

    function capNhatSoLuongChip(events) {
        var dem = { tatca: events.length };
        events.forEach(function (ev) {
            var nhom = ev.extendedProps.nhomLoc || 'condu';
            dem[nhom] = (dem[nhom] || 0) + 1;
        });
        document.querySelectorAll('.lcv-chip').forEach(function (chip) {
            var span = chip.querySelector('.lcv-count');
            if (span) span.textContent = dem[chip.dataset.loc] || 0;
        });
    }

    // ---- Panel hạn chót --------------------------------------------------
    function veDanhSachHanChot(danhSach) {
        var box = document.getElementById('lcvDeadlineList');
        if (!box) return;
        box.innerHTML = '';

        if (!danhSach.length) {
            box.innerHTML = '<div class="lcv-empty"><i class="fa fa-check-circle" style="font-size:22px;color:#16a34a"></i><br>Không có việc nào sắp đến hạn</div>';
            return;
        }

        danhSach.forEach(function (item) {
            var mau = mauTheoSoNgay(item.soNgayConLai);
            var a = document.createElement('a');
            a.className = 'lcv-dl-item';
            a.href = item.url;
            a.setAttribute('data-lcv-id', item.id);
            a.style.borderLeftColor = mau;
            a.innerHTML =
                '<div class="lcv-dl-ten">' + escapeHtml(item.ten) + '</div>' +
                '<div class="lcv-dl-meta">' +
                '<span class="lcv-badge" style="background:' + mau + '">' + escapeHtml(item.nhanHan) + '</span>' +
                '<span><i class="fa fa-flag-checkered"></i> ' + escapeHtml(item.hanChotText) + '</span>' +
                (item.nguoiTao ? '<span><i class="fa fa-user"></i> ' + escapeHtml(item.nguoiTao) + '</span>' : '') +
                '</div>';
            box.appendChild(a);
        });

        apDungNhayDo();
    }

    function taiDanhSachHanChot() {
        fetch('/FormCongViec/GetCongViecSapDenHan', { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('Lỗi tải danh sách hạn chót');
                return res.json();
            })
            .then(veDanhSachHanChot)
            .catch(function () {
                var box = document.getElementById('lcvDeadlineList');
                if (box) box.innerHTML = '<div class="lcv-empty">Không tải được danh sách hạn chót</div>';
            });
    }

    // ---- Toàn màn hình ---------------------------------------------------
    function batTatToanManHinh() {
        var wrapper = document.getElementById('calendarWrapper');
        if (!document.fullscreenElement) {
            if (wrapper.requestFullscreen) wrapper.requestFullscreen();
        } else if (document.exitFullscreen) {
            document.exitFullscreen();
        }
    }

    document.addEventListener('fullscreenchange', function () {
        var full = !!document.fullscreenElement;
        var icon = document.getElementById('iconFullscreenCalendar');
        var text = document.getElementById('textFullscreenCalendar');
        if (icon) icon.className = full ? 'fa fa-compress' : 'fa fa-expand';
        if (text) text.textContent = full ? 'Thoát toàn màn hình' : 'Toàn màn hình';
        if (calendar) {
            calendar.setOption('height', full ? 'parent' : 'auto');
            setTimeout(function () { calendar.updateSize(); }, 100);
        }
    });

    // ---- Khởi tạo --------------------------------------------------------
    document.addEventListener('DOMContentLoaded', function () {
        var el = document.getElementById('calendar');
        if (!el || typeof FullCalendar === 'undefined') return;

        calendar = new FullCalendar.Calendar(el, {
            initialView: 'dayGridMonth',
            locale: 'vi',
            height: 'auto',
            firstDay: 1,
            dayMaxEvents: 4,
            moreLinkText: function (n) { return '+ ' + n + ' việc nữa'; },
            headerToolbar: {
                left: 'prev,next today',
                center: 'title',
                right: 'dayGridMonth,timeGridWeek,listMonth'
            },
            buttonText: { today: 'Hôm nay', month: 'Tháng', week: 'Tuần', list: 'Danh sách' },
            events: '/FormCongViec/GetLichCongViecData',
            // Chỉ đếm lại số lượng — tuyệt đối không sửa event trong callback này
            eventsSet: capNhatSoLuongChip,
            eventClassNames: function (arg) {
                return ['lcv-' + (arg.event.extendedProps.nhomLoc || 'condu')];
            },
            eventClick: function (info) {
                if (info.event.url) {
                    window.location.href = info.event.url;
                    return false;
                }
            },
            eventDidMount: function (arg) {
                // Đánh dấu id để bật/tắt nháy đỏ được mà không phải vẽ lại lịch
                arg.el.setAttribute('data-lcv-id', arg.event.id);
                if (laViecCanhBao(arg.event.id)) arg.el.classList.add('lcv-nhay-do');

                // Tooltip gốc của trình duyệt: đủ dùng, không phải thêm thư viện ngoài
                var p = arg.event.extendedProps;
                arg.el.title =
                    arg.event.title +
                    '\nHạn hoàn thành: ' + (p.hanChotText || 'chưa đặt') +
                    '\nTình trạng hạn: ' + (p.nhanHan || '') +
                    '\nTrạng thái đơn: ' + (p.trangThaiText || '') +
                    (p.mucDoUuTien ? '\nƯu tiên: ' + p.mucDoUuTien : '') +
                    (p.nguoiTao ? '\nNgười tạo: ' + p.nguoiTao : '') +
                    (p.ngayTaoText ? '\nNgày tạo: ' + p.ngayTaoText : '');
            },
            eventContent: function (arg) {
                var p = arg.event.extendedProps;
                var icon = p.nhomLoc === 'quahan' ? 'fa-exclamation-triangle'
                    : p.nhomLoc === 'hoantat' ? 'fa-check'
                        : p.nhomLoc === 'khonghan' ? 'fa-question-circle'
                            : 'fa-clock-o';
                var html =
                    '<div class="lcv-ev">' +
                    '<div class="lcv-ev-title">' + escapeHtml(arg.event.title) + '</div>' +
                    '<div class="lcv-ev-han"><i class="fa ' + icon + '"></i>' +
                    escapeHtml(p.nhanHan || '') +
                    (p.hanChotText ? ' · ' + escapeHtml(p.hanChotText) : '') +
                    '</div></div>';
                return { html: html };
            }
        });

        calendar.render();
        taiDanhSachHanChot();

        var btnFull = document.getElementById('btnFullscreenCalendar');
        if (btnFull) btnFull.addEventListener('click', batTatToanManHinh);

        // Chip lọc — event delegation để vẫn chạy nếu sau này chip được vẽ động
        var boxChip = document.getElementById('lcvFilters');
        if (boxChip) {
            boxChip.addEventListener('click', function (e) {
                var chip = e.target.closest('.lcv-chip');
                if (!chip) return;
                boxChip.querySelectorAll('.lcv-chip').forEach(function (c) { c.classList.remove('active'); });
                chip.classList.add('active');
                boLocHienTai = chip.dataset.loc;
                apDungBoLoc();
            });
        }
    });
})();
