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

    const amBao = new Audio('https://assets.mixkit.co/active_storage/sfx/2869/2869-preview.mp3');
    amBao.volume = 0.6;

    let lastTopId = null;
    try { lastTopId = localStorage.getItem(khoaTop); } catch (e) { }
    let dangTai = false;
    let idsDaTai = [];

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

    function hieuUngMoi(item) {
        const tag = document.getElementById('latestNotiTag');
        const text = document.getElementById('latestNotiText');
        if (text) text.innerText = item.tieuDe || '';
        if (tag) tag.style.display = 'flex';
        amBao.play().catch(() => { });
        setTimeout(() => { if (tag) tag.style.display = 'none'; }, 8000);
    }

    async function tai(moDanhSach) {
        if (dangTai) return;
        dangTai = true;
        try {
            const res = await fetch(`${api}?skip=0&take=20&loai=binhluan`);
            if (!res.ok) return;
            const data = await res.json();
            const items = data.dataList || [];
            const daDoc = docIdsDaDoc();

            if (items.length > 0) {
                const topId = items[0].id.toString();
                if (lastTopId !== null && topId !== lastTopId && !daDoc.includes(topId)) hieuUngMoi(items[0]);
                lastTopId = topId;
                try { localStorage.setItem(khoaTop, topId); } catch (e) { }
            }

            idsDaTai = items.map(i => i.id.toString());
            const chuaDoc = items.filter(i => !daDoc.includes(i.id.toString())).length;
            if (badge) {
                badge.innerText = chuaDoc > 99 ? '99+' : chuaDoc;
                badge.style.display = chuaDoc > 0 ? 'flex' : 'none';
            }

            if (moDanhSach) {
                list.innerHTML = items.length === 0
                    ? '<div style="padding: 30px 20px; text-align: center; color: #94a3b8; font-size: 0.85rem;">Không có bình luận</div>'
                    : items.map(item => {
                        const isRead = daDoc.includes(item.id.toString());
                        return `<a href="${chiTiet}${encodeURIComponent(item[truongId])}" data-chat-id="${item.id}" style="text-decoration:none; display:flex; gap:15px; padding:16px 20px; border-bottom:1px solid #f8fafc; transition: 0.2s; color: inherit; background: ${isRead ? '#ffffff' : 'rgba(59, 130, 246, 0.05)'};">
        <div style="width: 44px; height: 44px; border-radius: 12px; background: #eff6ff; color: #3b82f6; display: flex; align-items: center; justify-content: center; flex-shrink: 0;"><i class="fa fa-commenting-o"></i></div>
        <div style="flex:1;"><div style="font-size: 0.88rem; font-weight: ${isRead ? '600' : '800'}; color: #1e293b;">${escapeHtml(item.tieuDe)}</div>
        <div style="font-size: 0.8rem; color: #64748b; white-space: pre-line;">${escapeHtml(item.mota)}</div>
        <div style="font-size: 0.7rem; color: #94a3b8; margin-top: 6px;"><i class="fa fa-clock-o"></i> ${escapeHtml(item.time)}</div></div></a>`;
                    }).join('');
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
        const a = e.target.closest('a[data-chat-id]');
        if (!a) return;
        const ids = docIdsDaDoc();
        if (!ids.includes(a.dataset.chatId)) { ids.push(a.dataset.chatId); ghiIdsDaDoc(ids); }
    });

    const nutDocHet = document.getElementById('chatDocHet');
    if (nutDocHet) nutDocHet.addEventListener('click', (e) => {
        e.stopPropagation();
        const ids = docIdsDaDoc();
        let doi = false;
        idsDaTai.forEach(id => { if (!ids.includes(id)) { ids.push(id); doi = true; } });
        if (doi) { ghiIdsDaDoc(ids); tai(true); }
    });

    // Tab ẩn thì bỏ lượt hỏi; quay lại tab thì nạp bù ngay.
    setInterval(() => { if (document.visibilityState === 'visible') tai(false); }, 30000);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') tai(false); });
    tai(false);
})();
