// Dải thống kê số máy tính theo loại thiết bị ở trang /QLKiemKe/ViewDanhSachTatCaMayTinh.
// Đếm trên TOÀN BỘ danh sách tải về (không theo ô tìm/bộ lọc) để luôn thấy quy mô thật của từng loại;
// số đang hiển thị sau lọc đã có ở chân bảng (#lblTongSoMay).
// Bấm một ô loại = chỉ tick đúng loại đó trong dropdown lọc; bấm "Tổng" = tick lại tất cả.
window.TscnThongKeLoai = (function () {
    var NHAN_CHUA_PHAN_LOAI = "Chưa phân loại";

    function ve(danhSach) {
        var vung = document.getElementById("divThongKeLoai");
        if (!vung) return;

        var demTheoLoai = {};
        (danhSach || []).forEach(function (m) {
            var loai = (m.loaiThietBi || "").toString().trim();
            demTheoLoai[loai] = (demTheoLoai[loai] || 0) + 1;
        });

        // Chưa phân loại lên đầu (là việc cần xử lý), còn lại xếp theo số lượng giảm dần
        var cacLoai = Object.keys(demTheoLoai).sort(function (a, b) {
            if (a === "") return -1;
            if (b === "") return 1;
            return demTheoLoai[b] - demTheoLoai[a] || a.localeCompare(b);
        });

        // Ô đang chọn: đúng một loại được tick -> sáng ô đó; tick hết -> sáng "Tổng"
        var dsTick = $(".chk-filter-loai:checked").map(function () { return this.value; }).get();
        var tongCheckbox = $(".chk-filter-loai").length;
        var loaiDangChon = dsTick.length === 1 && tongCheckbox > 1 ? dsTick[0] : null;
        var dangXemTatCa = tongCheckbox === 0 || dsTick.length === tongCheckbox;

        vung.textContent = "";
        vung.appendChild(taoO(null, "Tổng", (danhSach || []).length, dangXemTatCa));
        cacLoai.forEach(function (loai) {
            vung.appendChild(taoO(loai, loai === "" ? NHAN_CHUA_PHAN_LOAI : loai, demTheoLoai[loai], loai === loaiDangChon));
        });
    }

    function taoO(loai, nhan, soLuong, dangChon) {
        var nut = document.createElement("button");
        nut.type = "button";
        nut.className = "tscn-o-thong-ke" + (loai === null ? " o-tong" : "") + (loai === "" ? " o-chua-phan-loai" : "") + (dangChon ? " active" : "");
        if (loai !== null) nut.setAttribute("data-loai", loai);
        nut.title = loai === null ? "Hiện tất cả loại thiết bị" : "Chỉ xem loại: " + nhan;

        var spanNhan = document.createElement("span");
        spanNhan.className = "nhan";
        spanNhan.textContent = nhan;
        var spanSo = document.createElement("span");
        spanSo.className = "so";
        spanSo.textContent = soLuong.toLocaleString("vi-VN");

        nut.appendChild(spanNhan);
        nut.appendChild(spanSo);
        return nut;
    }

    $(document).on("click", "#divThongKeLoai .tscn-o-thong-ke", function () {
        var $chk = $(".chk-filter-loai");
        if ($chk.length === 0) return;
        var loai = this.getAttribute("data-loai");
        if (loai === null) {
            $chk.prop("checked", true);
        } else {
            $chk.each(function () { this.checked = this.value === loai; });
        }
        // Kích một lần là đủ: handler change của trang đọc lại toàn bộ checkbox rồi lọc + vẽ lại
        $chk.first().trigger("change");
    });

    return { ve: ve };
})();
