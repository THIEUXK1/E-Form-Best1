// Gợi ý (autocomplete) cho modal Thêm/Cập nhật Thiết bị — trang QL Kiểm kê > Thiết bị.
// Lấy thẳng từ dữ liệu thiết bị đang có trên trang (allThietBiCache) nên không tốn thêm request:
// người dùng gõ tới đâu trình duyệt lọc tới đó bằng <datalist> gốc của HTML.
// Riêng Quy cách gợi ý theo Loại thiết bị đang chọn, vì cấu hình máy tính và cấu hình máy in không liên quan gì nhau.
window.GoiYThietBiKiemKe = (function () {
    // Không gợi ý Hostname/Serial vì hai trường này phải là duy nhất cho mỗi thiết bị
    const CAU_HINH = [
        { input: '#tb_TenViTri', datalist: 'suggestTenViTri', truong: 'tenViTri', hoa: true },
        { input: '#tb_TenDangNhap', datalist: 'suggestTenDangNhap', truong: 'tenDangNhap', hoa: true },
        { input: '#tb_GhiChu', datalist: 'suggestGhiChu', truong: 'ghiChu' },
        { input: '#tb_WinLicense', datalist: 'dlWinLicense', truong: 'winLicense' },
        { input: '#tb_OfficeLicense', datalist: 'dlOfficeLicense', truong: 'officeLicense' }
    ];

    // Datalist quá dài làm trình duyệt lọc chậm và danh sách thả xuống mất trọng tâm
    const TOI_DA = 300;

    let duLieu = [];

    // Chỉ gợi ý từ thiết bị còn dùng — giá trị của thiết bị đã bỏ vào thùng rác không nên lan sang bản ghi mới
    function danhSachConDung() {
        return (duLieu || []).filter(function (x) {
            return !x.ngayXoa && !(x.tenTrangThai && x.tenTrangThai.toLowerCase().indexOf('xóa') >= 0);
        });
    }

    // Trả về các giá trị khác rỗng, gộp trùng không phân biệt hoa thường, giá trị dùng nhiều xếp trước
    function gomGiaTri(ds, truong, hoa) {
        const dem = new Map();
        ds.forEach(function (item) {
            let v = (item[truong] || '').trim();
            if (v === '') return;
            if (hoa) v = v.toUpperCase();
            const khoa = v.toLowerCase();
            const cu = dem.get(khoa);
            if (cu) cu.soLan++;
            else dem.set(khoa, { giaTri: v, soLan: 1 });
        });
        return Array.from(dem.values())
            .sort(function (a, b) {
                if (b.soLan !== a.soLan) return b.soLan - a.soLan;
                return a.giaTri.localeCompare(b.giaTri, 'vi');
            })
            .slice(0, TOI_DA)
            .map(function (x) { return x.giaTri; });
    }

    // Đổ giá trị vào <datalist>, tự tạo thẻ và gắn list= nếu view chưa có sẵn.
    // Các option viết cứng trong view (bản quyền Win/Office) được giữ lại làm gốc để không bị ghi đè.
    function veDatalist(cauHinh, giaTris) {
        const $input = $(cauHinh.input);
        if ($input.length === 0) return;

        let $dl = $('#' + cauHinh.datalist);
        if ($dl.length === 0) {
            $dl = $('<datalist>').attr('id', cauHinh.datalist);
            $input.after($dl);
        }
        if ($input.attr('list') !== cauHinh.datalist) $input.attr('list', cauHinh.datalist);

        // Lần đầu: nhớ lại các option tĩnh của view
        if ($dl.data('goc') === undefined) {
            $dl.data('goc', $dl.find('option').map(function () { return this.value; }).get());
        }
        const goc = $dl.data('goc') || [];

        const daCo = new Set(goc.map(function (v) { return (v || '').trim().toLowerCase(); }));
        const tatCa = goc.slice();
        giaTris.forEach(function (v) {
            const khoa = v.toLowerCase();
            if (daCo.has(khoa)) return;
            daCo.add(khoa);
            tatCa.push(v);
        });

        $dl.empty();
        tatCa.forEach(function (v) {
            // Dùng .val() thay vì nối chuỗi HTML: đây là dữ liệu người dùng nhập
            $dl.append($('<option>').val(v));
        });
    }

    // Quy cách phụ thuộc Loại thiết bị đang chọn: cùng loại xếp trước, còn lại nối phía sau
    function veGoiYQuyCach() {
        const loai = ($('#tb_LoaiThietBi').val() || '').trim().toLowerCase();
        const ds = danhSachConDung();
        const cungLoai = loai === '' ? [] : ds.filter(function (x) { return (x.loaiThietBi || '').trim().toLowerCase() === loai; });
        const khac = loai === '' ? ds : ds.filter(function (x) { return (x.loaiThietBi || '').trim().toLowerCase() !== loai; });

        const giaTris = gomGiaTri(cungLoai, 'quyCach', false);
        const daCo = new Set(giaTris.map(function (v) { return v.toLowerCase(); }));
        gomGiaTri(khac, 'quyCach', false).forEach(function (v) {
            if (!daCo.has(v.toLowerCase())) { daCo.add(v.toLowerCase()); giaTris.push(v); }
        });

        veDatalist({ input: '#tb_QuyCach', datalist: 'suggestQuyCach' }, giaTris.slice(0, TOI_DA));
    }

    const api = {
        // Gọi lại mỗi lần danh sách thiết bị được tải/cập nhật
        capNhat: function (dsThietBi) {
            duLieu = dsThietBi || [];
            const ds = danhSachConDung();
            CAU_HINH.forEach(function (c) { veDatalist(c, gomGiaTri(ds, c.truong, c.hoa)); });
            veGoiYQuyCach();
        }
    };

    $(function () {
        // Đổi loại thiết bị thì danh sách quy cách gợi ý phải đổi theo
        $(document).on('change', '#tb_LoaiThietBi', veGoiYQuyCach);
    });

    return api;
})();
