// Nút "Thêm lên WLC" ở trang chi tiết đơn Wi-Fi: mở modal hỏi mật khẩu domain rồi gọi
// /FormIT/KiemTraMacWifi (chỉ tra) hoặc /FormIT/ThemMacWifi (server tự tra trước, MAC đã có thì không ghi đè).
// Mật khẩu không giữ lại giữa các lần (xoá ô khi đóng modal).
(function () {
    const modal = document.getElementById('modalThemMacWlc');
    if (!modal) return;

    const form = document.getElementById('formThemMacWlc');
    const oMac = document.getElementById('themMacWlcMac');
    const oHoSo = document.getElementById('themMacWlcHoSo');
    const oMoTa = document.getElementById('themMacWlcMoTa');
    const oTaiKhoan = document.getElementById('themMacWlcTaiKhoan');
    const oMatKhau = document.getElementById('themMacWlcMatKhau');
    const oKetQua = document.getElementById('themMacWlcKetQua');
    const oLoi = document.getElementById('themMacWlcLoi');
    const btnGui = document.getElementById('btnGuiThemMacWlc');
    const btnKiemTra = document.getElementById('btnKiemTraMacWlc');
    let nutDangChon = null;
    let dangGui = false;

    // Cùng quy tắc với WlcMacService.ChuanHoaMoTa ở server, để người dùng thấy đúng chuỗi sẽ lên WLC
    function boDau(s) {
        return (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D')
            .replace(/[\s/|]+/g, '_').replace(/[^A-Za-z0-9._-]/g, '').replace(/_{2,}/g, '_').slice(0, 60);
    }

    function hienLoi(text) {
        oLoi.textContent = text || '';
        oLoi.hidden = !text;
    }

    function hienKetQua(text, daCo) {
        oKetQua.textContent = text || '';
        oKetQua.hidden = !text;
        oKetQua.classList.toggle('da-co', !!daCo);
    }

    // MAC đã có trên WLC: hiện từng trường đang lưu để IT so với đơn trước khi quyết định
    function hienCauHinhDangCo(hienCo) {
        hienKetQua('⚠️ MAC này đã tồn tại trên WLC — không ghi đè.', true);
        const bang = document.createElement('table');
        bang.className = 'them-mac-wlc-bang';
        [
            ['MAC', hienCo.mac],
            ['Tên đang lưu (description)', hienCo.moTa || '(trống)'],
            ['WLAN profile', hienCo.wlanProfile || '(trống)'],
            ['AAA attribute list', hienCo.aaaList || '(trống)']
        ].forEach(function (dong) {
            const tr = bang.insertRow();
            tr.insertCell().textContent = dong[0];
            tr.insertCell().textContent = dong[1];
        });
        oKetQua.appendChild(bang);
        const raw = document.createElement('code');
        raw.textContent = hienCo.dong;
        oKetQua.appendChild(raw);
    }

    function dong() {
        modal.hidden = true;
        oMatKhau.value = '';
        hienLoi('');
        hienKetQua('');
        nutDangChon = null;
    }

    function danhDauNut(nut, daThem, hienCo) {
        nut.disabled = true;
        nut.textContent = daThem ? '✔ Đã thêm' : '✔ Đã có: ' + (hienCo?.moTa || 'không mô tả');
        if (hienCo) nut.title = hienCo.dong;
        nut.style.background = daThem ? '#16a34a' : '#64748b';
        nut.style.cursor = 'default';
    }

    document.addEventListener('click', function (e) {
        const nut = e.target.closest('.btn-them-mac-wlc');
        if (!nut) return;
        e.preventDefault();
        nutDangChon = nut;
        oMac.textContent = nut.dataset.mac || '';
        oMoTa.value = boDau(nut.dataset.moTa);
        hienLoi('');
        hienKetQua('');
        modal.hidden = false;
        // Tài khoản giữ lại giữa các lần mở trong trang (không phải bí mật), chỉ mật khẩu bị xoá
        (oTaiKhoan.value.trim() ? oMatKhau : oTaiKhoan).focus();
    });

    oMoTa.addEventListener('blur', function () { oMoTa.value = boDau(oMoTa.value).replace(/^_+|_+$/g, ''); });
    document.getElementById('btnHuyThemMacWlc').addEventListener('click', function () { if (!dangGui) dong(); });
    modal.addEventListener('click', function (e) { if (e.target === modal && !dangGui) dong(); });

    async function gui(url, nutBam, chuCho) {
        if (!nutDangChon || dangGui) return null;
        if (!oTaiKhoan.value.trim()) { hienLoi('Nhập tài khoản domain.'); oTaiKhoan.focus(); return null; }
        if (!oMatKhau.value) { hienLoi('Nhập mật khẩu domain.'); oMatKhau.focus(); return null; }

        const nut = nutDangChon;
        const chuGoc = nutBam.textContent;
        dangGui = true;
        btnGui.disabled = btnKiemTra.disabled = true;
        nutBam.textContent = chuCho;
        hienLoi('');
        try {
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({
                    idFormIt: Number(modal.dataset.idForm),
                    idChiTiet: Number(nut.dataset.idChiTiet),
                    viTri: Number(nut.dataset.viTri),
                    hoSo: oHoSo.value,
                    moTa: boDau(oMoTa.value),
                    taiKhoan: oTaiKhoan.value.trim(),
                    matKhau: oMatKhau.value
                })
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return { nut: nut, data: await res.json() };
        } catch (err) {
            hienLoi('Lỗi kết nối máy chủ (' + err.message + ').');
            return null;
        } finally {
            dangGui = false;
            btnGui.disabled = btnKiemTra.disabled = false;
            nutBam.textContent = chuGoc;
        }
    }

    btnKiemTra.addEventListener('click', async function () {
        const kq = await gui('/FormIT/KiemTraMacWifi', btnKiemTra, 'Đang tra WLC...');
        if (!kq) return;
        const d = kq.data;
        if (!d.thanhCong) { hienLoi(d.thongBao || 'Tra cứu thất bại.'); oMatKhau.value = ''; oMatKhau.focus(); return; }
        if (d.duLieu?.daTonTai) {
            hienCauHinhDangCo(d.duLieu.hienCo);
            danhDauNut(kq.nut, false, d.duLieu.hienCo);
        } else {
            hienKetQua('Chưa có trên WLC — có thể thêm.', false);
        }
    });

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        const kq = await gui('/FormIT/ThemMacWifi', btnGui, 'Đang tra & đẩy lệnh lên WLC...');
        if (!kq) return;
        const d = kq.data;
        if (d.thanhCong) {
            danhDauNut(kq.nut, true);
            dong();
        } else if (d.duLieu?.daTonTai) {
            // Giữ modal mở để IT đọc dòng cấu hình đang có trên WLC
            danhDauNut(kq.nut, false, d.duLieu.hienCo);
            if (d.duLieu.hienCo) hienCauHinhDangCo(d.duLieu.hienCo); else hienKetQua(d.thongBao, true);
        } else {
            // Giữ modal mở để sửa mật khẩu/profile rồi gửi lại
            hienLoi(d.thongBao || 'Thêm MAC thất bại.');
            oMatKhau.value = '';
            oMatKhau.focus();
        }
    });
})();
