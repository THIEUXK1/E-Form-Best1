// Ô số liệu tổng dùng chung các trang QA (Kệ, Danh sách thùng, Tra cứu MES).
// window.qaTheSoLieu(idKhung, [[biểu tượng fa, nhãn, giá trị, màu ('' | 'xanh' | 'cam' | 'do')], ...]); mảng rỗng = ẩn khung.
(function () {
    window.qaTheSoLieu = function (idKhung, ds) {
        var khung = document.getElementById(idKhung);
        if (!khung) return;
        var frag = document.createDocumentFragment();
        ds.forEach(function (x) {
            var the = document.createElement('div');
            the.className = 'qa-ke-tk' + (x[3] ? ' ' + x[3] : '');
            var ic = document.createElement('i');
            ic.className = 'fa ' + x[0];
            var so = document.createElement('b');
            so.textContent = x[2];
            var nhan = document.createElement('span');
            nhan.textContent = x[1];
            the.append(ic, so, nhan);
            frag.appendChild(the);
        });
        khung.replaceChildren(frag);
    };
})();
