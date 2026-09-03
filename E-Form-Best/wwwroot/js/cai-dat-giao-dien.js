// Hộp "Cài đặt giao diện" trên thanh trên cùng của layout (nút bánh răng).
// Markup được chèn vào ô trống #slotCaiDatGiaoDien mà mỗi layout đặt sẵn,
// để 5 layout không phải chép lại cùng một khối HTML.
(function () {
    // Bảng màu gợi ý; người dùng vẫn có thể tự pha bằng ô chọn màu bên cạnh
    const MAU_CHON_SAN = [
        { ten: "Xanh dương", mau: "#1e40af" },
        { ten: "Xanh biển", mau: "#0284c7" },
        { ten: "Lam ngọc", mau: "#0d9488" },
        { ten: "Lục", mau: "#16a34a" },
        { ten: "Cam", mau: "#ea580c" },
        { ten: "Đỏ", mau: "#dc2626" },
        { ten: "Tím", mau: "#7c3aed" },
        { ten: "Hồng", mau: "#db2777" }
    ];

    function taoWidget(slot) {
        slot.innerHTML = `
            <button type="button" id="btnCaiDatGiaoDien" class="lang-btn" title="Cài đặt giao diện">
                <i class="fa fa-cog" style="color: #64748b;"></i>
                <span class="cddg-btn-text">Giao diện</span>
            </button>
        `;

        const box = document.createElement("div");
        box.id = "caiDatGiaoDienOverlay";
        box.className = "cddg-overlay";
        box.innerHTML = `
            <div class="cddg-panel" role="dialog" aria-modal="true" aria-labelledby="cddgTitle">
                <div class="cddg-head">
                    <div>
                        <div id="cddgTitle" class="cddg-title"><i class="fa fa-sliders"></i> Cài đặt giao diện</div>
                        <div class="cddg-desc">Áp dụng cho mọi trang danh sách đơn</div>
                    </div>
                    <button type="button" class="cddg-close" id="btnDongCaiDatGiaoDien" aria-label="Đóng">
                        <i class="fa fa-times"></i>
                    </button>
                </div>

                <div class="cddg-body">
                    <div class="cddg-row">
                        <div>
                            <div class="cddg-label">Dạng hiển thị danh sách</div>
                            <div class="cddg-hint">Xem theo bảng hoặc theo thẻ</div>
                        </div>
                        <div class="view-switch">
                            <button type="button" class="view-mode-btn" data-view="list">
                                <i class="fa fa-list"></i> Danh sách
                            </button>
                            <button type="button" class="view-mode-btn" data-view="card">
                                <i class="fa fa-th-large"></i> Thẻ
                            </button>
                        </div>
                    </div>

                    <div class="cddg-row">
                        <div>
                            <div class="cddg-label">Menu bên trái</div>
                            <div class="cddg-hint">Thu gọn chỉ còn biểu tượng</div>
                        </div>
                        <div class="view-switch">
                            <button type="button" class="menu-mode-btn" data-menu="day-du">
                                <i class="fa fa-bars"></i> Đầy đủ
                            </button>
                            <button type="button" class="menu-mode-btn" data-menu="gon">
                                <i class="fa fa-th-list"></i> Thu gọn
                            </button>
                        </div>
                    </div>

                    <div class="cddg-row">
                        <div>
                            <div class="cddg-label">Hiệu ứng nền</div>
                            <div class="cddg-hint">Theo mùa và ngày lễ Việt Nam</div>
                        </div>
                        <div class="view-switch">
                            <button type="button" class="hieu-ung-btn" data-hieu-ung="tat">
                                <i class="fa fa-ban"></i> Tắt
                            </button>
                            <button type="button" class="hieu-ung-btn" data-hieu-ung="bat">
                                <i class="fa fa-magic"></i> Bật
                            </button>
                        </div>
                    </div>

                    <div class="cddg-row">
                        <div>
                            <div class="cddg-label">Màu chủ đạo</div>
                            <div class="cddg-hint">Chọn sẵn hoặc tự pha màu</div>
                        </div>
                        <div class="cddg-mau-wrap">
                            ${MAU_CHON_SAN.map(m => `
                                <button type="button" class="mau-chu-dao-item" data-mau="${m.mau}"
                                        title="${m.ten}" style="--o-mau: ${m.mau};"></button>
                            `).join("")}
                            <button type="button" id="btnTuPhaMau" title="Tự chọn màu bất kỳ">Tự pha</button>
                            <button type="button" id="btnMauMacDinh" title="Trả về màu gốc của phân hệ">Mặc định</button>
                            <input type="color" id="inputMauChuDao" tabindex="-1" aria-hidden="true">
                        </div>
                    </div>
                </div>
            </div>
        `;
        document.body.appendChild(box);

        function mo() {
            box.classList.add("open");
            if (window.DangXemDon) DangXemDon.refreshButtons();
            if (window.MauChuDao) MauChuDao.refreshButtons();
            if (window.MenuThuGon) MenuThuGon.refreshButtons();
            if (window.HieuUngMua) HieuUngMua.refreshButtons();
        }
        function dong() { box.classList.remove("open"); }

        document.getElementById("btnCaiDatGiaoDien").addEventListener("click", e => {
            e.preventDefault();
            box.classList.contains("open") ? dong() : mo();
        });
        document.getElementById("btnDongCaiDatGiaoDien").addEventListener("click", dong);
        box.addEventListener("click", e => { if (e.target === box) dong(); });
        document.addEventListener("keydown", e => { if (e.key === "Escape") dong(); });
    }

    document.addEventListener("DOMContentLoaded", () => {
        const slot = document.getElementById("slotCaiDatGiaoDien");
        if (slot) taoWidget(slot);
    });
})();
