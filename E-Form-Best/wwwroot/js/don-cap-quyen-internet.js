/*
 * ĐƠN CẤP QUYỀN INTERNET (IT_CapQuyenInternet_13) — gửi đơn không tải lại trang.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — xử lý 100% bằng JS,
 * server trả JSON, cập nhật UI tại chỗ. View chỉ có markup + data-url.
 */
(function () {
    "use strict";

    var form = document.getElementById("internetForm");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var oDanAnh = document.getElementById("paste_AnhInternet");
    var oChonAnh = document.getElementById("input_Anh");
    var goiYDanAnh = oDanAnh ? oDanAnh.innerHTML : ""; // giữ gợi ý gốc để khôi phục sau khi gửi

    function giaTriRadio(ten) {
        var chon = form.querySelector('input[name="' + ten + '"]:checked');
        return chon ? chon.value : "";
    }

    // Ẩn/hiện các khối phụ thuộc lựa chọn: Thu hồi thì không cần phạm vi/thời hạn,
    // "Toàn bộ Internet" thì không cần danh sách website, "Vĩnh viễn" thì không cần ngày hết hạn
    function capNhatHienThi() {
        var capMoi = giaTriRadio("LoaiYeuCau") !== "Thu hồi";
        document.getElementById("khoiCapMoi").style.display = capMoi ? "" : "none";
        document.getElementById("khoiWebsite").style.display = giaTriRadio("PhamVi") === "Website cụ thể" ? "" : "none";
        document.getElementById("khoiNgayHetHan").style.display = giaTriRadio("ThoiHan") === "Có thời hạn" ? "" : "none";
    }

    form.addEventListener("change", function (e) {
        var ten = e.target.name;
        if (ten === "LoaiYeuCau" || ten === "PhamVi" || ten === "ThoiHan") capNhatHienThi();
    });

    function veXemTruoc(duLieuAnh) {
        if (!oDanAnh) return;
        var img = document.createElement("img");
        img.src = duLieuAnh;
        img.style.cssText = "height:100%; width:100%; object-fit:contain; border-radius:12px;";
        oDanAnh.replaceChildren(img);
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

    // Gửi xong thì dọn cả ô xem trước, không chỉ reset các input
    function datLaiForm() {
        form.reset();
        if (oDanAnh) oDanAnh.innerHTML = goiYDanAnh;
        if (oChonAnh) oChonAnh.value = "";
        capNhatHienThi();
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

    function trong(id) {
        return !document.getElementById(id).value.trim();
    }

    // Trả về null nếu hợp lệ, ngược lại { thongBao, idFocus }
    function kiemTraHopLe() {
        if (trong("TenMayTinh"))
            return { thongBao: "Vui lòng nhập Tên máy tính / Mã tài sản!", idFocus: "TenMayTinh" };
        if (trong("NguoiSuDung"))
            return { thongBao: "Vui lòng nhập Người sử dụng máy!", idFocus: "NguoiSuDung" };

        if (giaTriRadio("LoaiYeuCau") !== "Thu hồi") {
            if (giaTriRadio("PhamVi") === "Website cụ thể" && trong("DanhSachWebsite"))
                return { thongBao: "Vui lòng nhập danh sách website cần mở!", idFocus: "DanhSachWebsite" };

            if (giaTriRadio("ThoiHan") === "Có thời hạn") {
                var ngay = document.getElementById("NgayHetHan").value;
                if (!ngay)
                    return { thongBao: "Vui lòng chọn Ngày hết hạn!", idFocus: "NgayHetHan" };
                var homNay = new Date();
                homNay.setHours(0, 0, 0, 0);
                if (new Date(ngay + "T00:00:00") < homNay)
                    return { thongBao: "Ngày hết hạn không được nhỏ hơn hôm nay!", idFocus: "NgayHetHan" };
            }
        }

        if (trong("LyDo"))
            return { thongBao: "Vui lòng nhập Lý do cần truy cập Internet!", idFocus: "LyDo" };

        // Chỉ bắt chọn khi thực sự có nhân sự để chọn
        var coNhanSu = form.querySelectorAll('input[name="SelectedCongViecIds"]').length > 0;
        if (coNhanSu && form.querySelectorAll('input[name="SelectedCongViecIds"]:checked').length === 0)
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

        var tieuDe = (giaTriRadio("LoaiYeuCau") === "Thu hồi" ? "Thu hồi" : "Cấp") +
            " quyền Internet cho máy " + document.getElementById("TenMayTinh").value.trim() + "?";
        if (window.Swal) {
            var xacNhan = await Swal.fire({
                icon: "question",
                title: "Xác nhận gửi đơn?",
                text: tieuDe,
                showCancelButton: true,
                confirmButtonText: "Gửi",
                cancelButtonText: "Huỷ"
            });
            if (!xacNhan.isConfirmed) return;
        } else if (!confirm(tieuDe)) {
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

    // Trình duyệt có thể giữ lựa chọn radio khi quay lại trang → đồng bộ giao diện ngay khi tải
    capNhatHienThi();
})();
