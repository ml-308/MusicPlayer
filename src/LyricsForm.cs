using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MusicPlayer
{
    // ============ 桌面歌词浮窗（参考 Ever Play 的卡拉 OK 逐字高亮效果） ============
    public class LyricsForm : Form
    {
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;

        Settings settings;
        Theme T;
        Func<long> position;
        Func<bool> playing;
        Track track;
        Lyrics lyrics = Lyrics.Empty;
        System.Windows.Forms.Timer timer;

        int idx = -1;
        float appear = 1f;
        long posBase;
        long posStamp;
        long lastDrawn = -1;
        bool locked;
        bool dragging;
        Point dragStart;
        Point winStart;
        int Sf = 96;

        Font fontMain, fontTrans, fontNext;
        Bitmap bgCache;

        public event Action LockChanged;
        public event Action Hidden;
        public event Action PositionChanged;

        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

        public LyricsForm(Settings s, Func<long> posProvider, Func<bool> playingProvider)
        {
            settings = s;
            T = Theme.Create(s.Theme);
            position = posProvider;
            playing = playingProvider;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(18, 19, 22);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            BuildFonts();
            ApplyOpacity();
            locked = settings.LyricsLocked;
            BuildContextMenu();
            ApplyGeometry();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 33;
            timer.Tick += delegate(object o, EventArgs e) { OnFrame(); };
            timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                if (locked) cp.ExStyle |= WS_EX_TRANSPARENT;
                return cp;
            }
        }

        void BuildFonts()
        {
            int size = settings.LyricsFontSize;
            if (fontMain != null) fontMain.Dispose();
            if (fontTrans != null) fontTrans.Dispose();
            if (fontNext != null) fontNext.Dispose();
            fontMain = Theme.MakeFont("Microsoft YaHei UI", size, FontStyle.Bold);
            fontTrans = Theme.MakeFont("Microsoft YaHei UI", Math.Max(10f, size * 0.5f), FontStyle.Regular);
            fontNext = Theme.MakeFont("Microsoft YaHei UI", Math.Max(9f, size * 0.45f), FontStyle.Regular);
            RebuildBg();
        }

        void ApplyOpacity()
        {
            int v = Settings.Clamp(settings.LyricsOpacity, 25, 100);
            Opacity = v / 100.0;
        }

        public void ApplyGeometry()
        {
            try { Sf = DeviceDpi; } catch { Sf = 96; }
            if (Sf <= 0) Sf = 96;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(S(1080), wa.Width - S(120));
            if (w < S(460)) w = Math.Min(S(460), wa.Width - S(40));
            int h = S(26) + fontMain.Height + S(6) + fontTrans.Height + S(6) + fontNext.Height + S(26);
            Size = new Size(w, h);

            Point p;
            if (settings.LyricsX >= 0 && settings.LyricsY >= 0)
                p = new Point(settings.LyricsX, settings.LyricsY);
            else
                p = new Point(wa.Left + (wa.Width - w) / 2, wa.Bottom - h - S(90));

            // 保证在屏幕内
            if (p.X < wa.Left - w / 2) p.X = wa.Left + S(20);
            if (p.Y < wa.Top - h / 2) p.Y = wa.Top + S(20);
            if (p.X > wa.Right - w / 2) p.X = wa.Right - w - S(20);
            if (p.Y > wa.Bottom - S(20)) p.Y = wa.Bottom - h - S(20);
            Location = p;
            RebuildBg();
        }

        public void ApplySettings()
        {
            BuildFonts();
            ApplyOpacity();
            ApplyGeometry();
            if (locked != settings.LyricsLocked) SetLocked(settings.LyricsLocked);
            Invalidate();
        }

        void RebuildBg()
        {
            if (Width <= 0 || Height <= 0) return;
            if (bgCache != null) bgCache.Dispose();
            bgCache = new Bitmap(Width, Height);
            using (Graphics g = Graphics.FromImage(bgCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(255, 34, 36, 43), Color.FromArgb(255, 15, 16, 20), 90f))
                {
                    g.FillRectangle(b, ClientRectangle);
                }
                using (Pen p = new Pen(Color.FromArgb(46, 255, 255, 255), 1))
                    g.DrawLine(p, S(22), 1, Width - S(22), 1);
            }
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), S(16)))
            {
                Region = new Region(path);
            }
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 1) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void BuildContextMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.BackColor = Color.FromArgb(40, 42, 48);
            m.ForeColor = Color.FromArgb(232, 234, 237);
            m.Font = T.BaseFont;
            m.Items.Add("锁定歌词（鼠标穿透）", null, delegate(object o, EventArgs e) { SetLocked(true); });
            m.Items.Add("解锁歌词", null, delegate(object o, EventArgs e) { SetLocked(false); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("字号增大", null, delegate(object o, EventArgs e) { ChangeFontSize(2); });
            m.Items.Add("字号减小", null, delegate(object o, EventArgs e) { ChangeFontSize(-2); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("隐藏歌词", null, delegate(object o, EventArgs e) { HideOverlay(); });
            m.Items.Add("打开设置", null, delegate(object o, EventArgs e) { if (Hidden != null) Hidden(); });
            ContextMenuStrip = m;
        }

        void ChangeFontSize(int delta)
        {
            settings.LyricsFontSize = Settings.Clamp(settings.LyricsFontSize + delta, 14, 64);
            ApplySettings();
            if (PositionChanged != null) PositionChanged();
        }

        public void SetLocked(bool v)
        {
            locked = v;
            settings.LyricsLocked = v;
            if (IsHandleCreated)
            {
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                if (v) ex |= WS_EX_TRANSPARENT;
                else ex = ex & ~WS_EX_TRANSPARENT;
                SetWindowLong(Handle, GWL_EXSTYLE, ex);
            }
            if (LockChanged != null) LockChanged();
        }

        public bool Locked { get { return locked; } }

        public void HideOverlay()
        {
            Hide();
            if (Hidden != null) Hidden();
        }

        public void SetTrack(Track t)
        {
            track = t;
            lyrics = (t == null) ? Lyrics.Empty : LyricLoader.ForTrack(t);
            idx = -1;
            appear = 1f;
            posStamp = 0;
            lastDrawn = -1;
            Invalidate();
        }

        void OnFrame()
        {
            if (!Visible) return;
            long now = Environment.TickCount;
            if (posStamp == 0 || now - posStamp > 120)
            {
                posBase = position == null ? 0 : position();
                posStamp = now;
            }
            bool isPlaying = playing == null ? false : playing();
            long pos = posBase + (isPlaying ? (now - posStamp) : 0);
            if (pos < 0) pos = 0;

            bool animating = appear < 1f;
            int newIdx = (lyrics.Lines.Count > 0) ? lyrics.IndexAt(pos) : -1;
            if (newIdx != idx)
            {
                idx = newIdx;
                appear = 0f;
                animating = true;
            }
            if (animating) appear = Math.Min(1f, appear + 0.11f);

            if (animating || Math.Abs(pos - lastDrawn) > 3)
            {
                lastDrawn = pos;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try { g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; } catch { }

            if (bgCache != null) g.DrawImageUnscaled(bgCache, 0, 0);
            else { using (SolidBrush b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle); }

            int pad = S(22);
            bool hasTrans = false;
            LyricLine cur = null;
            LyricLine trans = null;
            LyricLine next = null;

            if (lyrics.Lines.Count > 0 && idx >= 0 && idx < lyrics.Lines.Count)
            {
                cur = lyrics.Lines[idx];
                hasTrans = cur.Trans.Length > 0;
                if (hasTrans) trans = cur;
                if (idx + 1 < lyrics.Lines.Count) next = lyrics.Lines[idx + 1];
            }

            if (cur == null)
            {
                DrawNoLyrics(g, pad);
                return;
            }

            int contentH = fontMain.Height + S(6) + (hasTrans ? fontTrans.Height + S(6) : 0) + S(4) + fontNext.Height;
            int y = (Height - contentH) / 2;
            int slide = (int)Math.Round((1f - EaseOut(appear)) * S(10));

            // 当前句：卡拉 OK 逐字高亮
            Rectangle rMain = new Rectangle(pad, y, Width - pad * 2, fontMain.Height);
            double prog = LineProgress(idx);
            Color dim = Color.FromArgb(255, 138, 144, 158);
            Color bright = Color.FromArgb(255, 255, 255, 255);
            DrawKaraoke(g, cur.Text, fontMain, rMain, prog, dim, bright, slide);
            y += fontMain.Height + S(6);

            if (hasTrans)
            {
                Rectangle rT = new Rectangle(pad, y, Width - pad * 2, fontTrans.Height);
                TextRenderer.DrawText(g, trans.Trans, fontTrans, rT,
                    Color.FromArgb(255, 176, 184, 196),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += fontTrans.Height + S(6);
            }
            y += S(4);

            if (next != null && next.Text.Trim().Length > 0)
            {
                Rectangle rN = new Rectangle(pad, y, Width - pad * 2, fontNext.Height);
                TextRenderer.DrawText(g, next.Text, fontNext, rN,
                    Color.FromArgb(255, 112, 118, 132),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        static float EaseOut(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        double LineProgress(int i)
        {
            if (i < 0 || i >= lyrics.Lines.Count) return 0;
            if (!lyrics.Synced) return 0;
            long t0 = lyrics.Lines[i].Time;
            long t1;
            if (i + 1 < lyrics.Lines.Count) t1 = lyrics.Lines[i + 1].Time;
            else t1 = t0 + 5000;
            if (t1 <= t0) t1 = t0 + 3000;
            long now = posBase + ((playing != null && playing()) ? (Environment.TickCount - posStamp) : 0);
            double p = (now - t0) / (double)(t1 - t0);
            if (p < 0) p = 0;
            if (p > 1) p = 1;
            return p;
        }

        void DrawKaraoke(Graphics g, string text, Font f, Rectangle r, double progress, Color dim, Color bright, int dy)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 注意：TextRenderer 走 GDI HDC，不受 Graphics.SetClip 影响，因此这里用 GDI+ 的 DrawString 才能做逐字裁剪
            RectangleF rf = new RectangleF(r.X, r.Y + dy, r.Width, r.Height);
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(dim))
                    g.DrawString(text, f, b, rf, sf);

                if (progress > 0.001)
                {
                    SizeF sz = g.MeasureString(text, f, new SizeF(r.Width, r.Height), sf);
                    float tw = Math.Min(sz.Width, r.Width);
                    float x = r.X + (r.Width - tw) / 2f;
                    float cw = (float)(tw * Math.Min(1.0, progress));
                    if (cw > 0.5f)
                    {
                        Region old = g.Clip;
                        g.SetClip(new RectangleF(x, rf.Y - S(4), cw, rf.Height + S(8)));
                        using (SolidBrush b = new SolidBrush(bright))
                            g.DrawString(text, f, b, rf, sf);
                        g.Clip = old;
                        if (old != null) old.Dispose();
                    }
                }
            }
        }

        void DrawNoLyrics(Graphics g, int pad)
        {
            string title = track != null ? track.DisplayTitle : "未在播放";
            string sub = (track != null) ? "暂无歌词 · 把同名 .lrc 文件放在歌曲旁边即可显示" : "双击主界面歌曲开始播放";
            int y = Height / 2 - fontNext.Height;
            Rectangle r1 = new Rectangle(pad, y, Width - pad * 2, fontTrans.Height + S(4));
            TextRenderer.DrawText(g, title, fontTrans, r1, Color.FromArgb(255, 205, 210, 220),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            Rectangle r2 = new Rectangle(pad, y + fontTrans.Height + S(6), Width - pad * 2, fontNext.Height);
            TextRenderer.DrawText(g, sub, fontNext, r2, Color.FromArgb(255, 118, 124, 136),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        // ---------- 拖动 / 双击锁定 ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !locked)
            {
                dragging = true;
                dragStart = Cursor.Position;
                winStart = Location;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Point p = Cursor.Position;
                Location = new Point(winStart.X + (p.X - dragStart.X), winStart.Y + (p.Y - dragStart.Y));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                Capture = false;
                settings.LyricsX = Left;
                settings.LyricsY = Top;
                if (PositionChanged != null) PositionChanged();
            }
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            SetLocked(!locked);
            base.OnDoubleClick(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RebuildBg();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) { timer.Stop(); timer.Dispose(); }
                if (fontMain != null) fontMain.Dispose();
                if (fontTrans != null) fontTrans.Dispose();
                if (fontNext != null) fontNext.Dispose();
                if (bgCache != null) bgCache.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}