/* Trang /TheoDoiHeThong — bấm ô hệ thống mở hộp theo dõi sự cố, hai bên hiện cùng lúc:
   trái = form (mặc định thêm mới; chọn sự cố bên phải thì chuyển sang cập nhật sự cố đó),
   phải = lịch sử sự cố, sự cố đang chọn mở rộng tại chỗ (bằng chứng + lịch sử xử lý).
   Không reload: lưu xong chỉ vẽ lại danh sách, chi tiết và nhãn trạng thái trên ô. */
(function ($) {
    'use strict';

    var TEN_TRANG_THAI = { DANG_XU_LY: 'Đang xử lý', THEO_DOI: 'Đang theo dõi', DA_KHAC_PHUC: 'Đã khắc phục' };
    var TEN_HANH_DONG = { TAO: 'Tạo sự cố', CAP_NHAT: 'Cập nhật', GHI_CHU: 'Ghi chú tiến độ', XOA: 'Xoá' };
    var FILE_TOI_DA = 20, CO_FILE_TOI_DA = 30 * 1024 * 1024, CO_GUI_TOI_DA = 100 * 1024 * 1024;

    var $modal = $('#modalSuCo');
    var $form = $('#formSuCo');
    var maHeThong = null;
    var idDangChon = null;  // sự cố đang sửa ở form bên trái (null = thêm mới)
    var idMoRong = null;    // sự cố đang mở rộng bên phải (sau khi thêm mới thì khác idDangChon)
    var maGui = null;   // khoá chống lưu trùng, đổi mỗi lần mở form
    // File chờ gửi: gom từ ô chọn file và từ Ctrl+V; ô <input type=file> không cộng dồn được nên JS tự giữ
    var fileDinhKem = [];
    var DUOI_CHO_PHEP = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.pdf', '.doc', '.docx', '.xls', '.xlsx',
        '.ppt', '.pptx', '.txt', '.log', '.csv', '.zip', '.rar', '.7z', '.mp4', '.mov', '.eml', '.msg'];
    var dangLuu = false;

    // crypto.randomUUID chỉ có trên HTTPS/localhost; trang nội bộ chạy http nên tự sinh từ getRandomValues
    function taoMaGui() {
        var b = crypto.getRandomValues(new Uint8Array(16));
        b[6] = (b[6] & 0x0f) | 0x40;
        b[8] = (b[8] & 0x3f) | 0x80;
        var h = Array.prototype.map.call(b, function (x) { return ('0' + x.toString(16)).slice(-2); }).join('');
        return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20);
    }

    // Máy chủ trả "yyyy-MM-ddTHH:mm:ss" giờ VN, không múi giờ → cắt chuỗi, không qua Date để khỏi lệch giờ
    function hienNgay(s) {
        if (!s) return '';
        return s.slice(8, 10) + '/' + s.slice(5, 7) + '/' + s.slice(0, 4) + ' ' + s.slice(11, 16);
    }
    function giaTriInput(s) { return s ? s.slice(0, 16) : ''; }
    function bayGioInput() {
        var d = new Date();
        d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
        return d.toISOString().slice(0, 16);
    }

    function hienCo(byte) {
        if (byte >= 1024 * 1024) return (byte / 1024 / 1024).toFixed(1) + ' MB';
        return Math.max(1, Math.round(byte / 1024)) + ' KB';
    }

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    function nhanTrangThai(tt) {
        return el('span', 'tdht-tt tdht-tt-' + tt.toLowerCase(), TEN_TRANG_THAI[tt] || tt);
    }

    function datNhanHeThong($nhan, soMo) {
        $nhan.removeClass('tot loi').addClass(soMo > 0 ? 'loi' : 'tot')
            .attr('data-so-mo', soMo).text(soMo > 0 ? soMo + ' sự cố đang mở' : 'Bình thường');
    }

    function capNhatTrangThaiHeThong(soMo) {
        if (soMo == null) return;
        var $o = $('.tdht-o[data-ma="' + maHeThong + '"]');
        $o.toggleClass('tdht-o-loi', soMo > 0);
        datNhanHeThong($o.find('.tdht-nhan-trang-thai'), soMo);
        datNhanHeThong($('#suCoNhanHeThong'), soMo);
    }

    function thongBao(text, loi) {
        $('#suCoThongBao').removeClass('text-success text-danger').addClass(loi ? 'text-danger' : 'text-success').text(text || '');
    }

    function layJson(url) {
        return fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } }).then(function (res) {
            if (!res.ok) throw new Error('Máy chủ trả lỗi ' + res.status);
            return res.json();
        });
    }

    // ---------- Bên phải: lịch sử sự cố ----------
    function taiDanhSach() {
        var khung = document.getElementById('dsSuCo');
        khung.replaceChildren(el('div', 'tdht-trong', 'Đang tải...'));
        return layJson('/TheoDoiHeThong/SuCo?maHeThong=' + encodeURIComponent(maHeThong)).then(function (res) {
            if (!res.thanhCong) throw new Error(res.thongBao);
            khung.replaceChildren();
            if (!res.duLieu.length) {
                khung.appendChild(el('div', 'tdht-trong', 'Chưa có sự cố nào được ghi nhận.'));
                return;
            }
            res.duLieu.forEach(function (x) {
                var dong = el('button', 'tdht-dong-su-co');
                dong.type = 'button';
                dong.dataset.id = x.idSuCo;
                var tren = el('div', 'd-flex align-items-start gap-2');
                tren.appendChild(el('span', 'tdht-dong-tieu-de flex-grow-1', x.tieuDe));
                tren.appendChild(nhanTrangThai(x.trangThai));
                dong.appendChild(tren);
                var thoiGian = hienNgay(x.batDauLuc) + (x.ketThucLuc ? ' → ' + hienNgay(x.ketThucLuc) : '');
                dong.appendChild(el('div', 'tdht-dong-phu', thoiGian + (x.nguoiChiuTrachNhiem ? ' · ' + x.nguoiChiuTrachNhiem : '')));
                if (x.nguyenNhan) dong.appendChild(el('div', 'tdht-dong-phu', 'Nguyên nhân: ' + x.nguyenNhan));
                khung.appendChild(dong);
            });
        }).catch(function (e) {
            khung.replaceChildren(el('div', 'tdht-trong text-danger', e.message || 'Không tải được danh sách.'));
        });
    }

    function boChiTiet() {
        idMoRong = null;
        $('#dsSuCo .tdht-chi-tiet').remove();
        $('#dsSuCo .tdht-dong-su-co').removeClass('active');
    }

    /** Khối mở rộng ngay dưới dòng sự cố đang chọn: bằng chứng + lịch sử xử lý. */
    function veChiTiet(suCo) {
        boChiTiet();
        var dong = document.querySelector('#dsSuCo .tdht-dong-su-co[data-id="' + suCo.idSuCo + '"]');
        if (!dong) return;
        idMoRong = suCo.idSuCo;
        dong.classList.add('active');

        var khoi = el('div', 'tdht-chi-tiet');
        if (suCo.moTa) khoi.appendChild(el('div', 'tdht-chi-tiet-mo-ta', suCo.moTa));

        khoi.appendChild(el('div', 'tdht-chi-tiet-muc', 'Bằng chứng'));
        khoi.appendChild(veBangChung(suCo.idSuCo, suCo.files || [], suCo.loiFile));

        khoi.appendChild(el('div', 'tdht-chi-tiet-muc', 'Lịch sử xử lý'));
        khoi.appendChild(veLichSu(suCo.lichSu || []));

        dong.after(khoi);
    }

    function veBangChung(idSuCo, ds, loi) {
        var khung = el('div', 'tdht-bang-chung');
        if (loi) khung.appendChild(el('div', 'text-danger small', loi));
        if (!ds.length) {
            if (!loi) khung.appendChild(el('div', 'tdht-dong-phu', 'Chưa có ảnh/file bằng chứng.'));
            return khung;
        }
        ds.forEach(function (f) {
            var a = el('a', 'tdht-file' + (f.laAnh ? ' tdht-file-anh' : ''));
            a.href = '/TheoDoiHeThong/SuCo/' + idSuCo + '/File/' + encodeURIComponent(f.tenLuu);
            a.target = '_blank';
            a.rel = 'noopener';
            a.title = f.ten;
            if (f.laAnh) {
                var img = el('img');
                img.src = a.href;
                img.alt = f.ten;
                img.loading = 'lazy';
                a.appendChild(img);
            } else {
                a.appendChild(el('i', 'fa fa-file-o tdht-file-icon'));
            }
            a.appendChild(el('span', 'tdht-file-ten', f.ten));
            a.appendChild(el('span', 'tdht-file-co', hienCo(f.kichThuoc)));
            khung.appendChild(a);
        });
        return khung;
    }

    function veLichSu(ds) {
        var khung = el('div', 'tdht-lich-su');
        if (!ds.length) { khung.appendChild(el('div', 'tdht-dong-phu', 'Chưa có lịch sử.')); return khung; }
        ds.forEach(function (x) {
            var muc = el('div', 'tdht-muc-lich-su');
            var dau = el('div', 'tdht-lich-su-dau');
            dau.appendChild(el('strong', null, TEN_HANH_DONG[x.hanhDong] || x.hanhDong));
            dau.appendChild(el('span', 'text-muted', ' · ' + hienNgay(x.thoiGian) + (x.nguoiThaoTac ? ' · ' + x.nguoiThaoTac : '')));
            muc.appendChild(dau);
            if (x.thayDoi) muc.appendChild(el('div', 'tdht-lich-su-doi', x.thayDoi));
            if (x.ghiChu) muc.appendChild(el('div', 'tdht-lich-su-ghi-chu', x.ghiChu));
            khung.appendChild(muc);
        });
        return khung;
    }

    function taiChiTiet(id) {
        return layJson('/TheoDoiHeThong/SuCo/' + id).then(function (res) {
            if (!res.thanhCong) throw new Error(res.thongBao);
            moForm(res.duLieu);
            veChiTiet(res.duLieu);
        }).catch(function (e) {
            thongBao(e.message || 'Không tải được sự cố.', true);
        });
    }

    // ---------- Bên trái: form thêm mới / cập nhật ----------
    function moForm(suCo) {
        idDangChon = suCo ? suCo.idSuCo : null;
        maGui = taoMaGui();
        $form[0].reset();
        thongBao('');
        $form.find('[name=idSuCo]').val(suCo ? suCo.idSuCo : '');
        $form.find('[name=tieuDe]').val(suCo ? suCo.tieuDe : '');
        $form.find('[name=trangThai]').val(suCo ? suCo.trangThai : 'DANG_XU_LY');
        $form.find('[name=batDauLuc]').val(suCo ? giaTriInput(suCo.batDauLuc) : bayGioInput());
        $form.find('[name=ketThucLuc]').val(suCo ? giaTriInput(suCo.ketThucLuc) : '');
        $form.find('[name=nguoiChiuTrachNhiem]').val(suCo ? suCo.nguoiChiuTrachNhiem || '' : '');
        $form.find('[name=nguyenNhan]').val(suCo ? suCo.nguyenNhan || '' : '');
        $form.find('[name=moTa]').val(suCo ? suCo.moTa || '' : '');
        fileDinhKem = [];
        veFileChon();
        anHienKetThuc();
        $('#tieuDeForm').text(suCo ? 'Cập nhật sự cố: ' + suCo.tieuDe : 'Thêm sự cố mới');
        $('#btnSuCoMoi').toggleClass('d-none', !suCo);
        $('#btnLuuSuCo').html(suCo ? '<i class="fa fa-save"></i> Lưu cập nhật' : '<i class="fa fa-plus"></i> Thêm sự cố');
    }

    function anHienKetThuc() {
        $('#oKetThuc').toggleClass('d-none', $form.find('[name=trangThai]').val() !== 'DA_KHAC_PHUC');
    }

    // Kiểm sớm ở trình duyệt cho đỡ phải đợi tải lên mới bị từ chối; máy chủ vẫn kiểm lại
    function kiemTraFileChon(files) {
        if (files.length > FILE_TOI_DA) return 'Tối đa ' + FILE_TOI_DA + ' file mỗi lần.';
        var tong = 0;
        for (var i = 0; i < files.length; i++) {
            if (files[i].size > CO_FILE_TOI_DA) return 'File "' + files[i].name + '" vượt quá 30 MB.';
            tong += files[i].size;
        }
        if (tong > CO_GUI_TOI_DA) return 'Tổng dung lượng tối đa 100 MB mỗi lần.';
        return null;
    }

    function duoiFile(ten) {
        var i = ten.lastIndexOf('.');
        return i < 0 ? '' : ten.slice(i).toLowerCase();
    }

    /** Thêm file vào danh sách chờ gửi; ảnh dán từ clipboard thường tên "image.png" nên đặt lại tên theo giờ. */
    function themFile(dsMoi, laDan) {
        var boQua = [];
        Array.prototype.forEach.call(dsMoi, function (f, i) {
            if (laDan && /^image\.\w+$/i.test(f.name)) {
                var d = new Date(), p = function (n) { return ('0' + n).slice(-2); };
                var ten = 'dan_' + d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '_' + p(d.getHours()) + p(d.getMinutes())
                    + p(d.getSeconds()) + (i ? '_' + (i + 1) : '') + duoiFile(f.name);
                f = new File([f], ten, { type: f.type });
            }
            if (DUOI_CHO_PHEP.indexOf(duoiFile(f.name)) < 0) { boQua.push(f.name || '(không tên)'); return; }
            fileDinhKem.push(f);
        });
        veFileChon(boQua.length ? 'Không nhận loại file: ' + boQua.join(', ') : null);
    }

    function veFileChon(loiThem) {
        var khung = document.getElementById('dsFileChon');
        khung.replaceChildren();
        var loi = loiThem || kiemTraFileChon(fileDinhKem);
        if (loi) khung.appendChild(el('div', 'text-danger', loi));
        fileDinhKem.forEach(function (f, i) {
            var dong = el('div', 'tdht-file-cho');
            dong.appendChild(el('i', 'fa ' + (/^image\//.test(f.type) ? 'fa-file-image-o' : 'fa-file-o')));
            dong.appendChild(el('span', 'flex-grow-1 text-truncate', f.name + ' (' + hienCo(f.size) + ')'));
            var xoa = el('button', 'btn btn-sm btn-link text-danger p-0 tdht-xoa-file');
            xoa.type = 'button';
            xoa.dataset.i = i;
            xoa.title = 'Bỏ file này';
            xoa.appendChild(el('i', 'fa fa-times'));
            dong.appendChild(xoa);
            khung.appendChild(dong);
        });
    }

    // ---------- Sự kiện ----------
    $(document).on('click', '.tdht-o[data-ma]', function () {
        maHeThong = this.dataset.ma;
        idMoRong = null;
        $('#suCoTenHeThong').text(maHeThong);
        var url = this.dataset.url;
        $('#suCoMoHeThong').toggleClass('d-none', !url).attr('href', url || '#');
        datNhanHeThong($('#suCoNhanHeThong'), Number($(this).find('.tdht-nhan-trang-thai').attr('data-so-mo')) || 0);
        moForm(null);
        bootstrap.Modal.getOrCreateInstance($modal[0]).show();
        taiDanhSach();
    });

    $(document).on('click', '#btnSuCoMoi', function () {
        moForm(null);
        boChiTiet();
        $form.find('[name=tieuDe]').trigger('focus');
    });

    $(document).on('click', '#dsSuCo .tdht-dong-su-co', function () {
        var id = Number(this.dataset.id);
        // Bấm lại sự cố đang mở thì thu gọn và quay về thêm mới
        if (id === idMoRong) { moForm(null); boChiTiet(); return; }
        taiChiTiet(id);
    });

    $(document).on('change', '#formSuCo [name=trangThai]', function () {
        // Chọn "Đã khắc phục" thì gợi ý giờ kết thúc là bây giờ; người dùng sửa lại được
        if (this.value === 'DA_KHAC_PHUC' && !$form.find('[name=ketThucLuc]').val())
            $form.find('[name=ketThucLuc]').val(bayGioInput());
        anHienKetThuc();
    });

    $(document).on('change', '#inputFileSuCo', function () {
        themFile(this.files, false);
        this.value = '';   // xoá chọn để lần chọn sau (kể cả cùng file) vẫn ăn sự kiện change
    });

    $(document).on('click', '#dsFileChon .tdht-xoa-file', function () {
        fileDinhKem.splice(Number(this.dataset.i), 1);
        veFileChon();
    });

    $(document).on('click', '#vungDanFile', function () { this.focus(); });

    // Ctrl+V ở bất kỳ đâu trong hộp sự cố: chỉ chặn khi clipboard có file, dán chữ vào ô nhập vẫn bình thường
    $(document).on('paste', '#modalSuCo', function (e) {
        var cd = e.originalEvent.clipboardData;
        if (!cd || !cd.files || !cd.files.length) return;
        e.preventDefault();
        themFile(cd.files, true);
        var $vung = $('#vungDanFile').addClass('da-dan');
        setTimeout(function () { $vung.removeClass('da-dan'); }, 600);
    });

    $(document).on('submit', '#formSuCo', function (e) {
        e.preventDefault();
        if (dangLuu) return;
        if (!$form.find('[name=tieuDe]').val().trim()) { thongBao('Nhập tiêu đề sự cố.', true); return; }
        if (!$form.find('[name=batDauLuc]').val()) { thongBao('Nhập thời điểm bắt đầu.', true); return; }
        var loiFile = kiemTraFileChon(fileDinhKem);
        if (loiFile) { thongBao(loiFile, true); return; }

        // File lấy từ danh sách JS giữ (chọn + dán); token chống giả mạo do antiforgery-ajax.js gắn vào header
        var duLieu = new FormData($form[0]);
        fileDinhKem.forEach(function (f) { duLieu.append('files', f, f.name); });
        duLieu.append('maHeThong', maHeThong);
        duLieu.append('maGui', maGui);
        if ($form.find('[name=trangThai]').val() !== 'DA_KHAC_PHUC') duLieu.delete('ketThucLuc');

        var $nut = $('#btnLuuSuCo');
        var nhanCu = $nut.html();
        dangLuu = true;
        $nut.prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i> Đang lưu...');

        $.ajax({ url: '/TheoDoiHeThong/SuCo/Luu', method: 'POST', data: duLieu, processData: false, contentType: false })
            .done(function (res) {
                if (!res.thanhCong) { thongBao(res.thongBao || 'Không lưu được.', true); return; }
                var laThemMoi = !idDangChon;
                var id = res.duLieu ? res.duLieu.idSuCo : idDangChon;
                capNhatTrangThaiHeThong(res.duLieu ? res.duLieu.soSuCoMo : null);
                var thongBaoCu = res.thongBao;
                // Thêm mới xong: form bên trái trống lại để nhập sự cố tiếp, bên phải mở sẵn sự cố vừa thêm
                taiDanhSach().then(function () {
                    if (!id) { thongBao(thongBaoCu); return; }
                    layJson('/TheoDoiHeThong/SuCo/' + id).then(function (ct) {
                        if (!ct.thanhCong) return;
                        moForm(laThemMoi ? null : ct.duLieu);
                        veChiTiet(ct.duLieu);
                        thongBao(thongBaoCu);
                    });
                });
            })
            .fail(function (xhr) {
                // 413 = vượt giới hạn dung lượng ở nginx/Kestrel, trước khi tới action
                thongBao(xhr.status === 413 ? 'File quá lớn, máy chủ từ chối nhận.' : 'Lỗi kết nối máy chủ, dữ liệu chưa được lưu.', true);
            })
            .always(function () {
                dangLuu = false;
                $nut.prop('disabled', false).html(nhanCu);
            });
    });
})(jQuery);
