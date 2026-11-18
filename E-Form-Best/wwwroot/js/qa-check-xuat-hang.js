// Trang QA "Check xuất hàng": tra barcode / mã lot / phiếu xuất / hoá đơn qua /QAForm/CheckXuatHang/DuLieu
// (server gọi NH-WMS 出仓查询 + NH-MES 订单综合查询), vẽ bảng theo lot (cột của báo cáo khiếu nại) và bảng chi tiết cuộn.
(function () {
    var form = document.getElementById('qa-xh-form');
    if (!form) return;
    var oMa = document.getElementById('qa-xh-ma');
    var oLoai = document.getElementById('qa-xh-loai');
    var nut = document.getElementById('qa-xh-nut');
    var thongBao = document.getElementById('qa-xh-thongbao');
    var tbLot = document.getElementById('qa-xh-tbody-lot');
    var tbXuat = document.getElementById('qa-xh-tbody-xuat');
    var tongXuat = document.getElementById('qa-xh-tong-xuat');
    var nutChep = document.getElementById('qa-xh-chep');
    var dsLotHienTai = [];
    var dangTra = null;

    var TEN_LOAI = { Barcode: 'barcode', MaLot: 'mã lot', PhieuXuat: 'phiếu xuất', HoaDon: 'hoá đơn' };

    function bao(chu, kieu) {
        thongBao.textContent = chu || '';
        thongBao.className = 'qa-mes-thongbao' + (kieu ? ' ' + kieu : '');
    }

    function so(x, le) {
        if (x === null || x === undefined || x === '') return '';
        return Number(x).toLocaleString('vi-VN', { maximumFractionDigits: le === undefined ? 2 : le });
    }

    function o(tr, giaTri, lop) {
        var td = document.createElement('td');
        td.textContent = giaTri === null || giaTri === undefined ? '' : giaTri;
        if (lop) td.className = lop;
        tr.appendChild(td);
        return td;
    }

    function dongTrong(tbody, soCot, chu) {
        var tr = document.createElement('tr');
        var td = document.createElement('td');
        td.colSpan = soCot;
        td.className = 'qa-mes-trong';
        td.textContent = chu;
        tr.appendChild(td);
        tbody.replaceChildren(tr);
    }

    // Nhà máy / khách cuối: mã kèm tên (tên chỉ có khi MES trả đơn hàng)
    function maTen(ma, ten) { return ten ? (ma || '') + ' - ' + ten : (ma || ''); }

    function veLot(ds) {
        dsLotHienTai = ds;
        nutChep.disabled = ds.length === 0;
        if (!ds.length) { dongTrong(tbLot, 17, 'Không có mã lot nào.'); return; }
        var frag = document.createDocumentFragment();
        ds.forEach(function (d, i) {
            var tr = document.createElement('tr');
            if (!d.coDonHang) tr.className = 'qa-xh-thieu';
            o(tr, i + 1);
            o(tr, maTen(d.nhaMay, d.tenNhaMay));
            o(tr, maTen(d.khachCuoi, d.tenKhachCuoi));
            o(tr, d.po);
            o(tr, d.maLot, 'qa-xh-dam');
            o(tr, d.maHangGoc);
            o(tr, d.maHang);
            o(tr, d.mauKhach);
            o(tr, d.maMauNoiBo);
            o(tr, so(d.slXuat), 'qa-xh-so qa-xh-dam');
            o(tr, so(d.slKhach), 'qa-xh-so');
            o(tr, d.soCuon, 'qa-xh-so');
            o(tr, (d.dsHoaDon || []).join(', '));
            o(tr, d.ngayXuatDau === d.ngayXuatCuoi ? d.ngayXuatDau : (d.ngayXuatDau || '') + ' → ' + (d.ngayXuatCuoi || ''));
            o(tr, d.maHangKhach);
            o(tr, d.mua);
            o(tr, d.coDonHang ? d.trangThai : 'Không thấy đơn trên NH-MES');
            frag.appendChild(tr);
        });
        tbLot.replaceChildren(frag);
    }

    function veXuat(ds, tong) {
        tongXuat.textContent = 'Chi tiết xuất kho (出仓): ' + ds.length.toLocaleString('vi-VN') + ' cuộn' +
            (tong > ds.length ? ' / ' + tong.toLocaleString('vi-VN') + ' dòng' : '');
        if (!ds.length) { dongTrong(tbXuat, 18, 'Chưa có dữ liệu xuất kho.'); return; }
        var frag = document.createDocumentFragment();
        ds.forEach(function (d, i) {
            var tr = document.createElement('tr');
            o(tr, i + 1);
            o(tr, d.hoaDon);
            o(tr, d.phieuXuat);
            o(tr, d.barcode, 'qa-xh-dam');
            o(tr, d.maLot);
            o(tr, d.maKhachHang);
            o(tr, d.khachCuoi);
            o(tr, d.maHang);
            o(tr, d.maMau);
            o(tr, d.loai);
            o(tr, so(d.slXuat), 'qa-xh-so');
            o(tr, so(d.slKhach), 'qa-xh-so');
            o(tr, so(d.klTinh, 3), 'qa-xh-so');
            o(tr, d.ngayNhapKho);
            o(tr, d.ngayXuat);
            o(tr, d.loaiXuat);
            o(tr, d.viTri);
            o(tr, d.hinhThucGiao);
            frag.appendChild(tr);
        });
        tbXuat.replaceChildren(frag);
    }

    function veThongKe(dl) {
        var lot = dl.dsLot || [], xuat = dl.dsXuat || [];
        if (!lot.length && !xuat.length) { window.qaTheSoLieu('qa-xh-thongke', []); return; }
        var hd = {};
        xuat.forEach(function (x) { if (x.hoaDon) hd[x.hoaDon] = 1; });
        var slXuat = xuat.reduce(function (t, x) { return t + (x.slXuat || 0); }, 0);
        var slKhach = xuat.reduce(function (t, x) { return t + (x.slKhach || 0); }, 0);
        window.qaTheSoLieu('qa-xh-thongke', [
            ['fa-tags', 'Mã lot', lot.length, ''],
            ['fa-barcode', 'Cuộn đã xuất', xuat.length.toLocaleString('vi-VN'), 'xanh'],
            ['fa-file-text-o', 'Hoá đơn', Object.keys(hd).length, ''],
            ['fa-arrows-h', 'SL xuất (Y)', so(slXuat), 'cam'],
            ['fa-arrows-h', 'SL khách (M)', so(slKhach), '']
        ]);
    }

    async function traCuu(ev) {
        ev.preventDefault();
        var ma = oMa.value.trim();
        if (!ma) { bao('Nhập mã cần tra.', 'loi'); oMa.focus(); return; }
        if (dangTra) dangTra.abort();
        var huy = new AbortController();
        dangTra = huy;
        nut.disabled = true;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang tra…';
        bao('Đang lấy dữ liệu từ NH-WMS và NH-MES…');
        try {
            var url = '/QAForm/CheckXuatHang/DuLieu?ma=' + encodeURIComponent(ma) + '&loai=' + encodeURIComponent(oLoai.value);
            var res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: huy.signal });
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            var kq = await res.json();
            if (!kq.thanhCong) { bao(kq.thongBao || 'Tra cứu thất bại.', 'loi'); return; }
            var dl = kq.duLieu;
            veLot(dl.dsLot || []);
            veXuat(dl.dsXuat || [], dl.tongXuat || 0);
            veThongKe(dl);
            var chu = 'Đã tra theo ' + (TEN_LOAI[dl.loai] || dl.loai) + ' "' + ma + '".';
            if (kq.thongBao) chu += ' ' + kq.thongBao;
            if (dl.canhBao) chu += ' ' + dl.canhBao;
            bao(chu, dl.canhBao || kq.thongBao ? 'loi' : 'ok');
        } catch (e) {
            if (e.name === 'AbortError') return;
            bao('Không tra được: ' + e.message, 'loi');
        } finally {
            if (dangTra === huy) {
                dangTra = null;
                nut.disabled = false;
                nut.innerHTML = '<i class="fa fa-search"></i> Tra cứu';
            }
        }
    }

    // Chép theo thứ tự cột file khiếu nại từ 制衣厂: 制衣厂, 终客, 国家 (trống), PO, 制单号, 货号, 宽度, 颜色, 内部色号, 出货数量
    async function chep() {
        var dong = dsLotHienTai.map(function (d) {
            return [d.nhaMay, d.khachCuoi, '', d.po, d.maLot, d.maHangGoc, d.maHang, d.mauKhach, d.maMauNoiBo, d.slXuat || '']
                .map(function (x) { return x === null || x === undefined ? '' : String(x).replace(/[\t\r\n]+/g, ' '); })
                .join('\t');
        }).join('\r\n');
        try {
            // navigator.clipboard chỉ có trên HTTPS/localhost; trang nội bộ chạy http thì chép qua textarea ẩn
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(dong);
            } else {
                var ta = document.createElement('textarea');
                ta.value = dong;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                var ok = document.execCommand('copy');
                ta.remove();
                if (!ok) throw new Error('copy');
            }
            bao('Đã chép ' + dsLotHienTai.length + ' dòng — dán vào file khiếu nại tại cột 制衣厂 (Nhà máy).', 'ok');
        } catch (e) {
            bao('Trình duyệt không cho chép tự động, hãy bôi đen bảng rồi Ctrl+C.', 'loi');
        }
    }

    form.addEventListener('submit', traCuu);
    nutChep.addEventListener('click', chep);
    oMa.focus();
})();
