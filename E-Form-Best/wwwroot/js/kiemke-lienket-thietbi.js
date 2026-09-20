// Chọn thiết bị kiểm kê (máy tính) để gắn vào máy đang xem ở trang /QLKiemKe/ViewChiTietMayTinh.
// Danh sách lấy đúng như trang /QLKiemKe/ThietBi: mọi thiết bị chưa xóa mềm, kể cả thiết bị đang gắn máy khác
// (có ghi rõ tên máy đang gắn). Chọn thiết bị đang gắn máy khác thì phải xác nhận mới chuyển liên kết.
// Gắn xong phát sự kiện 'kiemke:lienket-thaydoi' để phần bảng liên kết trong view tự nạp lại.
$(function () {
    var $vung = $("#vungChonThietBiLienKet");
    if ($vung.length === 0) return;

    var idMay = parseInt(new URLSearchParams(window.location.search).get("idMay"));
    if (!idMay) return;

    var $oTimKiem = $("#txtTimThietBiLienKet");
    var $select = $("#selThietBiChuaLienKet");
    var $nutLienKet = $("#btnLienKetThietBi");
    var $trangThai = $("#ttChonThietBiLienKet");
    var hanDebounce = null;

    function moTaThietBi(tb) {
        var phan = ["#" + tb.idThietBi + " - " + (tb.tenMayTinh || "(không tên)")];
        if (tb.seribacode) phan.push("SN: " + tb.seribacode);
        if (tb.tenViTri) phan.push(tb.tenViTri);
        if (tb.tenDangNhap) phan.push(tb.tenDangNhap);

        if (tb.idMayDangGan === idMay) {
            phan.push("★ đang gắn máy này");
        } else if (tb.idMayDangGan) {
            phan.push("⚠ đang gắn máy: " + (tb.tenMayDangGan || "#" + tb.idMayDangGan));
        } else {
            phan.push("chưa gắn máy");
        }
        return phan.join(" | ");
    }

    function napDanhSach() {
        var tuKhoa = ($oTimKiem.val() || "").trim();
        $select.prop("disabled", true).empty().append($("<option/>").val("").text("Đang tải danh sách..."));
        $nutLienKet.prop("disabled", true);

        $.ajax({
            url: "/QLKiemKe/ThietBiKiemKeDeLienKet?tuKhoa=" + encodeURIComponent(tuKhoa),
            type: "GET",
            dataType: "json",
            success: function (res) {
                $select.empty();
                if (!res.success) {
                    $select.append($("<option/>").val("").text("Không tải được danh sách"));
                    $trangThai.removeClass("text-muted").addClass("text-danger").text(res.message || "Không tải được danh sách thiết bị.");
                    return;
                }
                var ds = res.data || [];
                if (ds.length === 0) {
                    $select.append($("<option/>").val("").text("Không có thiết bị phù hợp"));
                    $trangThai.removeClass("text-danger").addClass("text-muted")
                        .text(tuKhoa ? "Không tìm thấy thiết bị khớp từ khóa." : "Chưa có thiết bị kiểm kê nào.");
                    return;
                }
                $select.prop("disabled", false).append($("<option/>").val("").text("-- Chọn thiết bị kiểm kê --"));
                $.each(ds, function (i, tb) {
                    // textContent qua .text(): tên máy/serial do người dùng nhập, không tin cậy
                    var $opt = $("<option/>").val(tb.idThietBi).text(moTaThietBi(tb));
                    $opt.attr("data-id-may-dang-gan", tb.idMayDangGan || "");
                    $opt.attr("data-ten-may-dang-gan", tb.tenMayDangGan || "");
                    $select.append($opt);
                });
                $nutLienKet.prop("disabled", false);

                var tong = typeof res.tongSo === "number" ? res.tongSo : ds.length;
                $trangThai.removeClass("text-danger").addClass("text-muted")
                    .text(tong > ds.length
                        ? ("Hiển thị " + ds.length + "/" + tong + " thiết bị (thiết bị chưa gắn máy xếp trước) - gõ từ khóa để thu hẹp.")
                        : ("Tìm thấy " + ds.length + " thiết bị."));
            },
            error: function (xhr) {
                // Nói rõ mã lỗi: 401 là hết phiên đăng nhập, 404 là trình duyệt còn giữ bản JS cũ - hai ca hay gặp nhất
                var moTa = "Không lấy được danh sách thiết bị (HTTP " + (xhr && xhr.status ? xhr.status : "?") + ").";
                if (xhr && xhr.status === 401) moTa = "Phiên đăng nhập đã hết hạn - đăng nhập lại rồi tải lại trang.";
                else if (xhr && xhr.status === 404) moTa = "Không tìm thấy API - nhấn Ctrl+F5 để tải lại bản JS mới.";
                $select.empty().append($("<option/>").val("").text("Lỗi tải danh sách"));
                $trangThai.removeClass("text-muted").addClass("text-danger").text(moTa);
            }
        });
    }

    $oTimKiem.on("input", function () {
        // Gõ tới đâu gọi tới đó sẽ bắn hàng chục request - chờ người dùng ngừng gõ 400ms
        clearTimeout(hanDebounce);
        hanDebounce = setTimeout(napDanhSach, 400);
    });

    $("#btnTaiLaiThietBiTrong").on("click", napDanhSach);

    // Gỡ liên kết ở bảng phía trên xong thì trạng thái thiết bị đổi, danh sách chọn phải nạp lại
    $(document).on("kiemke:go-lien-ket-xong", napDanhSach);

    function guiLienKet(idThietBi, chuyenLienKet) {
        var nhan = '<i class="fa fa-link"></i> Liên kết';
        $nutLienKet.prop("disabled", true).html('<i class="fa fa-spinner fa-spin"></i> Đang liên kết...');
        $.ajax({
            url: "/QLKiemKe/LienKetThietBiKiemKe",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({ idMay: idMay, idThietBi: idThietBi, chuyenLienKet: !!chuyenLienKet }),
            dataType: "json",
            success: function (res) {
                $nutLienKet.prop("disabled", false).html(nhan);
                if (res.success) {
                    $oTimKiem.val("");
                    napDanhSach();
                    $(document).trigger("kiemke:lienket-thaydoi");
                    Swal.fire({ icon: "success", title: "Đã liên kết", text: res.message || "" });
                    return;
                }

                // Thiết bị đang gắn máy khác: hỏi lại rồi gọi lần hai kèm cờ chuyển
                if (res.canChuyenLienKet) {
                    Swal.fire({
                        title: "Chuyển liên kết?",
                        text: (res.message || "") + " Chuyển sang máy đang xem?",
                        icon: "warning",
                        showCancelButton: true,
                        confirmButtonText: "Chuyển sang máy này",
                        cancelButtonText: "Hủy"
                    }).then(function (kq) {
                        if (kq.isConfirmed) guiLienKet(idThietBi, true);
                    });
                    return;
                }

                Swal.fire("Lỗi", res.message || "Không liên kết được thiết bị.", "error");
            },
            error: function () {
                $nutLienKet.prop("disabled", false).html(nhan);
                Swal.fire("Lỗi", "Không kết nối được máy chủ để liên kết thiết bị.", "error");
            }
        });
    }

    $nutLienKet.on("click", function () {
        var idThietBi = parseInt($select.val());
        if (!idThietBi) {
            Swal.fire("Chưa chọn thiết bị", "Hãy chọn một thiết bị kiểm kê trong danh sách.", "warning");
            return;
        }
        guiLienKet(idThietBi, false);
    });

    napDanhSach();
});
