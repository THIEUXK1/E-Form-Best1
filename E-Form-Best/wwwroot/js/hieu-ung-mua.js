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
            // Nền xanh lá non tươi, góc phải trên ửng hồng như cành đào nở; cánh hoa
            // vẽ tay bay theo gió nhẹ, điểm vài chiếc lộc non xanh nhạt.
            // Vẫn giữ độ tối vừa đủ để chữ trắng trên trang đọc rõ.
            ten: "Xuân",
            bieuTuong: "🌸",
            ngay: "radial-gradient(circle at 80% 12%, rgba(249, 168, 212, 0.55), transparent 42%), radial-gradient(circle at 25% 25%, #5a9a6e, #3a7a57 45%, #17402e)",
            dem: "radial-gradient(circle at 80% 12%, rgba(244, 114, 182, 0.3), transparent 42%), radial-gradient(circle at 25% 25%, #33694d, #22503b 45%, #0b2318)",
            hat: {
                kieu: "canh", ky_tu: ["#fbcfe8", "#f9a8d4", "#fce7f3", "#f472b6", "#fff1f2", "#fbcfe8", "#bbf7d0"],
                mau: "#f9a8d4", toc_do: 0.45
            }
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

    // Xem trước (chỉ quyền All, ô chọn do view render): ép một chủ đề / ngày-đêm bất kỳ
    // để duyệt giao diện mà không phải đợi tới ngày. Chỉ giữ trong bộ nhớ trang —
    // không lưu localStorage, không gửi máy chủ — tải lại trang là về như thật.
    // null = theo ngày giờ thực tế.
    let xemTruoc = null; // { chuDe: object|null, ngayDem: "auto"|"ngay"|"dem" }

    function banNgay() {
        if (xemTruoc && xemTruoc.ngayDem !== "auto") return xemTruoc.ngayDem === "ngay";
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
            // Mốc là Mùng 1; đợt Tết dài hơn lễ khác: 20 ngày trước tới 20 ngày sau.
            // Trước Tết tăng dần (20–16: mức 1, 15–11: mức 2, 10–0: mức 3),
            // từ Mùng 1 trở đi giữ nguyên mức 3 suốt 20 ngày, không giảm dần.
            hop: (d, al) => al.thang === 1 && al.ngay === 1,
            truoc: 20, sau: 20,
            muc: lech => lech > 15 ? 1 : (lech > 10 ? 2 : 3),
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
            // Đêm giao thừa: nền tím than nửa đêm, giấy kim tuyến nhiều màu bay, ánh vàng kim
            ten: "Tết Dương lịch", bieuTuong: "🎆",
            hop: d => d.getMonth() === 0 && d.getDate() === 1,
            ngay: "radial-gradient(circle at 30% 20%, #3b2a6e, #251a4d 45%, #0d0820)",
            dem: "radial-gradient(circle at 30% 20%, #2a1d55, #1a1238 45%, #070414)",
            hat: {
                kieu: "giay", ky_tu: ["#facc15", "#f472b6", "#60a5fa", "#34d399", "#f8fafc", "#fb923c"],
                mau: "#facc15", toc_do: 0.7
            }
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

    // Thêm cho đợt CAO ĐIỂM của từng lễ: lời chúc trên băng chữ và bảng màu pháo hoa.
    // Tách riêng theo tên để bảng LE ở trên vẫn gọn, dễ dò ngày.
    const CAO_DIEM_LE = {
        "Tết Nguyên đán": { loiChuc: "Chúc mừng năm mới — An khang thịnh vượng", phao: ["#fcd34d", "#f87171", "#fb923c", "#fde68a"] },
        "Tết Trung thu": { loiChuc: "Trung thu đoàn viên — Vui vẻ, ấm áp", phao: ["#fbbf24", "#fde68a", "#f97316", "#fff7ed"] },
        "Giỗ Tổ Hùng Vương": { loiChuc: "Uống nước nhớ nguồn — Giỗ Tổ Hùng Vương 10/3", phao: ["#fde047", "#ef4444", "#ffffff"] },
        // Năm mới lấy theo ngày đang xem: cuối tháng 12 thì chúc năm sau
        "Tết Dương lịch": {
            loiChuc: () => {
                const d = new Date();
                return "Chúc mừng năm mới " + (d.getFullYear() + (d.getMonth() === 11 ? 1 : 0)) + "!";
            },
            phao: ["#facc15", "#fde68a", "#facc15", "#f472b6", "#60a5fa", "#ffffff"]
        },
        "Quốc tế Phụ nữ": { loiChuc: "Chúc mừng ngày Quốc tế Phụ nữ 8/3", phao: ["#f9a8d4", "#f472b6", "#fda4af", "#ffffff"] },
        "Giải phóng miền Nam & Quốc tế Lao động": { loiChuc: "Mừng ngày Thống nhất 30/4 & Quốc tế Lao động 1/5", phao: ["#fde047", "#ef4444", "#ffffff"] },
        "Quốc tế Thiếu nhi": { loiChuc: "Chúc mừng ngày Quốc tế Thiếu nhi 1/6", phao: ["#60a5fa", "#f472b6", "#facc15", "#34d399"] },
        "Quốc khánh": { loiChuc: "Mừng Quốc khánh 2/9", phao: ["#fde047", "#ef4444", "#ffffff"] },
        "Phụ nữ Việt Nam": { loiChuc: "Chúc mừng ngày Phụ nữ Việt Nam 20/10", phao: ["#fda4af", "#f472b6", "#fecdd3", "#ffffff"] },
        "Nhà giáo Việt Nam": { loiChuc: "Tri ân ngày Nhà giáo Việt Nam 20/11", phao: ["#bfdbfe", "#93c5fd", "#fde68a", "#ffffff"] },
        "Quân đội nhân dân": { loiChuc: "Mừng ngày Quân đội nhân dân Việt Nam 22/12", phao: ["#fde047", "#86efac", "#ffffff"] },
        "Giáng sinh": { loiChuc: "Giáng sinh an lành — Merry Christmas", phao: ["#f87171", "#86efac", "#fde68a", "#e0f2fe"] }
    };
    LE.forEach(le => Object.assign(le, CAO_DIEM_LE[le.ten] || {}));

    // Hiệu ứng lễ kéo dài trước và sau ngày lễ 7 ngày.
    // Cách kiểm: xét từng ngày trong khoảng ±7 ngày quanh hôm nay, ngày nào
    // khớp luật của lễ thì lễ đó đang trong đợt. Làm vậy thì lễ âm lịch (Tết,
    // Trung thu) cũng tự đúng mà không cần tính riêng ngày dương của nó, và
    // trường hợp vắt qua năm mới (1/1) cũng không phải xử lý thêm.
    const SO_NGAY_KEO_DAI = 7;

    // Đợt lễ chia 3 mức theo khoảng cách tới ngày lễ: đẹp dần khi tới gần, giảm dần khi qua
    // (7 → 3 → 1 → 0 → 1 → 3 → 7 ngày).
    //   Mức 1 (lệch 4–7 ngày): nền + hạt rơi như thường.
    //   Mức 2 (lệch 2–3 ngày): hạt dày hơn, lấp lánh, vầng sáng nhẹ, pháo hoa thưa.
    //   Mức 3 (đúng ngày & lệch 1 ngày): hạt dày nhất, vầng sáng rõ, pháo hoa dày, băng lời chúc.
    const MUC_LE = {
        1: { heSoHat: 1, lapLanh: false, doSang: 0, phao: null, loiChuc: false },
        2: { heSoHat: 1.3, lapLanh: true, doSang: 0.6, phao: { nghiMin: 160, nghiThem: 200, banDoi: 0 }, loiChuc: false },
        3: { heSoHat: 1.6, lapLanh: true, doSang: 1, phao: { nghiMin: 50, nghiThem: 110, banDoi: 0.25 }, loiChuc: true }
    };

    function mucTheoLech(lech) {
        const k = Math.abs(lech);
        if (k <= 1) return 3;
        if (k <= 3) return 2;
        return 1;
    }

    // Số ngày lệch từ hôm nay tới lễ đang chạy (0 = đúng ngày); null = đang theo mùa.
    let lechLeThat = null;

    function chuDeHienTai() {
        if (xemTruoc && xemTruoc.chuDe) return xemTruoc.chuDe;
        const homNay = new Date();

        // Xét từ gần tới xa để lễ sát ngày nhất được ưu tiên khi hai lễ chồng nhau.
        // Quét theo đợt dài nhất (Tết 20 ngày); lễ nào ngắn hơn thì tự loại ở điều kiện dưới.
        // lech > 0: lễ còn ở phía trước (trước lễ); lech < 0: đã qua lễ.
        const xaNhat = Math.max(SO_NGAY_KEO_DAI, ...LE.map(x => Math.max(x.truoc || 0, x.sau || 0)));
        const dsLech = [0];
        for (let i = 1; i <= xaNhat; i++) dsLech.push(i, -i);

        for (const lech of dsLech) {
            const d = new Date(homNay.getFullYear(), homNay.getMonth(), homNay.getDate() + lech);
            let al;
            try {
                al = amLich(d);
            } catch (e) {
                al = { ngay: 0, thang: 0 };
            }
            const le = LE.find(x => {
                const gioiHan = lech >= 0 ? (x.truoc || SO_NGAY_KEO_DAI) : (x.sau || SO_NGAY_KEO_DAI);
                if (Math.abs(lech) > gioiHan) return false;
                try { return x.hop(d, al); } catch (e) { return false; }
            });
            if (le) {
                lechLeThat = lech;
                return le;
            }
        }

        lechLeThat = null;
        return muaHienTai();
    }

    // Mức hiệu ứng lễ 0–3 (0 = đang theo mùa). Gọi SAU chuDeHienTai() để lechLeThat đã đúng.
    function mucLeHienTai(chuDe) {
        if (!chuDe.loiChuc) return 0;
        if (xemTruoc && xemTruoc.muc !== "auto") return +xemTruoc.muc;
        // Xem trước một lễ: mặc định cho thấy bản đẹp nhất
        if (xemTruoc && xemTruoc.chuDe) return 3;
        if (lechLeThat === null) return 0;
        // Lễ có nhịp riêng (Tết) thì theo nhịp đó, còn lại 7–3–1 đối xứng
        return chuDe.muc ? chuDe.muc(lechLeThat) : mucTheoLech(lechLeThat);
    }

    // ---------- lớp hạt rơi ----------
    let canvas = null, ctx = null, hat = [], khungHinh = null, mauHat = "#ffffff";
    // "chu" = vẽ ký tự/emoji (mặc định); "giay" = mảnh giấy kim tuyến; "canh" = cánh hoa.
    // Với "giay"/"canh", ky_tu là danh sách màu.
    let kieuHat = "chu";

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

    // Vị trí đàn hạt được cất vào sessionStorage khi rời trang và lấy lại ở trang
    // sau, nhờ vậy chuyển mục trong menu thì sao/hoa/lá rơi tiếp chỗ cũ chứ không
    // "gieo lại" từ đầu — mắt nhìn thấy đúng như là chưa hề tải lại trang.
    // sessionStorage: chỉ sống trong một thẻ trình duyệt, đóng thẻ là sạch.
    const KEY_HAT = "eformHatMuaViTri";

    function luuViTriHat() {
        if (!canvas || !hat.length || !dauChuDeDangChay) return;
        try {
            sessionStorage.setItem(KEY_HAT, JSON.stringify({
                chuDe: dauChuDeDangChay,
                // lưu theo TỈ LỆ khung để menu rộng/hẹp khác nhau vẫn khớp
                hat: hat.map(h => [
                    +(h.x / canvas.width).toFixed(4),
                    +(h.y / canvas.height).toFixed(4),
                    +h.co.toFixed(2), +h.v.toFixed(3), +h.lech.toFixed(3),
                    h.kyTu, +h.mo.toFixed(2)
                ])
            }));
        } catch (e) { }
    }

    function docViTriHat(rong, cao) {
        try {
            const raw = sessionStorage.getItem(KEY_HAT);
            if (!raw) return null;
            const d = JSON.parse(raw);
            // Khác chủ đề (đổi mùa / sang ngày-đêm) thì phải gieo lại cho đúng ký tự
            if (!d || d.chuDe !== dauChuDeDangChay || !Array.isArray(d.hat) || !d.hat.length) return null;
            return d.hat.map(h => ({
                x: h[0] * rong, y: h[1] * cao,
                co: h[2], v: h[3], lech: h[4], kyTu: h[5], mo: h[6]
            }));
        } catch (e) {
            return null;
        }
    }

    function sinhHat(cauHinh) {
        const rong = canvas ? canvas.width : window.innerWidth;
        const cao = canvas ? canvas.height : window.innerHeight;
        // ít hạt thôi cho nhẹ máy; khung càng hẹp thì càng ít. Gần ngày lễ thì dày hơn.
        const soLuong = Math.round((rong < 200 ? 10 : (rong < 768 ? 18 : 34)) * (mucDangChay ? mucDangChay.heSoHat : 1));

        const cu = docViTriHat(rong, cao);
        hat = cu ? cu.slice(0, soLuong) : [];

        for (let i = hat.length; i < soLuong; i++) {
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
            // Cánh hoa: gió nhẹ thổi chéo sang phải; bay quá mép phải thì vòng lại mép trái
            if (kieuHat === "canh") {
                h.x += 0.35;
                if (h.x > canvas.width + 20) h.x = -20;
            }
            if (h.y > canvas.height + 20) {
                h.y = -20;
                h.x = Math.random() * canvas.width;
            }
            // Gần ngày lễ: hạt lấp lánh (độ mờ dao động nhẹ theo nhịp lắc riêng của từng hạt)
            ctx.globalAlpha = mucDangChay && mucDangChay.lapLanh ? h.mo * (0.7 + 0.3 * Math.sin(h.lech * 4)) : h.mo;

            // Giấy kim tuyến: kyTu là màu; mảnh giấy xoay và lật (co giãn chiều cao)
            // cho cảm giác đang bay lượn thay vì rơi thẳng
            if (kieuHat === "giay") {
                ctx.globalAlpha = Math.min(1, ctx.globalAlpha + 0.3);
                ctx.save();
                ctx.translate(h.x, h.y);
                ctx.rotate(h.lech * 3);
                ctx.scale(1, Math.cos(h.lech * 7));
                ctx.fillStyle = h.kyTu;
                ctx.fillRect(-h.co * 0.22, -h.co * 0.4, h.co * 0.45, h.co * 0.8);
                ctx.restore();
                return;
            }

            // Cánh hoa đào: hình giọt có khía ở đầu, chao nghiêng (co giãn chiều ngang)
            if (kieuHat === "canh") {
                const s = h.co * 0.45;
                ctx.globalAlpha = Math.min(1, ctx.globalAlpha + 0.25);
                ctx.save();
                ctx.translate(h.x, h.y);
                ctx.rotate(Math.sin(h.lech * 1.5) * 1.2 + h.lech);
                ctx.scale(0.55 + 0.45 * Math.cos(h.lech * 5), 1);
                ctx.fillStyle = h.kyTu;
                ctx.beginPath();
                ctx.moveTo(0, s);
                ctx.bezierCurveTo(s * 0.9, s * 0.3, s * 0.6, -s * 0.8, 0, -s * 0.55);
                ctx.bezierCurveTo(-s * 0.6, -s * 0.8, -s * 0.9, s * 0.3, 0, s);
                ctx.fill();
                ctx.restore();
                return;
            }

            ctx.font = h.co + "px serif";
            // Không đặt màu thì canvas vẽ bằng màu đen mặc định -> nền tối coi
            // như không thấy gì. Ký tự emoji tự có màu riêng, nhưng ký tự
            // thường (✦ • ❄) thì lấy màu của chủ đề.
            ctx.fillStyle = mauHat;
            ctx.fillText(h.kyTu, h.x, h.y);
        });
        ctx.globalAlpha = 1;
        if (mucDangChay && mucDangChay.doSang) veVangSang();
        if (mauPhao) vePhaoHoa();
        khungHinh = requestAnimationFrame(ve);
    }

    // ---------- gần ngày lễ: vầng sáng + pháo hoa ----------
    // mucDangChay: cấu hình MUC_LE đang vẽ; null = mùa thường
    let mucDangChay = null, mauPhao = null, nhipSang = 0;
    let phao = [], tia = [], demPhao = 0;
    const TRONG_LUC = 0.06;

    // Người dùng bật "giảm chuyển động" ở hệ điều hành thì không bắn pháo hoa
    const giamChuyenDong = !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);

    // Vầng sáng màu chủ đề tỏa từ mép trên, "thở" chậm
    function veVangSang() {
        nhipSang += 0.012;
        const w = canvas.width, h = canvas.height;
        const g = ctx.createRadialGradient(w / 2, -h * 0.15, 0, w / 2, -h * 0.15, h);
        g.addColorStop(0, mauHat);
        g.addColorStop(1, mauHat + "00");
        ctx.globalAlpha = (0.1 + 0.07 * Math.sin(nhipSang)) * mucDangChay.doSang;
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, w, h);
        ctx.globalAlpha = 1;
    }

    function chonMau() {
        return mauPhao[Math.floor(Math.random() * mauPhao.length)];
    }

    function banPhao() {
        const w = canvas.width, h = canvas.height;
        const y0 = h + 10;
        const dinh = h * (0.12 + Math.random() * 0.3);
        phao.push({
            x: w * (0.12 + Math.random() * 0.76), y: y0,
            vx: (Math.random() - 0.5) * 1.2,
            // vận tốc vừa đủ để tới đỉnh đã chọn: v² = 2·g·quãng đường
            vy: -Math.sqrt(2 * TRONG_LUC * (y0 - dinh)),
            mau: chonMau()
        });
    }

    function noPhao(p) {
        const n = Math.round((60 + Math.random() * 40) * (canvas.width < 768 ? 0.6 : 1));
        const lucNo = 2 + Math.random() * 2.5;
        for (let i = 0; i < n; i++) {
            const goc = (Math.PI * 2 * i) / n + Math.random() * 0.1;
            const tocDo = lucNo * (0.4 + Math.random() * 0.6);
            tia.push({
                x: p.x, y: p.y, px: p.x, py: p.y,
                vx: Math.cos(goc) * tocDo, vy: Math.sin(goc) * tocDo,
                // phần lớn cùng màu quả pháo, điểm xuyết vài màu khác cho rực
                mau: Math.random() < 0.75 ? p.mau : chonMau(),
                song: 1, giam: 0.011 + Math.random() * 0.01
            });
        }
    }

    function vePhaoHoa() {
        const nhip = mucDangChay.phao;
        if (--demPhao <= 0) {
            banPhao();
            if (Math.random() < nhip.banDoi) banPhao(); // thỉnh thoảng bắn đôi
            demPhao = nhip.nghiMin + Math.random() * nhip.nghiThem;
        }

        ctx.globalCompositeOperation = "lighter"; // tia chồng lên nhau thì sáng rực hơn
        ctx.lineCap = "round";

        for (let i = phao.length - 1; i >= 0; i--) {
            const p = phao[i];
            p.x += p.vx;
            p.y += p.vy;
            p.vy += TRONG_LUC;
            ctx.globalAlpha = 0.9;
            ctx.strokeStyle = p.mau;
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(p.x - p.vx * 3, p.y - p.vy * 3);
            ctx.stroke();
            if (p.vy >= -0.6) { // gần tới đỉnh thì nổ
                noPhao(p);
                phao.splice(i, 1);
            }
        }

        ctx.lineWidth = 2;
        for (let i = tia.length - 1; i >= 0; i--) {
            const t = tia[i];
            t.px = t.x; t.py = t.y;
            t.vx *= 0.975;
            t.vy = t.vy * 0.975 + 0.035;
            t.x += t.vx;
            t.y += t.vy;
            t.song -= t.giam;
            if (t.song <= 0) {
                tia.splice(i, 1);
                continue;
            }
            ctx.globalAlpha = t.song;
            ctx.strokeStyle = t.mau;
            ctx.beginPath();
            ctx.moveTo(t.px, t.py);
            ctx.lineTo(t.x, t.y);
            ctx.stroke();
        }

        ctx.globalCompositeOperation = "source-over";
        ctx.globalAlpha = 1;
    }

    // Băng chữ lời chúc giữa mép trên trang chủ (không hiện trong menu các layout)
    function hienLoiChuc(le) {
        let el = document.getElementById("loiChucLe");
        if (!le || !document.getElementById("nenTrangChu") || !document.body) {
            if (el) el.classList.remove("hien");
            return;
        }
        if (!el) {
            el = document.createElement("div");
            el.id = "loiChucLe";
            el.setAttribute("aria-live", "polite");
            document.body.appendChild(el);
        }
        // Emoji tách khỏi phần chữ: chữ tô gradient bằng background-clip sẽ làm emoji mất màu
        const bieuTuong = () => {
            const s = document.createElement("span");
            s.className = "bieu-tuong";
            s.textContent = le.bieuTuong;
            return s;
        };
        const chu = document.createElement("span");
        chu.className = "chu";
        chu.textContent = typeof le.loiChuc === "function" ? le.loiChuc() : le.loiChuc;
        el.replaceChildren(bieuTuong(), chu, bieuTuong());
        el.style.setProperty("--mau-loi-chuc", le.hat.mau);
        requestAnimationFrame(() => el.classList.add("hien"));
    }

    // le: chủ đề lễ đang chạy (null = mùa); muc: 0–3 theo MUC_LE
    function batHat(cauHinh, le, muc) {
        // Trang có layout mà chưa dựng xong menu thì thôi, lát nữa gọi lại
        if (!document.getElementById("nenTrangChu") && !document.getElementById("sidebar")) return;
        mauHat = cauHinh.mau || "#ffffff";
        kieuHat = cauHinh.kieu || "chu";
        mucDangChay = MUC_LE[muc] || null;
        taoCanvas();
        // Pháo hoa chỉ ở trang chủ (toàn màn); menu bên trái quá hẹp để bắn
        mauPhao = mucDangChay && mucDangChay.phao && le && !khungChua() && !giamChuyenDong ? le.phao : null;
        phao = [];
        tia = [];
        demPhao = 30;
        sinhHat(cauHinh);
        if (!khungHinh) ve();
    }

    function tatHat() {
        if (khungHinh) {
            cancelAnimationFrame(khungHinh);
            khungHinh = null;
        }
        mucDangChay = null;
        mauPhao = null;
        phao = [];
        tia = [];
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

        // Đang xem trước thì hiện hiệu ứng dù người dùng đang để tắt
        if (!bat && !xemTruoc) {
            document.documentElement.removeAttribute("data-hieu-ung-mua");
            document.documentElement.removeAttribute("data-cao-diem");
            document.documentElement.style.removeProperty("--nen-mua");
            tatHat();
            hienLoiChuc(null);
            dauChuDeDangChay = null;
            capNhatNut(mua);
            return;
        }

        // Cùng chủ đề, cùng ngày/đêm và hạt vẫn đang chạy -> để yên cho liền mạch
        const muc = mucLeHienTai(mua);
        const caoDiem = !!(MUC_LE[muc] && MUC_LE[muc].loiChuc);
        const dau = mua.ten + (banNgay() ? "|ngay" : "|dem") + "|muc" + muc;
        if (dau === dauChuDeDangChay && khungHinh) {
            capNhatNut(mua);
            return;
        }
        dauChuDeDangChay = dau;

        document.documentElement.style.setProperty("--nen-mua", banNgay() ? mua.ngay : mua.dem);
        document.documentElement.setAttribute("data-hieu-ung-mua", mua.ten);
        document.documentElement.toggleAttribute("data-cao-diem", caoDiem);
        batHat(mua.hat, muc ? mua : null, muc);
        hienLoiChuc(caoDiem ? mua : null);
        capNhatNut(mua);
    }

    // ============================================================
    // ĐỒNG BỘ VỚI TÀI KHOẢN
    // localStorage vẫn giữ để trang hiện ngay, không chờ mạng (tránh nháy màn hình).
    // Máy chủ giữ bản chính thức: đăng nhập máy khác vẫn đúng lựa chọn, và quản trị
    // biết được ai đang bật hiệu ứng. Mọi lỗi mạng đều nuốt — hiệu ứng chỉ là trang trí,
    // không được phép làm hỏng trang.
    // ============================================================
    const API = "/CaiDat/HieuUngNen";

    function layToken() {
        const o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : null;
    }

    function guiLenMayChu(giaTri) {
        const token = layToken();
        if (!token) return; // Trang chưa đăng nhập / chưa có token thì chỉ lưu cục bộ
        fetch(API, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify({ bat: giaTri })
        }).catch(() => { });
    }

    // Nhịp tim: cứ 30 phút báo "trang này còn mở và còn bật" để quản trị phân biệt được
    // người đang thực sự dùng với người bấm bật một lần rồi thôi. Chỉ gửi khi đang bật.
    const NHIP_PING = 30 * 60 * 1000;

    function guiPing() {
        if (!bat) return;
        const token = layToken();
        if (!token) return;
        fetch(API + "/Ping", {
            method: "POST",
            credentials: "same-origin",
            headers: { "RequestVerificationToken": token }
        }).catch(() => { });
    }

    async function dongBoTuMayChu() {
        try {
            const res = await fetch(API, { credentials: "same-origin", headers: { "Accept": "application/json" } });
            if (!res.ok) return;
            const data = await res.json();
            if (!data || data.thanhCong !== true) return;

            if (data.bat === null || typeof data.bat === "undefined") {
                // Máy chủ chưa có bản ghi: đẩy lựa chọn đang có sẵn ở máy này lên,
                // chỉ khi đang bật — để bảng không phình vì những người chưa từng dùng.
                if (bat) guiLenMayChu(true);
                return;
            }

            if (data.bat !== bat) {
                bat = data.bat === true;
                try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (e) { }
                apDung();
            }
        } catch (e) { }
    }

    function doiTrangThai() {
        bat = !bat;
        try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (e) { }
        guiLenMayChu(bat);
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
            guiLenMayChu(bat);
            apDung();
        }
    });

    // Áp nền NGAY lúc script chạy (script nằm trong <head>, chạy trước khi thân
    // trang được vẽ). Nếu đợi DOMContentLoaded thì menu đã vẽ xong bằng nền gốc
    // rồi mới đổi sang nền mùa, cộng thêm `transition` sẵn có của #sidebar nên
    // mỗi lần chuyển trang trong menu là thấy nền nháy một cái.
    // Chỉ đặt biến màu + thuộc tính, chưa đụng DOM (lúc này chưa có <body>).
    (function apDungNenSom() {
        if (!bat) return;
        const mua = chuDeHienTai();
        document.documentElement.style.setProperty("--nen-mua", banNgay() ? mua.ngay : mua.dem);
        document.documentElement.setAttribute("data-hieu-ung-mua", mua.ten);

        // Dựng lớp hạt ngay khi khung menu vừa được phân tích xong, không đợi
        // DOMContentLoaded (phải chờ cả ảnh/script cuối trang) — đợi thì có một
        // quãng menu trống rồi sao mới hiện ra, nhìn như hiệu ứng bị tải lại.
        (function choKhung() {
            if (document.getElementById("nenTrangChu") || document.getElementById("sidebar")) {
                apDung();
                return;
            }
            if (document.readyState === "loading") setTimeout(choKhung, 10);
        })();
    })();

    // ---------- xem trước chủ đề (quyền All) ----------
    // Ô chọn chủ đề: giá trị "" = theo hiện tại, "m:<khóa mùa>", "l:<chỉ số lễ>".
    function dungODsXemTruoc() {
        const o = document.getElementById("chonChuDeXemTruoc");
        if (!o || o.options.length > 1) return;

        const nhom = (nhan, ds) => {
            const g = document.createElement("optgroup");
            g.label = nhan;
            ds.forEach(([giaTri, chuDe]) => {
                const op = document.createElement("option");
                op.value = giaTri;
                op.textContent = chuDe.bieuTuong + " " + chuDe.ten;
                g.appendChild(op);
            });
            o.appendChild(g);
        };
        nhom("Mùa", Object.keys(MUA).map(k => ["m:" + k, MUA[k]]));
        nhom("Ngày lễ", LE.map((le, i) => ["l:" + i, le]));
    }

    function apDungXemTruoc() {
        const oChuDe = document.getElementById("chonChuDeXemTruoc");
        const oNgayDem = document.getElementById("chonNgayDemXemTruoc");
        const giaTri = oChuDe ? oChuDe.value : "";
        const ngayDem = oNgayDem ? oNgayDem.value : "auto";
        const oMuc = document.getElementById("chonMucXemTruoc");
        const muc = oMuc ? oMuc.value : "auto";

        let chuDe = null;
        if (giaTri.startsWith("m:")) chuDe = MUA[giaTri.slice(2)] || null;
        else if (giaTri.startsWith("l:")) chuDe = LE[+giaTri.slice(2)] || null;

        xemTruoc = (chuDe || ngayDem !== "auto" || muc !== "auto") ? { chuDe, ngayDem, muc } : null;

        const nutVe = document.getElementById("btnVeHieuUngHienTai");
        if (nutVe) nutVe.hidden = !xemTruoc;

        // Ép vẽ lại từ đầu cho chủ đề mới
        dauChuDeDangChay = null;
        apDung();
    }

    function veHienTai() {
        const oChuDe = document.getElementById("chonChuDeXemTruoc");
        const oNgayDem = document.getElementById("chonNgayDemXemTruoc");
        if (oChuDe) oChuDe.value = "";
        if (oNgayDem) oNgayDem.value = "auto";
        const oMuc = document.getElementById("chonMucXemTruoc");
        if (oMuc) oMuc.value = "auto";
        apDungXemTruoc();
    }

    document.addEventListener("change", e => {
        if (e.target.matches("#chonChuDeXemTruoc, #chonNgayDemXemTruoc, #chonMucXemTruoc")) apDungXemTruoc();
    });
    document.addEventListener("click", e => {
        if (e.target.closest("#btnVeHieuUngHienTai")) {
            e.preventDefault();
            veHienTai();
        }
    });

    // Rời trang thì cất vị trí đàn hạt lại cho trang sau dùng tiếp.
    // pagehide bắt được cả điều hướng thường lẫn đóng thẻ; visibilitychange là
    // lưới đỡ cho di động (nơi pagehide có thể không chạy).
    window.addEventListener("pagehide", luuViTriHat);
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "hidden") luuViTriHat();
    });

    document.addEventListener("DOMContentLoaded", () => {
        dungODsXemTruoc();
        apDung();
        dongBoTuMayChu();
        // Đổi mùa / chuyển ngày-đêm ngay trong lúc đang mở trang cũng phải bắt được
        setInterval(apDung, 60000);
        setInterval(guiPing, NHIP_PING);
    });

    function luuTrangThai() {
        try { localStorage.setItem(KEY, bat ? "1" : "0"); } catch (e) { }
    }

    window.HieuUngMua = {
        bat: () => { bat = true; luuTrangThai(); guiLenMayChu(true); apDung(); },
        tat: () => { bat = false; luuTrangThai(); guiLenMayChu(false); apDung(); },
        refreshButtons: () => capNhatNut(chuDeHienTai()),
        tenChuDe,
        dangBat: () => bat
    };
})();
