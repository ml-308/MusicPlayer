using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace MusicPlayer
{
    public enum PlayState { Stopped, Playing, Paused }

    public interface IAudioBackend : IDisposable
    {
        string Name { get; }
        bool Loaded { get; }
        bool HasEnded { get; }
        long PositionMs { get; set; }
        long DurationMs { get; }
        int Volume { get; set; }
        PlayState State { get; }
        string LastError { get; }
        void Load(string path);
        void Play();
        void Pause();
        void Stop();
        void Unload();
    }

    // ================= Windows 媒体引擎（Media Foundation / WinRT）=================
    public class WinRtBackend : IAudioBackend
    {
        MediaPlayer mp;
        bool ended, failed, loaded;
        string err = "";

        public WinRtBackend()
        {
            mp = new MediaPlayer();
            mp.MediaEnded += delegate(MediaPlayer s, object a) { ended = true; };
            mp.MediaFailed += delegate(MediaPlayer s, MediaPlayerFailedEventArgs a)
            {
                err = a.Error.ToString() + (string.IsNullOrEmpty(a.ErrorMessage) ? "" : (" - " + a.ErrorMessage));
                failed = true;
            };
        }

        public string Name { get { return "Windows 媒体引擎"; } }
        public bool Loaded { get { return loaded; } }
        public string LastError { get { return err; } }
        public bool HasEnded { get { return ended && !failed; } }
        public bool Failed { get { return failed; } }

        public void Load(string path)
        {
            ended = false; failed = false; loaded = false; err = "";
            mp.Source = MediaSource.CreateFromUri(new Uri(path));
            loaded = true;
            mp.Play();
        }

        public void Play()
        {
            try { mp.Play(); } catch { }
        }

        public void Pause() { try { mp.Pause(); } catch { } }
        public void Stop() { try { mp.Pause(); mp.PlaybackSession.Position = TimeSpan.Zero; } catch { } }

        public void Unload()
        {
            try { mp.Source = null; } catch { }
            loaded = false;
        }

        public long PositionMs
        {
            get
            {
                try { return (long)mp.PlaybackSession.Position.TotalMilliseconds; }
                catch { return 0; }
            }
            set
            {
                try { mp.PlaybackSession.Position = TimeSpan.FromMilliseconds(value < 0 ? 0 : value); }
                catch { }
            }
        }

        public long DurationMs
        {
            get
            {
                try
                {
                    double ms = mp.PlaybackSession.NaturalDuration.TotalMilliseconds;
                    if (ms > 0 && !double.IsInfinity(ms)) return (long)ms;
                }
                catch { }
                return 0;
            }
        }

        public int Volume
        {
            get { try { return (int)Math.Round(mp.Volume * 100); } catch { return 0; } }
            set { try { mp.Volume = Settings.Clamp(value, 0, 100) / 100.0; } catch { } }
        }

        public PlayState State
        {
            get
            {
                try
                {
                    MediaPlaybackState st = mp.PlaybackSession.PlaybackState;
                    if (st == MediaPlaybackState.Playing || st == MediaPlaybackState.Buffering || st == MediaPlaybackState.Opening) return PlayState.Playing;
                    if (st == MediaPlaybackState.Paused) return PlayState.Paused;
                }
                catch { }
                return PlayState.Stopped;
            }
        }

        public void Dispose()
        {
            try { mp.Source = null; mp.Dispose(); } catch { }
            loaded = false;
        }
    }

    // ================= MCI（winmm，兼容后备）=================
    public class MciBackend : IAudioBackend
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern uint mciSendString(string cmd, StringBuilder ret, uint len, IntPtr hwnd);
        [DllImport("winmm.dll")]
        static extern bool mciGetErrorString(uint err, StringBuilder buf, uint len);

        static int counter = 0;
        string alias;
        bool loaded, volOk = true, ended, userStopped, playedOnce;
        long durationMs, positionHint;
        int volume = 100;
        string err = "";

        public MciBackend()
        {
            counter++;
            alias = "mpmci" + counter.ToString();
        }

        public string Name { get { return "MCI 音频接口"; } }
        public bool Loaded { get { return loaded; } }
        public string LastError { get { return err; } }
        public bool VolumeUnsupported { get { return !volOk; } }

        static string Cmd(string cmd)
        {
            StringBuilder sb = new StringBuilder(700);
            uint e = mciSendString(cmd, sb, 700, IntPtr.Zero);
            if (e != 0)
            {
                StringBuilder eb = new StringBuilder(300);
                mciGetErrorString(e, eb, 300);
                return null;
            }
            return sb.ToString();
        }

        static string CmdOut(string cmd)
        {
            StringBuilder sb = new StringBuilder(700);
            uint e = mciSendString(cmd, sb, 700, IntPtr.Zero);
            if (e != 0) return null;
            return sb.ToString();
        }

        public void Load(string path)
        {
            loaded = false; ended = false; userStopped = false; playedOnce = false; err = "";
            string p = path.Replace('/', '\\');
            string ext = Path.GetExtension(p).ToLowerInvariant();
            string[] types;
            if (ext == ".wav" || ext == ".aif" || ext == ".aiff") types = new string[] { "waveaudio", "mpegvideo", "digitalvideo", null };
            else types = new string[] { "mpegvideo", "digitalvideo", "waveaudio", null };

            foreach (string t in types)
            {
                string cmd = "open \"" + p + "\" alias " + alias;
                if (t != null) cmd = "open \"" + p + "\" type " + t + " alias " + alias;
                if (Cmd(cmd) != null)
                {
                    loaded = true;
                    Cmd("set " + alias + " time format milliseconds");
                    string len = CmdOut("status " + alias + " length");
                    long d = 0;
                    if (len != null) long.TryParse(len.Trim(), out d);
                    durationMs = d;
                    return;
                }
                Cmd("close " + alias);
            }
            err = "MCI 无法打开此格式";
        }

        public void Play()
        {
            if (!loaded) return;
            userStopped = false;
            if (positionHint > 0) { Cmd("seek " + alias + " to " + positionHint); Cmd("play " + alias); }
            else Cmd("play " + alias);
            playedOnce = true;
        }

        public void Pause()
        {
            if (!loaded) return;
            Cmd("pause " + alias);
            positionHint = PositionMs;
        }

        public void Stop()
        {
            if (!loaded) return;
            userStopped = true;
            Cmd("stop " + alias);
            positionHint = 0;
        }

        public void Unload()
        {
            if (loaded) Cmd("close " + alias);
            loaded = false;
        }

        public long PositionMs
        {
            get
            {
                if (!loaded) return 0;
                string s = CmdOut("status " + alias + " position");
                long v = 0;
                if (s != null) long.TryParse(s.Trim(), out v);
                if (s == null) v = positionHint;
                return v;
            }
            set
            {
                if (!loaded) return;
                long v = value < 0 ? 0 : value;
                positionHint = v;
                Cmd("seek " + alias + " to " + v.ToString());
            }
        }

        public long DurationMs
        {
            get
            {
                if (!loaded) return 0;
                return durationMs;
            }
        }

        public int Volume
        {
            get { return volume; }
            set
            {
                volume = Settings.Clamp(value, 0, 100);
                if (!loaded) return;
                string r = Cmd("setaudio " + alias + " volume to " + (volume * 10).ToString());
                if (r == null) volOk = false;
            }
        }

        public PlayState State
        {
            get
            {
                if (!loaded) return PlayState.Stopped;
                string m = CmdOut("status " + alias + " mode");
                if (m == null) return PlayState.Stopped;
                m = m.Trim();
                if (m == "playing") return PlayState.Playing;
                if (m == "paused") return PlayState.Paused;
                return PlayState.Stopped;
            }
        }

        public bool HasEnded
        {
            get
            {
                if (!loaded || ended || userStopped || !playedOnce) return false;
                if (State == PlayState.Stopped)
                {
                    long pos = PositionMs;
                    if (durationMs <= 0 || pos >= durationMs - 600) { ended = true; return true; }
                    // 有些设备停止后位置归零，仍视为播放结束
                    if (pos == 0 && playedOnce) { ended = true; return true; }
                }
                return false;
            }
        }

        public void Dispose()
        {
            Unload();
        }
    }

    // ================= 播放引擎（自动选择后端 + 自动回退）=================
    public class AudioEngine : IDisposable
    {
        public event Action<string> TrackOpened;     // 参数：文件路径
        public event Action TrackEnded;              // 自然播放结束
        public event Action<string> TrackFailed;     // 无法播放
        public event Action StateChanged;

        Settings settings;
        WinRtBackend winrt;
        MciBackend mci;
        IAudioBackend active;
        string currentPath = "";
        string usedBackend = "";
        bool endRaised;
        bool fallbackTried;
        string pendingFailMsg = "";
        PlayState lastState = PlayState.Stopped;

        public AudioEngine(Settings s)
        {
            settings = s;
        }

        public string CurrentPath { get { return currentPath; } }
        public string BackendName { get { return usedBackend; } }
        public bool HasTrack { get { return active != null && active.Loaded; } }
        public long DurationMs { get { return active == null ? 0 : active.DurationMs; } }
        public string LastError { get { return active == null ? "" : active.LastError; } }

        public PlayState State
        {
            get { return active == null ? PlayState.Stopped : active.State; }
        }

        public int Volume
        {
            get { return settings.Volume; }
            set
            {
                settings.Volume = Settings.Clamp(value, 0, 100);
                if (active != null) active.Volume = settings.Volume;
            }
        }

        public long PositionMs
        {
            get { return active == null ? 0 : active.PositionMs; }
            set { if (active != null) active.PositionMs = value; }
        }

        void ApplyVolume()
        {
            if (active != null) active.Volume = settings.Volume;
        }

        IAudioBackend MakeWinRt()
        {
            if (winrt == null) winrt = new WinRtBackend();
            return winrt;
        }

        IAudioBackend MakeMci()
        {
            if (mci == null) mci = new MciBackend();
            return mci;
        }

        public bool PlayTrack(string path, long startMs)
        {
            return PlayTrack(path, startMs, true);
        }

        public bool PlayTrack(string path, long startMs, bool autoStart)
        {
            if (string.IsNullOrEmpty(path)) return false;
            currentPath = path;
            endRaised = false;
            fallbackTried = false;
            pendingFailMsg = "";

            string[] order = Order();
            for (int i = 0; i < order.Length; i++)
            {
                IAudioBackend b;
                if (order[i] == "winrt") b = MakeWinRt(); else b = MakeMci();
                if (TryUse(b, path, startMs, autoStart)) return true;
                if (i == 0) fallbackTried = true;
            }
            TrackFailed("无法播放：" + Path.GetFileName(path) + " （所有音频引擎均失败）");
            active = null;
            usedBackend = "";
            return false;
        }

        string[] Order()
        {
            if (settings.Engine == "winrt") return new string[] { "winrt", "mci" };
            if (settings.Engine == "mci") return new string[] { "mci", "winrt" };
            return new string[] { "winrt", "mci" };
        }

        bool TryUse(IAudioBackend b, string path, long startMs, bool autoStart)
        {
            try
            {
                if (active != null && active != b) active.Unload();
                b.Load(path);
                if (!b.Loaded)
                {
                    pendingFailMsg = b.LastError;
                    return false;
                }
                if (b is WinRtBackend)
                {
                    // WinRT 是异步打开：无法立刻知道成功与否，交给 Tick 判定
                    active = b;
                    usedBackend = b.Name;
                    ApplyVolume();
                    if (!autoStart) { }
                    return true;
                }
                active = b;
                usedBackend = b.Name;
                ApplyVolume();
                if (startMs > 0) b.PositionMs = startMs;
                if (autoStart) b.Play(); else b.Pause();
                endRaised = false;
                TrackOpened(path);
                RaiseState();
                return true;
            }
            catch (Exception ex)
            {
                pendingFailMsg = ex.Message;
                return false;
            }
        }

        public void Play()
        {
            if (active == null) return;
            active.Play();
            RaiseState();
        }

        public void Pause()
        {
            if (active == null) return;
            active.Pause();
            RaiseState();
        }

        public void Toggle()
        {
            if (active == null) return;
            if (active.State == PlayState.Playing) Pause(); else Play();
        }

        public void Stop()
        {
            if (active == null) return;
            active.Stop();
            endRaised = true;
            RaiseState();
        }

        public void Unload()
        {
            if (active != null) active.Unload();
            currentPath = "";
            RaiseState();
        }

        // 由界面定时器调用：处理 WinRT 异步失败、时长探测、播放结束
        public void Tick()
        {
            if (active == null) return;

            if (active is WinRtBackend)
            {
                WinRtBackend w = (WinRtBackend)active;
                if (w.Failed)
                {
                    string msg = w.LastError;
                    active.Unload();
                    active = null;
                    if (!fallbackTried && currentPath != "")
                    {
                        fallbackTried = true;
                        // 回退到 MCI
                        IAudioBackend b = MakeMci();
                        if (TryUse(b, currentPath, 0, true)) return;
                    }
                    TrackFailed("无法播放：" + Path.GetFileName(currentPath) + " - " + msg);
                    return;
                }
            }

            PlayState st = active.State;
            if (st != lastState) { lastState = st; RaiseState(); }

            if (!endRaised && active.HasEnded)
            {
                endRaised = true;
                Action h = TrackEnded;
                if (h != null) h();
            }
        }

        void RaiseState()
        {
            Action h = StateChanged;
            if (h != null) h();
        }

        public void Dispose()
        {
            if (winrt != null) winrt.Dispose();
            if (mci != null) mci.Dispose();
            winrt = null; mci = null; active = null;
        }
    }
}