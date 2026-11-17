// Hành vi chung của layout QA: bật/tắt sidebar trên mobile và menu tài khoản.
(function () {
    document.addEventListener('click', function (e) {
        var body = document.body;

        if (e.target.closest('#qa-toggle')) {
            body.classList.toggle('qa-sidebar-open');
            return;
        }
        if (e.target.closest('#qa-backdrop')) {
            body.classList.remove('qa-sidebar-open');
            return;
        }

        var user = document.querySelector('.qa-user');
        if (!user) return;
        if (e.target.closest('#qa-user-btn')) {
            user.classList.toggle('open');
        } else if (!e.target.closest('#qa-user-menu')) {
            user.classList.remove('open');
        }
    });

    // Công tắc "Bôi đen để copy": bật thì gắn lớp qa-che-do-copy lên <html>, các trang (qa-thung, qa-tra-cuu-mes)
    // bỏ qua kéo-tích dòng để chuột bôi đen chữ như bình thường. Không nhớ: mỗi lần tải trang mặc định bật.
    var congTacCopy = document.getElementById('qa-che-do-copy');
    function apCheDoCopy(bat) {
        document.documentElement.classList.toggle('qa-che-do-copy', bat);
        if (congTacCopy) congTacCopy.checked = bat;
    }
    apCheDoCopy(true);
    try { localStorage.removeItem('qa-che-do-copy'); } catch (e) { /* dọn giá trị bản trước đã lưu */ }
    if (congTacCopy) congTacCopy.addEventListener('change', function () { apCheDoCopy(congTacCopy.checked); });

    // Bảng QA nhiều cột: mặc định chỉ hiện cột quan trọng (th/td có lớp qa-cot-phu bị ẩn);
    // nút [data-hien-cot="<id bảng>"] bật/tắt, nhớ theo từng bảng trên trình duyệt này
    function apHienCot(nut, hienHet) {
        var bang = document.getElementById(nut.dataset.hienCot);
        if (!bang) return;
        bang.classList.toggle('qa-hien-het', hienHet);
        nut.classList.toggle('active', hienHet);
        var chu = nut.querySelector('span');
        if (chu) chu.textContent = hienHet ? 'Chỉ cột chính' : 'Hiện tất cả cột';
    }
    document.querySelectorAll('[data-hien-cot]').forEach(function (nut) {
        var hien = false;
        try { hien = localStorage.getItem('qa-hien-cot-' + nut.dataset.hienCot) === '1'; } catch (e) { /* storage bị chặn: dùng mặc định */ }
        apHienCot(nut, hien);
    });
    document.addEventListener('click', function (e) {
        var nut = e.target.closest('[data-hien-cot]');
        if (!nut) return;
        var bang = document.getElementById(nut.dataset.hienCot);
        if (!bang) return;
        var hien = !bang.classList.contains('qa-hien-het');
        apHienCot(nut, hien);
        try { localStorage.setItem('qa-hien-cot-' + nut.dataset.hienCot, hien ? '1' : '0'); } catch (e2) { /* bỏ qua */ }
    });

    // Ô .qa-copy (mã phối màu...): bấm = copy chữ trong ô. Chạy pha capture và chặn lan để không tích dòng / mở chi tiết.
    // Máy chủ chạy http qua IP (không phải ngữ cảnh an toàn) thì navigator.clipboard không có -> dùng execCommand.
    function copyChu(text) {
        // API mới bị từ chối (trang mất focus, quyền...) thì thử cách cũ
        if (navigator.clipboard && window.isSecureContext)
            return navigator.clipboard.writeText(text).catch(function () { return copyCu(text); });
        return copyCu(text);
    }
    function copyCu(text) {
        return new Promise(function (xong, loi) {
            var ta = document.createElement('textarea');
            ta.value = text;
            ta.setAttribute('readonly', '');
            ta.style.cssText = 'position:fixed;top:0;left:0;opacity:0;';
            document.body.appendChild(ta);
            ta.select();
            var ok = false;
            try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
            ta.remove();
            if (ok) xong(); else loi(new Error('Trình duyệt chặn copy'));
        });
    }
    document.addEventListener('click', function (e) {
        var o = e.target.closest('.qa-copy');
        if (!o) return;
        // Đang bôi đen chữ trong ô để copy tay thì để mặc định
        var chon = window.getSelection ? String(window.getSelection()) : '';
        if (chon && o.contains(window.getSelection().anchorNode)) return;
        var text = (o.textContent || '').trim();
        if (!text) return;
        e.preventDefault();
        e.stopPropagation();
        copyChu(text).then(function () {
            o.classList.add('da-copy');
            setTimeout(function () { o.classList.remove('da-copy'); }, 900);
            if (window.qaThongBao) window.qaThongBao('Đã copy: ' + text, 'ok');
        }, function (err) {
            if (window.qaThongBao) window.qaThongBao('Không copy được: ' + err.message, 'loi');
        });
    }, true);

    // Điện thoại: bảng .qa-mes-bang hiện dạng thẻ (CSS @media trong qa-layout.css), mỗi ô cần nhãn cột.
    // Gắn data-nhan = chữ tiêu đề cột tương ứng mỗi khi tbody được vẽ lại; màn rộng không dùng nhãn này.
    function tenCot(th) {
        var t = '';
        th.childNodes.forEach(function (n) { if (n.nodeType === 3 || (n.nodeType === 1 && !n.matches('button, input'))) t += n.textContent; });
        return t.trim();
    }
    function ganNhan(bang) {
        if (!bang.tHead || !bang.tHead.rows[0]) return;
        var ten = Array.prototype.map.call(bang.tHead.rows[0].cells, tenCot);
        Array.prototype.forEach.call(bang.tBodies, function (tb) {
            Array.prototype.forEach.call(tb.rows, function (tr) {
                if (tr.cells.length < 2) return; // dòng "chưa có dữ liệu" (1 ô colspan)
                Array.prototype.forEach.call(tr.cells, function (td, i) {
                    if (ten[i] && td.dataset.nhan !== ten[i]) td.dataset.nhan = ten[i];
                });
            });
        });
    }
    document.querySelectorAll('table.qa-mes-bang').forEach(function (bang) {
        ganNhan(bang);
        Array.prototype.forEach.call(bang.tBodies, function (tb) {
            new MutationObserver(function () { ganNhan(bang); }).observe(tb, { childList: true });
        });
    });
})();
