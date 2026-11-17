// Trang QA Thùng: hộp tải driver máy in tem TSC (file nằm trên file server, máy chủ đọc rồi trả về)
(function () {
    'use strict';

    function veDong(may, f) {
        // Tải file = điều hướng thật (được phép)
        var a = document.createElement('a');
        a.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';
        a.href = '/QAForm/DriverTsc/Tai?may=' + encodeURIComponent(may) + '&file=' + encodeURIComponent(f.ten);
        var ten = document.createElement('span');
        var i = document.createElement('i');
        i.className = 'fa fa-download me-2';
        ten.appendChild(i);
        ten.appendChild(document.createTextNode(f.ten));
        var nho = document.createElement('small');
        nho.className = 'text-muted ms-2 text-nowrap';
        nho.textContent = f.kichThuocMb + ' MB · ' + new Date(f.ngayCapNhat).toLocaleDateString('vi-VN');
        a.appendChild(ten);
        a.appendChild(nho);
        return a;
    }

    async function moDriver(nut) {
        var khung = document.getElementById('qa-driver-ds');
        var ghiChu = document.getElementById('qa-driver-ghichu');
        khung.replaceChildren();
        ghiChu.textContent = '';
        khung.textContent = 'Đang tải…';
        bootstrap.Modal.getOrCreateInstance(document.getElementById('qa-driver-modal')).show();
        nut.disabled = true;
        try {
            var res = await fetch('/QAForm/DriverTsc/DanhSach');
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) throw new Error(kq.thongBao);
            khung.replaceChildren();
            if (!kq.duLieu.length) {
                khung.textContent = 'Chưa có driver TSC nào trên file server, liên hệ IT.';
                return;
            }
            kq.duLieu.forEach(function (d) {
                var tieuDe = document.createElement('div');
                tieuDe.className = 'fw-semibold small mt-2 mb-1';
                tieuDe.textContent = d.may;
                var ds = document.createElement('div');
                ds.className = 'list-group';
                d.files.forEach(function (f) { ds.appendChild(veDong(d.may, f)); });
                khung.appendChild(tieuDe);
                khung.appendChild(ds);
            });
        } catch (ex) {
            khung.replaceChildren();
            ghiChu.textContent = ex.message;
        } finally {
            nut.disabled = false;
        }
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('#qa-driver-mo');
        if (nut) moDriver(nut);
    });
})();
