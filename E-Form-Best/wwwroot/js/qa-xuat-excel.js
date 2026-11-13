// Xuất Excel đúng bảng đang hiển thị: dòng qua bộ lọc cột (mọi trang, không chỉ trang đang xem)
// và chỉ các cột đang hiện. Đọc thẳng từ DOM nên luôn khớp với những gì người dùng thấy.
// Nút: <button data-xuat-excel="idBang" data-ten-file="..."> ; ExcelJS nạp lười khi bấm lần đầu.
(function () {
    var DUONG_DAN_EXCELJS = '/lib/exceljs/exceljs.min.js';
    var dangNap = null;

    function napExcelJs() {
        if (window.ExcelJS) return Promise.resolve();
        if (dangNap) return dangNap;
        dangNap = new Promise(function (ok, loi) {
            var s = document.createElement('script');
            s.src = DUONG_DAN_EXCELJS;
            s.onload = ok;
            s.onerror = function () { dangNap = null; loi(new Error('Không tải được thư viện Excel.')); };
            document.head.appendChild(s);
        });
        return dangNap;
    }

    function dangHien(el) { return getComputedStyle(el).display !== 'none'; }

    // Bỏ cột ô tích chọn; các cột ẩn (qa-cot-phu khi chưa bật "Hiện tất cả cột") cũng bỏ
    function cotXuat(bang) {
        var ds = [];
        Array.prototype.forEach.call(bang.tHead.rows[0].cells, function (th, i) {
            if (th.querySelector('input[type="checkbox"]')) return;
            if (!dangHien(th)) return;
            if (th.textContent.trim() === '') return; // cột nút thao tác, không có dữ liệu
            ds.push({ chiSo: i, ten: th.textContent.trim() });
        });
        return ds;
    }

    function giaTriO(td) {
        if (!td) return '';
        // Ô nhập tay (vd Số paiban ở chi tiết thùng): lấy giá trị đang nhập
        var inp = td.querySelector('input:not([type="checkbox"]), select');
        var t = inp ? String(inp.value).trim() : td.textContent.trim();
        if (inp && t !== '' && !isNaN(Number(t))) return Number(t);
        if (td.classList.contains('so') && t !== '' && !isNaN(Number(t))) return Number(t);
        return t;
    }

    function tenFile(nut) {
        var d = new Date();
        var p = function (n) { return String(n).padStart(2, '0'); };
        var ten = nut.dataset.tenFile || 'du-lieu';
        // data-ten-file-tu="id": thêm mã lấy từ phần tử đó (vd mã thùng trong tiêu đề chi tiết)
        var nguon = nut.dataset.tenFileTu && document.getElementById(nut.dataset.tenFileTu);
        var ma = nguon && (nguon.textContent.match(/[A-Z]{2,}-\d+-\d+/) || [])[0];
        if (ma) ten += '-' + ma;
        return ten + '-' + d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate())
            + '-' + p(d.getHours()) + p(d.getMinutes()) + '.xlsx';
    }

    function baoLoi(text) { if (window.qaThongBao) window.qaThongBao(text, 'loi'); else alert(text); }
    function hoi(text) { return window.qaXacNhan ? window.qaXacNhan(text) : Promise.resolve(confirm(text)); }

    async function xuat(nut) {
        var bang = document.getElementById(nut.dataset.xuatExcel);
        if (!bang) return;
        // Dòng dữ liệu thật (dòng "Chưa có dữ liệu" chỉ 1 ô) và không bị bộ lọc ẩn; dòng ngoài trang vẫn xuất
        var dong = Array.prototype.filter.call(bang.tBodies[0].rows, function (tr) { return tr.cells.length > 1 && !tr.hidden; });
        if (dong.length === 0) { baoLoi('Không có dòng nào để xuất.'); return; }
        var cot = cotXuat(bang);
        // data-canh-bao: trang gắn khi đang xem toàn bộ dữ liệu -> hỏi trước vì file rất lớn; {n} = số dòng sẽ xuất
        if (nut.dataset.canhBao && !(await hoi(nut.dataset.canhBao.replace('{n}', dong.length.toLocaleString('vi-VN'))))) return;

        var nhanCu = nut.innerHTML;
        nut.disabled = true;
        nut.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Đang xuất...';

        napExcelJs().then(function () {
            var wb = new ExcelJS.Workbook();
            var ws = wb.addWorksheet('Du lieu');
            ws.addRow(cot.map(function (c) { return c.ten; }));
            dong.forEach(function (tr) {
                ws.addRow(cot.map(function (c) { return giaTriO(tr.cells[c.chiSo]); }));
            });
            var dau = ws.getRow(1);
            dau.font = { bold: true };
            dau.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFDDEBF7' } };
            ws.views = [{ state: 'frozen', ySplit: 1 }];
            ws.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: cot.length } };
            ws.columns.forEach(function (col, i) {
                var dai = cot[i].ten.length;
                col.eachCell({ includeEmpty: false }, function (o) { dai = Math.max(dai, String(o.value).length); });
                col.width = Math.min(50, dai + 2);
            });
            return wb.xlsx.writeBuffer();
        }).then(function (buf) {
            var blob = new Blob([buf], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
            var a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = tenFile(nut);
            document.body.appendChild(a);
            a.click();
            a.remove();
            setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
        }).catch(function (e) {
            baoLoi(e && e.message ? e.message : 'Xuất Excel lỗi.');
        }).finally(function () {
            nut.disabled = false;
            nut.innerHTML = nhanCu;
        });
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('[data-xuat-excel]');
        if (!nut) return;
        e.preventDefault();
        xuat(nut);
    });
})();
