// Đổi dạng xem cho các trang danh sách đơn: dạng danh sách (bảng) <-> dạng thẻ.
// Dùng chung cho Area ITForm / HRform / SHDForm.
// Nút chuyển nằm trong hộp "Cài đặt giao diện" trên thanh trên cùng của layout
// (xem cai-dat-giao-dien.js), nên ở đây bắt sự kiện theo kiểu uỷ quyền:
// nút được chèn động lúc nào cũng chạy được.
// Lựa chọn lưu ở localStorage nên đổi ở một trang thì các trang khác cũng theo.
(function () {
    const KEY = "eformDangXemDon";
    let mode = "list";
    const subscribers = [];

    try {
        const saved = localStorage.getItem(KEY);
        if (saved === "card" || saved === "list") mode = saved;
    } catch (e) { }

    function escapeHtml(str) {
        return String(str ?? "").replace(/[&<>"']/g, c => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }

    // Đồng bộ trạng thái "đang chọn" cho mọi nút chuyển đang có trên trang
    function refreshButtons() {
        document.querySelectorAll(".view-mode-btn").forEach(btn => {
            btn.classList.toggle("active", btn.dataset.view === mode);
        });
    }

    function setMode(next) {
        if (next !== "list" && next !== "card") return;
        if (next === mode) return;
        mode = next;
        try { localStorage.setItem(KEY, mode); } catch (e) { }
        refreshButtons();
        subscribers.forEach(cb => { try { cb(mode); } catch (e) { console.error(e); } });
    }

    // Trang có hỗ trợ dạng thẻ đăng ký hàm vẽ lại của mình ở đây
    function onChange(cb) {
        if (typeof cb === "function") subscribers.push(cb);
    }

    // map(item) trả về mô tả thẻ, các trang tự quy đổi dữ liệu của mình:
    // { catalog, statusText, statusBg, statusFg, title, avatar, name, sub,
    //   chips: [], emptyChipText, progressWidth, progressColor, date, link, linkText, linkMuted }
    function renderCards(pageData, map, gridId) {
        const wrap = document.getElementById(gridId || "cardViewGrid");
        if (!wrap) return;

        wrap.innerHTML = "";
        pageData.forEach(item => {
            const c = map(item);

            let chipsHtml;
            const chips = (c.chips || []).filter(Boolean);
            if (chips.length > 0) {
                chipsHtml = chips.map(n =>
                    `<span class="don-card-chip"><i class="fa fa-user-circle-o"></i> ${escapeHtml(n)}</span>`
                ).join("");
            } else {
                chipsHtml = `<span class="don-card-chip-empty">${escapeHtml(c.emptyChipText || "Chưa có tiếp nhận")}</span>`;
            }

            const el = document.createElement("div");
            el.className = "don-card";
            el.innerHTML = `
                <div class="don-card-top">
                    <span class="don-card-cat">${escapeHtml(c.catalog)}</span>
                    <span class="don-card-status" style="background:${c.statusBg}; color:${c.statusFg}; border-color:${c.statusFg};">${escapeHtml(c.statusText)}</span>
                </div>
                <div class="don-card-title">${escapeHtml(c.title)}</div>
                <div class="don-card-person">
                    <div class="don-card-avatar">${escapeHtml(c.avatar || (c.name || "?").trim().charAt(0).toUpperCase())}</div>
                    <div>
                        <div class="don-card-name">${escapeHtml(c.name)}</div>
                        <div class="don-card-sub">${escapeHtml(c.sub)}</div>
                    </div>
                </div>
                <div class="don-card-chips">${chipsHtml}</div>
                <div class="don-card-progress"><div style="width:${c.progressWidth}; background:${c.progressColor};"></div></div>
                <div class="don-card-foot">
                    <span class="don-card-date"><i class="fa fa-calendar-o"></i> ${escapeHtml(c.date)}</span>
                    <a class="don-card-link ${c.linkMuted ? "muted" : ""}" href="${c.link}">${escapeHtml(c.linkText || "Chi tiết")} <i class="fa fa-arrow-right"></i></a>
                </div>
            `;
            wrap.appendChild(el);
        });
    }

    document.addEventListener("click", e => {
        const btn = e.target.closest(".view-mode-btn");
        if (!btn) return;
        e.preventDefault();
        setMode(btn.dataset.view);
    });

    document.addEventListener("DOMContentLoaded", refreshButtons);

    window.DangXemDon = {
        onChange,
        renderCards,
        refreshButtons,
        setMode,
        getMode: () => mode
    };
})();

// ============================================================
// DẠNG THẺ TỰ ĐỘNG CHO BẢNG THƯỜNG
// Bảng nào chỉ cần đánh dấu <table data-dang-xem="auto"> là có dạng thẻ,
// không phải viết mapper riêng: thẻ dựng từ chính các ô của dòng (nhân bản
// node nên nút bấm/liên kết trong ô vẫn hoạt động như cũ).
// ============================================================
(function () {
    const dsBangTuDong = [];

    function nhanTieuDe(table) {
        return Array.from(table.querySelectorAll("thead th")).map(th => th.textContent.trim());
    }

    function veThe(muc) {
        const { table, grid, wrap } = muc;
        const tbody = table.tBodies[0];
        if (!tbody) return;

        const isCard = DangXemDon.getMode() === "card";
        const rows = Array.from(tbody.rows);
        // Dòng báo rỗng/đang tải (một ô gộp cả bảng) thì giữ nguyên bảng
        const chiLaThongBao = rows.length === 0 || (rows.length === 1 && rows[0].cells.length === 1);

        if (!isCard || chiLaThongBao) {
            wrap.style.display = "";
            grid.style.display = "none";
            return;
        }

        wrap.style.display = "none";
        grid.style.display = "";
        grid.innerHTML = "";

        const tieuDe = nhanTieuDe(table);
        rows.forEach(row => {
            const cells = Array.from(row.cells);
            if (cells.length === 0) return;

            const card = document.createElement("div");
            card.className = "don-card don-card-auto";

            const head = document.createElement("div");
            head.className = "don-card-title";
            head.append(...Array.from(cells[0].childNodes).map(n => n.cloneNode(true)));
            card.appendChild(head);

            cells.slice(1).forEach((td, i) => {
                if (!td.textContent.trim() && !td.querySelector("a,button,input,img,i")) return;
                const dong = document.createElement("div");
                dong.className = "don-card-kv";

                const nhan = document.createElement("span");
                nhan.className = "don-card-kv-label";
                nhan.textContent = td.dataset.label || tieuDe[i + 1] || "";

                const giaTri = document.createElement("span");
                giaTri.className = "don-card-kv-value";
                giaTri.append(...Array.from(td.childNodes).map(n => n.cloneNode(true)));

                dong.appendChild(nhan);
                dong.appendChild(giaTri);
                card.appendChild(dong);
            });

            grid.appendChild(card);
        });
    }

    function gan(table) {
        // Ẩn cả khối cuộn bao ngoài (nếu có) để không còn khung bảng rỗng
        const wrap = table.closest(".table-responsive, .table-scroll, .ccdc-table-wrap") || table;
        const grid = document.createElement("div");
        grid.className = "card-view-grid";
        grid.style.display = "none";
        wrap.parentNode.insertBefore(grid, wrap.nextSibling);

        const muc = { table, grid, wrap };
        dsBangTuDong.push(muc);

        // Bảng được vẽ lại bằng JS của trang -> theo dõi để dựng lại thẻ
        new MutationObserver(() => veThe(muc)).observe(table, { childList: true, subtree: true });
        veThe(muc);
    }

    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll('table[data-dang-xem="auto"]').forEach(gan);
    });

    DangXemDon.onChange(() => dsBangTuDong.forEach(veThe));
})();
