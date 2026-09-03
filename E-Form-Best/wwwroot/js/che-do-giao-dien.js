// Chế độ sáng / tối cho toàn hệ thống.
// File này được nạp ĐỒNG BỘ ngay trong <head> của layout để đặt data-theme
// trước khi trang vẽ ra, tránh nháy trắng một nhịp rồi mới chuyển sang tối.
// Nút chọn nằm trong hộp "Cài đặt giao diện" (cai-dat-giao-dien.js).
//
// Vì hầu hết view trong dự án tô màu bằng style nội tuyến (và JS còn sinh thêm
// style lúc chạy), CSS phủ sẵn trong che-do-toi.css không thể bắt hết. Nên ở
// đây có thêm một lượt quét DOM: đọc màu THỰC TẾ đang hiển thị của từng phần
// tử, chỗ nào còn sáng thì hạ tối ngay tại phần tử đó (giữ nguyên tông màu gốc
// để badge/trạng thái không mất ý nghĩa). Bỏ chế độ tối thì trả lại như cũ.
(function () {
    const KEY = "eformChuDeGiaoDien";
    let mode = "light";
    const subscribers = [];

    try {
        const saved = localStorage.getItem(KEY);
        if (saved === "dark" || saved === "light") mode = saved;
    } catch (e) { }

    // ---------- tiện ích màu ----------
    function docMau(str) {
        if (!str) return null;
        const m = str.match(/rgba?\(([^)]+)\)/);
        if (!m) return null;
        const p = m[1].split(",").map(x => parseFloat(x.trim()));
        if (p.length < 3 || p.some(isNaN)) return null;
        return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 };
    }

    function sangToi(c) {
        // độ sáng cảm nhận, 0 = tối đen, 1 = trắng
        return (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255;
    }

    function doBaoHoa(c) {
        const max = Math.max(c.r, c.g, c.b), min = Math.min(c.r, c.g, c.b);
        if (max === 0) return 0;
        return (max - min) / max;
    }

    function sangHSL(c) {
        const r = c.r / 255, g = c.g / 255, b = c.b / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        let h = 0;
        const l = (max + min) / 2;
        const d = max - min;
        let s = 0;
        if (d !== 0) {
            s = d / (1 - Math.abs(2 * l - 1));
            if (max === r) h = ((g - b) / d) % 6;
            else if (max === g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        return { h, s, l };
    }

    // ---------- lượt quét hạ sáng ----------
    const NEN_APP = "#0b1727";
    const NEN_MAT = "#13233d";
    const VIEN = "#2c4468";
    const CHU = "#e6edf7";

    const daSua = [];        // phần tử đã bị đổi, để trả lại khi về chế độ sáng
    const BO_QUA = new Set(["SCRIPT", "STYLE", "SVG", "PATH", "IMG", "CANVAS", "VIDEO", "IFRAME", "BR", "HR"]);
    let dangQuet = false;

    function ghiDe(el, thuocTinh, giaTri) {
        let luu = el.__mauGoc;
        if (!luu) {
            luu = el.__mauGoc = {};
            daSua.push(el);
        }
        if (!(thuocTinh in luu)) {
            luu[thuocTinh] = {
                value: el.style.getPropertyValue(thuocTinh),
                priority: el.style.getPropertyPriority(thuocTinh)
            };
        }
        el.style.setProperty(thuocTinh, giaTri, "important");
    }

    function xuLyPhanTu(el) {
        if (BO_QUA.has(el.tagName)) return;
        if (el.closest("#sidebar")) return; // sidebar đã có gradient tối riêng

        const cs = getComputedStyle(el);

        // 1) Nền còn sáng -> hạ tối, giữ tông màu gốc
        const nen = docMau(cs.backgroundColor);
        if (nen && nen.a > 0.05) {
            const l = sangToi(nen);
            if (l > 0.8) {
                const hsl = sangHSL(nen);
                const bh = doBaoHoa(nen);
                ghiDe(el, "background-color",
                    bh < 0.12 ? NEN_MAT : `hsl(${Math.round(hsl.h)}, ${Math.round(Math.min(hsl.s, 0.6) * 100)}%, 17%)`);
            }
        }

        // 2) Chữ quá tối trên nền tối -> nâng sáng, giữ tông
        const chu = docMau(cs.color);
        if (chu && chu.a > 0.05) {
            const l = sangToi(chu);
            if (l < 0.45) {
                const hsl = sangHSL(chu);
                const bh = doBaoHoa(chu);
                ghiDe(el, "color",
                    bh < 0.12 ? CHU : `hsl(${Math.round(hsl.h)}, ${Math.round(Math.max(hsl.s, 0.55) * 100)}%, 72%)`);
            }
        }

        // 3) Viền sáng -> viền xanh đậm
        ["Top", "Right", "Bottom", "Left"].forEach(canh => {
            if (parseFloat(cs["border" + canh + "Width"]) > 0) {
                const v = docMau(cs["border" + canh + "Color"]);
                if (v && v.a > 0.05 && sangToi(v) > 0.72) {
                    ghiDe(el, "border-" + canh.toLowerCase() + "-color", VIEN);
                }
            }
        });
    }

    function quet(goc) {
        if (mode !== "dark" || !document.body) return;
        dangQuet = true;
        try {
            const root = goc && goc.nodeType === 1 ? goc : document.body;
            xuLyPhanTu(root);
            root.querySelectorAll("*").forEach(xuLyPhanTu);
            document.body.style.setProperty("background-color", NEN_APP, "important");
        } finally {
            dangQuet = false;
        }
    }

    function traLai() {
        daSua.forEach(el => {
            const luu = el.__mauGoc;
            if (!luu) return;
            Object.keys(luu).forEach(tt => {
                el.style.removeProperty(tt);
                if (luu[tt].value) el.style.setProperty(tt, luu[tt].value, luu[tt].priority);
            });
            delete el.__mauGoc;
        });
        daSua.length = 0;
        document.body && document.body.style.removeProperty("background-color");
    }

    // Nội dung vẽ động (bảng, thẻ, modal) cũng phải được hạ sáng theo
    let hen = null;
    function henQuet(goc) {
        if (mode !== "dark") return;
        clearTimeout(hen);
        hen = setTimeout(() => quet(goc), 60);
    }

    function theoDoiDOM() {
        new MutationObserver(dsThayDoi => {
            if (dangQuet || mode !== "dark") return;
            const coThemNode = dsThayDoi.some(x => x.addedNodes && x.addedNodes.length > 0);
            if (coThemNode) henQuet();
        }).observe(document.body, { childList: true, subtree: true });
    }

    // ---------- điều khiển chế độ ----------
    function apDung() {
        document.documentElement.setAttribute("data-theme", mode);
    }

    function refreshButtons() {
        document.querySelectorAll(".theme-mode-btn").forEach(btn => {
            btn.classList.toggle("active", btn.dataset.theme === mode);
        });
    }

    function setMode(next) {
        if (next !== "light" && next !== "dark") return;
        if (next === mode) return;
        mode = next;
        try { localStorage.setItem(KEY, mode); } catch (e) { }
        apDung();
        refreshButtons();
        if (mode === "dark") quet(); else traLai();
        subscribers.forEach(cb => { try { cb(mode); } catch (e) { console.error(e); } });
    }

    function onChange(cb) {
        if (typeof cb === "function") subscribers.push(cb);
    }

    apDung();

    document.addEventListener("click", e => {
        const btn = e.target.closest(".theme-mode-btn");
        if (!btn) return;
        e.preventDefault();
        setMode(btn.dataset.theme);
    });

    document.addEventListener("DOMContentLoaded", () => {
        refreshButtons();
        quet();
        theoDoiDOM();
    });

    // Ảnh/phông tải xong có thể làm bố cục đổi -> quét thêm một lượt cho chắc
    window.addEventListener("load", () => henQuet());

    window.ChuDeGiaoDien = {
        onChange,
        setMode,
        refreshButtons,
        quetLai: () => quet(),
        getMode: () => mode
    };
})();
