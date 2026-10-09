// /BaoCaoDinhKy — gom các báo cáo định kì thành tab trên một trang. 3 kiểu tab (data-kieu):
//  - tong-quan: Camera / AP / Switch — nạp partial _TongQuanNoiDung qua AJAX vào #bcDinhKy rồi giao cho
//    camera|ap|switch-tong-quan.js khởi tạo; kỳ và phạm vi công ty đang chọn được chép sang tab mới.
//  - thiet-bi: số liệu /QLKiemKe/GetKkThietBis vẽ bằng kiemke-tong-quan.js (cùng tab Tổng quan của trang Quản lý Thiết bị).
//  - iframe: trang thống kê sẵn có mở với ?nhung=1 (script inline, trùng id giữa các trang nên phải tách tài liệu).
(function () {
    'use strict';

    var luotNap = 0;          // bấm tab liên tiếp: chỉ lượt cuối được vẽ
    var boLocTongQuan = null; // bộ lọc lần cuối của nhóm tab tong-quan (giữ khi đi qua tab khác rồi quay lại)
    var daNapThietBi = false;

    function $id(id) { return document.getElementById(id); }

    function urlTrang(loai) {
        return function (cty) {
            return '/BaoCaoDinhKy?loai=' + encodeURIComponent(loai) + (cty ? '&congTy=' + encodeURIComponent(cty) : '');
        };
    }

    function doiUrl(loai) {
        try { history.replaceState(null, '', urlTrang(loai)('')); } catch (e) { /* chỉ mất bookmark đúng tab */ }
    }

    // Lấy bộ lọc của tab tong-quan đang hiện (chưa có thì null)
    function layBoLoc() {
        if (!$id('bcKy')) return null;
        return { ky: $id('bcKy').value, tu: $id('bcTuNgay').value, den: $id('bcDenNgay').value, congTy: $id('bcCongTy').value };
    }

    function chonCongTy(cty) {
        var el = $id('bcCongTy');
        if (!el || !cty) return;
        var co = Array.prototype.some.call(el.options, function (o) { return o.value === cty; });
        if (co) el.value = cty;
    }

    function hienLoi(khung, noiDung) {
        var div = document.createElement('div');
        div.className = 'alert alert-danger m-3';
        div.textContent = noiDung;
        khung.replaceChildren(div);
    }

    function hienPane(id) {
        document.querySelectorAll('.bc-dinh-ky .bc-pane').forEach(function (p) {
            p.classList.toggle('d-none', p.id !== id);
        });
    }

    function napTongQuan(nut, loai, congTyBanDau) {
        var khung = $id('bcDinhKy');
        var boLoc = layBoLoc() || boLocTongQuan;
        var luot = ++luotNap;
        hienPane('bcDinhKy');
        khung.setAttribute('aria-busy', 'true');

        fetch(nut.getAttribute('data-url'), { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (res) {
                if (!res.ok) throw new Error(res.status === 403 ? 'Bạn không có quyền xem báo cáo này.' : 'HTTP ' + res.status);
                return res.text();
            })
            .then(function (html) {
                if (luot !== luotNap) return;
                khung.innerHTML = html;
                if (boLoc) {
                    $id('bcKy').value = boLoc.ky;
                    $id('bcTuNgay').value = boLoc.tu;
                    $id('bcDenNgay').value = boLoc.den;
                    chonCongTy(boLoc.congTy);
                } else {
                    chonCongTy(congTyBanDau);
                }
                var khoiTao = window.BaoCaoTongQuan && window.BaoCaoTongQuan[loai];
                if (!khoiTao) throw new Error('Thiếu script báo cáo ' + loai + '.');
                khoiTao({ giuKy: !!boLoc, urlTrang: urlTrang(loai) });
            })
            .catch(function (e) {
                if (luot === luotNap) hienLoi(khung, 'Không tải được báo cáo: ' + e.message);
            })
            .then(function () {
                if (luot === luotNap) khung.removeAttribute('aria-busy');
            });
    }

    function napThietBi(nut) {
        hienPane('bcPaneThietBi');
        if (daNapThietBi) return;
        daNapThietBi = true;
        var goc = $id('tqTongQuan');

        fetch(nut.getAttribute('data-url'), { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function (res) {
                if (!res || !res.success) throw new Error((res && res.message) || 'máy chủ báo lỗi');
                if (!window.TongQuanKiemKe) throw new Error('thiếu script kiemke-tong-quan.js');
                window.TongQuanKiemKe.render(res.data || []);
            })
            .catch(function (e) {
                daNapThietBi = false;   // lỗi thì lần mở tab sau thử lại
                hienLoi(goc, 'Không tải được số liệu thiết bị: ' + e.message);
            });
    }

    // Iframe cùng origin nên đọc được chiều cao nội dung; trang con vẽ biểu đồ/đổi bộ lọc thì khung tự giãn theo
    function canChieuCao(khung) {
        var doc;
        try { doc = khung.contentDocument; } catch (e) { return; }
        if (!doc || !doc.body) return;
        // Đo body chứ không đo documentElement: html luôn cao bằng khung nên nội dung co lại thì khung không co theo
        var dat = function () {
            var cao = Math.ceil(doc.body.getBoundingClientRect().height);
            if (cao > 0) khung.style.height = cao + 'px';
        };
        dat();
        if (window.ResizeObserver) new ResizeObserver(dat).observe(doc.body);
    }

    function napIframe(nut, loai) {
        var pane = $id('bcPaneIframe');
        hienPane('bcPaneIframe');
        var khung = null;
        Array.prototype.forEach.call(pane.children, function (f) {
            var laTab = f.getAttribute('data-loai') === loai;
            f.classList.toggle('d-none', !laTab);
            if (laTab) khung = f;
        });
        if (khung) return;

        khung = document.createElement('iframe');
        khung.className = 'bc-iframe';
        khung.setAttribute('data-loai', loai);
        khung.title = nut.getAttribute('data-ten') || loai;
        khung.addEventListener('load', function () { canChieuCao(khung); });
        khung.src = nut.getAttribute('data-url');
        pane.appendChild(khung);
    }

    function moTab(nut, congTyBanDau) {
        var loai = nut.getAttribute('data-loai');
        var kieu = nut.getAttribute('data-kieu');

        document.querySelectorAll('.bc-tab').forEach(function (t) {
            var dangChon = t === nut;
            t.classList.toggle('active', dangChon);
            t.setAttribute('aria-selected', dangChon ? 'true' : 'false');
        });

        // Rời nhóm tong-quan: nhớ bộ lọc để lúc quay lại vẫn đúng kỳ/phạm vi
        if (kieu !== 'tong-quan') {
            boLocTongQuan = layBoLoc() || boLocTongQuan;
            ++luotNap;          // bỏ lượt nạp tong-quan còn dở
            $id('bcDinhKy').removeAttribute('aria-busy');
            doiUrl(loai);
        }

        if (kieu === 'tong-quan') napTongQuan(nut, loai, congTyBanDau);
        else if (kieu === 'thiet-bi') napThietBi(nut);
        else napIframe(nut, loai);
    }

    // yyyy-MM-dd theo giờ máy (toISOString lệch ngày do UTC)
    function ngayInput(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }

    // Excel tổng hợp: kỳ + phạm vi theo bộ lọc Camera/AP/Switch đang chọn (hoặc lần chọn cuối), chưa có thì 7 ngày gần nhất.
    // Tải file là điều hướng hợp lệ (trang giữ nguyên); file lớn mất vài giây nên khoá nút một lúc tránh bấm lặp.
    function xuatTongHop(nut) {
        var boLoc = layBoLoc() || boLocTongQuan;
        var den = new Date(), tu = new Date();
        tu.setDate(tu.getDate() - 6);
        var tuNgay = (boLoc && boLoc.tu) || ngayInput(tu);
        var denNgay = (boLoc && boLoc.den) || ngayInput(den);
        var cty = (boLoc && boLoc.congTy) || '';

        nut.disabled = true;
        var cu = nut.innerHTML;
        nut.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Đang tạo file...';
        setTimeout(function () { nut.disabled = false; nut.innerHTML = cu; }, 8000);

        window.location.href = '/BaoCaoDinhKy/XuatExcel?tuNgay=' + encodeURIComponent(tuNgay) + '&denNgay=' + encodeURIComponent(denNgay)
            + (cty ? '&congTy=' + encodeURIComponent(cty) : '');
    }

    document.addEventListener('click', function (e) {
        var xuat = e.target.closest('#bcXuatTongHop');
        if (xuat) { e.preventDefault(); xuatTongHop(xuat); return; }

        var nut = e.target.closest('.bc-tab');
        if (!nut || nut.classList.contains('active')) return;
        e.preventDefault();
        moTab(nut);
    });

    document.addEventListener('DOMContentLoaded', function () {
        var khung = $id('bcDinhKy');
        if (!khung) return;
        var nut = document.querySelector('.bc-tab[data-loai="' + khung.getAttribute('data-loai') + '"]')
            || document.querySelector('.bc-tab');
        moTab(nut, khung.getAttribute('data-cong-ty'));
    });
})();
