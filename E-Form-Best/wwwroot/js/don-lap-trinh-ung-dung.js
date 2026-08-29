/*
 * ĐƠN YÊU CẦU LẬP TRÌNH ỨNG DỤNG (IT_LapTrinhUngDung_11) — gửi đơn không tải lại trang.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — xử lý 100% bằng JS,
 * server trả JSON, cập nhật UI tại chỗ. View chỉ có markup + data-url.
 */
(function () {
    "use strict";

    var form = document.getElementById("lapTrinhForm");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var oDanAnh = document.getElementById("paste_AnhUngDung");
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
        if (!form.querySelector('input[name="LoaiYeuCau"]:checked'))
            return { thongBao: "Vui lòng chọn Loại yêu cầu!" };

        var tenUngDung = document.getElementById("TenUngDung");
        if (!tenUngDung.value.trim())
            return { thongBao: "Vui lòng nhập Tên ứng dụng / tool cần làm!", idFocus: "TenUngDung" };

        var moTa = document.getElementById("MoTaYeuCau");
        if (!moTa.value.trim())
            return { thongBao: "Vui lòng mô tả yêu cầu — đây là phần quan trọng nhất của đơn!", idFocus: "MoTaYeuCau" };

        // Mô tả quá ngắn thì lập trình viên không đủ thông tin để bắt đầu
        if (moTa.value.trim().length < 30)
            return { thongBao: "Mô tả yêu cầu quá ngắn. Hãy nêu rõ bài toán cần giải quyết (ít nhất 30 ký tự).", idFocus: "MoTaYeuCau" };

        var ngay = document.getElementById("NgayMongMuon");
        if (ngay.value) {
            var homNay = new Date();
            homNay.setHours(0, 0, 0, 0);
            if (new Date(ngay.value) < homNay)
                return { thongBao: "Ngày mong muốn không được nhỏ hơn ngày hôm nay!", idFocus: "NgayMongMuon" };
        }

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

    // Gửi xong thì dọn cả ô xem trước, không chỉ reset các input
    function datLaiForm() {
        form.reset();
        if (oDanAnh) oDanAnh.innerHTML = goiYDanAnh;
        if (oChonAnh) oChonAnh.value = "";
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
                title: "Xác nhận gửi đơn lập trình?",
                text: document.getElementById("TenUngDung").value.trim(),
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
