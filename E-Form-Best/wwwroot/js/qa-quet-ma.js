// Quét mã vạch / QR bằng camera (điện thoại hoặc webcam) cho phân hệ QA.
//   qaQuetMa([tieuDe]) -> Promise<string | null>  (null = đóng hộp, không quét được gì)
// Thư viện html5-qrcode (~370 KB) chỉ tải khi bấm quét lần đầu, trang không phải tải sẵn.
// Camera chỉ mở được trên https hoặc localhost (production đi qua nginx https nên dùng được).
(function () {
    var THU_VIEN = 'https://cdn.jsdelivr.net/npm/html5-qrcode@2.3.8/html5-qrcode.min.js';
    var dangTai = null;

    function taiThuVien() {
        if (window.Html5Qrcode) return Promise.resolve();
        if (dangTai) return dangTai;
        dangTai = new Promise(function (xong, loi) {
            var s = document.createElement('script');
            s.src = THU_VIEN;
            s.onload = function () { xong(); };
            s.onerror = function () { dangTai = null; loi(new Error('Không tải được thư viện quét mã (kiểm tra mạng Internet).')); };
            document.head.appendChild(s);
        });
        return dangTai;
    }

    function dinhDang() {
        var F = window.Html5QrcodeSupportedFormats;
        // Tem thùng in CODE128 (mã thùng / tên thùng) và QR; thêm vài loại phổ biến cho tem khác
        return F ? [F.CODE_128, F.QR_CODE, F.CODE_39, F.EAN_13, F.DATA_MATRIX] : undefined;
    }

    window.qaQuetMa = function (tieuDe) {
        return new Promise(function (traVe) {
            var nen = document.createElement('div');
            nen.className = 'qa-ht-nen qa-quet-nen';
            var hop = document.createElement('div');
            hop.className = 'qa-ht-hop qa-quet-hop';
            hop.setAttribute('role', 'dialog');
            hop.setAttribute('aria-modal', 'true');
            var tde = document.createElement('div');
            tde.className = 'qa-quet-tieude';
            tde.innerHTML = '<i class="fa fa-camera"></i> ';
            tde.appendChild(document.createTextNode(tieuDe || 'Quét mã'));
            var vung = document.createElement('div');
            vung.id = 'qa-quet-vung-' + Date.now();
            vung.className = 'qa-quet-vung';
            var tt = document.createElement('div');
            tt.className = 'qa-quet-tt';
            tt.textContent = 'Đang mở camera…';
            var chonCam = document.createElement('select');
            chonCam.className = 'form-select form-select-sm qa-quet-cam';
            chonCam.hidden = true;
            chonCam.title = 'Chọn camera';
            var thanhNut = document.createElement('div');
            thanhNut.className = 'qa-ht-nut';
            var nutDong = document.createElement('button');
            nutDong.type = 'button';
            nutDong.className = 'btn btn-outline-secondary';
            nutDong.textContent = 'Đóng';
            thanhNut.appendChild(nutDong);
            hop.append(tde, vung, tt, chonCam, thanhNut);
            nen.appendChild(hop);
            document.body.appendChild(nen);

            var qr = null, daXong = false;
            async function dung() {
                if (!qr) return;
                try { if (qr.isScanning) await qr.stop(); } catch (e) { /* camera đã tắt */ }
                try { qr.clear(); } catch (e) { /* bỏ qua */ }
            }
            async function ket(ma) {
                if (daXong) return;
                daXong = true;
                document.removeEventListener('keydown', phim, true);
                await dung();
                nen.remove();
                traVe(ma);
            }
            function phim(e) { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); ket(null); } }
            document.addEventListener('keydown', phim, true);
            nutDong.addEventListener('click', function () { ket(null); });

            function baoLoi(text) {
                tt.textContent = text;
                tt.classList.add('loi');
            }
            async function batCamera(camera) {
                await dung();
                if (daXong) return;
                tt.classList.remove('loi');
                tt.textContent = 'Đưa mã vạch / QR vào giữa khung…';
                qr = new window.Html5Qrcode(vung.id, { formatsToSupport: dinhDang(), verbose: false,
                    experimentalFeatures: { useBarCodeDetectorIfSupported: true } });
                await qr.start(camera, {
                    fps: 10,
                    // Khung ngắm rộng, thấp: hợp mã vạch CODE128 dài trên tem thùng
                    qrbox: function (w, h) { var r = Math.floor(Math.min(w * 0.9, 420)); return { width: r, height: Math.floor(Math.min(h * 0.6, r * 0.6)) }; }
                }, function (text) {
                    if (navigator.vibrate) try { navigator.vibrate(80); } catch (e) { /* bỏ qua */ }
                    ket((text || '').trim() || null);
                }, function () { /* khung chưa thấy mã: bỏ qua */ });
            }

            if (!window.isSecureContext || !navigator.mediaDevices) {
                baoLoi('Trình duyệt chỉ cho mở camera khi trang chạy https. Mở hệ thống bằng địa chỉ https rồi thử lại.');
                return;
            }
            taiThuVien().then(async function () {
                if (daXong) return;
                // Ưu tiên camera sau (điện thoại); máy có nhiều camera thì cho chọn
                try {
                    await batCamera({ facingMode: 'environment' });
                } catch (e) {
                    baoLoi('Không mở được camera: ' + (e && e.name === 'NotAllowedError' ? 'chưa cho phép quyền camera (bấm biểu tượng ổ khoá trên thanh địa chỉ để cho phép).' : (e && e.message ? e.message : e)));
                    return;
                }
                try {
                    var ds = await window.Html5Qrcode.getCameras();
                    if (ds && ds.length > 1 && !daXong) {
                        var dau = document.createElement('option');
                        dau.value = '';
                        dau.textContent = 'Camera sau (mặc định)';
                        chonCam.appendChild(dau);
                        ds.forEach(function (c) {
                            var op = document.createElement('option');
                            op.value = c.id;
                            op.textContent = c.label || ('Camera ' + c.id.slice(0, 6));
                            chonCam.appendChild(op);
                        });
                        chonCam.hidden = false;
                        chonCam.addEventListener('change', function () {
                            batCamera(chonCam.value ? chonCam.value : { facingMode: 'environment' })
                                .catch(function (e) { baoLoi('Không mở được camera này: ' + (e && e.message ? e.message : e)); });
                        });
                    }
                } catch (e) { /* không liệt kê được camera: vẫn dùng camera mặc định */ }
            }, function (e) { baoLoi(e.message); });
        });
    };
})();
