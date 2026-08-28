/*
 * ĐƠN YÊU CẦU CÀI ĐẶT PHẦN MỀM (IT_CaiDatPhanMem_10) — gửi đơn không tải lại trang.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — xử lý 100% bằng JS,
 * server trả JSON, cập nhật UI tại chỗ. View chỉ có markup + data-url.
 */
(function () {
    "use strict";

    var form = document.getElementById("phanMemForm");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var oDanAnh = document.getElementById("paste_AnhPhanMem");
    var oChonAnh = document.getElementById("input_Anh");
    var goiYDanAnh = oDanAnh ? oDanAnh.innerHTML : ""; // giữ gợi ý gốc để khôi phục sau khi gửi

    function veXemTruoc(duLieuAnh) {
        if (!oDanAnh) return;
        oDanAnh.innerHTML =
            '<img src="' + duLieuAnh +
            '" style="height:100%; width:100%; object-fit:contain; border-radius:12px;">';
    }

    // Dán ảnh từ Clipboard: vừa hiện xem trước, vừa nạp vào input file để gửi kèm
    if (oDanAnh) {
        oDanAnh.addEventListener("paste", function (e) {
            var items = (e.clipboardData || window.clipboardData).items;
            for (var i = 0; i < items.length; i++) {
                if (items[i].type.indexOf("image") === -1) continue;
                var blob = items[i].getAsFile();
                var reader = new FileReader();
                reader.onload = function (ev) {
                    veXemTruoc(ev.target.result);
                    var dt = new DataTransfer();
                    dt.items.add(blob);
                    oChonAnh.files = dt.files;
                };
                reader.readAsDataURL(blob);
            }
        });
    }

    // Chọn file thủ công cũng hiện xem trước như khi dán
    if (oChonAnh && oDanAnh) {
        oChonAnh.addEventListener("change", function () {
            if (!this.files || !this.files[0]) return;
            var reader = new FileReader();
            reader.onload = function (ev) { veXemTruoc(ev.target.result); };
            reader.readAsDataURL(this.files[0]);
        });
    }

    // Gửi xong thì dọn cả ô xem trước, không chỉ reset các input
    function datLaiForm() {
        form.reset();
        if (oDanAnh) oDanAnh.innerHTML = goiYDanAnh;
        if (oChonAnh) oChonAnh.value = "";
    }

    function canhBao(thongBao, idFocus) {
        if (window.Swal) {
            Swal.fire({ icon: "warning", title: "Thiếu thông tin", text: thongBao });
        } else {
            alert(thongBao);
        }
        if (idFocus) {
            var o = document.getElementById(idFocus);
            if (o) o.focus();
        }
    }

    // Trả về null nếu hợp lệ, ngược lại { thongBao, idFocus }
    function kiemTraHopLe() {
        var tenPhanMem = document.getElementById("TenPhanMem");
        if (!tenPhanMem.value.trim())
            return { thongBao: "Vui lòng nhập Tên phần mềm cần cài!", idFocus: "TenPhanMem" };

        var mayCaiDat = document.getElementById("MayCaiDat");
        if (!mayCaiDat.value.trim())
            return { thongBao: "Vui lòng nhập Tên máy hoặc mã tài sản cần cài!", idFocus: "MayCaiDat" };

        var ngayCanCo = document.getElementById("NgayCanCo");
        if (!ngayCanCo.value)
            return { thongBao: "Vui lòng chọn Ngày cần có!", idFocus: "NgayCanCo" };

        // Ngày cần có nằm trong quá khứ gần như luôn là gõ nhầm
        var homNay = new Date();
        homNay.setHours(0, 0, 0, 0);
        if (new Date(ngayCanCo.value) < homNay)
            return { thongBao: "Ngày cần có không được nhỏ hơn ngày hôm nay!", idFocus: "NgayCanCo" };

        var lyDo = document.getElementById("LyDoSuDung");
        if (!lyDo.value.trim())
            return { thongBao: "Vui lòng nhập Lý do sử dụng phần mềm!", idFocus: "LyDoSuDung" };

        if (form.querySelectorAll('input[name="SelectedCongViecIds"]:checked').length === 0)
            return { thongBao: "Vui lòng chọn ít nhất một nhân sự IT hỗ trợ!" };

        if (!document.getElementById("chkCamKet").checked)
            return { thongBao: "Bạn cần xác nhận vào ô cam kết trước khi gửi!" };

        return null;
    }

    function khoaNut(dangXuLy) {
        dangGui = dangXuLy;
        if (!btn) return;
        btn.disabled = dangXuLy;
        btn.style.opacity = dangXuLy ? "0.6" : "";
        btn.style.pointerEvents = dangXuLy ? "none" : "";
        btn.innerHTML = dangXuLy
            ? '<i class="fa fa-spinner fa-spin"></i> ĐANG XỬ LÝ...'
            : nhanBtnGoc;
    }

    function baoKetQua(ketQua) {
        var urlDonCho = form.dataset.urlDonCho;
        if (!window.Swal) {
            alert(ketQua.thongBao);
            datLaiForm();
            return;
        }
        Swal.fire({
            icon: ketQua.trung ? "info" : "success",
            title: ketQua.trung ? "Đơn đã được ghi nhận" : "Gửi đơn thành công",
            text: ketQua.thongBao,
            showCancelButton: true,
            confirmButtonText: "Tạo đơn khác",
            cancelButtonText: "Xem đơn chờ duyệt"
        }).then(function (r) {
            // Chỉ điều hướng khi chính người dùng chọn sang trang khác
            if (!r.isConfirmed && urlDonCho) window.location.href = urlDonCho;
        });
        datLaiForm();
    }

    form.addEventListener("submit", async function (e) {
        e.preventDefault();
        if (dangGui) return;

        var loi = kiemTraHopLe();
        if (loi) {
            canhBao(loi.thongBao, loi.idFocus);
            return;
        }

        if (window.Swal) {
            var xacNhan = await Swal.fire({
                icon: "question",
                title: "Xác nhận gửi đơn cài đặt phần mềm?",
                text: document.getElementById("TenPhanMem").value.trim(),
                showCancelButton: true,
                confirmButtonText: "Gửi",
                cancelButtonText: "Huỷ"
            });
            if (!xacNhan.isConfirmed) return;
        } else if (!confirm("Xác nhận gửi đơn này?")) {
            return;
        }

        khoaNut(true);
        try {
            // FormData lấy cả file đính kèm và __RequestVerificationToken nằm trong form
            var res = await fetch(form.dataset.url, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!res.ok) throw new Error("Máy chủ trả về mã " + res.status);

            var ketQua = await res.json();
            if (!ketQua.thanhCong) throw new Error(ketQua.thongBao || "Không lưu được đơn.");

            baoKetQua(ketQua);
        } catch (err) {
            if (window.Swal) {
                Swal.fire({ icon: "error", title: "Gửi đơn thất bại", text: err.message });
            } else {
                alert("Gửi đơn thất bại: " + err.message);
            }
        } finally {
            khoaNut(false); // lỗi thì giữ nguyên dữ liệu đã nhập để người dùng gửi lại
        }
    });
})();
