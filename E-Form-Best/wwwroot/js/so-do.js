// Sơ đồ mạng (topology) tự sinh từ DB — chỉ render, dữ liệu lấy ở /SoDo/{congTy}/DuLieu.
// Vẽ theo tầng: Công ty → Core → Phân phối → Truy cập; cụm AP (theo controller) và
// cụm Camera (theo đầu ghi) là các hộp liệt kê thiết bị kèm chấm trạng thái.
// Không dùng thư viện ngoài: dựng SVG bằng DOM thuần.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var MAU = { UP: "#22c55e", DOWN: "#ef4444", CHUA: "#9ca3af", KHONG: "#cbd5e1" };

    var trang = document.getElementById("sodoTrang");
    if (!trang) return;
    var url = trang.getAttribute("data-url");
    var svg = document.getElementById("sodoSvg");
    var wrap = document.getElementById("sodoWrap");
    var dangTai = document.getElementById("sodoDangTai");
    var oLoi = document.getElementById("sodoLoi");

    function el(name, attrs, text) {
        var e = document.createElementNS(SVGNS, name);
        if (attrs) for (var k in attrs) if (attrs.hasOwnProperty(k)) e.setAttribute(k, attrs[k]);
        if (text != null) e.textContent = text;
        return e;
    }
    function mau(tt) { return MAU[tt] || MAU.CHUA; }
    function clear(n) { while (n.firstChild) n.removeChild(n.firstChild); }

    function hienLoi(msg) {
        if (dangTai) dangTai.classList.add("d-none");
        if (oLoi) { oLoi.textContent = msg; oLoi.classList.remove("d-none"); }
    }

    // ---- Kích thước cơ bản ----
    var NODE_W = 170, NODE_H = 46, GAP_X = 26, GAP_Y = 74, GAP_ROW = 14;
    var CHIP_H = 22, BOX_GAP_X = 24, BOX_GAP_Y = 18;
    var COL_W = 180, CHIP_MAX_DONG = 12, BOX_MAX_COT = 4; // hộp cụm: quá 12 dòng thì chia cột, tối đa 4 cột
    var FP = 14, FRAME_HEAD = 24; // lề trong + đầu khung khi một tầng phải xuống dòng
    var PAD = 24; // lề quanh sơ đồ
    var RONG_MIN = 900;

    var duLieu = null;          // giữ dữ liệu lần tải gần nhất để dựng lại khi đổi cỡ cửa sổ
    var zoom = 1, baseW = 0, baseH = 0, tx = 0, ty = 0, gCam = null, daCanh = false;
    var Z_MIN = 0.2, Z_MAX = 3;

    // Vẽ 1 node thiết bị (rounded rect + icon chấm trạng thái + nhãn)
    function veNode(x, y, nhan, phu, tt, icon) {
        var g = el("g", { class: "sodo-node", transform: "translate(" + x + "," + y + ")" });
        g.appendChild(el("rect", { x: 0, y: 0, width: NODE_W, height: NODE_H, rx: 10, fill: "#fff", stroke: mau(tt), "stroke-width": 2 }));
        g.appendChild(el("circle", { cx: 16, cy: NODE_H / 2, r: 6, fill: mau(tt) }));
        var t1 = el("text", { x: 30, y: 19, "font-size": 12.5, "font-weight": 600, fill: "#1f2937" }, cat(nhan, 20));
        g.appendChild(t1);
        if (phu) g.appendChild(el("text", { x: 30, y: 35, "font-size": 11, fill: "#6b7280" }, cat(phu, 22)));
        var ti = el("title", null, nhan + (phu ? " — " + phu : "") + " [" + nhanTt(tt) + "]");
        g.appendChild(ti);
        return g;
    }

    function nhanTt(tt) {
        return tt === "UP" ? "Online" : tt === "DOWN" ? "Mất kết nối" : tt === "KHONG" ? "Không theo dõi" : "Chưa kiểm";
    }
    function cat(s, n) { s = s == null ? "" : String(s); return s.length > n ? s.slice(0, n - 1) + "…" : s; }

    // Kích thước hộp cụm: danh sách dài chia thành nhiều cột để hộp không kéo dài xuống quá sâu
    function coHop(n) {
        var cot = Math.min(BOX_MAX_COT, Math.max(1, Math.ceil(n / CHIP_MAX_DONG)));
        var dong = Math.max(1, Math.ceil(n / cot));
        return { cot: cot, dong: dong, w: cot * COL_W + 20, h: 26 + 6 + dong * CHIP_H + 8 };
    }

    // Vẽ 1 hộp cụm (AP theo controller / Camera theo đầu ghi) với danh sách chip thiết bị
    function veHop(x, y, tieuDe, items, iconMau) {
        var c = coHop(items.length);
        var g = el("g", { class: "sodo-node", transform: "translate(" + x + "," + y + ")" });
        g.appendChild(el("rect", { x: 0, y: 0, width: c.w, height: c.h, rx: 12, fill: "#fff", stroke: "#d1d5db", "stroke-width": 1.5 }));
        g.appendChild(el("rect", { x: 0, y: 0, width: c.w, height: 26, rx: 12, fill: iconMau, opacity: 0.12 }));
        g.appendChild(el("text", { x: 10, y: 17, "font-size": 12, "font-weight": 700, fill: "#374151" }, cat(tieuDe + " (" + items.length + ")", 26 * c.cot)));
        for (var i = 0; i < items.length; i++) {
            // Điền theo cột: hết một cột mới sang cột kế, giữ thứ tự đọc từ trên xuống
            var it = items[i], cot = Math.floor(i / c.dong), dong = i % c.dong;
            var xx = 4 + cot * COL_W, yy = 26 + 6 + dong * CHIP_H;
            g.appendChild(el("circle", { cx: xx + 10, cy: yy + 8, r: 5, fill: mau(it.tt) }));
            var tx = el("text", { x: xx + 22, y: yy + 12, "font-size": 11.5, fill: "#374151" }, cat(it.ten, 22));
            var ttl = el("title", null, it.ten + (it.ip ? " — " + it.ip : "") + " [" + nhanTt(it.tt) + "]");
            tx.appendChild(ttl);
            g.appendChild(tx);
        }
        return { g: g, w: c.w, h: c.h };
    }

    // Khung bao một tầng khi phải xuống nhiều dòng — nối 1 cạnh vào khung thay vì hàng trăm cạnh chéo nhau
    function veKhung(x, y, w, h, ten) {
        var g = el("g", {});
        g.appendChild(el("rect", { x: x, y: y, width: w, height: h, rx: 14, fill: "#f8fafc", stroke: "#cbd5e1", "stroke-width": 1.2, "stroke-dasharray": "5 4" }));
        g.appendChild(el("text", { x: x + FP, y: y + 17, "font-size": 12, "font-weight": 700, fill: "#475569" }, ten));
        return g;
    }

    // Đường nối cong giữa hai điểm (từ đáy node trên tới đỉnh node dưới)
    function veCanh(x1, y1, x2, y2) {
        var my = (y1 + y2) / 2;
        var d = "M" + x1 + "," + y1 + " C" + x1 + "," + my + " " + x2 + "," + my + " " + x2 + "," + y2;
        return el("path", { d: d, fill: "none", stroke: "#cbd5e1", "stroke-width": 1.5 });
    }

    // Xếp một hàng node giữa trục, trả về mảng toạ độ tâm trên/dưới
    function hangNode(nodes, y, tongRong) {
        var w = nodes.length * NODE_W + (nodes.length - 1) * GAP_X;
        var startX = Math.max(PAD, (tongRong - w) / 2);
        var res = [];
        for (var i = 0; i < nodes.length; i++) {
            var x = startX + i * (NODE_W + GAP_X);
            res.push({ node: nodes[i], x: x, cx: x + NODE_W / 2, top: y, bot: y + NODE_H });
        }
        return res;
    }

    function rongHang(n) { return n <= 0 ? 0 : n * NODE_W + (n - 1) * GAP_X; }

    // Bề rộng dựng sơ đồ = bề rộng khung đang hiển thị, để ở 100% các tầng tự xuống dòng thay vì phình ngang
    function rongKhaDung() {
        var w = (trang.clientWidth || 0) - 4;
        return Math.max(RONG_MIN, w);
    }

    function render(d) {
        clear(svg);
        var core = d.core || [], pp = d.phanPhoi || [], tc = d.truyCap || [];
        var nhomBox = [];
        (d.apNhom || []).forEach(function (g) { nhomBox.push({ tieuDe: "AP · " + g.ten, items: g.items || [], mau: "#22d3ee" }); });
        (d.camNhom || []).forEach(function (g) { nhomBox.push({ tieuDe: "Cam · " + g.ten, items: g.items || [], mau: "#a78bfa" }); });

        var tongRong = rongKhaDung();
        // Số node tối đa một dòng (trừ lề ngoài và lề trong khung)
        var moiDong = Math.max(1, Math.floor((tongRong - PAD * 2 - FP * 2 + GAP_X) / (NODE_W + GAP_X)));

        var gCanh = el("g", {}), gNode = el("g", {});
        // Mọi hình vẽ nằm trong 1 nhóm "camera" — kéo/zoom chỉ đổi transform của nhóm này
        gCam = el("g", {});
        gCam.appendChild(gCanh); gCam.appendChild(gNode);
        svg.appendChild(gCam);

        // Vẽ một tầng switch: vừa 1 dòng thì nối cạnh từng node; dài hơn thì gói vào khung lưới, nối 1 cạnh vào khung
        function veTang(list, y, ten, nguon) {
            if (list.length <= moiDong) {
                var row = hangNode(list, y, tongRong);
                row.forEach(function (r) {
                    gNode.appendChild(veNode(r.x, r.top, r.node.ten, r.node.ip, r.node.tt));
                    gCanh.appendChild(veCanh(nguon.cx, nguon.bot, r.cx, r.top));
                });
                return { bot: y + NODE_H, row: row };
            }
            var soDong = Math.ceil(list.length / moiDong);
            var fw = rongHang(moiDong) + FP * 2;
            var fh = FRAME_HEAD + soDong * (NODE_H + GAP_ROW) - GAP_ROW + FP;
            var fx = (tongRong - fw) / 2;
            gNode.appendChild(veKhung(fx, y, fw, fh, ten + " (" + list.length + ")"));
            for (var i = 0; i < list.length; i++) {
                var n = list[i];
                var nx = fx + FP + (i % moiDong) * (NODE_W + GAP_X);
                var ny = y + FRAME_HEAD + Math.floor(i / moiDong) * (NODE_H + GAP_ROW);
                gNode.appendChild(veNode(nx, ny, n.ten, n.ip, n.tt));
            }
            gCanh.appendChild(veCanh(nguon.cx, nguon.bot, tongRong / 2, y));
            return { bot: y + fh, row: null };
        }

        var y = PAD;
        // Tầng 0: công ty
        var ctY = y, ctCx = tongRong / 2;
        gNode.appendChild(veNode(ctCx - NODE_W / 2, ctY, d.congTy, "Công ty", "UP", null));
        var nutCt = { cx: ctCx, bot: ctY + NODE_H };
        y += NODE_H + GAP_Y;

        // Tầng 1: Core
        var tCore = veTang(core.length ? core : [{ ten: "(chưa có switch core)", ip: null, tt: "CHUA" }], y, "Core", nutCt);
        // Hub để nối các tầng dưới: core giữa hàng (hoặc tâm đáy khung core)
        var hub = tCore.row && tCore.row.length ? tCore.row[Math.floor(tCore.row.length / 2)] : { cx: ctCx, bot: tCore.bot };
        y = tCore.bot + GAP_Y;

        // Tầng 2: Phân phối
        if (pp.length) y = veTang(pp, y, "Phân phối", hub).bot + GAP_Y;

        // Tầng 3: Truy cập
        if (tc.length) y = veTang(tc, y, "Truy cập", hub).bot + GAP_Y;

        // Tầng 4: các hộp cụm AP + Camera — xếp theo kệ, hết bề rộng thì xuống kệ mới
        if (nhomBox.length) {
            var rongKe = tongRong - PAD * 2 - FP * 2;
            var ke = [], keHt = { items: [], w: 0, h: 0 };
            nhomBox.forEach(function (nb) {
                var c = coHop(nb.items.length);
                var w = Math.min(c.w, rongKe);
                if (keHt.items.length && keHt.w + BOX_GAP_X + w > rongKe) { ke.push(keHt); keHt = { items: [], w: 0, h: 0 }; }
                keHt.w += (keHt.items.length ? BOX_GAP_X : 0) + w;
                keHt.h = Math.max(keHt.h, c.h);
                keHt.items.push(nb);
            });
            ke.push(keHt);

            var nhieuKe = ke.length > 1;
            var yKe = y + (nhieuKe ? FRAME_HEAD : 0);
            var gHop = el("g", {});
            ke.forEach(function (k) {
                var bx = (tongRong - k.w) / 2;
                k.items.forEach(function (nb) {
                    var box = veHop(bx, yKe, nb.tieuDe, nb.items, nb.mau);
                    gHop.appendChild(box.g);
                    if (!nhieuKe) gCanh.appendChild(veCanh(hub.cx, hub.bot, bx + box.w / 2, yKe));
                    bx += box.w + BOX_GAP_X;
                });
                yKe += k.h + BOX_GAP_Y;
            });
            yKe -= BOX_GAP_Y;
            if (nhieuKe) {
                var maxKe = 0; ke.forEach(function (k) { if (k.w > maxKe) maxKe = k.w; });
                var kw = maxKe + FP * 2;
                gNode.appendChild(veKhung((tongRong - kw) / 2, y, kw, yKe - y + FP, "Cụm AP · Camera (" + nhomBox.length + ")"));
                gCanh.appendChild(veCanh(hub.cx, hub.bot, tongRong / 2, y));
                yKe += FP;
            }
            gNode.appendChild(gHop);
            y = yKe + GAP_Y;
        }

        baseW = tongRong;
        baseH = y - GAP_Y + PAD;

        // Thống kê lên thanh tiêu đề
        var tk = d.thongKe || {};
        var up = (tk.sw ? tk.sw.up : 0) + (tk.ap ? tk.ap.up : 0) + (tk.cam ? tk.cam.up : 0);
        var down = (tk.sw ? tk.sw.down : 0) + (tk.ap ? tk.ap.down : 0) + (tk.cam ? tk.cam.down : 0);
        setText("sodoUp", up); setText("sodoDown", down);

        if (dangTai) dangTai.classList.add("d-none");
        if (oLoi) oLoi.classList.add("d-none");
        wrap.classList.remove("d-none");
        // Phải chạy sau khi khung hiện ra mới đo được kích thước khung
        if (!daCanh) { daCanh = true; vuaNgang(); } else apDung();
    }

    function setText(id, v) { var e = document.getElementById(id); if (e) e.textContent = v; }

    // ---- Kéo / zoom kiểu bảng vẽ: không dùng thanh cuộn nên kéo trái/phải/lên/xuống tự do,
    // không bị chặn ở mép khi sơ đồ đã vừa bề rộng khung. tx/ty = độ dời, zoom = tỉ lệ.
    function apDung() {
        if (!gCam) return;
        // Chỉ chặn để sơ đồ không trôi mất hẳn khỏi khung (luôn chừa ít nhất ~80px trong tầm nhìn)
        var W = wrap.clientWidth, H = wrap.clientHeight, chua = 80;
        tx = Math.min(W - chua, Math.max(chua - baseW * zoom, tx));
        ty = Math.min(H - chua, Math.max(chua - baseH * zoom, ty));
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
        if (!baseW || !baseH) return;
        var W = wrap.clientWidth, H = wrap.clientHeight;
        zoom = Math.max(Z_MIN, Math.min((W - 8) / baseW, (H - 8) / baseH, 1));
        tx = (W - baseW * zoom) / 2;
        ty = Math.max(4, (H - baseH * zoom) / 2);
        apDung();
    }

    // Lần đầu mở: vừa bề rộng, bám đỉnh — sơ đồ cao thì kéo xuống xem tiếp, chữ không bị thu quá nhỏ
    function vuaNgang() {
        if (!baseW) return;
        var W = wrap.clientWidth;
        zoom = Math.max(Z_MIN, Math.min((W - 8) / baseW, 1));
        tx = (W - baseW * zoom) / 2;
        ty = 4;
        apDung();
    }

    function ganNut(id, fn) { var b = document.getElementById(id); if (b) b.addEventListener("click", function () { fn(); }); }
    ganNut("sodoZoomVao", function () { datZoom(zoom * 1.2); });
    ganNut("sodoZoomRa", function () { datZoom(zoom / 1.2); });
    ganNut("sodoZoomPct", function () { datZoom(1); });
    ganNut("sodoVuaKhung", vuaKhung);

    // Lăn chuột trong khung = zoom quanh con trỏ (kéo chuột để di chuyển)
    wrap.addEventListener("wheel", function (e) {
        if (!baseW) return;
        e.preventDefault();
        var r = wrap.getBoundingClientRect();
        datZoom(zoom * (e.deltaY < 0 ? 1.12 : 1 / 1.12), e.clientX - r.left, e.clientY - r.top);
    }, { passive: false });

    // Đổi cỡ cửa sổ → dựng lại để số node mỗi dòng khớp bề rộng mới
    var henDung = null;
    window.addEventListener("resize", function () {
        if (!duLieu) return;
        clearTimeout(henDung);
        henDung = setTimeout(function () { render(duLieu); }, 200);
    });

    function tai() {
        if (dangTai) { dangTai.classList.remove("d-none"); }
        wrap.classList.add("d-none");
        fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (res) {
                if (!res.ok) throw new Error("Máy chủ trả lỗi " + res.status);
                return res.json();
            })
            .then(function (d) {
                if (!d || !d.thanhCong) { hienLoi((d && d.thongBao) || "Không tải được sơ đồ."); return; }
                duLieu = d;
                render(d);
            })
            .catch(function (e) { hienLoi("Lỗi tải sơ đồ: " + e.message); });
    }

    // Bấm giữ chuột trái (hoặc chạm) rồi kéo để di chuyển sơ đồ theo mọi hướng
    (function ganKeoSoDo() {
        var dangKeo = false, x0 = 0, y0 = 0, tx0 = 0, ty0 = 0;
        wrap.addEventListener("pointerdown", function (e) {
            if (e.pointerType === "mouse" && e.button !== 0) return;
            dangKeo = true;
            x0 = e.clientX; y0 = e.clientY; tx0 = tx; ty0 = ty;
            wrap.setPointerCapture(e.pointerId);
            wrap.classList.add("dang-keo");
            e.preventDefault(); // không bôi đen chữ trong SVG khi kéo
        });
        wrap.addEventListener("pointermove", function (e) {
            if (!dangKeo) return;
            tx = tx0 + (e.clientX - x0);
            ty = ty0 + (e.clientY - y0);
            apDung();
        });
        function thaKeo(e) {
            if (!dangKeo) return;
            dangKeo = false;
            wrap.classList.remove("dang-keo");
            if (wrap.hasPointerCapture(e.pointerId)) wrap.releasePointerCapture(e.pointerId);
        }
        wrap.addEventListener("pointerup", thaKeo);
        wrap.addEventListener("pointercancel", thaKeo);
    })();

    var btn = document.getElementById("sodoLamMoi");
    if (btn) btn.addEventListener("click", tai);
    tai();
})();
