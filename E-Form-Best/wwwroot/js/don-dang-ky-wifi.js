/*
 * ĐƠN ĐĂNG KÝ SỬ DỤNG WIFI (IT_DangKiSuDungWifi_3) — gửi đơn không tải lại trang.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — xử lý 100% bằng JS,
 * server trả JSON, cập nhật UI tại chỗ. View chỉ có markup + data-url + <template>.
 */
(function () {
    "use strict";

    var form = document.getElementById("itWifiForm");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var wrapper = document.getElementById("dynamicWifiWrapper");
    var tplDong = document.getElementById("tplWifiRow");

    var oDanAnh = document.getElementById("paste_AnhWifi");
    var oChonAnh = document.getElementById("input_Anh");
    var goiYDanAnh = oDanAnh ? oDanAnh.innerHTML : ""; // giữ gợi ý gốc để khôi phục sau khi gửi

    // --- ĐỊNH DẠNG MAC: chỉ nhận hex, tự chèn dấu ":" sau mỗi 2 ký tự ---
    function dinhDangMac(giaTri) {
        var hex = (giaTri || "").toUpperCase().replace(/[^0-9A-F]/g, "").slice(0, 12);
        var ketQua = "";
        for (var i = 0; i < hex.length; i++) {
            if (i > 0 && i % 2 === 0) ketQua += ":";
            ketQua += hex[i];
        }
        return ketQua;
    }

    // Event delegation: các dòng thiết bị được sinh động nên không gắn listener từng ô
    form.addEventListener("input", function (e) {
        if (e.target.classList && e.target.classList.contains("mac-input")) {
            e.target.value = dinhDangMac(e.target.value);
        }
    });

    form.addEventListener("click", function (e) {
        var nutThem = e.target.closest("[data-them-dong]");
        if (nutThem && wrapper && tplDong) {
            wrapper.appendChild(tplDong.content.cloneNode(true));
            return;
        }

        var nutXoa = e.target.closest("[data-xoa-dong]");
        if (nutXoa) {
            var dong = nutXoa.closest(".dynamic-wifi-row");
            if (dong) dong.remove();
        }
    });

    // --- ẢNH MINH HOẠ: dán từ clipboard hoặc chọn file, đều hiện xem trước ---
    function veXemTruoc(duLieuAnh) {
        if (!oDanAnh) return;
        oDanAnh.innerHTML =
            '<img src="' + duLieuAnh +
            '" style="height:100%; width:100%; object-fit:contain; border-radius:12px;">';
    }

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
                    if (oChonAnh) oChonAnh.files = dt.files;
                };
                reader.readAsDataURL(blob);
            }
        });
    }

    if (oChonAnh && oDanAnh) {
        oChonAnh.addEventListener("change", function () {
            if (!this.files || !this.files[0]) return;
            var reader = new FileReader();
            reader.onload = function (ev) { veXemTruoc(ev.target.result); };
            reader.readAsDataURL(this.files[0]);
        });
    }

    function datLaiForm() {
        form.reset();
        // reset() không xoá được các dòng thiết bị đã thêm, phải dọn tay
        if (wrapper) {
            var dsDong = wrapper.querySelectorAll(".dynamic-wifi-row");
            for (var i = dsDong.length - 1; i > 0; i--) dsDong[i].remove();
        }
        if (oDanAnh) oDanAnh.innerHTML = goiYDanAnh;
        if (oChonAnh) oChonAnh.value = "";
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
        var arrMa = form.querySelectorAll('input[name="arrMaThietBi"]');
        var arrMac = form.querySelectorAll('input[name="arrMacTb"]');

        if (arrMa.length === 0)
            return { thongBao: "Vui lòng khai ít nhất một thiết bị!" };

        for (var i = 0; i < arrMa.length; i++) {
            if (!arrMa[i].value.trim())
                return { thongBao: "Dòng số " + (i + 1) + ": Vui lòng nhập Mã thiết bị / Định danh!", oFocus: arrMa[i] };

            if (arrMac[i].value.length < 17)
                return { thongBao: "Dòng số " + (i + 1) + ": Địa chỉ MAC không hợp lệ hoặc chưa đủ 17 ký tự!", oFocus: arrMac[i] };
        }

        var congViec = document.getElementById("selectedCongViecId");
        if (!congViec.value)
            return { thongBao: "Vui lòng chọn loại công việc!", oFocus: congViec };

        if (!document.getElementById("chkCamKet").checked)
            return { thongBao: "Bạn phải xác nhận cam kết trước khi gửi!" };

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

        var soThietBi = form.querySelectorAll('input[name="arrMaThietBi"]').length;

        if (window.Swal) {
            var xacNhan = await Swal.fire({
                icon: "question",
                title: "Xác nhận gửi đơn đăng ký sử dụng Wifi?",
                text: "Đơn gồm " + soThietBi + " thiết bị.",
                showCancelButton: true,
                confirmButtonText: "Gửi",
                cancelButtonText: "Huỷ"
            });
            if (!xacNhan.isConfirmed) return;
        } else if (!confirm("Xác nhận gửi đơn đăng ký sử dụng mạng Wifi công ty?")) {
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
