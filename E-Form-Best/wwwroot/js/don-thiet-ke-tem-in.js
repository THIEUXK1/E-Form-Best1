/*
 * ĐƠN YÊU CẦU THIẾT KẾ TEM IN (IT_ThietKeTemIn_9) — gửi đơn không tải lại trang.
 * Ràng buộc: .claude/rules/architecture-workflow.md mục 5 — xử lý 100% bằng JS,
 * server trả JSON, cập nhật UI tại chỗ. View chỉ có markup + data-url.
 */
(function () {
    "use strict";

    var form = document.getElementById("temInForm");
    if (!form) return;

    var btn = document.getElementById("btnSubmit");
    var nhanBtnGoc = btn ? btn.innerHTML : "";
    var dangGui = false; // chốt chống double-submit phía client

    var oDanAnh = document.getElementById("paste_AnhTem");
    var goiYDanAnh = oDanAnh ? oDanAnh.innerHTML : ""; // giữ gợi ý gốc để khôi phục sau khi gửi

    // Dán ảnh mẫu tem từ Clipboard: vừa hiện xem trước, vừa nạp vào input file để gửi kèm
    if (oDanAnh) {
        oDanAnh.addEventListener("paste", function (e) {
            var items = (e.clipboardData || window.clipboardData).items;
            for (var i = 0; i < items.length; i++) {
                if (items[i].type.indexOf("image") === -1) continue;
                var blob = items[i].getAsFile();
                var reader = new FileReader();
                var oXemTruoc = this;
                reader.onload = function (ev) {
                    oXemTruoc.innerHTML =
                        '<img src="' + ev.target.result +
                        '" style="height:100%; width:100%; object-fit:contain; border-radius:12px;">';
                    var dt = new DataTransfer();
                    dt.items.add(blob);
                    document.getElementById("input_Anh").files = dt.files;
                };
                reader.readAsDataURL(blob);
            }
        });
    }

    // Chọn file ảnh thủ công cũng hiện xem trước như khi dán
    var oChonAnh = document.getElementById("input_Anh");
    if (oChonAnh && oDanAnh) {
        oChonAnh.addEventListener("change", function () {
            if (!this.files || !this.files[0]) return;
            var reader = new FileReader();
            reader.onload = function (ev) {
                oDanAnh.innerHTML =
                    '<img src="' + ev.target.result +
                    '" style="height:100%; width:100%; object-fit:contain; border-radius:12px;">';
            };
            reader.readAsDataURL(this.files[0]);
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
        if (!form.querySelector('input[name="LoaiTem"]:checked'))
            return { thongBao: "Vui lòng chọn Loại tem cần thiết kế!" };

        var kichThuoc = document.getElementById("KichThuoc");
        if (!kichThuoc.value.trim())
            return { thongBao: "Vui lòng nhập Kích thước tem (vd: 50 x 30 mm)!", idFocus: "KichThuoc" };

        var soLuong = document.getElementById("SoLuong");
        if (!soLuong.value || Number(soLuong.value) <= 0)
            return { thongBao: "Số lượng phải lớn hơn 0!", idFocus: "SoLuong" };

        var noiDung = document.getElementById("NoiDungTem");
        if (!noiDung.value.trim())
            return { thongBao: "Vui lòng nhập Nội dung cần in trên tem!", idFocus: "NoiDungTem" };

        var ngayCanCo = document.getElementById("NgayCanCo");
        if (!ngayCanCo.value)
            return { thongBao: "Vui lòng chọn Ngày cần có tem!", idFocus: "NgayCanCo" };

        // Ngày cần có nằm trong quá khứ gần như luôn là gõ nhầm
        var homNay = new Date();
        homNay.setHours(0, 0, 0, 0);
        if (new Date(ngayCanCo.value) < homNay)
            return { thongBao: "Ngày cần có không được nhỏ hơn ngày hôm nay!", idFocus: "NgayCanCo" };

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

    // Gửi xong thì dọn form ngay tại chỗ, không điều hướng
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
                title: "Xác nhận gửi đơn thiết kế tem in?",
                text: form.querySelector('input[name="LoaiTem"]:checked').value,
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
