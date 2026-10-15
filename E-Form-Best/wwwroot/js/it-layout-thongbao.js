// Chuông thông báo + biểu tượng bình luận + dropdown tài khoản trên header của Area ITForm.
// markAsRead / markAllAsRead được gọi từ onclick trong HTML nên phải ở phạm vi toàn cục.

const notiSound = new Audio('https://assets.mixkit.co/active_storage/sfx/2869/2869-preview.mp3');
notiSound.volume = 0.6;

const take = 20;

// Bình luận tách khỏi chuông sang biểu tượng tin nhắn riêng; hai kênh dùng chung kho "đã đọc"
// (id LichSuFormIt là duy nhất) nhưng nạp, đếm, phát âm báo độc lập.
const kenhThongBao = {
    khac: {
        nut: document.getElementById('notiBell'),
        drop: document.getElementById('notificationDropdown'),
        list: document.getElementById('notiList'),
        badge: document.getElementById('notiBadge'),
        khoaTopId: 'lastNotifiedId',
        iconMacDinh: 'fa-bell'
    },
    binhluan: {
        nut: document.getElementById('notiChat'),
        drop: document.getElementById('chatDropdown'),
        list: document.getElementById('chatList'),
        badge: document.getElementById('notiChatBadge'),
        khoaTopId: 'lastNotifiedIdBinhLuan',
        iconMacDinh: 'fa-commenting-o'
    }
};

Object.keys(kenhThongBao).forEach(loai => {
    const k = kenhThongBao[loai];
    k.lastTopId = localStorage.getItem(k.khoaTopId) || null;
    k.skip = 0;
    k.isLoading = false;
    k.allFetchedIds = [];

    if (k.nut && k.drop) {
        k.nut.addEventListener('click', (e) => {
            e.stopPropagation();
            const isHidden = k.drop.style.display === 'none' || k.drop.style.display === '';
            // Mở kênh này thì đóng kênh kia, tránh hai dropdown chồng lên nhau.
            Object.values(kenhThongBao).forEach(o => { if (o !== k && o.drop) o.drop.style.display = 'none'; });
            k.drop.style.display = isHidden ? 'flex' : 'none';
            if (isHidden) loadNotifications(loai, true);
        });
    }
});

// Bấm ra ngoài thì form-layout-chung.js đóng notificationDropdown; dropdown bình luận đóng ở đây.
document.addEventListener('click', () => {
    const d = kenhThongBao.binhluan.drop;
    if (d) d.style.display = 'none';
});

const userBtn = document.getElementById('userMenuButton');
const userDrop = document.getElementById('userDropdown');
const userArrow = document.getElementById('arrowIcon');

if (userBtn && userDrop) {
    userBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = userDrop.style.display === 'none' || userDrop.style.display === '';
        userDrop.style.display = isHidden ? 'flex' : 'none';
        if (userArrow) userArrow.style.transform = isHidden ? 'rotate(180deg)' : 'rotate(0deg)';
    });
}

function escapeHtml(s) {
    const d = document.createElement('div');
    d.textContent = s == null ? '' : String(s);
    return d.innerHTML;
}

// Kênh bình luận gộp theo đơn: mỗi đơn là một hội thoại (cùng cách với binh-luan-header.js của các Area khác).
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

const KHOA_HOI_THOAI_IT = 'binhLuanIT_hoithoai';
ganMenuHoiThoai(KHOA_HOI_THOAI_IT, getReadIds, setReadCookie, () => loadNotifications('binhluan', true));

// Mở một hội thoại: đọc hết bình luận của đơn và bỏ cờ "chưa đọc thủ công".
function moHoiThoai(idDon, ids) {
    boChuaDocTay(KHOA_HOI_THOAI_IT, [idDon]);
    markAsRead(ids);
}

document.head.insertAdjacentHTML('beforeend', '<style>.ht-dong:hover{background:#f1f5f9}</style>');

