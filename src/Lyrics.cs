using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicPlayer
{
    public class LyricLine
    {
        public long Time;
        public int Seq;              // 文件中的原始顺序，用于保证同时间戳时的稳定排序
        public string Text = "";
        public string Trans = "";
    }

    public class Lyrics
    {
        public List<LyricLine> Lines = new List<LyricLine>();
        public bool Synced;
        public bool Found;
        public string Source = "";

        public int IndexAt(long ms)
        {
            int lo = 0, hi = Lines.Count - 1, res = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Lines[mid].Time <= ms) { res = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return res;
        }

        public static Lyrics Empty { get { return new Lyrics(); } }
    }

    // ============ 歌词查找与解析（.lrc 同目录匹配 + 内嵌歌词） ============
    public static class LyricLoader
    {
        static Dictionary<string, Lyrics> cache = new Dictionary<string, Lyrics>(StringComparer.OrdinalIgnoreCase);

        static readonly Regex TimeTag = new Regex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        static readonly Regex MetaTag = new Regex(@"^\[(ti|ar|al|by|offset|length|re|ve|au|tool):(.*)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex WordTag = new Regex(@"<\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?>", RegexOptions.Compiled);

        public static void ClearCache() { cache.Clear(); }

        public static Lyrics ForTrack(Track t)
        {
            if (t == null) return Lyrics.Empty;
            Lyrics ly;
            if (cache.TryGetValue(t.Path, out ly)) return ly;
            ly = Load(t.Path, t.Lyrics, t.Title, t.Artist);
            cache[t.Path] = ly;
            return ly;
        }

        public static Lyrics Load(string audioPath, string embedded, string title, string artist)
        {
            string path = FindLrcFile(audioPath, title, artist);
            if (path != null)
            {
                try
                {
                    string text = ReadTextSmart(path);
                    Lyrics ly = ParseLrc(text);
                    if (ly.Found) { ly.Source = path; return ly; }
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(embedded))
            {
                Lyrics ly = ParseLrc(embedded);
                if (ly.Found) { ly.Source = "内嵌歌词"; return ly; }
            }
            return Lyrics.Empty;
        }

        static string FindLrcFile(string audioPath, string title, string artist)
        {
            try
            {
                if (string.IsNullOrEmpty(audioPath)) return null;
                string dir = Path.GetDirectoryName(audioPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
                string baseName = Path.GetFileNameWithoutExtension(audioPath);
                List<string> names = new List<string>();
                names.Add(baseName + ".lrc");
                names.Add(baseName + ".LRC");
                names.Add(baseName + ".Lrc");
                if (!string.IsNullOrEmpty(title))
                {
                    names.Add(title + ".lrc");
                    if (!string.IsNullOrEmpty(artist)) names.Add(artist + " - " + title + ".lrc");
                }
                foreach (string n in names)
                {
                    string p = Path.Combine(dir, n);
                    if (File.Exists(p)) return p;
                }
                // 忽略大小写再找一遍（某些文件系统区分大小写）
                string target = baseName + ".lrc";
                foreach (string f in Directory.GetFiles(dir, "*.lrc"))
                    if (string.Equals(Path.GetFileName(f), target, StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch { }
            return null;
        }

        public static string ReadTextSmart(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch { }
            try { return Encoding.GetEncoding(936).GetString(b); }
            catch { }
            try { return Encoding.Default.GetString(b); } catch { return ""; }
        }

        public static Lyrics ParseLrc(string text)
        {
            Lyrics ly = new Lyrics();
            if (string.IsNullOrEmpty(text)) return ly;
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            long offset = 0;
            bool anyTime = false;
            List<LyricLine> tmp = new List<LyricLine>();

            foreach (string line0 in raw)
            {
                string line = line0.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("["))
                {
                    Match meta = MetaTag.Match(line);
                    if (meta.Success)
                    {
                        if (string.Equals(meta.Groups[1].Value, "offset", StringComparison.OrdinalIgnoreCase))
                        {
                            long o;
                            string v = meta.Groups[2].Value.Trim().Replace("+", "");
                            if (long.TryParse(v, out o)) offset = o;
                        }
                        continue;
                    }
                }
                MatchCollection ms = TimeTag.Matches(line);
                if (ms.Count == 0)
                {
                    LyricLine l0 = new LyricLine();
                    l0.Time = 0;
                    l0.Seq = tmp.Count;
                    l0.Text = WordTag.Replace(line, "").Trim();
                    if (l0.Text.Length > 0) tmp.Add(l0);
                    continue;
                }
                string body = TimeTag.Replace(line, "").Trim();
                body = WordTag.Replace(body, "").Trim();
                foreach (Match m in ms)
                {
                    int mm0, ss0, milli;
                    if (!int.TryParse(m.Groups[1].Value, out mm0)) continue;
                    if (!int.TryParse(m.Groups[2].Value, out ss0)) continue;
                    string frac = m.Groups[3].Value;
                    milli = 0;
                    if (frac.Length > 0)
                    {
                        if (frac.Length == 1) milli = int.Parse(frac) * 100;
                        else if (frac.Length == 2) milli = int.Parse(frac) * 10;
                        else milli = int.Parse(frac.Substring(0, 3));
                    }
                    LyricLine l = new LyricLine();
                    l.Time = mm0 * 60000L + ss0 * 1000L + milli;
                    l.Seq = tmp.Count;
                    l.Text = body;
                    tmp.Add(l);
                    anyTime = true;
                }
            }

            if (!anyTime)
            {
                ly.Lines = tmp;
                ly.Synced = false;
                ly.Found = tmp.Count > 0;
                return ly;
            }

            // 稳定排序：时间相同时保持文件中的先后（原文在前、翻译在后）
            tmp.Sort(delegate(LyricLine a, LyricLine b)
            {
                int c = a.Time.CompareTo(b.Time);
                if (c != 0) return c;
                return a.Seq.CompareTo(b.Seq);
            });

            // 相同时间戳的第二行视为翻译
            List<LyricLine> merged = new List<LyricLine>();
            foreach (LyricLine l in tmp)
            {
                if (merged.Count > 0 && merged[merged.Count - 1].Time == l.Time && merged[merged.Count - 1].Trans.Length == 0 && l.Text.Length > 0)
                {
                    merged[merged.Count - 1].Trans = l.Text;
                }
                else merged.Add(l);
            }

            if (offset != 0)
                foreach (LyricLine l in merged) l.Time -= offset;

            List<LyricLine> fin = new List<LyricLine>();
            foreach (LyricLine l in merged)
                if (l.Text.Trim().Length > 0 || l.Trans.Length > 0) fin.Add(l);

            ly.Lines = fin;
            ly.Synced = true;
            ly.Found = fin.Count > 0;
            return ly;
        }
    }
}