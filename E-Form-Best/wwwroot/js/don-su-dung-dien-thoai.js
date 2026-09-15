/*
 * ĐƠN XIN SỬ DỤNG ĐIỆN THOẠI (HR_DonSuDungDienThoai_12) — một đơn đăng ký cho nhiều người.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — gửi 100% bằng JS, không reload trang,
 * server trả JSON. View chỉ giữ markup + data-url + <template>.
 */
(function () {
    "use strict";

    var form = document.getElementById("hrFormDienThoai");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var wrapper = document.getElementById("dsNguoiSuDung");
    var tplDong = document.getElementById("tplNguoiRow");

    var oDanAnh = document.getElementById("paste_AnhMinhChung");
    var oChonAnh = document.getElementById("AnhMinhChung");
    var goiYDanAnh = oDanAnh ? oDanAnh.innerHTML : ""; // giữ gợi ý gốc để khôi phục sau khi gửi

    var oTgBatDau = document.getElementById("inpTGBatDau");
    if (oTgBatDau && !oTgBatDau.value) {
        var now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        oTgBatDau.value = now.toISOString().slice(0, 16);
    }

    // Event delegation: các dòng người sử dụng sinh động nên không gắn listener từng nút
    form.addEventListener("click", function (e) {
        var nutThem = e.target.closest("[data-them-nguoi]");
        if (nutThem && wrapper && tplDong) {
            wrapper.appendChild(tplDong.content.cloneNode(true));
            var dsO = wrapper.querySelectorAll('.dynamic-nguoi-row input[name="arrHoTen"]');
            if (dsO.length) dsO[dsO.length - 1].focus();
            return;
        }

        var nutXoa = e.target.closest("[data-xoa-nguoi]");
        if (nutXoa) {
            var dong = nutXoa.closest(".dynamic-nguoi-row");
            if (dong) dong.remove();
        }
    });

    // --- ẢNH MINH CHỨNG: dán từ clipboard hoặc chọn file, đều hiện xem trước ---
    function veXemTruoc(duLieuAnh) {
        if (!oDanAnh) return;
        oDanAnh.innerHTML =
            '<img src="' + duLieuAnh +
            '" style="width:100%; height:100%; object-fit:contain; border-radius:10px;">';
    }

    if (oDanAnh) {
        oDanAnh.addEventListener("click", function () { oDanAnh.focus(); });
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
                    if (oChonAnh) oChonAnh.files = dt.files;
                };
                reader.readAsDataURL(blob);
            }
        });
    }

    function datLaiForm() {
        form.reset();
        // reset() không xoá được các dòng người đã thêm, phải dọn tay
        if (wrapper) {
            var dsDong = wrapper.querySelectorAll(".dynamic-nguoi-row");
            for (var i = dsDong.length - 1; i > 0; i--) dsDong[i].remove();
        }
        if (oDanAnh) oDanAnh.innerHTML = goiYDanAnh;
        if (oChonAnh) oChonAnh.value = "";
        if (oTgBatDau) {
            var n = new Date();
            n.setMinutes(n.getMinutes() - n.getTimezoneOffset());
            oTgBatDau.value = n.toISOString().slice(0, 16);
        }
    }

    function canhBao(thongBao, oFocus) {
        if (window.Swal) {
            Swal.fire({ icon: "warning", title: "Thiếu thông tin", text: thongBao });
        } else {
            alert(thongBao);
        }
        if (oFocus) oFocus.focus();
    }

    // Trả về null nếu hợp lệ, ngược lại { thongBao, oFocus }
    function kiemTraHopLe() {
        var dsHoTen = form.querySelectorAll('input[name="arrHoTen"]');
        var dsMaThe = form.querySelectorAll('input[name="arrMaSoThe"]');
        var dsBoPhan = form.querySelectorAll('input[name="arrBoPhan"]');
        var dsChucVu = form.querySelectorAll('input[name="arrChucVu"]');

        if (dsHoTen.length === 0)
            return { thongBao: "Vui lòng khai ít nhất một người sử dụng!" };

        for (var i = 0; i < dsHoTen.length; i++) {
            if (!dsHoTen[i].value.trim())
                return { thongBao: "Người thứ " + (i + 1) + ": chưa nhập Họ tên!", oFocus: dsHoTen[i] };
            if (!dsMaThe[i].value.trim())
                return { thongBao: "Người thứ " + (i + 1) + ": chưa nhập Mã thẻ/Mã NV!", oFocus: dsMaThe[i] };
            if (!dsBoPhan[i].value.trim())
                return { thongBao: "Người thứ " + (i + 1) + ": chưa nhập Bộ phận!", oFocus: dsBoPhan[i] };
            if (!dsChucVu[i].value.trim())
                return { thongBao: "Người thứ " + (i + 1) + ": chưa nhập Chức vụ!", oFocus: dsChucVu[i] };
        }

        // Mã thẻ trùng nhau trong cùng một đơn là lỗi nhập liệu, chặn trước khi gửi
        var daCo = {};
        for (var j = 0; j < dsMaThe.length; j++) {
            var ma = dsMaThe[j].value.trim().toUpperCase();
            if (daCo[ma])
                return { thongBao: "Mã thẻ \"" + dsMaThe[j].value.trim() + "\" bị khai trùng hai lần!", oFocus: dsMaThe[j] };
            daCo[ma] = true;
        }

        var oTg = form.querySelector('input[name="chiTiet.ThoiGianBatDauSuDung"]');
        if (!oTg.value)
            return { thongBao: "Vui lòng nhập Thời gian bắt đầu sử dụng!", oFocus: oTg };

        var oLyDo = form.querySelector('textarea[name="chiTiet.LyDoSuDung"]');
        if (!oLyDo.value.trim())
            return { thongBao: "Vui lòng nhập Lý do sử dụng điện thoại!", oFocus: oLyDo };

        if (form.querySelectorAll('input[name="SelectedCongViecIds"]:checked').length === 0)
            return { thongBao: "Vui lòng chọn người tiếp nhận hỗ trợ!" };

        if (!document.getElementById("chkCamKet").checked)
            return { thongBao: "Bạn chưa tích chọn xác nhận cam kết!" };

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
        var urlDonCho = form.dataset.urlDoncho;
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
            canhBao(loi.thongBao, loi.oFocus);
            return;
        }

        var soNguoi = form.querySelectorAll('input[name="arrHoTen"]').length;

        if (window.Swal) {
            var xacNhan = await Swal.fire({
                icon: "question",
                title: "Xác nhận gửi đơn xin sử dụng điện thoại?",
                text: "Đơn đăng ký cho " + soNguoi + " người.",
                showCancelButton: true,
                confirmButtonText: "Gửi",
                cancelButtonText: "Huỷ"
            });
            if (!xacNhan.isConfirmed) return;
        } else if (!confirm("Xác nhận gửi đơn xin sử dụng điện thoại cho " + soNguoi + " người?")) {
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
