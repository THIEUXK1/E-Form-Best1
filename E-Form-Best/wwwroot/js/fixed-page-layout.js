/* ============================================================
   FIXED PAGE LAYOUT
   - Khoá cuộn toàn trang (desktop), chỉ vùng bảng được cuộn.
   - Tự dựng chuỗi flex từ .main-card xuống tới vùng cuộn.
   - Tự chèn nút "Ẩn lọc / Hiện lọc" cho khối bộ lọc của trang.

   Cách dùng trong View:
       <link rel="stylesheet" href="~/css/fixed-page-layout.css" />
       <script src="~/js/fixed-page-layout.js" defer></script>

   Tuỳ chọn:
       data-fp-scroll  : chỉ định vùng cuộn (mặc định: các .table-responsive)
       data-fp-filter  : chỉ định khối bộ lọc cần ẩn/hiện
       (Nút thu gọn layout nằm ở header của _Layout.cshtml)
   ============================================================ */
(function () {
    "use strict";

    var MOBILE_MAX = 768;
    var FILTER_SELECTOR = "[data-fp-filter], .advanced-filter, .filter-panel, #filterBar, .tscn-filter-card";
    var STORAGE_KEY = "fpHideFilter::" + window.location.pathname.toLowerCase();

    function isUsable(el) {
        // Bỏ qua bảng nằm trong modal / dropdown / vùng in ấn
        return !el.closest(".modal, .dropdown-menu, .offcanvas, .fp-ignore");
    }

    /* --- Dựng chuỗi flex từ vùng cuộn ngược lên .main-card --- */
    function buildChain(scrollEl, path) {
        var card = scrollEl.closest(".main-card");
        if (!card) return false;

        scrollEl.classList.add("fp-scroll");
        // Xoá giới hạn chiều cao cứng có sẵn trong view (vd: max-height: 70vh)
        scrollEl.style.maxHeight = "none";
        scrollEl.style.height = "auto";

        var node = scrollEl;
        while (node && node !== card && node.parentElement) {
            node.classList.add("fp-grow");
            path.push(node);
            var parent = node.parentElement;
            if (parent === card) break;
            parent.classList.add(parent.classList.contains("row") ? "fp-chain-row" : "fp-chain");
            node = parent;
        }
        return true;
    }

    function applyLayout() {
        var targets = document.querySelectorAll("[data-fp-scroll]");
        if (!targets.length) targets = document.querySelectorAll(".table-responsive");

        var path = [];
        var found = 0;
        Array.prototype.forEach.call(targets, function (el) {
            if (isUsable(el) && buildChain(el, path)) found++;
        });
        return found > 0;
    }

    /* --- Nút ẩn / hiện bộ lọc --- */
    function setFilterState(panels, btn, hidden) {
        panels.forEach(function (p) { p.classList.toggle("fp-filter-hidden", hidden); });
        btn.classList.toggle("is-off", hidden);
        btn.querySelector("i").className = hidden ? "fa fa-filter" : "fa fa-chevron-up";
        btn.querySelector("span").textContent = hidden ? "Hiện lọc" : "Ẩn lọc";
    }

    function setupToolbar() {
        var card = document.querySelector(".main-card");
        if (!card) return;

        var panels = Array.prototype.filter.call(
            card.querySelectorAll(FILTER_SELECTOR),
            function (el) { return isUsable(el); }
        );

        if (!panels.length) return;

        var bar = document.createElement("div");
        bar.className = "fp-filter-bar";

        var btn = document.createElement("button");
        btn.type = "button";
        btn.className = "fp-filter-toggle";
        btn.title = "Ẩn/hiện bộ lọc";
        btn.innerHTML = '<i class="fa fa-chevron-up"></i> <span>Ẩn lọc</span>';
        bar.appendChild(btn);

        panels[0].parentElement.insertBefore(bar, panels[0]);

        btn.addEventListener("click", function () {
            var hidden = !panels[0].classList.contains("fp-filter-hidden");
            localStorage.setItem(STORAGE_KEY, hidden ? "1" : "0");
            setFilterState(panels, btn, hidden);
        });

        setFilterState(panels, btn, localStorage.getItem(STORAGE_KEY) === "1");
    }

    function init() {
        if (applyLayout() && window.innerWidth > MOBILE_MAX) {
            document.documentElement.classList.add("fp-lock");
        }
        setupToolbar();

        // Trang tải dữ liệu bằng ajax: bảng có thể được chèn sau -> dựng lại chuỗi
        var replays = 0;
        var timer = setInterval(function () {
            applyLayout();
            if (++replays >= 6) clearInterval(timer);
        }, 800);

        window.addEventListener("resize", function () {
            var lock = window.innerWidth > MOBILE_MAX;
            document.documentElement.classList.toggle("fp-lock", lock && applyLayout());
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
