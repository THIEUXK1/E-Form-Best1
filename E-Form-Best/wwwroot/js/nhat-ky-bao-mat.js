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

    function dongTrong(text) {
        const tb = $('nkbmThanBang');
        tb.replaceChildren();
        const tr = document.createElement('tr');
        const td = o(text, 'nkbm-trong');
        td.colSpan = COT[tab].length;
        tr.appendChild(td);
        tb.appendChild(tr);
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
            tr.appendChild(o(dinhDangGio(d.thoiGian), 'nkbm-gio'));
            tr.appendChild(oTheLoai(d.loai));
            tr.appendChild(o(d.taiKhoan ? `${d.taiKhoan}${d.hoTen ? ' · ' + d.hoTen : ''}` : '(chưa đăng nhập)'));
            tr.appendChild(o(d.phuongThuc));
            const td = o(d.duongDan, 'nkbm-duong-dan');
            if (d.moTa) {
                const nho = document.createElement('div');
                nho.className = 'nkbm-ua';
                nho.textContent = d.moTa;
                td.appendChild(nho);
            }
            tr.appendChild(td);
            tr.appendChild(o(d.ip));
            tr.appendChild(oUa(d.trinhDuyet));
        } else if (tab === 'dangnhaploi') {
            tr.appendChild(o(dinhDangGio(d.thoiGian), 'nkbm-gio'));
            tr.appendChild(oTheLoai(d.lyDo, 'loai-loi'));
            tr.appendChild(o(d.taiKhoan));
            tr.appendChild(o(d.noiGoi));
            tr.appendChild(o(d.ip));
            tr.appendChild(oUa(d.trinhDuyet));
        } else {
            tr.appendChild(o(dinhDangGio(d.thoiGian), 'nkbm-gio'));
            tr.appendChild(o(dinhDangGio(d.dangXuat), 'nkbm-gio'));
            tr.appendChild(o(d.taiKhoan ? `${d.taiKhoan}${d.hoTen ? ' · ' + d.hoTen : ''}` : ''));
            tr.appendChild(o(d.mayTinh));
            tr.appendChild(o(d.ip));
            tr.appendChild(o(d.trangThai));
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
        dongTrong('Đang tải…');
        try {
            const p = new URLSearchParams(new FormData(form));
            p.set('nguon', tab);
            p.set('trang', String(trang));
            if (!LOAI_THEO_TAB[tab]) p.delete('loai');
            const kq = await layJson('/NhatKyBaoMat/DuLieu?' + p.toString());
            if (!kq.thanhCong) {
                hienLoi(kq.thongBao || 'Không tải được nhật ký.');
                dongTrong('Không có dữ liệu.');
                return;
            }
            const dl = kq.duLieu;
            soTrang = dl.soTrang;
            $('nkbmTongDong').textContent = `${dl.tong.toLocaleString('vi-VN')} dòng`;
            $('nkbmSoTrang').textContent = `${dl.trang} / ${dl.soTrang}`;
            $('nkbmTruoc').disabled = dl.trang <= 1;
            $('nkbmSau').disabled = dl.trang >= dl.soTrang;
            if (!dl.dong.length) {
                dongTrong('Không có sự kiện nào trong khoảng đã chọn.');
                return;
            }
            const tb = $('nkbmThanBang');
            tb.replaceChildren(...dl.dong.map(veDong));
        } catch (e) {
            hienLoi(e.message);
            dongTrong('Không tải được dữ liệu.');
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
        ds.forEach((x) => {
            const li = document.createElement('li');
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
            li.append(a, so, luc);
            ol.appendChild(li);
        });
    }

    async function taiTongQuan() {
        try {
            const kq = await layJson('/NhatKyBaoMat/TongQuan');
            if (!kq.thanhCong) { hienLoi(kq.thongBao); return; }
            const d = kq.duLieu;
            $('soChuaDangNhap').textContent = d.chuaDangNhap;
            $('soKhongDuQuyen').textContent = d.khongDuQuyen;
            $('soDuLieuNhayCam').textContent = d.duLieuNhayCam;
            $('soDangNhapLoi').textContent = d.dangNhapLoi;
            $('soDangNhap').textContent = d.dangNhap;
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
        if (e.target.closest('#nkbmLamMoi')) { taiTongQuan(); taiDuLieu(); }
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
