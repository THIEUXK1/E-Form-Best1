// Trang Danh sách thùng QA (/QAForm/Thung): danh sách, xem chi tiết dòng + nhật ký, đóng/mở lại, xoá mềm, xếp thùng lên kệ. Không reload.
// Kệ + sơ đồ ô ở qa-ke.js (nạp trước file này).
(function () {
    var $ = function (id) { return document.getElementById(id); };
    var dsThung = [];
    var idDangXem = null;
    var thungDangXem = null;
    var daChon = new Set();   // id thùng đang tích ở danh sách (giữ qua lọc / nạp lại)
    // Từ khoá tìm (mã thùng / mã phối màu...): lọc ở server vì phải dò hàng bên trong thùng; ?tim= trên URL để gửi link
    var tuKhoa = (new URLSearchParams(location.search).get('tim') || '').trim();
    var lanNap = 0, henTim = null;
    var TEN_TRANG_THAI = { Mo: 'Đang mở', DaDong: 'Đã đóng' };
    var TEN_HANH_DONG = { TaoThung: 'Tạo thùng', ThemDong: 'Thêm dòng', DongThung: 'Đóng thùng', MoLai: 'Mở lại', XoaThung: 'Xoá thùng', ChuyenDi: 'Chuyển dòng đi', ChuyenDen: 'Nhận dòng chuyển đến', BoDong: 'Bỏ dòng ra', SuaSoBan: 'Sửa số paiban', XepKe: 'Xếp kệ', GoKe: 'Gỡ khỏi kệ' };

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function thongBao(text, loai) {
        var el = $('qa-thung-thongbao');
        el.textContent = text || '';
        el.classList.toggle('loi', loai === 'loi');
        el.classList.toggle('ok', loai === 'ok');
        // Kết quả (thành công / lỗi) hiện thêm giữa màn hình cho dễ thấy; dòng chữ tại chỗ vẫn giữ
        if (loai && window.qaThongBao) window.qaThongBao(text, loai);
    }

    function so(n, le) { return (Number(n) || 0).toLocaleString('vi-VN', { maximumFractionDigits: le === undefined ? 3 : le }); }
    function ngayGio(s) {
        if (!s) return '';
        var d = new Date(s);
        return isNaN(d) ? s : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
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
        b.dataset.hanhDong = hanhDong;
        b.dataset.id = id;
        var i = document.createElement('i');
        i.className = 'fa ' + bieuTuong;
        b.appendChild(i);
        b.appendChild(document.createTextNode(' ' + text));
        return b;
    }

    async function layJson(url, tuyChon) {
        var res = await fetch(url, tuyChon || { headers: { 'Accept': 'application/json' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    function veDanhSach() {
        var loc = $('qa-thung-loc').value;
        var locKe = $('qa-thung-loc-ke').value;
        var ds = dsThung.filter(function (t) {
            if (loc && t.trangThai !== loc) return false;
            if (locKe === '0') return !t.idKe;
            return !locKe || t.idKe === parseInt(locKe, 10);
        });
        var tbody = $('qa-thung-tbody');
        if (ds.length === 0) {
            var tr = document.createElement('tr');
            var td = o(tr, tuKhoa && dsThung.length === 0 ? 'Không có thùng nào chứa "' + tuKhoa + '".'
                : dsThung.length === 0 ? 'Chưa có thùng nào. Vào Tra cứu mã phối màu, tích dòng rồi bấm Tạo thùng mới.' : 'Không có thùng khớp bộ lọc.', 'qa-mes-trong');
            td.colSpan = 12;
            tbody.replaceChildren(tr);
        } else {
            var frag = document.createDocumentFragment();
            ds.forEach(function (t) {
                var tr = document.createElement('tr');
                tr.dataset.id = t.idThung;
                if (t.idThung === idDangXem) tr.classList.add('qa-mes-dang-chon');
                if (daChon.has(t.idThung)) tr.classList.add('qa-thung-tich');
                var tdChon = document.createElement('td');
                tdChon.className = 'qa-mes-chon';
                var cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.className = 'form-check-input qa-thung-chon';
                cb.checked = daChon.has(t.idThung);
                tdChon.appendChild(cb);
                tr.appendChild(tdChon);
                var tdMa = o(tr, t.maThung, 'qa-mes-o-thung');
                tdMa.title = 'Bấm để xem chi tiết';
                if (t.khop) {
                    var khop = document.createElement('div');
                    khop.className = 'qa-thung-khop';
                    khop.textContent = 'Có: ' + t.khop + ' (' + t.soDongKhop + ' dòng)';
                    tdMa.appendChild(khop);
                }
                var tdTt = o(tr, TEN_TRANG_THAI[t.trangThai] || t.trangThai);
                tdTt.className = t.trangThai === 'Mo' ? 'qa-tt-mo' : 'qa-tt-dong';
                o(tr, t.viTri || '—', t.viTri ? 'qa-thung-vitri' : 'qa-thung-vitri trong');
                o(tr, so(t.soDong, 0), 'so');
                o(tr, so(t.tongMet), 'so');
                o(tr, so(t.tongKg), 'so');
                o(tr, so(t.tongHop, 0), 'so');
                o(tr, ngayGio(t.ngayTao));
                o(tr, t.nguoiTao);
                o(tr, t.ghiChu);
                var tdNut = document.createElement('td');
                tdNut.className = 'qa-thung-nut';
                tdNut.appendChild(nut('In tem', 'btn-outline-primary', 'in', t.idThung, 'fa-print'));
                tdNut.appendChild(t.trangThai === 'Mo'
                    ? nut('Đóng', 'btn-outline-secondary', 'dong', t.idThung, 'fa-lock')
                    : nut('Mở lại', 'btn-outline-success', 'mo', t.idThung, 'fa-unlock'));
                tdNut.appendChild(nut('Xoá', 'btn-outline-danger', 'xoa', t.idThung, 'fa-trash'));
                tr.appendChild(tdNut);
                frag.appendChild(tr);
            });
            tbody.replaceChildren(frag);
        }
        var mo = dsThung.filter(function (t) { return t.trangThai === 'Mo'; }).length;
        $('qa-thung-tom').textContent = (tuKhoa ? dsThung.length + ' thùng chứa "' + tuKhoa + '"' : dsThung.length + ' thùng') + ' (' + mo + ' đang mở)';
        veThanhXep(ds);
    }

    async function napDanhSach() {
        // Gõ nhanh nhiều lần: chỉ nhận kết quả của lần gọi cuối, lần cũ về sau thì bỏ
        var lan = ++lanNap;
        var k = tuKhoa;
        try {
            var kq = await layJson('/QAForm/Thung/DanhSach' + (k ? '?tuKhoa=' + encodeURIComponent(k) : ''));
            if (lan !== lanNap) return;
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            dsThung = kq.duLieu || [];
            // Thùng đã bị xoá thì bỏ khỏi danh sách đang tích
            var conId = new Set(dsThung.map(function (t) { return t.idThung; }));
            daChon.forEach(function (id) { if (!conId.has(id)) daChon.delete(id); });
            veDanhSach();
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        }
    }

    async function xemChiTiet(id, khongCuon) {
        idDangXem = id;
        veDanhSach();
        var khung = $('qa-thung-chitiet');
        try {
            var kq = await layJson('/QAForm/Thung/ChiTiet/' + id);
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); khung.hidden = true; return; }
            var t = kq.duLieu.thung, dong = kq.duLieu.dong, lichSu = kq.duLieu.lichSu;
            thungDangXem = t;
            veViTri();
            var tongM = 0, tongKg = 0;
            dong.forEach(function (d) { tongM += Number(d.slDongGoi) || 0; tongKg += Number(d.klDongGoi) || 0; });
            $('qa-ct-tieude').textContent = 'Thùng ' + t.maThung + ' · ' + (TEN_TRANG_THAI[t.trangThai] || t.trangThai)
                + (t.viTri ? ' · Kệ ' + t.viTri : '')
                + ' · ' + dong.length + ' dòng · ' + so(tongM) + ' m · ' + so(tongKg) + ' kg'
                + (t.ghiChu ? ' · ' + t.ghiChu : '');
            var tongBan = $('qa-ct-tongban');
            tongBan.textContent = '';

            var frag = document.createDocumentFragment();
            if (dong.length === 0) {
                var trTrong = document.createElement('tr');
                o(trTrong, 'Thùng chưa có dòng nào.', 'qa-mes-trong').colSpan = 19;
                frag.appendChild(trTrong);
            }
            dong.forEach(function (d, i) {
                var tr = document.createElement('tr');
                if (d.loai && d.loai !== 'A') tr.classList.add('qa-mes-bat-thuong');
                tr.dataset.idDong = d.idDong;
                var tdChon = document.createElement('td');
                tdChon.className = 'qa-mes-chon';
                var cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.className = 'form-check-input qa-ct-chon';
                cb.dataset.idDong = d.idDong;
                // Thùng đã đóng: không bỏ dòng ra được (server cũng chặn), phải mở lại trước
                cb.disabled = t.trangThai !== 'Mo';
                if (cb.disabled) cb.title = 'Thùng đã đóng — bấm Mở lại ở danh sách thùng để bỏ dòng ra';
                tdChon.appendChild(cb);
                tr.appendChild(tdChon);
                o(tr, i + 1, 'so');
                o(tr, d.maPhoiMau);
                o(tr, d.maKhachCuoi, 'qa-cot-phu');
                o(tr, d.maKhachHang);
                o(tr, d.maHang);
                o(tr, d.maMau);
                o(tr, d.slDonHang, 'so');
                o(tr, d.slDongGoi, 'so');
                o(tr, d.klDongGoi, 'so qa-cot-phu');
                o(tr, d.soHop, 'so');
                // Số bản nhập tay, lưu ngay khi rời ô / Enter
                var tdBan = document.createElement('td');
                tdBan.className = 'qa-ct-soban';
                var oBan = document.createElement('input');
                oBan.type = 'number';
                oBan.min = 0;
                oBan.max = 1000000;
                oBan.step = 1;
                oBan.inputMode = 'numeric';
                oBan.className = 'form-control form-control-sm qa-ct-soban-o';
                oBan.dataset.idDong = d.idDong;
                oBan.value = d.soBan === null || d.soBan === undefined ? '' : d.soBan;
                oBan.dataset.cu = oBan.value;
                tdBan.appendChild(oBan);
                tr.appendChild(tdBan);
                o(tr, d.loai);
                o(tr, d.moTaBatThuong, 'qa-cot-phu');
                o(tr, d.soPhieuNhuom, 'qa-cot-phu');
                o(tr, d.ngayDongGoi ? String(d.ngayDongGoi).substring(0, 10) : '');
                o(tr, d.soPhieuDongGoi, 'qa-cot-phu');
                o(tr, d.maNvDongGoi);
                o(tr, ngayGio(d.ngayThem));
                o(tr, d.nguoiThem);
                frag.appendChild(tr);
            });
            $('qa-ct-tbody').replaceChildren(frag);
            capNhatTongBan();
            var het = $('qa-ct-chon-het');
            het.checked = false;
            het.disabled = t.trangThai !== 'Mo' || dong.length === 0;
            het.title = t.trangThai !== 'Mo' ? 'Thùng đã đóng — mở lại để bỏ dòng ra' : 'Chọn tất cả dòng';
            veThanhBo();

            var ul = document.createDocumentFragment();
            lichSu.forEach(function (l) {
                var li = document.createElement('li');
                li.textContent = ngayGio(l.ngay) + ' · ' + (l.nguoi || '?') + ' · ' + (TEN_HANH_DONG[l.hanhDong] || l.hanhDong) + (l.noiDung ? ' — ' + l.noiDung : '');
                ul.appendChild(li);
            });
            $('qa-ct-lichsu').replaceChildren(ul);
            khung.hidden = false;
            if (!khongCuon) khung.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        }
    }

    // ===== Tích dòng trong chi tiết để bỏ ra khỏi thùng =====

    function dsDongTich() { return Array.from(document.querySelectorAll('#qa-ct-tbody .qa-ct-chon:checked')); }

    function veThanhBo() {
        var tich = dsDongTich();
        var tat = document.querySelectorAll('#qa-ct-tbody .qa-ct-chon:not(:disabled)');
        $('qa-ct-bo-thanh').hidden = tich.length === 0;
        $('qa-ct-bo-so').textContent = tich.length;
        var het = $('qa-ct-chon-het');
        het.checked = tat.length > 0 && tich.length === tat.length;
        het.indeterminate = tich.length > 0 && tich.length < tat.length;
        document.querySelectorAll('#qa-ct-tbody tr[data-id-dong]').forEach(function (tr) {
            var c = tr.querySelector('.qa-ct-chon');
            tr.classList.toggle('qa-ct-dong-tich', !!(c && c.checked));
        });
    }

    async function boDongRa(nutBam) {
        var ds = dsDongTich().map(function (c) { return parseInt(c.dataset.idDong, 10); });
        if (ds.length === 0 || !idDangXem) return;
        var ma = thungDangXem ? thungDangXem.maThung : '';
        if (!(await qaXacNhan('Bỏ ' + ds.length + ' dòng ra khỏi thùng ' + ma + '? Các dòng này sẽ được trả lại để bỏ vào thùng khác.'))) return;
        var id = idDangXem;
        nutBam.disabled = true;
        try {
            var kq = await layJson('/QAForm/Thung/BoRa', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                body: JSON.stringify({ dsIdDong: ds })
            });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                await napDanhSach();
                if (idDangXem === id) await xemChiTiet(id, true);
                if (window.qaKe) window.qaKe.napLai();
            }
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    function capNhatTongBan() {
        var tong = 0, coNhap = 0;
        document.querySelectorAll('#qa-ct-tbody .qa-ct-soban-o').forEach(function (x) {
            if (x.dataset.cu !== '') { tong += parseInt(x.dataset.cu, 10) || 0; coNhap++; }
        });
        $('qa-ct-tongban').textContent = coNhap ? 'Tổng số paiban: ' + so(tong, 0) + ' (' + coNhap + ' dòng đã nhập)' : '';
    }

    async function luuSoBan(oBan) {
        var giaTri = oBan.value.trim();
        if (giaTri === oBan.dataset.cu) return;
        var soBan = giaTri === '' ? null : Number(giaTri);
        if (soBan !== null && (!Number.isInteger(soBan) || soBan < 0 || soBan > 1000000)) {
            oBan.classList.add('loi');
            oBan.title = 'Số paiban là số nguyên từ 0 đến 1.000.000';
            thongBao('Số paiban là số nguyên từ 0 đến 1.000.000.', 'loi');
            return;
        }
        oBan.classList.remove('loi', 'ok');
        oBan.classList.add('dang-luu');
        try {
            var kq = await layJson('/QAForm/Thung/Dong/' + oBan.dataset.idDong + '/SoBan', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                body: JSON.stringify({ soBan: soBan })
            });
            oBan.classList.remove('dang-luu');
            if (kq.thanhCong) {
                oBan.dataset.cu = giaTri;
                oBan.classList.add('ok');
                oBan.title = 'Đã lưu';
                capNhatTongBan();
            } else {
                // Giữ số vừa gõ để người dùng sửa lại, không trả về số cũ
                oBan.classList.add('loi');
                oBan.title = kq.thongBao;
                thongBao(kq.thongBao, 'loi');
            }
        } catch (e) {
            oBan.classList.remove('dang-luu');
            oBan.classList.add('loi');
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        }
    }

    // Luôn lấy chi tiết mới từ máy chủ để tem khớp đúng các dòng đang có trong thùng
    async function inTem(id, nutBam) {
        if (!window.qaThungTem) { thongBao('Chưa tải được thư viện in tem, thử tải lại trang.', 'loi'); return; }
        if (nutBam) nutBam.disabled = true;
        try {
            var kq = await layJson('/QAForm/Thung/ChiTiet/' + id);
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            if (typeof window.JsBarcode !== 'function') thongBao('Không tải được thư viện mã vạch, tem in sẽ thiếu mã vạch.', 'loi');
            window.qaThungTem.inTem(kq.duLieu.thung, kq.duLieu.dong);
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            if (nutBam) nutBam.disabled = false;
        }
    }

    async function thaoTac(nutBam) {
        var id = parseInt(nutBam.dataset.id, 10);
        var hd = nutBam.dataset.hanhDong;
        if (hd === 'in') { inTem(id, nutBam); return; }
        var t = dsThung.find(function (x) { return x.idThung === id; });
        if (hd === 'xoa' && !(await qaXacNhan('Xoá thùng ' + (t ? t.maThung : '') + '? Các dòng trong thùng sẽ được trả lại để bỏ vào thùng khác.'))) return;
        var url = hd === 'xoa' ? '/QAForm/Thung/' + id + '/Xoa' : '/QAForm/Thung/' + id + '/TrangThai?dong=' + (hd === 'dong');
        nutBam.disabled = true;
        try {
            var kq = await layJson(url, { method: 'POST', headers: { 'Accept': 'application/json', 'RequestVerificationToken': token() } });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                if (hd === 'xoa' && idDangXem === id) { idDangXem = null; thungDangXem = null; $('qa-thung-chitiet').hidden = true; }
                if (hd === 'xoa' && window.qaKe) window.qaKe.napLai();
                await napDanhSach();
                if (idDangXem === id) await xemChiTiet(id);
            }
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    document.addEventListener('click', function (e) {
        var nutBam = e.target.closest('#qa-thung-tbody button[data-hanh-dong]');
        if (nutBam) { thaoTac(nutBam); return; }
        var nutIn = e.target.closest('#qa-ct-in');
        if (nutIn) { if (idDangXem) inTem(idDangXem, nutIn); return; }
        if (e.target.closest('#qa-ct-dong')) { idDangXem = null; thungDangXem = null; $('qa-thung-chitiet').hidden = true; veDanhSach(); return; }
        if (e.target.closest('#qa-xep-luu')) { xepNhieu(e.target.closest('button'), false); return; }
        if (e.target.closest('#qa-xep-go')) { xepNhieu(e.target.closest('button'), true); return; }
        if (e.target.closest('#qa-xep-bo')) { daChon.clear(); veDanhSach(); return; }
        // Bấm ô tích (hoặc vùng ô tích) không mở chi tiết
        var oTich = e.target.closest('#qa-thung-tbody td.qa-mes-chon');
        if (oTich) {
            var trTich = oTich.closest('tr[data-id]');
            if (e.target.type !== 'checkbox') { var c = oTich.querySelector('input'); c.checked = !c.checked; }
            tichThung(parseInt(trTich.dataset.id, 10), oTich.querySelector('input').checked);
            return;
        }
        var nutBoRa = e.target.closest('#qa-ct-bo-ra');
        if (nutBoRa) { boDongRa(nutBoRa); return; }
        if (e.target.closest('#qa-ct-bo-huy')) {
            document.querySelectorAll('#qa-ct-tbody .qa-ct-chon').forEach(function (c) { c.checked = false; });
            veThanhBo();
            return;
        }
        // Bấm vào dòng chi tiết (ngoài ô số bản / ô tích) = tích / bỏ tích dòng đó
        var trCt = e.target.closest('#qa-ct-tbody tr[data-id-dong]');
        if (trCt && !e.target.closest('input')) {
            var cbCt = trCt.querySelector('.qa-ct-chon');
            if (cbCt && !cbCt.disabled) { cbCt.checked = !cbCt.checked; veThanhBo(); }
            return;
        }
        var nutXep = e.target.closest('#qa-ct-xep');
        if (nutXep) { xepKe(nutXep, false); return; }
        var nutGo = e.target.closest('#qa-ct-go');
        if (nutGo) { xepKe(nutGo, true); return; }
        var tr = e.target.closest('#qa-thung-tbody tr[data-id]');
        if (tr) xemChiTiet(parseInt(tr.dataset.id, 10));
    });

    // Ô tìm: chờ ngừng gõ 350 ms mới gọi server
    document.addEventListener('input', function (e) {
        if (e.target.id !== 'qa-thung-tim') return;
        clearTimeout(henTim);
        henTim = setTimeout(function () {
            var moi = e.target.value.trim();
            if (moi === tuKhoa) return;
            tuKhoa = moi;
            napDanhSach();
        }, 350);
    });

    document.addEventListener('change', function (e) {
        if (e.target.id === 'qa-thung-loc' || e.target.id === 'qa-thung-loc-ke') veDanhSach();
        if (e.target.classList.contains('qa-ct-soban-o')) luuSoBan(e.target);
        if (e.target.classList.contains('qa-ct-chon')) veThanhBo();
        if (e.target.id === 'qa-ct-chon-het') {
            var chon = e.target.checked;
            document.querySelectorAll('#qa-ct-tbody .qa-ct-chon:not(:disabled)').forEach(function (c) { c.checked = chon; });
            veThanhBo();
        }
        if (e.target.id === 'qa-ct-ke') veTangO(parseInt(e.target.value, 10), null, null);
        if (e.target.id === 'qa-xep-ke') veTangO(parseInt(e.target.value, 10), null, null, 'qa-xep-');
        if (e.target.id === 'qa-thung-chon-het') {
            var chon = e.target.checked;
            dsDangHien().forEach(function (t) { if (chon) daChon.add(t.idThung); else daChon.delete(t.idThung); });
            veDanhSach();
        }
    });

    // ===== Vị trí thùng trên kệ =====

    function dsKe() { return window.qaKe ? window.qaKe.ds() : []; }
    function timKe(idKe) { return dsKe().find(function (k) { return k.idKe === idKe; }); }

    function luaChon(sel, giaTri, text) {
        var op = document.createElement('option');
        op.value = giaTri;
        op.textContent = text;
        sel.appendChild(op);
    }

    // Ô chọn tầng/ô theo kích thước kệ đang chọn; tầng liệt kê từ trên xuống như sơ đồ.
    // Kệ 1 tầng (kệ cha A): ẩn chọn tầng, ô ghi A1…A6
    function veTangO(idKe, tang, oChon, tienTo) {
        tienTo = tienTo || 'qa-ct-';
        var ke = timKe(idKe);
        var selTang = $(tienTo + 'tang'), selO = $(tienTo + 'o');
        selTang.replaceChildren();
        selO.replaceChildren();
        selTang.disabled = selO.disabled = !ke;
        selTang.hidden = !!ke && ke.soTang === 1;
        if (!ke) { luaChon(selTang, '', 'Tầng'); luaChon(selO, '', 'Ô'); return; }
        for (var i = ke.soTang; i >= 1; i--) luaChon(selTang, i, 'Tầng ' + i);
        for (var j = 1; j <= ke.soO; j++) luaChon(selO, j, ke.soTang === 1 ? ke.maKe + j : 'Ô ' + j);
        selTang.value = tang && tang <= ke.soTang ? tang : 1;
        selO.value = oChon && oChon <= ke.soO ? oChon : 1;
    }

    function napOChonKe(sel, giaTri) {
        sel.replaceChildren();
        luaChon(sel, '', dsKe().length ? '-- Chọn kệ --' : '(chưa có kệ — bấm nút Kệ để thêm)');
        dsKe().forEach(function (k) { luaChon(sel, k.idKe, k.maKe + (k.tenKe ? ' · ' + k.tenKe : '') + (k.khuVuc ? ' (' + k.khuVuc + ')' : '')); });
        sel.value = giaTri || '';
    }

    function dsDangHien() {
        var loc = $('qa-thung-loc').value, locKe = $('qa-thung-loc-ke').value;
        return dsThung.filter(function (t) {
            if (loc && t.trangThai !== loc) return false;
            if (locKe === '0') return !t.idKe;
            return !locKe || t.idKe === parseInt(locKe, 10);
        });
    }

    function tichThung(id, chon) {
        if (chon) daChon.add(id); else daChon.delete(id);
        var tr = document.querySelector('#qa-thung-tbody tr[data-id="' + id + '"]');
        if (tr) tr.classList.toggle('qa-thung-tich', chon);
        veThanhXep(dsDangHien());
    }

    // Thanh xếp kệ hiện khi có thùng được tích; ô "chọn tất cả" theo các thùng đang hiện
    function veThanhXep(dsHien) {
        var thanh = $('qa-xep-thanh');
        var dangAn = thanh.hidden;
        thanh.hidden = daChon.size === 0;
        $('qa-xep-so').textContent = daChon.size;
        var soHienChon = dsHien.filter(function (t) { return daChon.has(t.idThung); }).length;
        var het = $('qa-thung-chon-het');
        het.checked = dsHien.length > 0 && soHienChon === dsHien.length;
        het.indeterminate = soHienChon > 0 && soHienChon < dsHien.length;
        if (dangAn && !thanh.hidden) {
            // Mở thanh: nạp danh sách kệ, giữ kệ đã chọn lần trước
            var selKe = $('qa-xep-ke'), cu = parseInt(selKe.value, 10);
            napOChonKe(selKe, timKe(cu) ? cu : null);
            veTangO(parseInt(selKe.value, 10), null, null, 'qa-xep-');
            selKe.focus();
        }
    }

    // Gán theo kệ + ô đã chọn (ghép thành mã vị trí A-A1 / A-2-3); máy chủ tách lại và kiểm kệ có tồn tại
    async function xepNhieu(nutBam, go) {
        var ds = Array.from(daChon);
        if (ds.length === 0) return;
        var keChon = timKe(parseInt($('qa-xep-ke').value, 10));
        if (!go && !keChon) { thongBao('Chọn kệ và ô rồi bấm Gán.', 'loi'); $('qa-xep-ke').focus(); return; }
        var viTri = go ? '' : window.qaKe.maViTri(keChon, parseInt($('qa-xep-tang').value, 10) || 1, parseInt($('qa-xep-o').value, 10) || 1);
        if (go && !(await qaXacNhan('Gỡ ' + ds.length + ' thùng đã chọn khỏi kệ?'))) return;
        nutBam.disabled = true;
        try {
            var kq = await layJson('/QAForm/Thung/XepKe', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                body: JSON.stringify(go ? { dsIdThung: ds } : { dsIdThung: ds, viTri: viTri })
            });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                daChon.clear();
                await napDanhSach();
                if (idDangXem && ds.indexOf(idDangXem) >= 0) await xemChiTiet(idDangXem, true);
                if (window.qaKe) window.qaKe.napLai();
            }
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    function veViTri() {
        var t = thungDangXem;
        if (!t) return;
        $('qa-ct-vitri-hien').textContent = t.viTri || 'chưa xếp kệ';
        $('qa-ct-go').hidden = !t.idKe;
        napOChonKe($('qa-ct-ke'), t.idKe);
        veTangO(t.idKe, t.tang, t.o);
    }

    async function xepKe(nutBam, go) {
        if (!idDangXem) return;
        var idKe = parseInt($('qa-ct-ke').value, 10);
        if (!go && isNaN(idKe)) { thongBao('Chọn kệ, tầng, ô rồi bấm Xếp vào kệ.', 'loi'); return; }
        if (go && !(await qaXacNhan('Gỡ thùng ' + (thungDangXem ? thungDangXem.maThung : '') + ' khỏi kệ?'))) return;
        var id = idDangXem;
        nutBam.disabled = true;
        try {
            var kq = await layJson('/QAForm/Thung/' + id + '/ViTri', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                body: JSON.stringify(go ? { idKe: null } : { idKe: idKe, tang: parseInt($('qa-ct-tang').value, 10), o: parseInt($('qa-ct-o').value, 10) })
            });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                await napDanhSach();
                if (idDangXem === id) await xemChiTiet(id, true);
                if (window.qaKe) window.qaKe.napLai();
            }
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    document.addEventListener('keydown', function (e) {
        // Enter ở ô số bản: lưu (qua sự kiện change) rồi nhảy xuống dòng dưới để nhập liên tục
        if (e.key === 'Enter' && e.target.classList.contains('qa-ct-soban-o')) {
            e.preventDefault();
            var tat = Array.from(document.querySelectorAll('#qa-ct-tbody .qa-ct-soban-o'));
            var sau = tat[tat.indexOf(e.target) + 1];
            if (sau) { sau.focus(); sau.select(); } else e.target.blur();
        }
    });

    // Danh sách kệ đổi -> cập nhật ô lọc kệ + ô chọn kệ ở chi tiết
    document.addEventListener('qa-ke:doi', function (e) {
        var sel = $('qa-thung-loc-ke');
        var cu = sel.value;
        while (sel.options.length > 2) sel.remove(2);
        (e.detail || []).forEach(function (k) { luaChon(sel, k.idKe, 'Kệ ' + k.maKe); });
        sel.value = cu;
        if (sel.value !== cu) { sel.value = ''; veDanhSach(); }
        veViTri();
    });

    // Bấm ô trên sơ đồ khi đang xem 1 thùng -> điền sẵn kệ/tầng/ô, người dùng bấm Xếp vào kệ để lưu
    document.addEventListener('qa-ke:chon-o', function (e) {
        var d = e.detail;
        var ke = timKe(d.idKe);
        if (ke && daChon.size > 0) {
            var vt = window.qaKe.maViTri(ke, d.tang, d.o);
            $('qa-xep-ke').value = d.idKe;
            veTangO(d.idKe, d.tang, d.o, 'qa-xep-');
            $('qa-xep-thanh').scrollIntoView({ behavior: 'smooth', block: 'center' });
            $('qa-xep-luu').focus();
            thongBao('Đã điền vị trí ' + vt + ' cho ' + daChon.size + ' thùng đã tích — bấm Gán để lưu.', 'ok');
            return;
        }
        if (!idDangXem || !ke) { thongBao('Bấm 1 thùng trong danh sách để xem chi tiết trước, rồi bấm ô trên sơ đồ để chọn vị trí cho thùng đó.', 'loi'); return; }
        $('qa-ct-ke').value = d.idKe;
        veTangO(d.idKe, d.tang, d.o);
        var nutXep = $('qa-ct-xep');
        nutXep.scrollIntoView({ behavior: 'smooth', block: 'center' });
        nutXep.focus();
        thongBao('Đã chọn ô ' + window.qaKe.maViTri(ke, d.tang, d.o) + ' cho thùng ' + (thungDangXem ? thungDangXem.maThung : '') + ' — bấm Xếp vào kệ để lưu.', 'ok');
    });

    document.addEventListener('qa-ke:xem-thung', function (e) { xemChiTiet(e.detail.idThung); });

    // Xem trước trong hộp tùy chỉnh: dùng thùng đang mở chi tiết, không có thì thùng đầu danh sách
    if (window.qaThungTem) {
        window.qaThungTem.khoiTao(async function () {
            var id = idDangXem || (dsThung[0] && dsThung[0].idThung);
            if (!id) return null;
            var kq = await layJson('/QAForm/Thung/ChiTiet/' + id);
            return kq.thanhCong ? { thung: kq.duLieu.thung, dong: kq.duLieu.dong } : null;
        });
    }
    // ===== Tích bằng kéo chuột (bôi đen) — giống trang Tra cứu =====
    // Nhấn giữ rồi kéo qua các dòng: mọi dòng đi qua nhận cùng trạng thái với dòng đầu sau khi đảo.
    // Shift + bấm: áp cho cả khoảng từ dòng bấm trước.
    //  - Danh sách thùng: bấm-thả tại chỗ trên dòng vẫn là mở chi tiết; nhấn giữ rồi rê sang dòng khác mới là bôi đen.
    //    Bấm ở cột ô tích thì tích ngay như trang Tra cứu.
    //  - Dòng trong chi tiết: bấm vào dòng vốn đã là tích, nên kéo từ bất kỳ đâu trên dòng (trừ ô nhập số bản).
    var bangKeo = [
        {
            tbody: 'qa-thung-tbody',
            batDau: function (e) { return !!e.target.closest('td.qa-mes-chon'); },
            // Ngoài cột ô tích: chỉ "chờ kéo", rê sang dòng khác mới bắt đầu bôi đen
            choKeo: function (e) { return !e.target.closest('button, a, input, select, textarea, .qa-thung-nut'); },
            oTich: function (tr) { return tr && tr.dataset.id ? tr.querySelector('.qa-thung-chon') : null; },
            ap: function (tr, cb, chon) { cb.checked = chon; tichThung(parseInt(tr.dataset.id, 10), chon); },
            xong: function () { }
        },
        {
            tbody: 'qa-ct-tbody',
            batDau: function (e) { return !e.target.closest('input:not(.qa-ct-chon), button, a, select, textarea'); },
            oTich: function (tr) { return tr ? tr.querySelector('.qa-ct-chon') : null; },
            ap: function (tr, cb, chon) { cb.checked = chon; },
            xong: function () { veThanhBo(); }
        }
    ];
    var keo = null;            // { bang, trangThai, dau, banDau } khi đang kéo
    var neo = null;            // { bang, chiSo, trangThai } dòng bấm gần nhất cho Shift + bấm
    var boQuaClick = false;    // mousedown đã tự tích -> chặn click ngay sau đó khỏi đảo/mở chi tiết lần nữa
    var choKeo = null;         // { bang, chiSo } nhấn giữ ở dòng danh sách thùng, chưa rê sang dòng khác

    function dsDongBang(bang) { return Array.prototype.slice.call($(bang.tbody).querySelectorAll('tr')); }

    function apKhoangBang(bang, a, b, trangThai, banDau) {
        var ds = dsDongBang(bang);
        ds.forEach(function (tr, i) {
            var cb = bang.oTich(tr);
            if (!cb || cb.disabled) return;
            var trong = i >= Math.min(a, b) && i <= Math.max(a, b);
            var muon = trong ? trangThai : (banDau ? banDau[i] : cb.checked);
            if (cb.checked !== muon) bang.ap(tr, cb, muon);
        });
        bang.xong();
    }

    function batDauKeo(bang, ds, chiSo, trangThai) {
        neo = { bang: bang, chiSo: chiSo, trangThai: trangThai };
        keo = { bang: bang, trangThai: trangThai, dau: chiSo, banDau: ds.map(function (r) { var c = bang.oTich(r); return c ? c.checked : false; }) };
        document.body.classList.add('qa-dang-keo');
    }

    document.addEventListener('mousedown', function (e) {
        boQuaClick = false;
        choKeo = null;
        if (e.button !== 0) return;
        var bang = null, tr = null;
        for (var k = 0; k < bangKeo.length; k++) {
            tr = e.target.closest('#' + bangKeo[k].tbody + ' tr');
            if (tr) { bang = bangKeo[k]; break; }
        }
        if (!bang) return;
        var cb = bang.oTich(tr);
        if (!cb || cb.disabled) return;
        if (!bang.batDau(e)) {
            if (bang.choKeo && bang.choKeo(e) && !e.shiftKey) {
                e.preventDefault(); // không bôi đen chữ nếu rê chuột
                choKeo = { bang: bang, chiSo: dsDongBang(bang).indexOf(tr) };
            }
            return;
        }
        e.preventDefault(); // không bôi đen chữ khi kéo
        boQuaClick = true;
        var ds = dsDongBang(bang);
        var chiSo = ds.indexOf(tr);
        if (e.shiftKey && neo && neo.bang === bang) {
            apKhoangBang(bang, neo.chiSo, chiSo, neo.trangThai, null);
            return;
        }
        var trangThai = !cb.checked;
        batDauKeo(bang, ds, chiSo, trangThai);
        bang.ap(tr, cb, trangThai);
        bang.xong();
    });

    document.addEventListener('mouseover', function (e) {
        // Đang "chờ kéo" ở danh sách thùng mà rê sang dòng khác -> bắt đầu bôi đen từ dòng nhấn
        if (choKeo && !keo) {
            var trMoi = e.target.closest('#' + choKeo.bang.tbody + ' tr');
            if (!trMoi) return;
            var dsCho = dsDongBang(choKeo.bang);
            var chiSoMoi = dsCho.indexOf(trMoi);
            if (chiSoMoi < 0 || chiSoMoi === choKeo.chiSo) return;
            var cbDau = choKeo.bang.oTich(dsCho[choKeo.chiSo]);
            if (!cbDau) { choKeo = null; return; }
            batDauKeo(choKeo.bang, dsCho, choKeo.chiSo, !cbDau.checked);
            boQuaClick = true; // thả chuột xong không mở chi tiết
            choKeo = null;
        }
        if (!keo) return;
        var tr = e.target.closest('#' + keo.bang.tbody + ' tr');
        if (!tr) return;
        var chiSo = dsDongBang(keo.bang).indexOf(tr);
        if (chiSo >= 0) apKhoangBang(keo.bang, keo.dau, chiSo, keo.trangThai, keo.banDau);
    });

    document.addEventListener('mouseup', function () {
        choKeo = null;
        if (!keo) return;
        keo = null;
        document.body.classList.remove('qa-dang-keo');
    });

    // Chạy ở pha capture, trước các xử lý click khác của trang
    document.addEventListener('click', function (e) {
        if (!boQuaClick) return;
        boQuaClick = false;
        if (e.detail === 0) return; // phím Space trên ô tích: để mặc định
        e.preventDefault();
        e.stopPropagation();
    }, true);

    if (tuKhoa) $('qa-thung-tim').value = tuKhoa;
    napDanhSach();
})();
