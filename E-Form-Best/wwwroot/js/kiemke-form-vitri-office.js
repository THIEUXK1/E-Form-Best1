// Hai ô "Vị trí địa lý" và "Cần cài Office" trong modal Thêm/Cập nhật Thiết bị
// (trang QL Kiểm kê > Thiết bị). Tách riêng vì danh mục vị trí phải tải từ API,
// còn câu trả lời Office là 3 trạng thái đúng theo cột can_cai_office:
// 1 = cần, 0 = không cần, NULL = chưa trả lời.
window.FormViTriOfficeKiemKe = (function () {
    const O_VITRI = '#tb_IdViTriDiaLy';
    const O_OFFICE = '#tb_CanCaiOffice';

    let danhMuc = [];

    function ve(idDangChon) {
        const $sel = $(O_VITRI).empty();
        $sel.append($('<option>').val('').text('-- Chưa xác định vị trí --'));
        danhMuc.forEach(function (v) {
            // Dùng .text() để tên địa điểm (dữ liệu người dùng nhập) không thành HTML
            $sel.append($('<option>').val(v.idViTriDiaLy).text(v.tenViTriDiaLy));
        });
        $sel.val(idDangChon == null || idDangChon === '' ? '' : String(idDangChon));
    }

    const api = {
        taiDanhMuc: function () {
            $.get('/QLKiemKe/GetKkViTriDiaLys', function (res) {
                if (!res || !res.success) return;
                danhMuc = res.data || [];
                // Giữ nguyên lựa chọn hiện tại phòng khi danh mục về sau lúc modal đã mở
                ve($(O_VITRI).val() || '');
            });
        },

        // Đổ dữ liệu của một thiết bị đang sửa vào 2 ô
        dat: function (idViTriDiaLy, canCaiOffice) {
            ve(idViTriDiaLy || '');
            $(O_OFFICE).val(canCaiOffice === true ? 'true' : (canCaiOffice === false ? 'false' : ''));
        },

        // Về mặc định khi mở form Thêm mới
        datMacDinh: function () {
            ve('');
            $(O_OFFICE).val('');
        },

        lay: function () {
            return {
                idViTriDiaLy: $(O_VITRI).val() || '',
                canCaiOffice: $(O_OFFICE).val() || ''
            };
        },

        // Ghép vào FormData của saveThietBi. Không chọn thì gửi chuỗi rỗng để server hiểu là NULL.
        ghepVaoFormData: function (formData) {
            const v = api.lay();
            formData.append('IdViTriDiaLy', v.idViTriDiaLy);
            formData.append('CanCaiOffice', v.canCaiOffice);
        }
    };

    return api;
})();
