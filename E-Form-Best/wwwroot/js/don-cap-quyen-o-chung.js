// Đơn cấp quyền ổ chung / Internet (/FormIT/DonCapQuyenOChung): dán ảnh, đổi nhóm quyền, gửi AJAX.
(function () {
    const HTML_O_DAN_ANH = '<div style="text-align:center;"><i style="font-size: 20px; margin-bottom:8px;" class="fa fa-picture-o"></i><br>Bấm vào đây rồi Ctrl+V</div>';

    function layNhomQuyen() {
        const chon = document.querySelector('input[name="NhomQuyen"]:checked');
        return chon ? chon.value : "OChung";
    }

    // Đổi nhãn/placeholder và bộ "Loại quyền" theo nhóm; select đang ẩn bị disabled để không gửi lên
    function capNhatNhomQuyen() {
        const laInternet = layNhomQuyen() === "Internet";
        const khoa = laInternet ? "internet" : "oChung";

        const nhan = document.getElementById("nhanDuongDan");
        if (nhan) nhan.textContent = nhan.dataset[khoa];
        ["TenThuMucOchung", "MucDich"].forEach(function (id) {
            const o = document.getElementById(id);
            if (o) o.placeholder = o.dataset[khoa];
        });

        const selOChung = document.getElementById("LoaiQuyenOChung");
        const selInternet = document.getElementById("LoaiQuyenInternet");
        selOChung.disabled = laInternet;
        selOChung.style.display = laInternet ? "none" : "";
        selInternet.disabled = !laInternet;
        selInternet.style.display = laInternet ? "" : "none";
    }

    function hienLoi(thongBao) {
        const hop = document.getElementById("loiGuiDon");
        if (!hop) { alert(thongBao); return; }
        hop.textContent = "⚠️ " + thongBao;
        hop.style.display = thongBao ? "block" : "none";
        if (thongBao) hop.scrollIntoView({ behavior: "smooth", block: "center" });
    }

    function batBuoc(id, thongBao) {
        const o = document.getElementById(id);
        if (o && !o.value.trim()) {
            hienLoi(thongBao);
            o.focus();
            return false;
        }
        return true;
    }

    function kiemTraForm() {
        const laInternet = layNhomQuyen() === "Internet";
        if (!document.querySelector('input[name="LoaiYeuCau"]:checked')) { hienLoi("Vui lòng chọn Loại yêu cầu!"); return false; }
        if (!batBuoc("TenThuMucOchung", laInternet ? "Vui lòng nhập Website / phạm vi truy cập Internet!" : "Vui lòng nhập Tên/Đường dẫn thư mục ổ chung!")) return false;
        if (!batBuoc("NguoiSuDung", "Vui lòng nhập Tên nhân viên sử dụng quyền!")) return false;
        if (!batBuoc("TenMayTinh", "Vui lòng nhập Tên máy tính cần cấp quyền!")) return false;
        if (!batBuoc("BoPhanDuocChon", "Vui lòng chọn Bộ phận và Công ty xác nhận cho phép!")) return false;
        if (!batBuoc("MucDich", "Vui lòng nhập Mục đích sử dụng / Lý do!")) return false;
        if (!document.querySelector('input[name="SelectedCongViecIds"]:checked')) { hienLoi("Vui lòng chọn ít nhất một nhân sự IT hỗ trợ!"); return false; }
        if (!document.getElementById("chkCamKet").checked) { hienLoi("Bạn cần xác nhận vào ô cam kết trước khi gửi!"); return false; }
        return true;
    }

    async function guiDon(btn) {
        hienLoi("");
        if (!kiemTraForm()) return;

        const loaiYeuCau = document.querySelector('input[name="LoaiYeuCau"]:checked').value;
        const tenNhom = layNhomQuyen() === "Internet" ? "quyền Internet" : "quyền ổ chung";
        if (!confirm("Xác nhận gửi đơn " + loaiYeuCau + " " + tenNhom + " này?")) return;

        const form = document.getElementById("itOChungForm");
        const htmlGoc = btn.innerHTML;
        btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> ĐANG XỬ LÝ...';
        btn.style.opacity = "0.6";
        btn.style.pointerEvents = "none";

        try {
            const res = await fetch(form.action, { method: "POST", body: new FormData(form) });
            if (!res.ok) throw new Error("HTTP " + res.status);
            const kq = await res.json();
            if (!kq.thanhCong) {
                hienLoi(kq.thongBao || "Gửi đơn thất bại.");
                return;
            }

            alert("🎉 " + kq.thongBao);
            form.reset();
            document.getElementById("paste_AnhOChung").innerHTML = HTML_O_DAN_ANH;
            capNhatNhomQuyen();
        } catch (err) {
            hienLoi("Lỗi kết nối: " + err.message);
        } finally {
            btn.innerHTML = htmlGoc;
            btn.style.opacity = "1";
            btn.style.pointerEvents = "auto";
        }
    }

    document.addEventListener("change", function (e) {
        if (e.target.name === "NhomQuyen") capNhatNhomQuyen();
    });

    document.addEventListener("click", function (e) {
        const btn = e.target.closest("#btnSubmit");
        if (btn) guiDon(btn);
    });

    document.addEventListener("paste", function (e) {
        const vung = e.target.closest ? e.target.closest("#paste_AnhOChung") : null;
        if (!vung) return;
        const items = (e.clipboardData || {}).items || [];
        for (let i = 0; i < items.length; i++) {
            if (items[i].type.indexOf("image") === -1) continue;
            const blob = items[i].getAsFile();
            const reader = new FileReader();
            reader.onload = function (ev) {
                const img = document.createElement("img");
                img.src = ev.target.result;
                img.style.cssText = "height:100%; width:100%; object-fit:contain; border-radius:12px;";
                vung.replaceChildren(img);

                const dt = new DataTransfer();
                dt.items.add(blob);
                document.getElementById("input_Anh").files = dt.files;
            };
            reader.readAsDataURL(blob);
        }
    });

    // Trình duyệt có thể giữ lựa chọn radio khi quay lại trang → đồng bộ giao diện ngay khi tải
    capNhatNhomQuyen();
})();
