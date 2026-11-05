// Khung ký xác nhận 3 cấp dùng chung: Tự đánh giá (nhân viên ký), Chi tiết (cấp trên duyệt), Đánh giá nhân viên (xem).
// KyXacNhan.ve(idHop, { url, maNv, idKy }): vẽ vào #idHop. Thao tác xong tự tải lại khung; quyền do server trả về từng lần.
(function () {
    'use strict';

    var st = { hop: null, opt: null, d: null, moTuChoi: false, dangLuu: false, lanTai: 0 };

    function el(tag, attrs, con) {
        var e = document.createElement(tag);
        if (attrs) Object.keys(attrs).forEach(function (k) {
            if (k === 'text') e.textContent = attrs[k];
            else if (k === 'class') e.className = attrs[k];
            else if (attrs[k] !== null && attrs[k] !== undefined && attrs[k] !== false) e.setAttribute(k, attrs[k] === true ? '' : attrs[k]);
        });
        (con || []).forEach(function (c) { if (c) e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
        return e;
    }

    function token() {
        var o = document.querySelector('input[name="__RequestVerificationToken"]');
        return o ? o.value : '';
    }

    function goiApi(url, body) {
        var opt = body === undefined
            ? { headers: { 'Accept': 'application/json' } }
            : { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() }, body: JSON.stringify(body) };
        return fetch(url, opt).then(function (res) {
            if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
            if (!res.ok && res.status !== 403) throw new Error('Máy chủ trả lỗi ' + res.status + '.');
            return res.json();
        }).then(function (kq) {
            if (!kq.thanhCong) throw new Error(kq.thongBao || 'Thao tác thất bại.');
            return kq;
        });
    }

    function ngayGio(s) {
        if (!s) return '';
        var d = new Date(s);
        if (isNaN(d)) return '';
        var h = function (n) { return String(n).padStart(2, '0'); };
        return h(d.getDate()) + '/' + h(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + h(d.getHours()) + ':' + h(d.getMinutes());
    }

    function diem(v) { return v === null || v === undefined ? '—' : String(Math.round(v * 100) / 100); }

    var TEN_HANH_DONG = { gui_duyet: 'Gửi cấp trên duyệt', duyet: 'Cấp trên duyệt', tu_choi: 'Cấp trên từ chối', nv_ky: 'Nhân viên ký', mo_lai: 'Mở lại phiếu' };

    function buoc(so, tieuDe, lop, dong1, dong2) {
        return el('li', { class: 'kx-buoc ' + lop }, [
            el('span', { class: 'kx-so', text: lop === 'xong' ? '✓' : lop === 'tu-choi' ? '✕' : String(so) }),
            el('div', null, [el('b', { text: tieuDe }), dong1 ? el('span', { text: dong1 }) : null, dong2 ? el('small', { text: dong2 }) : null])
        ]);
    }

    function veBuoc(d) {
        var b = d.buocKy;
        var duyetXong = b === 'cho_ky' || b === 'da_ky';
        return el('ol', { class: 'kx-duong' }, [
            buoc(1, 'Quản lý chấm', b === 'tu_choi' ? 'dang' : 'xong', b === 'tu_choi' ? 'Đang chấm lại' : d.tenNguoiCham, ngayGio(d.ngayCham)),
            buoc(2, 'Cấp trên duyệt', duyetXong ? 'xong' : b === 'tu_choi' ? 'tu-choi' : 'dang', duyetXong ? d.tenNguoiDuyet : b === 'tu_choi' ? 'Đã trả về' : 'Đang chờ duyệt', ngayGio(d.ngayDuyet)),
            buoc(3, 'Nhân viên ký', b === 'da_ky' ? 'xong' : b === 'cho_ky' ? 'dang' : 'cho',
                b === 'da_ky' ? (d.nvDongY ? 'Đồng ý' : 'Không đồng ý') : b === 'cho_ky' ? 'Đang chờ ký' : 'Chưa tới lượt', ngayGio(d.ngayNvKy))
        ]);
    }

    function veKetQua(k) {
        return el('div', { class: 'kx-ket-qua' }, [
            el('div', { class: 'kh-tom-tat' }, [
                ['Part I', diem(k.diemKpi)], ['Part II', diem(k.diemNangLuc)], ['Tổng / 100', diem(k.diemThucTe)],
                ['So với chuẩn', diem(k.chenhLech)], ['Xếp loại', k.xepLoai || '—']
            ].map(function (o) { return el('div', { class: 'kh-o' }, [el('span', { text: o[0] }), el('b', { text: o[1] })]); })),
            k.nhanXet ? el('p', { class: 'kx-nhan-xet' }, [el('b', { text: 'Nhận xét của quản lý: ' }), k.nhanXet]) : null
        ]);
    }

    function veFormKy() {
        return el('form', { class: 'kx-form', id: 'kxFormKy', autocomplete: 'off' }, [
            el('div', { class: 'kx-chon' }, [
                el('label', null, [el('input', { type: 'radio', name: 'kxDongY', value: '1' }), ' Đồng ý']),
                el('label', null, [el('input', { type: 'radio', name: 'kxDongY', value: '0' }), ' Không đồng ý'])
            ]),
            el('label', { class: 'tdg-field' }, [el('span', { text: 'Nhận xét của bạn' }), el('textarea', { class: 'form-control', id: 'kxNhanXet', rows: '2', maxlength: '8000' })]),
            el('div', { class: 'nl-luu' }, [
                el('span', { class: 'nl-loi', id: 'kxLoi', role: 'alert' }),
                el('button', { type: 'submit', class: 'btn btn-primary', id: 'kxNutKy', text: 'Ký xác nhận' })
            ])
        ]);
    }

    function veFormDuyet(d) {
        return el('div', { class: 'kx-form' }, [
            st.moTuChoi ? el('label', { class: 'tdg-field' }, [el('span', { text: 'Lý do từ chối' }), el('textarea', { class: 'form-control', id: 'kxLyDo', rows: '2', maxlength: '1000' })]) : null,
            el('div', { class: 'nl-luu' }, [
                el('span', { class: 'nl-loi', id: 'kxLoi', role: 'alert' }),
                d.coTheTuChoi ? el('button', { type: 'button', class: 'btn btn-outline-danger', id: 'kxNutTuChoi', text: st.moTuChoi ? 'Gửi từ chối' : 'Từ chối' }) : null,
                !st.moTuChoi ? el('button', { type: 'button', class: 'btn btn-primary', id: 'kxNutDuyet', text: 'Duyệt phiếu' }) : null
            ]),
            !d.coTheTuChoi ? el('p', { class: 'tdg-card-desc', text: 'Kỳ đã đóng: muốn từ chối thì mở lại kỳ trước.' }) : null
        ]);
    }

    function veLichSu(ds) {
        if (!ds.length) return null;
        return el('details', { class: 'kx-lich-su' }, [
            el('summary', { text: 'Lịch sử ký (' + ds.length + ')' }),
            el('ul', null, ds.map(function (x) {
                return el('li', { text: ngayGio(x.ngay) + ' · ' + (TEN_HANH_DONG[x.hanhDong] || x.hanhDong) + ' · ' + (x.tenNguoi || '') + (x.ghiChu ? ' · ' + x.ghiChu : '') });
            }))
        ]);
    }

    function veKhung() {
        var hop = st.hop, d = st.d;
        hop.textContent = '';
        if (!d.coPhieu || (!d.buocKy && !d.chotTruocLuongKy)) return;
        if (d.chotTruocLuongKy) {
            hop.appendChild(el('div', { class: 'tdg-alert tdg-alert-info' }, [el('i', { class: 'fa fa-info-circle' }), el('div', { text: 'Phiếu này được chốt trước khi có luồng ký xác nhận.' })]));
            return;
        }
        var card = el('section', { class: 'tdg-card kx-khung' }, [
            el('h2', { class: 'tdg-card-title', text: 'Ký xác nhận' }),
            veBuoc(d),
            d.buocKy === 'tu_choi' && d.lyDoTuChoi ? el('div', { class: 'tdg-alert tdg-alert-warn' }, [el('i', { class: 'fa fa-exclamation-triangle' }), el('div', null, [el('b', { text: 'Cấp trên từ chối: ' }), d.lyDoTuChoi])]) : null,
            d.buocKy === 'da_ky' && d.nhanXetNv ? el('p', { class: 'kx-nhan-xet' }, [el('b', { text: 'Nhận xét của nhân viên: ' }), d.nhanXetNv]) : null,
            d.ketQua && (d.coTheKy || d.coTheDuyet || d.buocKy === 'da_ky') ? veKetQua(d.ketQua) : null,
            d.coTheKy ? veFormKy() : null,
            d.coTheDuyet ? veFormDuyet(d) : null,
            veLichSu(d.dsLichSu || [])
        ]);
        hop.appendChild(card);
    }

    function tai() {
        var lan = ++st.lanTai;
        return goiApi(st.opt.url).then(function (kq) {
            if (lan !== st.lanTai) return; // đã chuyển sang người/kỳ khác trong lúc chờ
            st.d = kq.duLieu;
            veKhung();
        }).catch(function (e) {
            if (lan !== st.lanTai) return;
            st.hop.textContent = '';
            st.hop.appendChild(el('div', { class: 'tdg-alert tdg-alert-warn' }, [el('i', { class: 'fa fa-exclamation-triangle' }), el('div', { text: e.message })]));
        });
    }

    function ve(idHop, opt) {
        st.hop = document.getElementById(idHop);
        st.opt = opt;
        st.moTuChoi = false;
        st.hop.textContent = '';
        return tai();
    }

    function gui(url, body, nut) {
        if (st.dangLuu) return;
        st.dangLuu = true;
        nut.disabled = true;
        var loi = document.getElementById('kxLoi');
        loi.textContent = '';
        goiApi(url, body)
            .then(function () { st.moTuChoi = false; return tai(); })
            .catch(function (e) { loi.textContent = e.message; nut.disabled = false; })
            .finally(function () { st.dangLuu = false; });
    }

    document.addEventListener('submit', function (e) {
        if (!e.target.closest || e.target.id !== 'kxFormKy') return;
        e.preventDefault();
        var chon = e.target.querySelector('input[name="kxDongY"]:checked');
        if (!chon) { document.getElementById('kxLoi').textContent = 'Chọn Đồng ý hoặc Không đồng ý.'; return; }
        gui('/QLCongViec/DanhGia/KyXacNhanApi/KyCuaToi', {
            idKy: st.opt.idKy, dongY: chon.value === '1', nhanXet: document.getElementById('kxNhanXet').value
        }, document.getElementById('kxNutKy'));
    });

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!t.closest) return;
        var nutDuyet = t.closest('#kxNutDuyet');
        if (nutDuyet) {
            gui('/QLCongViec/DanhGia/KyXacNhanApi/Duyet', { maNv: st.opt.maNv, idKy: st.opt.idKy }, nutDuyet);
            return;
        }
        var nutTuChoi = t.closest('#kxNutTuChoi');
        if (!nutTuChoi) return;
        if (!st.moTuChoi) { st.moTuChoi = true; veKhung(); return; }
        gui('/QLCongViec/DanhGia/KyXacNhanApi/TuChoi', { maNv: st.opt.maNv, idKy: st.opt.idKy, lyDo: document.getElementById('kxLyDo').value }, nutTuChoi);
    });

    window.KyXacNhan = { ve: ve };
})();
