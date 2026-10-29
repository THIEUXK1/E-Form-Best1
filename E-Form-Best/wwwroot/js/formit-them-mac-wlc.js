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
    // Mỗi WLC một nút "Thêm lên <WLC>" (data-ip), thêm riêng từng con
    const dsNutGui = Array.from(modal.querySelectorAll('.btn-gui-them-mac-wlc'));
    const dsWlcModal = dsNutGui.map(function (b) { return { wlc: b.dataset.wlc, ip: b.dataset.ip, co: null }; });
    function khoaNutGui(khoa) { dsNutGui.forEach(function (b) { b.disabled = khoa; }); }
    const btnKiemTra = document.getElementById('btnKiemTraMacWlc');
    // Form RADIUS (PDA / TV) cùng modal, chỉ có khi công ty đã cấu hình RadiusMac
    const oRadMac = document.getElementById('themMacRadiusMac');
    const oRadNhom = document.getElementById('themMacRadiusNhom');
    const oRadMoTa = document.getElementById('themMacRadiusMoTa');
    const oRadKetQua = document.getElementById('themMacRadiusKetQua');
    const oRadLoi = document.getElementById('themMacRadiusLoi');
    const btnRadius = document.getElementById('btnGuiThemMacRadius');
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

    // Mỗi WLC một khối: đã thêm / đã có (kèm các trường đang lưu để IT so với đơn) / chưa có / lỗi
    function hienKetQuaTungWlc(ds, laTraCuu) {
        oKetQua.textContent = '';
        oKetQua.hidden = false;
        oKetQua.classList.toggle('da-co', ds.some(function (k) { return k.daTonTai; }));
        ds.forEach(function (k) {
            const khoi = document.createElement('div');
            khoi.className = 'them-mac-wlc-mot';
            const tieuDe = document.createElement('b');
            const bieuTuong = k.thanhCong ? (laTraCuu ? '🆕' : '✅') : (k.daTonTai ? '⚠️' : '❌');
            tieuDe.textContent = bieuTuong + ' ' + k.wlc + ' — ' + (k.thanhCong && laTraCuu ? 'Chưa có, có thể thêm.' : k.thongBao);
            khoi.appendChild(tieuDe);
            if (k.hienCo) {
                const bang = document.createElement('table');
                bang.className = 'them-mac-wlc-bang';
                [
                    ['MAC', k.hienCo.mac],
                    ['Tên đang lưu (description)', k.hienCo.moTa || '(trống)'],
                    ['WLAN', k.hienCo.wlanProfile || '(trống)'],
                    ['AAA list / interface', k.hienCo.aaaList || '(trống)']
                ].forEach(function (dong) {
                    const tr = bang.insertRow();
                    tr.insertCell().textContent = dong[0];
                    tr.insertCell().textContent = dong[1];
                });
                khoi.appendChild(bang);
                if (k.daTonTai && k.ip) {
                    const nutGo = document.createElement('button');
                    nutGo.type = 'button';
                    nutGo.className = 'btn-go-mac-wlc';
                    nutGo.dataset.ip = k.ip;
                    nutGo.dataset.wlc = k.wlc;
                    nutGo.textContent = 'Gỡ khỏi WLC này';
                    khoi.appendChild(nutGo);
                }
            }
            oKetQua.appendChild(khoi);
        });
    }

    function hienRadius(oHop, text, daCo) {
        if (!oHop) return;
        oHop.textContent = text || '';
        oHop.hidden = !text;
        oHop.classList.toggle('da-co', !!daCo);
    }

    function dong() {
        modal.hidden = true;
        oMatKhau.value = '';
        hienLoi('');
        hienKetQua('');
        hienRadius(oRadKetQua, '');
        hienRadius(oRadLoi, '');
        nutDangChon = null;
    }

    // Kết quả server -> MAC có trên từng WLC không: true có / false thiếu / null chưa tra được (lỗi).
    // Khi tra cứu, thanhCong nghĩa là "chưa có"; khi thêm, thanhCong nghĩa là vừa thêm xong.
    function trangThaiTuKq(ds, laTraCuu) {
        return ds.map(function (k) {
            return { wlc: k.wlc, ip: k.ip, co: k.daTonTai ? true : (k.thanhCong ? !laTraCuu : null) };
        });
    }

    // Đủ mọi WLC -> nút "Đã có" (khoá); thiếu WLC nào -> giữ "+" (chi tiết từng WLC xem ở tooltip)
    function capNhatNut(nut, tt) {
        nut._trangThai = tt;
        capNhatNutKhongVe(nut, tt);
        veBang();
    }

    function capNhatNutKhongVe(nut, tt) {
        nut.parentNode.querySelector('.btn-mo-lai-mac-wlc')?.remove();
        nut.title = tt.map(function (t) {
            return t.wlc + ': ' + (t.co === true ? 'đã có' : t.co === false ? 'chưa có' : 'chưa tra được');
        }).join('\n');

        if (tt.every(function (t) { return t.co === true; })) {
            nut.disabled = true;
            nut.textContent = 'Đã có';
            Object.assign(nut.style, { width: 'auto', padding: '0 8px', fontSize: '13px', background: '#16a34a', cursor: 'default' });
            // Quyền All vẫn mở lại modal được (kiểm tra lại / gỡ) qua nút "+" nhỏ cạnh "Đã có"
            if (modal.dataset.laAll === '1') {
                const moLai = document.createElement('button');
                moLai.type = 'button';
                moLai.className = 'btn-mo-lai-mac-wlc';
                moLai.textContent = '+';
                moLai.title = 'Mở lại để kiểm tra / gỡ MAC trên WLC';
                moLai.setAttribute('aria-label', moLai.title);
                nut.after(moLai);
            }
            return;
        }
        nut.disabled = false;
        nut.textContent = '+';
        Object.assign(nut.style, { width: '28px', padding: '0', fontSize: '18px', background: '#0369a1', cursor: 'pointer' });
    }

    // Bảng dưới bình luận: mỗi MAC một dòng, mỗi WLC một cột (đã có / thiếu / chưa tra được).
    // Dựng lại từ trạng thái các nút "+" nên luôn khớp sau mỗi lần kiểm tra / thêm / gỡ.
    const thanBang = document.getElementById('bangMacWlcThan');
    const dsCotWlc = thanBang ? Array.from(document.querySelectorAll('#bangMacWlc th[data-ip]')).map(function (th) { return th.dataset.ip; }) : [];
    function veBang() {
        if (!thanBang) return;
        thanBang.textContent = '';
        const dsNut = document.querySelectorAll('.btn-them-mac-wlc');
        if (!dsNut.length) {
            const td = thanBang.insertRow().insertCell();
            td.colSpan = 1 + dsCotWlc.length;
            td.className = 'bang-mac-wlc-trong';
            td.textContent = 'Đơn không có MAC hợp lệ.';
            return;
        }
        dsNut.forEach(function (nut) {
            const tr = thanBang.insertRow();
            // Mã thiết bị ở trên, MAC ngay dòng dưới trong cùng ô -> bảng hẹp, không đẩy vỡ cột bình luận
            const tdTb = tr.insertCell();
            tdTb.className = 'thiet-bi';
            const ma = document.createElement('b');
            ma.textContent = nut.dataset.moTa || '(không mã)';
            const mac = document.createElement('span');
            mac.textContent = nut.dataset.mac || '';
            tdTb.append(ma, mac);
            dsCotWlc.forEach(function (ip) {
                const t = (nut._trangThai || []).find(function (x) { return x.ip === ip; });
                const co = t ? t.co : null;
                const td = tr.insertCell();
                td.className = co === true ? 'co' : co === false ? 'thieu' : 'chua-ro';
                // Chỉ biểu tượng cho gọn, chữ để ở tooltip
                td.textContent = co === true ? '✔' : co === false ? '✘' : '?';
                td.title = co === true ? 'Đã có' : co === false ? 'Thiếu' : 'Chưa tra được';
            });
        });
    }

    document.addEventListener('click', function (e) {
        const moLai = e.target.closest('.btn-mo-lai-mac-wlc');
        const nut = moLai ? moLai.parentNode.querySelector('.btn-them-mac-wlc') : e.target.closest('.btn-them-mac-wlc');
        if (!nut) return;
        e.preventDefault();
        nutDangChon = nut;
        oMac.textContent = nut.dataset.mac || '';
        oMoTa.value = boDau(nut.dataset.moTa);
        hienLoi('');
        hienKetQua('');
        if (btnRadius) {
            oRadMac.textContent = nut.dataset.mac || '';
            oRadMoTa.value = boDau(nut.dataset.moTa);
            hienRadius(oRadKetQua, '');
            hienRadius(oRadLoi, '');
        }
        modal.hidden = false;
        // Tài khoản giữ lại giữa các lần mở trong trang (không phải bí mật), chỉ mật khẩu bị xoá
        (oTaiKhoan.value.trim() ? oMatKhau : oTaiKhoan).focus();
    });

    oMoTa.addEventListener('blur', function () { oMoTa.value = boDau(oMoTa.value).replace(/^_+|_+$/g, ''); });
    document.getElementById('btnHuyThemMacWlc').addEventListener('click', function () { if (!dangGui) dong(); });
    modal.addEventListener('click', function (e) { if ((e.target === modal || e.target.classList.contains('them-mac-wlc-cum')) && !dangGui) dong(); });

    async function gui(url, nutBam, chuCho, ipWlc) {
        if (!nutDangChon || dangGui) return null;
        if (!oTaiKhoan.value.trim()) { hienLoi('Nhập tài khoản domain.'); oTaiKhoan.focus(); return null; }
        if (!oMatKhau.value) { hienLoi('Nhập mật khẩu domain.'); oMatKhau.focus(); return null; }

        const nut = nutDangChon;
        const chuGoc = nutBam.textContent;
        dangGui = true;
        khoaNutGui(true);
        btnKiemTra.disabled = nutBam.disabled = true;
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
                    matKhau: oMatKhau.value,
                    ipWlc: ipWlc || null
                })
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return { nut: nut, data: await res.json() };
        } catch (err) {
            hienLoi('Lỗi kết nối máy chủ (' + err.message + ').');
            return null;
        } finally {
            dangGui = false;
            khoaNutGui(false);
            btnKiemTra.disabled = nutBam.disabled = false;
            nutBam.textContent = chuGoc;
        }
    }

    // Mở trang: đánh dấu sẵn từng nút theo bản quét MAC trên WLC (job nền), không cần mật khẩu.
    // Lỗi / chưa có bản quét thì để nguyên "+", IT vẫn bấm Kiểm tra tay được.
    async function danhDauSan() {
        veBang();
        try {
            const res = await fetch('/FormIT/TrangThaiMacWifi/' + encodeURIComponent(modal.dataset.idForm));
            if (!res.ok) return;
            const d = await res.json();
            if (!d.thanhCong || !Array.isArray(d.duLieu)) return;
            d.duLieu.forEach(function (dong) {
                const nut = document.querySelector('.btn-them-mac-wlc[data-id-chi-tiet="' + dong.idChiTiet + '"][data-vi-tri="' + dong.viTri + '"]');
                if (!nut || !dong.wlc.some(function (w) { return w.co !== null; })) return;
                capNhatNut(nut, dong.wlc.map(function (w) { return { wlc: w.wlc, ip: w.ip, co: w.co }; }));
                const lanQuet = dong.wlc.map(function (w) { return w.quetLuc; }).filter(Boolean).sort()[0];
                if (lanQuet) nut.title += '\n(theo bản quét WLC lúc ' + new Date(lanQuet).toLocaleString('vi-VN') + ')';
            });
            const quetCu = d.duLieu.flatMap(function (dong) { return dong.wlc.map(function (w) { return w.quetLuc; }); }).filter(Boolean).sort()[0];
            const oQuet = document.getElementById('bangMacWlcQuet');
            if (oQuet && quetCu) oQuet.textContent = 'theo bản quét lúc ' + new Date(quetCu).toLocaleString('vi-VN');
        } catch (err) {
            console.warn('Không đọc được bản quét MAC trên WLC:', err);
        }
    }
    danhDauSan();

    // Gỡ MAC khỏi 1 WLC trong kết quả kiểm tra; xong thì thay khối đó bằng kết quả gỡ, các WLC khác giữ nguyên
    oKetQua.addEventListener('click', async function (e) {
        const nutGo = e.target.closest('.btn-go-mac-wlc');
        if (!nutGo || dangGui) return;
        if (!confirm('Gỡ MAC ' + oMac.textContent + ' khỏi ' + nutGo.dataset.wlc + '?\nThiết bị sẽ mất Wi-Fi qua WLC này.')) return;
        const kq = await gui('/FormIT/GoMacWifi', nutGo, 'Đang gỡ...', nutGo.dataset.ip);
        if (!kq) return;
        const d = kq.data;
        const khoi = nutGo.closest('.them-mac-wlc-mot');
        if (!d.duLieu?.ketQua) { hienLoi(d.thongBao || 'Gỡ MAC thất bại.'); oMatKhau.value = ''; oMatKhau.focus(); return; }
        const tieuDe = khoi.querySelector('b');
        tieuDe.textContent = (d.thanhCong ? '🗑️ ' : '❌ ') + d.thongBao;
        if (d.thanhCong) {
            khoi.querySelector('table')?.remove();
            nutGo.remove();
            // WLC vừa gỡ thành "thiếu" -> nút dòng thiết bị mở lại "+" kèm ghi chú
            const tt = (kq.nut._trangThai || []).map(function (t) {
                return t.ip === nutGo.dataset.ip ? Object.assign({}, t, { co: false }) : t;
            });
            if (tt.length) capNhatNut(kq.nut, tt);
        } else {
            oMatKhau.value = ''; oMatKhau.focus();
        }
    });

    btnKiemTra.addEventListener('click', async function () {
        const kq = await gui('/FormIT/KiemTraMacWifi', btnKiemTra, 'Đang tra WLC...');
        if (!kq) return;
        const d = kq.data;
        const ds = d.duLieu?.ketQua;
        if (!ds) { hienLoi(d.thongBao || 'Tra cứu thất bại.'); oMatKhau.value = ''; oMatKhau.focus(); return; }
        hienKetQuaTungWlc(ds, true);
        capNhatNut(kq.nut, trangThaiTuKq(ds, true));
        if (ds.some(function (k) { return !k.thanhCong && !k.daTonTai; })) { oMatKhau.value = ''; oMatKhau.focus(); }
    });

    // RADIUS: app trả ALREADY_EXISTS khi MAC đã có nên bấm lại không tạo trùng; dùng chung cờ dangGui với WLC
    if (btnRadius) {
        document.getElementById('formThemMacRadius').addEventListener('submit', function (e) { e.preventDefault(); });
        oRadMoTa.addEventListener('blur', function () { oRadMoTa.value = boDau(oRadMoTa.value).replace(/^_+|_+$/g, ''); });
        btnRadius.addEventListener('click', async function () {
            if (!nutDangChon || dangGui) return;
            const nut = nutDangChon;
            const chuGoc = btnRadius.textContent;
            dangGui = true;
            btnRadius.disabled = true;
            btnRadius.textContent = 'Đang thêm...';
            hienRadius(oRadLoi, '');
            hienRadius(oRadKetQua, '');
            try {
                const res = await fetch('/FormIT/ThemMacRadius', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    },
                    body: JSON.stringify({
                        idFormIt: Number(modal.dataset.idForm),
                        idChiTiet: Number(nut.dataset.idChiTiet),
                        viTri: Number(nut.dataset.viTri),
                        nhom: oRadNhom.value,
                        moTa: boDau(oRadMoTa.value)
                    })
                });
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const d = await res.json();
                if (!d.thanhCong) { hienRadius(oRadLoi, d.thongBao || 'Thêm MAC lên RADIUS thất bại.'); return; }
                const dsTen = d.duLieu?.usernames?.length ? ' (' + d.duLieu.usernames.join(', ') + ')' : '';
                hienRadius(oRadKetQua, (d.duLieu?.daTonTai ? '⚠️ ' : '✅ ') + d.thongBao + dsTen, d.duLieu?.daTonTai);
            } catch (err) {
                hienRadius(oRadLoi, 'Lỗi kết nối máy chủ (' + err.message + ').');
            } finally {
                dangGui = false;
                btnRadius.disabled = false;
                btnRadius.textContent = chuGoc;
            }
        });
    }

    // Enter trong ô nhập không tự đẩy lệnh lên WLC nào — phải bấm đúng nút của WLC
    form.addEventListener('submit', function (e) { e.preventDefault(); });

    dsNutGui.forEach(function (nutWlc) { nutWlc.addEventListener('click', async function () {
        const kq = await gui('/FormIT/ThemMacWifi', nutWlc, 'Đang tra & đẩy lệnh...', nutWlc.dataset.ip);
        if (!kq) return;
        const d = kq.data;
        const ds = d.duLieu?.ketQua;
        if (!ds) { hienLoi(d.thongBao || 'Thêm MAC thất bại.'); oMatKhau.value = ''; oMatKhau.focus(); return; }
        // Luôn giữ modal mở để IT đọc kết quả; WLC lỗi thì bấm lại (WLC đã có sẽ không bị ghi đè)
        hienKetQuaTungWlc(ds, false);
        // Chỉ thêm 1 WLC: gộp vào trạng thái đã biết của các WLC khác, chưa biết thì để "chưa tra được"
        const moi = trangThaiTuKq(ds, false);
        const tt = (kq.nut._trangThai || dsWlcModal).map(function (t) {
            return moi.find(function (m) { return m.ip === t.ip; }) || t;
        });
        capNhatNut(kq.nut, tt);
        if (!d.thanhCong) { oMatKhau.value = ''; oMatKhau.focus(); }
    }); });
})();
