// Đơn cấp quyền ổ chung (/FormIT/DonCapQuyenOChung): dán ảnh, gửi AJAX.
(function () {
    const HTML_O_DAN_ANH = '<div style="text-align:center;"><i style="font-size: 20px; margin-bottom:8px;" class="fa fa-picture-o"></i><br>Bấm vào đây rồi Ctrl+V</div>';

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
        if (!document.querySelector('input[name="LoaiYeuCau"]:checked')) { hienLoi("Vui lòng chọn Loại yêu cầu!"); return false; }
        if (!batBuoc("TenThuMucOchung", "Vui lòng nhập Tên/Đường dẫn thư mục ổ chung!")) return false;
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
        if (!confirm("Xác nhận gửi đơn " + loaiYeuCau + " này?")) return;

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
        } catch (err) {
            hienLoi("Lỗi kết nối: " + err.message);
        } finally {
            btn.innerHTML = htmlGoc;
            btn.style.opacity = "1";
            btn.style.pointerEvents = "auto";
        }
    }

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
})();
