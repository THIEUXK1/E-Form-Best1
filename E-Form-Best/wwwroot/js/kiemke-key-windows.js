// Luật "Có key / Chưa có key" cho cột Key Windows ở bảng thiết bị (trang QL Kiểm kê > Thiết bị).
// win_license trong DB có 2 nguồn ghi khác nhau:
//  - Nhập từ báo cáo quét key (ImportWinLicenseExcel): "Windows 10 Professional(MAK-66PKM)" - có loại key thì xét theo
//    đúng quy ước công ty như LaKeyDatChuanCongTy phía server: chỉ OEM hoặc MAK của công ty mới là có key;
//    GVLK/Retail generic là key công khai nên máy kích hoạt được cũng KHÔNG tính là có key.
//  - Đồng bộ cấu hình máy: "Professional - Có bản quyền", "Windows Pro", "Windows Home"... không có loại key nên chỉ tin nhãn.
//    Windows Home không đạt chuẩn công ty (mục tiêu toàn bộ lên Pro) nên tính là chưa có key.
window.KeyWindowsKiemKe = (function () {
    let makCongTy = null;

    function dsMakCongTy() {
        if (makCongTy === null) {
            makCongTy = String($('#adminDataTable').data('makCongTy') || '')
                .split(',')
                .map(function (k) { return k.trim().toUpperCase(); })
                .filter(Boolean);
        }
        return makCongTy;
    }

    return {
        laCoKey: function (giaTri) {
            const gt = (giaTri || '').trim();
            const thuong = gt.toLowerCase();
            if (thuong === '') return false;

            const loaiKey = gt.match(/\((OEM|MAK|GVLK|RETAIL|OTHER)[^)]*?(?:-([A-Z0-9]{5}))?\)\s*$/i);
            if (loaiKey) {
                const loai = loaiKey[1].toUpperCase();
                if (loai === 'OEM') return true;
                if (loai === 'MAK') return dsMakCongTy().indexOf((loaiKey[2] || '').toUpperCase()) >= 0;
                return false;
            }

            if (thuong.indexOf('home') >= 0) return false;
            if (thuong.startsWith('chưa') || thuong.startsWith('không')) return false;
            return thuong.indexOf('có bản quyền') >= 0 || thuong.startsWith('có') || thuong === 'windows pro';
        }
    };
})();
