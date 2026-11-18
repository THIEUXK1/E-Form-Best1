using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Bổ sung sức khoẻ tủ đĩa HPE MSA qua API HTTPS (XML) — SNMP của MSA không đưa ổ cứng / pool ra.
    /// Dùng tài khoản role monitor (chỉ đọc) trong .env: MsaApi__User / MsaApi__Pass (ghi đè: MsaApi__BPVN__User ...).
    /// Đăng nhập: /api/login/{băm "user_pass"} — MSA 2050/2060 băm SHA-256, MSA 2040 băm MD5 → thử lần lượt.
    /// Đọc: show system / disks / disk-groups / pools, rồi /api/exit để không giữ phiên trên controller.
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
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                return ThemLoi(kq, $"Không kết nối được API HTTPS {ip}: {ex.Message}");
            }
            if (khoa == null) return ThemLoi(kq, $"Tài khoản MSA API bị từ chối trên {ip} (sai user/mật khẩu hoặc chưa tạo tài khoản monitor).");

            try
            {
                var system = await DocAsync(goc, "show/system", khoa, ct);
                var disks = await DocAsync(goc, "show/disks", khoa, ct);
                var dgs = await DocAsync(goc, "show/disk-groups", khoa, ct);
                var pools = await DocAsync(goc, "show/pools", khoa, ct);

                kq.GhiChu = null;
                if (!kq.Nguon!.Contains("API")) kq.Nguon += " (SNMP + API)";
                HeThong(kq, system);
                OCung(kq, disks);
                DiskGroupVaPool(kq, dgs, pools);
                if (disks.Count == 0) kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "Danh sách ổ", ServerSnmpService.KhongRo, "API không trả ổ nào"));
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
                var ds = DsDoiTuong(XDocument.Parse(await r.Content.ReadAsStringAsync(ct)), "status");
                var st = ds.FirstOrDefault();
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

        private static async Task<List<Dictionary<string, string>>> DocAsync(string goc, string lenh, string khoa, CancellationToken ct)
        {
            using var r = await GuiAsync(goc + lenh, khoa, ct);
            r.EnsureSuccessStatusCode();
            var xml = XDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            var basetype = lenh[(lenh.LastIndexOf('/') + 1)..] switch { "disks" => "drives", var x => x };
            return DsDoiTuong(xml, basetype);
        }

        /// <summary>Mỗi OBJECT basetype=... → từ điển PROPERTY name → giá trị.</summary>
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

        private static string? Gt(Dictionary<string, string> d, string khoa)
            => d.TryGetValue(khoa, out var v) && !string.IsNullOrWhiteSpace(v) && v != "N/A" ? v : null;

        private static void HeThong(ServerSnmpService.KetQua kq, List<Dictionary<string, string>> ds)
        {
            var s = ds.FirstOrDefault();
            if (s == null) return;
            var ten = Gt(s, "system-name");
            if (ten != null) kq.ThongTin["Tên hệ thống (MSA)"] = ten;
            if (Gt(s, "product-id") is { } model) kq.Model ??= model;
            if (Gt(s, "midplane-serial-number") is { } sn) kq.Serial ??= sn;
            kq.Hang ??= Gt(s, "vendor-name");
            var tt = TrangThaiHealth(Gt(s, "health"));
            var lyDo = Gt(s, "health-reason");
            kq.DsMuc.Add(new ServerSnmpService.Muc("Tổng thể", "Tình trạng hệ thống (API)", tt,
                tt == ServerSnmpService.Ok ? "OK" : Noi(Gt(s, "health"), lyDo, Gt(s, "health-recommendation"))));
        }

        /// <summary>Từng ổ: health + usage (LEFTOVR / FAILED = ổ bị loại khỏi mảng).</summary>
        private static void OCung(ServerSnmpService.KetQua kq, List<Dictionary<string, string>> ds)
        {
            foreach (var d in ds.OrderBy(x => ViTri(Gt(x, "location"))))
            {
                var viTri = Gt(d, "location") ?? Gt(d, "durable-id") ?? "?";
                var usage = Gt(d, "usage") ?? "";
                var tt = TrangThaiHealth(Gt(d, "health"));
                var uU = usage.ToUpperInvariant();
                if (uU.Contains("LEFTOVR") || uU.Contains("FAILED")) tt = ServerSnmpService.Loi;
                var job = Gt(d, "current-job") ?? Gt(d, "job-running");
                // Dựng lại RAID trên ổ (RCON / CPYBK) = mảng đang thiếu ổ, cảnh báo cho tới khi xong
                if (tt == ServerSnmpService.Ok && job != null && (job.Contains("RCON") || job.Contains("CPYBK"))) tt = ServerSnmpService.CanhBao;

                var dungLuong = Gt(d, "size");
                var nhom = Gt(d, "disk-group");
                var vaiTro = uU switch
                {
                    var u when u.Contains("SPARE") => "ổ dự phòng",
                    "AVAIL" => "chưa dùng",
                    _ => null
                };
                var giaTri = Noi(dungLuong, Gt(d, "description"), nhom ?? vaiTro, Gt(d, "temperature"),
                    tt == ServerSnmpService.Ok ? null : Noi(Gt(d, "health"), Gt(d, "health-reason")));

                var chiTiet = new Dictionary<string, string>();
                void Them(string nhan, string? v) { if (!string.IsNullOrWhiteSpace(v)) chiTiet[nhan] = v; }
                Them("Hãng / model", Noi(Gt(d, "vendor"), Gt(d, "model")));
                Them("Serial", Gt(d, "serial-number"));
                Them("Dung lượng", dungLuong);
                Them("Loại", Noi(Gt(d, "description"), Gt(d, "storage-tier")));
                Them("Disk group / pool", Noi(nhom, Gt(d, "storage-pool-name")));
                Them("Vai trò", usage);
                Them("Trạng thái", Noi(Gt(d, "status"), (Gt(d, "led-status") ?? "").Trim()));
                Them("Sức khoẻ", Noi(Gt(d, "health"), Gt(d, "health-reason")));
                Them("Khuyến nghị", Gt(d, "health-recommendation"));
                Them("Nhiệt độ", Gt(d, "temperature"));
                if (long.TryParse(Gt(d, "power-on-hours"), out var gio)) Them("Giờ hoạt động", $"{gio:N0} giờ (~{gio / 8760.0:0.#} năm)");
                Them("SSD còn lại", Gt(d, "ssd-life-left"));
                Them("Tác vụ đang chạy", job);
                if (long.TryParse(Gt(d, "error"), out var loi) && loi > 0) Them("Lỗi", loi.ToString());

                kq.DsMuc.Add(new ServerSnmpService.Muc("Ổ cứng", "Ổ " + viTri, tt, giaTri, chiTiet.Count > 0 ? chiTiet : null));
            }
        }

        /// <summary>Pool → bảng RAID (mỗi pool 1 khối, disk group là ổ logic); dung lượng tủ = tổng pool.</summary>
        private static void DiskGroupVaPool(ServerSnmpService.KetQua kq, List<Dictionary<string, string>> dgs, List<Dictionary<string, string>> pools)
        {
            decimal tong = 0, trong = 0;
            var tomTat = new List<string>();
            foreach (var p in pools.OrderBy(x => Gt(x, "name")))
            {
                var ten = Gt(p, "name") ?? "?";
                var ttPool = TrangThaiHealth(Gt(p, "health"));
                var dsO = new List<ServerSnmpService.ORaid>();
                foreach (var g in dgs.Where(x => Gt(x, "pool") == ten).OrderBy(x => Gt(x, "name")))
                {
                    var (tt, moTa) = TrangThaiDiskGroup(g);
                    if (tt == ServerSnmpService.Loi || (tt == ServerSnmpService.CanhBao && ttPool == ServerSnmpService.Ok)) ttPool = tt;
                    dsO.Add(new ServerSnmpService.ORaid(
                        $"{Gt(g, "name")} ({Gt(g, "diskcount")} ổ)", Gt(g, "raidtype"), Gt(g, "size"), tt, moTa));
                    tomTat.Add(Gt(g, "raidtype") ?? "");
                }
                kq.DsRaid.Add(new ServerSnmpService.BoDieuKhienRaid("Pool " + ten, null, ttPool, dsO));

                // *-numeric tính theo block 512 byte
                if (decimal.TryParse(Gt(p, "total-size-numeric"), NumberStyles.Number, CultureInfo.InvariantCulture, out var t) &&
                    decimal.TryParse(Gt(p, "total-avail-numeric"), NumberStyles.Number, CultureInfo.InvariantCulture, out var a) && t > 0)
                {
                    tong += t * 512;
                    trong += a * 512;
                    var tyLe = (t - a) / t;
                    // Pool thin-provisioning đầy là volume ghi lỗi ngay, ngưỡng high-threshold MSA ~ 96–99 % nên tự cảnh báo sớm ở 90 %
                    var ttDl = tyLe >= 0.95m ? ServerSnmpService.Loi : tyLe >= 0.9m ? ServerSnmpService.CanhBao : ServerSnmpService.Ok;
                    kq.DsMuc.Add(new ServerSnmpService.Muc("Dung lượng", "Pool " + ten, ttDl,
                        $"{(t - a) * 512 / MotTb:0.##} / {t * 512 / MotTb:0.##} TB ({tyLe:P0})"));
                }
                kq.DsMuc.Add(new ServerSnmpService.Muc("RAID", "Pool " + ten, ttPool,
                    ttPool == ServerSnmpService.Ok ? $"{dsO.Count} disk group — bình thường"
                        : string.Join("; ", dsO.Where(x => x.TrangThai != ServerSnmpService.Ok).Select(x => $"{x.Ten}: {x.MoTa}"))));
            }
            if (tong > 0)
            {
                kq.DungLuongTb = Math.Round(tong / MotTb, 2);
                kq.DaDungTb = Math.Round((tong - trong) / MotTb, 2);
            }
            if (tomTat.Count > 0) kq.RaidTomTat ??= string.Join(" + ", tomTat.GroupBy(x => x).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
        }

        /// <summary>status: FTOL bình thường; FTDN thiếu ổ còn chịu lỗi; CRIT thiếu ổ hết chịu lỗi; OFFL / QTxx / STOP mất mảng.</summary>
        private static (string tt, string moTa) TrangThaiDiskGroup(Dictionary<string, string> g)
        {
            var st = (Gt(g, "status") ?? "").ToUpperInvariant();
            var job = Gt(g, "current-job") ?? Gt(g, "job-running");
            var tienDo = Gt(g, "current-job-completion");
            var (tt, moTa) = st switch
            {
                "FTOL" => (ServerSnmpService.Ok, "Bình thường"),
                "FTDN" => (ServerSnmpService.CanhBao, "Suy giảm — thiếu ổ, vẫn chịu lỗi"),
                "CRIT" => (ServerSnmpService.Loi, "NGUY CẤP — hỏng thêm 1 ổ là mất dữ liệu"),
                "OFFL" or "STOP" or "QTOF" or "QTCR" or "QTDN" => (ServerSnmpService.Loi, "Mất mảng / offline (" + st + ")"),
                "UP" => (ServerSnmpService.Ok, "Bình thường"),
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
            if (Gt(g, "freespace") is { } trong) moTa += $" · trống {trong}";
            return (tt, moTa);
        }

        /// <summary>"1.10" → (1, 10) để sắp đúng thứ tự khay.</summary>
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
