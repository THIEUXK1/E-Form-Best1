// Quản lý tủ điện thoại (/FormHR/QuanLyTuDienThoai): danh sách, thêm, sửa, xoá — toàn bộ qua fetch, không reload trang.
(function () {
    'use strict';

    var duLieu = [];             // tủ máy chủ trả về (đã lọc theo quyền, bộ phận, từ khoá)
    var dsHien = [];             // duLieu sau khi lọc tình trạng (Đang dùng / Còn trống)
    var locTrangThai = '';
    var laQuanLy = false;        // AdminHR/AdminIT/All: thêm/sửa/xoá/in; người khác chỉ xem
    var modal = null;
    var modalIn = null;
    var henGioTim = null;
    var daChon = new Set();      // idTu đã tích để in tem
    var boNhoMau = {};           // idMau → trang tem đã tải, đỡ gọi lại khi đổi qua lại

    function layToken() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function thongBao(icon, text) {
        if (window.Swal) Swal.fire({ icon: icon, text: text, timer: icon === 'success' ? 1500 : undefined, showConfirmButton: icon !== 'success' });
        else alert(text);
    }

    async function goiPost(url, formData) {
        var res = await fetch(url, {
            method: 'POST',
            headers: { 'RequestVerificationToken': layToken() },
            body: formData
        });
        if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
        return res.json();
    }

    function oTrong(text, icon) {
        var tr = document.createElement('tr');
        var td = document.createElement('td');
        td.colSpan = 6;
        td.className = 'tdt-trong';
        var i = document.createElement('i');
        i.className = 'fa ' + (icon || 'fa-inbox');
        td.appendChild(i);
        td.appendChild(document.createTextNode(text));
        tr.appendChild(td);
        return tr;
    }

    function taoEl(the, lop, chu) {
        var e = document.createElement(the);
        if (lop) e.className = lop;
        if (chu != null) e.textContent = chu;
        return e;
    }

    // Màu cố định theo tên (bộ phận, người dùng) để cùng tên luôn cùng màu
    function sacDo(ten) {
        var h = 0;
        String(ten || '').split('').forEach(function (c) { h = (h * 31 + c.charCodeAt(0)) % 360; });
        return h;
    }

    function chuCaiDau(ten) {
        var tu = String(ten || '').trim().split(/\s+/).filter(Boolean);
        if (!tu.length) return '?';
        return (tu.length > 1 ? tu[tu.length - 2][0] + tu[tu.length - 1][0] : tu[0].slice(0, 2)).toUpperCase();
    }

    function coNguoiDung(t) { return !!(t.nguoiSuDung || t.maNhanVien); }

    function veThongKe() {
        var dung = duLieu.filter(coNguoiDung).length;
        document.getElementById('tdtTkTong').textContent = duLieu.length;
        document.getElementById('tdtTkDangDung').textContent = dung;
        document.getElementById('tdtTkTrong').textContent = duLieu.length - dung;
        document.getElementById('tdtTkBoPhan').textContent = new Set(duLieu.map(function (t) { return t.idBoPhan; })).size;
    }

    function veBang() {
        var body = document.getElementById('tdtBody');
        body.replaceChildren();
        dsHien = duLieu.filter(function (t) {
            return !locTrangThai || (locTrangThai === 'dung') === coNguoiDung(t);
        });
        veThongKe();
        var chan = document.getElementById('tdtChan');
        chan.textContent = dsHien.length ? 'Hiển thị ' + dsHien.length + ' / ' + duLieu.length + ' tủ' : '';
        if (!dsHien.length) {
            body.appendChild(duLieu.length
                ? oTrong('Không có tủ nào khớp bộ lọc.', 'fa-filter')
                : oTrong(laQuanLy ? 'Chưa có tủ nào. Bấm "Thêm tủ" để tạo.' : 'Bộ phận của bạn chưa có tủ nào.', 'fa-inbox'));
            capNhatChon();
            return;
        }
        dsHien.forEach(function (t) {
            var tr = document.createElement('tr');
            if (daChon.has(t.idTu)) tr.className = 'dang-chon';

            if (laQuanLy) {
                var tdChon = taoEl('td', 'tdt-o-chon');
                var cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.className = 'form-check-input tdt-chon';
                cb.dataset.id = t.idTu;
                cb.checked = daChon.has(t.idTu);
                cb.setAttribute('aria-label', 'Chọn tủ ' + t.tenTu + ' để in tem');
                tdChon.appendChild(cb);
                tr.appendChild(tdChon);
            }

            var tdTu = taoEl('td', 'tdt-o-tu');
            var tu = taoEl('div', 'tdt-tu');
            var ic = taoEl('span', 'tdt-tu-icon');
            ic.appendChild(taoEl('i', 'fa fa-mobile'));
            tu.appendChild(ic);
            var tuChu = taoEl('div');
            tuChu.appendChild(taoEl('div', 'tdt-tu-ten', t.tenTu));
            if (t.viTri) {
                var vt = taoEl('div', 'tdt-tu-vi-tri');
                vt.appendChild(taoEl('i', 'fa fa-map-marker'));
                vt.appendChild(document.createTextNode(t.viTri));
                tuChu.appendChild(vt);
            }
            tu.appendChild(tuChu);
            tdTu.appendChild(tu);
            tr.appendChild(tdTu);

            var tdBp = taoEl('td', 'tdt-o-bo-phan');
            var bp = taoEl('span', 'tdt-bo-phan', t.tenBoPhan);
            var h = sacDo(t.tenBoPhan);
            bp.style.background = 'hsl(' + h + ' 85% 94%)';
            bp.style.color = 'hsl(' + h + ' 55% 32%)';
            bp.title = t.tenBoPhan || '';
            tdBp.appendChild(bp);
            tr.appendChild(tdBp);

            var tdNg = taoEl('td', 'tdt-o-nguoi');
            if (coNguoiDung(t)) {
                var ng = taoEl('div', 'tdt-nguoi');
                var anh = taoEl('span', 'tdt-anh', chuCaiDau(t.nguoiSuDung || t.maNhanVien));
                anh.style.background = 'hsl(' + sacDo(t.nguoiSuDung || t.maNhanVien) + ' 60% 52%)';
                anh.setAttribute('aria-hidden', 'true');
                ng.appendChild(anh);
                var ngChu = taoEl('div');
                ngChu.appendChild(taoEl('div', 'tdt-nguoi-ten', t.nguoiSuDung || '(chưa ghi tên)'));
                if (t.maNhanVien) ngChu.appendChild(taoEl('div', 'tdt-nguoi-ma', t.maNhanVien));
                ng.appendChild(ngChu);
                tdNg.appendChild(ng);
            } else {
                tdNg.appendChild(taoEl('span', 'tdt-chua-co', 'Chưa có người dùng'));
            }
            tr.appendChild(tdNg);

            var tdTt = taoEl('td', 'tdt-o-tinh-trang');
            tdTt.appendChild(coNguoiDung(t) ? taoEl('span', 'tdt-tinh-trang tdt-dang-dung', 'Đang dùng') : taoEl('span', 'tdt-tinh-trang tdt-con-trong', 'Còn trống'));
            tr.appendChild(tdTt);

            if (laQuanLy) {
                var tdNut = taoEl('td', 'tdt-o-thao-tac text-end');
                var nhom = taoEl('div', 'tdt-thao-tac');
                [['tdt-sua', 'fa-pencil', 'Sửa'], ['tdt-xoa', 'fa-trash-o', 'Xoá']].forEach(function (n) {
                    var b = taoEl('button', n[0]);
                    b.type = 'button';
                    b.title = n[2] + ' tủ ' + t.tenTu;
                    b.setAttribute('aria-label', b.title);
                    b.dataset.id = t.idTu;
                    b.appendChild(taoEl('i', 'fa ' + n[1]));
                    nhom.appendChild(b);
                });
                tdNut.appendChild(nhom);
                tr.appendChild(tdNut);
            }
            body.appendChild(tr);
        });
        capNhatChon();
    }

    function capNhatChon() {
        if (!laQuanLy) return;
        var n = daChon.size;
        document.getElementById('tdtSoChon').textContent = n;
        document.getElementById('tdtSoChon2').textContent = n;
        document.getElementById('tdtThanhChon').hidden = n === 0;
        document.getElementById('tdtBtnIn').disabled = n === 0;
        var tatCa = document.getElementById('tdtChonTatCa');
        var soHienChon = dsHien.filter(function (t) { return daChon.has(t.idTu); }).length;
        tatCa.checked = soHienChon > 0 && soHienChon === dsHien.length;
        tatCa.indeterminate = soHienChon > 0 && soHienChon < dsHien.length;
    }

    // ===== Xoá nhiều tủ đã tích =====

    async function xoaNhieu(nut) {
        var ds = duLieu.filter(function (t) { return daChon.has(t.idTu); });
        if (!ds.length) return;
        var ten = ds.slice(0, 5).map(function (t) { return t.tenTu; }).join(', ') + (ds.length > 5 ? '… (+' + (ds.length - 5) + ')' : '');
        var dongY = window.Swal
            ? (await Swal.fire({ icon: 'warning', title: 'Xoá ' + ds.length + ' tủ?', text: ten, showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ', confirmButtonColor: '#dc3545' })).isConfirmed
            : confirm('Xoá ' + ds.length + ' tủ: ' + ten + '?');
        if (!dongY) return;
        nut.disabled = true;
        try {
            var fd = new FormData();
            ds.forEach(function (t) { fd.append('dsIdTu', t.idTu); });
            var kq = await goiPost('/FormHR/QuanLyTuDienThoai/XoaNhieu', fd);
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Xoá thất bại.');
            daChon.clear();
            thongBao('success', kq.thongBao);
            taiDanhSach();
        } catch (ex) {
            thongBao('error', ex.message);
        } finally {
            nut.disabled = false;
        }
    }

    // ===== Nhập từ Excel: Kiểm tra (xem trước) → Nhập =====

    var modalExcel = null;
    var TEN_KQ = { them: ['Thêm mới', 'tdt-kq-them'], sua: ['Cập nhật', 'tdt-kq-sua'], giu: ['Không đổi', 'tdt-kq-giu'], loi: ['Lỗi', 'tdt-kq-loi'] };

    function datLaiExcel() {
        document.getElementById('tdtExcelKetQua').hidden = true;
        document.getElementById('tdtExcelLoi').textContent = '';
        document.getElementById('tdtBtnNhapNgay').disabled = true;
    }

    function moNhapExcel() {
        document.getElementById('tdtFileExcel').value = '';
        datLaiExcel();
        modalExcel.show();
    }

    function veKetQuaExcel(d) {
        var tt = document.getElementById('tdtExcelTomTat');
        tt.replaceChildren();
        [['them', d.soThem], ['sua', d.soSua], ['giu', d.soGiuNguyen], ['loi', d.soLoi]].forEach(function (x) {
            var s = taoEl('span', TEN_KQ[x[0]][1], TEN_KQ[x[0]][0] + ': ' + x[1]);
            tt.appendChild(s);
        });
        var body = document.getElementById('tdtExcelDong');
        body.replaceChildren();
        d.dong.forEach(function (r) {
            var tr = document.createElement('tr');
            if (r.trangThai === 'loi') tr.className = 'tdt-dong-loi';
            tr.appendChild(taoEl('td', null, r.dong));
            var tdKq = document.createElement('td');
            var kq = taoEl('span', 'tdt-kq ' + TEN_KQ[r.trangThai][1], TEN_KQ[r.trangThai][0]);
            tdKq.appendChild(kq);
            if (r.loi) tdKq.appendChild(taoEl('div', 'text-danger small', r.loi));
            tr.appendChild(tdKq);
            [r.boPhan, r.tenTu, r.viTri, r.maNhanVien, r.nguoiSuDung].forEach(function (v) { tr.appendChild(taoEl('td', null, v || '')); });
            body.appendChild(tr);
        });
        document.getElementById('tdtExcelKetQua').hidden = false;
    }

    async function guiExcel(luu) {
        var file = document.getElementById('tdtFileExcel').files[0];
        var loi = document.getElementById('tdtExcelLoi');
        var nutKiem = document.getElementById('tdtBtnKiemExcel'), nutNhap = document.getElementById('tdtBtnNhapNgay');
        loi.textContent = '';
        if (!file) { loi.textContent = 'Chọn file Excel (.xlsx) trước.'; return; }
        var nut = luu ? nutNhap : nutKiem, nhanCu = nut.innerHTML;
        nutKiem.disabled = true; nutNhap.disabled = true;
        nut.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>' + (luu ? 'Đang nhập…' : 'Đang kiểm tra…');
        try {
            var fd = new FormData();
            fd.append('file', file);
            fd.append('luu', luu ? 'true' : 'false');
            var kq = await goiPost('/FormHR/QuanLyTuDienThoai/NhapExcel', fd);
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không nhập được file.');
            veKetQuaExcel(kq.duLieu);
            if (luu) {
                modalExcel.hide();
                thongBao('success', kq.thongBao || 'Đã nhập.');
                taiDanhSach();
                return;
            }
            var coThayDoi = kq.duLieu.soThem + kq.duLieu.soSua > 0;
            nutNhap.disabled = !coThayDoi;
            nutNhap.lastChild.textContent = coThayDoi ? ' Nhập ' + (kq.duLieu.soThem + kq.duLieu.soSua) + ' tủ' : ' Nhập';
            if (!coThayDoi) loi.textContent = kq.duLieu.soLoi ? 'Sửa các dòng lỗi trong file rồi kiểm tra lại.' : 'Không có gì thay đổi so với dữ liệu hiện tại.';
            else if (kq.duLieu.soLoi) loi.textContent = 'Các dòng lỗi sẽ bị bỏ qua khi nhập.';
        } catch (ex) {
            loi.textContent = ex.message;
        } finally {
            nut.innerHTML = nhanCu;
            nutKiem.disabled = false;
            if (luu) nutNhap.disabled = false;
        }
    }

    // ===== In tem =====

    function tuDaChon() { return duLieu.filter(function (t) { return daChon.has(t.idTu); }); }

    function docCaiDatInForm() {
        var T = window.TemNhan;
        return {
            soBan: Math.round(T.so(document.getElementById('tdtInSoBan').value, 1, 1, 50)),
            xoay: document.getElementById('tdtInXoay').checked,
            lechX: T.so(document.getElementById('tdtInLechX').value, 0, -20, 20),
            lechY: T.so(document.getElementById('tdtInLechY').value, 0, -20, 20)
        };
    }

    var mauDaNapTrang = null;    // idMau đã đổ danh sách trang vào ô "Trang nhãn"

    // Bản đang thiết kế chưa bấm Lưu: trang Thiết kế tem tự ghi nháp vào localStorage của trình duyệt.
    // Khoá phải trùng KHOA_NHAP trong tem-thiet-ke.js. Lưu xong bên đó tự xoá nháp -> chỉ còn mẫu đã lưu.
    var KHOA_NHAP_TEM = 'tem-thiet-ke-nhap';
    var ID_NHAP = 'nhap';

    function docNhapTem() {
        try {
            var n = JSON.parse(localStorage.getItem(KHOA_NHAP_TEM) || 'null');
            if (n && !n.duAn && n.trang) n.duAn = [n.trang]; // nháp bản cũ chỉ có 1 trang
            return n && Array.isArray(n.duAn) && n.duAn.length ? n : null;
        } catch (ex) { return null; }
    }

    function ghiChuNhap() {
        var o = document.getElementById('tdtInNhapGhiChu');
        var laNhap = document.getElementById('tdtInMau').value === ID_NHAP;
        o.hidden = !laNhap;
        o.textContent = laNhap ? 'Đang dùng bản thiết kế chưa lưu trên trình duyệt này. In được ngay, nhưng máy khác không thấy — bấm "Lưu" ở trang Thiết kế tem để dùng chung.' : '';
    }

    // Trả về trang nhãn đang chọn của mẫu (mẫu lưu dạng nhiều trang như file .tnl)
    async function layMau(idMau) {
        if (idMau === ID_NHAP && !boNhoMau[idMau]) {
            var nhap = docNhapTem();
            if (!nhap) throw new Error('Bản thiết kế chưa lưu không còn — mở lại hộp thoại In.');
            boNhoMau[idMau] = nhap.duAn.map(window.TemNhan.chuanHoaTrang);
        }
        if (!boNhoMau[idMau]) {
            var res = await fetch('/FormHR/QuanLyTuDienThoai/MauTem/' + idMau);
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được mẫu tem.');
            boNhoMau[idMau] = window.TemNhan.docTnl(kq.duLieu.noiDung);
        }
        var ds = boNhoMau[idMau], sel = document.getElementById('tdtInTrang');
        if (mauDaNapTrang !== idMau) {
            mauDaNapTrang = idMau;
            sel.replaceChildren();
            ds.forEach(function (t, i) {
                var op = document.createElement('option');
                op.value = i; op.textContent = t.Name + ' (' + t.WidthMm + ' × ' + t.HeightMm + ' mm)';
                sel.appendChild(op);
            });
            document.getElementById('tdtInTrangHop').hidden = ds.length < 2;
        }
        return ds[Math.min(parseInt(sel.value, 10) || 0, ds.length - 1)];
    }

    async function veXemTruoc() {
        var T = window.TemNhan;
        var hop = document.getElementById('tdtInXem');
        var loi = document.getElementById('tdtInLoi');
        var idMau = document.getElementById('tdtInMau').value;
        loi.textContent = '';
        hop.replaceChildren();
        if (!idMau) return;
        try {
            var trang = await layMau(idMau);
            var c = docCaiDatInForm();
            var kq = T.veNhan(trang, { dong: T.taoDong(tuDaChon()[0] || null), stt: 0 });
            var giay = document.createElement('div');
            giay.className = 'tdt-in-xem-giay';
            giay.appendChild(kq.el);
            hop.appendChild(giay);
            T.thuNhoChu(kq.el);
            // Phóng cho vừa khung xem; kích thước thật vẫn là mm
            var rong = T.hieuRong(trang) * T.PX_MM, cao = trang.HeightMm * T.PX_MM;
            var z = Math.min(4, (hop.clientWidth - 32) / rong, 220 / cao);
            if (!(z > 0)) z = 2; // modal chưa hiện xong thì chưa đo được bề ngang
            kq.el.style.transformOrigin = '0 0';
            kq.el.style.transform = 'scale(' + z + ')';
            giay.style.width = (rong * z) + 'px';
            giay.style.height = (cao * z) + 'px';
            if (c.xoay) giay.title = 'Khi in sẽ xoay 90°';
            if (kq.loi.length) loi.textContent = 'Mã vạch lỗi: ' + kq.loi[0].thongBao + ' — sửa dữ liệu tủ hoặc mẫu trước khi in.';
        } catch (ex) {
            loi.textContent = ex.message;
        }
    }

    function ghiAgent(text, kieu) {
        var o = document.getElementById('tdtInAgent');
        o.textContent = text || '';
        o.className = 'tdt-in-agent' + (kieu ? ' tdt-' + kieu : '');
    }

    // Có Tem in Agent trên máy này thì liệt kê máy in Windows; không có thì chỉ in qua hộp thoại trình duyệt
    async function napMayIn(mayDaChon) {
        var sel = document.getElementById('tdtInMay');
        sel.replaceChildren();
        var op0 = document.createElement('option');
        op0.value = ''; op0.textContent = 'Hộp thoại in của trình duyệt';
        sel.appendChild(op0);
        var ping = await window.TemNhan.agent.kiemTra();
        if (!ping) {
            ghiAgent('Máy này chưa chạy Tem in Agent nên in qua hộp thoại của trình duyệt. Tải bộ cài, cài xong mở lại hộp thoại này để chọn máy in và khổ tem của driver.');
            return;
        }
        try {
            var ds = await window.TemNhan.agent.mayIn();
            ds.forEach(function (m) {
                var op = document.createElement('option');
                op.value = m.name;
                op.textContent = m.name + (m.isTape ? ' (máy in băng)' : '') + (m.isDefault ? ' — mặc định' : '');
                sel.appendChild(op);
            });
            if (mayDaChon && ds.some(function (m) { return m.name === mayDaChon; })) sel.value = mayDaChon;
            ghiAgent('Tem in Agent ' + ping.version + ' trên máy ' + ping.machine + ': ' + ds.length + ' máy in.', 'ok');
        } catch (ex) {
            ghiAgent(ex.message, 'canh-bao');
        }
    }

    async function taiAgent(nut) {
        nut.disabled = true;
        try {
            var bc = await window.TemNhan.agent.taiBoCai();
            ghiAgent('Đang tải bộ cài TemNhanAgent_CaiDat.msi (' + bc.kichThuocMb + ' MB). Mở file để cài (không cần quyền admin), cài xong mở lại hộp thoại In tem.', 'ok');
        } catch (ex) {
            ghiAgent(ex.message, 'canh-bao');
        } finally {
            nut.disabled = false;
        }
    }

    // ===== Driver máy in: mỗi thư mục Driver\<máy in> trên file server là 1 lựa chọn =====
    var dsDriver = [];

    async function moDriver() {
        bootstrap.Modal.getOrCreateInstance(document.getElementById('tdtModalDriver')).show();
        var sel = document.getElementById('tdtDriverMay');
        var ghiChu = document.getElementById('tdtDriverGhiChu');
        sel.disabled = true;
        sel.replaceChildren(new Option('Đang tải…', ''));
        document.getElementById('tdtDriverDs').replaceChildren();
        ghiChu.textContent = '';
        try {
            var res = await fetch('/FormHR/QuanLyTuDienThoai/DriverMayIn/DanhSach');
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) throw new Error(kq.thongBao);
            dsDriver = kq.duLieu.ds;
            if (!dsDriver.length) {
                sel.replaceChildren(new Option('Chưa có driver nào', ''));
                return;
            }
            sel.replaceChildren(new Option('— Chọn máy in —', ''));
            dsDriver.forEach(function (d) { sel.appendChild(new Option(d.may, d.may)); });
            sel.disabled = false;
            if (dsDriver.length === 1) { sel.value = dsDriver[0].may; veDriver(); }
        } catch (ex) {
            sel.replaceChildren(new Option('Không tải được', ''));
            ghiChu.textContent = ex.message;
        }
    }

    function veDriver() {
        var may = document.getElementById('tdtDriverMay').value;
        var khung = document.getElementById('tdtDriverDs');
        khung.replaceChildren();
        var d = dsDriver.find(function (x) { return x.may === may; });
        if (!d) return;
        d.files.forEach(function (f) {
            // Tải file = điều hướng thật (được phép); máy chủ đọc file server rồi trả về
            var a = document.createElement('a');
            a.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';
            a.href = '/FormHR/QuanLyTuDienThoai/DriverMayIn/Tai?may=' + encodeURIComponent(d.may) + '&file=' + encodeURIComponent(f.ten);
            var ten = document.createElement('span');
            var i = document.createElement('i');
            i.className = 'fa fa-download me-2';
            ten.appendChild(i);
            ten.appendChild(document.createTextNode(f.ten));
            var nho = document.createElement('small');
            nho.className = 'text-muted ms-2 text-nowrap';
            nho.textContent = f.kichThuocMb + ' MB · ' + new Date(f.ngayCapNhat).toLocaleDateString('vi-VN');
            a.appendChild(ten);
            a.appendChild(nho);
            khung.appendChild(a);
        });
    }

    // So khổ mẫu với các khổ tem driver của máy in đang chọn
    async function kiemKhoMay() {
        var T = window.TemNhan;
        var may = document.getElementById('tdtInMay').value;
        var idMau = document.getElementById('tdtInMau').value;
        document.getElementById('tdtInGoiY').hidden = !!may;
        if (!may || !idMau) return;
        try {
            var trang = await layMau(idMau);
            var rong = T.hieuRong(trang), cao = trang.HeightMm;
            var ds = await T.agent.khoTem(may);
            if (document.getElementById('tdtInMay').value !== may) return; // đã đổi máy khác trong lúc chờ
            var khop = ds.filter(function (m) { return T.khopKho(m, rong, cao); });
            var khoMau = rong + ' × ' + cao + ' mm';
            if (khop.length) ghiAgent('Khổ mẫu ' + khoMau + ' khớp khổ "' + khop[0].display + '" của driver.', 'ok');
            else ghiAgent('Driver không có khổ ' + khoMau + '. Khổ của máy: ' +
                (ds.slice(0, 8).map(function (m) { return m.display; }).join(', ') || '(không đọc được)') +
                '.\nTạo khổ đúng trong Printing Preferences hoặc sửa khổ mẫu, nếu không tem có thể bị lệch.', 'canh-bao');
        } catch (ex) {
            ghiAgent(ex.message, 'canh-bao');
        }
    }

    async function moIn() {
        if (!daChon.size) return;
        var c = window.TemNhan.docCaiDatIn();
        document.getElementById('tdtInSoTu').textContent = daChon.size;
        document.getElementById('tdtInSoBan').value = c.soBan;
        document.getElementById('tdtInXoay').checked = c.xoay;
        document.getElementById('tdtInLechX').value = c.lechX;
        document.getElementById('tdtInLechY').value = c.lechY;
        document.getElementById('tdtInLoi').textContent = '';
        document.getElementById('tdtInXem').replaceChildren();
        document.getElementById('tdtInGoiY').hidden = false;
        ghiAgent('Đang tìm Tem in Agent…');

        var sel = document.getElementById('tdtInMau');
        var nutIn = document.getElementById('tdtBtnInNgay');
        sel.replaceChildren();
        nutIn.disabled = true;
        modalIn.show();
        try {
            var res = await fetch('/FormHR/QuanLyTuDienThoai/MauTem');
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được danh sách mẫu tem.');
            var ds = kq.duLieu || [];
            var nhap = docNhapTem();
            if (!ds.length && !nhap) {
                var op0 = document.createElement('option');
                op0.value = ''; op0.textContent = '(chưa có mẫu tem)';
                sel.appendChild(op0);
                document.getElementById('tdtInLoi').textContent = 'Chưa có mẫu tem nào. Bấm "Sửa / tạo mẫu tem" để thiết kế, xong bấm "Lưu".';
                ghiChuNhap();
                return;
            }
            // Bản đang thiết kế (chưa lưu) đứng đầu và được chọn sẵn: vừa chỉnh xong sang đây in là đúng mẫu vừa chỉnh
            if (nhap) {
                var opNhap = document.createElement('option');
                opNhap.value = ID_NHAP;
                opNhap.textContent = '✎ Đang thiết kế, chưa lưu' + (nhap.tenMau ? ': ' + nhap.tenMau : '');
                sel.appendChild(opNhap);
            }
            ds.forEach(function (m) {
                var op = document.createElement('option');
                op.value = m.idMau; op.textContent = m.tenMau;
                sel.appendChild(op);
            });
            if (nhap) sel.value = ID_NHAP;
            else if (c.idMau && ds.some(function (m) { return String(m.idMau) === String(c.idMau); })) sel.value = String(c.idMau);
            boNhoMau = {}; // mẫu có thể vừa được sửa ở trang thiết kế
            mauDaNapTrang = null;
            ghiChuNhap();
            nutIn.disabled = false;
            veXemTruoc();
            await napMayIn(c.mayIn);
            kiemKhoMay();
        } catch (ex) {
            document.getElementById('tdtInLoi').textContent = ex.message;
        }
    }

    async function inNgay() {
        var nut = document.getElementById('tdtBtnInNgay');
        var loi = document.getElementById('tdtInLoi');
        var idMau = document.getElementById('tdtInMau').value;
        if (!idMau) return;
        var c = docCaiDatInForm();
        c.idMau = idMau;
        c.mayIn = document.getElementById('tdtInMay').value;
        window.TemNhan.luuCaiDatIn(c);
        nut.disabled = true;
        loi.textContent = '';
        try {
            var trang = await layMau(idMau);
            var dsDong = tuDaChon().map(window.TemNhan.taoDong);
            if (c.mayIn) {
                // In thẳng qua driver bằng agent (không hiện hộp thoại)
                var kq = await window.TemNhan.agent.inNhan(c.mayIn, trang, dsDong, c);
                modalIn.hide();
                if (kq.warning) thongBao('warning', 'Đã gửi ' + kq.pages + ' tem. ' + kq.warning);
                else thongBao('success', 'Đã gửi ' + kq.pages + ' tem tới máy in.');
            } else {
                await window.TemNhan.inNhan(trang, dsDong, c);
            }
        } catch (ex) {
            loi.textContent = ex.message;
        } finally {
            nut.disabled = false;
        }
    }

    async function taiDanhSach() {
        var body = document.getElementById('tdtBody');
        var p = new URLSearchParams();
        var bp = document.getElementById('tdtLocBoPhan').value;
        var tk = document.getElementById('tdtTuKhoa').value.trim();
        if (bp) p.set('idBoPhan', bp);
        if (tk) p.set('tuKhoa', tk);
        try {
            var res = await fetch('/FormHR/QuanLyTuDienThoai/DanhSach?' + p.toString());
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Không tải được danh sách.');
            duLieu = kq.duLieu || [];
            // Bỏ chọn các tủ không còn hiện (đã lọc đi / đã xoá) để không in nhầm tủ người dùng không thấy
            var conHien = new Set(duLieu.map(function (t) { return t.idTu; }));
            daChon.forEach(function (id) { if (!conHien.has(id)) daChon.delete(id); });
            veBang();
        } catch (e) {
            body.replaceChildren(oTrong(e.message, 'fa-exclamation-triangle'));
        }
    }

    function moModal(tu) {
        var form = document.getElementById('tdtForm');
        form.reset();
        document.getElementById('tdtLoi').textContent = '';
        document.getElementById('tdtIdTu').value = tu ? tu.idTu : 0;
        document.getElementById('tdtModalTieuDe').textContent = tu ? 'Sửa tủ' : 'Thêm tủ';
        if (tu) {
            document.getElementById('tdtBoPhan').value = String(tu.idBoPhan);
            document.getElementById('tdtTenTu').value = tu.tenTu || '';
            document.getElementById('tdtViTri').value = tu.viTri || '';
            document.getElementById('tdtMaNv').value = tu.maNhanVien || '';
            document.getElementById('tdtNguoiSuDung').value = tu.nguoiSuDung || '';
        } else {
            // Thêm mới: lấy sẵn bộ phận đang lọc cho đỡ chọn lại
            document.getElementById('tdtBoPhan').value = document.getElementById('tdtLocBoPhan').value;
        }
        modal.show();
    }

    async function luu(e) {
        e.preventDefault();
        var loi = document.getElementById('tdtLoi');
        var nut = document.getElementById('tdtBtnLuu');
        loi.textContent = '';
        if (!document.getElementById('tdtBoPhan').value) { loi.textContent = 'Vui lòng chọn bộ phận.'; return; }
        if (!document.getElementById('tdtTenTu').value.trim()) { loi.textContent = 'Vui lòng nhập tên tủ.'; return; }

        nut.disabled = true;
        var nhanCu = nut.textContent;
        nut.textContent = 'Đang lưu...';
        try {
            var kq = await goiPost('/FormHR/QuanLyTuDienThoai/Luu', new FormData(e.target));
            if (!kq.thanhCong) { loi.textContent = kq.thongBao || 'Lưu thất bại.'; return; }
            modal.hide();
            thongBao('success', kq.thongBao);
            taiDanhSach();
        } catch (ex) {
            loi.textContent = ex.message;
        } finally {
            nut.disabled = false;
            nut.textContent = nhanCu;
        }
    }

    async function xoa(nut) {
        var tu = duLieu.find(function (t) { return String(t.idTu) === nut.dataset.id; });
        if (!tu) return;
        var hoi = window.Swal
            ? (await Swal.fire({ icon: 'warning', text: 'Xoá tủ "' + tu.tenTu + '" (' + tu.tenBoPhan + ')?', showCancelButton: true, confirmButtonText: 'Xoá', cancelButtonText: 'Huỷ', confirmButtonColor: '#dc3545' })).isConfirmed
            : confirm('Xoá tủ "' + tu.tenTu + '"?');
        if (!hoi) return;

        nut.disabled = true;
        try {
            var fd = new FormData();
            fd.append('idTu', tu.idTu);
            var kq = await goiPost('/FormHR/QuanLyTuDienThoai/Xoa', fd);
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Xoá thất bại.');
            thongBao('success', kq.thongBao);
            taiDanhSach();
        } catch (ex) {
            nut.disabled = false;
            thongBao('error', ex.message);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        laQuanLy = document.getElementById('tdtTrang').dataset.laQuanLy === '1';
        // Hộp thoại thêm/sửa và in tem chỉ có trong trang của admin
        if (laQuanLy) {
            modal = new bootstrap.Modal(document.getElementById('tdtModal'));
            modalIn = new bootstrap.Modal(document.getElementById('tdtModalIn'));
            modalExcel = new bootstrap.Modal(document.getElementById('tdtModalExcel'));
        }
        taiDanhSach();
    });

    document.addEventListener('click', function (e) {
        var loc = e.target.closest('[data-trang-thai]');
        if (loc) {
            locTrangThai = loc.dataset.trangThai;
            document.querySelectorAll('[data-trang-thai]').forEach(function (b) { b.classList.toggle('dang-chon', b === loc); });
            veBang();
            return;
        }
        if (!laQuanLy) return;
        if (e.target.closest('#tdtBtnThem')) { moModal(null); return; }
        if (e.target.closest('#tdtBtnDriver')) { moDriver(); return; }
        // In tem → sang trang thiết kế tem, mang theo các tủ đã tích (tích sẵn ở bảng Dữ liệu)
        if (e.target.closest('#tdtBtnIn') || e.target.closest('#tdtBtnIn2')) {
            if (!daChon.size) return;
            window.location.href = '/FormHR/QuanLyTuDienThoai/ThietKeTem?tu=' + Array.from(daChon).join(',');
            return;
        }
        var nutXoaNhieu = e.target.closest('#tdtBtnXoaNhieu');
        if (nutXoaNhieu) { xoaNhieu(nutXoaNhieu); return; }
        if (e.target.closest('#tdtBtnBoChon')) { daChon.clear(); veBang(); return; }
        if (e.target.closest('#tdtBtnNhapExcel')) { moNhapExcel(); return; }
        if (e.target.closest('#tdtBtnKiemExcel')) { guiExcel(false); return; }
        if (e.target.closest('#tdtBtnNhapNgay')) { guiExcel(true); return; }
        if (e.target.closest('#tdtBtnInNgay')) { inNgay(); return; }
        var nutTai = e.target.closest('#tdtTaiAgent');
        if (nutTai) { taiAgent(nutTai); return; }
        var sua = e.target.closest('.tdt-sua');
        if (sua) { moModal(duLieu.find(function (t) { return String(t.idTu) === sua.dataset.id; })); return; }
        var nutXoa = e.target.closest('.tdt-xoa');
        if (nutXoa) xoa(nutXoa);
    });

    document.addEventListener('submit', function (e) {
        if (e.target.id === 'tdtForm') luu(e);
    });

    document.addEventListener('change', function (e) {
        var o = e.target;
        if (o.id === 'tdtLocBoPhan') { taiDanhSach(); return; }
        if (o.id === 'tdtDriverMay') { veDriver(); return; }
        if (o.id === 'tdtFileExcel') { datLaiExcel(); if (o.files[0]) guiExcel(false); return; } // chọn file xong kiểm tra luôn
        if (o.classList.contains('tdt-chon')) {
            var id = parseInt(o.dataset.id, 10);
            if (o.checked) daChon.add(id); else daChon.delete(id);
            o.closest('tr').classList.toggle('dang-chon', o.checked);
            capNhatChon();
            return;
        }
        if (o.id === 'tdtChonTatCa') {
            // Chỉ chọn / bỏ các tủ đang hiện (theo bộ lọc tình trạng)
            dsHien.forEach(function (t) { if (o.checked) daChon.add(t.idTu); else daChon.delete(t.idTu); });
            document.querySelectorAll('#tdtBody .tdt-chon').forEach(function (cb) { cb.checked = o.checked; cb.closest('tr').classList.toggle('dang-chon', o.checked); });
            capNhatChon();
            return;
        }
        if (o.id === 'tdtInMau') ghiChuNhap();
        if (['tdtInMau', 'tdtInXoay', 'tdtInTrang'].indexOf(o.id) >= 0) veXemTruoc();
        if (o.id === 'tdtInMau' || o.id === 'tdtInMay' || o.id === 'tdtInTrang') kiemKhoMay();
    });

    document.addEventListener('input', function (e) {
        if (e.target.id !== 'tdtTuKhoa') return;
        clearTimeout(henGioTim);
        henGioTim = setTimeout(taiDanhSach, 300);
    });
})();
