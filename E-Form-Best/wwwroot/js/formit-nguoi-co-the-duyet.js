// Chi tiết đơn IT: nút "mắt" ở bước Quản lý (chỉ quyền All) — bật/tắt danh sách người có thể duyệt đơn.
(function () {
    let daTai = false;

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.js-xem-nguoi-duyet');
        if (!btn) return;
        e.preventDefault();

        const khung = document.getElementById('dsNguoiCoTheDuyet');
        if (!khung) return;

        // Đã tải rồi thì chỉ bật/tắt, không gọi lại server
        if (daTai) {
            khung.style.display = khung.style.display === 'none' ? 'block' : 'none';
            return;
        }

        btn.disabled = true;
        khung.style.display = 'block';
        khung.textContent = 'Đang tải...';

        try {
            const res = await fetch('/FormIT/NguoiCoTheDuyet/' + encodeURIComponent(btn.dataset.id));
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const data = await res.json();
            khung.textContent = '';

            if (!data.thanhCong) {
                khung.textContent = data.thongBao || 'Không tải được danh sách.';
                return;
            }

            const tieuDe = document.createElement('div');
            tieuDe.style.cssText = 'font-weight:800; margin-bottom:6px; color:#0f172a;';
            tieuDe.textContent = 'Người có thể duyệt — ' + (data.duLieu.congTy || '?') + ' / ' + (data.duLieu.boPhan || '?');
            khung.appendChild(tieuDe);

            const ds = data.duLieu.danhSach || [];
            if (ds.length === 0) {
                const trong = document.createElement('div');
                trong.style.color = '#b45309';
                trong.textContent = 'Chưa có ai được gán quyền duyệt cho bộ phận này (chỉ quyền All duyệt được).';
                khung.appendChild(trong);
            } else {
                const ul = document.createElement('ul');
                ul.style.cssText = 'margin:0; padding-left:18px;';
                ds.forEach(function (u) {
                    const li = document.createElement('li');
                    li.textContent = u.hoTen + (u.taiKhoan ? ' (' + u.taiKhoan + ')' : '');
                    ul.appendChild(li);
                });
                khung.appendChild(ul);
            }
            daTai = true;
        } catch (err) {
            khung.textContent = 'Lỗi tải danh sách người duyệt.';
        } finally {
            btn.disabled = false;
        }
    });
})();
