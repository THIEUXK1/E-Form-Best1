// Sửa thông tin dòng "Tài sản khác đi kèm" trên trang /QLKiemKe/ViewCheckMayHienTai.
// Dòng được vẽ động sau mỗi lần tra cứu chủ sở hữu nên bắt sự kiện qua document; dữ liệu dòng nằm ở $row.data('ts').
(function () {
    'use strict';

    // Dùng chung cho lúc vẽ danh sách (view) và lúc cập nhật lại 1 dòng sau khi lưu
    window.moTaTaiSanKhac = function (ts) {
        return (ts.loaiThietBi || 'Thiết bị')
            + (ts.seribacode ? ' - S/N: ' + ts.seribacode : '')
            + (ts.ip ? ' - IP: ' + ts.ip : '')
            + (ts.quyCach ? ' - Quy cách: ' + ts.quyCach : '')
            + (ts.tenViTri ? ' (' + ts.tenViTri + ')' : '');
    };

    var dsLoai = null;
    var $dongDangSua = null;

    function napDanhMucLoai() {
        if (dsLoai) return Promise.resolve(dsLoai);
        return fetch('/QLKiemKe/GetKkLoaiThietBis')
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (res) {
                if (!res || !res.success) throw new Error((res && res.message) || 'Không tải được danh mục loại thiết bị.');
                dsLoai = (res.data || []).map(function (x) { return x.tenLoai; }).filter(Boolean)
                    .sort(function (a, b) { return a.localeCompare(b, 'vi'); });
                return dsLoai;
            });
    }

    function veDropdownLoai(loaiHienTai) {
        var sel = document.getElementById('ddlSuaTskLoai');
        sel.innerHTML = '';
        var dau = document.createElement('option');
        dau.value = '';
        dau.textContent = '-- Chọn --';
        sel.appendChild(dau);

        var ds = dsLoai.slice();
        // Loại cũ không còn trong danh mục vẫn hiện để người dùng thấy giá trị đang lưu (server sẽ bắt chọn lại)
        if (loaiHienTai && ds.indexOf(loaiHienTai) === -1) ds.unshift(loaiHienTai);
        ds.forEach(function (ten) {
            var op = document.createElement('option');
            op.value = ten;
            op.textContent = ten;
            sel.appendChild(op);
        });
        sel.value = loaiHienTai || '';
    }

    // Loại nhận diện theo IP (máy in) bắt buộc IP, còn lại bắt buộc Serial - quy tắc lấy từ kiemke-trung-tai-san-khac.js
    function theoIp(loai) {
        return !!(window.QuyTacTrungTaiSanKhac && window.QuyTacTrungTaiSanKhac.layQuyTac(loai) === 'Ip');
    }

    function danhDauBatBuoc() {
        var ip = theoIp(document.getElementById('ddlSuaTskLoai').value);
        document.getElementById('batBuocSuaTskIp').classList.toggle('d-none', !ip);
        document.querySelector('label[for="txtSuaTskSerial"] .text-danger').classList.toggle('d-none', ip);
    }

    $(document).on('change', '#ddlSuaTskLoai', danhDauBatBuoc);

    function hienLoi(thongBao) {
        var box = document.getElementById('loiSuaTaiSanKhac');
        box.textContent = thongBao;
        box.style.display = thongBao ? '' : 'none';
    }

    function khoaNut(dangGui) {
        var btn = document.getElementById('btnLuuSuaTaiSanKhac');
        btn.disabled = dangGui;
        btn.innerHTML = dangGui
            ? '<span class="spinner-border spinner-border-sm" style="margin-right: 4px;"></span> Đang lưu...'
            : '<i class="fa fa-save" style="margin-right: 4px;"></i> Lưu';
    }

    $(document).on('click', '#lblTaiSanKhacDiKem .btn-sua-tt-tsk', function () {
        $dongDangSua = $(this).closest('.item-tai-san-khac');
        var ts = $dongDangSua.data('ts') || {};

        hienLoi('');
        khoaNut(false);
        document.getElementById('hidSuaTskId').value = ts.idThietBi;
        document.getElementById('lblSuaTskId').textContent = '#' + ts.idThietBi;
        document.getElementById('txtSuaTskSerial').value = ts.seribacode || '';
        document.getElementById('txtSuaTskIp').value = ts.ip || '';
        document.getElementById('txtSuaTskQuyCach').value = ts.quyCach || '';
        document.getElementById('txtSuaTskViTri').value = ts.tenViTri || '';
        document.getElementById('txtSuaTskGhiChu').value = ts.ghiChu || '';

        napDanhMucLoai()
            .then(function () { veDropdownLoai(ts.loaiThietBi); danhDauBatBuoc(); })
            .catch(function (err) { hienLoi(err.message); });

        bootstrap.Modal.getOrCreateInstance(document.getElementById('modalSuaTaiSanKhac')).show();
    });

    $(document).on('submit', '#formSuaTaiSanKhac', function (e) {
        e.preventDefault();
        hienLoi('');

        var loai = document.getElementById('ddlSuaTskLoai').value.trim();
        var serial = document.getElementById('txtSuaTskSerial').value.trim();
        var ip = document.getElementById('txtSuaTskIp').value.trim();
        if (!loai) { hienLoi('Vui lòng chọn Loại thiết bị.'); return; }
        if (theoIp(loai) && !ip) { hienLoi(loai + ' nhận diện theo IP, vui lòng nhập IP.'); return; }
        if (!theoIp(loai) && !serial) { hienLoi('Vui lòng nhập Serial.'); return; }

        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        var fd = new FormData();
        fd.append('IdThietBi', document.getElementById('hidSuaTskId').value);
        fd.append('LoaiThietBi', loai);
        fd.append('Seribacode', serial);
        fd.append('Ip', ip);
        fd.append('QuyCach', document.getElementById('txtSuaTskQuyCach').value.trim());
        fd.append('TenViTri', document.getElementById('txtSuaTskViTri').value.trim());
        fd.append('GhiChu', document.getElementById('txtSuaTskGhiChu').value.trim());
        fd.append('__RequestVerificationToken', tokenInput ? tokenInput.value : '');

        khoaNut(true);
        fetch('/QLKiemKe/SuaThongTinTaiSanKhac', { method: 'POST', body: fd })
            .then(function (res) {
                if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status + '. Vui lòng thử lại.');
                return res.json();
            })
            .then(function (res) {
                if (!res.thanhCong) {
                    hienLoi(res.thongBao || res.message || 'Lưu không thành công.');
                    khoaNut(false);
                    return;
                }

                // Cập nhật đúng dòng vừa sửa, không vẽ lại cả danh sách
                if ($dongDangSua && $dongDangSua.length) {
                    var ts = $.extend($dongDangSua.data('ts') || {}, res.duLieu);
                    $dongDangSua.data('ts', ts);
                    $dongDangSua.find('.txt-tsk').text(window.moTaTaiSanKhac(ts));
                }
                bootstrap.Modal.getOrCreateInstance(document.getElementById('modalSuaTaiSanKhac')).hide();
                khoaNut(false);
                if (window.Swal) Swal.fire({ icon: 'success', title: res.thongBao, timer: 1500, showConfirmButton: false });
            })
            .catch(function (err) {
                hienLoi(err.message || 'Lỗi kết nối khi lưu thiết bị.');
                khoaNut(false);
            });
    });
})();
