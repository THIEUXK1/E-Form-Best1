// Lọc / sắp xếp kiểu Excel trên đầu cột cho bảng đã có dữ liệu (lọc ngay trên trình duyệt, không gọi máy chủ).
// Dùng: <table data-loc-cot> ; <th> nào có data-loc thì có nút ▼. Ô dữ liệu lấy theo thứ tự cột (td thứ i = th thứ i).
// Dòng bị lọc được ẩn (tr.hidden). Mỗi lần lọc đổi, bảng phát sự kiện 'qa-loc-cot:doi' (detail = { hien, tong }).
// Vẽ lại tbody (tra lại) thì tự áp lại bộ lọc đang có. window.qaLocCot.xoa(table) để bỏ hết bộ lọc.
(function () {
    var TRAN_HIEN = 1000;      // số giá trị tối đa liệt kê trong hộp (gõ tìm để thu hẹp)
    var TRONG = '(Trống)';
    var hop = null;            // hộp lọc đang mở { table, cot, el }

    function zoomBody() { return parseFloat(getComputedStyle(document.body).zoom) || 1; }
    function trangThai(table) { return table._locCot || (table._locCot = { loc: {} }); } // loc[cot] = Set giá trị được hiện

    function giaTriO(tr, cot) {
        var td = tr.cells[cot];
        var v = td ? td.textContent.trim() : '';
        return v === '' ? TRONG : v;
    }
    function laDongDuLieu(tr) { return tr.cells.length > 1; } // bỏ dòng "Chưa có dữ liệu" (1 ô colspan)
    function dsDongDuLieu(table) { return Array.prototype.filter.call(table.tBodies[0].rows, laDongDuLieu); }

    // Dòng qua mọi bộ lọc, trừ cột boQua (để hộp của cột đó liệt kê giá trị theo các cột khác — giống Excel)
    function quaLoc(tr, loc, boQua) {
        for (var c in loc) {
            if (+c === boQua) continue;
            if (!loc[c].has(giaTriO(tr, +c))) return false;
        }
        return true;
    }

    function apDung(table) {
        var loc = trangThai(table).loc;
        var ds = dsDongDuLieu(table), hien = 0;
        ds.forEach(function (tr) {
            var ok = quaLoc(tr, loc, -1);
            tr.hidden = !ok;
            if (ok) hien++;
        });
        Array.prototype.forEach.call(table.tHead.rows[0].cells, function (th, i) {
            var nut = th.querySelector('.qa-loc-nut');
            if (nut) nut.classList.toggle('dang-loc', !!loc[i]);
        });
        table.classList.toggle('qa-dang-loc-cot', Object.keys(loc).length > 0);
        table.dispatchEvent(new CustomEvent('qa-loc-cot:doi', { bubbles: true, detail: { hien: hien, tong: ds.length } }));
    }

    function soSanh(laSo) {
        return function (a, b) {
            if (a === TRONG) return 1;
            if (b === TRONG) return -1;
            return laSo ? parseFloat(a) - parseFloat(b) : a.localeCompare(b, 'vi', { numeric: true });
        };
    }
    function cotLaSo(giaTri) { return giaTri.length > 0 && giaTri.every(function (v) { return v === TRONG || /^-?\d+(\.\d+)?$/.test(v); }); }

    function sapXep(table, cot, tang) {
        var tbody = table.tBodies[0];
        var ds = dsDongDuLieu(table);
        var laSo = cotLaSo(ds.map(function (tr) { return giaTriO(tr, cot); }));
        var ss = soSanh(laSo);
        ds.sort(function (a, b) { var r = ss(giaTriO(a, cot), giaTriO(b, cot)); return tang ? r : -r; });
        var frag = document.createDocumentFragment();
        ds.forEach(function (tr) { frag.appendChild(tr); });
        tbody.appendChild(frag);
    }

    function dongHop() {
        if (!hop) return;
        hop.el.remove();
        hop = null;
    }

    function moHop(table, th, cot) {
        dongHop();
        var loc = trangThai(table).loc;
        // Đếm giá trị trên các dòng qua bộ lọc của cột khác
        var dem = new Map();
        dsDongDuLieu(table).forEach(function (tr) {
            if (!quaLoc(tr, loc, cot)) return;
            var v = giaTriO(tr, cot);
            dem.set(v, (dem.get(v) || 0) + 1);
        });
        var dsGiaTri = Array.from(dem.keys());
        dsGiaTri.sort(soSanh(cotLaSo(dsGiaTri)));
        var dangChon = loc[cot] ? new Set(loc[cot]) : new Set(dsGiaTri);

        var el = document.createElement('div');
        el.className = 'qa-loc-hop';
        el.innerHTML =
            '<div class="qa-loc-sx">' +
            '<button type="button" data-sx="tang"><i class="fa fa-sort-alpha-asc"></i> Sắp xếp A → Z</button>' +
            '<button type="button" data-sx="giam"><i class="fa fa-sort-alpha-desc"></i> Sắp xếp Z → A</button>' +
            '</div>' +
            '<button type="button" class="qa-loc-xoa-cot"><i class="fa fa-times"></i> Xoá lọc cột này</button>' +
            '<input type="search" class="form-control form-control-sm qa-loc-tim" placeholder="Tìm...">' +
            '<div class="qa-loc-ds"></div>' +
            '<div class="qa-loc-nut-duoi">' +
            '<button type="button" class="btn btn-sm btn-success qa-loc-ok">OK</button>' +
            '<button type="button" class="btn btn-sm btn-outline-secondary qa-loc-huy">Huỷ</button>' +
            '</div>';
        var dsEl = el.querySelector('.qa-loc-ds');
        var tim = el.querySelector('.qa-loc-tim');

        function dongChon(text, checked, lop) {
            var lb = document.createElement('label');
            if (lop) lb.className = lop;
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.className = 'form-check-input';
            cb.checked = checked;
            lb.appendChild(cb);
            lb.appendChild(document.createTextNode(' ' + text));
            return { lb: lb, cb: cb };
        }

        function dsKhopTim() {
            var q = tim.value.trim().toLowerCase();
            return q ? dsGiaTri.filter(function (v) { return v.toLowerCase().indexOf(q) >= 0; }) : dsGiaTri;
        }

        function ve() {
            var khop = dsKhopTim();
            var frag = document.createDocumentFragment();
            var tatCa = dongChon('(Chọn tất cả' + (tim.value.trim() ? ' kết quả tìm' : '') + ')',
                khop.length > 0 && khop.every(function (v) { return dangChon.has(v); }), 'qa-loc-tatca');
            tatCa.cb.addEventListener('change', function () {
                khop.forEach(function (v) { if (tatCa.cb.checked) dangChon.add(v); else dangChon.delete(v); });
                ve();
            });
            frag.appendChild(tatCa.lb);
            khop.slice(0, TRAN_HIEN).forEach(function (v) {
                var d = dongChon(v + '  (' + dem.get(v).toLocaleString('vi-VN') + ')', dangChon.has(v));
                d.cb.addEventListener('change', function () {
                    if (d.cb.checked) dangChon.add(v); else dangChon.delete(v);
                    tatCa.cb.checked = khop.every(function (x) { return dangChon.has(x); });
                });
                frag.appendChild(d.lb);
            });
            if (khop.length > TRAN_HIEN) {
                var them = document.createElement('div');
                them.className = 'qa-loc-them';
                them.textContent = 'Còn ' + (khop.length - TRAN_HIEN).toLocaleString('vi-VN') + ' giá trị — gõ ô Tìm để thu hẹp.';
                frag.appendChild(them);
            }
            if (khop.length === 0) {
                var trong = document.createElement('div');
                trong.className = 'qa-loc-them';
                trong.textContent = 'Không có giá trị khớp.';
                frag.appendChild(trong);
            }
            dsEl.replaceChildren(frag);
        }

        function ok() {
            // Đang gõ tìm: như Excel, chỉ giữ các giá trị khớp tìm và đang tích
            var khop = new Set(dsKhopTim());
            var chon = dsGiaTri.filter(function (v) { return dangChon.has(v) && (!tim.value.trim() || khop.has(v)); });
            if (chon.length === dsGiaTri.length) delete loc[cot];
            else loc[cot] = new Set(chon);
            dongHop();
            apDung(table);
        }

        tim.addEventListener('input', ve);
        tim.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); ok(); } });
        el.querySelector('.qa-loc-ok').addEventListener('click', ok);
        el.querySelector('.qa-loc-huy').addEventListener('click', dongHop);
        el.querySelector('.qa-loc-xoa-cot').addEventListener('click', function () { delete loc[cot]; dongHop(); apDung(table); });
        el.querySelectorAll('[data-sx]').forEach(function (b) {
            b.addEventListener('click', function () { sapXep(table, cot, b.dataset.sx === 'tang'); dongHop(); apDung(table); });
        });
        ve();

        // Đặt hộp dưới nút; body có zoom (menu % kích thước) nên toạ độ màn hình phải chia zoom
        document.body.appendChild(el);
        var z = zoomBody();
        var r = th.getBoundingClientRect();
        var rong = el.offsetWidth;
        var trai = Math.min(r.left / z, (window.innerWidth / z) - rong - 8);
        el.style.left = Math.max(8, trai) + 'px';
        el.style.top = (r.bottom / z + 2) + 'px';
        el.style.maxHeight = Math.max(220, window.innerHeight / z - r.bottom / z - 16) + 'px';
        hop = { table: table, cot: cot, el: el };
        tim.focus();
    }

    function khoiTao(table) {
        if (table._locCotDaGan) return;
        table._locCotDaGan = true;
        Array.prototype.forEach.call(table.tHead.rows[0].cells, function (th, i) {
            if (!th.hasAttribute('data-loc')) return;
            var nut = document.createElement('button');
            nut.type = 'button';
            nut.className = 'qa-loc-nut';
            nut.title = 'Lọc / sắp xếp cột này';
            nut.innerHTML = '<i class="fa fa-caret-down"></i>';
            nut.addEventListener('click', function (e) {
                e.stopPropagation();
                if (hop && hop.table === table && hop.cot === i) { dongHop(); return; }
                moHop(table, th, i);
            });
            th.appendChild(nut);
        });
        // tbody vẽ lại (tra lại, bỏ vào thùng...) -> áp lại bộ lọc đang có
        new MutationObserver(function () {
            if (Object.keys(trangThai(table).loc).length) apDung(table);
        }).observe(table.tBodies[0], { childList: true });
    }

    document.addEventListener('mousedown', function (e) {
        if (hop && !e.target.closest('.qa-loc-hop') && !e.target.closest('.qa-loc-nut')) dongHop();
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') dongHop(); });
    window.addEventListener('resize', dongHop);
    document.addEventListener('scroll', function (e) { if (hop && !e.target.closest?.('.qa-loc-hop')) dongHop(); }, true);

    document.querySelectorAll('table[data-loc-cot]').forEach(khoiTao);

    window.qaLocCot = {
        khoiTao: khoiTao,
        xoa: function (table) { trangThai(table).loc = {}; dongHop(); apDung(table); },
        dangLoc: function (table) { return Object.keys(trangThai(table).loc).length > 0; }
    };
})();
