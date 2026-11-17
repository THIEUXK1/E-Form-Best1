// Trang Nhật ký thao tác QA (/QAForm/LichSu) — chỉ quyền All (server chặn).
// Tab "Nhật ký thao tác": mọi thao tác trên thùng (QA_ThungLichSu), lọc ngày/người/hành động/từ khoá.
// Tab "Thùng đã xoá": xem dòng + nhật ký thùng đã xoá mềm, khôi phục khi xoá nhầm. Không reload.
(function () {
    var $ = function (id) { return document.getElementById(id); };
    var TEN_HANH_DONG = { TaoThung: 'Tạo thùng', ThemDong: 'Thêm dòng', DongThung: 'Đóng thùng', MoLai: 'Mở lại', XoaThung: 'Xoá thùng', ChuyenDi: 'Chuyển dòng đi', ChuyenDen: 'Nhận dòng chuyển đến', BoDong: 'Bỏ dòng ra', SuaSoBan: 'Sửa số paiban', XepKe: 'Xếp kệ', GoKe: 'Gỡ khỏi kệ', DoiTen: 'Đổi tên', SuaGhiChu: 'Sửa ghi chú', KhoiPhuc: 'Khôi phục thùng', ChoMuon: 'Cho mượn', TraMuon: 'Nhận trả' };
    var TEN_TRANG_THAI = { Mo: 'Đang mở', DaDong: 'Đã đóng' };
    var dsDaXoa = [];
    var lanNapNk = 0, lanNapDx = 0, henNk = null, henDx = null, daNapDx = false;

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }
    function so(n) { return (Number(n) || 0).toLocaleString('vi-VN', { maximumFractionDigits: 3 }); }
    function ngayGio(s) {
        if (!s) return '';
        var d = new Date(s);
        return isNaN(d) ? s : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }
    function ngayInput(d) {
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    function o(tr, giaTri, lop) {
        var td = document.createElement('td');
        td.textContent = giaTri === null || giaTri === undefined ? '' : giaTri;
        if (lop) td.className = lop;
        tr.appendChild(td);
        return td;
    }
    function nut(text, lop, hanhDong, id, bieuTuong) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm ' + lop;
        b.dataset.daxoa = hanhDong;
        b.dataset.id = id;
        var i = document.createElement('i');
        i.className = 'fa ' + bieuTuong;
        b.appendChild(i);
        b.appendChild(document.createTextNode(' ' + text));
        return b;
    }
    function thongBao(text, loai) {
        var el = $('qa-ls-thongbao');
        el.textContent = text || '';
        el.classList.toggle('loi', loai === 'loi');
        el.classList.toggle('ok', loai === 'ok');
        if (loai && window.qaThongBao) window.qaThongBao(text, loai);
    }
    async function layJson(url, tuyChon) {
        var res = await fetch(url, tuyChon || { headers: { 'Accept': 'application/json' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }
    function dongTrong(text, soCot) {
        var tr = document.createElement('tr');
        var td = o(tr, text, 'qa-mes-trong');
        td.colSpan = soCot;
        return tr;
    }
    function lopHanhDong(hd) { return hd === 'XoaThung' ? 'qa-ls-xoa' : hd === 'KhoiPhuc' ? 'qa-ls-khoiphuc' : ''; }

    // ===== TAB NHẬT KÝ =====
    async function napNhatKy() {
        var lan = ++lanNapNk;
        var p = new URLSearchParams();
        [['tuNgay', 'qa-nk-tu'], ['denNgay', 'qa-nk-den'], ['hanhDong', 'qa-nk-hd'], ['nguoi', 'qa-nk-nguoi'], ['tuKhoa', 'qa-nk-tim']].forEach(function (x) {
            var v = $(x[1]).value.trim();
            if (v) p.set(x[0], v);
        });
        var tbody = $('qa-nk-tbody');
        try {
            var kq = await layJson('/QAForm/LichSu/DuLieu?' + p.toString());
            if (lan !== lanNapNk) return;
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            var ds = kq.duLieu || [];
            $('qa-nk-tom').textContent = ds.length + ' thao tác' + (ds.length >= 1000 ? ' (1000 mới nhất — thu hẹp bộ lọc để xem thêm)' : '');
            tbody.replaceChildren();
            if (ds.length === 0) { tbody.appendChild(dongTrong('Không có thao tác nào khớp bộ lọc.', 6)); return; }
            ds.forEach(function (l) {
                var tr = document.createElement('tr');
                var lop = lopHanhDong(l.hanhDong);
                if (lop) tr.className = lop;
                o(tr, ngayGio(l.ngay));
                o(tr, l.nguoi);
                o(tr, TEN_HANH_DONG[l.hanhDong] || l.hanhDong);
                o(tr, l.maThung + (l.thungDaXoa ? ' (đã xoá)' : ''));
                o(tr, l.tenThung);
                o(tr, l.noiDung, 'qa-ls-noidung');
                tbody.appendChild(tr);
            });
        } catch (e) {
            if (lan !== lanNapNk) return;
            tbody.replaceChildren(dongTrong('Không tải được: ' + e.message, 6));
        }
    }

    // ===== TAB THÙNG ĐÃ XOÁ =====
    function veDaXoa() {
        var tbody = $('qa-daxoa-tbody');
        tbody.replaceChildren();
        $('qa-daxoa-tom').textContent = dsDaXoa.length ? dsDaXoa.length + ' thùng' + (dsDaXoa.length >= 500 ? ' (500 mới xoá nhất)' : '') : '';
        var dem = $('qa-daxoa-dem');
        if (!$('qa-daxoa-tim').value.trim()) { dem.textContent = dsDaXoa.length; dem.hidden = dsDaXoa.length === 0; }
        if (dsDaXoa.length === 0) { tbody.appendChild(dongTrong('Không có thùng đã xoá nào.', 11)); return; }
        dsDaXoa.forEach(function (t) {
            var tr = document.createElement('tr');
            o(tr, t.maThung);
            o(tr, t.tenThung);
            o(tr, TEN_TRANG_THAI[t.trangThai] || t.trangThai);
            o(tr, t.viTri);
            o(tr, so(t.soDong));
            o(tr, so(t.tongMet));
            o(tr, ngayGio(t.ngayTao));
            o(tr, t.nguoiTao);
            o(tr, ngayGio(t.ngayXoa));
            o(tr, t.nguoiXoa);
            var td = o(tr, null, 'qa-daxoa-nut');
            td.appendChild(nut('Chi tiết', 'btn-outline-secondary', 'xem', t.idThung, 'fa-list'));
            td.appendChild(nut('Khôi phục', 'btn-outline-success', 'khoiphuc', t.idThung, 'fa-undo'));
            tbody.appendChild(tr);
        });
    }

    async function napDaXoa() {
        var lan = ++lanNapDx;
        var tuKhoa = $('qa-daxoa-tim').value.trim();
        try {
            var kq = await layJson('/QAForm/Thung/DaXoa' + (tuKhoa ? '?tuKhoa=' + encodeURIComponent(tuKhoa) : ''));
            if (lan !== lanNapDx) return;
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            dsDaXoa = kq.duLieu || [];
            daNapDx = true;
            veDaXoa();
        } catch (e) {
            if (lan !== lanNapDx) return;
            $('qa-daxoa-tbody').replaceChildren(dongTrong('Không tải được: ' + e.message, 11));
        }
    }

    // "Chi tiết": mở/đóng dòng con ngay dưới thùng (nhật ký đầy đủ + dòng hàng lúc xoá)
    async function xemChiTiet(id, nutBam) {
        var tr = nutBam.closest('tr');
        var ke = tr.nextElementSibling;
        if (ke && ke.classList.contains('qa-daxoa-ct')) { ke.remove(); return; }
        nutBam.disabled = true;
        try {
            var kq = await layJson('/QAForm/Thung/DaXoa/' + id);
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            var trCt = document.createElement('tr');
            trCt.className = 'qa-daxoa-ct';
            var td = document.createElement('td');
            td.colSpan = 11;

            var h1 = document.createElement('h6');
            h1.textContent = 'Nhật ký thùng';
            var ulLs = document.createElement('ul');
            (kq.duLieu.lichSu || []).forEach(function (l) {
                var li = document.createElement('li');
                li.className = lopHanhDong(l.hanhDong);
                li.textContent = ngayGio(l.ngay) + ' · ' + (l.nguoi || '?') + ' · ' + (TEN_HANH_DONG[l.hanhDong] || l.hanhDong) + (l.noiDung ? ' — ' + l.noiDung : '');
                ulLs.appendChild(li);
            });
            if (!ulLs.children.length) { var liT = document.createElement('li'); liT.textContent = 'Chưa có nhật ký.'; ulLs.appendChild(liT); }

            var dong = kq.duLieu.dong || [];
            var h2 = document.createElement('h6');
            h2.textContent = 'Dòng trong thùng lúc xoá (' + dong.length + ')';
            var ulDong = document.createElement('ul');
            dong.forEach(function (d) {
                var li = document.createElement('li');
                li.textContent = [d.maPhoiMau, d.maHang, d.maMau].filter(Boolean).join(' · ')
                    + ' — ' + so(d.slDongGoi) + ' m' + (d.soHop ? ', ' + so(d.soHop) + ' hộp' : '')
                    + (d.soBan !== null && d.soBan !== undefined ? ', paiban ' + d.soBan : '')
                    + (d.soPhieuDongGoi ? ' · phiếu ' + d.soPhieuDongGoi : '')
                    + ' (bỏ vào ' + ngayGio(d.ngayThem) + (d.nguoiThem ? ' bởi ' + d.nguoiThem : '') + ')';
                ulDong.appendChild(li);
            });
            if (!dong.length) { var liD = document.createElement('li'); liD.textContent = 'Thùng rỗng.'; ulDong.appendChild(liD); }

            td.append(h1, ulLs, h2, ulDong);
            trCt.appendChild(td);
            tr.after(trCt);
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    function guiKhoiPhuc(id, boDongTrung) {
        return layJson('/QAForm/Thung/' + id + '/KhoiPhuc?boDongTrung=' + (boDongTrung ? 'true' : 'false'),
            { method: 'POST', headers: { 'Accept': 'application/json', 'RequestVerificationToken': token() } });
    }

    async function khoiPhuc(id, nutBam) {
        var t = dsDaXoa.find(function (x) { return x.idThung === id; });
        var ten = t ? t.maThung + (t.tenThung ? ' (' + t.tenThung + ')' : '') : '';
        if (!(await qaXacNhan('Khôi phục thùng ' + ten + '?\nThùng sẽ hiện lại trong Danh sách thùng với các dòng lúc bị xoá.', { nutDongY: 'Khôi phục' }))) return;
        nutBam.disabled = true;
        try {
            var kq = await guiKhoiPhuc(id, false);
            // Trong lúc bị xoá, dòng của thùng đã được bỏ vào thùng khác -> hỏi có bỏ các dòng trùng ra để khôi phục không
            if (!kq.thanhCong && kq.duLieu && kq.duLieu.coDongTrung) {
                var dongY = await qaXacNhan(kq.thongBao + '\n\nKhôi phục thùng và BỎ các dòng trùng ra (các dòng đó giữ nguyên ở thùng hiện tại)?',
                    { nutDongY: 'Khôi phục, bỏ dòng trùng', nguyHiem: true, tichXacNhan: 'Tôi hiểu các dòng trùng sẽ không quay lại thùng này' });
                if (!dongY) return;
                kq = await guiKhoiPhuc(id, true);
            }
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                await napDaXoa();
                napNhatKy();
            }
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    // ===== TAB + SỰ KIỆN =====
    function chonTab(ten) {
        document.querySelectorAll('[data-ls-tab]').forEach(function (b) { b.classList.toggle('active', b.dataset.lsTab === ten); });
        $('qa-ls-nhatky').hidden = ten !== 'nhatky';
        $('qa-ls-daxoa').hidden = ten !== 'daxoa';
        thongBao('');
        if (ten === 'daxoa' && !daNapDx) napDaXoa();
        try { localStorage.setItem('qa-ls-tab', ten); } catch (e) { /* bỏ qua */ }
    }

    document.addEventListener('click', function (e) {
        var tab = e.target.closest('[data-ls-tab]');
        if (tab) { chonTab(tab.dataset.lsTab); return; }
        var b = e.target.closest('[data-daxoa]');
        if (!b || b.disabled) return;
        var id = parseInt(b.dataset.id, 10);
        if (b.dataset.daxoa === 'xem') xemChiTiet(id, b);
        else if (b.dataset.daxoa === 'khoiphuc') khoiPhuc(id, b);
    });
    ['qa-nk-tu', 'qa-nk-den', 'qa-nk-hd'].forEach(function (id) { $(id).addEventListener('change', napNhatKy); });
    ['qa-nk-nguoi', 'qa-nk-tim'].forEach(function (id) {
        $(id).addEventListener('input', function () { clearTimeout(henNk); henNk = setTimeout(napNhatKy, 350); });
    });
    $('qa-daxoa-tim').addEventListener('input', function () { clearTimeout(henDx); henDx = setTimeout(napDaXoa, 350); });

    // Mặc định 30 ngày gần nhất; nạp số thùng đã xoá ngay để hiện số trên tab
    var homNay = new Date();
    $('qa-nk-den').value = ngayInput(homNay);
    $('qa-nk-tu').value = ngayInput(new Date(homNay.getFullYear(), homNay.getMonth(), homNay.getDate() - 30));
    napNhatKy();
    napDaXoa();
    var tabLuu = null;
    try { tabLuu = localStorage.getItem('qa-ls-tab'); } catch (e) { /* bỏ qua */ }
    if (tabLuu === 'daxoa') chonTab('daxoa');
})();
