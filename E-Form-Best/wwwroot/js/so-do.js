// Sơ đồ mạng (topology) tự sinh từ DB — chỉ render, dữ liệu lấy ở /SoDo/{congTy}/DuLieu.
// Dữ liệu gồm: Công ty → Core → Phân phối → Truy cập; cụm AP (theo controller) và cụm Camera (theo đầu ghi).
// Người dùng tự chọn bố cục (phân tầng cổ điển / phân tầng gọn / hình tròn / lực đẩy / thủ công),
// kiểu đường nối, cách hiện cụm và bộ lọc; lựa chọn + vị trí kéo tay lưu localStorage theo công ty.
// Không dùng thư viện ngoài: dựng SVG bằng DOM thuần.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var MAU = { UP: "#22c55e", DOWN: "#ef4444", CHUA: "#9ca3af", KHONG: "#cbd5e1", MOT: "#f59e0b" };

    var trang = document.getElementById("sodoTrang");
    if (!trang) return;
    var url = trang.getAttribute("data-url");
    var congTy = trang.getAttribute("data-cong-ty") || "";
    var svg = document.getElementById("sodoSvg");
    var wrap = document.getElementById("sodoWrap");
    var dangTai = document.getElementById("sodoDangTai");
    var oLoi = document.getElementById("sodoLoi");
    var oTim = document.getElementById("sodoTim");

    function el(name, attrs, text) {
        var e = document.createElementNS(SVGNS, name);
        if (attrs) for (var k in attrs) if (attrs.hasOwnProperty(k)) e.setAttribute(k, attrs[k]);
        if (text != null) e.textContent = text;
        return e;
    }
    function mau(tt) { return MAU[tt] || MAU.CHUA; }
    function clear(n) { while (n.firstChild) n.removeChild(n.firstChild); }
    function cat(s, n) { s = s == null ? "" : String(s); return s.length > n ? s.slice(0, n - 1) + "…" : s; }
    function setText(id, v) { var e = document.getElementById(id); if (e) e.textContent = v; }
    function nhanTt(tt) {
        return tt === "UP" ? "Online" : tt === "DOWN" ? "Mất kết nối" : tt === "KHONG" ? "Không theo dõi" : tt === "MOT" ? "Có thiết bị mất KN" : "Chưa kiểm";
    }
    function hienLoi(msg) {
        if (dangTai) dangTai.classList.add("d-none");
        if (oLoi) { oLoi.textContent = msg; oLoi.classList.remove("d-none"); }
    }

    // ---- Kích thước cơ bản ----
    var NODE_W = 170, NODE_H = 46, GAP_X = 26, GAP_Y = 74, GAP_ROW = 14;
    var CHIP_H = 22, BOX_GAP_X = 24, BOX_GAP_Y = 18;
    var COL_W = 180, CHIP_MAX_DONG = 12, BOX_MAX_COT = 4; // hộp cụm: quá 12 dòng thì chia cột, tối đa 4 cột
    var GON_W = 196, GON_H = 54;                           // cụm hiện dạng nút gọn
    var FP = 14, FRAME_HEAD = 24; // lề trong + đầu khung khi một tầng phải xuống dòng
    var PAD = 24, RONG_MIN = 900;
    var Z_MIN = 0.05, Z_MAX = 3;

    // ---- Lựa chọn người dùng (lưu theo công ty; hỏng/không có localStorage vẫn chạy mặc định) ----
    var KHOA = "sodo:" + congTy;
    var cfg = { bocuc: "tang", canh: "cong", cum: "hop", tt: "", loai: { core: 1, pp: 1, tc: 1, ap: 1, cam: 1 }, pos: {} };
    try {
        var luu = JSON.parse(localStorage.getItem(KHOA) || "null");
        if (luu && typeof luu === "object") {
            ["bocuc", "canh", "cum", "tt"].forEach(function (k) { if (typeof luu[k] === "string") cfg[k] = luu[k]; });
            if (luu.loai) for (var lk in cfg.loai) if (luu.loai.hasOwnProperty(lk)) cfg.loai[lk] = luu.loai[lk] ? 1 : 0;
            if (luu.pos && typeof luu.pos === "object") cfg.pos = luu.pos;
        }
    } catch (e) { /* trình duyệt chặn localStorage: dùng mặc định */ }
    function luuCfg() { try { localStorage.setItem(KHOA, JSON.stringify(cfg)); } catch (e) { } }

    var duLieu = null, doThi = null, nodeMap = {}, keCanh = {};
    var zoom = 1, tx = 0, ty = 0, gCam = null;
    var bien = { x0: 0, y0: 0, x1: 0, y1: 0 }; // khung bao sơ đồ (toạ độ vẽ)

    // ================= DỰNG ĐỒ THỊ TỪ DỮ LIỆU + BỘ LỌC =================
    function khopTt(tt) {
        if (!cfg.tt) return true;
        if (cfg.tt === "CHUA") return tt === "CHUA" || tt === "KHONG";
        return tt === cfg.tt;
    }
    function ttCum(items) {
        var up = 0, down = 0;
        items.forEach(function (it) { if (it.tt === "UP") up++; else if (it.tt === "DOWN") down++; });
        if (down && down === items.length) return "DOWN";
        if (down) return "MOT";
        return up ? "UP" : "CHUA";
    }
    // Kích thước hộp cụm: danh sách dài chia thành nhiều cột để hộp không kéo dài xuống quá sâu
    function coHop(n) {
        var cot = Math.min(BOX_MAX_COT, Math.max(1, Math.ceil(n / CHIP_MAX_DONG)));
        var dong = Math.max(1, Math.ceil(n / cot));
        return { cot: cot, dong: dong, w: cot * COL_W + 20, h: 26 + 6 + dong * CHIP_H + 8 };
    }

    function dungDoThi(d) {
        var cty = { id: "cty", loai: "cty", ten: d.congTy, phu: "Công ty", tt: "UP", w: NODE_W, h: NODE_H };
        function sw(list, loai) {
            if (!cfg.loai[loai]) return [];
            return (list || []).filter(function (n) { return khopTt(n.tt); }).map(function (n) {
                return { id: n.id, loai: loai, ten: n.ten, phu: n.ip, ip: n.ip, tt: n.tt, w: NODE_W, h: NODE_H };
            });
        }
        var core = sw(d.core, "core"), pp = sw(d.phanPhoi, "pp"), tc = sw(d.truyCap, "tc");
        // Giữ như bản cũ: chưa khai báo core thì vẫn có một ô nhắc ở tầng core
        if (cfg.loai.core && !(d.core || []).length && !cfg.tt)
            core.push({ id: "core-trong", loai: "core", ten: "(chưa có switch core)", phu: null, tt: "CHUA", w: NODE_W, h: NODE_H });

        var cum = [];
        function nhom(arr, loai, tien, mauIcon) {
            if (!cfg.loai[loai]) return;
            (arr || []).forEach(function (g) {
                var items = (g.items || []).filter(function (it) { return khopTt(it.tt); });
                if (!items.length) return;
                var sz = cfg.cum === "gon" ? { w: GON_W, h: GON_H } : coHop(items.length);
                cum.push({ id: loai + "g:" + g.ten, loai: loai, ten: tien + " · " + g.ten, items: items, mau: mauIcon, tt: ttCum(items), w: sz.w, h: sz.h });
            });
        }
        nhom(d.apNhom, "ap", "AP", "#22d3ee");
        nhom(d.camNhom, "cam", "Cam", "#a78bfa");

        var canh = [];
        core.forEach(function (n) { canh.push({ a: "cty", b: n.id }); });
        // Hub nối các tầng dưới: core giữa hàng (hoặc công ty nếu không có core) — giống bản cũ
        var hub = core.length ? core[Math.floor(core.length / 2)] : cty;
        pp.concat(tc, cum).forEach(function (n) { canh.push({ a: hub.id, b: n.id }); });

        return { cty: cty, core: core, pp: pp, tc: tc, cum: cum, hub: hub, nodes: [cty].concat(core, pp, tc, cum), canh: canh };
    }

    // ================= CÁC BỐ CỤC — đặt n.x, n.y (góc trên trái) =================
    function rongHang(n) { return n <= 0 ? 0 : n * NODE_W + (n - 1) * GAP_X; }
    function datHang(list, y, tongRong) {
        var w = 0;
        list.forEach(function (n, i) { w += (i ? GAP_X : 0) + n.w; });
        var x = Math.max(PAD, (tongRong - w) / 2);
        list.forEach(function (n) { n.x = x; n.y = y; x += n.w + GAP_X; });
    }

    // Phân tầng cổ điển (bản cũ): mỗi tầng một hàng ngang, nối từng thiết bị về hub
    function bocucTang(g) {
        var rongCum = 0;
        g.cum.forEach(function (n, i) { rongCum += (i ? BOX_GAP_X : 0) + n.w; });
        var tongRong = Math.max(rongHang(g.core.length), rongHang(g.pp.length), rongHang(g.tc.length), rongCum, 640) + PAD * 2;
        var y = PAD;
        g.cty.x = tongRong / 2 - NODE_W / 2; g.cty.y = y;
        y += NODE_H + GAP_Y;
        [g.core, g.pp, g.tc].forEach(function (list) {
            if (!list.length) return;
            datHang(list, y, tongRong);
            y += NODE_H + GAP_Y;
        });
        if (g.cum.length) {
            var x = Math.max(PAD, (tongRong - rongCum) / 2);
            g.cum.forEach(function (n) { n.x = x; n.y = y; x += n.w + BOX_GAP_X; });
        }
        return {};
    }

    // Phân tầng gọn: tầng dài tự xuống dòng trong khung, nối 1 cạnh vào khung thay vì hàng trăm cạnh chéo
    function bocucGon(g) {
        var tongRong = Math.max(RONG_MIN, (wrap.clientWidth || 0) - 4);
        var moiDong = Math.max(1, Math.floor((tongRong - PAD * 2 - FP * 2 + GAP_X) / (NODE_W + GAP_X)));
        var kq = { khung: [], canhThem: [] };
        var y = PAD;
        g.cty.x = tongRong / 2 - NODE_W / 2; g.cty.y = y;
        y += NODE_H + GAP_Y;

        function tang(list, ten, nguon) {
            if (!list.length) return;
            if (list.length <= moiDong) { datHang(list, y, tongRong); y += NODE_H + GAP_Y; return; }
            var soDong = Math.ceil(list.length / moiDong);
            var fw = rongHang(moiDong) + FP * 2;
            var fh = FRAME_HEAD + soDong * (NODE_H + GAP_ROW) - GAP_ROW + FP;
            var fx = (tongRong - fw) / 2;
            kq.khung.push({ x: fx, y: y, w: fw, h: fh, ten: ten + " (" + list.length + ")" });
            list.forEach(function (n, i) {
                n.x = fx + FP + (i % moiDong) * (NODE_W + GAP_X);
                n.y = y + FRAME_HEAD + Math.floor(i / moiDong) * (NODE_H + GAP_ROW);
                n.trongKhung = true;
            });
            kq.canhThem.push({ nguon: nguon, x: tongRong / 2, y: y });
            y += fh + GAP_Y;
        }
        tang(g.core, "Core", g.cty);
        tang(g.pp, "Phân phối", g.hub);
        tang(g.tc, "Truy cập", g.hub);

        // Cụm AP + Camera xếp theo kệ, hết bề rộng thì xuống kệ mới
        if (g.cum.length) {
            var rongKe = tongRong - PAD * 2 - FP * 2;
            var ke = [], keHt = { items: [], w: 0, h: 0 };
            g.cum.forEach(function (n) {
                if (keHt.items.length && keHt.w + BOX_GAP_X + n.w > rongKe) { ke.push(keHt); keHt = { items: [], w: 0, h: 0 }; }
                keHt.w += (keHt.items.length ? BOX_GAP_X : 0) + n.w;
                keHt.h = Math.max(keHt.h, n.h);
                keHt.items.push(n);
            });
            ke.push(keHt);
            var nhieuKe = ke.length > 1, yKe = y + (nhieuKe ? FRAME_HEAD : 0), maxKe = 0;
            ke.forEach(function (k) {
                var bx = (tongRong - k.w) / 2;
                k.items.forEach(function (n) { n.x = bx; n.y = yKe; n.trongKhung = nhieuKe; bx += n.w + BOX_GAP_X; });
                yKe += k.h + BOX_GAP_Y;
                if (k.w > maxKe) maxKe = k.w;
            });
            if (nhieuKe) {
                var kw = maxKe + FP * 2;
                kq.khung.push({ x: (tongRong - kw) / 2, y: y, w: kw, h: yKe - BOX_GAP_Y - y + FP, ten: "Cụm AP · Camera (" + g.cum.length + ")" });
                kq.canhThem.push({ nguon: g.hub, x: tongRong / 2, y: y });
            }
        }
        return kq;
    }

    function banKinh(n) { return Math.sqrt(n.w * n.w + n.h * n.h) / 2; }

    // Hình tròn: hub ở tâm, các tầng là các vòng đồng tâm toả ra ngoài
    function bocucTron(g) {
        var tam = g.hub;
        tam.x = -tam.w / 2; tam.y = -tam.h / 2;
        var vong1 = g.core.filter(function (n) { return n !== tam; });
        if (tam !== g.cty) vong1.unshift(g.cty);
        var vongs = [vong1, g.pp, g.tc, g.cum].filter(function (a) { return a.length; });
        var r = 0, nuaTruoc = banKinh(tam);
        vongs.forEach(function (vong, k) {
            var nuaMax = 0, chuVi = 0;
            vong.forEach(function (n) { var b = banKinh(n); if (b > nuaMax) nuaMax = b; chuVi += b * 2 + 20; });
            r = Math.max(r + nuaTruoc + nuaMax + 50, chuVi / (2 * Math.PI));
            var lech = k % 2 ? Math.PI / vong.length : 0; // so le giữa các vòng để cạnh đỡ trùng nhau
            vong.forEach(function (n, i) {
                var a = -Math.PI / 2 + lech + i * 2 * Math.PI / vong.length;
                n.x = r * Math.cos(a) - n.w / 2;
                n.y = r * Math.sin(a) - n.h / 2;
            });
            nuaTruoc = nuaMax;
        });
        return {};
    }

    // Lực đẩy: khởi đầu từ hình tròn rồi mô phỏng lò xo (cạnh) + lực đẩy (mọi cặp), cuối cùng gỡ chồng lấn
    function bocucLuc(g) {
        bocucTron(g);
        var ns = g.nodes, N = ns.length, idx = {};
        var P = ns.map(function (n, i) { idx[n.id] = i; return { x: n.x + n.w / 2, y: n.y + n.h / 2, r: banKinh(n), dx: 0, dy: 0 }; });
        var E = g.canh.map(function (c) { return [idx[c.a], idx[c.b]]; }).filter(function (p) { return p[0] != null && p[1] != null; });
        var t = 80;
        for (var it = 0; it < 260; it++) {
            var i, j, p, q, vx, vy, dd, f;
            for (i = 0; i < N; i++) { P[i].dx = 0; P[i].dy = 0; }
            for (i = 0; i < N; i++) for (j = i + 1; j < N; j++) {
                p = P[i]; q = P[j]; vx = p.x - q.x; vy = p.y - q.y;
                dd = Math.sqrt(vx * vx + vy * vy) || 0.01;
                var toiThieu = p.r + q.r + 30;
                f = 4000 / dd + (dd < toiThieu ? (toiThieu - dd) * 0.6 : 0);
                vx /= dd; vy /= dd;
                p.dx += vx * f; p.dy += vy * f; q.dx -= vx * f; q.dy -= vy * f;
            }
            E.forEach(function (e) {
                var a = P[e[0]], b = P[e[1]], ex = b.x - a.x, ey = b.y - a.y;
                var d2 = Math.sqrt(ex * ex + ey * ey) || 0.01, L = a.r + b.r + 70;
                var fs = (d2 - L) * 0.08;
                ex /= d2; ey /= d2;
                a.dx += ex * fs; a.dy += ey * fs; b.dx -= ex * fs; b.dy -= ey * fs;
            });
            for (i = 0; i < N; i++) {
                p = P[i]; var m = Math.sqrt(p.dx * p.dx + p.dy * p.dy) || 1;
                var buoc = Math.min(m, t);
                p.x += p.dx / m * buoc; p.y += p.dy / m * buoc;
            }
            t = Math.max(1, t * 0.985);
        }
        ns.forEach(function (n, i) { n.x = P[i].x - n.w / 2; n.y = P[i].y - n.h / 2; });
        goChongLan(ns);
        return {};
    }

    // Đẩy các hình chữ nhật chồng nhau ra theo trục chồng ít hơn
    function goChongLan(ns) {
        var CACH = 12;
        for (var lan = 0; lan < 40; lan++) {
            var con = false;
            for (var i = 0; i < ns.length; i++) for (var j = i + 1; j < ns.length; j++) {
                var a = ns[i], b = ns[j];
                var ox = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x) + CACH;
                var oy = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y) + CACH;
                if (ox <= 0 || oy <= 0) continue;
                con = true;
                if (ox < oy) { var sx = (a.x < b.x ? -1 : 1) * ox / 2; a.x += sx; b.x -= sx; }
                else { var sy = (a.y < b.y ? -1 : 1) * oy / 2; a.y += sy; b.y -= sy; }
            }
            if (!con) break;
        }
    }

    // Thủ công: vị trí đã kéo tay (lưu localStorage); thiết bị chưa kéo lấy theo phân tầng cổ điển
    function bocucTay(g) {
        bocucTang(g);
        g.nodes.forEach(function (n) {
            var p = cfg.pos[n.id];
            if (p && isFinite(p.x) && isFinite(p.y)) { n.x = p.x; n.y = p.y; }
        });
        return {};
    }

    var BOCUC = { tang: bocucTang, gon: bocucGon, tron: bocucTron, luc: bocucLuc, tay: bocucTay };

    // ================= VẼ =================
    function veNode(n) {
        var g = el("g", { class: "sodo-node", "data-id": n.id, transform: "translate(" + n.x + "," + n.y + ")" });
        g.appendChild(el("rect", { class: "sodo-vien", x: 0, y: 0, width: NODE_W, height: NODE_H, rx: 10, fill: "#fff", stroke: mau(n.tt), "stroke-width": 2 }));
        g.appendChild(el("circle", { cx: 16, cy: NODE_H / 2, r: 6, fill: mau(n.tt) }));
        g.appendChild(el("text", { x: 30, y: 19, "font-size": 12.5, "font-weight": 600, fill: "#1f2937" }, cat(n.ten, 20)));
        if (n.phu) g.appendChild(el("text", { x: 30, y: 35, "font-size": 11, fill: "#6b7280" }, cat(n.phu, 22)));
        g.appendChild(el("title", null, n.ten + (n.phu ? " — " + n.phu : "") + " [" + nhanTt(n.tt) + "]"));
        return g;
    }

    // Hộp cụm liệt kê thiết bị kèm chấm trạng thái
    function veHop(n) {
        var c = coHop(n.items.length);
        var g = el("g", { class: "sodo-node", "data-id": n.id, transform: "translate(" + n.x + "," + n.y + ")" });
        g.appendChild(el("rect", { class: "sodo-vien", x: 0, y: 0, width: c.w, height: c.h, rx: 12, fill: "#fff", stroke: "#d1d5db", "stroke-width": 1.5 }));
        g.appendChild(el("rect", { x: 0, y: 0, width: c.w, height: 26, rx: 12, fill: n.mau, opacity: 0.12 }));
        g.appendChild(el("text", { x: 10, y: 17, "font-size": 12, "font-weight": 700, fill: "#374151" }, cat(n.ten + " (" + n.items.length + ")", 26 * c.cot)));
        n.chips = [];
        n.items.forEach(function (it, i) {
            // Điền theo cột: hết một cột mới sang cột kế, giữ thứ tự đọc từ trên xuống
            var cot = Math.floor(i / c.dong), dong = i % c.dong;
            var xx = 4 + cot * COL_W, yy = 26 + 6 + dong * CHIP_H;
            g.appendChild(el("circle", { cx: xx + 10, cy: yy + 8, r: 5, fill: mau(it.tt) }));
            var t = el("text", { x: xx + 22, y: yy + 12, "font-size": 11.5, fill: "#374151" }, cat(it.ten, 22));
            t.appendChild(el("title", null, it.ten + (it.ip ? " — " + it.ip : "") + " [" + nhanTt(it.tt) + "]"));
            g.appendChild(t);
            n.chips.push({ it: it, t: t });
        });
        return g;
    }

    // Cụm dạng nút gọn: chỉ tên + số lượng online / mất KN, danh sách xem trong tooltip
    function veGon(n) {
        var up = 0, down = 0;
        n.items.forEach(function (it) { if (it.tt === "UP") up++; else if (it.tt === "DOWN") down++; });
        var g = el("g", { class: "sodo-node", "data-id": n.id, transform: "translate(" + n.x + "," + n.y + ")" });
        g.appendChild(el("rect", { class: "sodo-vien", x: 0, y: 0, width: GON_W, height: GON_H, rx: 12, fill: "#fff", stroke: mau(n.tt), "stroke-width": 2 }));
        g.appendChild(el("rect", { x: 0, y: 0, width: 8, height: GON_H, rx: 4, fill: n.mau }));
        g.appendChild(el("text", { x: 18, y: 21, "font-size": 12.5, "font-weight": 700, fill: "#1f2937" }, cat(n.ten, 24)));
        var t = el("text", { x: 18, y: 40, "font-size": 11.5, fill: "#6b7280" }, n.items.length + " thiết bị  ");
        t.appendChild(el("tspan", { fill: MAU.UP, "font-weight": 600 }, "● " + up + "  "));
        t.appendChild(el("tspan", { fill: down ? MAU.DOWN : "#9ca3af", "font-weight": 600 }, "● " + down));
        g.appendChild(t);
        var ds = n.items.slice(0, 40).map(function (it) { return (it.tt === "DOWN" ? "✖ " : it.tt === "UP" ? "● " : "○ ") + it.ten + (it.ip ? " — " + it.ip : ""); });
        if (n.items.length > 40) ds.push("… và " + (n.items.length - 40) + " thiết bị khác");
        g.appendChild(el("title", null, n.ten + "\n" + ds.join("\n")));
        n.chips = [];
        return g;
    }

    function veKhung(k) {
        var g = el("g", {});
        g.appendChild(el("rect", { x: k.x, y: k.y, width: k.w, height: k.h, rx: 14, fill: "#f8fafc", stroke: "#cbd5e1", "stroke-width": 1.2, "stroke-dasharray": "5 4" }));
        g.appendChild(el("text", { x: k.x + FP, y: k.y + 17, "font-size": 12, "font-weight": 700, fill: "#475569" }, k.ten));
        return g;
    }

    // Đường nối giữa 2 điểm theo kiểu người dùng chọn; doc = đi theo chiều dọc (trên→dưới)
    function duong(x1, y1, x2, y2, doc) {
        var k = cfg.canh, mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
        if (k === "thang") return "M" + x1 + "," + y1 + " L" + x2 + "," + y2;
        if (k === "gap") return doc
            ? "M" + x1 + "," + y1 + " V" + my + " H" + x2 + " V" + y2
            : "M" + x1 + "," + y1 + " H" + mx + " V" + y2 + " H" + x2;
        return doc
            ? "M" + x1 + "," + y1 + " C" + x1 + "," + my + " " + x2 + "," + my + " " + x2 + "," + y2
            : "M" + x1 + "," + y1 + " C" + mx + "," + y1 + " " + mx + "," + y2 + " " + x2 + "," + y2;
    }
    // Chọn cạnh của hình để nối: nút dưới thì đáy→đỉnh, nằm ngang thì mép trái/phải
    function duongGiua(a, b) {
        if (b.y >= a.y + a.h) return duong(a.x + a.w / 2, a.y + a.h, b.x + b.w / 2, b.y, true);
        if (a.y >= b.y + b.h) return duong(a.x + a.w / 2, a.y, b.x + b.w / 2, b.y + b.h, true);
        if (b.x >= a.x + a.w) return duong(a.x + a.w, a.y + a.h / 2, b.x, b.y + b.h / 2, false);
        if (a.x >= b.x + b.w) return duong(a.x, a.y + a.h / 2, b.x + b.w, b.y + b.h / 2, false);
        return duong(a.x + a.w / 2, a.y + a.h / 2, b.x + b.w / 2, b.y + b.h / 2, true);
    }
    function kieuCanh(path, tt) {
        // Cạnh tới thiết bị/cụm mất kết nối hoàn toàn tô đỏ nét đứt để nhìn ra nhánh hỏng
        path.setAttribute("stroke", tt === "DOWN" ? "#fca5a5" : "#cbd5e1");
        path.setAttribute("stroke-width", 1.5);
        if (tt === "DOWN") path.setAttribute("stroke-dasharray", "5 4");
    }

    function render(giuGoc) {
        if (!duLieu) return;
        var g = dungDoThi(duLieu);
        var kq = (BOCUC[cfg.bocuc] || bocucTang)(g);
        doThi = g; nodeMap = {}; keCanh = {};
        g.nodes.forEach(function (n) { nodeMap[n.id] = n; keCanh[n.id] = []; });

        clear(svg);
        gCam = el("g", {});
        var gKhung = el("g", {}), gCanh = el("g", {}), gNode = el("g", {});
        gCam.appendChild(gKhung); gCam.appendChild(gCanh); gCam.appendChild(gNode);
        svg.appendChild(gCam);

        (kq.khung || []).forEach(function (k) { gKhung.appendChild(veKhung(k)); });
        g.canh.forEach(function (c) {
            var a = nodeMap[c.a], b = nodeMap[c.b];
            if (!a || !b || b.trongKhung) return; // trong khung: đã có 1 cạnh nối vào khung
            c.el = el("path", { class: "sodo-canh", fill: "none", d: duongGiua(a, b) });
            kieuCanh(c.el, b.tt);
            gCanh.appendChild(c.el);
            keCanh[a.id].push(c); keCanh[b.id].push(c);
        });
        (kq.canhThem || []).forEach(function (c) {
            var n = c.nguon, p = el("path", { class: "sodo-canh", fill: "none", d: duong(n.x + n.w / 2, n.y + n.h, c.x, c.y, true) });
            kieuCanh(p, "UP");
            gCanh.appendChild(p);
        });
        g.nodes.forEach(function (n) {
            n.el = n.items ? (cfg.cum === "gon" ? veGon(n) : veHop(n)) : veNode(n);
            gNode.appendChild(n.el);
        });

        // Khung bao để vừa khung / chặn kéo trôi mất
        var x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
        g.nodes.concat(kq.khung || []).forEach(function (r) {
            x0 = Math.min(x0, r.x); y0 = Math.min(y0, r.y); x1 = Math.max(x1, r.x + r.w); y1 = Math.max(y1, r.y + r.h);
        });
        bien = { x0: x0 - PAD, y0: y0 - PAD, x1: x1 + PAD, y1: y1 + PAD };

        capNhatThongKe(g);
        if (dangTai) dangTai.classList.add("d-none");
        if (oLoi) oLoi.classList.add("d-none");
        wrap.classList.remove("d-none");
        timKiem(false);
        // Phải chạy sau khi khung hiện ra mới đo được kích thước khung
        if (giuGoc) apDung(); else gocBanDau();
    }

    function capNhatThongKe(g) {
        var tk = duLieu.thongKe || {};
        var up = (tk.sw ? tk.sw.up : 0) + (tk.ap ? tk.ap.up : 0) + (tk.cam ? tk.cam.up : 0);
        var down = (tk.sw ? tk.sw.down : 0) + (tk.ap ? tk.ap.down : 0) + (tk.cam ? tk.cam.down : 0);
        setText("sodoUp", up); setText("sodoDown", down);
        var tong = (tk.sw ? tk.sw.tong : 0) + (tk.ap ? tk.ap.tong : 0) + (tk.cam ? tk.cam.tong : 0);
        var sw = g.core.concat(g.pp, g.tc).filter(function (n) { return n.id !== "core-trong"; }).length, ap = 0, cam = 0;
        g.cum.forEach(function (n) { if (n.loai === "ap") ap += n.items.length; else cam += n.items.length; });
        setText("sodoTkHien", (sw + ap + cam) + " / " + tong);
        setText("sodoTkSw", sw); setText("sodoTkAp", ap); setText("sodoTkCam", cam);
        setText("sodoTkCum", g.cum.length);
    }

    // ================= KÉO / ZOOM kiểu bảng vẽ (transform, không thanh cuộn) =================
    function apDung() {
        if (!gCam) return;
        // Chỉ chặn để sơ đồ không trôi mất hẳn khỏi khung (luôn chừa ít nhất ~80px trong tầm nhìn)
        var W = wrap.clientWidth, H = wrap.clientHeight, chua = 80;
        tx = Math.min(W - chua - bien.x0 * zoom, Math.max(chua - bien.x1 * zoom, tx));
        ty = Math.min(H - chua - bien.y0 * zoom, Math.max(chua - bien.y1 * zoom, ty));
        gCam.setAttribute("transform", "translate(" + tx + "," + ty + ") scale(" + zoom + ")");
        setText("sodoZoomPct", Math.round(zoom * 100) + "%");
    }
    // (ax, ay) = điểm trong khung giữ đứng yên khi zoom (mặc định tâm khung)
    function datZoom(z, ax, ay) {
        z = Math.min(Z_MAX, Math.max(Z_MIN, z));
        if (ax == null) { ax = wrap.clientWidth / 2; ay = wrap.clientHeight / 2; }
        tx = ax - (ax - tx) * z / zoom;
        ty = ay - (ay - ty) * z / zoom;
        zoom = z;
        apDung();
    }
    function vuaKhung() {
        var bw = bien.x1 - bien.x0, bh = bien.y1 - bien.y0;
        if (!(bw > 0 && bh > 0)) return;
        var W = wrap.clientWidth, H = wrap.clientHeight;
        zoom = Math.max(Z_MIN, Math.min(W / bw, H / bh, 1));
        tx = (W - bw * zoom) / 2 - bien.x0 * zoom;
        ty = (H - bh * zoom) / 2 - bien.y0 * zoom;
        apDung();
    }
    // Phân tầng: bám đỉnh, giữa là công ty, chữ không thu quá nhỏ (sơ đồ rộng thì kéo ngang xem tiếp)
    function gocBanDau() {
        if (cfg.bocuc !== "tang" && cfg.bocuc !== "tay") { vuaKhung(); return; }
        var W = wrap.clientWidth, bw = bien.x1 - bien.x0, c = doThi.cty;
        zoom = Math.min(1, Math.max(0.55, W / bw));
        if (bw * zoom <= W) tx = (W - bw * zoom) / 2 - bien.x0 * zoom;
        else tx = W / 2 - (c.x + c.w / 2) * zoom;
        ty = 4 - bien.y0 * zoom;
        apDung();
    }
    function denNode(n) {
        var W = wrap.clientWidth, H = wrap.clientHeight;
        zoom = Math.max(zoom, 1);
        tx = W / 2 - (n.x + n.w / 2) * zoom;
        ty = H / 2 - (n.y + Math.min(n.h, 200) / 2) * zoom;
        apDung();
    }

    wrap.addEventListener("wheel", function (e) {
        if (!gCam) return;
        e.preventDefault();
        var r = wrap.getBoundingClientRect();
        datZoom(zoom * (e.deltaY < 0 ? 1.12 : 1 / 1.12), e.clientX - r.left, e.clientY - r.top);
    }, { passive: false });

    // Giữ chuột trên nền = di chuyển sơ đồ; giữ trên một thiết bị/cụm = kéo thiết bị đó (chuyển sang Thủ công)
    (function ganKeo() {
        var cheDo = null, x0 = 0, y0 = 0, tx0 = 0, ty0 = 0, idKeo = null, nx0 = 0, ny0 = 0, daDi = false;
        wrap.addEventListener("pointerdown", function (e) {
            if (e.pointerType === "mouse" && e.button !== 0) return;
            var g = e.target.closest ? e.target.closest(".sodo-node[data-id]") : null;
            x0 = e.clientX; y0 = e.clientY; daDi = false;
            if (g && nodeMap[g.getAttribute("data-id")]) {
                cheDo = "node"; idKeo = g.getAttribute("data-id");
                nx0 = nodeMap[idKeo].x; ny0 = nodeMap[idKeo].y;
            } else {
                cheDo = "nen"; tx0 = tx; ty0 = ty;
            }
            wrap.setPointerCapture(e.pointerId);
            wrap.classList.add("dang-keo");
            e.preventDefault(); // không bôi đen chữ trong SVG khi kéo
        });
        wrap.addEventListener("pointermove", function (e) {
            if (!cheDo) return;
            var dx = e.clientX - x0, dy = e.clientY - y0;
            if (!daDi && Math.abs(dx) + Math.abs(dy) < 4) return; // bấm nhẹ không tính là kéo
            if (cheDo === "nen") { daDi = true; tx = tx0 + dx; ty = ty0 + dy; apDung(); return; }
            if (!daDi) {
                daDi = true;
                // Kéo lần đầu ở bố cục tự động → chốt toàn bộ vị trí hiện tại thành bố cục thủ công
                if (cfg.bocuc !== "tay") {
                    cfg.pos = {};
                    doThi.nodes.forEach(function (n) { cfg.pos[n.id] = { x: n.x, y: n.y }; });
                    cfg.bocuc = "tay"; dongBoDieuKhien();
                    render(true);
                }
            }
            var n = nodeMap[idKeo];
            if (!n) return;
            n.x = nx0 + dx / zoom; n.y = ny0 + dy / zoom;
            n.el.setAttribute("transform", "translate(" + n.x + "," + n.y + ")");
            keCanh[n.id].forEach(function (c) {
                if (c.el) c.el.setAttribute("d", duongGiua(nodeMap[c.a], nodeMap[c.b]));
            });
        });
        function tha(e) {
            if (!cheDo) return;
            if (cheDo === "node" && daDi && nodeMap[idKeo]) {
                var n = nodeMap[idKeo];
                cfg.pos[n.id] = { x: Math.round(n.x), y: Math.round(n.y) };
                bien.x0 = Math.min(bien.x0, n.x - PAD); bien.y0 = Math.min(bien.y0, n.y - PAD);
                bien.x1 = Math.max(bien.x1, n.x + n.w + PAD); bien.y1 = Math.max(bien.y1, n.y + n.h + PAD);
                luuCfg();
            }
            cheDo = null;
            wrap.classList.remove("dang-keo");
            if (wrap.hasPointerCapture(e.pointerId)) wrap.releasePointerCapture(e.pointerId);
        }
        wrap.addEventListener("pointerup", tha);
        wrap.addEventListener("pointercancel", tha);
    })();

    // ================= TÌM KIẾM: làm mờ thiết bị không khớp, Enter để nhảy tới =================
    function timKiem(nhay) {
        if (!doThi) return;
        var q = oTim ? oTim.value.trim().toLowerCase() : "";
        var khop = [], dem = 0;
        function co(s) { return s != null && String(s).toLowerCase().indexOf(q) >= 0; }
        doThi.nodes.forEach(function (n) {
            // Cụm khớp khi tên cụm khớp hoặc có thiết bị bên trong khớp
            var trung = !!q && (co(n.ten) || co(n.ip));
            if (n.items) n.items.forEach(function (it) { if (q && (co(it.ten) || co(it.ip))) { trung = true; dem++; } });
            else if (trung) dem++;
            (n.chips || []).forEach(function (c) { c.t.classList.toggle("sodo-chip-khop", !!q && (co(c.it.ten) || co(c.it.ip))); });
            n.el.classList.toggle("sodo-khop", trung);
            n.el.classList.toggle("sodo-mo", !!q && !trung);
            n.khop = trung;
            if (trung) khop.push(n);
        });
        doThi.canh.forEach(function (c) { if (c.el) c.el.classList.toggle("sodo-mo", !!q && !nodeMap[c.b].khop); });
        setText("sodoTimKq", q ? (dem ? dem + " kết quả" : "Không thấy") : "");
        if (nhay && khop.length) denNode(khop[0]);
    }
    if (oTim) {
        oTim.addEventListener("input", function () { timKiem(false); });
        oTim.addEventListener("keydown", function (e) { if (e.key === "Enter") { e.preventDefault(); timKiem(true); } });
    }

    // ================= ĐIỀU KHIỂN =================
    function $(id) { return document.getElementById(id); }
    function dongBoDieuKhien() {
        if ($("sodoBoCuc")) $("sodoBoCuc").value = cfg.bocuc;
        if ($("sodoKieuCanh")) $("sodoKieuCanh").value = cfg.canh;
        if ($("sodoKieuCum")) $("sodoKieuCum").value = cfg.cum;
        if ($("sodoLocTt")) $("sodoLocTt").value = cfg.tt;
        var chon = 0, tong = 0;
        document.querySelectorAll("[data-sodo-loai]").forEach(function (cb) {
            cb.checked = !!cfg.loai[cb.getAttribute("data-sodo-loai")];
            tong++; if (cb.checked) chon++;
        });
        setText("sodoLoaiNhan", chon === tong ? "Tất cả" : chon + "/" + tong);
    }
    function doiChon(id, khoa, giuGoc) {
        var s = $(id);
        if (!s) return;
        s.addEventListener("change", function () { cfg[khoa] = s.value; luuCfg(); render(giuGoc); });
    }
    doiChon("sodoBoCuc", "bocuc", false);
    doiChon("sodoKieuCanh", "canh", true);
    doiChon("sodoKieuCum", "cum", false);
    doiChon("sodoLocTt", "tt", true);

    document.addEventListener("change", function (e) {
        var cb = e.target.closest && e.target.closest("[data-sodo-loai]");
        if (!cb) return;
        cfg.loai[cb.getAttribute("data-sodo-loai")] = cb.checked ? 1 : 0;
        luuCfg(); dongBoDieuKhien(); render(true);
    });
    document.addEventListener("click", function (e) {
        var b = e.target.closest && e.target.closest("[data-sodo-loai-tat]");
        if (!b) return;
        e.preventDefault();
        var bat = b.getAttribute("data-sodo-loai-tat") === "1";
        for (var k in cfg.loai) cfg.loai[k] = bat ? 1 : 0;
        luuCfg(); dongBoDieuKhien(); render(true);
    });

    function ganNut(id, fn) { var b = $(id); if (b) b.addEventListener("click", function (e) { e.preventDefault(); fn(); }); }
    ganNut("sodoZoomVao", function () { datZoom(zoom * 1.2); });
    ganNut("sodoZoomRa", function () { datZoom(zoom / 1.2); });
    ganNut("sodoZoomPct", function () { datZoom(1); });
    ganNut("sodoVuaKhung", vuaKhung);
    ganNut("sodoLamMoi", tai);
    // Xếp lại: bỏ vị trí kéo tay, về phân tầng cổ điển
    ganNut("sodoXepLai", function () {
        cfg.pos = {};
        if (cfg.bocuc === "tay") cfg.bocuc = "tang";
        luuCfg(); dongBoDieuKhien(); render(false);
    });
    ganNut("sodoToanManHinh", function () {
        var the = $("sodoThe");
        if (!the) return;
        if (document.fullscreenElement) document.exitFullscreen();
        else if (the.requestFullscreen) the.requestFullscreen();
    });
    document.addEventListener("fullscreenchange", function () { setTimeout(function () { render(true); vuaKhung(); }, 60); });

    // Tải sơ đồ đang hiển thị thành file SVG (mở được bằng trình duyệt / chèn vào Word, Visio)
    ganNut("sodoTaiSvg", function () {
        if (!gCam) return;
        var bw = bien.x1 - bien.x0, bh = bien.y1 - bien.y0;
        var out = el("svg", { xmlns: SVGNS, width: Math.ceil(bw), height: Math.ceil(bh), viewBox: bien.x0 + " " + bien.y0 + " " + bw + " " + bh, "font-family": "Segoe UI, Arial, sans-serif" });
        out.appendChild(el("rect", { x: bien.x0, y: bien.y0, width: bw, height: bh, fill: "#fff" }));
        var ban = gCam.cloneNode(true);
        ban.removeAttribute("transform");
        out.appendChild(ban);
        var blob = new Blob([new XMLSerializer().serializeToString(out)], { type: "image/svg+xml;charset=utf-8" });
        var a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = "so-do-mang-" + congTy + ".svg";
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    });

    // Đổi cỡ cửa sổ: bố cục gọn phụ thuộc bề rộng nên dựng lại; bố cục khác chỉ cần chặn lại khung nhìn
    var henDung = null;
    window.addEventListener("resize", function () {
        if (!duLieu) return;
        clearTimeout(henDung);
        henDung = setTimeout(function () { if (cfg.bocuc === "gon") render(true); else apDung(); }, 200);
    });

    function tai() {
        if (dangTai) dangTai.classList.remove("d-none");
        wrap.classList.add("d-none");
        fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (res) {
                if (!res.ok) throw new Error("Máy chủ trả lỗi " + res.status);
                return res.json();
            })
            .then(function (d) {
                if (!d || !d.thanhCong) { hienLoi((d && d.thongBao) || "Không tải được sơ đồ."); return; }
                var lanDau = !duLieu;
                duLieu = d;
                render(!lanDau);
            })
            .catch(function (e) { hienLoi("Lỗi tải sơ đồ: " + e.message); });
    }

    dongBoDieuKhien();
    tai();
})();
