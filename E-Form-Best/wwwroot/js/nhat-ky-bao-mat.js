// Trang Nhật ký bảo mật (/NhatKyBaoMat) — chỉ quyền All. Dữ liệu người dùng nhập (đường dẫn, tài khoản gõ,
// User-Agent) có thể chứa mã độc → luôn dựng DOM bằng textContent, không ghép HTML.
(function () {
    'use strict';

    const LOAI_THEO_TAB = {
        baomat: [
            ['', 'Tất cả'],
            ['ChuaDangNhap', 'Chưa đăng nhập bị chặn'],
            ['KhongDuQuyen', 'Thiếu quyền bị chặn'],
            ['DuLieuNhayCam', 'Lấy dữ liệu nhạy cảm']
        ],
        dangnhaploi: [
            ['', 'Tất cả'],
            ['SaiMatKhau', 'Sai mật khẩu'],
            ['KhongTonTai', 'Tài khoản không tồn tại'],
            ['DangKhoa', 'Đang bị khoá'],
            ['VuaKhoa', 'Vừa bị khoá'],
            ['TokenLoi', 'Token lỗi']
        ],
        truycap: null
    };

    const TEN_LOAI = {
        ChuaDangNhap: 'Chưa đăng nhập', KhongDuQuyen: 'Thiếu quyền', DuLieuNhayCam: 'Dữ liệu nhạy cảm',
        SaiMatKhau: 'Sai mật khẩu', KhongTonTai: 'Không tồn tại', DangKhoa: 'Đang khoá', VuaKhoa: 'Vừa khoá', TokenLoi: 'Token lỗi'
    };

    const COT = {
        baomat: ['Thời gian', 'Loại', 'Tài khoản', 'Phương thức', 'Đường dẫn / dữ liệu', 'IP', 'Trình duyệt'],
        dangnhaploi: ['Thời gian', 'Lý do', 'Tài khoản đã gõ', 'Nơi gọi', 'IP', 'Trình duyệt'],
        truycap: ['Đăng nhập', 'Đăng xuất', 'Tài khoản', 'Máy tính', 'IP', 'Trạng thái', 'Trình duyệt']
    };

    const $ = (id) => document.getElementById(id);
    const form = $('nkbmLoc');
    let tab = 'baomat';
    let trang = 1;
    let soTrang = 1;
    let dangTai = false;

    function dinhDangGio(s) {
        if (!s) return '';
        const d = new Date(s);
        if (isNaN(d)) return s;
        const p = (n) => String(n).padStart(2, '0');
        return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
    }

    function o(text, lop) {
        const td = document.createElement('td');
        if (lop) td.className = lop;
        td.textContent = text == null ? '' : String(text);
        return td;
    }

    // Giờ đậm ở trên, ngày nhạt ở dưới — quét cột theo giờ dễ hơn chuỗi dài một dòng
    function oGio(s) {
        const td = document.createElement('td');
        td.className = 'nkbm-gio';
        const d = s ? new Date(s) : null;
        if (!d || isNaN(d)) {
            td.textContent = s || '—';
            if (!s) td.classList.add('nkbm-gio-trong');
            return td;
        }
        const p = (n) => String(n).padStart(2, '0');
        const b = document.createElement('b');
        b.textContent = `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
        const span = document.createElement('span');
        span.textContent = `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()}`;
        td.append(b, span);
        return td;
    }

    function oNguoi(taiKhoan, hoTen, chuKhiTrong) {
        const td = document.createElement('td');
        td.className = 'nkbm-nguoi';
        if (!taiKhoan) {
            td.textContent = chuKhiTrong || '—';
            td.classList.add('nkbm-nguoi-an');
            return td;
        }
        const b = document.createElement('b');
        b.textContent = taiKhoan;
        td.appendChild(b);
        if (hoTen) {
            const span = document.createElement('span');
            span.textContent = hoTen;
            td.appendChild(span);
        }
        return td;
    }

    // IP bấm được → lọc ngay bảng hiện tại theo IP đó
    function oIp(ip) {
        const td = document.createElement('td');
        if (!ip) { td.textContent = '—'; return td; }
        const a = document.createElement('a');
        a.href = '#';
        a.className = 'nkbm-the-ip nkbm-loc-ip';
        a.dataset.ip = ip;
        a.dataset.tab = tab;
        a.title = 'Lọc theo IP này';
        a.textContent = ip;
        td.appendChild(a);
        return td;
    }

    function oPhuongThuc(pt) {
        const td = document.createElement('td');
        if (!pt) return td;
        const span = document.createElement('span');
        const ma = String(pt).toUpperCase();
        span.className = 'nkbm-pt' + (/^[A-Z]+$/.test(ma) ? ' nkbm-pt-' + ma : '');
        span.textContent = ma;
        td.appendChild(span);
        return td;
    }

    function oTheLoai(loai, lopRieng) {
        const td = document.createElement('td');
        const span = document.createElement('span');
        span.className = 'nkbm-the-loai ' + (lopRieng || ('loai-' + loai));
        span.textContent = TEN_LOAI[loai] || loai || '';
        td.appendChild(span);
        return td;
    }

    function oUa(ua) {
        const td = o(ua, 'nkbm-ua');
        if (ua) td.title = ua;
        return td;
    }

    function hienLoi(thongBao) {
        const el = $('nkbmLoi');
        el.textContent = thongBao || '';
        el.hidden = !thongBao;
    }

    function dongTrong(text, icon) {
        const tb = $('nkbmThanBang');
        tb.replaceChildren();
        const tr = document.createElement('tr');
        const td = o('', 'nkbm-trong');
        td.colSpan = COT[tab].length;
        if (icon) {
            const i = document.createElement('i');
            i.className = 'fa ' + icon;
            td.appendChild(i);
        }
        td.appendChild(document.createTextNode(text));
        tr.appendChild(td);
        tb.appendChild(tr);
    }

    // Khung chờ: vài dòng xám nhấp nháy đúng số cột, đỡ giật bảng khi đổi trang/tab
    function dongCho() {
        const dong = [];
        for (let i = 0; i < 8; i++) {
            const tr = document.createElement('tr');
            tr.className = 'nkbm-xuong';
            COT[tab].forEach((_, j) => {
                const td = document.createElement('td');
                const vach = document.createElement('i');
                vach.style.width = (45 + ((i * 7 + j * 13) % 45)) + '%';
                td.appendChild(vach);
                tr.appendChild(td);
            });
            dong.push(tr);
        }
        $('nkbmThanBang').replaceChildren(...dong);
    }

    // Đánh dấu ô tổng quan khớp với tab + loại đang lọc
    function danhDauOTong() {
        const loai = LOAI_THEO_TAB[tab] ? $('chonLoai').value : '';
        document.querySelectorAll('.nkbm-o').forEach((el) => {
            el.classList.toggle('dang-chon', el.dataset.tab === tab && (el.dataset.loai || '') === loai);
        });
    }

    function veDauBang() {
        const tr = document.createElement('tr');
        COT[tab].forEach((ten) => {
            const th = document.createElement('th');
            th.textContent = ten;
            tr.appendChild(th);
        });
        $('nkbmDauBang').replaceChildren(tr);
    }

    function veChonLoai(giaTri) {
        const ds = LOAI_THEO_TAB[tab];
        $('nhanLoai').hidden = !ds;
        const sel = $('chonLoai');
        sel.replaceChildren();
        (ds || []).forEach(([v, t]) => {
            const op = document.createElement('option');
            op.value = v;
            op.textContent = t;
            sel.appendChild(op);
        });
        sel.value = giaTri || '';
    }

    function veDong(d) {
        const tr = document.createElement('tr');
        if (tab === 'baomat') {
            tr.appendChild(oGio(d.thoiGian));
            tr.appendChild(oTheLoai(d.loai));
            tr.appendChild(oNguoi(d.taiKhoan, d.hoTen, 'Chưa đăng nhập'));
            tr.appendChild(oPhuongThuc(d.phuongThuc));
            const td = o(d.duongDan, 'nkbm-duong-dan');
            if (d.moTa) {
                const nho = document.createElement('div');
                nho.className = 'nkbm-mo-ta';
                nho.textContent = d.moTa;
                td.appendChild(nho);
            }
            tr.appendChild(td);
            tr.appendChild(oIp(d.ip));
            tr.appendChild(oUa(d.trinhDuyet));
        } else if (tab === 'dangnhaploi') {
            tr.appendChild(oGio(d.thoiGian));
            tr.appendChild(oTheLoai(d.lyDo, 'loai-loi'));
            tr.appendChild(oNguoi(d.taiKhoan, '', '(trống)'));
            tr.appendChild(o(d.noiGoi, 'nkbm-may'));
            tr.appendChild(oIp(d.ip));
            tr.appendChild(oUa(d.trinhDuyet));
        } else {
            tr.appendChild(oGio(d.thoiGian));
            tr.appendChild(oGio(d.dangXuat));
            tr.appendChild(oNguoi(d.taiKhoan, d.hoTen, ''));
            tr.appendChild(o(d.mayTinh, 'nkbm-may'));
            tr.appendChild(oIp(d.ip));
            const tdTt = document.createElement('td');
            if (d.trangThai != null && d.trangThai !== '') {
                const span = document.createElement('span');
                span.className = 'nkbm-the-loai loai-trung';
                span.textContent = String(d.trangThai);
                tdTt.appendChild(span);
            }
            tr.appendChild(tdTt);
            tr.appendChild(oUa(d.trinhDuyet));
        }
        return tr;
    }

    async function layJson(url) {
        const res = await fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' });
        if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
        if (res.status === 403) throw new Error('Bạn không có quyền xem nhật ký bảo mật.');
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
        return res.json();
    }

    async function taiDuLieu() {
        if (dangTai) return;
        dangTai = true;
        const nutLoc = form.querySelector('button[type=submit]');
        nutLoc.disabled = true;
        hienLoi('');
        danhDauOTong();
        dongCho();
        try {
            const p = new URLSearchParams(new FormData(form));
            p.set('nguon', tab);
            p.set('trang', String(trang));
            if (!LOAI_THEO_TAB[tab]) p.delete('loai');
            const kq = await layJson('/NhatKyBaoMat/DuLieu?' + p.toString());
            if (!kq.thanhCong) {
                hienLoi(kq.thongBao || 'Không tải được nhật ký.');
                dongTrong('Không có dữ liệu.', 'fa-inbox');
                return;
            }
            const dl = kq.duLieu;
            soTrang = dl.soTrang;
            $('nkbmTongDong').textContent = `${dl.tong.toLocaleString('vi-VN')} dòng`;
            $('nkbmSoTrang').textContent = `${dl.trang} / ${dl.soTrang}`;
            $('nkbmTruoc').disabled = dl.trang <= 1;
            $('nkbmSau').disabled = dl.trang >= dl.soTrang;
            if (!dl.dong.length) {
                dongTrong('Không có sự kiện nào trong khoảng đã chọn.', 'fa-check-circle');
                return;
            }
            const tb = $('nkbmThanBang');
            tb.replaceChildren(...dl.dong.map(veDong));
        } catch (e) {
            hienLoi(e.message);
            dongTrong('Không tải được dữ liệu.', 'fa-plug');
        } finally {
            dangTai = false;
            nutLoc.disabled = false;
        }
    }

    function veDsIp(id, ds, tabLoc) {
        const ol = $(id);
        ol.replaceChildren();
        if (!ds || !ds.length) {
            const li = document.createElement('li');
            li.className = 'nkbm-trong';
            li.textContent = 'Không có.';
            ol.appendChild(li);
            return;
        }
        // Thanh nền theo tỷ lệ với IP nhiều nhất để nhìn ra chênh lệch ngay
        const max = Math.max(...ds.map((x) => x.soLan || 0), 1);
        ds.forEach((x, i) => {
            const li = document.createElement('li');
            li.className = 'nkbm-dong-ip';
            const thanh = document.createElement('span');
            thanh.className = 'nkbm-ip-thanh';
            thanh.style.width = Math.max(4, Math.round((x.soLan || 0) / max * 100)) + '%';
            const hang = document.createElement('span');
            hang.className = 'nkbm-ip-hang';
            hang.textContent = String(i + 1);
            const giua = document.createElement('span');
            giua.className = 'nkbm-ip-giua';
            const a = document.createElement('a');
            a.href = '#';
            a.textContent = x.ip;
            a.dataset.ip = x.ip;
            a.dataset.tab = tabLoc;
            a.className = 'nkbm-loc-ip';
            const so = document.createElement('span');
            so.className = 'nkbm-ip-so';
            so.textContent = `${x.soLan} lần`;
            const luc = document.createElement('span');
            luc.className = 'nkbm-ip-luc';
            luc.textContent = 'gần nhất ' + dinhDangGio(x.lanCuoi);
            giua.append(a, luc);
            li.append(thanh, hang, giua, so);
            ol.appendChild(li);
        });
    }

    async function taiTongQuan() {
        try {
            const kq = await layJson('/NhatKyBaoMat/TongQuan');
            if (!kq.thanhCong) { hienLoi(kq.thongBao); return; }
            const d = kq.duLieu;
            [['soChuaDangNhap', d.chuaDangNhap], ['soKhongDuQuyen', d.khongDuQuyen], ['soDuLieuNhayCam', d.duLieuNhayCam],
             ['soDangNhapLoi', d.dangNhapLoi], ['soDangNhap', d.dangNhap]].forEach(([id, so]) => {
                const el = $(id);
                el.textContent = Number(so || 0).toLocaleString('vi-VN');
                el.classList.toggle('co-so', Number(so) > 0);
            });
            const bayGio = new Date();
            const p2 = (n) => String(n).padStart(2, '0');
            $('nkbmCapNhat').lastElementChild.textContent =
                `Cập nhật ${p2(bayGio.getHours())}:${p2(bayGio.getMinutes())}:${p2(bayGio.getSeconds())}`;
            veDsIp('dsIpBiChan', d.ipBiChan, 'baomat');
            veDsIp('dsIpDangNhapLoi', d.ipDangNhapLoi, 'dangnhaploi');
        } catch (e) {
            hienLoi(e.message);
        }
    }

    function chuyenTab(tabMoi, loai, tuKhoa) {
        tab = tabMoi;
        trang = 1;
        document.querySelectorAll('.nkbm-tab-nut').forEach((b) => b.classList.toggle('dang-chon', b.dataset.tab === tab));
        veDauBang();
        veChonLoai(loai);
        if (tuKhoa !== undefined) form.elements.q.value = tuKhoa;
        taiDuLieu();
    }

    // Uỷ quyền sự kiện trên document — các phần tử danh sách IP được vẽ lại mỗi lần làm mới
    document.addEventListener('click', (e) => {
        const nutTab = e.target.closest('.nkbm-tab-nut');
        if (nutTab) { chuyenTab(nutTab.dataset.tab, '', ''); return; }

        const oTong = e.target.closest('.nkbm-o');
        if (oTong) { chuyenTab(oTong.dataset.tab, oTong.dataset.loai, ''); return; }

        const lienKetIp = e.target.closest('.nkbm-loc-ip');
        if (lienKetIp) {
            e.preventDefault();
            chuyenTab(lienKetIp.dataset.tab, '', lienKetIp.dataset.ip);
            return;
        }

        if (e.target.closest('#nkbmTruoc') && trang > 1) { trang--; taiDuLieu(); return; }
        if (e.target.closest('#nkbmSau') && trang < soTrang) { trang++; taiDuLieu(); return; }
        const nutLamMoi = e.target.closest('#nkbmLamMoi');
        if (nutLamMoi && !nutLamMoi.disabled) {
            nutLamMoi.disabled = true;
            nutLamMoi.classList.add('dang-quay');
            Promise.all([taiTongQuan(), taiDuLieu()]).finally(() => {
                nutLamMoi.disabled = false;
                nutLamMoi.classList.remove('dang-quay');
            });
        }
    });

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        trang = 1;
        taiDuLieu();
    });

    veDauBang();
    veChonLoai('');
    taiTongQuan();
    taiDuLieu();
})();
