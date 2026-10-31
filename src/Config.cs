using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    // ============ 极简 JSON 读写（不依赖任何第三方库） ============
    public static class Json
    {
        public static string Encode(object o)
        {
            StringBuilder sb = new StringBuilder();
            Enc(sb, o, 0);
            return sb.ToString();
        }

        static void Enc(StringBuilder sb, object o, int ind)
        {
            string pad = new string(' ', ind + 2);
            string pad2 = new string(' ', ind);
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { EncStr(sb, (string)o); return; }
            if (o is bool) { sb.Append(((bool)o) ? "true" : "false"); return; }
            if (o is int || o is long) { sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture)); return; }
            if (o is double) { sb.Append(((double)o).ToString("0.###", CultureInfo.InvariantCulture)); return; }
            if (o is System.Collections.IDictionary)
            {
                var d = (System.Collections.IDictionary)o;
                if (d.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (System.Collections.DictionaryEntry e in d)
                {
                    sb.Append(pad); EncStr(sb, Convert.ToString(e.Key)); sb.Append(": ");
                    Enc(sb, e.Value, ind + 2);
                    if (++i < d.Count) sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append(pad2); sb.Append("}");
                return;
            }
            if (o is System.Collections.IEnumerable)
            {
                bool any = false;
                sb.Append("[");
                System.Text.StringBuilder tmp = new StringBuilder();
                foreach (object item in (System.Collections.IEnumerable)o)
                {
                    if (any) tmp.Append(", ");
                    Enc(tmp, item, ind + 2);
                    any = true;
                }
                sb.Append(tmp.ToString());
                sb.Append("]");
                return;
            }
            EncStr(sb, Convert.ToString(o, CultureInfo.InvariantCulture));
        }

        static void EncStr(StringBuilder sb, string s)
        {
            if (s == null) { sb.Append("null"); return; }
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object Decode(string text)
        {
            if (text == null) return null;
            int i = 0;
            object v = Value(text, ref i);
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') { i++; return Obj(s, ref i); }
            if (c == '[') { i++; return Arr(s, ref i); }
            if (c == '"') { i++; return Str(s, ref i); }
            if (string.Compare(s, i, "true", 0, 4, true) == 0) { i += 4; return true; }
            if (string.Compare(s, i, "false", 0, 5, true) == 0) { i += 5; return false; }
            if (string.Compare(s, i, "null", 0, 4, true) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            string num = s.Substring(st, i - st);
            double d;
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            Skip(s, ref i);
            while (i < s.Length && s[i] != '}')
            {
                Skip(s, ref i);
                if (s[i] == '}') break;
                if (s[i] != '"') { i++; continue; }
                i++;
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object v = Value(s, ref i);
                d[key] = v;
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }
            if (i < s.Length) i++;
            return d;
        }

        static List<object> Arr(string s, ref int i)
        {
            List<object> list = new List<object>();
            Skip(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                list.Add(Value(s, ref i));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
                Skip(s, ref i);
            }
            if (i < s.Length) i++;
            return list;
        }

        static string Str(string s, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 <= s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------- 便捷读取 ----------
        public static Dictionary<string, object> AsObj(object o) { return o as Dictionary<string, object>; }
        public static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            return v == null ? def : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            double dd;
            if (v is double) dd = (double)v;
            else if (!double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out dd)) return def;
            return (int)Math.Round(dd);
        }
        public static long GetLong(Dictionary<string, object> d, string key, long def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            double dd;
            if (v is double) dd = (double)v;
            else if (!double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out dd)) return def;
            return (long)Math.Round(dd);
        }
        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v is bool) return (bool)v;
            if (v == null) return def;
            bool b;
            if (bool.TryParse(Convert.ToString(v), out b)) return b;
            double dd;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out dd)) return dd != 0;
            return def;
        }
        public static List<string> GetStrList(Dictionary<string, object> d, string key)
        {
            List<string> res = new List<string>();
            if (d == null || !d.ContainsKey(key)) return res;
            var l = d[key] as List<object>;
            if (l == null) return res;
            foreach (object o in l) if (o != null) res.Add(Convert.ToString(o));
            return res;
        }
    }

    // ============ 设置 ============
    public class Settings
    {
        public List<string> Folders = new List<string>();
        public List<string> ExcludeDirs = new List<string>();
        public List<string> Extensions = DefaultExtensions();
        public int Volume = 75;
        public string PlayMode = "repeatAll";      // sequential / repeatAll / repeatOne / shuffle
        public bool Recursive = true;
        public bool ScanOnStart = true;
        public bool RememberState = true;
        public bool UseCache = true;
        public bool ShowCover = true;
        public string Theme = "dark";              // dark / light
        public string Engine = "auto";             // auto / winrt / mci
        public bool MinimizeTray = false;          // 关闭窗口时最小化到托盘
        public bool MinimizeToTrayInstead = false;
        public int SeekStep = 5;                   // 快进/快退秒数
        public int HistoryLength = 30;             // 随机播放不重复深度
        public bool StopAfterCurrent = false;
        public int SleepTimerMinutes = 0;          // 0 = 关闭
        public int SortColumn = 0;                 // 0=不排序(原始顺序)
        public bool SortAsc = true;
        public bool LyricsEnabled = true;          // 桌面歌词
        public int LyricsFontSize = 30;
        public int LyricsOpacity = 88;
        public bool LyricsLocked = false;
        public int LyricsX = -1, LyricsY = -1;
        public int WindowX = -1, WindowY = -1, WindowW = 1080, WindowH = 680;
        public string LastTrack = "";
        public long LastPositionMs = 0;
        public List<string> LastPlaylist = new List<string>();

        public static List<string> DefaultExtensions()
        {
            return new List<string>(new string[] {
                ".mp3", ".wav", ".wma", ".flac", ".m4a", ".aac", ".ogg", ".oga",
                ".opus", ".aif", ".aiff", ".ape", ".wv", ".mp4", ".mka", ".alac"
            });
        }

        public static Settings Load(string file)
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(file)) return s;
                Dictionary<string, object> d = Json.AsObj(Json.Decode(File.ReadAllText(file, Encoding.UTF8)));
                if (d == null) return s;
                s.Folders = Json.GetStrList(d, "folders");
                s.ExcludeDirs = Json.GetStrList(d, "exclude");
                List<string> ex = Json.GetStrList(d, "extensions");
                if (ex.Count > 0) s.Extensions = ex;
                s.Volume = Clamp(Json.GetInt(d, "volume", s.Volume), 0, 100);
                s.PlayMode = Json.GetStr(d, "playMode", s.PlayMode);
                s.Recursive = Json.GetBool(d, "recursive", s.Recursive);
                s.ScanOnStart = Json.GetBool(d, "scanOnStart", s.ScanOnStart);
                s.RememberState = Json.GetBool(d, "rememberState", s.RememberState);
                s.UseCache = Json.GetBool(d, "useCache", s.UseCache);
                s.ShowCover = Json.GetBool(d, "showCover", s.ShowCover);
                s.Theme = Json.GetStr(d, "theme", s.Theme);
                s.Engine = Json.GetStr(d, "engine", s.Engine);
                s.MinimizeToTrayInstead = Json.GetBool(d, "minimizeTray", s.MinimizeToTrayInstead);
                s.SeekStep = Clamp(Json.GetInt(d, "seekStep", s.SeekStep), 1, 60);
                s.HistoryLength = Clamp(Json.GetInt(d, "history", s.HistoryLength), 2, 500);
                s.StopAfterCurrent = Json.GetBool(d, "stopAfterCurrent", false);
                s.SleepTimerMinutes = Json.GetInt(d, "sleepTimer", 0);
                s.SortColumn = Json.GetInt(d, "sortColumn", 0);
                s.SortAsc = Json.GetBool(d, "sortAsc", true);
                s.LyricsEnabled = Json.GetBool(d, "lyrics", s.LyricsEnabled);
                s.LyricsFontSize = Clamp(Json.GetInt(d, "lyricsSize", s.LyricsFontSize), 14, 64);
                s.LyricsOpacity = Clamp(Json.GetInt(d, "lyricsOpacity", s.LyricsOpacity), 25, 100);
                s.LyricsLocked = Json.GetBool(d, "lyricsLocked", s.LyricsLocked);
                s.LyricsX = Json.GetInt(d, "lyricsX", -1);
                s.LyricsY = Json.GetInt(d, "lyricsY", -1);
                s.WindowX = Json.GetInt(d, "winX", -1);
                s.WindowY = Json.GetInt(d, "winY", -1);
                s.WindowW = Json.GetInt(d, "winW", 1080);
                s.WindowH = Json.GetInt(d, "winH", 680);
                s.LastTrack = Json.GetStr(d, "lastTrack", "");
                s.LastPositionMs = Json.GetLong(d, "lastPos", 0);
                s.LastPlaylist = Json.GetStrList(d, "lastPlaylist");
            }
            catch { }
            return s;
        }

        public void Save(string file)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["folders"] = Folders;
                d["exclude"] = ExcludeDirs;
                d["extensions"] = Extensions;
                d["volume"] = Volume;
                d["playMode"] = PlayMode;
                d["recursive"] = Recursive;
                d["scanOnStart"] = ScanOnStart;
                d["rememberState"] = RememberState;
                d["useCache"] = UseCache;
                d["showCover"] = ShowCover;
                d["theme"] = Theme;
                d["engine"] = Engine;
                d["minimizeTray"] = MinimizeToTrayInstead;
                d["seekStep"] = SeekStep;
                d["history"] = HistoryLength;
                d["stopAfterCurrent"] = StopAfterCurrent;
                d["sleepTimer"] = SleepTimerMinutes;
                d["sortColumn"] = SortColumn;
                d["sortAsc"] = SortAsc;
                d["lyrics"] = LyricsEnabled;
                d["lyricsSize"] = LyricsFontSize;
                d["lyricsOpacity"] = LyricsOpacity;
                d["lyricsLocked"] = LyricsLocked;
                d["lyricsX"] = LyricsX;
                d["lyricsY"] = LyricsY;
                d["winX"] = WindowX;
                d["winY"] = WindowY;
                d["winW"] = WindowW;
                d["winH"] = WindowH;
                d["lastTrack"] = LastTrack;
                d["lastPos"] = LastPositionMs;
                d["lastPlaylist"] = LastPlaylist;
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(file, Json.Encode(d), new UTF8Encoding(true));
            }
            catch { }
        }

        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}