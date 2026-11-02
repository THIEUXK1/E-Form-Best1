// Tải bộ cài Tool local trên trang /QLKiemKe/ViewCheckMayHienTai: nhập TK AdminIT/All, server trả file .msi.
// Dùng fetch + blob thay vì form POST để sai mật khẩu thì báo ngay trong hộp, không rời trang.
(function () {
    function hienLoi(thongBao) {
        var o = document.getElementById('taiCongCuLoi');
        o.textContent = thongBao;
        o.style.display = thongBao ? '' : 'none';
    }

    // Lấy tên file từ Content-Disposition (ưu tiên filename*=UTF-8''...)
    function layTenFile(res) {
        var cd = res.headers.get('Content-Disposition') || '';
        var m = /filename\*=UTF-8''([^;]+)/i.exec(cd) || /filename="?([^";]+)"?/i.exec(cd);
        return m ? decodeURIComponent(m[1]) : 'HardwareHelperSetup.msi';
    }

    document.addEventListener('show.bs.modal', function (e) {
        if (e.target.id !== 'modalTaiCongCu') return;
        hienLoi('');
        document.getElementById('txtTaiCongCuMk').value = '';
    });

    document.addEventListener('submit', async function (e) {
        if (e.target.id !== 'formTaiCongCu') return;
        e.preventDefault();

        var nut = document.getElementById('btnTaiCongCu');
        var nhanCu = nut.innerHTML;
        nut.disabled = true;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang kiểm tra...';
        hienLoi('');

        try {
            var duLieu = new FormData();
            duLieu.append('taikhoan', document.getElementById('txtTaiCongCuTk').value.trim());
            duLieu.append('matkhau', document.getElementById('txtTaiCongCuMk').value);

            var res = await fetch('/QLKiemKe/TaiCongCu', { method: 'POST', body: duLieu });
            if (res.status === 429) { hienLoi('Thử quá nhiều lần, vui lòng đợi một lúc rồi thử lại.'); return; }

            var loai = res.headers.get('Content-Type') || '';
            if (loai.indexOf('application/json') >= 0) {
                var kq = await res.json();
                hienLoi(kq.thongBao || kq.message || 'Không tải được Tool.');
                return;
            }
            if (!res.ok) { hienLoi('Không tải được Tool (mã lỗi ' + res.status + ').'); return; }

            var blob = await res.blob();
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = layTenFile(res);
            document.body.appendChild(a);
            a.click();
            a.remove();
            setTimeout(function () { URL.revokeObjectURL(url); }, 10000);

            document.getElementById('txtTaiCongCuMk').value = '';
            bootstrap.Modal.getOrCreateInstance(document.getElementById('modalTaiCongCu')).hide();
        } catch (err) {
            hienLoi('Lỗi kết nối máy chủ, vui lòng thử lại.');
        } finally {
            nut.disabled = false;
            nut.innerHTML = nhanCu;
        }
    });
})();
