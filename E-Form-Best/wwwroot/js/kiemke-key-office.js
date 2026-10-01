// Công tắc "Key Office" trong modal Thêm/Cập nhật Thiết bị và nhãn cột Key Office ở bảng (trang QL Kiểm kê > Thiết bị).
// Cột office_license trong DB có nhiều cách ghi cho cùng một nghĩa ("Có bản quyền", "Có (Chưa rõ phiên bản)",
// "Chưa có", "Không có bản quyền", "Không xác định"...) nên chỉ quy về 2 trạng thái: chỉ nhãn khẳng định có bản quyền
// mới là Có key, còn lại (kể cả trống) đều là Chưa có key vì không chứng minh được máy có license.
window.KeyOfficeKiemKe = (function () {
    const O_GIA_TRI = '#tb_OfficeLicense';
    const O_CONG_TAC = '#tb_OfficeLicenseSwitch';
    const O_NHAN = '#tb_OfficeLicenseNhan';
    const CO_KEY = 'Có bản quyền';
    const CHUA_CO_KEY = 'Chưa có bản quyền';

    function laCoKey(giaTri) {
        const thuong = (giaTri || '').trim().toLowerCase();
        return thuong.startsWith('có') || thuong.startsWith('office');
    }

    function veNhan(coKey) {
        $(O_NHAN)
            .text(coKey ? 'Có key' : 'Chưa có key')
            .toggleClass('text-success', coKey)
            .toggleClass('text-danger', !coKey);
    }

    const api = {
        laCoKey: laCoKey,

        // Đổ giá trị của thiết bị đang sửa. Giữ nguyên chuỗi gốc trong ô ẩn: chỉ khi người dùng gạt công tắc
        // mới ghi lại thành nhãn chuẩn, tránh việc mở form rồi bấm Lưu lại âm thầm đổi dữ liệu cũ.
        dat: function (giaTri) {
            const coKey = laCoKey(giaTri);
            $(O_GIA_TRI).val(giaTri || '');
            $(O_CONG_TAC).prop('checked', coKey);
            veNhan(coKey);
        }
    };

    $(function () {
        $(document).on('change', O_CONG_TAC, function () {
            const coKey = this.checked;
            // trigger('change') để đoạn nhớ form Thêm mới (localStorage) vẫn bắt được giá trị mới
            $(O_GIA_TRI).val(coKey ? CO_KEY : CHUA_CO_KEY).trigger('change');
            veNhan(coKey);
        });
    });

    return api;
})();
