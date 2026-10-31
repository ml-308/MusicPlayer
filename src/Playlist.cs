using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    public class Playlist
    {
        public List<Track> Tracks = new List<Track>();
        public int Current = -1;
        public string Mode = "repeatAll";      // sequential / repeatAll / repeatOne / shuffle
        public bool StopAfterCurrent = false;
        public int HistoryLength = 30;

        List<int> history = new List<int>();
        Random rnd = new Random();

        public static string ModeName(string mode)
        {
            if (mode == "sequential") return "顺序播放";
            if (mode == "repeatOne") return "单曲循环";
            if (mode == "shuffle") return "随机播放";
            return "列表循环";
        }

        public static string ModeNext(string mode)
        {
            if (mode == "sequential") return "repeatAll";
            if (mode == "repeatAll") return "repeatOne";
            if (mode == "repeatOne") return "shuffle";
            return "sequential";
        }

        public Track CurrentTrack
        {
            get { return (Current >= 0 && Current < Tracks.Count) ? Tracks[Current] : null; }
        }

        public void Clear()
        {
            Tracks.Clear();
            Current = -1;
            history.Clear();
        }

        public void Add(Track t) { Tracks.Add(t); }

        public void ResetHistory()
        {
            history.Clear();
            if (Current >= 0) history.Add(Current);
        }

        public int NextIndex(bool auto)
        {
            int n = Tracks.Count;
            if (n == 0) return -1;
            if (StopAfterCurrent && auto) return -1;
            if (Current < 0) return 0;
            if (Mode == "repeatOne") return auto ? Current : ((Current + 1) % n);
            if (Mode == "shuffle")
            {
                if (n == 1) return 0;
                int tries = 0;
                while (tries++ < 200)
                {
                    int k = rnd.Next(n);
                    if (k == Current) continue;
                    if (history.Contains(k)) continue;
                    Push(k);
                    return k;
                }
                int fallback = rnd.Next(n);
                if (fallback == Current && n > 1) fallback = (fallback + 1) % n;
                Push(fallback);
                return fallback;
            }
            int next = Current + 1;
            if (next >= n)
            {
                if (Mode == "sequential") return -1;
                next = 0;
            }
            return next;
        }

        void Push(int idx)
        {
            history.Add(idx);
            while (history.Count > Math.Max(2, HistoryLength)) history.RemoveAt(0);
        }

        public int PrevIndex()
        {
            int n = Tracks.Count;
            if (n == 0) return -1;
            if (Mode == "shuffle")
            {
                if (history.Count > 1)
                {
                    history.RemoveAt(history.Count - 1);
                    return history[history.Count - 1];
                }
                int k = rnd.Next(n);
                Push(k);
                return k;
            }
            if (Mode == "repeatOne") return Current < 0 ? 0 : Current;
            int p = Current - 1;
            if (p < 0) p = (Mode == "sequential") ? 0 : n - 1;
            return p;
        }

        public void ShuffleAll()
        {
            int n = Tracks.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                Track t = Tracks[i]; Tracks[i] = Tracks[j]; Tracks[j] = t;
            }
            Current = -1;
            history.Clear();
        }

        // ---- M3U 导入导出 ----
        public static List<string> ImportM3U(string file)
        {
            List<string> res = new List<string>();
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(file));
                foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("#")) continue;
                    string p = line;
                    if (!Path.IsPathRooted(p)) p = Path.Combine(dir, p);
                    if (File.Exists(p)) res.Add(p);
                }
            }
            catch { }
            return res;
        }

        public static void ExportM3U(string file, List<Track> tracks)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            foreach (Track t in tracks)
            {
                sb.AppendLine("#EXTINF:" + (int)Math.Round(t.Duration) + "," + t.DisplayArtist + " - " + t.DisplayTitle);
                sb.AppendLine(t.Path);
            }
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        }
    }
}