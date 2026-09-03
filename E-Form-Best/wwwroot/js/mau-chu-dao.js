// Màu chủ đạo do người dùng tự chọn (hộp "Cài đặt giao diện").
// Nạp đồng bộ trong <head> để màu có ngay từ nhịp vẽ đầu, không nháy màu cũ.
//
// Cách hoạt động: chỉ đặt vài biến CSS trên <html> và bật cờ data-mau-chu-dao;
// việc tô màu do mau-chu-dao.css lo. Không chọn màu thì không có cờ, giao diện
// giữ nguyên màu gốc của từng phân hệ (IT xanh, SHD xanh biển, Công việc lục...).
(function () {
    const KEY = "eformMauChuDao";
    let mau = null;

    try {
        const saved = localStorage.getItem(KEY);
        if (saved && /^#[0-9a-fA-F]{6}$/.test(saved)) mau = saved.toLowerCase();
    } catch (e) { }

    function hexSangRgb(hex) {
        return {
            r: parseInt(hex.slice(1, 3), 16),
            g: parseInt(hex.slice(3, 5), 16),
            b: parseInt(hex.slice(5, 7), 16)
        };
    }

    function rgbSangHsl(c) {
        const r = c.r / 255, g = c.g / 255, b = c.b / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        const l = (max + min) / 2;
        const d = max - min;
        let h = 0, s = 0;
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

    function apDung() {
        const root = document.documentElement;

        if (!mau) {
            root.removeAttribute("data-mau-chu-dao");
            ["--mau-chu-dao", "--mau-chu-dao-dam", "--mau-chu-dao-sang", "--mau-chu-dao-mo"]
                .forEach(v => root.style.removeProperty(v));
            return;
        }

        const rgb = hexSangRgb(mau);
        const hsl = rgbSangHsl(rgb);
        const h = Math.round(hsl.h);
        const s = Math.round(Math.max(hsl.s, 0.35) * 100);

        root.style.setProperty("--mau-chu-dao", mau);
        // đậm hơn cho trạng thái hover / gradient
        root.style.setProperty("--mau-chu-dao-dam", `hsl(${h}, ${s}%, ${Math.round(Math.max(hsl.l - 0.12, 0.14) * 100)}%)`);
        // sáng hơn để làm màu chữ, nhất là khi đang ở chế độ tối
        root.style.setProperty("--mau-chu-dao-sang", `hsl(${h}, ${s}%, 72%)`);
        // nền nhạt cho chip / nhãn
        root.style.setProperty("--mau-chu-dao-mo", `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, 0.16)`);
        root.setAttribute("data-mau-chu-dao", "1");
    }

    // Đánh dấu ô màu đang chọn trong hộp cài đặt
    function refreshButtons() {
        let trungOChonSan = false;
        document.querySelectorAll(".mau-chu-dao-item").forEach(el => {
            const dangChon = !!mau && el.dataset.mau === mau;
            if (dangChon) trungOChonSan = true;
            el.classList.toggle("active", dangChon);
        });

        const input = document.getElementById("inputMauChuDao");
        if (input) input.value = mau || "#1e40af";

        // "Tự pha" sáng lên khi màu đang dùng không nằm trong các ô chọn sẵn
        const nutTuPha = document.getElementById("btnTuPhaMau");
        if (nutTuPha) nutTuPha.classList.toggle("active", !!mau && !trungOChonSan);

        const nutMacDinh = document.getElementById("btnMauMacDinh");
        if (nutMacDinh) nutMacDinh.classList.toggle("active", !mau);
    }

    function setMau(hex) {
        if (hex && !/^#[0-9a-fA-F]{6}$/.test(hex)) return;
        mau = hex ? hex.toLowerCase() : null;
        try {
            if (mau) localStorage.setItem(KEY, mau);
            else localStorage.removeItem(KEY);
        } catch (e) { }
        apDung();
        refreshButtons();
    }

    apDung();

    document.addEventListener("click", e => {
        const o = e.target.closest(".mau-chu-dao-item");
        if (o) {
            e.preventDefault();
            setMau(o.dataset.mau);
            return;
        }
        if (e.target.closest("#btnMauMacDinh")) {
            e.preventDefault();
            setMau(null);
            // "Mặc định" nghĩa là trả giao diện về nguyên bản, nên tắt luôn
            // hiệu ứng nền theo mùa / ngày lễ
            if (window.HieuUngMua) HieuUngMua.tat();
            return;
        }
        // Nút "Tự pha" chỉ là bề mặt: mở hộp chọn màu thật của trình duyệt
        if (e.target.closest("#btnTuPhaMau")) {
            e.preventDefault();
            const input = document.getElementById("inputMauChuDao");
            if (input) {
                input.style.pointerEvents = "auto";
                input.click();
                input.style.pointerEvents = "none";
            }
        }
    });

    document.addEventListener("input", e => {
        if (e.target && e.target.id === "inputMauChuDao") setMau(e.target.value);
    });

    document.addEventListener("DOMContentLoaded", refreshButtons);

    window.MauChuDao = {
        setMau,
        refreshButtons,
        getMau: () => mau
    };
})();
