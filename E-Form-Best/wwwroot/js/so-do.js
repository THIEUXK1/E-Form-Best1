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
    var NODE_W = 170, NODE_H = 46, GAP_X = 26, GAP_Y = 74;
    var BOX_W = 210, CHIP_H = 22, BOX_PAD = 34, BOX_GAP_X = 24;
    var PAD = 24; // lề quanh sơ đồ

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

    // Vẽ 1 hộp cụm (AP theo controller / Camera theo đầu ghi) với danh sách chip thiết bị
    function veHop(x, y, tieuDe, items, iconMau) {
        var h = BOX_PAD + items.length * CHIP_H + 10;
        var g = el("g", { class: "sodo-node", transform: "translate(" + x + "," + y + ")" });
        g.appendChild(el("rect", { x: 0, y: 0, width: BOX_W, height: h, rx: 12, fill: "#fff", stroke: "#d1d5db", "stroke-width": 1.5 }));
        g.appendChild(el("rect", { x: 0, y: 0, width: BOX_W, height: 26, rx: 12, fill: iconMau, opacity: 0.12 }));
        g.appendChild(el("text", { x: 10, y: 17, "font-size": 12, "font-weight": 700, fill: "#374151" }, cat(tieuDe + " (" + items.length + ")", 26)));
        for (var i = 0; i < items.length; i++) {
            var it = items[i], yy = 26 + 6 + i * CHIP_H;
            g.appendChild(el("circle", { cx: 14, cy: yy + 8, r: 5, fill: mau(it.tt) }));
            var tx = el("text", { x: 26, y: yy + 12, "font-size": 11.5, fill: "#374151" }, cat(it.ten, 24));
            var ttl = el("title", null, it.ten + (it.ip ? " — " + it.ip : "") + " [" + nhanTt(it.tt) + "]");
            tx.appendChild(ttl);
            g.appendChild(tx);
        }
        g._h = h;
        return { g: g, w: BOX_W, h: h };
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

    function render(d) {
        clear(svg);
        var core = d.core || [], pp = d.phanPhoi || [], tc = d.truyCap || [];
        var nhomBox = [];
        (d.apNhom || []).forEach(function (g) { nhomBox.push({ tieuDe: "AP · " + g.ten, items: g.items, mau: "#22d3ee" }); });
        (d.camNhom || []).forEach(function (g) { nhomBox.push({ tieuDe: "Cam · " + g.ten, items: g.items, mau: "#a78bfa" }); });

        // Bề rộng tổng = hàng rộng nhất trong các tầng
        var rongBox = nhomBox.length > 0 ? nhomBox.length * BOX_W + (nhomBox.length - 1) * BOX_GAP_X : 0;
        var tongRong = Math.max(
            rongHang(1), rongHang(core.length), rongHang(pp.length), rongHang(tc.length), rongBox, 640
        ) + PAD * 2;

        var gCanh = el("g", {}), gNode = el("g", {});
        svg.appendChild(gCanh); svg.appendChild(gNode);

        var y = PAD;
        // Tầng 0: công ty
        var ctY = y, ctCx = tongRong / 2;
        gNode.appendChild(veNode(ctCx - NODE_W / 2, ctY, d.congTy, "Công ty", "UP", null));
        y += NODE_H + GAP_Y;

        // Tầng 1: Core
        var coreRow = hangNode(core.length ? core : [{ ten: "(chưa có switch core)", ip: null, tt: "CHUA" }], y, tongRong);
        coreRow.forEach(function (r) {
            gNode.appendChild(veNode(r.x, r.top, r.node.ten, r.node.ip, r.node.tt));
            gCanh.appendChild(veCanh(ctCx, ctY + NODE_H, r.cx, r.top));
        });
        // Hub để nối các tầng dưới: core đầu tiên (hoặc công ty nếu không có core)
        var hub = coreRow.length ? coreRow[Math.floor(coreRow.length / 2)] : { cx: ctCx, bot: ctY + NODE_H };
        y += NODE_H + GAP_Y;

        // Tầng 2: Phân phối
        if (pp.length) {
            var ppRow = hangNode(pp, y, tongRong);
            ppRow.forEach(function (r) {
                gNode.appendChild(veNode(r.x, r.top, r.node.ten, r.node.ip, r.node.tt));
                gCanh.appendChild(veCanh(hub.cx, hub.bot, r.cx, r.top));
            });
            y += NODE_H + GAP_Y;
        }

        // Tầng 3: Truy cập
        if (tc.length) {
            var tcRow = hangNode(tc, y, tongRong);
            tcRow.forEach(function (r) {
                gNode.appendChild(veNode(r.x, r.top, r.node.ten, r.node.ip, r.node.tt));
                gCanh.appendChild(veCanh(hub.cx, hub.bot, r.cx, r.top));
            });
            y += NODE_H + GAP_Y;
        }

        // Tầng 4: các hộp cụm AP + Camera
        var maxBoxH = 0;
        if (nhomBox.length) {
            var startX = Math.max(PAD, (tongRong - rongBox) / 2);
            for (var i = 0; i < nhomBox.length; i++) {
                var bx = startX + i * (BOX_W + BOX_GAP_X);
                var box = veHop(bx, y, nhomBox[i].tieuDe, nhomBox[i].items, nhomBox[i].mau);
                gNode.appendChild(box.g);
                gCanh.appendChild(veCanh(hub.cx, hub.bot, bx + BOX_W / 2, y));
                if (box.h > maxBoxH) maxBoxH = box.h;
            }
            y += maxBoxH + GAP_Y;
        }

        var tongCao = y - GAP_Y + PAD;
        svg.setAttribute("width", tongRong);
        svg.setAttribute("height", tongCao);
        svg.setAttribute("viewBox", "0 0 " + tongRong + " " + tongCao);

        // Thống kê lên thanh tiêu đề
        var tk = d.thongKe || {};
        var up = (tk.sw ? tk.sw.up : 0) + (tk.ap ? tk.ap.up : 0) + (tk.cam ? tk.cam.up : 0);
        var down = (tk.sw ? tk.sw.down : 0) + (tk.ap ? tk.ap.down : 0) + (tk.cam ? tk.cam.down : 0);
        setText("sodoUp", up); setText("sodoDown", down);

        if (dangTai) dangTai.classList.add("d-none");
        if (oLoi) oLoi.classList.add("d-none");
        wrap.classList.remove("d-none");
    }

    function setText(id, v) { var e = document.getElementById(id); if (e) e.textContent = v; }

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
                render(d);
            })
            .catch(function (e) { hienLoi("Lỗi tải sơ đồ: " + e.message); });
    }

    var btn = document.getElementById("sodoLamMoi");
    if (btn) btn.addEventListener("click", tai);
    tai();
})();
