using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    public class Track
    {
        public string Path = "";
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public string AlbumArtist = "";
        public string Genre = "";
        public int Year;
        public int TrackNo;
        public double Duration;
        public int Bitrate;
        public int SampleRate;
        public long Size;
        public string Ext = "";
        public bool TagsLoaded;
        public bool TagsFailed;
        public string Lyrics = "";

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(Title)) return Title;
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Path; }
            }
        }

        public string DisplayArtist
        {
            get { return string.IsNullOrEmpty(Artist) ? "未知艺术家" : Artist; }
        }

        public string DisplayAlbum
        {
            get { return string.IsNullOrEmpty(Album) ? "" : Album; }
        }
    }

    public static class Library
    {
        public static readonly string[] Columns = new string[] { "#", "标题", "艺术家", "专辑", "时长", "格式", "比特率", "大小", "文件路径" };

        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "--:--";
            int total = (int)Math.Round(seconds);
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, s);
            return string.Format("{0:00}:{1:00}", m, s);
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1073741824.0).ToString("0.00") + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / 1048576.0).ToString("0.0") + " MB";
            return (bytes / 1024.0).ToString("0") + " KB";
        }

        public static string FormatBitrate(int kbps)
        {
            return kbps > 0 ? (kbps + " kbps") : "";
        }

        // 只读取文件名/大小等基础信息，不打开文件内容（极快）
        public static Track MakeTrack(string path)
        {
            Track t = new Track();
            t.Path = path;
            t.Ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            try
            {
                FileInfo fi = new FileInfo(path);
                t.Size = fi.Length;
            }
            catch { }
            return t;
        }

        public static void FillTags(Track t, TagCache cache, Settings s)
        {
            try
            {
                long size = t.Size;
                DateTime mt = DateTime.MinValue;
                try { mt = File.GetLastWriteTimeUtc(t.Path); } catch { }
                TagInfo info = null;
                if (s.UseCache && cache != null) info = cache.Get(t.Path, size, mt);
                if (info == null)
                {
                    info = TagReader.Read(t.Path);
                    if (s.UseCache && cache != null) cache.Put(t.Path, size, mt, info);
                }
                t.Title = info.Title;
                t.Artist = info.Artist;
                t.Album = info.Album;
                t.AlbumArtist = info.AlbumArtist;
                t.Genre = info.Genre;
                t.Year = info.Year;
                t.TrackNo = info.TrackNo;
                t.Duration = info.Duration;
                t.Bitrate = info.Bitrate;
                t.SampleRate = info.SampleRate;
                t.Lyrics = info.Lyrics;
                t.TagsLoaded = true;
            }
            catch { t.TagsFailed = true; t.TagsLoaded = true; }
        }

        public static List<string> CollectFiles(List<string> folders, Settings s, Func<bool> cancelled, Action<string> progress)
        {
            List<string> res = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in folders)
            {
                if (cancelled != null && cancelled()) break;
                if (Directory.Exists(f)) Walk(f, s, res, seen, cancelled, progress);
                else if (File.Exists(f))
                {
                    if (TagReader.IsAudio(f, s.Extensions) && !seen.Contains(f)) { seen.Add(f); res.Add(f); }
                }
            }
            return res;
        }

        static bool IsExcluded(string dir, List<string> excludes)
        {
            if (excludes == null) return false;
            string name = Path.GetFileName(dir);
            if (name.StartsWith("$") || name.StartsWith(".")) return true;
            foreach (string e in excludes)
            {
                if (string.IsNullOrEmpty(e)) continue;
                if (string.Equals(dir, e, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Length > 0 && string.Equals(name, e, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static void Walk(string dir, Settings s, List<string> res, HashSet<string> seen, Func<bool> cancelled, Action<string> progress)
        {
            if (cancelled != null && cancelled()) return;
            if (IsExcluded(dir, s.ExcludeDirs)) return;
            string[] files = null;
            try { files = Directory.GetFiles(dir); }
            catch { return; }
            foreach (string f in files)
            {
                if (cancelled != null && cancelled()) return;
                if (!TagReader.IsAudio(f, s.Extensions)) continue;
                if (seen.Contains(f)) continue;
                seen.Add(f);
                res.Add(f);
                if (progress != null && (res.Count % 64) == 0) progress(dir);
            }
            if (!s.Recursive) return;
            string[] dirs = null;
            try { dirs = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (string d in dirs)
            {
                if (cancelled != null && cancelled()) return;
                Walk(d, s, res, seen, cancelled, progress);
            }
        }

        // 排序
        public static Comparison<Track> Comparer(int column, bool asc)
        {
            return delegate(Track a, Track b)
            {
                int r = 0;
                switch (column)
                {
                    case 1: r = string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.CurrentCultureIgnoreCase); break;
                    case 2: r = string.Compare(a.DisplayArtist, b.DisplayArtist, StringComparison.CurrentCultureIgnoreCase); break;
                    case 3: r = string.Compare(a.DisplayAlbum, b.DisplayAlbum, StringComparison.CurrentCultureIgnoreCase); break;
                    case 4: r = a.Duration.CompareTo(b.Duration); break;
                    case 5: r = string.Compare(a.Ext, b.Ext, StringComparison.OrdinalIgnoreCase); break;
                    case 6: r = a.Bitrate.CompareTo(b.Bitrate); break;
                    case 7: r = a.Size.CompareTo(b.Size); break;
                    case 8: r = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase); break;
                    default: r = 0; break;
                }
                if (r == 0) r = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                return asc ? r : -r;
            };
        }

        public static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '(' || c == ')' || c > 127) sb.Append(c);
                else sb.Append('_');
            }
            return sb.ToString().Trim();
        }
    }

    // ================= 标签缓存（避免重复解析，加快二次扫描）=================
    public class TagCache
    {
        class Entry
        {
            public long Size;
            public long MTime;
            public string Title = "", Artist = "", Album = "", AlbumArtist = "", Genre = "";
            public int Year, TrackNo;
            public double Duration;
            public int Bitrate, SampleRate;
        }

        Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        bool dirty;

        public static TagCache Load(string file)
        {
            TagCache c = new TagCache();
            try
            {
                if (!File.Exists(file)) return c;
                List<object> arr = Json.Decode(File.ReadAllText(file, Encoding.UTF8)) as List<object>;
                if (arr == null) return c;
                foreach (object o in arr)
                {
                    Dictionary<string, object> d = Json.AsObj(o);
                    if (d == null) continue;
                    string p = Json.GetStr(d, "p", null);
                    if (string.IsNullOrEmpty(p)) continue;
                    Entry e = new Entry();
                    e.Size = Json.GetLong(d, "sz", 0);
                    e.MTime = Json.GetLong(d, "mt", 0);
                    e.Title = Json.GetStr(d, "t", ""); e.Artist = Json.GetStr(d, "a", "");
                    e.Album = Json.GetStr(d, "al", ""); e.AlbumArtist = Json.GetStr(d, "aa", "");
                    e.Genre = Json.GetStr(d, "g", "");
                    e.Year = Json.GetInt(d, "y", 0); e.TrackNo = Json.GetInt(d, "n", 0);
                    e.Duration = Json.GetLong(d, "dur", 0) / 1000.0;
                    e.Bitrate = Json.GetInt(d, "br", 0); e.SampleRate = Json.GetInt(d, "sr", 0);
                    c.map[p] = e;
                }
            }
            catch { }
            return c;
        }

        public void Save(string file)
        {
            try
            {
                if (!dirty) return;
                List<object> arr = new List<object>();
                foreach (KeyValuePair<string, Entry> kv in map)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    Entry e = kv.Value;
                    d["p"] = kv.Key; d["sz"] = e.Size; d["mt"] = e.MTime;
                    d["t"] = e.Title; d["a"] = e.Artist; d["al"] = e.Album; d["aa"] = e.AlbumArtist; d["g"] = e.Genre;
                    d["y"] = e.Year; d["n"] = e.TrackNo;
                    d["dur"] = (long)Math.Round(e.Duration * 1000);
                    d["br"] = e.Bitrate; d["sr"] = e.SampleRate;
                    arr.Add(d);
                }
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(file, Json.Encode(arr), new UTF8Encoding(true));
            }
            catch { }
        }

        static long ToUnix(DateTime t)
        {
            try { return (long)(t.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
            catch { return 0; }
        }

        public TagInfo Get(string path, long size, DateTime mtime)
        {
            Entry e;
            if (!map.TryGetValue(path, out e)) return null;
            if (e.Size != size) return null;
            long mt = ToUnix(mtime);
            if (e.MTime != mt && mt != 0) return null;
            TagInfo t = new TagInfo();
            t.Title = e.Title; t.Artist = e.Artist; t.Album = e.Album; t.AlbumArtist = e.AlbumArtist;
            t.Genre = e.Genre; t.Year = e.Year; t.TrackNo = e.TrackNo;
            t.Duration = e.Duration; t.Bitrate = e.Bitrate; t.SampleRate = e.SampleRate;
            return t;
        }

        public void Put(string path, long size, DateTime mtime, TagInfo t)
        {
            Entry e = new Entry();
            e.Size = size;
            e.MTime = ToUnix(mtime);
            e.Title = t.Title; e.Artist = t.Artist; e.Album = t.Album; e.AlbumArtist = t.AlbumArtist;
            e.Genre = t.Genre; e.Year = t.Year; e.TrackNo = t.TrackNo;
            e.Duration = t.Duration; e.Bitrate = t.Bitrate; e.SampleRate = t.SampleRate;
            map[path] = e;
            dirty = true;
        }

        public void Clear()
        {
            map.Clear();
            dirty = true;
        }

        public int Count { get { return map.Count; } }
    }
}