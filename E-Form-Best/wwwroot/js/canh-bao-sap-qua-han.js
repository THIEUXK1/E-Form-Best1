// Nháy đỏ cảnh báo "sắp quá hạn / đã trễ hạn" dùng chung cho cả Area Công việc:
//   - Mục menu "Lịch công việc" và "Công việc chỉ định"
//   - Bất kỳ phần tử nào có thuộc tính data-lcv-id (ô việc trên lịch, dòng bảng đơn chỉ định…)
//
// Nguồn dữ liệu: /FormCongViec/GetDanhSachSapQuaHan — API này ĐÃ lọc theo đúng phạm vi quyền
// của từng tài khoản (người tạo / người duyệt / người liên quan / bộ phận), nên mỗi người chỉ
// nháy đỏ đúng những đơn mà chính họ nhận được thông báo.
//
// Không tự gọi API: layout đã có vòng lặp checkSapQuaHanLayout() 60s/lần và bắn ra sự kiện
// 'cv:sapquahan' kèm danh sách đơn — mọi trang nghe ké nên toàn hệ thống chỉ tốn một lần gọi.
(function () {
    'use strict';

    // Lưu lại lần cảnh báo gần nhất để script nạp sau (trang con) vẫn lấy được ngay,
    // không phải chờ tới nhịp kiểm tra kế tiếp.
    var api = window.CvSapQuaHan || {};
    api.items = api.items || [];
    api.ids = api.ids || [];
    window.CvSapQuaHan = api;

    // Các mục menu cần nháy khi có cảnh báo
    var ID_MENU = ['navLichCongViec', 'navCongViecChiDinh'];

    function coTrongCanhBao(id) {
        return api.ids.indexOf(String(id)) !== -1;
    }

    // Bật/tắt nháy cho mọi phần tử đã gắn data-lcv-id. Trang nào vẽ lại danh sách
    // thì gọi lại hàm này (window.CvSapQuaHan.apDung()) sau khi vẽ xong.
    function apDung() {
        document.querySelectorAll('[data-lcv-id]').forEach(function (el) {
            el.classList.toggle('lcv-nhay-do', coTrongCanhBao(el.getAttribute('data-lcv-id')));
        });
    }

    function capNhatMenu() {
        var soDon = api.items.length;
        ID_MENU.forEach(function (idMenu) {
            var link = document.getElementById(idMenu);
            if (!link) return; // tài khoản không có quyền thấy mục này

            var badge = link.querySelector('.nav-canh-bao-badge');
            if (soDon > 0) {
                link.classList.add('co-canh-bao-han');
                link.title = 'Có ' + soDon + ' công việc sắp quá hạn hoặc đã trễ hạn';
                if (badge) badge.textContent = soDon;
            } else {
                link.classList.remove('co-canh-bao-han');
                link.removeAttribute('title');
                if (badge) badge.textContent = '';
            }
        });
    }

    document.addEventListener('cv:sapquahan', function (e) {
        api.items = (e.detail && e.detail.length) ? e.detail : [];
        api.ids = api.items.map(function (x) { return String(x.id); });
        capNhatMenu();
        apDung();
    });

    api.apDung = apDung;
    api.coTrongCanhBao = coTrongCanhBao;
})();
