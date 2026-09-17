// Đổi Bộ phận / Tên công ty của một đơn IT ngay trên trang chi tiết.
// Khung này chỉ được render cho quyền "All"; server vẫn kiểm quyền lại lần nữa.
(function () {
    'use strict';

    // Bỏ dấu tiếng Việt để gõ "ke toan" vẫn ra "Kế toán"
    function boDau(chuoi) {
        return (chuoi || '').toLowerCase()
            .normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd');
    }

    // Nâng cấp một <select> thành combobox có ô tìm kiếm.
    // <select> gốc được giữ lại (ẩn) làm nơi lưu giá trị, nên phần gửi dữ liệu không đổi.
    function taoComboTimKiem(select, placeholder) {
        const cacLuaChon = Array.from(select.options)
            .map(o => o.value)
            .filter(v => v !== '');

        const boc = document.createElement('div');
        boc.className = 'combo-tim-kiem';
        select.parentNode.insertBefore(boc, select);
        boc.appendChild(select);
        select.style.display = 'none';

        const oNhap = document.createElement('input');
        oNhap.type = 'text';
        oNhap.autocomplete = 'off';
        oNhap.placeholder = placeholder;
        oNhap.value = select.value || '';
        boc.appendChild(oNhap);

        const dsEl = document.createElement('div');
        dsEl.className = 'combo-tim-kiem-danhsach';
        boc.appendChild(dsEl);

        let viTriChon = -1;
        let dangHien = [];

        function veDanhSach(tuKhoa) {
            const khoa = boDau(tuKhoa);
            dangHien = cacLuaChon.filter(v => boDau(v).includes(khoa));
            dsEl.innerHTML = '';

            if (dangHien.length === 0) {
                const trong = document.createElement('div');
                trong.className = 'khong-co';
                trong.textContent = 'Không tìm thấy';
                dsEl.appendChild(trong);
            } else {
                dangHien.forEach((v, i) => {
                    const dong = document.createElement('div');
                    dong.textContent = v; // textContent: dữ liệu danh mục không được coi là HTML
                    dong.dataset.viTri = i;
                    dsEl.appendChild(dong);
                });
            }
            viTriChon = -1;
        }

        function moDanhSach(tuKhoa) {
            veDanhSach(tuKhoa);
            dsEl.classList.add('hien');
        }

        function dongDanhSach() {
            dsEl.classList.remove('hien');
        }

        function chon(giaTri) {
            select.value = giaTri;
            oNhap.value = giaTri;
            oNhap.classList.remove('combo-chua-chon');
            dongDanhSach();
            select.dispatchEvent(new Event('change', { bubbles: true }));
        }

        function toSang(i) {
            Array.from(dsEl.children).forEach(el => el.classList.remove('dang-chon'));
            const el = dsEl.querySelector('[data-vi-tri="' + i + '"]');
            if (el) {
                el.classList.add('dang-chon');
                el.scrollIntoView({ block: 'nearest' });
            }
        }

        oNhap.addEventListener('focus', () => moDanhSach(''));
        oNhap.addEventListener('input', () => {
            select.value = '';
            oNhap.classList.add('combo-chua-chon');
            moDanhSach(oNhap.value);
        });

        oNhap.addEventListener('keydown', function (e) {
            if (!dsEl.classList.contains('hien')) return;
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                viTriChon = Math.min(viTriChon + 1, dangHien.length - 1);
                toSang(viTriChon);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                viTriChon = Math.max(viTriChon - 1, 0);
                toSang(viTriChon);
            } else if (e.key === 'Enter') {
                if (viTriChon >= 0 && dangHien[viTriChon]) {
                    e.preventDefault();
                    chon(dangHien[viTriChon]);
                }
            } else if (e.key === 'Escape') {
                dongDanhSach();
            }
        });

        // Event delegation: các dòng được vẽ lại mỗi lần gõ
        dsEl.addEventListener('mousedown', function (e) {
            const dong = e.target.closest('[data-vi-tri]');
            if (!dong) return;
            e.preventDefault();
            chon(dangHien[parseInt(dong.dataset.viTri, 10)]);
        });

        // Rời ô mà chưa chọn đúng mục nào thì trả về giá trị hợp lệ gần nhất
        oNhap.addEventListener('blur', function () {
            dongDanhSach();
            const khop = cacLuaChon.find(v => boDau(v) === boDau(oNhap.value));
            if (khop) chon(khop);
            else if (!select.value) oNhap.classList.add('combo-chua-chon');
        });

        return oNhap;
    }

    document.addEventListener('DOMContentLoaded', function () {
        const khung = document.getElementById('khungDoiDonVi');
        if (!khung) return;

        const idDon = parseInt(khung.dataset.idDon, 10);
        const selBoPhan = document.getElementById('selBoPhanDon');
        const selCongTy = document.getElementById('selCongTyDon');
        const btnLuu = document.getElementById('btnLuuDonVi');
        const oThongBao = document.getElementById('thongBaoDoiDonVi');

        taoComboTimKiem(selBoPhan, 'Gõ để tìm bộ phận...');
        taoComboTimKiem(selCongTy, 'Gõ để tìm công ty...');

        function baoLoi(noiDung, laLoi) {
            oThongBao.textContent = noiDung;
            oThongBao.style.color = laLoi ? '#b91c1c' : '#15803d';
            oThongBao.style.display = 'block';
        }

        btnLuu.addEventListener('click', async function () {
            const boPhan = selBoPhan.value.trim();
            const tenCongTy = selCongTy.value.trim();

            if (!boPhan || !tenCongTy) {
                baoLoi('Vui lòng chọn đủ Bộ phận và Tên công ty.', true);
                return;
            }

            // Khoá nút chống double-submit
            const nhanCu = btnLuu.innerHTML;
            btnLuu.disabled = true;
            btnLuu.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang lưu...';
            oThongBao.style.display = 'none';

            try {
                const res = await fetch('/FormIT/DoiDonViDon', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    },
                    body: JSON.stringify({ idFormIt: idDon, boPhan: boPhan, tenCongTy: tenCongTy })
                });

                if (!res.ok) {
                    baoLoi(res.status === 403 ? 'Bạn không có quyền đổi đơn vị của đơn.' : 'Máy chủ trả về lỗi ' + res.status + '.', true);
                    return;
                }

                const data = await res.json();
                if (!data.success) {
                    baoLoi(data.message || 'Không lưu được.', true);
                    return;
                }

                // Cập nhật đúng vùng bị ảnh hưởng, không tải lại trang
                const oBoPhan = document.getElementById('txtBoPhanDon');
                const oCongTy = document.getElementById('txtTenCongTyDon');
                if (oBoPhan) oBoPhan.textContent = data.boPhan || '';
                if (oCongTy) oCongTy.textContent = data.tenCongTy || '---';

                baoLoi(data.message || 'Đã cập nhật.', false);
            } catch (err) {
                baoLoi('Lỗi kết nối máy chủ.', true);
            } finally {
                btnLuu.disabled = false;
                btnLuu.innerHTML = nhanCu;
            }
        });
    });
})();
