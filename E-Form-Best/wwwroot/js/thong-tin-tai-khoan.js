// Trang Hồ sơ cá nhân (/ThongTinTaiKhoan): đổi/xoá ảnh đại diện, đổi mật khẩu, dịch nhãn offline.
(function () {
    'use strict';

    const banDich = {
        vi: {
            'title-page': 'Hồ sơ cá nhân', 'status-online': 'Trực tuyến', 'btn-close': 'Đóng', 'btn-exit': 'Đăng xuất',
            'sec-info': 'Thông tin tài khoản', 'label-manv': 'Mã nhân viên', 'label-tk': 'Tài khoản', 'label-email': 'Email',
            'label-sdt': 'Số điện thoại', 'label-co': 'Công ty', 'label-dept': 'Phòng ban',
            'label-ngaytao': 'Ngày tạo tài khoản', 'label-capnhat': 'Cập nhật lần cuối', 'empty-val': 'Chưa cập nhật',
            'label-assigned': 'Bộ phận phụ trách', 'no-dept': 'Chưa được gán bộ phận',
            'sec-security': 'Bảo mật', 'label-pass': 'Mật khẩu',
            'desc-pass': 'Sau khi đổi mật khẩu, mọi phiên đăng nhập sẽ bị đăng xuất và bạn cần đăng nhập lại.',
            'btn-pass': 'Đổi mật khẩu', 'title-pass': 'Thay đổi mật khẩu', 'old-pass': 'Mật khẩu hiện tại',
            'new-pass': 'Mật khẩu mới', 'confirm-pass': 'Xác nhận mật khẩu mới', 'btn-save': 'Cập nhật', 'btn-cancel': 'Hủy'
        },
        en: {
            'title-page': 'My profile', 'status-online': 'Online', 'btn-close': 'Close', 'btn-exit': 'Log out',
            'sec-info': 'Account information', 'label-manv': 'Employee ID', 'label-tk': 'Username', 'label-email': 'Email',
            'label-sdt': 'Phone', 'label-co': 'Company', 'label-dept': 'Department',
            'label-ngaytao': 'Created on', 'label-capnhat': 'Last updated', 'empty-val': 'Not set',
            'label-assigned': 'Assigned sections', 'no-dept': 'No section assigned',
            'sec-security': 'Security', 'label-pass': 'Password',
            'desc-pass': 'After changing your password, all sessions are signed out and you must log in again.',
            'btn-pass': 'Change password', 'title-pass': 'Change password', 'old-pass': 'Current password',
            'new-pass': 'New password', 'confirm-pass': 'Confirm new password', 'btn-save': 'Update', 'btn-cancel': 'Cancel'
        },
        zh: {
            'title-page': '个人资料', 'status-online': '在线', 'btn-close': '关闭', 'btn-exit': '退出',
            'sec-info': '账户信息', 'label-manv': '员工编号', 'label-tk': '账号', 'label-email': '邮箱',
            'label-sdt': '电话', 'label-co': '公司', 'label-dept': '部门',
            'label-ngaytao': '创建日期', 'label-capnhat': '最后更新', 'empty-val': '未填写',
            'label-assigned': '负责部门', 'no-dept': '未分配部门',
            'sec-security': '安全', 'label-pass': '密码',
            'desc-pass': '更改密码后，所有会话将被注销，需要重新登录。',
            'btn-pass': '更改密码', 'title-pass': '更改密码', 'old-pass': '当前密码',
            'new-pass': '新密码', 'confirm-pass': '确认新密码', 'btn-save': '更新', 'btn-cancel': '取消'
        }
    };

    function dichTrang(ngonNgu) {
        const bang = banDich[ngonNgu];
        if (!bang) return;
        document.querySelectorAll('[data-key]').forEach(el => {
            const giaTri = bang[el.getAttribute('data-key')];
            if (giaTri) el.textContent = giaTri;
        });
        document.querySelectorAll('[data-ngon-ngu]').forEach(b => b.classList.toggle('dang-chon', b.dataset.ngonNgu === ngonNgu));
        document.documentElement.lang = ngonNgu;
        try { localStorage.setItem('ttkNgonNgu', ngonNgu); } catch (e) { /* trình duyệt chặn storage: bỏ qua */ }
    }

    async function docJson(res) {
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    // ===== ẢNH ĐẠI DIỆN =====
    const anh = document.getElementById('profileImg');
    const khungAnh = document.getElementById('khungAnh');
    const thongBaoAnh = document.getElementById('thongBaoAnh');
    const btnXoaAnh = document.getElementById('btnXoaAnh');
    const fileInput = document.getElementById('fileInput');

    function anhMacDinh() {
        anh.onerror = null;
        anh.src = anh.dataset.anhMacDinh;
    }
    // Script nạp cuối trang: ảnh có thể đã lỗi trước khi gắn sự kiện
    if (anh.complete && anh.naturalWidth === 0) anhMacDinh();
    else anh.addEventListener('error', anhMacDinh, { once: true });

    function baoAnh(noiDung, laLoi) {
        thongBaoAnh.textContent = noiDung || '';
        thongBaoAnh.classList.toggle('loi', !!laLoi);
    }

    async function taiAnhLen(file) {
        if (!file.type.startsWith('image/')) { baoAnh('Vui lòng chọn file ảnh.', true); return; }

        const formData = new FormData();
        formData.append('fileAnh', file);
        khungAnh.classList.add('dang-tai');
        baoAnh('Đang tải ảnh lên...');
        try {
            const kq = await docJson(await fetch('/DonXetDuyet/CapNhatAnh', { method: 'POST', body: formData }));
            if (!kq.success) { baoAnh(kq.message || 'Không cập nhật được ảnh.', true); return; }
            // Đổi ảnh tại chỗ, thêm tham số thời gian để trình duyệt không lấy ảnh cũ trong cache
            anh.addEventListener('error', anhMacDinh, { once: true });
            anh.src = '/DonXetDuyet/GetAvatar?path=' + encodeURIComponent(kq.tenFile || '') + '&t=' + Date.now();
            btnXoaAnh.hidden = false;
            baoAnh('');
        } catch (err) {
            baoAnh('Lỗi kết nối máy chủ, vui lòng thử lại.', true);
        } finally {
            khungAnh.classList.remove('dang-tai');
            fileInput.value = '';
        }
    }

    async function xoaAnh() {
        if (!confirm('Xóa ảnh đại diện hiện tại?')) return;
        btnXoaAnh.disabled = true;
        try {
            const kq = await docJson(await fetch('/DonXetDuyet/XoaAnhDaiDien', { method: 'POST' }));
            if (!kq.success) { baoAnh(kq.message || 'Không xóa được ảnh.', true); return; }
            anhMacDinh();
            btnXoaAnh.hidden = true;
            baoAnh('');
        } catch (err) {
            baoAnh('Lỗi kết nối máy chủ, vui lòng thử lại.', true);
        } finally {
            btnXoaAnh.disabled = false;
        }
    }

    // ===== ĐỔI MẬT KHẨU =====
    const hopThoai = document.getElementById('passwordModal');
    const formMk = document.getElementById('changePasswordForm');
    const loiMk = document.getElementById('loiDoiMk');
    const btnLuuMk = document.getElementById('btnUpdatePass');

    function baoLoiMk(noiDung, thanhCong) {
        loiMk.textContent = noiDung || '';
        loiMk.classList.toggle('hien', !!noiDung);
        loiMk.classList.toggle('thanh-cong', !!thanhCong);
    }
    function moHopThoai() {
        baoLoiMk('');
        hopThoai.classList.add('mo');
        document.getElementById('oldPassword').focus();
    }
    function dongHopThoai() {
        if (btnLuuMk.disabled) return;
        hopThoai.classList.remove('mo');
    }

    formMk.addEventListener('submit', async function (e) {
        e.preventDefault();
        const cu = formMk.oldPassword.value;
        const moi = formMk.newPassword.value;
        const xacNhan = formMk.confirmPassword.value;

        if (!cu || !moi || !xacNhan) { baoLoiMk('Vui lòng nhập đủ các ô.'); return; }
        if (moi !== xacNhan) { baoLoiMk('Mật khẩu xác nhận không khớp!'); formMk.confirmPassword.focus(); return; }

        const nhanCu = btnLuuMk.innerHTML;
        btnLuuMk.disabled = true;
        btnLuuMk.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        baoLoiMk('');
        let xong = false;
        try {
            const kq = await docJson(await fetch('/DonXetDuyet/DoiMatKhau', { method: 'POST', body: new FormData(formMk) }));
            if (kq.success) {
                xong = true;
                baoLoiMk(kq.message || 'Đổi mật khẩu thành công!', true);
                // Máy chủ đã đăng xuất phiên hiện tại -> sang trang đăng nhập (điều hướng hợp lệ)
                setTimeout(() => { window.location.href = '/DonXetDuyet/DangNhap'; }, 1500);
            } else {
                baoLoiMk(kq.message || 'Không đổi được mật khẩu.');
            }
        } catch (err) {
            baoLoiMk('Lỗi hệ thống, vui lòng thử lại.');
        } finally {
            if (!xong) {
                btnLuuMk.disabled = false;
                btnLuuMk.innerHTML = nhanCu;
            }
        }
    });

    // ===== SỰ KIỆN =====
    document.addEventListener('click', function (e) {
        const nutNgonNgu = e.target.closest('[data-ngon-ngu]');
        if (nutNgonNgu) { dichTrang(nutNgonNgu.dataset.ngonNgu); return; }

        const nutHienMk = e.target.closest('.ttk-hien-mk');
        if (nutHienMk) {
            const o = nutHienMk.parentElement.querySelector('input');
            const dangAn = o.type === 'password';
            o.type = dangAn ? 'text' : 'password';
            nutHienMk.querySelector('i').className = dangAn ? 'fas fa-eye-slash' : 'fas fa-eye';
            return;
        }

        if (e.target.closest('#btnDoiAnh')) { fileInput.click(); return; }
        if (e.target.closest('#btnXoaAnh')) { xoaAnh(); return; }
        if (e.target.closest('#btnMoDoiMatKhau')) { moHopThoai(); return; }
        if (e.target.closest('#btnHuyDoiMk')) { dongHopThoai(); return; }
        if (e.target.closest('#btnDong')) { window.close(); return; }
        if (e.target === hopThoai) dongHopThoai();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && hopThoai.classList.contains('mo')) dongHopThoai();
    });

    fileInput.addEventListener('change', function () {
        if (this.files && this.files[0]) taiAnhLen(this.files[0]);
    });

    try {
        const daLuu = localStorage.getItem('ttkNgonNgu');
        if (daLuu && daLuu !== 'vi') dichTrang(daLuu);
    } catch (e) { /* bỏ qua */ }
})();
