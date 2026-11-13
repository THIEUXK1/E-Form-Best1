// Hộp xác nhận / thông báo hiện GIỮA màn hình cho phân hệ QA, thay confirm()/alert() của trình duyệt
// (hộp gốc của Chrome nằm sát mép trên, dễ bị bỏ qua).
//   qaXacNhan(text[, { nutDongY, nguyHiem }]) -> Promise<boolean>
//   qaThongBao(text, 'ok' | 'loi')  : ok = hiện giữa màn hình rồi tự tắt; loi = hộp có nút Đóng
(function () {
    function taoHop(text, loai, coHuy, tuyChon) {
        tuyChon = tuyChon || {};
        return new Promise(function (xong) {
            var nen = document.createElement('div');
            nen.className = 'qa-ht-nen';
            var hop = document.createElement('div');
            hop.className = 'qa-ht-hop qa-ht-' + loai;
            hop.setAttribute('role', coHuy ? 'alertdialog' : 'alert');
            hop.setAttribute('aria-modal', 'true');

            var icon = document.createElement('i');
            icon.className = 'qa-ht-icon fa ' + (loai === 'ok' ? 'fa-check-circle' : loai === 'loi' ? 'fa-times-circle' : 'fa-question-circle');
            var noiDung = document.createElement('div');
            noiDung.className = 'qa-ht-noidung';
            noiDung.textContent = text || '';
            hop.appendChild(icon);
            hop.appendChild(noiDung);

            var thanhNut = document.createElement('div');
            thanhNut.className = 'qa-ht-nut';
            var nutHuy = null;
            if (coHuy) {
                nutHuy = document.createElement('button');
                nutHuy.type = 'button';
                nutHuy.className = 'btn btn-outline-secondary';
                nutHuy.textContent = 'Huỷ';
                thanhNut.appendChild(nutHuy);
            }
            var nutOk = document.createElement('button');
            nutOk.type = 'button';
            nutOk.className = 'btn ' + (tuyChon.nguyHiem ? 'btn-danger' : coHuy ? 'btn-primary' : 'btn-secondary');
            nutOk.textContent = tuyChon.nutDongY || (coHuy ? 'Đồng ý' : 'Đóng');
            thanhNut.appendChild(nutOk);
            hop.appendChild(thanhNut);
            nen.appendChild(hop);

            var focusCu = document.activeElement;
            function dong(kq) {
                document.removeEventListener('keydown', phim, true);
                nen.remove();
                if (focusCu && focusCu.focus) try { focusCu.focus(); } catch (e) { /* bỏ qua */ }
                xong(kq);
            }
            function phim(e) {
                if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); dong(false); }
                else if (e.key === 'Enter' && document.activeElement !== nutHuy) { e.preventDefault(); e.stopPropagation(); dong(true); }
            }
            nutOk.addEventListener('click', function () { dong(true); });
            if (nutHuy) nutHuy.addEventListener('click', function () { dong(false); });
            // Bấm ra ngoài: chỉ đóng hộp thông báo; hộp xác nhận phải chọn rõ ràng
            nen.addEventListener('click', function (e) { if (e.target === nen && !coHuy) dong(true); });
            document.addEventListener('keydown', phim, true);
            document.body.appendChild(nen);
            nutOk.focus();
        });
    }

    window.qaXacNhan = function (text, tuyChon) { return taoHop(text, 'hoi', true, tuyChon); };

    window.qaThongBao = function (text, loai) {
        if (!text) return;
        if (loai === 'loi') return taoHop(text, 'loi', false);
        // Báo thành công: không chặn thao tác, tự mờ dần sau 2,2 giây
        var cu = document.querySelector('.qa-ht-nhanh');
        if (cu) cu.remove();
        var el = document.createElement('div');
        el.className = 'qa-ht-nhanh';
        el.setAttribute('role', 'status');
        var icon = document.createElement('i');
        icon.className = 'fa fa-check-circle';
        var span = document.createElement('span');
        span.textContent = text;
        el.appendChild(icon);
        el.appendChild(span);
        document.body.appendChild(el);
        setTimeout(function () { el.classList.add('an'); }, 2200);
        setTimeout(function () { el.remove(); }, 2600);
    };
})();
