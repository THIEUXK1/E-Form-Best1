// Chuông thông báo + dropdown tài khoản trên header của Area ITForm.
// markAsRead / markAllAsRead được gọi từ onclick trong HTML nên phải ở phạm vi toàn cục.

const notiSound = new Audio('https://assets.mixkit.co/active_storage/sfx/2869/2869-preview.mp3');
notiSound.volume = 0.6;

let lastTopId = localStorage.getItem('lastNotifiedId') || null;
let currentSkip = 0;
const take = 20;
let isLoading = false;
let hasMoreData = true;
let allFetchedIds = [];

const notiBell = document.getElementById('notiBell');
const notiDrop = document.getElementById('notificationDropdown');
const notiList = document.getElementById('notiList');
const notiBadge = document.getElementById('notiBadge');
const userBtn = document.getElementById('userMenuButton');
const userDrop = document.getElementById('userDropdown');
const userArrow = document.getElementById('arrowIcon');

if (notiBell) {
    notiBell.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = notiDrop.style.display === 'none' || notiDrop.style.display === '';
        notiDrop.style.display = isHidden ? 'flex' : 'none';
        if (isHidden) loadNotifications(true, false);
    });
}

if (userBtn && userDrop) {
    userBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = userDrop.style.display === 'none' || userDrop.style.display === '';
        userDrop.style.display = isHidden ? 'flex' : 'none';
        if (userArrow) userArrow.style.transform = isHidden ? 'rotate(180deg)' : 'rotate(0deg)';
    });
}

async function loadNotifications(isOpen = false, isLoadMore = false) {
    if (isLoading) return;
    const readIdsFromCookie = getReadIds();
    if (!isLoadMore) { currentSkip = 0; hasMoreData = true; }
    try {
        isLoading = true;
        const res = await fetch(`/FormIT/GetNotifications?skip=${currentSkip}&take=${take}`);
        const data = await res.json();
        const listItems = data.top5 || data.dataList || [];

        if (listItems.length > 0) {
            const currentTopId = listItems[0].id.toString();
            if (lastTopId !== null && currentTopId !== lastTopId && !readIdsFromCookie.includes(currentTopId)) {
                triggerNewNotificationEffect(listItems[0]);
            }
            lastTopId = currentTopId;
            localStorage.setItem('lastNotifiedId', currentTopId);
        }

        const unreadCount = listItems.filter(item => !readIdsFromCookie.includes(item.id.toString())).length;
        if (notiBadge) {
            if (unreadCount > 0) {
                notiBadge.innerText = unreadCount > 99 ? "99+" : unreadCount;
                notiBadge.style.display = 'flex';
            } else { notiBadge.style.display = 'none'; }
        }

        const html = listItems.map(item => {
            const isRead = readIdsFromCookie.includes(item.id.toString());
            if (!allFetchedIds.includes(item.id.toString())) allFetchedIds.push(item.id.toString());
            return `<a href="/FormIT/ChiTiet/${item.idFormIt}" onclick="markAsRead('${item.id}')" style="text-decoration:none; display:flex; gap:15px; padding:16px 20px; border-bottom:1px solid #f8fafc; transition: 0.2s; color: inherit; background: ${isRead ? '#ffffff' : 'rgba(59, 130, 246, 0.05)'};">
        <div style="width: 44px; height: 44px; border-radius: 12px; background: #eff6ff; color: #3b82f6; display: flex; align-items: center; justify-content: center; flex-shrink: 0;"><i class="fa ${item.icon || 'fa-bell'}"></i></div>
        <div style="flex:1;"><div style="font-size: 0.88rem; font-weight: ${isRead ? '600' : '800'}; color: #1e293b;">${item.tieuDe}</div>
        <div style="font-size: 0.8rem; color: #64748b;">${item.mota || ''}</div>
        <div style="font-size: 0.7rem; color: #94a3b8; margin-top: 6px;"><i class="fa fa-clock-o"></i> ${item.time}</div></div></a>`;
        }).join('');

        if (isOpen && notiList) {
            if (isLoadMore) notiList.insertAdjacentHTML('beforeend', html);
            else notiList.innerHTML = html;
        }
        currentSkip += listItems.length;
        if (listItems.length < take) hasMoreData = false;
    } catch (err) { } finally { isLoading = false; }
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
        setTimeout(() => loadNotifications(false, false), 300);
    }
}

function markAllAsRead(e) {
    if (e) e.stopPropagation();

    let readIds = getReadIds();
    let hasChanges = false;

    allFetchedIds.forEach(id => {
        if (!readIds.includes(id)) {
            readIds.push(id);
            hasChanges = true;
        }
    });

    if (hasChanges) {
        setReadCookie(readIds);
        loadNotifications(true, false);
    }
}

// Tab đang ẩn thì bỏ lượt hỏi, tránh mỗi tab mở sẵn nện 120 request/giờ vào GetNotifications.
// Khi người dùng quay lại tab thì nạp bù ngay để không trễ thông báo.
setInterval(() => {
    if (document.visibilityState === 'visible') loadNotifications(false, false);
}, 30000);

document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') loadNotifications(false, false);
});

loadNotifications(false, false);
