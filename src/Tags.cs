using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    public class TagInfo
    {
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public string AlbumArtist = "";
        public string Genre = "";
        public int Year;
        public int TrackNo;
        public int DiscNo;
        public double Duration;
        public int Bitrate;
        public int SampleRate;
        public int Channels;
        public byte[] Cover;
        public string CoverMime = "";
        public string Lyrics = "";
        public string Tags = "";          // 需要时一次性读取的容器标签
        public bool Any;
    }

    // ================= 音乐标签读取（纯手写解析，无第三方依赖，只读文件头/尾，不加载整个音频） =================
    public static class TagReader
    {
        static readonly string[] TEXT_EXT = new string[] { ".txt", ".log", ".cue", ".m3u", ".jpg", ".png" };

        public static bool IsAudio(string path, List<string> extensions)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            if (extensions != null && extensions.Count > 0)
            {
                foreach (string x in extensions) if (string.Equals(x, e, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            foreach (string x in Settings.DefaultExtensions()) if (string.Equals(x, e, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static TagInfo Read(string path)
        {
            TagInfo t = new TagInfo();
            FileStream fs = null;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".wav" || ext == ".wave") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseWav(fs, t); }
                else if (ext == ".flac") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseFlac(fs, t); }
                else if (ext == ".ogg" || ext == ".oga" || ext == ".opus" || ext == ".spx") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseOgg(fs, t); }
                else if (ext == ".m4a" || ext == ".mp4" || ext == ".m4b" || ext == ".aac" || ext == ".alac" || ext == ".mka") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); if (!ParseMp4(fs, t) && ext == ".aac") ParseAdts(fs, t); }
                else if (ext == ".wma" || ext == ".asf") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseAsf(fs, t); }
                else if (ext == ".aif" || ext == ".aiff") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseAiff(fs, t); }
                else if (ext == ".ape" || ext == ".wv" || ext == ".mpc") { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseApe(fs, t); }
                else { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ParseMp3(fs, t); }
            }
            catch { }
            finally { if (fs != null) try { fs.Close(); } catch { } }
            t.Any = true;
            return t;
        }

        // 只读取封面（用于播放时按需加载，避免扫描时全部读入内存）
        public static byte[] ReadCover(string path)
        {
            TagInfo t = Read(path);
            return t.Cover;
        }

        // ---------------- 工具 ----------------
        static byte[] ReadAt(Stream s, long off, int count)
        {
            if (off < 0 || off >= s.Length) return new byte[0];
            if (off + count > s.Length) count = (int)(s.Length - off);
            if (count <= 0) return new byte[0];
            byte[] b = new byte[count];
            s.Position = off;
            int got = 0;
            while (got < count)
            {
                int n = s.Read(b, got, count - got);
                if (n <= 0) break;
                got += n;
            }
            if (got == count) return b;
            byte[] r = new byte[got];
            Array.Copy(b, r, got);
            return r;
        }

        static int Be32(byte[] b, int p) { return (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]; }
        static int Le32(byte[] b, int p) { return b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24); }
        static int Le16(byte[] b, int p) { return b[p] | (b[p + 1] << 8); }
        static int Be16(byte[] b, int p) { return (b[p] << 8) | b[p + 1]; }
        static int SyncSafe(byte[] b, int p) { return ((b[p] & 0x7f) << 21) | ((b[p + 1] & 0x7f) << 14) | ((b[p + 2] & 0x7f) << 7) | (b[p + 3] & 0x7f); }
        static string Ascii(byte[] b, int p, int n) { StringBuilder sb = new StringBuilder(); for (int i = 0; i < n && p + i < b.Length; i++) { char c = (char)b[p + i]; if (c == '\0') break; sb.Append(c); } return sb.ToString(); }
        static string Latin1(byte[] b, int p, int n) { return Encoding.GetEncoding(28591).GetString(b, p, Math.Max(0, Math.Min(n, b.Length - p))); }
        static string Utf8(byte[] b, int p, int n) { return Encoding.UTF8.GetString(b, p, Math.Max(0, Math.Min(n, b.Length - p))); }
        static bool Match(byte[] b, int p, string s) { if (p + s.Length > b.Length) return false; for (int i = 0; i < s.Length; i++) if (b[p + i] != (byte)s[i]) return false; return true; }

        static string DecodeText(byte[] d, int p, out int next)
        {
            next = d.Length;
            if (p >= d.Length) return "";
            byte enc = d[p];
            int q = p + 1;
            if (enc == 0)
            {
                int e = q; while (e < d.Length && d[e] != 0) e++;
                next = e + 1;
                return Latin1(d, q, e - q);
            }
            if (enc == 1 || enc == 2)
            {
                bool be = enc == 2;
                int e = q;
                while (e + 1 < d.Length)
                {
                    if (d[e] == 0 && d[e + 1] == 0) break;
                    e += 2;
                }
                int len = e - q;
                if (len <= 0) { next = e + 2; return ""; }
                byte[] tmp = new byte[len];
                Array.Copy(d, q, tmp, 0, len);
                if (!be && len >= 2 && tmp[0] == 0xFF && tmp[1] == 0xFE)
                {
                    byte[] s2 = new byte[len - 2]; Array.Copy(tmp, 2, s2, 0, len - 2);
                    next = e + 2; return Encoding.Unicode.GetString(s2);
                }
                if (!be && len >= 2 && tmp[0] == 0xFE && tmp[1] == 0xFF)
                {
                    byte[] s2 = new byte[len - 2]; Array.Copy(tmp, 2, s2, 0, len - 2);
                    next = e + 2; return Encoding.BigEndianUnicode.GetString(s2);
                }
                if (be)
                {
                    // UTF-16BE 无 BOM
                    byte[] s2 = tmp;
                    if (s2.Length >= 2 && s2[0] == 0xFE && s2[1] == 0xFF) { byte[] t3 = new byte[s2.Length - 2]; Array.Copy(s2, 2, t3, 0, s2.Length - 2); s2 = t3; }
                    else if (s2.Length >= 2 && s2[0] == 0xFF && s2[1] == 0xFE) { byte[] t3 = new byte[s2.Length - 2]; Array.Copy(s2, 2, t3, 0, s2.Length - 2); s2 = t3; }
                    next = e + 2; return Encoding.BigEndianUnicode.GetString(s2);
                }
                next = e + 2; return Encoding.Unicode.GetString(tmp);
            }
            // UTF-8
            int z = q; while (z < d.Length && d[z] != 0) z++;
            next = z + 1;
            return Utf8(d, q, z - q);
        }

        static string Clean(string s)
        {
            if (s == null) return "";
            int n = s.IndexOf('\0');
            if (n >= 0) s = s.Substring(0, n);
            return s.Trim();
        }

        // ---------------- ID3v2 ----------------
        static bool ParseId3v2(byte[] tag, TagInfo t, bool coverWanted)
        {
            if (tag.Length < 10) return false;
            int ver = tag[3];
            byte flags = tag[5];

            int p = 0;
            if ((flags & 0x40) != 0)
            {
                if (ver == 3) { int es = Be32(tag, 0); p = 4 + es; }
                else { int es = SyncSafe(tag, 0); p = es; }
            }
            int idLen = ver == 2 ? 3 : 4;
            while (p + idLen + (ver == 2 ? 3 : 6) <= tag.Length)
            {
                string id = Ascii(tag, p, idLen);
                if (id.Length < idLen) break;
                bool ok = true;
                for (int i = 0; i < id.Length; i++) if (!(char.IsLetterOrDigit(id[i]))) { ok = false; break; }
                if (!ok) break;

                int size;
                if (ver == 2) size = (tag[p + 3] << 16) | (tag[p + 4] << 8) | tag[p + 5];
                else if (ver == 4) size = SyncSafe(tag, p + 4);
                else size = Be32(tag, p + 4);
                int hdr = idLen + (ver == 2 ? 3 : 6);
                if (size < 0 || p + hdr + size > tag.Length) break;
                int ds = p + hdr;
                int di = ds;
                byte[] data = new byte[size];
                Array.Copy(tag, ds, data, 0, size);
                p = ds + size;

                if (ver >= 3)
                {
                    byte f2 = tag[ds - 1];
                    if ((f2 & 0x02) != 0) continue;      // 已压缩，跳过
                    if ((f2 & 0x01) != 0 && ver == 4) { } // 数据长度指示，忽略
                    if ((f2 & 0x01) != 0 && ver == 3) continue;
                }

                string key = id;
                if (key == "TIT2" || key == "TT2") t.Title = Clean(DecodeText(data, 0, out di));
                else if (key == "TPE1" || key == "TP1") t.Artist = Clean(DecodeText(data, 0, out di));
                else if (key == "TALB" || key == "TAL") t.Album = Clean(DecodeText(data, 0, out di));
                else if (key == "TPE2" || key == "TP2") t.AlbumArtist = Clean(DecodeText(data, 0, out di));
                else if (key == "TCON" || key == "TCO") t.Genre = CleanGenre(Clean(DecodeText(data, 0, out di)));
                else if (key == "TYER" || key == "TYE") { int y; int.TryParse(Clean(DecodeText(data, 0, out di)), out y); if (y > 0) t.Year = y; }
                else if (key == "TDRC") { string v = Clean(DecodeText(data, 0, out di)); if (v.Length >= 4) { int y; int.TryParse(v.Substring(0, 4), out y); if (y > 0) t.Year = y; } }
                else if (key == "TRCK" || key == "TRK") { string v = Clean(DecodeText(data, 0, out di)); int a = v.IndexOf('/'); if (a >= 0) v = v.Substring(0, a); int n; int.TryParse(v.Trim(), out n); t.TrackNo = n; }
                else if (key == "TPOS") { string v = Clean(DecodeText(data, 0, out di)); int a = v.IndexOf('/'); if (a >= 0) v = v.Substring(0, a); int n; int.TryParse(v.Trim(), out n); t.DiscNo = n; }
                else if (key == "USLT" || key == "ULT")
                {
                    if (t.Lyrics.Length == 0) t.Lyrics = ParseUslt(data);
                }
                else if ((key == "APIC" || key == "PIC") && coverWanted && t.Cover == null)
                {
                    int q = 1;
                    if (key == "PIC") { string fmt = Ascii(data, q, 3); q += 3; q = SkipLatin(data, q); }
                    else { string mime = Ascii(data, q, data.Length - q); t.CoverMime = mime; q += mime.Length + 1; }
                    if (q < data.Length) q++;                       // 图片类型
                    DecodeText(data, q, out di); q = di;            // 描述
                    if (q < 0 || q > data.Length) q = data.Length;
                    int clen = data.Length - q;
                    if (clen > 0)
                    {
                        t.Cover = new byte[clen];
                        Array.Copy(data, q, t.Cover, 0, clen);
                    }
                }
            }
            return true;
        }

        // 以给定编码解码一段（不消耗编码字节）
        static string DecodeFrom(byte[] d, int start, byte enc, out int next)
        {
            int len = d.Length - start;
            if (len <= 0) { next = d.Length; return ""; }
            byte[] tmp = new byte[len + 1];
            tmp[0] = enc;
            Array.Copy(d, start, tmp, 1, len);
            int n;
            string s = DecodeText(tmp, 0, out n);
            next = start + Math.Max(0, n - 1);
            return s;
        }

        // ID3v2 USLT：编码(1) + 语言(3) + 描述(以编码结尾符) + 正文
        static string ParseUslt(byte[] d)
        {
            try
            {
                if (d.Length < 5) return "";
                byte enc = d[0];
                int q = 4;
                int n1;
                DecodeFrom(d, q, enc, out n1);
                q = n1;
                int n2;
                return DecodeFrom(d, q, enc, out n2);
            }
            catch { return ""; }
        }

        static int SkipLatin(byte[] d, int p)
        {
            int e = p; while (e < d.Length && d[e] != 0) e++;
            return e + 1;
        }

        static string CleanGenre(string g)
        {
            if (g == null) return "";
            if (g.Length >= 2 && g[0] == '(' && g.IndexOf(')') > 0)
            {
                int e = g.IndexOf(')');
                string num = g.Substring(1, e - 1);
                int n;
                if (int.TryParse(num, out n)) return g.Substring(e + 1).Trim();
            }
            return g;
        }

        // ---------------- ID3v1 ----------------
        static void ParseId3v1(Stream fs, TagInfo t)
        {
            if (fs.Length < 128) return;
            byte[] b = ReadAt(fs, fs.Length - 128, 128);
            if (b.Length < 128 || !Match(b, 0, "TAG")) return;
            if (t.Title == "") t.Title = Clean(Latin1(b, 3, 30));
            if (t.Artist == "") t.Artist = Clean(Latin1(b, 33, 30));
            if (t.Album == "") t.Album = Clean(Latin1(b, 63, 30));
            if (t.Year == 0) { int y; int.TryParse(Clean(Latin1(b, 93, 4)), out y); if (y > 0) t.Year = y; }
            byte commentEnd = b[125];
            if (commentEnd == 0) { if (t.TrackNo == 0) t.TrackNo = b[126]; }
        }

        // ---------------- MP3 ----------------
        static void ParseMp3(Stream fs, TagInfo t)
        {
            long audioStart = 0;
            byte[] head = ReadAt(fs, 0, 10);
            if (head.Length >= 10 && Match(head, 0, "ID3"))
            {
                bool wholeUnsync = (head[5] & 0x80) != 0;
                int size = SyncSafe(head, 6);
                int ver = head[3];
                if (size > 0 && size < 32 * 1024 * 1024)
                {
                    byte[] tag = ReadAt(fs, 10, size);
                    if (wholeUnsync && ver < 4) tag = DeUnsync(tag);
                    ParseId3v2(tag, t, true);
                }
                audioStart = 10 + size;
            }
            if (t.Title == "" && t.Artist == "") ParseId3v1(fs, t);

            // 解析第一个有效帧
            byte[] buf = ReadAt(fs, audioStart, 65536);
            int off = FindFrameSync(buf, 0);
            if (off < 0) { GuessByAvgBitrate(fs, t, audioStart); return; }
            int version, layer, bitrate, rate, padding, channelMode, samples, frameLen, brIndex, srIndex;
            if (!FrameInfo(buf, off, out version, out layer, out bitrate, out rate, out padding, out channelMode, out samples, out frameLen, out brIndex, out srIndex))
            { GuessByAvgBitrate(fs, t, audioStart); return; }

            t.Bitrate = bitrate / 1000;
            t.SampleRate = rate;
            t.Channels = channelMode == 3 ? 1 : 2;

            // Xing / Info 头（VBR）
            int sideInfo = (version == 3) ? (channelMode == 3 ? 17 : 32) : (channelMode == 3 ? 9 : 17);
            int xo = off + 4 + sideInfo;
            long total = 0;
            if (xo + 12 <= buf.Length && (Match(buf, xo, "Xing") || Match(buf, xo, "Info")))
            {
                int flags = Be32(buf, xo + 4);
                if ((flags & 1) != 0)
                {
                    int frames = Be32(buf, xo + 8);
                    if (frames > 0) { t.Duration = (double)frames * samples / rate; return; }
                }
                if ((flags & 2) != 0)
                {
                    int bytes = Be32(buf, xo + 12);
                    if (bitrate > 0 && bytes > 0) { t.Duration = (double)bytes * 8 / bitrate; return; }
                }
            }
            total = fs.Length - audioStart;
            if (bitrate > 0) t.Duration = (double)total * 8 / bitrate;
        }

        static void GuessByAvgBitrate(Stream fs, TagInfo t, long audioStart)
        {
            long total = fs.Length - audioStart;
            if (total <= 0) return;
            if (t.Duration <= 0 && t.Bitrate > 0) t.Duration = (double)total * 8 / (t.Bitrate * 1000.0);
        }

        static int FindFrameSync(byte[] b, int from)
        {
            for (int i = from; i + 3 < b.Length; i++)
            {
                if (b[i] == 0xFF && (b[i + 1] & 0xE0) == 0xE0 && (b[i + 1] & 0x18) != 0x08 && (b[i + 2] & 0xF0) != 0xF0)
                    return i;
            }
            return -1;
        }

        static readonly int[] BR1 = new int[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        static readonly int[] BR2 = new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        static readonly int[] SR1 = new int[] { 44100, 48000, 32000, 0 };
        static readonly int[] SR2 = new int[] { 22050, 24000, 16000, 0 };
        static readonly int[] SR25 = new int[] { 11025, 12000, 8000, 0 };

        static bool FrameInfo(byte[] b, int p, out int version, out int layer, out int bitrate, out int rate, out int padding, out int channelMode, out int samples, out int frameLen, out int brIndex, out int srIndex)
        {
            version = layer = bitrate = rate = padding = channelMode = samples = frameLen = brIndex = srIndex = 0;
            if (p + 4 > b.Length) return false;
            version = (b[p + 1] >> 3) & 3;
            layer = (b[p + 1] >> 1) & 3;
            brIndex = (b[p + 2] >> 4) & 0xF;
            srIndex = (b[p + 2] >> 2) & 3;
            padding = (b[p + 2] >> 1) & 1;
            channelMode = (b[p + 3] >> 6) & 3;
            if (version == 1 || brIndex == 0 || brIndex == 15 || srIndex == 3) return false;
            int[] srt = version == 3 ? SR1 : (version == 2 ? SR2 : SR25);
            rate = srt[srIndex];
            if (rate <= 0) return false;
            int[] brt = version == 3 ? BR1 : BR2;
            bitrate = brt[brIndex] * 1000;
            if (bitrate <= 0) return false;
            samples = version == 3 ? 1152 : 576;
            if (layer == 3) frameLen = (samples / 8) * bitrate / rate + padding;
            else if (layer == 2) frameLen = 144 * bitrate / rate + padding;
            else frameLen = (12 * bitrate / rate + padding) * 4;
            return true;
        }

        static byte[] DeUnsync(byte[] b)
        {
            MemoryStream ms = new MemoryStream(b.Length);
            for (int i = 0; i < b.Length; i++)
            {
                ms.WriteByte(b[i]);
                if (b[i] == 0xFF && i + 1 < b.Length && b[i + 1] == 0x00) i++;
            }
            return ms.ToArray();
        }

        // ---------------- FLAC ----------------
        static void ParseFlac(Stream fs, TagInfo t)
        {
            byte[] h = ReadAt(fs, 0, 4);
            if (h.Length < 4 || !Match(h, 0, "fLaC")) { ParseId3v1(fs, t); return; }
            long p = 4;
            while (p + 4 <= fs.Length)
            {
                byte[] bh = ReadAt(fs, p, 4);
                if (bh.Length < 4) break;
                bool last = (bh[0] & 0x80) != 0;
                int type = bh[0] & 0x7F;
                int len = (bh[1] << 16) | (bh[2] << 8) | bh[3];
                long dp = p + 4;
                if (type == 0 && len >= 34)
                {
                    byte[] si = ReadAt(fs, dp, 34);
                    if (si.Length >= 34)
                    {
                        int rate = ((si[10] & 0x0F) << 14) | (si[11] << 6) | (si[12] >> 2);
                        int ch = ((si[12] & 1) << 2) | (si[13] >> 6);
                        long totalSamples = ((long)(si[13] & 0x0F) << 32) | ((long)si[14] << 24) | ((long)si[15] << 16) | ((long)si[16] << 8) | si[17];
                        t.SampleRate = rate;
                        t.Channels = ch + 1;
                        if (rate > 0 && totalSamples > 0) t.Duration = (double)totalSamples / rate;
                    }
                }
                else if (type == 4)
                {
                    byte[] vc = ReadAt(fs, dp, len);
                    ParseVorbisComment(vc, 0, t, false);
                }
                else if (type == 6)
                {
                    byte[] pic = ReadAt(fs, dp, len);
                    ParseFlacPicture(pic, t, true);
                }
                p = dp + len;
                if (last) break;
            }
        }

        static void ParseFlacPicture(byte[] d, TagInfo t, bool want)
        {
            try
            {
                int q = 0;
                q += 4;                                  // 类型
                int mlen = Be32(d, q); q += 4;
                if (want) t.CoverMime = Latin1(d, q, mlen);
                q += mlen;
                int dlen = Be32(d, q); q += 4;
                q += dlen;                               // 描述
                q += 16;                                 // 宽高色深颜色数
                int len = Be32(d, q); q += 4;
                if (want && len > 0 && q + len <= d.Length)
                {
                    t.Cover = new byte[len];
                    Array.Copy(d, q, t.Cover, 0, len);
                }
            }
            catch { }
        }

        // ---------------- Vorbis Comment / OpusTags ----------------
        static void ParseVorbisComment(byte[] d, int p, TagInfo t, bool opus)
        {
            try
            {
                int q = p;
                int vl = Le32(d, q); q += 4;
                q += vl;                                  // vendor
                int count = Le32(d, q); q += 4;
                for (int i = 0; i < count && q + 4 <= d.Length; i++)
                {
                    int len = Le32(d, q); q += 4;
                    if (len <= 0 || q + len > d.Length) break;
                    string s = Utf8(d, q, len); q += len;
                    int eq = s.IndexOf('=');
                    if (eq < 0) continue;
                    string k = s.Substring(0, eq).ToUpperInvariant();
                    string v = s.Substring(eq + 1);
                    if (k == "TITLE") t.Title = v;
                    else if (k == "ARTIST") t.Artist = v;
                    else if (k == "ALBUM") t.Album = v;
                    else if (k == "ALBUMARTIST" || k == "ALBUM ARTIST") t.AlbumArtist = v;
                    else if (k == "LYRICS" || k == "UNSYNCEDLYRICS" || k == "SYNCEDLYRICS") { if (t.Lyrics.Length == 0) t.Lyrics = v; }
                    else if (k == "GENRE") t.Genre = v;
                    else if (k == "DATE") { int y; int.TryParse(v.Length >= 4 ? v.Substring(0, 4) : v, out y); if (y > 0) t.Year = y; }
                    else if (k == "TRACKNUMBER") { int n; int.TryParse(v, out n); t.TrackNo = n; }
                    else if (k == "DISCNUMBER") { int n; int.TryParse(v, out n); t.DiscNo = n; }
                }
            }
            catch { }
        }

        // ---------------- OGG ----------------
        static void ParseOgg(Stream fs, TagInfo t)
        {
            byte[] head = ReadAt(fs, 0, 65536);
            int rate = 0, channels = 0;
            bool opus = false;
            int p = 0;
            int packetNo = 0;
            while (p + 27 <= head.Length && packetNo < 3)
            {
                if (!Match(head, p, "OggS")) break;
                int nseg = head[p + 26];
                if (p + 27 + nseg > head.Length) break;
                int segsum = 0;
                for (int i = 0; i < nseg; i++) segsum += head[p + 27 + i];
                int dataStart = p + 27 + nseg;
                if (dataStart + segsum > head.Length) break;

                if (packetNo == 0)
                {
                    if (Match(head, dataStart, "\x01vorbis"))
                    {
                        channels = head[dataStart + 11];
                        rate = Le32(head, dataStart + 12);
                    }
                    else if (Match(head, dataStart, "OpusHead"))
                    {
                        opus = true;
                        channels = head[dataStart + 9];
                        rate = Le32(head, dataStart + 12);
                        if (rate == 0) rate = 48000;
                    }
                }
                else if (packetNo == 1)
                {
                    if (Match(head, dataStart, "\x03vorbis")) ParseVorbisComment(head, dataStart + 7, t, false);
                    else if (Match(head, dataStart, "OpusTags")) ParseVorbisComment(head, dataStart + 8, t, true);
                }
                p = dataStart + segsum;
                packetNo++;
            }
            t.SampleRate = rate;
            t.Channels = channels;

            // 时长：读文件尾部最后一个页的 granule
            long glen = Math.Min(65536, fs.Length);
            byte[] tail = ReadAt(fs, fs.Length - glen, (int)glen);
            for (int i = tail.Length - 27; i >= 0; i--)
            {
                if (Match(tail, i, "OggS"))
                {
                    long granule = (long)(uint)(tail[i + 6] | (tail[i + 7] << 8) | (tail[i + 8] << 16) | (tail[i + 9] << 24))
                                 | ((long)(uint)(tail[i + 10] | (tail[i + 11] << 8) | (tail[i + 12] << 16) | (tail[i + 13] << 24)) << 32);
                    if (granule > 0 && rate > 0)
                    {
                        double g = opus ? granule / 48000.0 : (double)granule / rate;
                        if (g > 0) { t.Duration = g; return; }
                    }
                    break;
                }
            }
        }

        // ---------------- MP4 / M4A ----------------
        static bool ParseMp4(Stream fs, TagInfo t)
        {
            long size = fs.Length;
            long moov = -1;
            WalkBoxes(fs, 0, size, delegate(long pos, string type, long cpos, long cend)
            {
                if (type == "moov" && moov < 0) moov = cpos;
                return true;
            });
            if (moov < 0) { ParseId3v1(fs, t); return false; }
            ParseMoov(fs, moov, size, t);
            return true;
        }

        static long ReadBoxHeader(Stream fs, long pos, long limit, out string type, out long contentStart, out long end)
        {
            type = ""; contentStart = pos; end = limit;
            if (pos + 8 > limit) return -1;
            byte[] b = ReadAt(fs, pos, 8);
            if (b.Length < 8) return -1;
            long sz = (uint)Be32(b, 0);
            type = Ascii(b, 4, 4);
            long cstart = pos + 8;
            if (sz == 1)
            {
                byte[] b2 = ReadAt(fs, pos + 8, 8);
                if (b2.Length < 8) return -1;
                sz = ((long)(uint)Be32(b2, 0) << 32) | (uint)Be32(b2, 4);
                cstart = pos + 16;
            }
            else if (sz == 0) sz = limit - pos;
            contentStart = cstart;
            end = pos + sz;
            if (end > limit) end = limit;
            if (end <= pos) return -1;
            return end;
        }

        static void WalkBoxes(Stream fs, long start, long limit, Func<long, string, long, long, bool> visit)
        {
            long p = start;
            while (p + 8 <= limit)
            {
                string type; long cs, end;
                long e = ReadBoxHeader(fs, p, limit, out type, out cs, out end);
                if (e < 0) break;
                if (!visit(p, type, cs, end)) return;
                p = end;
            }
        }

        static void ParseMoov(Stream fs, long start, long limit, TagInfo t)
        {
            WalkBoxes(fs, start, limit, delegate(long pos, string type, long cs, long ce)
            {
                if (type == "mvhd") ParseMvhd(fs, cs, ce, t, false);
                else if (type == "trak")
                {
                    WalkBoxes(fs, cs, ce, delegate(long p2, string t2, long c2, long e2)
                    {
                        if (t2 == "mdia")
                        {
                            WalkBoxes(fs, c2, e2, delegate(long p3, string t3, long c3, long e3)
                            {
                                if (t3 == "mdhd") ParseMvhd(fs, c3, e3, t, true);
                                return true;
                            });
                        }
                        return true;
                    });
                }
                else if (type == "udta")
                {
                    WalkBoxes(fs, cs, ce, delegate(long p2, string t2, long c2, long e2)
                    {
                        if (t2 == "meta")
                        {
                            long st = c2 + 4;               // meta 有 4 字节 version/flags
                            WalkBoxes(fs, st, e2, delegate(long p3, string t3, long c3, long e3)
                            {
                                if (t3 == "ilst") ParseIlst(fs, c3, e3, t);
                                return true;
                            });
                        }
                        return true;
                    });
                }
                return true;
            });
        }

        static void ParseMvhd(Stream fs, long cs, long ce, TagInfo t, bool audioOnly)
        {
            byte[] b = ReadAt(fs, cs, (int)Math.Min(64, ce - cs));
            if (b.Length < 20) return;
            int ver = b[0];
            int rate, dur;
            if (ver == 1)
            {
                if (b.Length < 32) return;
                rate = Be32(b, 20);
                dur = 0;
                long d = ((long)(uint)Be32(b, 24) << 32) | (uint)Be32(b, 28);
                double dd = d;
                if (rate > 0) { double sec = dd / rate; t.Duration = sec; }
                return;
            }
            rate = Be32(b, 12);
            dur = Be32(b, 16);
            if (rate > 0 && dur > 0)
            {
                double sec = (double)(uint)dur / rate;
                if (audioOnly || t.Duration <= 0) t.Duration = sec;
            }
        }

        static void ParseIlst(Stream fs, long cs, long ce, TagInfo t)
        {
            WalkBoxes(fs, cs, ce, delegate(long pos, string type, long c2, long e2)
            {
                string key = type;
                WalkBoxes(fs, c2, e2, delegate(long p3, string t3, long c3, long e3)
                {
                    if (t3 == "data")
                    {
                        byte[] b = ReadAt(fs, c3, (int)Math.Min(e3 - c3, 16 * 1024 * 1024));
                        if (b.Length >= 8)
                        {
                            int dtype = Be32(b, 0);
                            int payload = 8;
                            if (key == "\xA9nam") t.Title = Utf8(b, payload, b.Length - payload);
                            else if (key == "\xA9ART") t.Artist = Utf8(b, payload, b.Length - payload);
                            else if (key == "\xA9alb") t.Album = Utf8(b, payload, b.Length - payload);
                            else if (key == "aART") t.AlbumArtist = Utf8(b, payload, b.Length - payload);
                            else if (key == "\xA9gen") t.Genre = Utf8(b, payload, b.Length - payload);
                            else if (key == "\xA9lyr") { if (t.Lyrics.Length == 0) t.Lyrics = Utf8(b, payload, b.Length - payload); }
                            else if (key == "\xA9day") { string v = Utf8(b, payload, b.Length - payload); int y; int.TryParse(v.Length >= 4 ? v.Substring(0, 4) : v, out y); if (y > 0) t.Year = y; }
                            else if (key == "trkn")
                            {
                                if (b.Length >= payload + 4) t.TrackNo = Be16(b, payload + 2);
                            }
                            else if (key == "disk")
                            {
                                if (b.Length >= payload + 4) t.DiscNo = Be16(b, payload + 2);
                            }
                            else if (key == "covr")
                            {
                                int clen = b.Length - payload;
                                if (clen > 0)
                                {
                                    t.Cover = new byte[clen];
                                    Array.Copy(b, payload, t.Cover, 0, clen);
                                    t.CoverMime = (dtype == 13) ? "image/jpeg" : (dtype == 14 ? "image/png" : "");
                                }
                            }
                        }
                    }
                    return true;
                });
                return true;
            });
        }

        static void ParseAdts(Stream fs, TagInfo t)
        {
            byte[] b = ReadAt(fs, 0, 4096);
            for (int i = 0; i + 7 < b.Length; i++)
            {
                if (b[i] == 0xFF && (b[i + 1] & 0xF6) == 0xF0)
                {
                    int srIndex = (b[i + 2] >> 2) & 0xF;
                    int[] rates = new int[] { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };
                    if (srIndex < rates.Length) t.SampleRate = rates[srIndex];
                    t.Channels = ((b[i + 2] & 1) << 2) | (b[i + 3] >> 6);
                    t.Bitrate = (int)(fs.Length * 8 / 1000.0);
                    break;
                }
            }
        }

        // ---------------- WAV ----------------
        static void ParseWav(Stream fs, TagInfo t)
        {
            if (fs.Length < 44) return;
            byte[] h = ReadAt(fs, 0, 12);
            if (h.Length < 12 || !Match(h, 0, "RIFF") || !Match(h, 8, "WAVE")) return;
            long p = 12;
            int byteRate = 0;
            long dataLen = 0;
            while (p + 8 <= fs.Length)
            {
                byte[] ch = ReadAt(fs, p, 8);
                if (ch.Length < 8) break;
                string id = Ascii(ch, 0, 4);
                long len = (uint)Le32(ch, 4);
                long dp = p + 8;
                if (id == "fmt ")
                {
                    byte[] f = ReadAt(fs, dp, (int)Math.Min(len, 40));
                    if (f.Length >= 16)
                    {
                        int chans = Le16(f, 2);
                        int rate = Le32(f, 4);
                        byteRate = Le32(f, 8);
                        t.Channels = chans;
                        t.SampleRate = rate;
                        t.Bitrate = byteRate * 8 / 1000;
                    }
                }
                else if (id == "data") { dataLen = len; }
                else if (id == "LIST")
                {
                    byte[] l = ReadAt(fs, dp, (int)Math.Min(len, 65536));
                    ParseRiffInfo(l, t);
                }
                else if (id == "id3 " || id == "ID3 ")
                {
                    byte[] tag = ReadAt(fs, dp, (int)Math.Min(len, 16 * 1024 * 1024));
                    byte[] full = new byte[tag.Length + 10];
                    byte[] pre = new byte[] { (byte)'I', (byte)'D', (byte)'3', 3, 0, 0, 0, 0, 0, 0 };
                    if (tag.Length >= 10 && Match(tag, 0, "ID3"))
                    {
                        Array.Copy(tag, full, tag.Length);
                        ParseId3v2(full, t, true);
                    }
                    else ParseId3v2(tag, t, true);
                }
                if (len <= 0) break;
                p = dp + len + (len % 2);
            }
            if (dataLen > 0 && byteRate > 0) t.Duration = (double)dataLen / byteRate;
            if (byteRate == 0)
            {
                long d = fs.Length - 44;
                if (t.SampleRate > 0 && t.Channels > 0) t.Duration = (double)d / (t.SampleRate * t.Channels * 2);
            }
        }

        static void ParseRiffInfo(byte[] d, TagInfo t)
        {
            if (d.Length < 4 || !Match(d, 0, "INFO")) return;
            int p = 4;
            while (p + 8 <= d.Length)
            {
                string id = Ascii(d, p, 4);
                int len = Le32(d, p + 4);
                int dp = p + 8;
                if (len < 0 || dp + len > d.Length) break;
                string v = Clean(Latin1(d, dp, len));
                if (id == "INAM") t.Title = v;
                else if (id == "IART") t.Artist = v;
                else if (id == "IPRD") t.Album = v;
                else if (id == "ICRD") { int y; int.TryParse(v.Length >= 4 ? v.Substring(0, 4) : v, out y); if (y > 0) t.Year = y; }
                else if (id == "IGNR") t.Genre = v;
                else if (id == "ITRK") { int n; int.TryParse(v, out n); t.TrackNo = n; }
                p = dp + len + (len % 2);
            }
        }

        // ---------------- ASF / WMA ----------------
        static readonly byte[] ASF_HEADER = new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C };
        static readonly byte[] ASF_FILEPROPS = new byte[] { 0xA1, 0xDC, 0xAB, 0x8C, 0x47, 0xA9, 0xCF, 0x11, 0x8E, 0xE4, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65 };
        static readonly byte[] ASF_CONTENTDESC = new byte[] { 0x64, 0xF2, 0xDC, 0xA9, 0x5D, 0x5D, 0x11, 0xD0, 0x8E, 0x2D, 0x00, 0xC0, 0x4F, 0xD7, 0xA7, 0x1E };
        static readonly byte[] ASF_EXTDESC = new byte[] { 0x40, 0xA4, 0xD0, 0xD2, 0x07, 0xE3, 0xD2, 0x11, 0x97, 0xF0, 0x00, 0xA0, 0xC9, 0x5E, 0xA8, 0x50 };
        static readonly byte[] ASF_STREAMPROPS = new byte[] { 0x91, 0x07, 0xDC, 0xB7, 0xB7, 0xA9, 0xCF, 0x11, 0x8E, 0xE6, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65 };

        static bool GuidEq(byte[] a, int p, byte[] g) { if (p + 16 > a.Length) return false; for (int i = 0; i < 16; i++) if (a[p + i] != g[i]) return false; return true; }

        static void ParseAsf(Stream fs, TagInfo t)
        {
            byte[] h = ReadAt(fs, 0, 30);
            if (h.Length < 30 || !GuidEq(h, 0, ASF_HEADER)) return;
            long headerSize = (long)(uint)Le32(h, 16);
            long p = 30;
            long limit = Math.Min(headerSize, fs.Length);
            while (p + 24 <= limit)
            {
                byte[] ob = ReadAt(fs, p, 24);
                if (ob.Length < 24) break;
                long osize = (long)(uint)Le64(ob, 16);
                long dp = p + 24;
                long dlen = osize - 24;
                if (osize < 24 || dp > limit) break;
                if (GuidEq(ob, 0, ASF_FILEPROPS) && dlen >= 80)
                {
                    byte[] d = ReadAt(fs, dp, 88);
                    if (d.Length >= 88)
                    {
                        long playDuration = Le64(d, 64);      // 100ns
                        long preroll = Le64(d, 80);           // ms
                        if (playDuration > 0) t.Duration = playDuration / 10000000.0 - preroll / 1000.0;
                    }
                }
                else if (GuidEq(ob, 0, ASF_CONTENTDESC))
                {
                    byte[] d = ReadAt(fs, dp, (int)Math.Min(dlen, 65536));
                    if (d.Length >= 10)
                    {
                        int l1 = Le16(d, 0), l2 = Le16(d, 2), l3 = Le16(d, 4), l4 = Le16(d, 6), l5 = Le16(d, 8);
                        int q = 10;
                        t.Title = Clean(Utf16Le(d, q, l1)); q += l1;
                        t.Artist = Clean(Utf16Le(d, q, l2)); q += l2;
                        q += l3;
                        q += l4;
                    }
                }
                else if (GuidEq(ob, 0, ASF_EXTDESC))
                {
                    byte[] d = ReadAt(fs, dp, (int)Math.Min(dlen, 262144));
                    if (d.Length >= 2)
                    {
                        int count = Le16(d, 0);
                        int q = 2;
                        for (int i = 0; i < count && q + 4 <= d.Length; i++)
                        {
                            int nl = Le16(d, q); q += 2;
                            string name = Utf16Le(d, q, nl); q += nl;
                            int type = Le16(d, q); q += 2;
                            int vl = Le16(d, q); q += 2;
                            if (q + vl > d.Length) break;
                            string val = "";
                            if (type == 0) val = Clean(Utf16Le(d, q, vl));
                            else if (type == 3 && vl >= 4) { int v = Le32(d, q); val = (v / 10000000).ToString(); }
                            q += vl;
                            if (name == "WM/AlbumTitle") t.Album = val;
                            else if (name == "WM/AlbumArtist") { if (t.AlbumArtist == "") t.AlbumArtist = val; }
                            else if (name == "WM/Genre" && t.Genre == "") t.Genre = val;
                            else if (name == "WM/Year" && t.Year == 0) { int y; int.TryParse(val, out y); if (y > 0) t.Year = y; }
                            else if (name == "WM/TrackNumber" && t.TrackNo == 0) { int n; int.TryParse(val, out n); t.TrackNo = n; }
                        }
                    }
                }
                else if (GuidEq(ob, 0, ASF_STREAMPROPS))
                {
                    byte[] d = ReadAt(fs, dp, (int)Math.Min(dlen, 80));
                    if (d.Length >= 74)
                    {
                        int chans = Le16(d, 40);
                        int rate = Le32(d, 44);
                        int bits = Le16(d, 54);
                        if (rate > 0)
                        {
                            t.SampleRate = rate;
                            t.Channels = chans;
                            t.Bitrate = rate * chans * bits / 1000;
                        }
                    }
                }
                if (osize <= 0) break;
                p = dp + dlen;
            }
        }

        static long Le64(byte[] b, int p) { return (long)(uint)Le32(b, p) | ((long)(uint)Le32(b, p + 4) << 32); }

        static string Utf16Le(byte[] b, int p, int len)
        {
            if (len <= 0 || p + len > b.Length) return "";
            try { return Encoding.Unicode.GetString(b, p, len - (len % 2)); }
            catch { return ""; }
        }

        // ---------------- AIFF ----------------
        static void ParseAiff(Stream fs, TagInfo t)
        {
            byte[] h = ReadAt(fs, 0, 12);
            if (h.Length < 12 || !Match(h, 0, "FORM")) return;
            long p = 12;
            while (p + 8 <= fs.Length)
            {
                byte[] ch = ReadAt(fs, p, 8);
                if (ch.Length < 8) break;
                string id = Ascii(ch, 0, 4);
                long len = (uint)Be32(ch, 4);
                long dp = p + 8;
                if (id == "COMM" && len >= 18)
                {
                    byte[] c = ReadAt(fs, dp, (int)Math.Min(len, 64));
                    if (c.Length >= 18)
                    {
                        int chans = Be16(c, 0);
                        long frames = (uint)Be32(c, 2);
                        int bits = Be16(c, 6);
                        double rate = Ext80(c, 8);
                        t.Channels = chans;
                        t.SampleRate = (int)Math.Round(rate);
                        if (rate > 0) t.Duration = frames / rate;
                        if (rate > 0) t.Bitrate = (int)(rate * chans * bits / 1000);
                    }
                }
                else if (id == "NAME") t.Title = Clean(Latin1(ReadAt(fs, dp, (int)Math.Min(len, 4096)), 0, (int)Math.Min(len, 4096)));
                else if (id == "AUTH") t.Artist = Clean(Latin1(ReadAt(fs, dp, (int)Math.Min(len, 4096)), 0, (int)Math.Min(len, 4096)));
                else if (id == "ANNO" && t.Album == "") t.Album = Clean(Latin1(ReadAt(fs, dp, (int)Math.Min(len, 4096)), 0, (int)Math.Min(len, 4096)));
                if (len <= 0) break;
                p = dp + len + (len % 2);
            }
        }

        static double Ext80(byte[] b, int p)
        {
            if (p + 10 > b.Length) return 0;
            int se = ((b[p] & 0x7F) << 8) | b[p + 1];
            long mant = 0;
            for (int i = 0; i < 8; i++) mant = (mant << 8) | b[p + 2 + i];
            int e = se - 16383 - 63;
            double v = mant * Math.Pow(2, e);
            if ((b[p] & 0x80) != 0) v = -v;
            return v;
        }

        // ---------------- APE / WavPack（尾部 APEv2 标签） ----------------
        static void ParseApe(Stream fs, TagInfo t)
        {
            long flen = fs.Length;
            long start = Math.Max(0, flen - 128);
            byte[] tail = ReadAt(fs, start, (int)(flen - start));
            int fp = -1;
            for (int i = tail.Length - 32; i >= 0; i--)
            {
                if (Match(tail, i, "APETAGEX")) { fp = i; break; }
            }
            if (fp < 0) { ParseId3v1(fs, t); return; }
            int itemCount = Le32(tail, fp + 12);
            int tagSize = Le32(tail, fp + 8);
            long tagStart = start + fp - (tagSize - 32);
            byte[] tag = ReadAt(fs, tagStart, tagSize);
            int q = 0;
            for (int i = 0; i < itemCount && q + 8 < tag.Length; i++)
            {
                int vlen = Le32(tag, q); q += 4;
                int flags = Le32(tag, q); q += 4;
                int kend = q;
                while (kend < tag.Length && tag[kend] != 0) kend++;
                string key = Ascii(tag, q, kend - q);
                q = kend + 1;
                if (vlen < 0 || q + vlen > tag.Length) break;
                string val = Utf8(tag, q, vlen);
                q += vlen;
                if (key == "Title") t.Title = val;
                else if (key == "Artist") t.Artist = val;
                else if (key == "Album") t.Album = val;
                else if (key == "Genre") t.Genre = val;
                else if (key == "Year") { int y; int.TryParse(val.Length >= 4 ? val.Substring(0, 4) : val, out y); if (y > 0) t.Year = y; }
                else if (key == "Track") { int n; int.TryParse(val, out n); t.TrackNo = n; }
            }
        }
    }
}