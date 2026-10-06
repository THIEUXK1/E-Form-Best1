// Tab "Tổng quan" của trang Quản lý Thiết bị (/QLKiemKe/ThietBi).
// Toàn bộ số liệu tính phía client từ đúng mảng thiết bị bảng đang dùng (GetKkThietBis đã loại thiết bị bị chặn),
// nên số trên Tổng quan luôn khớp với bảng, không cần thêm API.
window.TongQuanKiemKe = (function () {
    const NGAY_MS = 86400000;
    const SAP_HET_BH_NGAY = 90;      // Bảo hành còn ≤ 90 ngày coi là sắp hết
    const KIEM_GAN_DAY_NGAY = 30;    // Đã kiểm trong 30 ngày coi là "mới kiểm"
    const MAU = ['#2a78d6', '#1baf7a', '#eda100', '#e34948', '#8b5cf6', '#0ea5e9', '#f97316', '#14b8a6', '#ec4899', '#64748b', '#84cc16', '#a16207'];
    const MAU_TRANG_THAI = { hoatDong: '#1baf7a', hong: '#e34948', baoTri: '#eda100', khoIT: '#0d9488', khac: '#64748b' };

    let duLieuGoc = [];
    let bieuDo = {};
    let congTyDangChon = '';
    let tuKhoaBang = '';
    let nhomCanhBao = 'hong';

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }
    function rong(s) { return !s || String(s).trim() === ''; }
    function tl(a, b) { return b ? Math.round(a * 1000 / b) / 10 : 0; }
    function soVN(n) { return Number(n || 0).toLocaleString('vi-VN'); }
    function ngayVN(v) {
        if (!v) return '';
        const d = new Date(v);
        return isNaN(d) ? '' : `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
    }

    // Phân nhóm trạng thái theo tên — cùng quy tắc với getStatusColor của bảng để màu khớp nhau
    function nhomTrangThai(t) {
        t = (t || '').toLowerCase();
        if (t.includes('hoạt động')) return 'hoatDong';
        if (t.includes('hỏng')) return 'hong';
        if (t.includes('bảo trì')) return 'baoTri';
        if (t.trim() === 'kho it') return 'khoIT';
        return 'khac';
    }

    function soNgayTu(v, homNay) {
        if (!v) return null;
        const d = new Date(v);
        return isNaN(d) ? null : Math.floor((homNay - d) / NGAY_MS);
    }

    // Gắn sẵn các cờ tính toán cho từng thiết bị để các phần bên dưới chỉ việc đếm
    function chuanHoa(ds) {
        const homNay = new Date(); homNay.setHours(0, 0, 0, 0);
        return ds.map(x => {
            const ngayKiem = soNgayTu(x.thoiGianCheck, homNay);
            let conBH = null;
            if (x.hanBaoHanh) {
                const h = new Date(x.hanBaoHanh);
                if (!isNaN(h)) conBH = Math.floor((h - homNay) / NGAY_MS);
            }
            return {
                goc: x,
                nhomTT: nhomTrangThai(x.tenTrangThai),
                ngayKiem: ngayKiem,
                conBH: conBH,
                congTy: rong(x.tenCongTy) ? 'Chưa xác định' : x.tenCongTy.trim(),
                boPhan: rong(x.tenBoPhan) ? 'Chưa xác định' : x.tenBoPhan.trim(),
                loai: rong(x.loaiThietBi) ? 'Chưa rõ' : x.loaiThietBi.trim(),
                viTriDiaLy: rong(x.tenViTriDiaLy) ? 'Chưa khai báo' : x.tenViTriDiaLy.trim(),
                win: rong(x.winLicense) ? 'Trống / Chưa rõ' : x.winLicense.trim(),
                office: rong(x.officeLicense) ? 'Trống / Chưa rõ' : x.officeLicense.trim(),
                version: rong(x.version) ? 'Chưa có agent' : x.version.trim(),
                coNguoiDung: !!x.idNguoiDung,
                thieuHost: rong(x.tenMayTinh),
                thieuSerial: rong(x.seribacode),
                coAgent: x.idMay != null,
                loiAgent: !rong(x.error)
            };
        });
    }

    function demTheo(ds, f) {
        const m = new Map();
        ds.forEach(x => { const k = f(x); m.set(k, (m.get(k) || 0) + 1); });
        return [...m.entries()].sort((a, b) => b[1] - a[1]);
    }

    function mauChu() {
        return getComputedStyle(document.documentElement).getPropertyValue('--chu-mo').trim() || '#52514e';
    }
    function mauLuoi() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'rgba(157,176,204,0.18)' : '#e1e0d9';
    }

    function veBieuDo(id, cauHinh) {
        if (bieuDo[id]) { bieuDo[id].destroy(); delete bieuDo[id]; }
        const el = document.getElementById(id);
        if (!el || typeof Chart === 'undefined') return;
        bieuDo[id] = new Chart(el.getContext('2d'), cauHinh);
    }

    function cauHinhCot(nhan, giaTri, mau, ngang, tenSeries) {
        return {
            type: 'bar',
            data: { labels: nhan, datasets: [{ label: tenSeries || 'Số thiết bị', data: giaTri, backgroundColor: mau || '#2a78d6', borderRadius: 4, maxBarThickness: 34 }] },
            options: {
                indexAxis: ngang ? 'y' : 'x',
                responsive: true, maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { beginAtZero: true, grid: { display: !!ngang, color: mauLuoi() }, ticks: { color: mauChu(), precision: 0 } },
                    y: { beginAtZero: true, grid: { display: !ngang, color: mauLuoi() }, ticks: { color: mauChu(), precision: 0, autoSkip: false } }
                }
            }
        };
    }

    function cauHinhTron(nhan, giaTri, mau) {
        return {
            type: 'doughnut',
            data: { labels: nhan, datasets: [{ data: giaTri, backgroundColor: mau, borderColor: 'rgba(255,255,255,0.9)', borderWidth: 2 }] },
            options: {
                responsive: true, maintainAspectRatio: false, cutout: '58%',
                plugins: {
                    legend: { position: 'right', labels: { color: mauChu(), boxWidth: 12, font: { weight: '600' } } },
                    tooltip: {
                        callbacks: {
                            label: ctx => {
                                const tong = ctx.dataset.data.reduce((a, b) => a + b, 0);
                                return ` ${ctx.label}: ${soVN(ctx.parsed)} (${tl(ctx.parsed, tong)}%)`;
                            }
                        }
                    }
                }
            }
        };
    }

    // ---------------- Khung HTML ----------------
    function khungHtml() {
        const the = (id, tieuDe, icon, cao, lop) => `
            <div class="${lop || 'col-xl-4 col-lg-6'}">
                <div class="tq-the h-100">
                    <div class="tq-the-tieude"><i class="${icon} me-1"></i>${tieuDe}</div>
                    <div class="tq-the-than" style="height:${cao || 280}px"><canvas id="${id}"></canvas></div>
                </div>
            </div>`;
        return `
            <div class="tq-thanh-cong-cu">
                <div class="d-flex align-items-center gap-2 flex-wrap">
                    <label for="tqCongTy" class="small fw-bold mb-0 tq-mo">Phạm vi:</label>
                    <select id="tqCongTy" class="form-select form-select-sm" style="width:auto;min-width:200px"></select>
                    <span class="small tq-mo" id="tqThoiDiem"></span>
                </div>
                <div class="small tq-mo">Không tính thiết bị trong thùng rác và thiết bị bị chặn.</div>
            </div>
            <div class="row g-3 mb-3" id="tqKpi"></div>
            <div class="row g-3 mb-3" id="tqTienDo"></div>
            <div class="row g-3 mb-3">
                ${the('tqChartLoaiTT', 'Loại thiết bị theo Trạng thái', 'fas fa-layer-group text-primary', 320, 'col-xl-8')}
                ${the('tqChartTrangThai', 'Tỷ lệ Trạng thái', 'fas fa-chart-pie text-success', 320, 'col-xl-4')}
                ${the('tqChartCongTy', 'Theo Công ty', 'fas fa-building text-info')}
                ${the('tqChartKiem', 'Lần kiểm tra gần nhất', 'fas fa-clipboard-check text-success')}
                ${the('tqChartBaoHanh', 'Tình trạng Bảo hành', 'fas fa-shield-alt text-warning')}
                ${the('tqChartBoPhan', 'Top 15 Bộ phận nhiều thiết bị nhất', 'fas fa-sitemap text-primary', 400, 'col-xl-6')}
                ${the('tqChartViTri', 'Theo Vị trí địa lý', 'fas fa-map-marker-alt text-danger', 400, 'col-xl-6')}
                ${the('tqChartWin', 'Bản quyền Windows', 'fab fa-windows text-primary')}
                ${the('tqChartOffice', 'Bản quyền Office', 'fas fa-file-word text-info')}
                ${the('tqChartVersion', 'Phiên bản Agent', 'fas fa-robot text-secondary')}
                ${the('tqChartThang', 'Thiết bị thêm mới & lượt kiểm gần nhất — 12 tháng', 'fas fa-chart-line text-primary', 300, 'col-12')}
            </div>
            <div class="tq-the mb-3">
                <div class="tq-the-tieude d-flex justify-content-between align-items-center flex-wrap gap-2">
                    <span><i class="fas fa-table text-primary me-1"></i>Chi tiết theo Bộ phận</span>
                    <input type="search" id="tqTimBoPhan" class="form-control form-control-sm" style="max-width:240px" placeholder="Lọc bộ phận / công ty..." />
                </div>
                <div class="table-responsive tq-bang-wrap"><table class="table table-sm table-hover align-middle mb-0 tq-bang" id="tqBangBoPhan"></table></div>
            </div>
            <div class="tq-the mb-3">
                <div class="tq-the-tieude"><i class="fas fa-exclamation-triangle text-danger me-1"></i>Thiết bị cần chú ý</div>
                <div class="d-flex flex-wrap gap-2 px-3 pt-2" id="tqNhomCanhBao"></div>
                <div class="table-responsive tq-bang-wrap"><table class="table table-sm table-hover align-middle mb-0 tq-bang" id="tqBangCanhBao"></table></div>
            </div>`;
    }

    // ---------------- KPI ----------------
    function oKpi(giaTri, nhan, phu, icon, mau) {
        return `
            <div class="col-xxl-2 col-xl-3 col-md-4 col-6">
                <div class="tq-kpi" style="--tq-mau:${mau}">
                    <div class="tq-kpi-icon"><i class="${icon}"></i></div>
                    <div class="tq-kpi-so">${giaTri}</div>
                    <div class="tq-kpi-nhan">${nhan}</div>
                    ${phu ? `<div class="tq-kpi-phu">${phu}</div>` : ''}
                </div>
            </div>`;
    }

    function veKpi(ds) {
        const n = ds.length;
        const c = f => ds.filter(f).length;
        const hd = c(x => x.nhomTT === 'hoatDong'), hong = c(x => x.nhomTT === 'hong'), bt = c(x => x.nhomTT === 'baoTri'), khoIT = c(x => x.nhomTT === 'khoIT'), khac = c(x => x.nhomTT === 'khac');
        const coND = c(x => x.coNguoiDung);
        const kiem30 = c(x => x.ngayKiem != null && x.ngayKiem <= KIEM_GAN_DAY_NGAY);
        const chuaKiem = c(x => x.ngayKiem == null);
        const hetBH = c(x => x.conBH != null && x.conBH < 0);
        const sapHetBH = c(x => x.conBH != null && x.conBH >= 0 && x.conBH <= SAP_HET_BH_NGAY);
        const khongBH = c(x => x.conBH == null);
        const thieuHost = c(x => x.thieuHost), thieuSerial = c(x => x.thieuSerial);
        const coAgent = c(x => x.coAgent), loiAgent = c(x => x.loiAgent);
        const canOffice = c(x => x.goc.canCaiOffice === true);
        const chuaTraLoiOffice = c(x => x.goc.canCaiOffice == null);
        const soBoPhan = new Set(ds.map(x => x.congTy + '|' + x.boPhan)).size;
        const soLoai = new Set(ds.map(x => x.loai)).size;

        $('#tqKpi').html([
            oKpi(soVN(n), 'Tổng thiết bị', `${soBoPhan} bộ phận · ${soLoai} loại`, 'fas fa-boxes', '#4f46e5'),
            oKpi(soVN(hd), 'Đang hoạt động', `${tl(hd, n)}% tổng số`, 'fas fa-check-circle', MAU_TRANG_THAI.hoatDong),
            oKpi(soVN(hong), 'Hỏng / Cần sửa', `${tl(hong, n)}% tổng số`, 'fas fa-tools', MAU_TRANG_THAI.hong),
            oKpi(soVN(bt), 'Đang bảo trì', `Kho IT: ${soVN(khoIT)} · khác: ${soVN(khac)}`, 'fas fa-wrench', MAU_TRANG_THAI.baoTri),
            oKpi(soVN(coND), 'Đã cấp phát người dùng', `Chưa cấp phát: ${soVN(n - coND)}`, 'fas fa-user-check', '#0ea5e9'),
            oKpi(soVN(kiem30), `Kiểm trong ${KIEM_GAN_DAY_NGAY} ngày`, `${tl(kiem30, n)}% · chưa kiểm lần nào: ${soVN(chuaKiem)}`, 'fas fa-clipboard-check', '#14b8a6'),
            oKpi(soVN(hetBH), 'Hết hạn bảo hành', `Sắp hết (≤${SAP_HET_BH_NGAY} ngày): ${soVN(sapHetBH)}`, 'fas fa-shield-alt', '#f97316'),
            oKpi(soVN(khongBH), 'Chưa khai hạn BH', `${tl(khongBH, n)}% tổng số`, 'fas fa-question-circle', '#94a3b8'),
            oKpi(soVN(coAgent), 'Có agent kết nối', `${tl(coAgent, n)}% · agent báo lỗi: ${soVN(loiAgent)}`, 'fas fa-robot', '#8b5cf6'),
            oKpi(soVN(thieuHost), 'Thiếu Hostname', `Thiếu Serial: ${soVN(thieuSerial)}`, 'fas fa-desktop', '#e34948'),
            oKpi(soVN(canOffice), 'Cần cài Office', `Chưa trả lời: ${soVN(chuaTraLoiOffice)}`, 'fas fa-file-word', '#2a78d6'),
            oKpi(soVN(c(x => !rong(x.goc.ip))), 'Đã có địa chỉ IP', `Chưa có IP: ${soVN(c(x => rong(x.goc.ip)))}`, 'fas fa-network-wired', '#64748b')
        ].join(''));

        // Thanh tiến độ: nhìn nhanh mức độ "đầy đủ" của dữ liệu
        const thanh = (nhan, so, mau) => `
            <div class="col-xl-3 col-md-6">
                <div class="tq-tiendo">
                    <div class="d-flex justify-content-between small fw-bold mb-1"><span>${nhan}</span><span>${soVN(so)}/${soVN(n)} · ${tl(so, n)}%</span></div>
                    <div class="progress" style="height:8px"><div class="progress-bar" style="width:${tl(so, n)}%;background:${mau}"></div></div>
                </div>
            </div>`;
        $('#tqTienDo').html([
            thanh(`Đã kiểm trong ${KIEM_GAN_DAY_NGAY} ngày`, kiem30, '#14b8a6'),
            thanh('Đã cấp phát người dùng', coND, '#0ea5e9'),
            thanh('Có hạn bảo hành còn hiệu lực', c(x => x.conBH != null && x.conBH >= 0), '#f97316'),
            thanh('Đủ Hostname + Serial', c(x => !x.thieuHost && !x.thieuSerial), '#4f46e5')
        ].join(''));
    }

    // ---------------- Biểu đồ ----------------
    function veCacBieuDo(ds) {
        // Loại × Trạng thái (cột chồng ngang)
        const dsLoai = demTheo(ds, x => x.loai).map(e => e[0]);
        const nhomTT = [['hoatDong', 'Hoạt động'], ['hong', 'Hỏng'], ['baoTri', 'Bảo trì'], ['khoIT', 'Kho IT'], ['khac', 'Khác']];
        veBieuDo('tqChartLoaiTT', {
            type: 'bar',
            data: {
                labels: dsLoai,
                datasets: nhomTT.map(([k, ten]) => ({
                    label: ten, backgroundColor: MAU_TRANG_THAI[k], borderRadius: 3, maxBarThickness: 26,
                    data: dsLoai.map(l => ds.filter(x => x.loai === l && x.nhomTT === k).length)
                }))
            },
            options: {
                indexAxis: 'y', responsive: true, maintainAspectRatio: false,
                plugins: { legend: { position: 'top', labels: { color: mauChu(), boxWidth: 12 } } },
                scales: {
                    x: { stacked: true, beginAtZero: true, grid: { color: mauLuoi() }, ticks: { color: mauChu(), precision: 0 } },
                    y: { stacked: true, grid: { display: false }, ticks: { color: mauChu(), autoSkip: false } }
                }
            }
        });

        // Trạng thái theo đúng tên trạng thái trong danh mục
        const tt = demTheo(ds, x => rong(x.goc.tenTrangThai) ? 'Chưa rõ' : x.goc.tenTrangThai);
        veBieuDo('tqChartTrangThai', cauHinhTron(tt.map(e => e[0]), tt.map(e => e[1]), tt.map(e => MAU_TRANG_THAI[nhomTrangThai(e[0])])));

        const ct = demTheo(ds, x => x.congTy);
        veBieuDo('tqChartCongTy', cauHinhCot(ct.map(e => e[0]), ct.map(e => e[1]), '#0ea5e9'));

        // Tuổi lần kiểm gần nhất
        const moc = [
            ['≤ 30 ngày', x => x.ngayKiem != null && x.ngayKiem <= 30, '#1baf7a'],
            ['31 – 90 ngày', x => x.ngayKiem > 30 && x.ngayKiem <= 90, '#84cc16'],
            ['91 – 180 ngày', x => x.ngayKiem > 90 && x.ngayKiem <= 180, '#eda100'],
            ['> 180 ngày', x => x.ngayKiem > 180, '#f97316'],
            ['Chưa kiểm lần nào', x => x.ngayKiem == null, '#e34948']
        ];
        veBieuDo('tqChartKiem', cauHinhTron(moc.map(m => m[0]), moc.map(m => ds.filter(m[1]).length), moc.map(m => m[2])));

        const bh = [
            ['Còn > 1 năm', x => x.conBH > 365, '#1baf7a'],
            [`Còn ${SAP_HET_BH_NGAY + 1} ngày – 1 năm`, x => x.conBH > SAP_HET_BH_NGAY && x.conBH <= 365, '#84cc16'],
            [`Sắp hết (≤ ${SAP_HET_BH_NGAY} ngày)`, x => x.conBH != null && x.conBH >= 0 && x.conBH <= SAP_HET_BH_NGAY, '#eda100'],
            ['Đã hết hạn', x => x.conBH != null && x.conBH < 0, '#e34948'],
            ['Chưa khai báo', x => x.conBH == null, '#94a3b8']
        ];
        veBieuDo('tqChartBaoHanh', cauHinhTron(bh.map(m => m[0]), bh.map(m => ds.filter(m[1]).length), bh.map(m => m[2])));

        const bp = demTheo(ds, x => x.boPhan).slice(0, 15);
        veBieuDo('tqChartBoPhan', cauHinhCot(bp.map(e => e[0]), bp.map(e => e[1]), '#4f46e5', true));

        const vt = demTheo(ds, x => x.viTriDiaLy);
        veBieuDo('tqChartViTri', cauHinhCot(vt.map(e => e[0]), vt.map(e => e[1]), '#e34948', true));

        const win = demTheo(ds, x => x.win);
        veBieuDo('tqChartWin', cauHinhCot(win.map(e => e[0]), win.map(e => e[1]), win.map((_, i) => MAU[i % MAU.length])));
        const off = demTheo(ds, x => x.office);
        veBieuDo('tqChartOffice', cauHinhCot(off.map(e => e[0]), off.map(e => e[1]), off.map((_, i) => MAU[i % MAU.length])));
        const ver = demTheo(ds, x => x.version).slice(0, 10);
        veBieuDo('tqChartVersion', cauHinhCot(ver.map(e => e[0]), ver.map(e => e[1]), '#8b5cf6', true));

        // 12 tháng gần nhất: số thiết bị thêm mới (NgayTao) và số thiết bị có lần kiểm gần nhất rơi vào tháng đó
        const thang = [];
        const now = new Date();
        for (let i = 11; i >= 0; i--) {
            const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
            thang.push({ khoa: d.getFullYear() * 100 + d.getMonth(), nhan: `${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}` });
        }
        const khoaThang = v => { if (!v) return null; const d = new Date(v); return isNaN(d) ? null : d.getFullYear() * 100 + d.getMonth(); };
        const dem = f => thang.map(t => ds.filter(x => khoaThang(f(x)) === t.khoa).length);
        veBieuDo('tqChartThang', {
            type: 'line',
            data: {
                labels: thang.map(t => t.nhan),
                datasets: [
                    { label: 'Thêm mới', data: dem(x => x.goc.ngayTao), borderColor: '#2a78d6', backgroundColor: 'rgba(42,120,214,0.12)', fill: true, tension: 0.3, pointRadius: 3 },
                    { label: 'Lần kiểm gần nhất', data: dem(x => x.goc.thoiGianCheck), borderColor: '#1baf7a', backgroundColor: 'rgba(27,175,122,0.10)', fill: true, tension: 0.3, pointRadius: 3 }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { labels: { color: mauChu(), boxWidth: 12 } } },
                scales: {
                    x: { grid: { display: false }, ticks: { color: mauChu() } },
                    y: { beginAtZero: true, grid: { color: mauLuoi() }, ticks: { color: mauChu(), precision: 0 } }
                }
            }
        });
    }

    // ---------------- Bảng theo Bộ phận ----------------
    function veBangBoPhan(ds) {
        const cotLoai = demTheo(ds, x => x.loai).slice(0, 6).map(e => e[0]);
        const nhom = new Map();
        ds.forEach(x => {
            const k = x.congTy + '|' + x.boPhan;
            if (!nhom.has(k)) nhom.set(k, { congTy: x.congTy, boPhan: x.boPhan, ds: [] });
            nhom.get(k).ds.push(x);
        });
        let hang = [...nhom.values()].sort((a, b) => b.ds.length - a.ds.length);
        if (tuKhoaBang) hang = hang.filter(h => (h.boPhan + ' ' + h.congTy).toLowerCase().includes(tuKhoaBang));

        const dong = (ten, phu, d, lop) => {
            const n = d.length, c = f => d.filter(f).length;
            const loaiKhac = n - cotLoai.reduce((s, l) => s + c(x => x.loai === l), 0);
            const kiem = c(x => x.ngayKiem != null && x.ngayKiem <= KIEM_GAN_DAY_NGAY);
            const pk = tl(kiem, n);
            const mauPk = pk >= 80 ? '#1baf7a' : pk >= 50 ? '#eda100' : '#e34948';
            const o = v => `<td class="text-end">${v ? soVN(v) : '<span class="tq-mo">–</span>'}</td>`;
            return `<tr class="${lop || ''}">
                <td><div class="fw-bold">${esc(ten)}</div>${phu ? `<div class="small tq-mo">${esc(phu)}</div>` : ''}</td>
                <td class="text-end fw-bold">${soVN(n)}</td>
                ${cotLoai.map(l => o(c(x => x.loai === l))).join('')}${o(loaiKhac)}
                ${o(c(x => x.nhomTT === 'hoatDong'))}${o(c(x => x.nhomTT === 'hong'))}${o(c(x => !x.coNguoiDung))}
                ${o(c(x => x.conBH != null && x.conBH < 0))}${o(c(x => x.ngayKiem == null))}
                <td style="min-width:130px"><div class="d-flex align-items-center gap-2">
                    <div class="progress flex-grow-1" style="height:6px"><div class="progress-bar" style="width:${pk}%;background:${mauPk}"></div></div>
                    <span class="small fw-bold" style="color:${mauPk}">${pk}%</span></div></td>
            </tr>`;
        };

        const dau = `<thead><tr>
            <th>Bộ phận</th><th class="text-end">Tổng</th>
            ${cotLoai.map(l => `<th class="text-end">${esc(l)}</th>`).join('')}<th class="text-end">Loại khác</th>
            <th class="text-end">Hoạt động</th><th class="text-end">Hỏng</th><th class="text-end">Chưa cấp phát</th>
            <th class="text-end">Hết BH</th><th class="text-end">Chưa kiểm</th><th>Kiểm ≤ ${KIEM_GAN_DAY_NGAY} ngày</th>
        </tr></thead>`;
        const soCot = 10 + cotLoai.length;
        const than = hang.length
            ? hang.map(h => dong(h.boPhan, h.congTy, h.ds)).join('') + dong('Tổng cộng', `${hang.length} bộ phận`, hang.flatMap(h => h.ds), 'tq-dong-tong')
            : `<tr><td colspan="${soCot}" class="text-center tq-mo py-4">Không có bộ phận nào khớp.</td></tr>`;
        $('#tqBangBoPhan').html(dau + '<tbody>' + than + '</tbody>');
    }

    // ---------------- Thiết bị cần chú ý ----------------
    const NHOM_CANH_BAO = [
        { k: 'hong', ten: 'Hỏng / Cần sửa', loc: x => x.nhomTT === 'hong', mau: '#e34948' },
        { k: 'khoIT', ten: 'Đang ở Kho IT', loc: x => x.nhomTT === 'khoIT', mau: '#0d9488' },
        { k: 'hetBH', ten: 'Hết hạn bảo hành', loc: x => x.conBH != null && x.conBH < 0, mau: '#f97316' },
        { k: 'sapHetBH', ten: `Sắp hết BH (≤${SAP_HET_BH_NGAY} ngày)`, loc: x => x.conBH != null && x.conBH >= 0 && x.conBH <= SAP_HET_BH_NGAY, mau: '#eda100' },
        { k: 'lauChuaKiem', ten: 'Chưa kiểm > 180 ngày', loc: x => x.ngayKiem > 180, mau: '#a16207' },
        { k: 'chuaKiem', ten: 'Chưa kiểm lần nào', loc: x => x.ngayKiem == null, mau: '#64748b' },
        { k: 'loiAgent', ten: 'Agent báo lỗi', loc: x => x.loiAgent, mau: '#8b5cf6' },
        { k: 'thieuTT', ten: 'Thiếu Hostname / Serial', loc: x => x.thieuHost || x.thieuSerial, mau: '#0ea5e9' },
        { k: 'chuaCapPhat', ten: 'Chưa cấp phát người dùng', loc: x => !x.coNguoiDung, mau: '#4f46e5' }
    ];
    const GIOI_HAN_DONG = 200;

    function veCanhBao(ds) {
        $('#tqNhomCanhBao').html(NHOM_CANH_BAO.map(g => {
            const so = ds.filter(g.loc).length;
            return `<button type="button" class="tq-chip ${g.k === nhomCanhBao ? 'is-active' : ''}" data-tq-nhom="${g.k}" style="--tq-mau:${g.mau}">
                ${g.ten} <span class="tq-chip-so">${soVN(so)}</span></button>`;
        }).join(''));

        const g = NHOM_CANH_BAO.find(x => x.k === nhomCanhBao) || NHOM_CANH_BAO[0];
        let d = ds.filter(g.loc);
        // Sắp xếp để việc gấp nhất lên đầu
        if (g.k === 'hetBH' || g.k === 'sapHetBH') d.sort((a, b) => a.conBH - b.conBH);
        else if (g.k === 'lauChuaKiem') d.sort((a, b) => b.ngayKiem - a.ngayKiem);
        else d.sort((a, b) => (a.congTy + a.boPhan).localeCompare(b.congTy + b.boPhan, 'vi'));

        const tong = d.length;
        d = d.slice(0, GIOI_HAN_DONG);
        const dau = `<thead><tr><th>ID</th><th>Hostname / Serial</th><th>Loại</th><th>Công ty / Bộ phận</th><th>Người dùng</th><th>Trạng thái</th><th>Kiểm gần nhất</th><th>Hạn BH</th><th>Ghi chú</th><th></th></tr></thead>`;
        const than = d.length ? d.map(x => {
            const t = x.goc;
            const kiem = x.ngayKiem == null ? '<span class="text-danger small fw-bold">Chưa kiểm</span>' : `${ngayVN(t.thoiGianCheck)}<div class="small tq-mo">${x.ngayKiem} ngày trước</div>`;
            const bhTxt = x.conBH == null ? '<span class="tq-mo">–</span>'
                : `${ngayVN(t.hanBaoHanh)}<div class="small ${x.conBH < 0 ? 'text-danger' : x.conBH <= SAP_HET_BH_NGAY ? 'text-warning' : 'tq-mo'}">${x.conBH < 0 ? `quá ${-x.conBH} ngày` : `còn ${x.conBH} ngày`}</div>`;
            const ghiChu = g.k === 'loiAgent' ? t.error : t.ghiChu;
            return `<tr>
                <td class="tq-mo">#${t.idThietBi}</td>
                <td><div class="fw-bold">${x.thieuHost ? '<span class="text-danger">(thiếu hostname)</span>' : esc(t.tenMayTinh)}</div><div class="small tq-mo">${x.thieuSerial ? '<span class="text-danger">(thiếu serial)</span>' : esc(t.seribacode)}</div></td>
                <td>${esc(x.loai)}</td>
                <td><div>${esc(x.boPhan)}</div><div class="small tq-mo">${esc(x.congTy)}</div></td>
                <td>${t.tenNguoiDung ? `${esc(t.tenNguoiDung)}<div class="small tq-mo">${esc(t.tk || '')}</div>` : '<span class="tq-mo">Chưa cấp phát</span>'}</td>
                <td><span class="badge" style="background:${MAU_TRANG_THAI[x.nhomTT]}">${esc(t.tenTrangThai || 'Chưa rõ')}</span></td>
                <td>${kiem}</td><td>${bhTxt}</td>
                <td class="small" style="max-width:260px;white-space:normal">${esc(ghiChu || '')}</td>
                <td class="text-end"><button type="button" class="btn btn-sm btn-outline-primary" data-tq-sua="${t.idThietBi}" title="Mở thiết bị"><i class="fas fa-pen"></i></button></td>
            </tr>`;
        }).join('') : `<tr><td colspan="10" class="text-center tq-mo py-4"><i class="fas fa-check-circle text-success me-1"></i>Không có thiết bị nào trong nhóm này.</td></tr>`;
        const chan = tong > GIOI_HAN_DONG ? `<tfoot><tr><td colspan="10" class="text-center small tq-mo">Đang hiện ${GIOI_HAN_DONG}/${soVN(tong)} thiết bị — dùng bộ lọc ở tab Thiết bị để xem đủ.</td></tr></tfoot>` : '';
        $('#tqBangCanhBao').html(dau + '<tbody>' + than + '</tbody>' + chan);
    }

    // ---------------- Điều phối ----------------
    function veLai() {
        const ds = congTyDangChon ? duLieuGoc.filter(x => x.congTy === congTyDangChon) : duLieuGoc;
        veKpi(ds);
        veCacBieuDo(ds);
        veBangBoPhan(ds);
        veCanhBao(ds);
    }

    function render(dsThietBi) {
        const $goc = $('#tqTongQuan');
        if (!$goc.length) return;
        if (!$goc.data('daDung')) { $goc.html(khungHtml()).data('daDung', true); }

        const conHieuLuc = (dsThietBi || []).filter(x => !(x.tenTrangThai && x.tenTrangThai.toLowerCase().includes('xóa')));
        duLieuGoc = chuanHoa(conHieuLuc);

        const dsCongTy = demTheo(duLieuGoc, x => x.congTy);
        if (congTyDangChon && !dsCongTy.some(e => e[0] === congTyDangChon)) congTyDangChon = '';
        const $sel = $('#tqCongTy').empty().append($('<option>').val('').text(`Tất cả công ty (${soVN(duLieuGoc.length)})`));
        dsCongTy.forEach(([ten, so]) => $sel.append($('<option>').val(ten).text(`${ten} (${soVN(so)})`)));
        $sel.val(congTyDangChon);

        const now = new Date();
        $('#tqThoiDiem').text(`Số liệu lúc ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')} ${ngayVN(now)}`);
        veLai();
    }

    $(document).on('change', '#tqCongTy', function () { congTyDangChon = $(this).val() || ''; veLai(); });
    $(document).on('input', '#tqTimBoPhan', function () {
        tuKhoaBang = ($(this).val() || '').toLowerCase().trim();
        const ds = congTyDangChon ? duLieuGoc.filter(x => x.congTy === congTyDangChon) : duLieuGoc;
        veBangBoPhan(ds);
    });
    $(document).on('click', '[data-tq-nhom]', function () {
        nhomCanhBao = $(this).data('tq-nhom');
        const ds = congTyDangChon ? duLieuGoc.filter(x => x.congTy === congTyDangChon) : duLieuGoc;
        veCanhBao(ds);
    });
    // Mở modal sửa của bảng thiết bị (editThietBi định nghĩa trong IndexThietBi.cshtml)
    $(document).on('click', '[data-tq-sua]', function () {
        const id = Number($(this).data('tq-sua'));
        const tb = duLieuGoc.find(x => x.goc.idThietBi === id);
        if (tb && typeof window.editThietBi === 'function') window.editThietBi(tb.goc);
    });

    return { render: render };
})();
