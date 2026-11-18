// Trang QA "Check xuất hàng" — tab Bảng khiếu nại: lưới kiểu Excel trên QA_KhieuNai.
// Ô sửa trực tiếp (contenteditable / input / select), rời ô hoặc Enter là lưu riêng ô đó qua
// POST /QAForm/CheckXuatHang/KhieuNai/{id}/Sua; nhập mã lot thì server tự điền cột hệ thống (MES/WMS) và trả lại cả dòng.
// Phím: Enter / Shift+Enter xuống / lên, Tab sang ô kế, Alt+Enter xuống dòng trong ô, Esc huỷ sửa.
(function () {
    var the = document.getElementById('qa-kn-the');
    if (!the) return;
    var formLoc = document.getElementById('qa-kn-loc');
    var thead = document.getElementById('qa-kn-thead');
    var tbody = document.getElementById('qa-kn-tbody');
    var thongBao = document.getElementById('qa-kn-thongbao');
    var nutThem = document.getElementById('qa-kn-them');
    var oNhap = document.getElementById('qa-kn-nhap');
    var nutXuat = document.getElementById('qa-kn-xuat');
    var goiYChungLoai = document.getElementById('qa-kn-goiy-chungloai');

    var dsCot = [];          // mô tả cột server gửi (khoa, tieuDe, kieu, tuDong, vang…)
    var duLieu = {};         // id -> dòng
    var tongDong = 0;
    var COT_DAI = 1000;      // cột chuỗi dài hơn mức này thì cho xuống dòng, ô rộng hơn

    // ---------- Tab ----------
    function chonTab(tab) {
        document.querySelectorAll('[data-tab]').forEach(function (el) { el.hidden = el.getAttribute('data-tab') !== tab; });
        document.querySelectorAll('[data-chon-tab]').forEach(function (b) {
            b.classList.toggle('active', b.getAttribute('data-chon-tab') === tab);
        });
        try { localStorage.setItem('qa-check-xuat-hang-tab', tab); } catch (e) { /* trình duyệt chặn lưu: bỏ qua */ }
        var o = document.getElementById(tab === 'tra' ? 'qa-xh-ma' : 'qa-kn-tukhoa');
        if (o && tab === 'tra') o.focus();
    }
    document.addEventListener('click', function (ev) {
        var b = ev.target.closest('[data-chon-tab]');
        if (b) chonTab(b.getAttribute('data-chon-tab'));
    });
    var tabNho = 'kn';
    try { tabNho = localStorage.getItem('qa-check-xuat-hang-tab') || 'kn'; } catch (e) { /* bỏ qua */ }
    chonTab(tabNho === 'tra' ? 'tra' : 'kn');

    // ---------- Tiện ích ----------
    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function bao(chu, kieu) {
        thongBao.textContent = chu || '';
        thongBao.className = 'qa-mes-thongbao' + (kieu ? ' ' + kieu : '');
    }

    async function goi(url, tuyChon) {
        var res = await fetch(url, tuyChon);
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
        return res.json();
    }

    function postJson(url, body) {
        return goi(url, {
            method: 'POST',
            headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify(body || {})
        });
    }

    // Giá trị server -> chữ hiển thị trong ô
    function hienThi(cot, v) {
        if (v === null || v === undefined) return '';
        if (cot.kieu === 'So') return Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 });
        if (cot.kieu === 'Ngay') { var p = String(v).split('-'); return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : v; }
        return String(v);
    }

    function layGiaTriO(o) {
        if (o.tagName === 'SELECT' || o.tagName === 'INPUT') return o.value.trim();
        return (o.innerText || '').replace(/ /g, ' ').trim();
    }

    function ganGiaTriO(o, chu) {
        if (o.tagName === 'SELECT' || o.tagName === 'INPUT') o.value = chu;
        else o.textContent = chu;
        o.setAttribute('data-goc', chu);
    }

    function locHienTai() {
        var p = new URLSearchParams(new FormData(formLoc));
        Array.from(p.keys()).forEach(function (k) { if (!p.get(k)) p.delete(k); });
        return p;
    }

    // ---------- Vẽ ----------
    function veTieuDe() {
        var tr = document.createElement('tr');
        var thSo = document.createElement('th');
        thSo.className = 'qa-kn-co-dinh qa-kn-stt';
        thSo.textContent = '#';
        tr.appendChild(thSo);
        var thNut = document.createElement('th');
        thNut.className = 'qa-kn-co-dinh qa-kn-nut-cot';
        thNut.title = 'Lấy lại từ MES / xoá dòng';
        thNut.innerHTML = '<i class="fa fa-cog"></i>';
        tr.appendChild(thNut);
        dsCot.forEach(function (c) {
            var th = document.createElement('th');
            th.className = (c.vang ? 'qa-kn-vang' : 'qa-kn-xanh') + (c.tuDong ? ' qa-kn-tudong' : '') + (c.doDai >= COT_DAI ? ' qa-kn-dai' : '');
            var chu = document.createElement('span');
            chu.textContent = c.tieuDe;
            var ma = document.createElement('small');
            ma.textContent = c.cotExcel + (c.tuDong ? ' · tự điền' : '');
            th.append(chu, ma);
            if (c.tuDong) th.title = 'Tự điền từ NH-MES / NH-WMS khi nhập mã lot (vẫn sửa tay được)';
            tr.appendChild(th);
        });
        thead.replaceChildren(tr);
    }

    function taoO(c, d) {
        var td = document.createElement('td');
        td.setAttribute('data-k', c.khoa);
        if (c.tuDong) td.className = 'qa-kn-tudong';
        var o;
        if (c.kieu === 'TrangThai') {
            o = document.createElement('select');
            o.className = 'qa-kn-tt';
            ['pending', 'close'].forEach(function (t) {
                var op = document.createElement('option');
                op.value = t;
                op.textContent = t;
                o.appendChild(op);
            });
        } else if (c.khoa === 'chungLoai') {
            o = document.createElement('input');
            o.type = 'text';
            o.className = 'qa-kn-input';
            o.setAttribute('list', 'qa-kn-goiy-chungloai');
            o.maxLength = c.doDai;
        } else {
            o = document.createElement('div');
            o.className = 'qa-kn-o' + (c.doDai >= COT_DAI ? ' qa-kn-dai' : '') + (c.kieu === 'So' ? ' qa-kn-so' : '');
            o.contentEditable = 'plaintext-only';
            if (o.contentEditable !== 'plaintext-only') o.contentEditable = 'true';
            o.spellcheck = false;
        }
        o.setAttribute('data-o', '1');
        o.setAttribute('data-k', c.khoa);
        ganGiaTriO(o, hienThi(c, d[c.khoa]));
        td.appendChild(o);
        return td;
    }

    function taoDong(d, stt) {
        var tr = document.createElement('tr');
        tr.setAttribute('data-id', d.id);
        var tdSo = document.createElement('td');
        tdSo.className = 'qa-kn-co-dinh qa-kn-stt';
        tdSo.textContent = stt;
        tr.appendChild(tdSo);
        var tdNut = document.createElement('td');
        tdNut.className = 'qa-kn-co-dinh qa-kn-nut-cot';
        tdNut.innerHTML = '<button type="button" class="qa-kn-nut" data-dien-lai title="Lấy lại cột hệ thống từ NH-MES/NH-WMS theo mã lot"><i class="fa fa-refresh"></i></button>' +
            '<button type="button" class="qa-kn-nut xoa" data-xoa title="Xoá dòng"><i class="fa fa-trash-o"></i></button>';
        tr.appendChild(tdNut);
        dsCot.forEach(function (c) { tr.appendChild(taoO(c, d)); });
        capNhatDong(tr, d);
        return tr;
    }

    // Đổ lại giá trị dòng từ server (trừ ô đang được gõ), tô trạng thái
    function capNhatDong(tr, d) {
        duLieu[d.id] = d;
        tr.classList.toggle('qa-kn-close', d.trangThai === 'close');
        tr.title = 'Sửa lần cuối: ' + (d.nguoiSua || '') + ' ' + (d.ngaySua || '') +
            (d.dienTuDongLuc ? ' · Tự điền MES/WMS lúc ' + d.dienTuDongLuc : '');
        tr.querySelectorAll('[data-o]').forEach(function (o) {
            // Ô đang gõ dở thì giữ chữ người dùng; ô chỉ đang được chọn (chưa sửa) vẫn đổ giá trị mới
            if (o === document.activeElement && layGiaTriO(o) !== (o.getAttribute('data-goc') || '')) return;
            var c = dsCot.find(function (x) { return x.khoa === o.getAttribute('data-k'); });
            ganGiaTriO(o, hienThi(c, d[c.khoa]));
        });
    }

    function danhLaiStt() {
        Array.from(tbody.querySelectorAll('tr[data-id]')).forEach(function (tr, i) { tr.firstChild.textContent = i + 1; });
    }

    function veThongKe() {
        var ds = Object.keys(duLieu).map(function (k) { return duLieu[k]; });
        var pending = ds.filter(function (d) { return d.trangThai !== 'close'; }).length;
        var slKn = ds.reduce(function (t, d) { return t + (Number(d.slKhieuNai) || 0); }, 0);
        window.qaTheSoLieu('qa-kn-thongke', [
            ['fa-list', 'Dòng khiếu nại', ds.length.toLocaleString('vi-VN') + (tongDong > ds.length ? ' / ' + tongDong.toLocaleString('vi-VN') : ''), ''],
            ['fa-hourglass-half', 'pending', pending.toLocaleString('vi-VN'), 'cam'],
            ['fa-check-circle', 'close', (ds.length - pending).toLocaleString('vi-VN'), 'xanh'],
            ['fa-arrows-h', 'SL khiếu nại (Y)', slKn.toLocaleString('vi-VN', { maximumFractionDigits: 2 }), '']
        ]);
    }

    function dongTrong(chu) {
        var tr = document.createElement('tr');
        var td = document.createElement('td');
        td.colSpan = dsCot.length + 2 || 1;
        td.className = 'qa-mes-trong';
        // Bảng rộng hơn màn hình: chữ dính mép trái vùng nhìn thấy thay vì canh giữa cả bảng
        var span = document.createElement('span');
        span.textContent = chu;
        td.appendChild(span);
        tr.appendChild(td);
        tbody.replaceChildren(tr);
    }

    async function taiDs() {
        bao('Đang tải bảng khiếu nại…');
        try {
            var kq = await goi('/QAForm/CheckXuatHang/KhieuNai?' + locHienTai().toString(), { headers: { 'Accept': 'application/json' } });
            if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); dongTrong(kq.thongBao); return; }
            var dl = kq.duLieu;
            if (!dsCot.length) { dsCot = dl.cot; veTieuDe(); }
            duLieu = {};
            tongDong = dl.tong;
            var frag = document.createDocumentFragment();
            dl.ds.forEach(function (d, i) { frag.appendChild(taoDong(d, i + 1)); });
            if (dl.ds.length) tbody.replaceChildren(frag);
            else dongTrong('Không có dòng khiếu nại nào. Bấm "Thêm dòng" hoặc "Nhập Excel".');
            goiYChungLoai.replaceChildren.apply(goiYChungLoai, (dl.goiYChungLoai || []).map(function (s) {
                var op = document.createElement('option');
                op.value = s;
                return op;
            }));
            veThongKe();
            bao(dl.tong > dl.ds.length ? 'Đang hiện ' + dl.ds.length + '/' + dl.tong + ' dòng — lọc theo ngày để xem phần còn lại.' : '');
            nutXuat.href = '/QAForm/CheckXuatHang/KhieuNai/XuatExcel?' + locHienTai().toString();
        } catch (e) {
            bao('Không tải được bảng khiếu nại: ' + e.message, 'loi');
        }
    }

    // ---------- Lưu ô ----------
    async function luuO(o) {
        var tr = o.closest('tr[data-id]');
        if (!tr) return;
        var moi = layGiaTriO(o);
        if (moi === (o.getAttribute('data-goc') || '')) { o.classList.remove('loi'); return; }
        var id = tr.getAttribute('data-id');
        var khoa = o.getAttribute('data-k');
        o.classList.add('dang-luu');
        o.classList.remove('loi');
        if (khoa === 'maLot' && moi) bao('Đang lấy thông tin lot ' + moi + ' từ NH-MES / NH-WMS…');
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/Sua', { truong: khoa, giaTri: moi });
            if (!kq.thanhCong) {
                // Giữ chữ người dùng đã gõ, đánh dấu lỗi để sửa lại
                o.classList.add('loi');
                o.title = kq.thongBao;
                bao(kq.thongBao, 'loi');
                return;
            }
            o.title = '';
            o.setAttribute('data-goc', moi);
            // Đổ lại cả dòng (giá trị đã chuẩn hoá + cột tự điền khi đổi mã lot); ô đang gõ thì giữ nguyên
            if (kq.duLieu) capNhatDong(tr, kq.duLieu);
            veThongKe();
            if (kq.thongBao) bao(kq.thongBao, 'loi');
            else if (khoa === 'maLot' && moi) bao('Đã điền thông tin lot ' + moi + ' từ NH-MES / NH-WMS.', 'ok');
            else bao('');
        } catch (e) {
            o.classList.add('loi');
            bao('Không lưu được: ' + e.message, 'loi');
        } finally {
            o.classList.remove('dang-luu');
        }
    }

    tbody.addEventListener('focusout', function (ev) {
        var o = ev.target.closest('[data-o]');
        if (o && o.tagName !== 'SELECT') luuO(o);
    });
    tbody.addEventListener('change', function (ev) {
        var o = ev.target.closest('select[data-o]');
        if (o) luuO(o);
    });

    // Paste vào ô contenteditable: chỉ lấy chữ thuần
    tbody.addEventListener('paste', function (ev) {
        var o = ev.target.closest('div[data-o]');
        if (!o || o.contentEditable === 'plaintext-only') return;
        ev.preventDefault();
        document.execCommand('insertText', false, (ev.clipboardData || window.clipboardData).getData('text'));
    });

    function oCungCot(o, buoc) {
        var tr = o.closest('tr[data-id]');
        var khoa = o.getAttribute('data-k');
        var dich = buoc > 0 ? tr.nextElementSibling : tr.previousElementSibling;
        return dich ? dich.querySelector('[data-o][data-k="' + khoa + '"]') : null;
    }

    function dat(o) {
        if (!o) return;
        o.focus();
        if (o.tagName === 'DIV') {
            var r = document.createRange();
            r.selectNodeContents(o);
            var s = window.getSelection();
            s.removeAllRanges();
            s.addRange(r);
        } else if (o.select) {
            o.select();
        }
    }

    tbody.addEventListener('keydown', function (ev) {
        var o = ev.target.closest('[data-o]');
        if (!o) return;
        if (ev.key === 'Escape') {
            ganGiaTriO(o, o.getAttribute('data-goc') || '');
            o.classList.remove('loi');
            o.blur();
            return;
        }
        if (ev.key === 'Enter' && ev.altKey && o.tagName === 'DIV') {
            ev.preventDefault();
            document.execCommand('insertLineBreak');
            return;
        }
        if (ev.key === 'Enter' || (o.tagName !== 'SELECT' && (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') && !ev.altKey)) {
            // Ô nhiều dòng: mũi tên giữ di chuyển con trỏ trong ô
            if (ev.key !== 'Enter' && o.classList.contains('qa-kn-dai')) return;
            ev.preventDefault();
            var len = ev.key === 'ArrowUp' || (ev.key === 'Enter' && ev.shiftKey);
            dat(oCungCot(o, len ? -1 : 1) || (o.blur(), null));
        }
    });

    // ---------- Nút dòng ----------
    tbody.addEventListener('click', async function (ev) {
        var nut = ev.target.closest('[data-dien-lai], [data-xoa]');
        if (!nut) return;
        var tr = nut.closest('tr[data-id]');
        var id = tr.getAttribute('data-id');
        var d = duLieu[id] || {};
        if (nut.hasAttribute('data-xoa')) {
            if (!confirm('Xoá dòng khiếu nại ' + (d.maLot || '') + ' (' + (hienThi({ kieu: 'Ngay' }, d.ngayKhieuNai) || 'chưa có ngày') + ')?')) return;
        }
        nut.disabled = true;
        var icon = nut.innerHTML;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i>';
        try {
            if (nut.hasAttribute('data-xoa')) {
                var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/Xoa');
                if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); return; }
                tr.remove();
                delete duLieu[id];
                tongDong = Math.max(0, tongDong - 1);
                danhLaiStt();
                veThongKe();
                bao(kq.thongBao, 'ok');
            } else {
                bao('Đang lấy lại thông tin lot ' + (d.maLot || '') + '…');
                var kq2 = await postJson('/QAForm/CheckXuatHang/KhieuNai/' + id + '/DienLai');
                if (kq2.duLieu) capNhatDong(tr, kq2.duLieu);
                bao(kq2.thanhCong ? 'Đã cập nhật lot ' + (d.maLot || '') + ' từ NH-MES / NH-WMS.' : kq2.thongBao, kq2.thanhCong ? 'ok' : 'loi');
                veThongKe();
            }
        } catch (e) {
            bao('Lỗi: ' + e.message, 'loi');
        } finally {
            if (nut.isConnected) { nut.disabled = false; nut.innerHTML = icon; }
        }
    });

    // ---------- Thêm dòng ----------
    function guid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        return 'k' + Date.now().toString(36) + Math.random().toString(36).slice(2, 12);
    }

    nutThem.addEventListener('click', async function () {
        nutThem.disabled = true;
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/Them', { khoaTao: guid() });
            if (!kq.thanhCong) { bao(kq.thongBao, 'loi'); return; }
            if (!tbody.querySelector('tr[data-id]')) tbody.replaceChildren();
            var tr = taoDong(kq.duLieu, tbody.querySelectorAll('tr[data-id]').length + 1);
            tbody.appendChild(tr);
            tongDong++;
            veThongKe();
            tr.scrollIntoView({ block: 'nearest' });
            dat(tr.querySelector('[data-o][data-k="maLot"]'));
            bao('Đã thêm dòng — nhập mã lot để tự điền nhà máy, khách cuối, PO, mã hàng, màu, SL xuất.', 'ok');
        } catch (e) {
            bao('Không thêm được dòng: ' + e.message, 'loi');
        } finally {
            nutThem.disabled = false;
        }
    });

    // ---------- Nhập Excel ----------
    // Chọn được nhiều file (vd cả 12 tháng): gửi lần lượt từng file theo tên, gộp kết quả; file lỗi không chặn file sau
    oNhap.addEventListener('change', async function () {
        var dsTep = Array.from(oNhap.files || []).sort(function (a, b) { return a.name.localeCompare(b.name); });
        if (!dsTep.length) return;
        var ketQua = [], coLoi = false;
        for (var i = 0; i < dsTep.length; i++) {
            var tep = dsTep[i];
            bao('Đang nhập ' + (dsTep.length > 1 ? '(' + (i + 1) + '/' + dsTep.length + ') ' : '') + tep.name + '…');
            var fd = new FormData();
            fd.append('tep', tep);
            try {
                var kq = await goi('/QAForm/CheckXuatHang/KhieuNai/NhapExcel', {
                    method: 'POST', headers: { 'Accept': 'application/json', 'RequestVerificationToken': token() }, body: fd
                });
                if (!kq.thanhCong) coLoi = true;
                ketQua.push(tep.name + ': ' + kq.thongBao);
            } catch (e) {
                coLoi = true;
                ketQua.push(tep.name + ': lỗi ' + e.message);
            }
        }
        oNhap.value = '';
        await taiDs();
        bao(ketQua.join(' · '), coLoi ? 'loi' : 'ok');
    });

    // Đồng bộ thư mục QA trên file server: server đọc từng file và nhập (dòng mới thêm, dòng đã có điền ô trống)
    var nutDongBo = document.getElementById('qa-kn-dongbo');
    nutDongBo.addEventListener('click', async function () {
        nutDongBo.disabled = true;
        var icon = nutDongBo.innerHTML;
        nutDongBo.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang đồng bộ…';
        bao('Đang đọc các file khiếu nại trong thư mục QA…');
        try {
            var kq = await postJson('/QAForm/CheckXuatHang/KhieuNai/DongBoThuMuc');
            await taiDs();
            bao((kq.thongBao || '').split('\n').join(' · '), kq.thanhCong ? 'ok' : 'loi');
        } catch (e) {
            bao('Không đồng bộ được: ' + e.message, 'loi');
        } finally {
            nutDongBo.disabled = false;
            nutDongBo.innerHTML = icon;
        }
    });

    formLoc.addEventListener('submit', function (ev) {
        ev.preventDefault();
        taiDs();
    });

    taiDs();
})();
