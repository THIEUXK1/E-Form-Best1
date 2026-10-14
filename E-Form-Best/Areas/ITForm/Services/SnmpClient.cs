using System.Net;
using System.Net.Sockets;
using System.Text;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Client SNMP v2c tối giản (GET / GETBULK walk) tự mã hoá BER qua UDP — dự án không thêm NuGet.
    /// Chỉ đọc, dùng cho ServerSnmpWorker lấy sức khoẻ server / thiết bị lưu trữ.
    /// </summary>
    public static class SnmpClient
    {
        public record Bien(string Oid, object? GiaTri);

        private const byte TagInt = 0x02, TagOctet = 0x04, TagNull = 0x05, TagOid = 0x06, TagSeq = 0x30;
        private const byte PduGet = 0xA0, PduBulk = 0xA5, PduResponse = 0xA2;
        private const byte EndOfMib = 0x82;

        private static int _reqId = Environment.TickCount & 0x3FFFFFFF;

        /// <summary>GET nhiều OID một lần; OID không có trả null.</summary>
        public static async Task<Dictionary<string, object?>> GetAsync(string ip, string community, IEnumerable<string> oids,
            int timeoutMs = 2000, int soLanThu = 2, CancellationToken ct = default)
        {
            var ds = oids.ToList();
            var kq = new Dictionary<string, object?>();
            // Gửi từng lô 20 OID cho gói tin không vượt MTU
            for (var i = 0; i < ds.Count; i += 20)
            {
                var lo = ds.Skip(i).Take(20).ToList();
                var tl = await GuiAsync(ip, community, TaoPdu(PduGet, 0, 0, lo), timeoutMs, soLanThu, ct);
                foreach (var b in tl) kq[b.Oid] = b.GiaTri;
            }
            return kq;
        }

        /// <summary>Đi hết cây con dưới gốc bằng GETBULK; dừng khi ra khỏi cây, hết MIB hoặc quá toiDa dòng.</summary>
        public static async Task<List<Bien>> WalkAsync(string ip, string community, string goc,
            int timeoutMs = 2000, int soLanThu = 2, int toiDa = 5000, CancellationToken ct = default)
        {
            var kq = new List<Bien>();
            var tienTo = goc.TrimEnd('.') + ".";
            var hienTai = goc;
            while (kq.Count < toiDa)
            {
                var tl = await GuiAsync(ip, community, TaoPdu(PduBulk, 0, 25, new[] { hienTai }), timeoutMs, soLanThu, ct);
                if (tl.Count == 0) break;
                var het = false;
                foreach (var b in tl)
                {
                    if (b.GiaTri is HetMib || !b.Oid.StartsWith(tienTo)) { het = true; break; }
                    kq.Add(b);
                }
                if (het || tl[^1].Oid == hienTai) break;
                hienTai = tl[^1].Oid;
            }
            return kq;
        }

        private sealed class HetMib { public static readonly HetMib Mot = new(); }

        private static async Task<List<Bien>> GuiAsync(string ip, string community, byte[] pdu, int timeoutMs, int soLanThu, CancellationToken ct)
        {
            var goi = Tlv(TagSeq, Noi(Tlv(TagInt, MaHoaSo(1)), Tlv(TagOctet, Encoding.ASCII.GetBytes(community)), pdu));
            var reqId = DocReqId(pdu);
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            var dich = new IPEndPoint(IPAddress.Parse(ip), 161);

            for (var lan = 0; lan < Math.Max(1, soLanThu); lan++)
            {
                await udp.SendAsync(goi, dich, ct);
                using var het = CancellationTokenSource.CreateLinkedTokenSource(ct);
                het.CancelAfter(timeoutMs);
                try
                {
                    while (true)
                    {
                        var nhan = await udp.ReceiveAsync(het.Token);
                        var tl = Giai(nhan.Buffer, out var id, out var loi);
                        if (id != reqId) continue; // gói trễ của lần gửi trước
                        if (loi != 0) throw new InvalidOperationException($"SNMP lỗi {loi} từ {ip}");
                        return tl;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* hết giờ, thử lại */ }
            }
            throw new TimeoutException($"SNMP {ip} không trả lời (community sai hoặc chặn UDP 161)");
        }

        // ---------------- Mã hoá ----------------

        private static byte[] TaoPdu(byte loai, int a, int b, IEnumerable<string> oids)
        {
            var id = Interlocked.Increment(ref _reqId) & 0x3FFFFFFF;
            var vb = Tlv(TagSeq, Noi(oids.Select(o => Tlv(TagSeq, Noi(Tlv(TagOid, MaHoaOid(o)), new byte[] { TagNull, 0 }))).ToArray()));
            return Tlv(loai, Noi(Tlv(TagInt, MaHoaSo(id)), Tlv(TagInt, MaHoaSo(a)), Tlv(TagInt, MaHoaSo(b)), vb));
        }

        private static int DocReqId(byte[] pdu)
        {
            var p = 0;
            DocTlv(pdu, ref p, out _, out var noiDung);
            var q = 0;
            DocTlv(noiDung, ref q, out _, out var idBytes);
            return (int)SoNguyen(idBytes);
        }

        private static byte[] Noi(params byte[][] phan)
        {
            var kq = new byte[phan.Sum(x => x.Length)];
            var i = 0;
            foreach (var p in phan) { Buffer.BlockCopy(p, 0, kq, i, p.Length); i += p.Length; }
            return kq;
        }

        private static byte[] Tlv(byte tag, byte[] v)
        {
            byte[] len;
            if (v.Length < 0x80) len = new[] { (byte)v.Length };
            else
            {
                var l = new List<byte>();
                for (var n = v.Length; n > 0; n >>= 8) l.Insert(0, (byte)(n & 0xFF));
                l.Insert(0, (byte)(0x80 | l.Count));
                len = l.ToArray();
            }
            return Noi(new[] { tag }, len, v);
        }

        private static byte[] MaHoaSo(long n)
        {
            var b = new List<byte>();
            do { b.Insert(0, (byte)(n & 0xFF)); n >>= 8; } while (n != 0 && n != -1);
            if (n == 0 && (b[0] & 0x80) != 0) b.Insert(0, 0);
            if (n == -1 && (b[0] & 0x80) == 0) b.Insert(0, 0xFF);
            return b.ToArray();
        }

        private static byte[] MaHoaOid(string oid)
        {
            var p = oid.Trim('.').Split('.').Select(uint.Parse).ToArray();
            var kq = new List<byte> { (byte)(p[0] * 40 + p[1]) };
            foreach (var x in p.Skip(2))
            {
                var t = new List<byte> { (byte)(x & 0x7F) };
                for (var v = x >> 7; v > 0; v >>= 7) t.Insert(0, (byte)(0x80 | (v & 0x7F)));
                kq.AddRange(t);
            }
            return kq.ToArray();
        }

        // ---------------- Giải mã ----------------

        private static void DocTlv(byte[] d, ref int p, out byte tag, out byte[] v)
        {
            tag = d[p++];
            int len = d[p++];
            if ((len & 0x80) != 0)
            {
                var n = len & 0x7F;
                len = 0;
                for (var i = 0; i < n; i++) len = (len << 8) | d[p++];
            }
            v = new byte[len];
            Buffer.BlockCopy(d, p, v, 0, len);
            p += len;
        }

        private static List<Bien> Giai(byte[] d, out int reqId, out int loi)
        {
            var p = 0;
            DocTlv(d, ref p, out _, out var msg);
            var q = 0;
            DocTlv(msg, ref q, out _, out _);          // version
            DocTlv(msg, ref q, out _, out _);          // community
            DocTlv(msg, ref q, out var loaiPdu, out var pdu);
            if (loaiPdu != PduResponse) throw new InvalidOperationException("Gói SNMP không phải Response");

            var r = 0;
            DocTlv(pdu, ref r, out _, out var id);
            DocTlv(pdu, ref r, out _, out var st);
            DocTlv(pdu, ref r, out _, out _);
            DocTlv(pdu, ref r, out _, out var vbl);
            reqId = (int)SoNguyen(id);
            loi = (int)SoNguyen(st);

            var kq = new List<Bien>();
            var s = 0;
            while (s < vbl.Length)
            {
                DocTlv(vbl, ref s, out _, out var vb);
                var t = 0;
                DocTlv(vb, ref t, out _, out var oidBytes);
                DocTlv(vb, ref t, out var tag, out var val);
                kq.Add(new Bien(GiaiOid(oidBytes), GiaiGiaTri(tag, val)));
            }
            return kq;
        }

        private static object? GiaiGiaTri(byte tag, byte[] v) => tag switch
        {
            TagInt => SoNguyen(v),
            TagOctet => v,
            TagOid => GiaiOid(v),
            0x40 => v.Length == 4 ? new IPAddress(v).ToString() : null,          // IpAddress
            0x41 or 0x42 or 0x43 or 0x46 => (long)SoKhongDau(v),                // Counter32 / Gauge32 / TimeTicks / Counter64
            EndOfMib => HetMib.Mot,
            _ => null                                                             // NULL / noSuchObject / noSuchInstance / Opaque
        };

        private static long SoNguyen(byte[] v)
        {
            if (v.Length == 0) return 0;
            long n = (v[0] & 0x80) != 0 ? -1 : 0;
            foreach (var b in v) n = (n << 8) | b;
            return n;
        }

        private static ulong SoKhongDau(byte[] v)
        {
            ulong n = 0;
            foreach (var b in v) n = (n << 8) | b;
            return n;
        }

        private static string GiaiOid(byte[] v)
        {
            if (v.Length == 0) return "";
            var sb = new StringBuilder();
            sb.Append(v[0] / 40).Append('.').Append(v[0] % 40);
            ulong x = 0;
            for (var i = 1; i < v.Length; i++)
            {
                x = (x << 7) | (uint)(v[i] & 0x7F);
                if ((v[i] & 0x80) == 0) { sb.Append('.').Append(x); x = 0; }
            }
            return sb.ToString();
        }

        /// <summary>OCTET STRING -> chuỗi (bỏ ký tự rác cuối); kiểu khác -> ToString.</summary>
        public static string? Chuoi(object? v) => v switch
        {
            null => null,
            byte[] b => Encoding.UTF8.GetString(b).TrimEnd('\0', ' ', '\r', '\n'),
            _ => v.ToString()
        };

        public static long? So(object? v) => v switch
        {
            long l => l,
            byte[] b when long.TryParse(Encoding.ASCII.GetString(b).Trim(), out var n) => n,
            _ => null
        };
    }
}
