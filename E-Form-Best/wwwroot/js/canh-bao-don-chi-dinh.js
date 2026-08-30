// Quy tắc nháy đỏ cho bảng "Công việc chỉ định" (/FormCongViec/HoanTatDonChiDinh).
//
// Chỉ nháy khi đơn ĐANG CÒN DỞ, tức ở trang Chi tiết hiện "Bàn giao kết quả: ---" và
// "Đang theo dõi triển khai..." — đó chính là trạng thái idAdmin == null (chưa ai bàn giao
// kết quả). Đơn đã bàn giao / đã hủy thì dù hạn có gần cũng KHÔNG nháy nữa.
//
// Dữ liệu bảng đã được server lọc theo quyền của từng tài khoản nên mỗi người chỉ nháy
// đúng những đơn thuộc phạm vi của mình.
(function () {
    'use strict';

    // Ngưỡng "sắp đến hạn": còn dưới 24 giờ (khớp với chuông cảnh báo ở layout); đã trễ hạn cũng tính
    var SO_GIO_CANH_BAO = 24;

    // Đơn có được phép nháy hay không (điều kiện nghiệp vụ, không phụ thuộc thời điểm)
    function dangConDoDang(item) {
        if (!item) return false;
        if ((item.tenForm || '').indexOf('[ĐÃ HỦY]') !== -1) return false;
        if (item.idAdmin != null) return false;      // đã bàn giao kết quả
        return !!item.thoiHanHoanThanh;              // không đặt hạn thì không có gì để cảnh báo
    }

    function sapDenHan(hanChotIso) {
        var conLaiGio = (new Date(hanChotIso) - new Date()) / 3600000;
        return conLaiGio <= SO_GIO_CANH_BAO;
    }

    // Đánh dấu một dòng bảng: gắn hạn chót lên DOM để còn kiểm lại theo thời gian,
    // rồi bật nháy nếu đã tới ngưỡng.
    function danhDauDong(tr, item) {
        if (!dangConDoDang(item)) return;
        tr.setAttribute('data-han-canh-bao', item.thoiHanHoanThanh);
        if (sapDenHan(item.thoiHanHoanThanh)) tr.classList.add('lcv-nhay-do');
    }

    // Người dùng có thể mở trang rất lâu — quét lại mỗi phút để đơn vừa bước vào
    // ngưỡng 24h bắt đầu nháy mà không phải tải lại trang.
    function quetLai() {
        document.querySelectorAll('[data-han-canh-bao]').forEach(function (tr) {
            tr.classList.toggle('lcv-nhay-do', sapDenHan(tr.getAttribute('data-han-canh-bao')));
        });
    }

    setInterval(quetLai, 60000);

    window.CanhBaoDonChiDinh = { danhDauDong: danhDauDong, soGioCanhBao: SO_GIO_CANH_BAO };
})();
