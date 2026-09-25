// Quy tắc trùng theo loại cho dòng "Tài sản khác" trên trang /QLKiemKe/ViewCheckMayHienTai.
// Quy tắc do server đọc từ appsettings.json (KiemKe:TrungTaiSanKhac) đổ vào #cfgQuyTacTrungTaiSanKhac.
//   Serial    : trùng Serial + Loại  → bắt buộc Serial
//   Ip        : trùng IP + Loại      → bắt buộc IP, Serial không bắt buộc (máy in)
//   KhongKiem : luôn thêm mới        → bắt buộc Serial, không hỏi trùng (PDA)
(function () {
    'use strict';

    var cauHinh = { macDinh: 'Serial', theoLoai: {} };
    try {
        var el = document.getElementById('cfgQuyTacTrungTaiSanKhac');
        if (el && el.dataset.quyTac) cauHinh = JSON.parse(el.dataset.quyTac);
    } catch (e) {
        console.error('Không đọc được quy tắc trùng Tài sản khác', e);
    }

    function layQuyTac(loai) {
        var ten = (loai || '').trim().toLowerCase();
        var khoa = Object.keys(cauHinh.theoLoai || {}).find(function (k) { return k.trim().toLowerCase() === ten; });
        return khoa ? cauHinh.theoLoai[khoa] : (cauHinh.macDinh || 'Serial');
    }

    window.QuyTacTrungTaiSanKhac = { layQuyTac: layQuyTac };

    // Đổi loại ở một dòng → hiện/ẩn ô IP và đánh dấu trường bắt buộc đúng theo quy tắc của loại đó
    $(document).on('change', '#dsRowTaiSanKhac .select-loai-tskhac', function () {
        var $dong = $(this).closest('.dong-tai-san-khac');
        var theoIp = layQuyTac($(this).val()) === 'Ip';
        $dong.find('.input-ip-tskhac').toggleClass('d-none', !theoIp);
        $dong.find('.input-serial-tskhac').attr('placeholder', theoIp ? 'Serial (không bắt buộc)' : 'Nhập Serial...');
    });

    function dongMoTaTrung(t) {
        var dong = document.createElement('div');
        dong.className = 'border rounded p-2 mb-2 text-start';

        var tieuDe = document.createElement('div');
        tieuDe.className = 'fw-bold';
        tieuDe.textContent = 'Dòng ' + (t.viTriDong + 1) + ': ' + (t.loaiThietBi || '') + ' - ' + t.khoa + '  →  đã có ở #' + t.idThietBi;
        dong.appendChild(tieuDe);

        var chiTiet = document.createElement('div');
        chiTiet.className = 'small text-muted';
        chiTiet.textContent = 'Đang đứng tên: ' + (t.tenNguoiDung || 'chưa có') + (t.tk ? ' (' + t.tk + ')' : '')
            + ' | Bộ phận: ' + (t.tenBoPhan || '-')
            + ' | Vị trí: ' + (t.tenViTri || '-')
            + (t.quyCach ? ' | Quy cách: ' + t.quyCach : '')
            + (t.thoiGianCheck ? ' | Kiểm kê gần nhất: ' + new Date(t.thoiGianCheck).toLocaleDateString('vi-VN') : '');
        dong.appendChild(chiTiet);

        if (t.duongDanAnh) {
            var anh = document.createElement('img');
            anh.src = '/QLKiemKe/GetEvidenceImage?fileName=' + encodeURIComponent(t.duongDanAnh);
            anh.alt = 'Ảnh thiết bị #' + t.idThietBi;
            anh.style.cssText = 'max-height:70px; max-width:120px; border-radius:6px; margin-top:4px;';
            anh.onerror = function () { this.style.display = 'none'; };
            dong.appendChild(anh);
        }
        return dong;
    }

    // Gọi trước khi gửi form Xác nhận tài sản. Trả Promise<{ tiepTuc, thongBao }>; khi tiếp tục thì các dòng được xác nhận
    // ghi đè có thêm rows[i].idGhiDe để server biết người nhập đã đồng ý ghi đè đúng thiết bị đó.
    window.kiemTraTrungTaiSanKhac = function (rows, maNhanVien) {
        if (!rows || rows.length === 0) return Promise.resolve({ tiepTuc: true });

        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        var fd = new FormData();
        fd.append('maNhanVien', maNhanVien || '');
        fd.append('__RequestVerificationToken', tokenInput ? tokenInput.value : '');
        rows.forEach(function (r, i) {
            fd.append('rows[' + i + '].LoaiThietBi', r.loai || '');
            fd.append('rows[' + i + '].Serial', r.serial || '');
            fd.append('rows[' + i + '].Ip', r.ip || '');
        });

        return fetch('/QLKiemKe/KiemTraTrungTaiSanKhac', { method: 'POST', body: fd })
            .then(function (res) {
                if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status + ' khi kiểm tra trùng Tài sản khác.');
                return res.json();
            })
            .then(function (res) {
                if (!res.thanhCong) return { tiepTuc: false, thongBao: res.thongBao || res.message || 'Không kiểm tra được trùng Tài sản khác.' };

                var dsTrung = res.duLieu || [];
                // Đang đứng tên đúng người này = kiểm kê lại → ghi đè luôn, không hỏi
                dsTrung.forEach(function (t) { if (t.cungNguoiDung) rows[t.viTriDong].idGhiDe = t.idThietBi; });

                var canHoi = dsTrung.filter(function (t) { return !t.cungNguoiDung; });
                if (canHoi.length === 0) return { tiepTuc: true };

                var noiDung = document.createElement('div');
                var moDau = document.createElement('p');
                moDau.className = 'mb-2';
                moDau.textContent = 'Các dòng dưới đây trùng thiết bị đã có của người khác. Đúng là cùng thiết bị thì bấm "Đúng máy này" để cập nhật sang người dùng/vị trí mới; nếu là máy khác thì bấm "Sửa lại" và kiểm tra Serial/IP trên tem.';
                noiDung.appendChild(moDau);
                canHoi.forEach(function (t) { noiDung.appendChild(dongMoTaTrung(t)); });

                return Swal.fire({
                    icon: 'warning',
                    title: 'Phát hiện thiết bị trùng',
                    html: noiDung,
                    width: 680,
                    showCancelButton: true,
                    confirmButtonText: 'Đúng máy này - ghi đè',
                    cancelButtonText: 'Sửa lại',
                    confirmButtonColor: '#d97706',
                    reverseButtons: true,
                    focusCancel: true
                }).then(function (kq) {
                    if (!kq.isConfirmed) {
                        return { tiepTuc: false, thongBao: 'Chưa gửi. Kiểm tra lại Serial/IP ở dòng ' + canHoi.map(function (t) { return t.viTriDong + 1; }).join(', ') + ' của Tài sản khác.' };
                    }
                    canHoi.forEach(function (t) { rows[t.viTriDong].idGhiDe = t.idThietBi; });
                    return { tiepTuc: true };
                });
            });
    };
})();
