using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Quét sâu một máy in theo địa chỉ IP: ping, dò cổng dịch vụ in, rồi đọc toàn bộ nhóm API web
    /// của FUJIFILM Apeos để dựng "lý lịch" thiết bị (firmware, RAM, khay giấy, vật tư, nguồn điện,
    /// lịch sử lỗi và mức dùng suy ra từ đó).
    ///
    /// Khác với <see cref="MayInApiService"/> — nơi chỉ lấy vài chỉ số cần lưu hằng ngày — dịch vụ
    /// này đọc rộng và KHÔNG ghi gì vào CSDL, dùng cho màn hình xem tại chỗ.
    /// </summary>
    public class MayInQuetService
    {
        // Cổng dịch vụ in thường gặp. SNMP (161/UDP) không dò được bằng TCP nên không liệt kê ở đây.
        private static readonly (int Cong, string Ten)[] DanhSachCong =
        {
            (9100, "RAW (JetDirect)"),
            (515,  "LPD"),
            (631,  "IPP"),
            (80,   "HTTP"),
            (443,  "HTTPS")
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public MayInQuetService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<object> QuetAsync(string diaChiIp, string? model, CancellationToken ct = default)
        {
            var mang = await QuetMangAsync(diaChiIp, ct);
            var client = _httpClientFactory.CreateClient(MayInApiService.TenHttpClient);

            // Đa số máy ép HTTPS nhưng có máy chỉ mở API trên cổng 80 — dò một lần rồi dùng chung
            // cho cả nhóm endpoint, khỏi phải thử hai lần ở từng cái.
            var goc = $"https://{diaChiIp}";
            var counter = await LayJsonAsync(client, $"{goc}/home/api/billing-counter", ct);
            if (counter is null)
            {
                var gocHttp = $"http://{diaChiIp}";
                var thuHttp = await LayJsonAsync(client, $"{gocHttp}/home/api/billing-counter", ct);
                if (thuHttp is not null)
                {
                    goc = gocHttp;
                    counter = thuHttp;
                }
            }

            var about = await LayJsonAsync(client, $"{goc}/home/api/about", ct);
            var cauHinh = await LayJsonAsync(client, $"{goc}/home/api/device-configuration", ct);
            var khayGiay = await LayJsonAsync(client, $"{goc}/home/api/paper-tray", ct);
            var vatTu = await LayJsonAsync(client, $"{goc}/home/api/supplies-info", ct);
            var tietKiemDien = await LayJsonAsync(client, $"{goc}/home/api/power-saver-status", ct);
            var lichSuLoi = await LayJsonAsync(client, $"{goc}/home/api/faulthistory", ct);

            var thietBi = DocThietBi(about, cauHinh);
            var soDem = DocCounter(counter);
            var counterTong = LayCounterTong(counter);

            // Công suất tra theo Model trong danh mục, nếu trống thì theo tên máy tự khai qua API
            var congSuat = DocCongSuat(model, LayChuoi(about, "DevFrndlName"), counterTong);
            var trangMoiNgayKhuyenNghi = congSuat?.NgayKhuyenNghi;

            return new
            {
                mang,
                hoTroApi = about is not null || counter is not null,
                thietBi,
                khay = DocKhayGiay(khayGiay),
                vatTu = DocVatTu(vatTu),
                counter = soDem,
                nguonDien = DocNguonDien(tietKiemDien),
                congSuat,
                mucDung = DocMucDung(lichSuLoi, counterTong, trangMoiNgayKhuyenNghi),
                lichSuLoi = DocLichSuLoi(lichSuLoi)
            };
        }

        #region Ping + cổng

        /// <summary>
        /// Ping hàng loạt để biết máy nào đang bật, dùng cho việc xếp máy online lên đầu danh sách.
        ///
        /// Kết quả giữ trong bộ nhớ 60 giây: mỗi lần đổi bộ lọc là một lần gọi danh sách, ping lại
        /// gần trăm máy mỗi lần thì trang ì và mạng nhà máy lãnh đủ. Máy in bật/tắt không đổi theo
        /// từng giây nên 60 giây là đủ tươi.
        /// </summary>
        public async Task<Dictionary<string, bool>> PingNhieuAsync(IReadOnlyCollection<string> dsIp, CancellationToken ct = default)
        {
            const string khoaCache = "MayIn:TrangThaiPing";

            if (_cache.TryGetValue(khoaCache, out Dictionary<string, bool>? daCo)
                && daCo is not null
                && dsIp.All(daCo.ContainsKey))
            {
                return daCo;
            }

            var ketQua = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();

            await Parallel.ForEachAsync(
                dsIp,
                new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = ct },
                async (ip, token) => { ketQua[ip] = await PingMotMayAsync(ip, 1000, token); });

            var bang = new Dictionary<string, bool>(ketQua);
            _cache.Set(khoaCache, bang, TimeSpan.FromSeconds(60));
            return bang;
        }

        private static async Task<bool> PingMotMayAsync(string diaChiIp, int hetGioMs, CancellationToken ct)
        {
            try
            {
                using var ping = new Ping();
                var traLoi = await ping.SendPingAsync(diaChiIp, TimeSpan.FromMilliseconds(hetGioMs), cancellationToken: ct);
                return traLoi.Status == IPStatus.Success;
            }
            catch
            {
                // Tên máy không phân giải được, mạng chặn ICMP... đều coi như không tới được
                return false;
            }
        }

        private static async Task<object> QuetMangAsync(string diaChiIp, CancellationToken ct)
        {
            var song = false;
            long? thoiGian = null;
            int? ttl = null;
            var trangThai = "Không phản hồi";

            try
            {
                using var ping = new Ping();
                var traLoi = await ping.SendPingAsync(diaChiIp, TimeSpan.FromSeconds(2), cancellationToken: ct);
                song = traLoi.Status == IPStatus.Success;
                trangThai = song ? "Phản hồi" : traLoi.Status.ToString();
                if (song)
                {
                    thoiGian = traLoi.RoundtripTime;
                    // TTL còn lại cho biết gói tin đã đi qua mấy router (thiết bị thường xuất phát từ 64)
                    ttl = traLoi.Options?.Ttl;
                }
            }
            catch (Exception ex)
            {
                trangThai = "Lỗi ping: " + ex.Message;
            }

            var dsCong = new List<object>();
            foreach (var (cong, ten) in DanhSachCong)
            {
                dsCong.Add(new { cong, ten, mo = await CongMoAsync(diaChiIp, cong, ct) });
            }

            return new
            {
                song,
                trangThai,
                thoiGianMs = thoiGian,
                ttl,
                soRouter = ttl is null ? (int?)null : SoRouterDaQua(ttl.Value),
                cong = dsCong
            };
        }

        private static async Task<bool> CongMoAsync(string diaChiIp, int cong, CancellationToken ct)
        {
            try
            {
                using var tcp = new TcpClient();
                using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hetGio.CancelAfter(TimeSpan.FromMilliseconds(900));
                await tcp.ConnectAsync(diaChiIp, cong, hetGio.Token);
                return tcp.Connected;
            }
            catch
            {
                // Cổng đóng, bị chặn hay hết giờ đều quy về "không mở" — người xem không cần phân biệt
                return false;
            }
        }

        /// <summary>TTL xuất phát của thiết bị mạng thường là 64/128/255; hiệu số là số router đã qua.</summary>
        private static int SoRouterDaQua(int ttl)
        {
            int[] mocXuatPhat = { 64, 128, 255 };
            var moc = mocXuatPhat.FirstOrDefault(x => x >= ttl);
            return moc == 0 ? 0 : moc - ttl;
        }

        #endregion

        #region Đọc từng nhóm dữ liệu

        private static object? DocThietBi(JsonElement? about, JsonElement? cauHinh)
        {
            if (about is null && cauHinh is null) return null;

            string? Chuoi(JsonElement? goc, string khoa)
                => goc is not null && goc.Value.TryGetProperty(khoa, out var v) ? v.GetString() : null;

            var phienBan = new List<object>();
            if (cauHinh is not null
                && cauHinh.Value.TryGetProperty("SoftwareVersions", out var dsPb)
                && dsPb.ValueKind == JsonValueKind.Array)
            {
                foreach (var pb in dsPb.EnumerateArray())
                {
                    phienBan.Add(new
                    {
                        ten = pb.TryGetProperty("Name", out var n) ? n.GetString() : null,
                        soHieu = pb.TryGetProperty("Version", out var v) ? v.GetString() : null
                    });
                }
            }

            var oDia = new List<object>();
            if (cauHinh is not null
                && cauHinh.Value.TryGetProperty("StorageVolumes", out var dsO)
                && dsO.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in dsO.EnumerateArray())
                {
                    oDia.Add(new
                    {
                        ten = o.TryGetProperty("VolumeName", out var n) ? n.GetString() : null,
                        tong = o.TryGetProperty("TotalSize", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : (int?)null,
                        trong = o.TryGetProperty("FreeSpaceSize", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int?)null,
                        donVi = o.TryGetProperty("TotalSizeUnit", out var dv) ? dv.GetString() : "MB"
                    });
                }
            }

            return new
            {
                ten = Chuoi(about, "DevFrndlName"),
                serial = Chuoi(about, "SerialNumber"),
                hostName = Chuoi(about, "HostName"),
                ipChinh = Chuoi(about, "IPv4PrimaryAddress"),
                trangThai = Chuoi(about, "DeviceStatus"),
                viTriCauHinh = Chuoi(about, "Location"),
                nguoiQuanTri = Chuoi(about, "AdminName"),
                emailQuanTri = Chuoi(about, "AdminEmail"),
                phienBanHeThong = Chuoi(about, "SoftwareVersion"),
                ram = cauHinh is not null && cauHinh.Value.TryGetProperty("RAMSize", out var ram) && ram.ValueKind == JsonValueKind.Number
                      ? ram.GetInt32() : (int?)null,
                ramDonVi = cauHinh is not null && cauHinh.Value.TryGetProperty("RAMSizeUnit", out var rdv) ? rdv.GetString() : "MB",
                phienBan,
                oDia
            };
        }

        private static object? DocKhayGiay(JsonElement? khayGiay)
        {
            if (khayGiay is null
                || !khayGiay.Value.TryGetProperty("PaperTrays", out var ds)
                || ds.ValueKind != JsonValueKind.Array) return null;

            var dsKhay = new List<object>();
            foreach (var khay in ds.EnumerateArray())
            {
                dsKhay.Add(new
                {
                    ten = khay.TryGetProperty("NameId", out var n) ? n.GetString() : null,
                    khoGiay = khay.TryGetProperty("MediumSize", out var s) ? s.GetString() : null,
                    loaiGiay = khay.TryGetProperty("MediumType", out var t) ? t.GetString() : null,
                    khoHoTro = khay.TryGetProperty("MediumSizeSupported", out var hs) && hs.ValueKind == JsonValueKind.Array
                               ? hs.EnumerateArray().Select(x => x.GetString()).ToList()
                               : new List<string?>()
                });
            }

            return dsKhay;
        }

        private static object? DocVatTu(JsonElement? vatTu)
        {
            if (vatTu is null
                || !vatTu.Value.TryGetProperty("Supplies", out var ds)
                || ds.ValueKind != JsonValueKind.Array) return null;

            var dsVatTu = new List<object>();
            foreach (var muc in ds.EnumerateArray())
            {
                var conLai = muc.TryGetProperty("Remaining", out var r) && int.TryParse(r.GetString(), out var pt)
                             ? pt : (int?)null;

                // PageRemaining là chuỗi: số trang còn lại, hoặc "AT_LEAST_ONE" với vật tư không đo được
                var trangConLai = muc.TryGetProperty("PageRemaining", out var p) && int.TryParse(p.GetString(), out var tcl)
                                  ? tcl : (int?)null;

                dsVatTu.Add(new
                {
                    ten = muc.TryGetProperty("Name", out var n) ? n.GetString() : null,
                    trangThai = muc.TryGetProperty("State", out var s) ? s.GetString() : null,
                    conLai,
                    trangConLai,
                    ngayLap = muc.TryGetProperty("DateInstalled", out var d) ? d.GetString() : null,
                    loai = muc.TryGetProperty("ChangeableType", out var lt) ? lt.GetString() : null
                });
            }

            return dsVatTu;
        }

        private static object? DocCounter(JsonElement? counter)
        {
            if (counter is null
                || !counter.Value.TryGetProperty("UsageCounters", out var ds)
                || ds.ValueKind != JsonValueKind.Array) return null;

            var bang = new Dictionary<string, int>();
            foreach (var muc in ds.EnumerateArray())
            {
                var ten = muc.TryGetProperty("DomesticName", out var t) ? t.GetString() : null;
                if (ten is null || !muc.TryGetProperty("Count", out var c) || c.ValueKind != JsonValueKind.Number) continue;
                bang[ten] = c.GetInt32();
            }

            int? Lay(string khoa) => bang.TryGetValue(khoa, out var v) ? v : (int?)null;

            var inTong = Lay("PRINT_TOTAL_IMPRESSION");
            var copyTong = Lay("COPY_TOTAL_IMPRESSION");

            return new
            {
                inTong,
                copyTong,
                scanTong = Lay("SCAN_TOTAL_IMPRESSION"),
                scanMau = Lay("SCAN_TOTAL_COLOR_IMPRESSION"),
                scanTrangDen = Lay("SCAN_TOTAL_BW_IMPRESSION"),
                inMau = Lay("PRINT_TOTAL_COLOR_IMPRESSION"),
                inTrangDen = Lay("PRINT_TOTAL_BW_IMPRESSION"),
                tong = inTong is null && copyTong is null ? (int?)null : (inTong ?? 0) + (copyTong ?? 0)
            };
        }

        private static object? DocNguonDien(JsonElement? tietKiemDien)
        {
            if (tietKiemDien is null) return null;

            string? Chuoi(string khoa) => tietKiemDien.Value.TryGetProperty(khoa, out var v) ? v.GetString() : null;
            int? So(string khoa) => tietKiemDien.Value.TryGetProperty(khoa, out var v) && v.ValueKind == JsonValueKind.Number
                                    ? v.GetInt32() : (int?)null;

            return new
            {
                mayIn = Chuoi("PrinterStatus"),
                mayQuet = Chuoi("ScannerStatus"),
                banDieuKhien = Chuoi("PanelStatus"),
                phutChay = So("PrinterRunTime"),
                phutChoLenh = So("PrinterStandbyTime"),
                phutNgu = So("SleepTime"),
                phutTat = So("PowerOffTime")
            };
        }

        /// <summary>
        /// Công suất hãng công bố cho dòng máy này. Dùng record thay cho anonymous type vì
        /// <see cref="QuetAsync"/> còn phải đọc lại NgayKhuyenNghi để tính % của từng khoảng.
        /// </summary>
        public record CongSuat(
            string? Ten,
            int? AmpvKhuyenNghi,
            int? AmpvToiDa,
            int? DutyCycle,
            int? TuoiTho,
            int? NgayKhuyenNghi,
            int? NgayToiDa,
            int SoNgayLamViec,
            int? DaDung,
            double? PhanTramTuoiTho,
            string? Nguon);

        /// <summary>
        /// Tra công suất trong appsettings theo Model (bảng MayIn) rồi tới tên máy tự khai qua API.
        /// Không khai báo thì trả null để giao diện ẩn bảng — thà thiếu còn hơn đoán sai chỉ tiêu.
        /// </summary>
        private CongSuat? DocCongSuat(string? model, string? tenMayTheoApi, int? counterTong)
        {
            var bang = _configuration.GetSection("MayIn:SpecTheoModel");
            var spec = TimSpec(bang, model) ?? TimSpec(bang, tenMayTheoApi);
            if (spec is null) return null;

            var ampvKhuyenNghi = spec.GetValue<int?>("AmpvKhuyenNghi");
            var ampvToiDa = spec.GetValue<int?>("AmpvToiDa");
            var tuoiTho = spec.GetValue<int?>("TuoiTho");
            var soNgayLamViec = _configuration.GetValue<int?>("MayIn:SoNgayLamViecMoiThang") ?? 20;
            if (soNgayLamViec <= 0) soNgayLamViec = 20;

            return new CongSuat(
                spec.GetValue<string>("Ten") ?? model,
                ampvKhuyenNghi,
                ampvToiDa,
                spec.GetValue<int?>("DutyCycle"),
                tuoiTho,
                ampvKhuyenNghi is null ? null : ampvKhuyenNghi.Value / soNgayLamViec,
                ampvToiDa is null ? null : ampvToiDa.Value / soNgayLamViec,
                soNgayLamViec,
                counterTong,
                counterTong is null || tuoiTho is null or 0
                    ? null
                    : Math.Round(counterTong.Value * 100.0 / tuoiTho.Value, 1),
                // Ghi rõ số lấy từ đâu để người dùng đối chiếu datasheet, khỏi tin suông
                spec.GetValue<string>("Nguon"));
        }

        private static IConfigurationSection? TimSpec(IConfigurationSection bang, string? khoa)
        {
            if (string.IsNullOrWhiteSpace(khoa)) return null;

            // So khớp không phân biệt hoa thường vì Model trong Excel gõ tay ("FX3360s" / "FX3360S")
            foreach (var muc in bang.GetChildren())
            {
                if (string.Equals(muc.Key, khoa.Trim(), StringComparison.OrdinalIgnoreCase)) return muc;
            }

            return null;
        }

        private static string? LayChuoi(JsonElement? goc, string khoa)
            => goc is not null && goc.Value.TryGetProperty(khoa, out var v) ? v.GetString() : null;

        /// <summary>Tổng in + copy đọc trực tiếp lúc quét, dùng làm mốc "tới hiện tại".</summary>
        private static int? LayCounterTong(JsonElement? counter)
        {
            if (counter is null
                || !counter.Value.TryGetProperty("UsageCounters", out var ds)
                || ds.ValueKind != JsonValueKind.Array) return null;

            int? inTong = null, copyTong = null;
            foreach (var muc in ds.EnumerateArray())
            {
                var ten = muc.TryGetProperty("DomesticName", out var t) ? t.GetString() : null;
                if (ten is null || !muc.TryGetProperty("Count", out var c) || c.ValueKind != JsonValueKind.Number) continue;

                if (ten == "PRINT_TOTAL_IMPRESSION") inTong = c.GetInt32();
                else if (ten == "COPY_TOTAL_IMPRESSION") copyTong = c.GetInt32();
            }

            return inTong is null && copyTong is null ? null : (inTong ?? 0) + (copyTong ?? 0);
        }

        /// <summary>
        /// Suy mức dùng trang/ngày từ lịch sử lỗi: mỗi bản ghi kèm mốc thời gian và Volume
        /// (đồng hồ tổng in+copy tại thời điểm đó). Trả về nhiều khoảng để so được ngắn hạn với
        /// dài hạn, cộng thêm khoảng "tới hiện tại" dựng từ đồng hồ đọc trực tiếp lúc quét.
        ///
        /// Đây là SUY LUẬN từ dữ liệu máy để lại, hãng không có tài liệu xác nhận ý nghĩa cột Volume.
        /// </summary>
        private object? DocMucDung(JsonElement? lichSuLoi, int? counterTong, int? trangMoiNgayKhuyenNghi)
        {
            var moc = DocMocLoi(lichSuLoi);
            if (moc.Count == 0) return null;

            var cacKhoang = new List<object>();
            var moiNhat = moc[0];

            object? Khoang(string nhan, DateTime tu, int volumeTu, DateTime den, int volumeDen)
            {
                var soNgay = (den - tu).TotalDays;
                var soTrang = volumeDen - volumeTu;
                if (soNgay <= 0 || soTrang < 0) return null;

                var trangMoiNgay = soTrang / soNgay;

                return new
                {
                    nhan,
                    tuNgay = tu.ToString("dd/MM/yyyy"),
                    denNgay = den.ToString("dd/MM/yyyy"),
                    soNgay = Math.Round(soNgay, 1),
                    soTrang,
                    trangMoiNgay = Math.Round(trangMoiNgay, 1),
                    phanTramKhuyenNghi = trangMoiNgayKhuyenNghi is null or 0
                                         ? (double?)null
                                         : Math.Round(trangMoiNgay * 100.0 / trangMoiNgayKhuyenNghi.Value, 1)
                };
            }

            // 1) Ngắn hạn: giữa hai mốc lỗi gần nhất — phản ánh nhịp in hiện tại
            if (moc.Count >= 2)
            {
                var truoc = moc[1];
                var k = Khoang("Gần đây", truoc.ThoiDiem, truoc.Volume, moiNhat.ThoiDiem, moiNhat.Volume);
                if (k != null) cacKhoang.Add(k);
            }

            // 2) Dài hạn: từ bản ghi cũ nhất máy còn giữ tới mốc mới nhất
            var cuNhat = moc[^1];
            if (cuNhat.ThoiDiem < moiNhat.ThoiDiem)
            {
                var k = Khoang("Dài hạn", cuNhat.ThoiDiem, cuNhat.Volume, moiNhat.ThoiDiem, moiNhat.Volume);
                if (k != null) cacKhoang.Add(k);
            }

            // 3) Từ mốc lỗi mới nhất tới đồng hồ đọc được ngay lúc quét — phần "hôm nay"
            if (counterTong is not null)
            {
                var k = Khoang("Tới hiện tại", moiNhat.ThoiDiem, moiNhat.Volume, DateTime.Now, counterTong.Value);
                if (k != null) cacKhoang.Add(k);
            }

            if (cacKhoang.Count == 0) return null;

            return new
            {
                cacKhoang,
                soBanGhiLoi = moc.Count,
                ghiChu = "Máy không công bố số trang theo từng ngày. Các mốc trên lấy từ đồng hồ tổng kèm trong lịch sử lỗi "
                         + "nên chỉ là ước lượng; khoảng càng ngắn thì sai số càng lớn."
            };
        }

        private static object? DocLichSuLoi(JsonElement? lichSuLoi)
        {
            var moc = DocMocLoi(lichSuLoi);
            if (moc.Count == 0) return null;

            return moc.Take(15).Select(x => new
            {
                thoiDiem = x.ThoiDiem.ToString("dd/MM/yyyy HH:mm"),
                maLoi = $"{x.ChainCode:000}-{x.LinkCode:000}",
                x.Volume
            }).ToList();
        }

        private record MocLoi(DateTime ThoiDiem, int Volume, int ChainCode, int LinkCode);

        private static List<MocLoi> DocMocLoi(JsonElement? lichSuLoi)
        {
            var ketQua = new List<MocLoi>();
            if (lichSuLoi is null
                || !lichSuLoi.Value.TryGetProperty("FaultHistory", out var ds)
                || ds.ValueKind != JsonValueKind.Array) return ketQua;

            foreach (var muc in ds.EnumerateArray())
            {
                int So(string khoa) => muc.TryGetProperty(khoa, out var v) && v.ValueKind == JsonValueKind.Number
                                       ? v.GetInt32() : 0;

                var nam = So("Year");
                var thang = So("Month");
                var ngay = So("Day");
                if (nam < 2000 || thang is < 1 or > 12 || ngay is < 1 or > 31) continue;

                try
                {
                    var thoiDiem = new DateTime(nam, thang, ngay, So("Hour"), So("Minute"), 0);
                    ketQua.Add(new MocLoi(thoiDiem, So("Volume"), So("ChainCode"), So("LinkCode")));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Bản ghi có ngày giờ vô lý thì bỏ qua, không làm hỏng cả lần quét
                }
            }

            return ketQua.OrderByDescending(x => x.ThoiDiem).ToList();
        }

        #endregion

        private static async Task<JsonElement?> LayJsonAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                using var phanHoi = await client.GetAsync(url, ct);
                if (!phanHoi.IsSuccessStatusCode) return null;

                var noiDung = await phanHoi.Content.ReadAsStringAsync(ct);
                // Máy đời cũ trả trang HTML với mã 200 — không phải JSON thì coi như không có dữ liệu
                if (string.IsNullOrWhiteSpace(noiDung) || noiDung.TrimStart().StartsWith('<')) return null;

                using var tep = JsonDocument.Parse(noiDung);
                return tep.RootElement.Clone();
            }
            catch (Exception)
            {
                // Thiếu một nhóm dữ liệu không được làm hỏng cả lần quét
                return null;
            }
        }
    }
}
