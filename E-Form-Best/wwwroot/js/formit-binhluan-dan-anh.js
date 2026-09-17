// Dán (Ctrl+V) hoặc kéo-thả ảnh/file vào khung bình luận đơn IT để đính kèm nhanh.
// Ảnh chụp màn hình từ clipboard không có tên file nên phải tự đặt tên, nếu không
// server lưu ra file không phần mở rộng và trình duyệt không hiển thị được ảnh.
(function () {
    'use strict';

    var GIOI_HAN_BYTE = 50 * 1024 * 1024;

    function dinhDangDungLuong(byte) {
        return (byte / 1048576).toFixed(2) + ' MB';
    }

    function datTenAnhDan(blob) {
        if (blob.name && blob.name !== 'image.png' && blob.name.indexOf('.') > 0) return blob;
        var duoi = (blob.type.split('/')[1] || 'png').replace('jpeg', 'jpg');
        var d = new Date();
        var ten = 'anh-dan-' +
            d.getFullYear() +
            String(d.getMonth() + 1).padStart(2, '0') +
            String(d.getDate()).padStart(2, '0') + '_' +
            String(d.getHours()).padStart(2, '0') +
            String(d.getMinutes()).padStart(2, '0') +
            String(d.getSeconds()).padStart(2, '0') + '.' + duoi;
        return new File([blob], ten, { type: blob.type });
    }

    function khoiTao() {
        var form = document.getElementById('formBinhLuan');
        var textarea = document.getElementById('txtBinhLuan');
        var input = document.getElementById('fileBinhLuan');
        var dropZone = document.getElementById('dropZoneBinhLuan');
        var preview = document.getElementById('previewFileBinhLuan');
        if (!form || !input || !preview) return;

        var urlDangXem = null;

        function xoaPreview() {
            if (urlDangXem) { URL.revokeObjectURL(urlDangXem); urlDangXem = null; }
            preview.innerHTML = '';
            preview.style.display = 'none';
        }

        function vePreview(file) {
            xoaPreview();
            if (!file) return;

            var box = document.createElement('div');
            box.style.cssText = 'display:inline-flex; align-items:center; gap:10px; padding:8px 12px; background:#f8fafc; border:1px solid #e2e8f0; border-radius:12px; max-width:100%;';

            if (file.type.indexOf('image/') === 0) {
                urlDangXem = URL.createObjectURL(file);
                var img = document.createElement('img');
                img.src = urlDangXem;
                img.style.cssText = 'width:64px; height:64px; object-fit:cover; border-radius:8px; flex-shrink:0; cursor:zoom-in;';
                img.title = 'Bấm để xem ảnh lớn';
                // Dùng lại khung xem ảnh phóng to sẵn có của trang chi tiết
                img.addEventListener('click', function () {
                    if (typeof window.openZoom === 'function') window.openZoom(urlDangXem);
                    else window.open(urlDangXem, '_blank');
                });
                box.appendChild(img);
            } else {
                var icon = document.createElement('i');
                icon.className = 'fa fa-paperclip';
                icon.style.cssText = 'color:#64748b; font-size:18px;';
                box.appendChild(icon);
            }

            var info = document.createElement('div');
            info.style.cssText = 'display:flex; flex-direction:column; min-width:0;';
            var ten = document.createElement('span');
            ten.textContent = file.name;
            ten.style.cssText = 'font-size:13px; font-weight:700; color:#0f172a; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; max-width:220px;';
            var size = document.createElement('span');
            size.textContent = dinhDangDungLuong(file.size);
            size.style.cssText = 'font-size:11px; color:#94a3b8;';
            info.appendChild(ten);
            info.appendChild(size);
            box.appendChild(info);

            var btnXoa = document.createElement('button');
            btnXoa.type = 'button';
            btnXoa.title = 'Bỏ đính kèm';
            btnXoa.innerHTML = '<i class="fa fa-times"></i>';
            btnXoa.style.cssText = 'border:none; background:#fee2e2; color:#ef4444; width:28px; height:28px; border-radius:50%; cursor:pointer; flex-shrink:0;';
            btnXoa.addEventListener('click', function () { ganFile(null); });
            box.appendChild(btnXoa);

            preview.appendChild(box);
            preview.style.display = 'block';
        }

        // Gán file vào <input type=file> để form submit gửi kèm; bắn 'change' để
        // nhãn "Đính kèm file" trong view cập nhật theo đúng luồng cũ.
        function ganFile(file) {
            if (file && file.size > GIOI_HAN_BYTE) {
                alert('Tối đa 50MB');
                return;
            }
            var dt = new DataTransfer();
            if (file) dt.items.add(file);
            input.files = dt.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
            vePreview(file);
        }

        if (textarea) {
            textarea.addEventListener('paste', function (e) {
                var cd = e.clipboardData || window.clipboardData;
                if (!cd) return;
                var items = cd.items || [];
                for (var i = 0; i < items.length; i++) {
                    if (items[i].kind !== 'file') continue;
                    var blob = items[i].getAsFile();
                    if (!blob) continue;
                    // Chặn dán mặc định để trình duyệt không chèn thêm gì vào textarea
                    e.preventDefault();
                    ganFile(blob.type.indexOf('image/') === 0 ? datTenAnhDan(blob) : blob);
                    return;
                }
            });
        }

        if (dropZone) {
            var sang = function (on) {
                dropZone.style.borderColor = on ? '#3b82f6' : '#cbd5e1';
                dropZone.style.background = on ? '#eff6ff' : '#f8fafc';
            };
            ['dragenter', 'dragover'].forEach(function (evt) {
                form.addEventListener(evt, function (e) { e.preventDefault(); e.stopPropagation(); sang(true); });
            });
            ['dragleave', 'dragend'].forEach(function (evt) {
                form.addEventListener(evt, function (e) {
                    e.preventDefault(); e.stopPropagation();
                    if (evt === 'dragleave' && form.contains(e.relatedTarget)) return;
                    sang(false);
                });
            });
            form.addEventListener('drop', function (e) {
                e.preventDefault(); e.stopPropagation(); sang(false);
                var f = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
                if (f) ganFile(f);
            });
        }

        // Chọn file bằng nút thì cũng vẽ preview; form.reset() sau khi gửi thì xoá preview.
        input.addEventListener('change', function (e) {
            if (e.isTrusted) vePreview(input.files[0] || null);
        });
        form.addEventListener('reset', function () { setTimeout(xoaPreview, 0); });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', khoiTao);
    else khoiTao();
})();
