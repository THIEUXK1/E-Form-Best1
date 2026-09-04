// Hiệu ứng nền trang chủ (/): màu đổi theo NGÀY LỄ Việt Nam (ưu tiên) và theo mùa
// máy và theo ngày/đêm, kèm lớp hạt rơi nhẹ.
// MẶC ĐỊNH TẮT. Ai thích thì bấm nút ở góc phải trên để bật; chỉ khi đã tự tay
// bật (lưu "1") thì lần sau vào mới chạy sẵn.
(function () {
    const KEY = "eformHieuUngMua";
    let bat = false;
    try { bat = localStorage.getItem(KEY) === "1"; } catch (e) { }

    // Mùa theo lịch miền Bắc: xuân 2-4, hạ 5-7, thu 8-10, đông 11-1
    const MUA = {
        xuan: {
            ten: "Xuân",
            bieuTuong: "🌸",
            ngay: "radial-gradient(circle at 30% 20%, #2b5f7a, #1f4a63 45%, #10283a)",
            dem: "radial-gradient(circle at 30% 20%, #1d3f57, #14304a 45%, #08172a)",
            hat: { ky_tu: ["🌸", "🌷"], mau: "#f9a8d4", toc_do: 0.5 }
        },
        ha: {
            ten: "Hạ",
            bieuTuong: "☀️",
            ngay: "radial-gradient(circle at 30% 20%, #1c6b74, #14545f 45%, #06222c)",
            dem: "radial-gradient(circle at 30% 20%, #123f4d, #0d3040 45%, #041824)",
            hat: { ky_tu: ["✦", "•"], mau: "#fde68a", toc_do: 0.35 }
        },
        thu: {
            ten: "Thu",
            bieuTuong: "🍂",
            ngay: "radial-gradient(circle at 30% 20%, #6b4a2a, #4d3520 45%, #21160c)",
            dem: "radial-gradient(circle at 30% 20%, #46301c, #322213 45%, #150e07)",
            hat: { ky_tu: ["🍂", "🍁"], mau: "#fdba74", toc_do: 0.6 }
        },
        dong: {
            ten: "Đông",
            bieuTuong: "❄️",
            ngay: "radial-gradient(circle at 30% 20%, #24486b, #1b3552 45%, #0a1526)",
            dem: "radial-gradient(circle at 30% 20%, #17314c, #112438 45%, #060d18)",
            hat: { ky_tu: ["❄", "•"], mau: "#e0f2fe", toc_do: 0.45 }
        }
    };

    function muaHienTai() {
        const t = new Date().getMonth() + 1;
        if (t >= 2 && t <= 4) return MUA.xuan;
        if (t >= 5 && t <= 7) return MUA.ha;
        if (t >= 8 && t <= 10) return MUA.thu;
        return MUA.dong;
    }

    function banNgay() {
        const g = new Date().getHours();
        return g >= 6 && g < 18;
    }

    // ============================================================
    // NGÀY LỄ VIỆT NAM — ưu tiên hơn mùa
    // ============================================================

    // Đổi dương lịch sang âm lịch (thuật toán Hồ Ngọc Đức, múi giờ +7).
    // Cần cho Tết, Trung thu, Giỗ Tổ vì đó là ngày âm, mỗi năm rơi vào một
    // ngày dương khác nhau.
    function jdTuNgay(dd, mm, yy) {
        const a = Math.floor((14 - mm) / 12);
        const y = yy + 4800 - a;
        const m = mm + 12 * a - 3;
        let jd = dd + Math.floor((153 * m + 2) / 5) + 365 * y + Math.floor(y / 4)
            - Math.floor(y / 100) + Math.floor(y / 400) - 32045;
        if (jd < 2299161) {
            jd = dd + Math.floor((153 * m + 2) / 5) + 365 * y + Math.floor(y / 4) - 32083;
        }
        return jd;
    }

    function ngayTrangMoi(k) {
        const T = k / 1236.85;
        const T2 = T * T, T3 = T2 * T;
        const dr = Math.PI / 180;
        let Jd1 = 2415020.75933 + 29.53058868 * k + 0.0001178 * T2 - 0.000000155 * T3;
        Jd1 += 0.00033 * Math.sin((166.56 + 132.87 * T - 0.009173 * T2) * dr);
        const M = 359.2242 + 29.10535608 * k - 0.0000333 * T2 - 0.00000347 * T3;
        const Mpr = 306.0253 + 385.81691806 * k + 0.0107306 * T2 + 0.00001236 * T3;
        const F = 21.2964 + 390.67050646 * k - 0.0016528 * T2 - 0.00000239 * T3;
        let C1 = (0.1734 - 0.000393 * T) * Math.sin(M * dr) + 0.0021 * Math.sin(2 * dr * M);
        C1 = C1 - 0.4068 * Math.sin(Mpr * dr) + 0.0161 * Math.sin(dr * 2 * Mpr);
        C1 = C1 - 0.0004 * Math.sin(dr * 3 * Mpr);
        C1 = C1 + 0.0104 * Math.sin(dr * 2 * F) - 0.0051 * Math.sin(dr * (M + Mpr));
        C1 = C1 - 0.0074 * Math.sin(dr * (M - Mpr)) + 0.0004 * Math.sin(dr * (2 * F + M));
        C1 = C1 - 0.0004 * Math.sin(dr * (2 * F - M)) - 0.0006 * Math.sin(dr * (2 * F + Mpr));
        C1 = C1 + 0.0010 * Math.sin(dr * (2 * F - Mpr)) + 0.0005 * Math.sin(dr * (2 * Mpr + M));
        let deltat;
        if (T < -11) {
            deltat = 0.001 + 0.000839 * T + 0.0002261 * T2 - 0.00000845 * T3 - 0.000000081 * T * T3;
        } else {
            deltat = -0.000278 + 0.000265 * T + 0.000262 * T2;
        }
        return Math.floor(Jd1 + C1 - deltat + 0.5 + 8 / 24.0);
    }

    function kinhDoMatTroi(jdn) {
        const T = (jdn - 2451545.0) / 36525;
        const T2 = T * T;
        const dr = Math.PI / 180;
        const M = 357.52910 + 35999.05030 * T - 0.0001559 * T2 - 0.00000048 * T * T2;
        const L0 = 280.46645 + 36000.76983 * T + 0.0003032 * T2;
        let DL = (1.914600 - 0.004817 * T - 0.000014 * T2) * Math.sin(dr * M);
        DL += (0.019993 - 0.000101 * T) * Math.sin(dr * 2 * M) + 0.000290 * Math.sin(dr * 3 * M);
        let L = L0 + DL;
        L = L * dr;
        L = L - Math.PI * 2 * Math.floor(L / (Math.PI * 2));
        return Math.floor(L / Math.PI * 6);
    }

    function thangGieng11(yy) {
        const off = jdTuNgay(31, 12, yy) - 2415021;
        const k = Math.floor(off / 29.530588853);
        let nm = ngayTrangMoi(k);
        if (kinhDoMatTroi(nm) >= 9) nm = ngayTrangMoi(k - 1);
        return nm;
    }

    function thangNhuan(a11) {
        const k = Math.floor((a11 - 2415021.076998695) / 29.530588853 + 0.5);
        let last, i = 1;
        let arc = kinhDoMatTroi(ngayTrangMoi(k + 1));
        do {
            last = arc;
            i++;
            arc = kinhDoMatTroi(ngayTrangMoi(k + i));
        } while (arc !== last && i < 14);
        return i - 1;
    }

    // Trả về { ngay, thang } âm lịch
    function amLich(d) {
        const dd = d.getDate(), mm = d.getMonth() + 1, yy = d.getFullYear();
        const dayNumber = jdTuNgay(dd, mm, yy);
        const k = Math.floor((dayNumber - 2415021.076998695) / 29.530588853);
        let monthStart = ngayTrangMoi(k + 1);
        if (monthStart > dayNumber) monthStart = ngayTrangMoi(k);

        let a11 = thangGieng11(yy), b11;
        if (a11 >= monthStart) {
            b11 = a11;
            a11 = thangGieng11(yy - 1);
        } else {
            b11 = thangGieng11(yy + 1);
        }

        const lunarDay = dayNumber - monthStart + 1;
        const diff = Math.floor((monthStart - a11) / 29);
        let lunarMonth = diff + 11;

        if (b11 - a11 > 365) {
            const leapMonthDiff = thangNhuan(a11);
            if (diff >= leapMonthDiff) lunarMonth = diff + 10;
        }
        if (lunarMonth > 12) lunarMonth = lunarMonth - 12;

        return { ngay: lunarDay, thang: lunarMonth };
    }

    // Mỗi lễ mô tả như một "mùa" nhưng có thêm hàm kiểm tra ngày
    const LE = [
        {
            ten: "Tết Nguyên đán", bieuTuong: "🧧",
            hop: (d, al) => (al.thang === 12 && al.ngay >= 23) || (al.thang === 1 && al.ngay <= 5),
            ngay: "radial-gradient(circle at 30% 20%, #8a1b1b, #5e1212 45%, #2a0808)",
            dem: "radial-gradient(circle at 30% 20%, #611414, #400d0d 45%, #1c0505)",
            hat: { ky_tu: ["🌸", "🧧", "✦"], mau: "#fcd34d", toc_do: 0.5 }
        },
        {
            ten: "Tết Trung thu", bieuTuong: "🏮",
            hop: (d, al) => al.thang === 8 && al.ngay >= 14 && al.ngay <= 16,
            ngay: "radial-gradient(circle at 30% 20%, #4a3a7a, #33285c 45%, #16112c)",
            dem: "radial-gradient(circle at 30% 20%, #3a2d63, #261d47 45%, #100c20)",
            hat: { ky_tu: ["🏮", "🌕", "✦"], mau: "#fbbf24", toc_do: 0.4 }
        },
        {
            ten: "Giỗ Tổ Hùng Vương", bieuTuong: "🇻🇳",
            hop: (d, al) => al.thang === 3 && al.ngay === 10,
            ngay: "radial-gradient(circle at 30% 20%, #7a2020, #571616 45%, #240909)",
            dem: "radial-gradient(circle at 30% 20%, #571717, #3b0f0f 45%, #180606)",
            hat: { ky_tu: ["⭐", "✦"], mau: "#fde047", toc_do: 0.4 }
        },
        {
            ten: "Tết Dương lịch", bieuTuong: "🎆",
            hop: d => d.getMonth() === 0 && d.getDate() === 1,
            ngay: "radial-gradient(circle at 30% 20%, #24486b, #1b3552 45%, #0a1526)",
            dem: "radial-gradient(circle at 30% 20%, #17314c, #112438 45%, #060d18)",
            hat: { ky_tu: ["🎆", "✦", "❄"], mau: "#e0f2fe", toc_do: 0.5 }
        },
        {
            ten: "Quốc tế Phụ nữ", bieuTuong: "🌷",
            hop: d => d.getMonth() === 2 && d.getDate() === 8,
            ngay: "radial-gradient(circle at 30% 20%, #6d2350, #4d183a 45%, #240b1a)",
            dem: "radial-gradient(circle at 30% 20%, #4e1a3a, #360f28 45%, #170611)",
            hat: { ky_tu: ["🌷", "🌸"], mau: "#f9a8d4", toc_do: 0.45 }
        },
        {
            ten: "Giải phóng miền Nam & Quốc tế Lao động", bieuTuong: "🇻🇳",
            hop: d => (d.getMonth() === 3 && d.getDate() === 30) || (d.getMonth() === 4 && d.getDate() === 1),
            ngay: "radial-gradient(circle at 30% 20%, #8a1b1b, #5e1212 45%, #2a0808)",
            dem: "radial-gradient(circle at 30% 20%, #611414, #400d0d 45%, #1c0505)",
            hat: { ky_tu: ["⭐", "✦"], mau: "#fde047", toc_do: 0.45 }
        },
        {
            ten: "Quốc tế Thiếu nhi", bieuTuong: "🎈",
            hop: d => d.getMonth() === 5 && d.getDate() === 1,
            ngay: "radial-gradient(circle at 30% 20%, #1c6b74, #14545f 45%, #06222c)",
            dem: "radial-gradient(circle at 30% 20%, #123f4d, #0d3040 45%, #041824)",
            hat: { ky_tu: ["🎈", "✦"], mau: "#fde68a", toc_do: 0.5 }
        },
        {
            ten: "Quốc khánh", bieuTuong: "🇻🇳",
            hop: d => d.getMonth() === 8 && d.getDate() === 2,
            ngay: "radial-gradient(circle at 30% 20%, #8a1b1b, #5e1212 45%, #2a0808)",
            dem: "radial-gradient(circle at 30% 20%, #611414, #400d0d 45%, #1c0505)",
            hat: { ky_tu: ["⭐", "✦"], mau: "#fde047", toc_do: 0.45 }
        },
        {
            ten: "Phụ nữ Việt Nam", bieuTuong: "🌹",
            hop: d => d.getMonth() === 9 && d.getDate() === 20,
            ngay: "radial-gradient(circle at 30% 20%, #6d2350, #4d183a 45%, #240b1a)",
            dem: "radial-gradient(circle at 30% 20%, #4e1a3a, #360f28 45%, #170611)",
            hat: { ky_tu: ["🌹", "🌸"], mau: "#fda4af", toc_do: 0.45 }
        },
        {
            ten: "Nhà giáo Việt Nam", bieuTuong: "🎓",
            hop: d => d.getMonth() === 10 && d.getDate() === 20,
            ngay: "radial-gradient(circle at 30% 20%, #2f3f7a, #222e5c 45%, #0e142c)",
            dem: "radial-gradient(circle at 30% 20%, #253163, #1a2247 45%, #0a0f20)",
            hat: { ky_tu: ["🎓", "📚", "✦"], mau: "#bfdbfe", toc_do: 0.4 }
        },
        {
            ten: "Quân đội nhân dân", bieuTuong: "⭐",
            hop: d => d.getMonth() === 11 && d.getDate() === 22,
            ngay: "radial-gradient(circle at 30% 20%, #3d5a2a, #2b4020 45%, #121b0c)",
            dem: "radial-gradient(circle at 30% 20%, #2c421e, #1e2e15 45%, #0b1108)",
            hat: { ky_tu: ["⭐", "✦"], mau: "#fde047", toc_do: 0.4 }
        },
        {
            ten: "Giáng sinh", bieuTuong: "🎄",
            hop: d => d.getMonth() === 11 && d.getDate() >= 23 && d.getDate() <= 26,
            ngay: "radial-gradient(circle at 30% 20%, #1f5138, #163a29 45%, #081812)",
            dem: "radial-gradient(circle at 30% 20%, #16402c, #0f2c1f 45%, #05100b)",
            hat: { ky_tu: ["❄", "🎄", "✦"], mau: "#e0f2fe", toc_do: 0.5 }
        }
    ];

    // Hiệu ứng lễ kéo dài trước và sau ngày lễ 7 ngày.
    // Cách kiểm: xét từng ngày trong khoảng ±7 ngày quanh hôm nay, ngày nào
    // khớp luật của lễ thì lễ đó đang trong đợt. Làm vậy thì lễ âm lịch (Tết,
    // Trung thu) cũng tự đúng mà không cần tính riêng ngày dương của nó, và
    // trường hợp vắt qua năm mới (1/1) cũng không phải xử lý thêm.
    const SO_NGAY_KEO_DAI = 7;

    function chuDeHienTai() {
        const homNay = new Date();

        // Xét từ gần tới xa để lễ sát ngày nhất được ưu tiên khi hai lễ chồng nhau
        const dsLech = [0];
        for (let i = 1; i <= SO_NGAY_KEO_DAI; i++) dsLech.push(i, -i);

        for (const lech of dsLech) {
            const d = new Date(homNay.getFullYear(), homNay.getMonth(), homNay.getDate() + lech);
            let al;
            try {
                al = amLich(d);
            } catch (e) {
                al = { ngay: 0, thang: 0 };
            }
            const le = LE.find(x => {
                try { return x.hop(d, al); } catch (e) { return false; }
            });
            if (le) return le;
        }

        return muaHienTai();
    }

    // ---------- lớp hạt rơi ----------
    let canvas = null, ctx = null, hat = [], khungHinh = null, mauHat = "#ffffff";

    // Trang chủ: hạt phủ toàn màn hình. Các trang có layout: hạt chỉ rơi trong
    // khung menu bên trái, nằm sau chữ và biểu tượng của menu.
    function khungChua() {
        return document.getElementById("nenTrangChu") ? null : document.getElementById("sidebar");
    }

    function taoCanvas() {
        if (canvas) return canvas;
        const khung = khungChua();

        canvas = document.createElement("canvas");
        canvas.id = "lopHatMua";

        if (khung) {
            canvas.classList.add("hat-trong-menu");
            khung.appendChild(canvas);
            // menu co/giãn khi thu gọn -> phải đo lại khung vẽ
            if (window.ResizeObserver) {
                new ResizeObserver(chinhKichThuoc).observe(khung);
            }
        } else {
            canvas.style.zIndex = "1";
            document.body.appendChild(canvas);
        }

        ctx = canvas.getContext("2d");
        window.addEventListener("resize", chinhKichThuoc);
        chinhKichThuoc();
        return canvas;
    }

    function chinhKichThuoc() {
        if (!canvas) return;
        const khung = khungChua();
        canvas.width = khung ? khung.clientWidth : window.innerWidth;
        canvas.height = khung ? khung.clientHeight : window.innerHeight;
    }

    function sinhHat(cauHinh) {
        hat = [];
        const rong = canvas ? canvas.width : window.innerWidth;
        const cao = canvas ? canvas.height : window.innerHeight;
        // ít hạt thôi cho nhẹ máy; khung càng hẹp thì càng ít
        const soLuong = rong < 200 ? 10 : (rong < 768 ? 18 : 34);
        for (let i = 0; i < soLuong; i++) {
            hat.push({
                x: Math.random() * rong,
                y: Math.random() * cao,
                co: 10 + Math.random() * 12,
                v: cauHinh.toc_do * (0.6 + Math.random()),
                lech: Math.random() * Math.PI * 2,
                kyTu: cauHinh.ky_tu[Math.floor(Math.random() * cauHinh.ky_tu.length)],
                mo: 0.35 + Math.random() * 0.45
            });
        }
    }

    function ve() {
        if (!ctx) return;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        hat.forEach(h => {
            h.y += h.v;
            h.lech += 0.01;
            h.x += Math.sin(h.lech) * 0.4;
            if (h.y > canvas.height + 20) {
                h.y = -20;
                h.x = Math.random() * canvas.width;
            }
            ctx.globalAlpha = h.mo;
            ctx.font = h.co + "px serif";
            // Không đặt màu thì canvas vẽ bằng màu đen mặc định -> nền tối coi
            // như không thấy gì. Ký tự emoji tự có màu riêng, nhưng ký tự
            // thường (✦ • ❄) thì lấy màu của chủ đề.
            ctx.fillStyle = mauHat;
            ctx.fillText(h.kyTu, h.x, h.y);
        });
        ctx.globalAlpha = 1;
        khungHinh = requestAnimationFrame(ve);
    }

    function batHat(cauHinh) {
        // Trang có layout mà chưa dựng xong menu thì thôi, lát nữa gọi lại
        if (!document.getElementById("nenTrangChu") && !document.getElementById("sidebar")) return;
        mauHat = cauHinh.mau || "#ffffff";
        taoCanvas();
        sinhHat(cauHinh);
        if (!khungHinh) ve();
    }

    function tatHat() {
        if (khungHinh) {
            cancelAnimationFrame(khungHinh);
            khungHinh = null;
        }
        if (ctx && canvas) ctx.clearRect(0, 0, canvas.width, canvas.height);
    }

    // ---------- áp dụng ----------
    // Nhãn nút nói thẳng hành động sẽ xảy ra khi bấm, không phải trạng thái,
    // để người không thích hiệu ứng nhìn là biết chỗ tắt ngay.
    function capNhatNut(mua) {
        // Cặp nút Tắt/Bật trong hộp "Cài đặt giao diện" của các layout
        document.querySelectorAll(".hieu-ung-btn").forEach(btn => {
            btn.classList.toggle("active", (btn.dataset.hieuUng === "bat") === bat);
        });

        const nut = document.getElementById("btnHieuUngMua");
        if (!nut) return;

        const ten = document.getElementById("tenMuaHienTai");
        if (ten) ten.textContent = bat ? "Tắt hiệu ứng" : "Bật hiệu ứng nền";

        const icon = nut.querySelector(".nhan-hieu-ung");
        if (icon) icon.textContent = bat ? "🚫" : mua.bieuTuong;

        nut.title = bat
            ? `Đang bật: ${mua.ten} ${mua.bieuTuong} — bấm để tắt`
            : "Bấm để bật hiệu ứng nền theo mùa và ngày lễ";
    }

    // Dùng lại đúng chủ đề đó cho các layout khác: chỉ cần cho biết tên chủ đề
    // đang chạy để nơi khác hiển thị, còn màu thì đã nằm ở biến --nen-mua.
    function tenChuDe() {
        return chuDeHienTai().ten;
    }

    // Chủ đề + trạng thái ngày/đêm đang được vẽ. Dùng để nhận ra "không có gì
    // đổi" mà bỏ qua, tránh mỗi phút lại gieo lại đàn hạt làm hiệu ứng giật cục.
    let dauChuDeDangChay = null;

    function apDung() {
        const mua = chuDeHienTai();

        if (!bat) {
            document.documentElement.removeAttribute("data-hieu-ung-mua");
            document.documentElement.style.removeProperty("--nen-mua");
            tatHat();
            dauChuDeDangChay = null;
            capNhatNut(mua);
            return;
        }

        // Cùng chủ đề, cùng ngày/đêm và hạt vẫn đang chạy -> để yên cho liền mạch
        const dau = mua.ten + (banNgay() ? "|ngay" : "|dem");
        if (dau === dauChuDeDangChay && khungHinh) {
            capNhatNut(mua);
            return;
        }
        dauChuDeDangChay = dau;

        document.documentElement.style.setProperty("--nen-mua", banNgay() ? mua.ngay : mua.dem);
        document.documentElement.setAttribute("data-hieu-ung-mua", mua.ten);
        batHat(mua.hat);
        capNhatNut(mua);
    }

    function doiTrangThai() {
        bat = !bat;
        try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (e) { }
        apDung();
    }

    document.addEventListener("click", e => {
        if (e.target.closest("#btnHieuUngMua")) {
            e.preventDefault();
            doiTrangThai();
            return;
        }
        const nutCaiDat = e.target.closest(".hieu-ung-btn");
        if (nutCaiDat) {
            e.preventDefault();
            bat = nutCaiDat.dataset.hieuUng === "bat";
            try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (err) { }
            apDung();
        }
    });

    document.addEventListener("DOMContentLoaded", () => {
        apDung();
        // Đổi mùa / chuyển ngày-đêm ngay trong lúc đang mở trang cũng phải bắt được
        setInterval(apDung, 60000);
    });

    function luuTrangThai() {
        try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (e) { }
    }

    window.HieuUngMua = {
        bat: () => { bat = true; luuTrangThai(); apDung(); },
        tat: () => { bat = false; luuTrangThai(); apDung(); },
        refreshButtons: () => capNhatNut(chuDeHienTai()),
        tenChuDe,
        dangBat: () => bat
    };
})();
