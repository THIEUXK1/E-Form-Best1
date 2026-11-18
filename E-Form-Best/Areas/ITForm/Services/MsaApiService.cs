using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Bổ sung sức khoẻ tủ đĩa HPE MSA qua API HTTPS (XML) — SNMP của MSA không đưa ổ cứng / pool ra.
    /// Dùng tài khoản role monitor (chỉ đọc) trong .env: MsaApi__User / MsaApi__Pass (ghi đè: MsaApi__BPVN__User ...).
    /// Đăng nhập: /api/login/{băm "user_pass"} — MSA 2060 băm SHA-256, MSA 2040 băm MD5 → thử lần lượt.
    /// Đọc: show system / enclosures / disks / disk-groups / pools, rồi /api/exit để không giữ phiên trên controller.
    /// Kết quả: vài mục tóm tắt vào DsMuc (để tính trạng thái + nhật ký) và KetQua.Msa (sơ đồ khay ổ, disk group, pool cho trang).
    /// </summary>
    public static class MsaApiService
    {
        // Controller MSA dùng chứng chỉ tự ký; chỉ gọi tới IP SAN nội bộ đã khai trong KK_Server
        private static readonly HttpClient Http = new(new SocketsHttpHandler
        {
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        { Timeout = TimeSpan.FromSeconds(30) };

        private const decimal MotTb = 1024m * 1024 * 1024 * 1024;

        // ===== Dữ liệu cho sơ đồ trên trang (serialize vào KK_ServerSucKhoe.du_lieu, khoá "msa") =====

        /// <summary>Khung đĩa (enclosure): SoCot x SoHang khe, đánh số khe từ 1 trái → phải, trên → dưới.</summary>
        public record Khung(int Id, string? MoTa, int SoKhe, int SoCot, int SoHang, string TrangThai);

        /// <summary>1 ổ trong khe. VaiTro: DuLieu / DuPhong / ChuaDung / BiLoai.</summary>
        public record Khay(int IdKhung, int Khe, string VaiTro, string TrangThai, string? DiskGroup, string? Pool,
            string? DungLuong, string? Loai, string? NhietDo, Dictionary<string, string> ChiTiet);

        public record DiskGroup(string Ten, string? Pool, string? Raid, int SoO, string? DungLuong, string? Trong, string TrangThai, string MoTa);

        public record Pool(string Ten, decimal? TongTb, decimal? DaDungTb, string TrangThai, int SoDiskGroup);

        /// <summary>Thành phần MSA tự báo không khoẻ (unhealthy-component): cổng mạng, quạt, nguồn...</summary>
        public record ThanhPhanLoi(string Ten, string TrangThai, string? LyDo, string? KhuyenNghi);

        public class ThongTin
        {
            public List<Khung> DsKhung { get; } = new();
            public List<Khay> DsKhay { get; } = new();
            public List<DiskGroup> DsDiskGroup { get; } = new();
            public List<Pool> DsPool { get; } = new();
            public List<ThanhPhanLoi> DsThanhPhanLoi { get; } = new();
        }

        /// <summary>Tài khoản theo công ty, rồi cấu hình chung; null = chưa cấu hình → chỉ dùng SNMP.</summary>
        public static (string User, string Pass)? TaiKhoan(IConfiguration cfg, string? congTy)
        {
            string? Doc(string khoa) => (congTy != null ? cfg[$"MsaApi:{congTy}:{khoa}"] : null) is { Length: > 0 } r ? r : cfg[$"MsaApi:{khoa}"];
            var user = Doc("User");
            var pass = Doc("Pass");
            return string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(pass) ? null : (user.Trim(), pass);
        }

        /// <summary>
        /// Thêm ổ cứng / disk group / pool / dung lượng vào kết quả SNMP. Trả thông báo lỗi (null = đọc được);
        /// lỗi API không làm hỏng phần SNMP đã có, chỉ thêm 1 mục cảnh báo.
        /// </summary>
        public static async Task<string?> BoSungAsync(ServerSnmpService.KetQua kq, string ip, string user, string pass, CancellationToken ct)
        {
            var goc = $"https://{ip}/api/";
            string? khoa;
            try { khoa = await DangNhapAsync(goc, user, pass, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException && !ct.IsCancellationRequested)
            {
                return ThemLoi(kq, $"Không kết nối được API HTTPS {ip}: {ex.Message}");
            }
            if (khoa == null) return ThemLoi(kq, $"Tài khoản MSA API bị từ chối trên {ip} (sai user/mật khẩu hoặc chưa tạo tài khoản monitor).");

            try
            {
                var system = await DocAsync(goc, "show/system", khoa, ct);
                var enclosures = await DocAsync(goc, "show/enclosures", khoa, ct);
                var disks = await DocAsync(goc, "show/disks", khoa, ct);
                var dgs = await DocAsync(goc, "show/disk-groups", khoa, ct);
                var pools = await DocAsync(goc, "show/pools", khoa, ct);

                var tt = new ThongTin();
                kq.Msa = tt;
                kq.GhiChu = null;
                HeThong(kq, DsDoiTuong(system, "system"));
                ThanhPhanKhongKhoe(kq, tt, DsDoiTuong(system, "unhealthy-component").Concat(DsDoiTuong(enclosures, "unhealthy-component")));
                KhungDia(tt, DsDoiTuong(enclosures, "enclosures"));
                OCung(kq, tt, DsDoiTuong(disks, "drives"));
                DiskGroupVaPool(kq, tt, DsDoiTuong(dgs, "disk-groups"), DsDoiTuong(pools, "pools"));
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                return ThemLoi(kq, $"Đọc API MSA {ip} lỗi: {ex.Message}");
            }
            finally
            {
                try { using var r = await GuiAsync(goc + "exit", khoa, CancellationToken.None); } catch { /* phiên tự hết hạn */ }
            }
        }

        private static string ThemLoi(ServerSnmpService.KetQua kq, string loi)
        {
            kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "API quản trị MSA", ServerSnmpService.CanhBao, loi));
            return loi;
        }

        private static async Task<string?> DangNhapAsync(string goc, string user, string pass, CancellationToken ct)
        {
            var chuoi = Encoding.UTF8.GetBytes(user + "_" + pass);
            foreach (var bam in new[] { SHA256.HashData(chuoi), MD5.HashData(chuoi) })
            {
                using var r = await GuiAsync(goc + "login/" + Convert.ToHexString(bam).ToLowerInvariant(), null, ct);
                if (!r.IsSuccessStatusCode) continue;
                var st = DsDoiTuong(XDocument.Parse(await r.Content.ReadAsStringAsync(ct)), "status").FirstOrDefault();
                if (st != null && st.GetValueOrDefault("response-type-numeric") == "0" && !string.IsNullOrWhiteSpace(st.GetValueOrDefault("response")))
                    return st["response"];
            }
            return null;
        }

        private static async Task<HttpResponseMessage> GuiAsync(string url, string? khoa, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (khoa != null)
            {
                // MSA 2060 đọc header sessionKey, MSA 2040 đọc cookie wbisessionkey — gửi cả hai
                req.Headers.TryAddWithoutValidation("sessionKey", khoa);
                req.Headers.TryAddWithoutValidation("Cookie", "wbisessionkey=" + khoa);
            }
            return await Http.SendAsync(req, ct);
        }

        private static async Task<XDocument> DocAsync(string goc, string lenh, string khoa, CancellationToken ct)
        {
            using var r = await GuiAsync(goc + lenh, khoa, ct);
            r.EnsureSuccessStatusCode();
            return XDocument.Parse(await r.Content.ReadAsStringAsync(ct));
        }

        /// <summary>Mỗi OBJECT basetype=... (kể cả lồng trong object khác) → từ điển PROPERTY name → giá trị.</summary>
        private static List<Dictionary<string, string>> DsDoiTuong(XDocument xml, string basetype)
            => xml.Descendants("OBJECT").Where(o => (string?)o.Attribute("basetype") == basetype)
                .Select(o => o.Elements("PROPERTY")
                    .GroupBy(p => (string?)p.Attribute("name") ?? "")
                    .ToDictionary(g => g.Key, g => g.First().Value.Trim()))
                .ToList();

        /// <summary>health: OK / Degraded / Fault / Unknown / N/A.</summary>
        private static string TrangThaiHealth(string? health) => (health ?? "").ToUpperInvariant() switch
        {
            "OK" => ServerSnmpService.Ok,
            "DEGRADED" => ServerSnmpService.CanhBao,
            "FAULT" => ServerSnmpService.Loi,
            _ => ServerSnmpService.KhongRo
        };

        private static string Nang(string a, string b)
        {
            int Hang(string x) => x switch { ServerSnmpService.Loi => 3, ServerSnmpService.CanhBao => 2, ServerSnmpService.Ok => 1, _ => 0 };
            return Hang(a) >= Hang(b) ? a : b;
        }

        private static string? Gt(Dictionary<string, string> d, string khoa)
            => d.TryGetValue(khoa, out var v) && !string.IsNullOrWhiteSpace(v) && v != "N/A" ? v : null;

        private static int So(Dictionary<string, string> d, string khoa) => int.TryParse(Gt(d, khoa), out var n) ? n : 0;

        private static void HeThong(ServerSnmpService.KetQua kq, List<Dictionary<string, string>> ds)
        {
            var s = ds.FirstOrDefault();
            if (s == null) return;
            if (Gt(s, "system-name") is { } ten) kq.ThongTin["Tên hệ thống (MSA)"] = ten;
            if (Gt(s, "product-id") is { } model) kq.Model ??= model;
            if (Gt(s, "midplane-serial-number") is { } sn) kq.Serial ??= sn;
            kq.Hang ??= Gt(s, "vendor-name");
            var tt = TrangThaiHealth(Gt(s, "health"));
            kq.DsMuc.Add(new ServerSnmpService.Muc("Tổng thể", "Tình trạng hệ thống (API)", tt,
                tt == ServerSnmpService.Ok ? "OK" : Noi(Gt(s, "health"), Gt(s, "health-reason"))));
        }

        /// <summary>MSA tự liệt kê thành phần không khoẻ kèm lý do + khuyến nghị — đây là chỗ giải thích "Degraded".</summary>
        private static void ThanhPhanKhongKhoe(ServerSnmpService.KetQua kq, ThongTin tt, IEnumerable<Dictionary<string, string>> ds)
        {
            foreach (var c in ds.GroupBy(x => Gt(x, "component-id") ?? Gt(x, "primary-key") ?? "").Select(g => g.First()))
            {
                var ten = Gt(c, "component-id") ?? Gt(c, "component-type") ?? "Thành phần";
                var t = TrangThaiHealth(Gt(c, "health"));
                if (t == ServerSnmpService.KhongRo) t = ServerSnmpService.CanhBao;
                var lyDo = Gt(c, "health-reason");
                tt.DsThanhPhanLoi.Add(new ThanhPhanLoi(ten, t, lyDo, Gt(c, "health-recommendation")));
                kq.DsMuc.Add(new ServerSnmpService.Muc("Tổng thể", ten, t, lyDo ?? Gt(c, "health")));
            }
        }

        private static void KhungDia(ThongTin tt, List<Dictionary<string, string>> ds)
        {
            foreach (var e in ds.OrderBy(x => So(x, "enclosure-id")))
            {
                var soKhe = So(e, "slots");
                if (soKhe <= 0) soKhe = So(e, "number-of-disks");
                var cot = So(e, "columns");
                var hang = So(e, "rows");
                if (cot <= 0 || hang <= 0) { cot = soKhe; hang = 1; }
                tt.DsKhung.Add(new Khung(So(e, "enclosure-id"), Gt(e, "description") ?? Gt(e, "model"), soKhe, cot, hang, TrangThaiHealth(Gt(e, "health"))));
            }
        }

        /// <summary>Từng ổ: health + usage (VIRTUAL POOL / GLOBAL SP / DEDICATED SP / AVAIL / LEFTOVR / FAILED).</summary>
        private static void OCung(ServerSnmpService.KetQua kq, ThongTin tt, List<Dictionary<string, string>> ds)
        {
            var tong = ServerSnmpService.Ok;
            foreach (var d in ds.OrderBy(x => ViTri(Gt(x, "location"))))
            {
                var viTri = Gt(d, "location") ?? Gt(d, "durable-id") ?? "?";
                var usage = (Gt(d, "usage") ?? "").ToUpperInvariant();
                var t = TrangThaiHealth(Gt(d, "health"));
                var vaiTro = usage switch
                {
                    var u when u.Contains("LEFTOVR") || u.Contains("FAILED") => "BiLoai",
                    var u when u.EndsWith(" SP") || u.Contains("SPARE") => "DuPhong",
                    "AVAIL" => "ChuaDung",
                    _ => "DuLieu"
                };
                if (vaiTro == "BiLoai") t = ServerSnmpService.Loi;
                var job = Gt(d, "current-job") ?? Gt(d, "job-running");
                // Dựng lại RAID trên ổ (RCON / CPYBK) = mảng đang thiếu ổ, cảnh báo cho tới khi xong
                if (t == ServerSnmpService.Ok && job != null && (job.Contains("RCON") || job.Contains("CPYBK"))) t = ServerSnmpService.CanhBao;

                var chiTiet = new Dictionary<string, string>();
                void Them(string nhan, string? v) { if (!string.IsNullOrWhiteSpace(v)) chiTiet[nhan] = v; }
                Them("Vị trí", viTri);
                Them("Hãng / model", Noi(Gt(d, "vendor"), Gt(d, "model")));
                Them("Serial", Gt(d, "serial-number"));
                Them("Dung lượng", Gt(d, "size"));
                Them("Loại", Noi(Gt(d, "description"), Gt(d, "storage-tier")));
                Them("Disk group / pool", Noi(Gt(d, "disk-group"), Gt(d, "storage-pool-name")));
                Them("Vai trò", Gt(d, "usage"));
                Them("Trạng thái", Noi(Gt(d, "status"), Gt(d, "led-status")));
                Them("Sức khoẻ", Noi(Gt(d, "health"), Gt(d, "health-reason")));
                if (t != ServerSnmpService.Ok) Them("Khuyến nghị", Gt(d, "health-recommendation"));
                Them("Nhiệt độ", Gt(d, "temperature"));
                if (long.TryParse(Gt(d, "power-on-hours"), out var gio)) Them("Giờ hoạt động", $"{gio:N0} giờ (~{gio / 8760.0:0.#} năm)");
                Them("SSD còn lại", Gt(d, "ssd-life-left"));
                Them("Tác vụ đang chạy", job);
                if (long.TryParse(Gt(d, "error"), out var loi) && loi > 0) Them("Lỗi", loi.ToString());

                var (khung, khe) = ViTri(viTri);
                tt.DsKhay.Add(new Khay(khung, khe, vaiTro, t, Gt(d, "disk-group"), Gt(d, "storage-pool-name"),
                    Gt(d, "size"), Gt(d, "description"), Gt(d, "temperature"), chiTiet));

                // Chỉ ổ bất thường mới thành mục riêng (để vào tóm tắt + nhật ký); ổ tốt xem trên sơ đồ khay
                if (t != ServerSnmpService.Ok)
                {
                    kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "Ổ " + viTri, t,
                        Noi(Gt(d, "size"), Gt(d, "disk-group"), Gt(d, "health"), Gt(d, "health-reason"), vaiTro == "BiLoai" ? "bị loại khỏi mảng" : null), chiTiet));
                    tong = Nang(tong, t);
                }
            }

            var soDuPhong = tt.DsKhay.Count(x => x.VaiTro == "DuPhong");
            var soLoi = tt.DsKhay.Count(x => x.TrangThai != ServerSnmpService.Ok);
            kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "Tổng số ổ", ds.Count == 0 ? ServerSnmpService.KhongRo : tong,
                Noi($"{ds.Count} ổ", $"{tt.DsKhay.Count(x => x.VaiTro == "DuLieu")} dữ liệu", $"{soDuPhong} dự phòng",
                    tt.DsKhay.Count(x => x.VaiTro == "ChuaDung") is > 0 and var cd ? $"{cd} chưa dùng" : null,
                    soLoi > 0 ? $"{soLoi} bất thường" : null)));
            // Không còn ổ dự phòng = hỏng ổ tiếp theo phải chờ thay tay mới dựng lại RAID
            kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "Ổ dự phòng", soDuPhong > 0 ? ServerSnmpService.Ok : ServerSnmpService.CanhBao,
                soDuPhong > 0 ? $"{soDuPhong} ổ sẵn sàng" : "Không có ổ dự phòng"));
        }

        /// <summary>Disk group + pool → bảng trên sơ đồ; dung lượng tủ = tổng pool.</summary>
        private static void DiskGroupVaPool(ServerSnmpService.KetQua kq, ThongTin tt, List<Dictionary<string, string>> dgs, List<Dictionary<string, string>> pools)
        {
            var ttRaid = ServerSnmpService.Ok;
            foreach (var g in dgs.OrderBy(x => Gt(x, "pool")).ThenBy(x => Gt(x, "name")))
            {
                var (t, moTa) = TrangThaiDiskGroup(g);
                var ten = Gt(g, "name") ?? "?";
                tt.DsDiskGroup.Add(new DiskGroup(ten, Gt(g, "pool"), Gt(g, "raidtype"), So(g, "diskcount"), Gt(g, "size"), Gt(g, "freespace"), t, moTa));
                if (t != ServerSnmpService.Ok)
                {
                    kq.DsMuc.Add(new ServerSnmpService.Muc("RAID", "Disk group " + ten, t, Noi(Gt(g, "raidtype"), moTa)));
                    ttRaid = Nang(ttRaid, t);
                }
            }
            if (dgs.Count > 0)
                kq.DsMuc.Add(new ServerSnmpService.Muc("RAID", "Disk group", ttRaid,
                    $"{dgs.Count} disk group" + (ttRaid == ServerSnmpService.Ok ? " — bình thường" : $" — {tt.DsDiskGroup.Count(x => x.TrangThai != ServerSnmpService.Ok)} bất thường")));

            decimal tong = 0, trong = 0;
            foreach (var p in pools.OrderBy(x => Gt(x, "name")))
            {
                var ten = Gt(p, "name") ?? "?";
                var t = TrangThaiHealth(Gt(p, "health"));
                foreach (var g in tt.DsDiskGroup.Where(x => x.Pool == ten)) t = Nang(t, g.TrangThai);
                decimal? tongTb = null, dungTb = null;
                // *-numeric tính theo block 512 byte
                if (decimal.TryParse(Gt(p, "total-size-numeric"), NumberStyles.Number, CultureInfo.InvariantCulture, out var ts) &&
                    decimal.TryParse(Gt(p, "total-avail-numeric"), NumberStyles.Number, CultureInfo.InvariantCulture, out var ta) && ts > 0)
                {
                    tong += ts * 512;
                    trong += ta * 512;
                    tongTb = Math.Round(ts * 512 / MotTb, 2);
                    dungTb = Math.Round((ts - ta) * 512 / MotTb, 2);
                    var tyLe = (ts - ta) / ts;
                    // Pool thin-provisioning đầy là volume ghi lỗi ngay; ngưỡng MSA ~ 96–99 % nên tự cảnh báo sớm ở 90 %
                    var ttDl = tyLe >= 0.95m ? ServerSnmpService.Loi : tyLe >= 0.9m ? ServerSnmpService.CanhBao : ServerSnmpService.Ok;
                    t = Nang(t, ttDl);
                    kq.DsMuc.Add(new ServerSnmpService.Muc("Dung lượng", "Pool " + ten, ttDl, $"{dungTb:0.##} / {tongTb:0.##} TB ({tyLe:P0})"));
                }
                tt.DsPool.Add(new Pool(ten, tongTb, dungTb, t, tt.DsDiskGroup.Count(x => x.Pool == ten)));
            }
            if (tong > 0)
            {
                kq.DungLuongTb = Math.Round(tong / MotTb, 2);
                kq.DaDungTb = Math.Round((tong - trong) / MotTb, 2);
            }
            var dsRaid = tt.DsDiskGroup.Select(x => x.Raid).Where(x => x != null).ToList();
            if (dsRaid.Count > 0) kq.RaidTomTat ??= string.Join(" + ", dsRaid.GroupBy(x => x).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
        }

        /// <summary>status: FTOL bình thường; FTDN thiếu ổ còn chịu lỗi; CRIT thiếu ổ hết chịu lỗi; OFFL / QTxx / STOP mất mảng.</summary>
        private static (string tt, string moTa) TrangThaiDiskGroup(Dictionary<string, string> g)
        {
            var st = (Gt(g, "status") ?? "").ToUpperInvariant();
            var job = Gt(g, "current-job") ?? Gt(g, "job-running");
            var tienDo = Gt(g, "current-job-completion");
            var (tt, moTa) = st switch
            {
                "FTOL" or "UP" => (ServerSnmpService.Ok, "Bình thường"),
                "FTDN" => (ServerSnmpService.CanhBao, "Suy giảm — thiếu ổ, vẫn chịu lỗi"),
                "CRIT" => (ServerSnmpService.Loi, "NGUY CẤP — hỏng thêm 1 ổ là mất dữ liệu"),
                "OFFL" or "STOP" or "QTOF" or "QTCR" or "QTDN" => (ServerSnmpService.Loi, "Mất mảng / offline (" + st + ")"),
                _ => (TrangThaiHealth(Gt(g, "health")), st.Length > 0 ? st : "Không rõ")
            };
            // VRSC/MDRC = quét lỗi định kỳ (scrub), không đáng lo; RCON = đang dựng lại sau khi thay ổ
            var tenJob = job switch
            {
                null => null,
                var j when j.Contains("RCON") => "đang dựng lại RAID",
                var j when j.Contains("VRSC") || j.Contains("MDRC") || j.Contains("VRFY") => "đang quét kiểm tra (scrub)",
                var j when j.Contains("INIT") => "đang khởi tạo",
                var j => j
            };
            if (tenJob == "đang dựng lại RAID" && tt == ServerSnmpService.Ok) tt = ServerSnmpService.CanhBao;
            if (tenJob != null) moTa += $" · {tenJob}" + (tienDo != null ? $" {tienDo}" : "");
            return (tt, moTa);
        }

        /// <summary>"1.10" → (1, 10).</summary>
        private static (int, int) ViTri(string? s)
        {
            var p = (s ?? "").Split('.');
            return (p.Length > 0 && int.TryParse(p[0], out var a) ? a : 999, p.Length > 1 && int.TryParse(p[1], out var b) ? b : 999);
        }

        private static string? Noi(params string?[] ds)
        {
            var s = string.Join(" · ", ds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
            return s.Length > 0 ? s : null;
        }
    }
}
