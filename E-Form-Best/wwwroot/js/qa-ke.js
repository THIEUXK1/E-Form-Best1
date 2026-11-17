// Kệ chứa thùng QA ở trang /QAForm/Ke (đứng riêng) và khung ẩn trang /QAForm/Thung: thêm/sửa/xoá kệ, sơ đồ ô (tầng × ô) và thùng đang đặt. Không reload.
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
        // Kết quả (thành công / lỗi) hiện thêm giữa màn hình cho dễ thấy; dòng chữ tại chỗ vẫn giữ
        if (loai && window.qaThongBao) window.qaThongBao(text, loai);
    }

    // Mã vị trí giống QaKeService.MaViTri: kệ 1 tầng "A-A3" (kệ cha A, ô con A3), nhiều tầng "A-2-3"
    function maO(ke, tang, o) { return ke.soTang === 1 ? ke.maKe + o : 'Tầng ' + tang + ' · ô ' + o; }
    function maViTri(ke, tang, o) {
        return ke.soTang === 1 ? ke.maKe + '-' + ke.maKe + o : ke.maKe + '-' + tang + '-' + o;
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
            o(trTrong, 'Chưa có kệ nào. Bấm Thêm kệ hoặc Tạo dãy kệ (A–Z) để tạo.', 'qa-mes-trong').colSpan = 8;
            tbody.replaceChildren(trTrong);
        } else {
            var frag = document.createDocumentFragment();
            dsKe.forEach(function (k) {
                var tr = document.createElement('tr');
                tr.dataset.keId = k.idKe;
                if (k.idKe === idKeSoDo) tr.classList.add('qa-mes-dang-chon');
                var tdMa = o(tr, '', 'qa-mes-o-thung');
                tdMa.title = 'Bấm để xem sơ đồ ô';
                var huy = document.createElement('span');
                huy.className = 'qa-ke-ma';
                huy.textContent = k.maKe;
                tdMa.appendChild(huy);
                o(tr, k.tenKe);
                o(tr, k.khuVuc);
                o(tr, k.soTang === 1 ? k.soO + ' ô (' + maO(k, 1, 1) + '–' + maO(k, 1, k.soO) + ')' : k.soTang + ' × ' + k.soO);
                o(tr, k.soThung, 'so' + (k.soThung ? '' : ' qa-ke-so-0'));
                tr.appendChild(thanhMuc(k.soThung, k.soTang * k.soO));
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
        var tongO = dsKe.reduce(function (s, k) { return s + k.soTang * k.soO; }, 0);
        var keTrong = dsKe.filter(function (k) { return !k.soThung; }).length;
        $('qa-ke-tom').textContent = 'Kệ chứa thùng';
        veThongKe([
            ['fa-th', 'Kệ', dsKe.length, ''],
            ['fa-square-o', 'Tổng ô', tongO, ''],
            ['fa-cube', 'Thùng trên kệ', tongThung, 'xanh'],
            ['fa-inbox', 'Kệ còn trống', keTrong, 'cam']
        ]);
    }

    // Ô số liệu tổng đầu trang: [biểu tượng, nhãn, giá trị, màu]
    function veThongKe(ds, idKhung) {
        if (window.qaTheSoLieu) window.qaTheSoLieu(idKhung || 'qa-ke-thongke', ds);
    }

    // Thanh mức dùng: số thùng so với số ô (1 ô có thể chứa nhiều thùng nên chặn ở 100%)
    function thanhMuc(soThung, soO) {
        var td = document.createElement('td');
        td.className = 'qa-ke-muc-o';
        var pt = soO ? Math.min(100, Math.round(soThung * 100 / soO)) : 0;
        var thanh = document.createElement('span');
        thanh.className = 'qa-ke-muc' + (pt >= 100 ? ' day' : pt >= 70 ? ' gan-day' : '');
        var dau = document.createElement('i');
        dau.style.width = pt + '%';
        thanh.appendChild(dau);
        var chu = document.createElement('small');
        chu.textContent = pt + '%';
        td.append(thanh, chu);
        return td;
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
                + (ke.khuVuc ? ' · ' + ke.khuVuc : '') + ' · ' + thung.length + ' thùng · '
                + Object.keys(theoO).length + '/' + (ke.soTang * ke.soO) + ' ô có thùng';
            var luoi = $('qa-ke-sodo');
            luoi.style.gridTemplateColumns = 'auto repeat(' + ke.soO + ', minmax(130px, 1fr))';
            var frag = document.createDocumentFragment();
            for (var tang = ke.soTang; tang >= 1; tang--) {
                var nhan = document.createElement('div');
                nhan.className = 'qa-ke-tang';
                nhan.textContent = ke.soTang === 1 ? 'Kệ ' + ke.maKe : 'Tầng ' + tang;
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
                    ma.textContent = ke.soTang === 1 ? maO(ke, 1, so) : maViTri(ke, tang, so);
                    if (dsThung.length > 1) {
                        var dem = document.createElement('span');
                        dem.className = 'qa-ke-o-dem';
                        dem.textContent = dsThung.length;
                        ma.appendChild(dem);
                    }
                    oKe.appendChild(ma);
                    if (!dsThung.length) {
                        var trong = document.createElement('div');
                        trong.className = 'qa-ke-o-trong';
                        trong.textContent = 'Trống';
                        oKe.appendChild(trong);
                    }
                    dsThung.forEach(function (t) {
                        var chip = document.createElement('button');
                        chip.type = 'button';
                        chip.className = 'qa-ke-chip' + (t.trangThai === 'Mo' ? '' : ' da-dong');
                        chip.dataset.idThung = t.idThung;
                        chip.dataset.maThung = t.maThung;
                        var icThung = document.createElement('i');
                        icThung.className = 'fa ' + (t.trangThai === 'Mo' ? 'fa-cube' : 'fa-lock');
                        chip.append(icThung, document.createTextNode(' ' + t.maThung));
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
        $('qa-ke-tang').value = k ? k.soTang : 1;
        $('qa-ke-o').value = k ? k.soO : 6;
        $('qa-ke-ghichu').value = k ? (k.ghiChu || '') : '';
        $('qa-ke-luu').lastChild.textContent = k ? ' Lưu kệ ' + k.maKe : ' Lưu kệ';
        $('qa-ke-day-form').hidden = true;
        $('qa-ke-form').hidden = false;
        $('qa-ke-ma').focus();
    }

    async function taoDayKe(e) {
        e.preventDefault();
        var nutTao = $('qa-ke-day-luu');
        if (nutTao.disabled) return;
        var tu = $('qa-ke-day-tu').value.trim().toUpperCase(), den = $('qa-ke-day-den').value.trim().toUpperCase();
        if (!/^[A-Z]$/.test(tu) || !/^[A-Z]$/.test(den) || tu > den) { thongBao('Từ kệ / đến kệ là 1 chữ cái A–Z, từ ≤ đến.', 'loi'); return; }
        var soO = parseInt($('qa-ke-day-o').value, 10) || 0, soTang = parseInt($('qa-ke-day-tang').value, 10) || 0;
        var soKe = den.charCodeAt(0) - tu.charCodeAt(0) + 1;
        if (!(await qaXacNhan('Tạo ' + soKe + ' kệ ' + tu + '–' + den + ', mỗi kệ ' + (soTang === 1 ? soO + ' ô' : soTang + ' tầng × ' + soO + ' ô') + '? (kệ đã có sẽ bỏ qua)'))) return;
        nutTao.disabled = true;
        thongBao('Đang tạo...');
        try {
            var kq = await guiJson('/QAForm/Ke/TaoDay', {
                tuMa: tu, denMa: den, soTang: soTang, soO: soO, khuVuc: $('qa-ke-day-khuvuc').value
            });
            thongBao(kq.thongBao, kq.thanhCong ? 'ok' : 'loi');
            if (kq.thanhCong) { $('qa-ke-day-form').hidden = true; await napLai(); }
        } catch (err) {
            thongBao('Không gọi được máy chủ: ' + err.message, 'loi');
        } finally {
            nutTao.disabled = false;
        }
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
        if (!(await qaXacNhan('Xoá kệ ' + (k ? k.maKe : '') + '?'))) return;
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
        if (e.target.closest('#qa-ke-day-mo')) { $('qa-ke-form').hidden = true; $('qa-ke-day-form').hidden = false; $('qa-ke-day-tu').focus(); return; }
        if (e.target.closest('#qa-ke-day-huy')) { $('qa-ke-day-form').hidden = true; return; }
        var nutKe = e.target.closest('#qa-ke-tbody button[data-ke-lenh]');
        if (nutKe) {
            var id = parseInt(nutKe.dataset.id, 10);
            if (nutKe.dataset.keLenh === 'sua') moForm(dsKe.find(function (x) { return x.idKe === id; }));
            else xoaKe(id, nutKe);
            return;
        }
        var chip = e.target.closest('#qa-ke-sodo .qa-ke-chip');
        if (chip) {
            // Trang /QAForm/Ke đứng riêng: mở thùng ở trang Danh sách thùng (tìm theo mã thùng)
            if ($('qa-ke-khung').dataset.docLap) { window.location.href = '/QAForm/Thung?tim=' + encodeURIComponent(chip.dataset.maThung); return; }
            phat('qa-ke:xem-thung', { idThung: parseInt(chip.dataset.idThung, 10) });
            return;
        }
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
        else if (e.target.id === 'qa-ke-day-form') taoDayKe(e);
    });

    window.qaKe = {
        ds: function () { return dsKe; },
        maViTri: maViTri,
        maO: maO,
        veThongKe: veThongKe,
        napLai: napLai,
        moSoDo: function (idKe) { $('qa-ke-khung').hidden = false; return xemSoDo(idKe); }
    };
    napLai();
})();
