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

async function loadNotifications(loai, isOpen = false) {
    const k = kenhThongBao[loai];
    if (!k || k.isLoading) return;
    const readIdsFromCookie = getReadIds();
    k.skip = 0;
    try {
        k.isLoading = true;
        const res = await fetch(`/FormIT/GetNotifications?skip=${k.skip}&take=${take}&loai=${loai}`);
        if (!res.ok) return;
        const data = await res.json();
        const listItems = data.top5 || data.dataList || [];

        if (listItems.length > 0) {
            const currentTopId = listItems[0].id.toString();
            if (k.lastTopId !== null && currentTopId !== k.lastTopId && !readIdsFromCookie.includes(currentTopId)) {
                triggerNewNotificationEffect(listItems[0]);
            }
            k.lastTopId = currentTopId;
            localStorage.setItem(k.khoaTopId, currentTopId);
        }

        const unreadCount = listItems.filter(item => !readIdsFromCookie.includes(item.id.toString())).length;
        if (k.badge) {
            if (unreadCount > 0) {
                k.badge.innerText = unreadCount > 99 ? "99+" : unreadCount;
                k.badge.style.display = 'flex';
            } else { k.badge.style.display = 'none'; }
        }

        const html = listItems.map(item => {
            const isRead = readIdsFromCookie.includes(item.id.toString());
            if (!k.allFetchedIds.includes(item.id.toString())) k.allFetchedIds.push(item.id.toString());
            return `<a href="/FormIT/ChiTiet/${item.idFormIt}" onclick="markAsRead('${item.id}')" style="text-decoration:none; display:flex; gap:15px; padding:16px 20px; border-bottom:1px solid #f8fafc; transition: 0.2s; color: inherit; background: ${isRead ? '#ffffff' : 'rgba(59, 130, 246, 0.05)'};">
        <div style="width: 44px; height: 44px; border-radius: 12px; background: #eff6ff; color: #3b82f6; display: flex; align-items: center; justify-content: center; flex-shrink: 0;"><i class="fa ${item.icon || k.iconMacDinh}"></i></div>
        <div style="flex:1;"><div style="font-size: 0.88rem; font-weight: ${isRead ? '600' : '800'}; color: #1e293b;">${escapeHtml(item.tieuDe)}</div>
        <div style="font-size: 0.8rem; color: #64748b; white-space: pre-line;">${escapeHtml(item.mota)}</div>
        <div style="font-size: 0.7rem; color: #94a3b8; margin-top: 6px;"><i class="fa fa-clock-o"></i> ${item.time}</div></div></a>`;
        }).join('');

        if (isOpen && k.list) {
            k.list.innerHTML = html || '<div style="padding: 30px 20px; text-align: center; color: #94a3b8; font-size: 0.85rem;">Không có thông báo</div>';
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

function triggerNewNotificationEffect(item) {
    const latestTag = document.getElementById('latestNotiTag');
    const latestText = document.getElementById('latestNotiText');
    if (latestText) latestText.innerText = item.tieuDe;
    if (latestTag) latestTag.style.display = 'flex';
    notiSound.play().catch(() => { });
    setTimeout(() => { if (latestTag) latestTag.style.display = 'none'; }, 8000);
}

function markAsRead(id) {
    let readIds = getReadIds();
    if (!readIds.includes(id.toString())) {
        readIds.push(id.toString());
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

    if (hasChanges) {
        setReadCookie(readIds);
        loadNotifications(loai, true);
    }
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
