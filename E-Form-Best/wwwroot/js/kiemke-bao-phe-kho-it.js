// Trang Quản lý Thiết bị: nút "Báo phế" (→ trạng thái Hỏng, dòng tô đỏ) và "Kho IT" (→ trạng thái Kho IT, dòng tô xanh),
// cùng 2 chế độ xem từ menu: /QLKiemKe/ThietBi?xem=kho-it và ?xem=bao-phe.
window.BaoPheKhoITKiemKe = (function () {
    const CHE_DO = new URLSearchParams(location.search).get('xem') || '';

    // allThietBiCache khai báo bằng let trong view nên không nằm trên window, phải gọi tên trần
    function dsCache() { return typeof allThietBiCache !== "undefined" ? allThietBiCache : []; }

    function laHong(item) { return (item.tenTrangThai || '').toLowerCase().includes('hỏng'); }
    function laKhoIT(item) { return (item.tenTrangThai || '').trim().toLowerCase() === 'kho it'; }

    // Lọc theo mục menu đang mở; thiết bị đã báo phế chỉ nằm ở ?xem=bao-phe, rời khỏi danh sách chính
    function loc(ds) {
        if (CHE_DO === 'kho-it') return ds.filter(laKhoIT);
        if (CHE_DO === 'bao-phe') return ds.filter(laHong);
        return ds.filter(x => !laHong(x));
    }

    function lopDong(item) {
        if (laHong(item)) return 'tb-dong-bao-phe';
        if (laKhoIT(item)) return 'tb-dong-kho-it';
        return '';
    }

    // Đã ở trạng thái đó rồi thì không hiện nút tương ứng nữa
    function nut(item) {
        let h = '';
        if (!laHong(item)) h += `<button type="button" class="btn btn-sm btn-outline-danger px-2" data-doi-tt="baoPhe" data-id="${item.idThietBi}" title="Báo phế (chuyển trạng thái Hỏng)"><i class="fas fa-recycle"></i></button>`;
        if (!laKhoIT(item)) h += `<button type="button" class="btn btn-sm btn-outline-success px-2" data-doi-tt="khoIT" data-id="${item.idThietBi}" title="Xác nhận thiết bị đang ở Kho IT"><i class="fas fa-warehouse"></i></button>`;
        return h;
    }

    function tieuDeCheDo() {
        if (CHE_DO === 'kho-it') return '<span class="badge bg-success px-3 py-2" style="font-size:13px;border-radius:8px"><i class="fas fa-warehouse me-1"></i> Đang xem: Kho IT</span>';
        if (CHE_DO === 'bao-phe') return '<span class="badge bg-danger px-3 py-2" style="font-size:13px;border-radius:8px"><i class="fas fa-recycle me-1"></i> Đang xem: Tài sản báo phế</span>';
        return '';
    }

    $(document).on('click', '[data-doi-tt]', function () {
        const $nut = $(this);
        const id = Number($nut.data('id'));
        const loai = $nut.data('doi-tt');
        const item = dsCache().find(x => x.idThietBi === id) || {};
        const ten = item.tenMayTinh || item.seribacode || ('#' + id);
        const laBaoPhe = loai === 'baoPhe';

        Swal.fire({
            title: laBaoPhe ? 'Báo phế thiết bị?' : 'Xác nhận ở Kho IT?',
            html: laBaoPhe
                ? `Thiết bị <b></b> sẽ chuyển sang trạng thái <b class="text-danger">Hỏng</b>.`
                : `Thiết bị <b></b> sẽ chuyển sang trạng thái <b class="text-success">Kho IT</b>.`,
            icon: laBaoPhe ? 'warning' : 'question',
            showCancelButton: true,
            confirmButtonText: laBaoPhe ? 'Báo phế' : 'Xác nhận',
            cancelButtonText: 'Huỷ',
            confirmButtonColor: laBaoPhe ? '#dc3545' : '#198754',
            didOpen: popup => { popup.querySelector('.swal2-html-container b').textContent = ten; }
        }).then(kq => {
            if (!kq.isConfirmed) return;
            $nut.prop('disabled', true).find('i').attr('class', 'fas fa-spinner fa-spin');
            $.post('/QLKiemKe/DoiTrangThaiNhanhThietBi', { id: id, loai: loai })
                .done(res => {
                    if (!res || !res.success) {
                        Swal.fire('Không thực hiện được', (res && res.message) || 'Lỗi không rõ', 'error');
                        if (typeof renderUIThietBi === 'function') renderUIThietBi();
                        return;
                    }
                    // Cập nhật cache cục bộ rồi vẽ lại bảng + Tổng quan, không tải lại trang
                    const tb = dsCache().find(x => x.idThietBi === id);
                    if (tb) { tb.idTrangThai = res.idTrangThai; tb.tenTrangThai = res.tenTrangThai; tb.ngayCapNhat = new Date().toISOString(); }
                    const trangCu = typeof trangHienTaiThietBi !== "undefined" ? trangHienTaiThietBi : 1;
                    if (typeof renderThongKe === 'function') renderThongKe();
                    if (typeof renderUIThietBi === 'function') renderUIThietBi();
                    // renderUIThietBi đưa về trang 1 — giữ người dùng ở trang đang xem
                    if (trangCu && typeof veBangThietBi === 'function') { trangHienTaiThietBi = trangCu; veBangThietBi(); }
                    Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: res.message, showConfirmButton: false, timer: 2000 });
                })
                .fail(() => {
                    Swal.fire('Lỗi kết nối', 'Không gửi được yêu cầu, vui lòng thử lại.', 'error');
                    if (typeof renderUIThietBi === 'function') renderUIThietBi();
                });
        });
    });

    $(function () {
        const td = tieuDeCheDo();
        if (td) $('#lblSoLuongThietBi').before(td);
    });

    return { cheDo: CHE_DO, loc: loc, lopDong: lopDong, nut: nut };
})();
