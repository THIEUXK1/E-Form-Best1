// Menu thu gọn (lite): sidebar của layout rút lại còn mỗi biểu tượng.
// Nạp đồng bộ trong <head> để trang không nhấp nháy menu rộng rồi mới co lại.
// Nút bật/tắt nằm trong hộp "Cài đặt giao diện" (cai-dat-giao-dien.js).
(function () {
    const KEY = "eformMenuThuGon";
    let gon = false;

    try {
        gon = localStorage.getItem(KEY) === "1";
    } catch (e) { }

    function apDung() {
        if (gon) document.documentElement.setAttribute("data-menu-gon", "1");
        else document.documentElement.removeAttribute("data-menu-gon");
    }

    // Chữ của mục menu bị ẩn khi thu gọn -> chuyển thành chú thích rê chuột
    function ganGoiY() {
        document.querySelectorAll("#sidebar .nav-link-item").forEach(a => {
            if (a.dataset.goiY) return;
            const chu = (a.querySelector(".lang-text") || a).textContent.trim();
            if (chu) a.dataset.goiY = chu;
        });
    }

    // Bốn layout dựng sidebar khác nhau: IT có sẵn class (.brand-text,
    // .nav-section-title, .lang-text), HR/SHD/Công việc thì phần lớn là thẻ
    // trơn tô bằng style nội tuyến. Nên thay vì đoán theo class, ở đây đánh dấu
    // theo CẤU TRÚC: cái nào là chữ thì gắn class .menu-an-khi-gon, CSS chỉ
    // việc ẩn class đó lúc thu gọn. Nhờ vậy 4 layout thu gọn giống hệt nhau.
    function chuanBiMenu() {
        const sidebar = document.getElementById("sidebar");
        if (!sidebar || sidebar.dataset.daChuanBi) return;

        // 1) Khối tên thương hiệu cạnh ô logo vuông
        const logo = sidebar.querySelector(".brand-row a, :scope > a");
        if (logo) {
            Array.from(logo.children).forEach((con, i) => {
                if (i > 0) con.classList.add("menu-an-khi-gon");
            });
        }

        // 2) Tiêu đề nhóm ("Cá nhân & Đăng ký", "Phê duyệt"...) — là thẻ div
        //    nằm thẳng trong <nav>, không phải đường kẻ phân cách
        const nav = sidebar.querySelector("#main-sidebar-nav");
        if (nav) {
            Array.from(nav.children).forEach(el => {
                if (el.tagName === "DIV" && !el.classList.contains("nav-divider") && !el.querySelector("a")) {
                    el.classList.add("menu-an-khi-gon");
                }
            });
        }

        // 3) Trong mỗi mục menu: phần nào không chứa biểu tượng thì là chữ
        sidebar.querySelectorAll(".nav-link-item").forEach(muc => {
            muc.querySelectorAll("span").forEach(sp => {
                if (!sp.querySelector("i")) sp.classList.add("menu-an-khi-gon");
            });
            // mũi tên ">" ở cuối mục
            const dsIcon = Array.from(muc.children).filter(x => x.tagName === "I");
            if (dsIcon.length > 1) dsIcon.slice(1).forEach(x => x.classList.add("menu-an-khi-gon"));
            if (muc.children.length > 1 && muc.lastElementChild.tagName === "I" && muc.firstElementChild.tagName !== "I") {
                muc.lastElementChild.classList.add("menu-an-khi-gon");
            }
        });

        // 4) Khối tài khoản dưới chân menu: giữ ảnh đại diện, ẩn phần chữ
        const nutUser = sidebar.querySelector("#userMenuButton");
        if (nutUser) {
            Array.from(nutUser.children).forEach((con, i) => {
                if (i > 0) con.classList.add("menu-an-khi-gon");
            });
        }

        sidebar.dataset.daChuanBi = "1";
    }

    // Chú thích khi rê chuột vào biểu tượng.
    // Không dùng ::after trong sidebar được: khối <nav> có overflow-y:auto nên
    // mọi thứ tràn ra ngoài bề ngang đều bị cắt. Vì vậy vẽ một ô nổi
    // position:fixed gắn thẳng vào <body>.
    let oGoiY = null;

    function layOGoiY() {
        if (!oGoiY) {
            oGoiY = document.createElement("div");
            oGoiY.className = "menu-goi-y";
            document.body.appendChild(oGoiY);
        }
        return oGoiY;
    }

    function hienGoiY(el) {
        if (!gon || window.innerWidth < 769) return;
        const chu = el.dataset.goiY;
        if (!chu) return;

        const o = layOGoiY();
        o.textContent = chu;
        const r = el.getBoundingClientRect();
        o.style.left = (r.right + 12) + "px";
        o.style.top = (r.top + r.height / 2) + "px";
        o.classList.add("hien");
    }

    function anGoiY() {
        if (oGoiY) oGoiY.classList.remove("hien");
    }

    function refreshButtons() {
        document.querySelectorAll(".menu-mode-btn").forEach(btn => {
            btn.classList.toggle("active", (btn.dataset.menu === "gon") === gon);
        });
    }

    function setGon(bat) {
        gon = !!bat;
        try { localStorage.setItem(KEY, gon ? "1" : "0"); } catch (e) { }
        apDung();
        chuanBiMenu();
        ganGoiY();
        anGoiY();
        refreshButtons();
    }

    apDung();

    document.addEventListener("click", e => {
        const btn = e.target.closest(".menu-mode-btn");
        if (!btn) return;
        e.preventDefault();
        setGon(btn.dataset.menu === "gon");
    });

    document.addEventListener("DOMContentLoaded", () => {
        chuanBiMenu();
        ganGoiY();
        refreshButtons();

        const sidebar = document.getElementById("sidebar");
        if (sidebar) {
            sidebar.addEventListener("mouseover", e => {
                const muc = e.target.closest(".nav-link-item");
                if (muc) hienGoiY(muc);
            });
            sidebar.addEventListener("mouseout", e => {
                if (e.target.closest(".nav-link-item")) anGoiY();
            });
            sidebar.addEventListener("scroll", anGoiY, true);
        }
        window.addEventListener("scroll", anGoiY, true);
    });

    window.MenuThuGon = {
        setGon,
        refreshButtons,
        dangGon: () => gon
    };
})();
