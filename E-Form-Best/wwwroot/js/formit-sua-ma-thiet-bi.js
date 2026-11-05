// Quyền "All" sửa Mã thiết bị / Định danh ngay trên bảng thiết bị của đơn Wi-Fi.
// Nút chỉ được render cho quyền "All"; server vẫn kiểm quyền lại lần nữa.
(function () {
    'use strict';

    function moSua(nut) {
        const o = nut.closest('.o-ma-thiet-bi');
        const giaTri = o.querySelector('.ma-thiet-bi-gia-tri');

        const khung = document.createElement('div');
        khung.className = 'khung-sua-ma-thiet-bi';

        const input = document.createElement('input');
        input.type = 'text';
        input.value = giaTri.textContent;
        input.setAttribute('aria-label', 'Mã thiết bị / Định danh');

        const btnLuu = document.createElement('button');
        btnLuu.type = 'button';
        btnLuu.className = 'btn-luu-ma-thiet-bi';
        btnLuu.textContent = 'Lưu';

        const btnHuy = document.createElement('button');
        btnHuy.type = 'button';
        btnHuy.className = 'btn-huy-ma-thiet-bi';
        btnHuy.textContent = 'Hủy';

        const loi = document.createElement('div');
        loi.className = 'loi-ma-thiet-bi';
        loi.hidden = true;

        khung.append(input, btnLuu, btnHuy, loi);
        o.append(khung);
        giaTri.hidden = true;
        nut.hidden = true;
        input.focus();
        input.select();
    }

    function dongSua(o) {
        o.querySelector('.khung-sua-ma-thiet-bi').remove();
        o.querySelector('.ma-thiet-bi-gia-tri').hidden = false;
        o.querySelector('.btn-sua-ma-thiet-bi').hidden = false;
    }

    function baoLoi(o, noiDung) {
        const loi = o.querySelector('.loi-ma-thiet-bi');
        loi.textContent = noiDung;
        loi.hidden = !noiDung;
    }

    // Chỉ thay nội dung, giữ nguyên phần tử vì danh sách đang gắn sự kiện cuộn tải thêm
    async function capNhatLichSu() {
        const res = await fetch(window.location.pathname, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) return;
        const trangMoi = new DOMParser().parseFromString(await res.text(), 'text/html');
        const cu = document.getElementById('historyScrollList');
        const moi = trangMoi.getElementById('historyScrollList');
        if (cu && moi) cu.innerHTML = moi.innerHTML;
    }

    async function luu(o) {
        const nut = o.querySelector('.btn-sua-ma-thiet-bi');
        const btnLuu = o.querySelector('.btn-luu-ma-thiet-bi');
        const input = o.querySelector('.khung-sua-ma-thiet-bi input');
        if (btnLuu.disabled) return;

        const maMoi = input.value.trim();
        if (!maMoi) {
            baoLoi(o, 'Nhập mã thiết bị / định danh.');
            return;
        }

        // Khoá nút chống double-submit
        btnLuu.disabled = true;
        btnLuu.textContent = 'Đang lưu...';
        baoLoi(o, '');

        let data;
        try {
            const res = await fetch('/FormIT/SuaMaThietBiWifi', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({
                    idFormIt: parseInt(nut.dataset.idForm, 10),
                    idChiTiet: parseInt(nut.dataset.idChiTiet, 10),
                    viTri: parseInt(nut.dataset.viTri, 10),
                    maCu: o.querySelector('.ma-thiet-bi-gia-tri').textContent,
                    maMoi: maMoi
                })
            });

            if (!res.ok) {
                baoLoi(o, 'Máy chủ trả về lỗi ' + res.status + '.');
                return;
            }

            data = await res.json();
            if (!data.thanhCong) {
                baoLoi(o, data.thongBao || 'Không lưu được.');
                return;
            }
        } catch (err) {
            baoLoi(o, 'Lỗi kết nối máy chủ.');
            return;
        } finally {
            btnLuu.disabled = false;
            btnLuu.textContent = 'Lưu';
        }

        o.querySelector('.ma-thiet-bi-gia-tri').textContent = data.duLieu.maThietBi;
        dongSua(o);

        // Nút "+" WLC cùng dòng lấy mô tả từ data-mo-ta, phải đổi theo để không đẩy tên cũ lên WLC
        const nutWlc = o.closest('tr').querySelector('.btn-them-mac-wlc');
        if (nutWlc) nutWlc.dataset.moTa = data.duLieu.moTa;
        document.dispatchEvent(new CustomEvent('ma-thiet-bi-da-sua'));

        // Đã lưu thành công: lỗi lúc lấy lại lịch sử không được báo là lưu thất bại
        try {
            await capNhatLichSu();
        } catch (err) { /* lịch sử sẽ đúng khi mở lại trang */ }
    }

    document.addEventListener('click', function (e) {
        const nut = e.target.closest('.btn-sua-ma-thiet-bi, .btn-luu-ma-thiet-bi, .btn-huy-ma-thiet-bi');
        if (!nut) return;

        const o = nut.closest('.o-ma-thiet-bi');
        if (nut.classList.contains('btn-sua-ma-thiet-bi')) moSua(nut);
        else if (nut.classList.contains('btn-luu-ma-thiet-bi')) luu(o);
        else dongSua(o);
    });

    document.addEventListener('keydown', function (e) {
        if (!e.target.matches('.khung-sua-ma-thiet-bi input')) return;
        const o = e.target.closest('.o-ma-thiet-bi');
        if (e.key === 'Enter') {
            e.preventDefault();
            luu(o);
        } else if (e.key === 'Escape') {
            dongSua(o);
        }
    });
})();