async function loadNotifications(loai, isOpen = false) {
    const k = kenhThongBao[loai];
    if (!k || k.isLoading) return;
    const readIdsFromCookie = getReadIds();
    k.skip = 0;
    try {
        k.isLoading = true;
        // Bình luận lấy rộng hơn vì gộp theo đơn: vài đơn bình luận dày có thể chiếm hết 20 bản ghi.
        const res = await fetch(`/FormIT/GetNotifications?skip=${k.skip}&take=${loai === 'binhluan' ? 60 : take}&loai=${loai}`);
        if (!res.ok) return;
        const data = await res.json();
        const listItems = data.top5 || data.dataList || [];

        // Bình luận của chính mình (cuaToi): vẫn hiện trong danh sách nhưng không kêu, không bật nhãn, không đếm.
        const topKhac = listItems.find(item => !item.cuaToi);
        if (topKhac) {
            const currentTopId = topKhac.id.toString();
            if (k.lastTopId !== null && currentTopId !== k.lastTopId && !readIdsFromCookie.includes(currentTopId)) {
                if (loai === 'binhluan') {
                    const p = phanTich(topKhac);
                    triggerNewNotificationEffect(topKhac, `${tieuDeDon(topKhac.idFormIt, topKhac.tenDon)} · ${p.ten}: ${p.noiDung}`.slice(0, 80));
                } else triggerNewNotificationEffect(topKhac);
            }
            k.lastTopId = currentTopId;
            localStorage.setItem(k.khoaTopId, currentTopId);
        }

        const hoiThoai = loai === 'binhluan' ? apGhimVaChuaDoc(nhomTheoDon(listItems, i => i.idFormIt, readIdsFromCookie), KHOA_HOI_THOAI_IT) : null;
        if (hoiThoai) k.donDaTai = hoiThoai.map(n => n.idDon);
        // Bình luận: badge đếm số hội thoại (đơn) còn tin chưa đọc; chuông: đếm từng thông báo.
        const unreadCount = hoiThoai
            ? hoiThoai.filter(n => n.chuaDoc > 0).length
            : listItems.filter(item => !readIdsFromCookie.includes(item.id.toString())).length;
        if (k.badge) {
            if (unreadCount > 0) {
                k.badge.innerText = unreadCount > 99 ? "99+" : unreadCount;
                k.badge.style.display = 'flex';
            } else { k.badge.style.display = 'none'; }
        }

        listItems.forEach(item => { if (!k.allFetchedIds.includes(item.id.toString())) k.allFetchedIds.push(item.id.toString()); });
        const html = hoiThoai
            ? hoiThoai.map(n => dongHoiThoai(n, `/FormIT/ChiTiet/${n.idDon}`, `onclick="moHoiThoai('${n.idDon}', '${n.items.map(i => i.id).join(',')}')"`)).join('')
            : listItems.map(item => {
            const isRead = readIdsFromCookie.includes(item.id.toString());
            return `<a href="/FormIT/ChiTiet/${item.idFormIt}" onclick="markAsRead('${item.id}')" style="text-decoration:none; display:flex; gap:15px; padding:16px 20px; border-bottom:1px solid #f8fafc; transition: 0.2s; color: inherit; background: ${isRead ? '#ffffff' : 'rgba(59, 130, 246, 0.05)'};">
        <div style="width: 44px; height: 44px; border-radius: 12px; background: #eff6ff; color: #3b82f6; display: flex; align-items: center; justify-content: center; flex-shrink: 0;"><i class="fa ${item.icon || k.iconMacDinh}"></i></div>
        <div style="flex:1;"><div style="font-size: 0.88rem; font-weight: ${isRead ? '600' : '800'}; color: #1e293b;">${escapeHtml(item.tieuDe)}</div>
        <div style="font-size: 0.8rem; color: #64748b; white-space: pre-line;">${escapeHtml(item.mota)}</div>
        <div style="font-size: 0.7rem; color: #94a3b8; margin-top: 6px;"><i class="fa fa-clock-o"></i> ${item.time}</div></div></a>`;
        }).join('');

        if (isOpen && k.list) {
            k.list.innerHTML = html || `<div style="padding: 30px 20px; text-align: center; color: #94a3b8; font-size: 0.85rem;">${loai === 'binhluan' ? 'Chưa có bình luận nào' : 'Không có thông báo'}</div>`;
        }
        k.skip += listItems.length;
    } catch (err) { } finally { k.isLoading = false; }
}

