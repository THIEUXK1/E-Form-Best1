// Kệ chứa thùng QA ở trang /QAForm/Thung: thêm/sửa/xoá kệ, sơ đồ ô (tầng × ô) và thùng đang đặt. Không reload.
// Giao tiếp với qa-thung.js qua window.qaKe + sự kiện trên document:
//   'qa-ke:doi'       — danh sách kệ vừa nạp lại (detail = mảng kệ)
//   'qa-ke:chon-o'    — bấm 1 ô trên sơ đồ (detail = { idKe, tang, o })
//   'qa-ke:xem-thung' — bấm mã thùng trên sơ đồ (detail = { idThung })
(function () {
    var $ = function (id) { return document.getElementById(id); };
    var dsKe = [];
    var idKeSoDo = null;

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function thongBao(text, loai) {
        var el = $('qa-ke-thongbao');
        el.textContent = text || '';
        el.classList.toggle('loi', loai === 'loi');
        el.classList.toggle('ok', loai === 'ok');
    }

    function phat(ten, chiTiet) { document.dispatchEvent(new CustomEvent(ten, { detail: chiTiet })); }

    async function layJson(url, tuyChon) {
        var res = await fetch(url, tuyChon || { headers: { 'Accept': 'application/json' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    function guiJson(url, duLieu) {
        return layJson(url, {
            method: 'POST',
            headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: duLieu === undefined ? undefined : JSON.stringify(duLieu)
        });
    }

    function o(tr, giaTri, lop) {
        var td = document.createElement('td');
        td.textContent = giaTri === null || giaTri === undefined ? '' : giaTri;
        if (lop) td.className = lop;
        tr.appendChild(td);
        return td;
    }

    function nut(text, lop, lenh, id, bieuTuong) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm ' + lop;
        b.dataset.keLenh = lenh;
        b.dataset.id = id;
        var i = document.createElement('i');
        i.className = 'fa ' + bieuTuong;
        b.appendChild(i);
        b.appendChild(document.createTextNode(' ' + text));
        return b;
    }

    function veDanhSach() {
        var tbody = $('qa-ke-tbody');
        if (dsKe.length === 0) {
            var trTrong = document.createElement('tr');
            o(trTrong, 'Chưa có kệ nào. Bấm Thêm kệ để tạo.', 'qa-mes-trong').colSpan = 7;
            tbody.replaceChildren(trTrong);
        } else {
            var frag = document.createDocumentFragment();
            dsKe.forEach(function (k) {
                var tr = document.createElement('tr');
                tr.dataset.keId = k.idKe;
                if (k.idKe === idKeSoDo) tr.classList.add('qa-mes-dang-chon');
                o(tr, k.maKe, 'qa-mes-o-thung').title = 'Bấm để xem sơ đồ ô';
                o(tr, k.tenKe);
                o(tr, k.khuVuc);
                o(tr, k.soTang + ' × ' + k.soO);
                o(tr, k.soThung, 'so');
                o(tr, k.ghiChu);
                var td = document.createElement('td');
                td.className = 'qa-thung-nut';
                td.appendChild(nut('Sửa', 'btn-outline-primary', 'sua', k.idKe, 'fa-pencil'));
                td.appendChild(nut('Xoá', 'btn-outline-danger', 'xoa', k.idKe, 'fa-trash'));
                tr.appendChild(td);
                frag.appendChild(tr);
            });
            tbody.replaceChildren(frag);
        }
        var tongThung = dsKe.reduce(function (s, k) { return s + k.soThung; }, 0);
        $('qa-ke-tom').textContent = 'Kệ chứa thùng · ' + dsKe.length + ' kệ · ' + tongThung + ' thùng đang trên kệ';
    }

    async function napLai() {
        try {
            var kq = await layJson('/QAForm/Ke/DanhSach');
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); return; }
            dsKe = kq.duLieu || [];
            if (idKeSoDo && !dsKe.some(function (k) { return k.idKe === idKeSoDo; })) { idKeSoDo = null; $('qa-ke-sodo-khung').hidden = true; }
            veDanhSach();
            phat('qa-ke:doi', dsKe);
            if (idKeSoDo) await xemSoDo(idKeSoDo);
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        }
    }

    // Sơ đồ: tầng cao nhất ở trên cùng giống kệ thật, ô đánh số từ trái sang
    async function xemSoDo(idKe) {
        idKeSoDo = idKe;
        veDanhSach();
        try {
            var kq = await layJson('/QAForm/Ke/' + idKe + '/SoDo');
            if (!kq.thanhCong) { thongBao(kq.thongBao, 'loi'); $('qa-ke-sodo-khung').hidden = true; return; }
            var ke = kq.duLieu.ke, thung = kq.duLieu.thung;
            var theoO = {};
            thung.forEach(function (t) { (theoO[t.tang + '-' + t.o] = theoO[t.tang + '-' + t.o] || []).push(t); });

            $('qa-ke-sodo-tieude').textContent = 'Sơ đồ kệ ' + ke.maKe + (ke.tenKe ? ' · ' + ke.tenKe : '')
                + (ke.khuVuc ? ' · ' + ke.khuVuc : '') + ' · ' + thung.length + ' thùng';
            var luoi = $('qa-ke-sodo');
            luoi.style.gridTemplateColumns = 'auto repeat(' + ke.soO + ', minmax(110px, 1fr))';
            var frag = document.createDocumentFragment();
            for (var tang = ke.soTang; tang >= 1; tang--) {
                var nhan = document.createElement('div');
                nhan.className = 'qa-ke-tang';
                nhan.textContent = 'Tầng ' + tang;
                frag.appendChild(nhan);
                for (var so = 1; so <= ke.soO; so++) {
                    var dsThung = theoO[tang + '-' + so] || [];
                    var oKe = document.createElement('div');
                    oKe.className = 'qa-ke-o' + (dsThung.length ? ' co-thung' : '');
                    oKe.dataset.idKe = ke.idKe;
                    oKe.dataset.tang = tang;
                    oKe.dataset.o = so;
                    var ma = document.createElement('div');
                    ma.className = 'qa-ke-o-ma';
                    ma.textContent = ke.maKe + '-' + tang + '-' + so;
                    oKe.appendChild(ma);
                    dsThung.forEach(function (t) {
                        var chip = document.createElement('button');
                        chip.type = 'button';
                        chip.className = 'qa-ke-chip' + (t.trangThai === 'Mo' ? '' : ' da-dong');
                        chip.dataset.idThung = t.idThung;
                        chip.textContent = t.maThung;
                        chip.title = t.soDong + ' dòng · ' + (t.trangThai === 'Mo' ? 'đang mở' : 'đã đóng');
                        oKe.appendChild(chip);
                    });
                    frag.appendChild(oKe);
                }
            }
            luoi.replaceChildren(frag);
            $('qa-ke-sodo-khung').hidden = false;
        } catch (e) {
            thongBao('Không gọi được máy chủ: ' + e.message, 'loi');
        }
    }

    function moForm(k) {
        $('qa-ke-id').value = k ? k.idKe : '';
        $('qa-ke-ma').value = k ? k.maKe : '';
        $('qa-ke-ten').value = k ? (k.tenKe || '') : '';
        $('qa-ke-khuvuc').value = k ? (k.khuVuc || '') : '';
        $('qa-ke-tang').value = k ? k.soTang : 3;
        $('qa-ke-o').value = k ? k.soO : 4;
        $('qa-ke-ghichu').value = k ? (k.ghiChu || '') : '';
        $('qa-ke-luu').lastChild.textContent = k ? ' Lưu kệ ' + k.maKe : ' Lưu kệ';
        $('qa-ke-form').hidden = false;
        $('qa-ke-ma').focus();
    }

    async function luuKe(e) {
        e.preventDefault();
        var nutLuu = $('qa-ke-luu');
        if (nutLuu.disabled) return;
        var id = parseInt($('qa-ke-id').value, 10);
        nutLuu.disabled = true;
        thongBao('Đang lưu...');
        try {
            var kq = await guiJson('/QAForm/Ke/Luu', {
                idKe: isNaN(id) ? null : id,
                maKe: $('qa-ke-ma').value,
                tenKe: $('qa-ke-ten').value,
                khuVuc: $('qa-ke-khuvuc').value,
                soTang: parseInt($('qa-ke-tang').value, 10) || 0,
                soO: parseInt($('qa-ke-o').value, 10) || 0,
                ghiChu: $('qa-ke-ghichu').value
            });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) {
                $('qa-ke-form').hidden = true;
                if (kq.duLieu && kq.duLieu.idKe) idKeSoDo = kq.duLieu.idKe;
                await napLai();
            }
        } catch (err) {
            thongBao('Không gọi được máy chủ: ' + err.message, 'loi');
        } finally {
            nutLuu.disabled = false;
        }
    }

    async function xoaKe(id, nutBam) {
        var k = dsKe.find(function (x) { return x.idKe === id; });
        if (!confirm('Xoá kệ ' + (k ? k.maKe : '') + '?')) return;
        nutBam.disabled = true;
        try {
            var kq = await guiJson('/QAForm/Ke/' + id + '/Xoa');
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) await napLai();
        } catch (err) {
            thongBao('Không gọi được máy chủ: ' + err.message, 'loi');
        } finally {
            nutBam.disabled = false;
        }
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('#qa-ke-mo')) {
            var khung = $('qa-ke-khung');
            khung.hidden = !khung.hidden;
            if (!khung.hidden) khung.scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }
        if (e.target.closest('#qa-ke-an')) { $('qa-ke-khung').hidden = true; return; }
        if (e.target.closest('#qa-ke-them')) { moForm(null); return; }
        if (e.target.closest('#qa-ke-huy')) { $('qa-ke-form').hidden = true; return; }
        var nutKe = e.target.closest('#qa-ke-tbody button[data-ke-lenh]');
        if (nutKe) {
            var id = parseInt(nutKe.dataset.id, 10);
            if (nutKe.dataset.keLenh === 'sua') moForm(dsKe.find(function (x) { return x.idKe === id; }));
            else xoaKe(id, nutKe);
            return;
        }
        var chip = e.target.closest('#qa-ke-sodo .qa-ke-chip');
        if (chip) { phat('qa-ke:xem-thung', { idThung: parseInt(chip.dataset.idThung, 10) }); return; }
        var oKe = e.target.closest('#qa-ke-sodo .qa-ke-o');
        if (oKe) {
            phat('qa-ke:chon-o', { idKe: parseInt(oKe.dataset.idKe, 10), tang: parseInt(oKe.dataset.tang, 10), o: parseInt(oKe.dataset.o, 10) });
            return;
        }
        var tr = e.target.closest('#qa-ke-tbody tr[data-ke-id]');
        if (tr) xemSoDo(parseInt(tr.dataset.keId, 10));
    });

    document.addEventListener('submit', function (e) {
        if (e.target.id === 'qa-ke-form') luuKe(e);
    });

    window.qaKe = {
        ds: function () { return dsKe; },
        napLai: napLai,
        moSoDo: function (idKe) { $('qa-ke-khung').hidden = false; return xemSoDo(idKe); }
    };
    napLai();
})();
