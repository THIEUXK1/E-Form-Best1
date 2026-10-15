// Biểu tượng bình luận (tin nhắn) cạnh chuông trên header các Area HR / SHD / Công việc.
// Chuông vẫn do script riêng của từng layout xử lý (gọi API với loai=khac); file này chỉ lo kênh bình luận.
// Cấu hình đọc từ data-* trên #notiChat:
//   data-api        API thông báo của Area (vd /FormHR/GetNotificationsHR)
//   data-chi-tiet   tiền tố link chi tiết đơn (vd /FormHR/ChiTiet/)
//   data-truong-id  tên trường id đơn trong JSON (vd idFormHr)
//   data-khoa-doc   tên cookie "đã đọc" dùng chung với chuông (id lịch sử là duy nhất nên không lẫn)
//   data-khoa-top   khoá localStorage nhớ id mới nhất để phát âm báo khi có bình luận mới
(function () {
    const nut = document.getElementById('notiChat');
    const drop = document.getElementById('chatDropdown');
    const list = document.getElementById('chatList');
    const badge = document.getElementById('notiChatBadge');
    if (!nut || !drop || !list) return;

    const api = nut.dataset.api;
    const chiTiet = nut.dataset.chiTiet;
    const truongId = nut.dataset.truongId;
    const khoaDoc = nut.dataset.khoaDoc;
    const khoaTop = nut.dataset.khoaTop;

    document.head.insertAdjacentHTML('beforeend', '<style>.ht-dong:hover{background:#f1f5f9}</style>');

    const amBao = new Audio('https://assets.mixkit.co/active_storage/sfx/2869/2869-preview.mp3');
    amBao.volume = 0.6;

    let lastTopId = null;
    try { lastTopId = localStorage.getItem(khoaTop); } catch (e) { }
    let dangTai = false;
    let idsDaTai = [];
    let donDaTai = [];
    const khoaHoiThoai = khoaTop + '_hoithoai';

    function docIdsDaDoc() {
        const name = khoaDoc + '=';
        const ca = document.cookie.split(';');
        for (let i = 0; i < ca.length; i++) {
            const c = ca[i].trim();
            if (c.indexOf(name) === 0) return c.substring(name.length).split(',').filter(x => x !== '');
        }
        return [];
    }

    // Chỉ giữ 300 id gần nhất: cookie quá ~4KB sẽ bị trình duyệt âm thầm từ chối ghi.
    function ghiIdsDaDoc(ids) {
        const d = new Date();
        d.setTime(d.getTime() + (30 * 24 * 60 * 60 * 1000));
        document.cookie = `${khoaDoc}=${ids.slice(-300).join(',')};expires=${d.toUTCString()};path=/`;
    }

    function escapeHtml(s) {
        const d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    // Tách Mota (định dạng do phần bình luận từng Area ghi) thành người gửi + nội dung xem trước.
    function phanTich(item) {
        const mota = (item.mota || '').trim();
        if (mota.startsWith('👤')) {
            const dong = mota.split('\n').map(x => x.trim());
            const ten = dong[0].replace(/^👤\s*/, '').replace(/\s*\([^)]*\)$/, '');
            const noiDung = (dong.find(x => x.startsWith('💬')) || '').replace(/^💬\s*/, '');
            const coFile = dong.some(x => x.startsWith('📎'));
            return { ten, noiDung: (noiDung + (coFile ? ' 📎' : '')).trim() };
        }
        let m = mota.match(/^Người dùng\s+(.+?)\s+đã bình luận:\s*([\s\S]*)$/);
        if (m) return { ten: m[1], noiDung: m[2] };
        m = mota.match(/^(.+?)\s+(đã xóa[\s\S]*)$/);
        if (m) return { ten: m[1], noiDung: m[2], xoa: true };
        return { ten: item.tieuDe || 'Bình luận', noiDung: mota };
    }

    // Tiêu đề hội thoại: "#123 (Tên đơn)"; đơn cũ thiếu TenForm thì chỉ hiện số.
    const tieuDeDon = (idDon, tenDon) => `#${idDon}${tenDon ? ` (${tenDon})` : ''}`;

    const MAU_AVATAR = ['#2563eb', '#7c3aed', '#db2777', '#059669', '#d97706', '#4f46e5', '#dc2626', '#0d9488'];

    function avatar(ten) {
        const chu = ((ten || '').trim() || '?').split(/\s+/);
        const kyTu = ((chu.length > 1 ? chu[chu.length - 2][0] : '') + chu[chu.length - 1][0]).toUpperCase();
        let h = 0;
        for (const c of ten || '') h = (h * 31 + c.charCodeAt(0)) >>> 0;
        return { kyTu, mau: MAU_AVATAR[h % MAU_AVATAR.length] };
    }

    // Gộp thông báo bình luận theo đơn: mỗi đơn là một hội thoại. API trả mới nhất trước nên phần tử đầu
    // của mỗi nhóm là tin mới nhất; thứ tự hội thoại giữ theo tin mới nhất của đơn.
    function nhomTheoDon(items, layIdDon, daDocIds) {
        const nhom = new Map();
        items.forEach(item => {
            const idDon = layIdDon(item);
            if (!nhom.has(idDon)) nhom.set(idDon, { idDon, items: [], chuaDoc: 0 });
            const n = nhom.get(idDon);
            n.items.push(item);
            if (!item.cuaToi && !daDocIds.includes(item.id.toString())) n.chuaDoc++;
        });
        return [...nhom.values()];
    }

    // Một dòng hội thoại: avatar người gửi tin cuối, tiêu đề là đơn, xem trước "Tên: nội dung", số tin chưa đọc.
    function dongHoiThoai(n, href, thuocTinh) {
        const cuoi = n.items[0];
        const p = phanTich(cuoi);
        const av = avatar(p.ten);
        const nguoi = cuoi.cuaToi ? 'Bạn' : p.ten;
        const xemTruoc = (p.xoa ? `${nguoi} ${p.noiDung}` : `${nguoi}: ${p.noiDung}`).replace(/\s+/g, ' ').trim();
        const chuaDoc = n.chuaDoc > 0;
        return `<a href="${href}" ${thuocTinh} class="ht-dong" data-don="${escapeHtml(n.idDon)}" data-chat-ids="${n.items.map(i => i.id).join(',')}" data-ghim="${n.ghim ? 1 : 0}" data-chua-doc="${chuaDoc ? 1 : 0}" style="text-decoration:none; display:flex; align-items:center; gap:12px; padding:10px 12px; margin:2px 8px; border-radius:10px; color:inherit; transition:background .15s;">
        <div style="width:48px; height:48px; border-radius:50%; background:${av.mau}; color:#fff; display:flex; align-items:center; justify-content:center; font-weight:700; font-size:0.95rem; flex-shrink:0;">${escapeHtml(av.kyTu)}</div>
        <div style="flex:1; min-width:0;">
            <div style="display:flex; justify-content:space-between; gap:8px;">
                <span style="font-size:0.92rem; font-weight:${chuaDoc ? '700' : '600'}; color:#1e293b; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">${n.ghim ? '<i class="fa fa-thumb-tack" style="color:#2563eb; margin-right:6px;"></i>' : ''}${escapeHtml(tieuDeDon(n.idDon, n.items[0].tenDon))}</span>
                <span style="flex-shrink:0; font-size:0.72rem; color:${chuaDoc ? '#2563eb' : '#94a3b8'}; font-weight:${chuaDoc ? '700' : '400'};">${escapeHtml(cuoi.time)}</span>
            </div>
            <div style="display:flex; justify-content:space-between; align-items:center; gap:8px; margin-top:2px;">
                <span style="font-size:0.82rem; color:${chuaDoc ? '#1e293b' : '#64748b'}; font-weight:${chuaDoc ? '600' : '400'}; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;${p.xoa ? ' font-style:italic;' : ''}">${escapeHtml(xemTruoc)}</span>
                ${chuaDoc ? `<span style="flex-shrink:0; min-width:20px; height:20px; padding:0 6px; border-radius:10px; background:#2563eb; color:#fff; font-size:0.7rem; font-weight:700; display:flex; align-items:center; justify-content:center;">${n.chuaDoc > 9 ? '9+' : n.chuaDoc}</span>` : ''}
            </div>
        </div>
    </a>`;
    }

    // Ghim / "đánh dấu chưa đọc" là tiện ích riêng từng người xem nên lưu localStorage (mất thì chỉ mất ghim, không ảnh hưởng dữ liệu).
    function docDanhSachLuu(khoa) {
        try { return (localStorage.getItem(khoa) || '').split(',').filter(x => x !== ''); } catch (e) { return []; }
    }

    function ghiDanhSachLuu(khoa, ds) {
        try { localStorage.setItem(khoa, ds.slice(-200).join(',')); } catch (e) { }
    }

    function doiTrongDanhSach(khoa, idDon, them) {
        const ds = docDanhSachLuu(khoa).filter(x => x !== String(idDon));
        if (them) ds.push(String(idDon));
        ghiDanhSachLuu(khoa, ds);
    }

    // Áp ghim + "chưa đọc thủ công" lên danh sách hội thoại; hội thoại ghim lên đầu, giữ thứ tự mới nhất.
    function apGhimVaChuaDoc(hoiThoai, khoa) {
        const ghim = docDanhSachLuu(khoa + '_ghim');
        const chuaDocTay = docDanhSachLuu(khoa + '_chuadoc');
        hoiThoai.forEach(n => {
            n.ghim = ghim.includes(String(n.idDon));
            n.chuaDocTay = chuaDocTay.includes(String(n.idDon));
            if (n.chuaDocTay && n.chuaDoc === 0) n.chuaDoc = 1;
        });
        return hoiThoai.filter(n => n.ghim).concat(hoiThoai.filter(n => !n.ghim));
    }

    // Menu chuột phải trên dòng hội thoại (a.ht-dong mang data-don, data-chat-ids, data-ghim, data-chua-doc).
    //   khoa        tiền tố khoá localStorage của kênh
    //   docDaDoc    () => mảng id lịch sử đã đọc
    //   ghiDaDoc    (mảng) => lưu lại
    //   veLai       () => nạp lại danh sách + badge
    function ganMenuHoiThoai(khoa, docDaDoc, ghiDaDoc, veLai) {
        const menu = document.createElement('div');
        menu.className = 'ht-menu';
        menu.style.cssText = 'display:none; position:fixed; z-index:3000; min-width:210px; background:#fff; border-radius:10px; box-shadow:0 10px 25px rgba(0,0,0,.15); border:1px solid #e2e8f0; padding:6px; font-size:0.85rem;';
        document.body.appendChild(menu);
        document.head.insertAdjacentHTML('beforeend', '<style>.ht-menu button{display:flex; align-items:center; gap:10px; width:100%; padding:8px 10px; border:none; background:none; border-radius:6px; color:#1e293b; cursor:pointer; text-align:left;}.ht-menu button:hover{background:#f1f5f9}.ht-menu i{width:16px; color:#64748b; text-align:center}</style>');

        let dongDangChon = null;
        const an = () => { menu.style.display = 'none'; dongDangChon = null; };

        document.addEventListener('contextmenu', (e) => {
            const a = e.target.closest('a.ht-dong');
            if (!a) { an(); return; }
            e.preventDefault();
            dongDangChon = a;
            const ghim = a.dataset.ghim === '1';
            const chuaDoc = a.dataset.chuaDoc === '1';
            menu.innerHTML = `<button type="button" data-lenh="ghim"><i class="fa fa-thumb-tack"></i>${ghim ? 'Bỏ ghim' : 'Ghim hội thoại'}</button>`
                + (chuaDoc
                    ? '<button type="button" data-lenh="dadoc"><i class="fa fa-check"></i>Đánh dấu là đã đọc</button>'
                    : '<button type="button" data-lenh="chuadoc"><i class="fa fa-envelope-o"></i>Đánh dấu là chưa đọc</button>');
            menu.style.display = 'block';
            // Giữ menu trong màn hình khi bấm sát mép phải/dưới.
            const x = Math.min(e.clientX, window.innerWidth - menu.offsetWidth - 8);
            const y = Math.min(e.clientY, window.innerHeight - menu.offsetHeight - 8);
            menu.style.left = x + 'px';
            menu.style.top = y + 'px';
        });

        menu.addEventListener('click', (e) => {
            // Không cho lan ra document, kẻo đóng luôn dropdown bình luận.
            e.stopPropagation();
            const nut = e.target.closest('button[data-lenh]');
            if (!nut || !dongDangChon) return;
            const idDon = dongDangChon.dataset.don;
            const lenh = nut.dataset.lenh;
            if (lenh === 'ghim') {
                doiTrongDanhSach(khoa + '_ghim', idDon, dongDangChon.dataset.ghim !== '1');
            } else if (lenh === 'dadoc') {
                const ids = docDaDoc();
                dongDangChon.dataset.chatIds.split(',').forEach(id => { if (id && !ids.includes(id)) ids.push(id); });
                ghiDaDoc(ids);
                doiTrongDanhSach(khoa + '_chuadoc', idDon, false);
            } else if (lenh === 'chuadoc') {
                doiTrongDanhSach(khoa + '_chuadoc', idDon, true);
            }
            an();
            veLai();
        });

        document.addEventListener('click', an);
        document.addEventListener('keydown', (e) => { if (e.key === 'Escape') an(); });
        window.addEventListener('scroll', an, true);
    }

    // Mở hội thoại / Đọc tất cả thì bỏ cờ "chưa đọc thủ công" của các đơn đó.
    function boChuaDocTay(khoa, dsIdDon) {
        const ds = docDanhSachLuu(khoa + '_chuadoc').filter(x => !dsIdDon.map(String).includes(x));
        ghiDanhSachLuu(khoa + '_chuadoc', ds);
    }

    function hieuUngMoi(item) {
        const tag = document.getElementById('latestNotiTag');
        const text = document.getElementById('latestNotiText');
        const p = phanTich(item);
        if (text) text.innerText = `${tieuDeDon(item[truongId], item.tenDon)} · ${p.ten}: ${p.noiDung}`.slice(0, 80);
        if (tag) tag.style.display = 'flex';
        amBao.play().catch(() => { });
        setTimeout(() => { if (tag) tag.style.display = 'none'; }, 8000);
    }

    async function tai(moDanhSach) {
        if (dangTai) return;
        dangTai = true;
        try {
            // Lấy rộng hơn 20 vì gộp theo đơn: vài đơn bình luận dày có thể chiếm hết 20 bản ghi.
            const res = await fetch(`${api}?skip=0&take=60&loai=binhluan`);
            if (!res.ok) return;
            const data = await res.json();
            const items = data.dataList || [];
            const daDoc = docIdsDaDoc();

            // Bình luận của chính mình: vẫn hiện trong danh sách nhưng không kêu, không bật nhãn, không đếm.
            const topKhac = items.find(i => !i.cuaToi);
            if (topKhac) {
                const topId = topKhac.id.toString();
                if (lastTopId !== null && topId !== lastTopId && !daDoc.includes(topId)) hieuUngMoi(topKhac);
                lastTopId = topId;
                try { localStorage.setItem(khoaTop, topId); } catch (e) { }
            }

            idsDaTai = items.map(i => i.id.toString());
            const hoiThoai = apGhimVaChuaDoc(nhomTheoDon(items, i => i[truongId], daDoc), khoaHoiThoai);
            donDaTai = hoiThoai.map(n => n.idDon);
            // Badge đếm số hội thoại (đơn) còn tin chưa đọc, không đếm từng bình luận.
            const chuaDoc = hoiThoai.filter(n => n.chuaDoc > 0).length;
            if (badge) {
                badge.innerText = chuaDoc > 99 ? '99+' : chuaDoc;
                badge.style.display = chuaDoc > 0 ? 'flex' : 'none';
            }

            if (moDanhSach) {
                list.innerHTML = hoiThoai.length === 0
                    ? '<div style="padding: 30px 20px; text-align: center; color: #94a3b8; font-size: 0.85rem;">Chưa có bình luận nào</div>'
                    : hoiThoai.map(n => dongHoiThoai(n, `${chiTiet}${encodeURIComponent(n.idDon)}`, '')).join('');
            }
        } catch (err) { } finally { dangTai = false; }
    }

    nut.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = drop.style.display === 'none' || drop.style.display === '';
        const dropChuong = document.getElementById('notificationDropdown');
        if (dropChuong) dropChuong.style.display = 'none';
        drop.style.display = isHidden ? 'flex' : 'none';
        if (isHidden) tai(true);
    });

    // Mở chuông thì đóng dropdown bình luận (script chuông chặn lan sự kiện nên phải gắn thẳng vào nút).
    const chuong = document.getElementById('notiBell');
    if (chuong) chuong.addEventListener('click', () => { drop.style.display = 'none'; });

    // Bấm ra ngoài thì đóng; bấm trong dropdown (cuộn, nút Đọc tất cả) thì giữ.
    drop.addEventListener('click', (e) => e.stopPropagation());
    document.addEventListener('click', () => { drop.style.display = 'none'; });

    list.addEventListener('click', (e) => {
        // Mở một hội thoại = đọc hết các bình luận của đơn đó.
        const a = e.target.closest('a[data-chat-ids]');
        if (!a) return;
        const ids = docIdsDaDoc();
        let doi = false;
        a.dataset.chatIds.split(',').forEach(id => { if (id && !ids.includes(id)) { ids.push(id); doi = true; } });
        if (doi) ghiIdsDaDoc(ids);
        boChuaDocTay(khoaHoiThoai, [a.dataset.don]);
    });

    const nutDocHet = document.getElementById('chatDocHet');
    if (nutDocHet) nutDocHet.addEventListener('click', (e) => {
        e.stopPropagation();
        const ids = docIdsDaDoc();
        let doi = false;
        idsDaTai.forEach(id => { if (!ids.includes(id)) { ids.push(id); doi = true; } });
        if (doi) ghiIdsDaDoc(ids);
        boChuaDocTay(khoaHoiThoai, donDaTai);
        tai(true);
    });

    ganMenuHoiThoai(khoaHoiThoai, docIdsDaDoc, ghiIdsDaDoc, () => tai(true));

    // Tab ẩn thì bỏ lượt hỏi; quay lại tab thì nạp bù ngay.
    setInterval(() => { if (document.visibilityState === 'visible') tai(false); }, 30000);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') tai(false); });
    tai(false);
})();