function loadAll() {
    Object.keys(kenhThongBao).forEach(loai => loadNotifications(loai, false));
}

// Cookie "read_notifications" cũ không giới hạn dung lượng: dùng lâu ngày chuỗi ID
// vượt quá ~4KB khiến trình duyệt âm thầm từ chối ghi cookie mới, làm nút "Đọc tất cả"
// mất tác dụng vĩnh viễn. Chuyển sang localStorage (không bị giới hạn này) và dọn cookie cũ 1 lần.
(function migrateReadNotificationsCookie() {
    try {
        if (!localStorage.getItem('read_notifications')) {
            const name = "read_notifications=";
            const ca = document.cookie.split(';');
            for (let i = 0; i < ca.length; i++) {
                let c = ca[i].trim();
                if (c.indexOf(name) === 0) { localStorage.setItem('read_notifications', c.substring(name.length)); break; }
            }
        }
        document.cookie = "read_notifications=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
    } catch (e) { }
})();

function getReadIds() {
    try {
        const stored = localStorage.getItem('read_notifications');
        return stored ? stored.split(',').filter(id => id !== "") : [];
    } catch (e) { return []; }
}

function setReadCookie(ids) {
    // Chỉ giữ 300 id gần nhất, tránh lặp lại tình trạng phình to không giới hạn như cookie cũ.
    const trimmed = ids.slice(-300);
    try { localStorage.setItem('read_notifications', trimmed.join(',')); } catch (e) { }
}

function triggerNewNotificationEffect(item, noiDung) {
    const latestTag = document.getElementById('latestNotiTag');
    const latestText = document.getElementById('latestNotiText');
    if (latestText) latestText.innerText = noiDung || item.tieuDe;
    if (latestTag) latestTag.style.display = 'flex';
    notiSound.play().catch(() => { });
    setTimeout(() => { if (latestTag) latestTag.style.display = 'none'; }, 8000);
}

// id có thể là danh sách "1,2,3": mở một hội thoại bình luận = đọc hết bình luận của đơn đó.
function markAsRead(id) {
    let readIds = getReadIds();
    let doi = false;
    id.toString().split(',').forEach(x => { if (x && !readIds.includes(x)) { readIds.push(x); doi = true; } });
    if (doi) {
        setReadCookie(readIds);
        setTimeout(loadAll, 300);
    }
}

function markAllAsRead(e, loai = 'khac') {
    if (e) e.stopPropagation();
    const k = kenhThongBao[loai];
    if (!k) return;

    let readIds = getReadIds();
    let hasChanges = false;

    k.allFetchedIds.forEach(id => {
        if (!readIds.includes(id)) {
            readIds.push(id);
            hasChanges = true;
        }
    });

    // Bình luận: "Đọc tất cả" cũng bỏ các cờ "chưa đọc thủ công" của đơn đang hiện.
    const coChuaDocTay = loai === 'binhluan' && (k.donDaTai || []).length > 0;
    if (coChuaDocTay) boChuaDocTay(KHOA_HOI_THOAI_IT, k.donDaTai);

    if (hasChanges) setReadCookie(readIds);
    if (hasChanges || coChuaDocTay) loadNotifications(loai, true);
}

// Tab đang ẩn thì bỏ lượt hỏi, tránh mỗi tab mở sẵn nện request liên tục vào GetNotifications.
// Khi người dùng quay lại tab thì nạp bù ngay để không trễ thông báo.
setInterval(() => {
    if (document.visibilityState === 'visible') loadAll();
}, 30000);

document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') loadAll();
});

loadAll();
