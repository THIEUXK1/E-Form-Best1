/*
 * DANH SÁCH CHẶN THIẾT BỊ (dùng chung cho trang Thiết bị và trang Danh sách tất cả máy tính)
 * - ChanThietBiKiemKe.chan(seri, tenMay)  : đưa 1 thiết bị vào danh sách chặn (hỏi lý do)
 * - ChanThietBiKiemKe.moDanhSach()        : mở modal xem/bỏ chặn
 * Sau khi chặn/bỏ chặn sẽ gọi lại hàm nạp dữ liệu của trang qua ChanThietBiKiemKe.onThayDoi.
 */
(function (window, $) {
    "use strict";

    var MODAL_ID = "modalDanhSachChanThietBi";

    function escapeHtml(s) {
        return $("<div>").text(s == null ? "" : s).html();
    }

    function taoModalNeuChua() {
        if (document.getElementById(MODAL_ID)) return;
        var html =
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true">' +
            '  <div class="modal-dialog modal-lg modal-dialog-scrollable">' +
            '    <div class="modal-content">' +
            '      <div class="modal-header bg-dark text-white">' +
            '        <h5 class="modal-title"><i class="fa fa-ban me-2"></i>Danh sách thiết bị bị chặn</h5>' +
            '        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>' +
            '      </div>' +
            '      <div class="modal-body">' +
            '        <div class="alert alert-warning py-2 small mb-3">' +
            '          Thiết bị trong danh sách này bị ẩn khỏi toàn bộ trang kiểm kê (Thiết bị, Danh sách máy tính, Thống kê, Tài sản bộ phận, biên bản) ' +
            '          và không được đồng bộ / nhập mới trở lại. Bỏ chặn để hiển thị lại.' +
            '        </div>' +
            '        <div class="table-responsive">' +
            '          <table class="table table-sm table-bordered table-hover align-middle mb-0">' +
            '            <thead class="table-secondary">' +
            '              <tr>' +
            '                <th style="width:40px;">#</th>' +
            '                <th>Tên máy</th>' +
            '                <th>Serial</th>' +
            '                <th>Lý do</th>' +
            '                <th>Người chặn</th>' +
            '                <th>Ngày chặn</th>' +
            '                <th style="width:110px;" class="text-center">Hành động</th>' +
            '              </tr>' +
            '            </thead>' +
            '            <tbody id="tbodyDanhSachChan"></tbody>' +
            '          </table>' +
            '        </div>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        $("body").append(html);

        $(document).on("click", "#tbodyDanhSachChan .btn-bo-chan", function () {
            boChan($(this).data("id"));
        });
    }

    function napDanhSach() {
        $.get("/QLKiemKe/GetDanhSachChan", function (res) {
            var html = "";
            if (!res.success) {
                html = '<tr><td colspan="7" class="text-danger text-center">' + escapeHtml(res.message) + '</td></tr>';
            } else if (!res.data || res.data.length === 0) {
                html = '<tr><td colspan="7" class="text-muted text-center">Chưa có thiết bị nào bị chặn.</td></tr>';
            } else {
                $.each(res.data, function (i, x) {
                    html += '<tr>' +
                        '<td>' + (i + 1) + '</td>' +
                        '<td class="fw-bold">' + escapeHtml(x.tenMay || "-") + '</td>' +
                        '<td class="text-danger">' + escapeHtml(x.seri || "-") + '</td>' +
                        '<td>' + escapeHtml(x.lyDo || "-") + '</td>' +
                        '<td>' + escapeHtml(x.nguoiChan || "-") + '</td>' +
                        '<td>' + escapeHtml(x.ngayChan || "-") + '</td>' +
                        '<td class="text-center">' +
                        '  <button type="button" class="btn btn-xs btn-outline-success py-0 px-2 btn-bo-chan" data-id="' + x.idChan + '" style="font-size:11px;">' +
                        '    <i class="fa fa-undo"></i> Bỏ chặn' +
                        '  </button>' +
                        '</td>' +
                        '</tr>';
                });
            }
            $("#tbodyDanhSachChan").html(html);
        });
    }

    function baoThayDoi() {
        if (typeof window.ChanThietBiKiemKe.onThayDoi === "function") {
            window.ChanThietBiKiemKe.onThayDoi();
        }
    }

    function chan(seri, tenMay) {
        var moTa = (tenMay || "(không tên)") + (seri ? " - Serial: " + seri : "");
        Swal.fire({
            title: "Chặn thiết bị này?",
            html: "<b>" + escapeHtml(moTa) + "</b><br/><span class='small text-muted'>Thiết bị sẽ bị ẩn khỏi toàn bộ danh sách kiểm kê và không được đồng bộ lại.</span>",
            input: "text",
            inputPlaceholder: "Lý do chặn (không bắt buộc)",
            icon: "warning",
            showCancelButton: true,
            confirmButtonText: "Chặn",
            cancelButtonText: "Hủy",
            confirmButtonColor: "#d33"
        }).then(function (kq) {
            if (!kq.isConfirmed) return;
            $.ajax({
                url: "/QLKiemKe/ChanThietBi",
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ seri: seri || "", tenMay: tenMay || "", lyDo: kq.value || "" }),
                success: function (res) {
                    if (res.success) {
                        Swal.fire({ toast: true, position: "top-end", icon: "success", title: res.message, showConfirmButton: false, timer: 2000 });
                        baoThayDoi();
                    } else {
                        Swal.fire("Lỗi", res.message || "Không thể chặn thiết bị.", "error");
                    }
                },
                error: function () { Swal.fire("Lỗi", "Không kết nối được máy chủ.", "error"); }
            });
        });
    }

    function boChan(idChan) {
        Swal.fire({
            title: "Bỏ chặn thiết bị?",
            text: "Thiết bị sẽ hiển thị trở lại trong các danh sách kiểm kê.",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Bỏ chặn",
            cancelButtonText: "Hủy"
        }).then(function (kq) {
            if (!kq.isConfirmed) return;
            $.ajax({
                url: "/QLKiemKe/BoChanThietBi",
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ idChan: idChan }),
                success: function (res) {
                    if (res.success) {
                        Swal.fire({ toast: true, position: "top-end", icon: "success", title: res.message, showConfirmButton: false, timer: 2000 });
                        napDanhSach();
                        baoThayDoi();
                    } else {
                        Swal.fire("Lỗi", res.message || "Không thể bỏ chặn.", "error");
                    }
                },
                error: function () { Swal.fire("Lỗi", "Không kết nối được máy chủ.", "error"); }
            });
        });
    }

    function moDanhSach() {
        taoModalNeuChua();
        napDanhSach();
        new bootstrap.Modal(document.getElementById(MODAL_ID)).show();
    }

    window.ChanThietBiKiemKe = {
        chan: chan,
        boChan: boChan,
        moDanhSach: moDanhSach,
        onThayDoi: null
    };
})(window, jQuery);
