// IT trả đơn "Hệ thống E-form" tự duyệt về cho quản lý bộ phận xác nhận.
// Khung này chỉ được render cho quyền "All" / "AdminIT"; server vẫn kiểm quyền lại lần nữa.
(function () {
    'use strict';

    // Trạng thái sau khi chuyển kéo theo nhiều vùng (quyền "All" còn hiện thêm nút Duyệt),
    // nên lấy lại đúng các vùng đó từ server thay vì dựng lại logic hiển thị ở JS
    const CAC_VUNG = ['trangThaiDonIT', 'khungTienTrinhDon', 'khungNutXuLyDon', 'historyScrollList'];

    async function capNhatCacVung() {
        const res = await fetch(window.location.pathname, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);

        const trangMoi = new DOMParser().parseFromString(await res.text(), 'text/html');
        CAC_VUNG.forEach(function (id) {
            const cu = document.getElementById(id);
            const moi = trangMoi.getElementById(id);
            if (cu && moi) cu.replaceWith(moi);
        });
    }

    function hienOTrong(hien) {
        document.getElementById('oNhapYeuCauQuanLy').hidden = !hien;
        document.getElementById('btnYeuCauQuanLy').hidden = hien;
        if (hien) document.getElementById('txtLyDoYeuCauQuanLy').focus();
    }

    function baoLoi(noiDung) {
        const o = document.getElementById('baoLoiYeuCauQuanLy');
        o.textContent = noiDung;
        o.hidden = !noiDung;
    }

    async function gui(btnGui) {
        const khung = document.getElementById('khungYeuCauQuanLy');
        const lyDo = document.getElementById('txtLyDoYeuCauQuanLy').value.trim();
        if (!lyDo) {
            baoLoi('Vui lòng nhập lý do cần quản lý xác nhận.');
            return;
        }

        // Khoá nút chống double-submit
        const nhanCu = btnGui.innerHTML;
        btnGui.disabled = true;
        btnGui.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang gửi...';
        baoLoi('');

        try {
            const res = await fetch('/FormIT/YeuCauQuanLyXacNhan', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({ idFormIt: parseInt(khung.dataset.idDon, 10), lyDo: lyDo })
            });

            if (!res.ok) {
                baoLoi(res.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.' : 'Máy chủ trả về lỗi ' + res.status + '.');
                return;
            }

            const data = await res.json();
            if (!data.thanhCong) {
                baoLoi(data.thongBao || 'Không gửi được.');
                return;
            }
        } catch (err) {
            baoLoi('Lỗi kết nối máy chủ.');
            return;
        } finally {
            btnGui.disabled = false;
            btnGui.innerHTML = nhanCu;
        }

        // Đơn đã chuyển thành công: lỗi lúc lấy lại giao diện không được báo là gửi thất bại
        try {
            await capNhatCacVung();
        } catch (err) {
            khung.textContent = 'Đã chuyển đơn cho quản lý. Mở lại trang để xem trạng thái mới.';
        }
    }

    // Delegation: khung nút được thay mới sau khi cập nhật vùng
    document.addEventListener('click', function (e) {
        const nut = e.target.closest('#btnYeuCauQuanLy, #btnBoYeuCauQuanLy, #btnGuiYeuCauQuanLy');
        if (!nut) return;

        if (nut.id === 'btnYeuCauQuanLy') hienOTrong(true);
        else if (nut.id === 'btnBoYeuCauQuanLy') { baoLoi(''); hienOTrong(false); }
        else gui(nut);
    });
})();
