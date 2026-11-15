using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MusicPlayer
{
    // ============ 迷你播放器（收起状态，贴在屏幕右上角） ============
    // 支持两种操作方式：
    //   目标位置 = “正常位置”，即迷你窗所要吸附的位置；当窗口贴到屏幕右/上边缘时
    //   用它算出 “已隐藏位置”，从而实现“鼠标离开 → 缩进屏幕边缘，鼠标移入 → 滑出”
    public interface IMiniHost
    {
        void MiniToggleExpanded();
        void MiniClose();
        string MiniTrackTitle { get; }
        string MiniTrackInfo { get; }
        bool MiniIsPlaying { get; }
        long MiniPositionMs { get; }
        long MiniDurationMs { get; }
        int MiniVolume { get; set; }
        void MiniSeekTo(double ratio);
        void MiniPrev();
        void MiniNext();
        void MiniPlayPause();
        void MiniOpenFull();
        void MiniDragFinished(Point screenLocation);
        void MiniSetAlwaysOnTop(bool onTop);
        bool MiniAlwaysOnTop { get; }
    }

    public class MiniPlayerPanel : Panel
    {
        IMiniHost host;
        Theme T;
        int Sf = 96;

        // 子控件
        PictureBox cover;
        Label lblTitle, lblInfo, lblTime, lblDragHint;
        MiniSeekBar seek;
        MiniButton btnPrev, btnPlay, btnNext, btnVolume, btnFull, btnTop, btnClose;
        Image coverImage;

        bool expanded = false;      // 是否展开（鼠标悬停展开）
        Point targetLocation = Point.Empty;
        Point hiddenLocation = Point.Empty;
        Rectangle collapseTo = Rectangle.Empty;
        System.Windows.Forms.Timer slideTimer;
        int slideStep;

        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

        public MiniPlayerPanel(IMiniHost h, Theme theme)
        {
            host = h;
            T = theme;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(24, 25, 29);   // 深色半透明底，直接绘制
            BuildChildren();
        }

        void BuildChildren()
        {
            cover = new PictureBox();
            cover.SizeMode = PictureBoxSizeMode.Zoom;
            cover.BackColor = Color.FromArgb(44, 46, 52);
            cover.Cursor = Cursors.Hand;
            cover.Click += delegate(object s, EventArgs e) { host.MiniToggleExpanded(); };

            lblTitle = MakeLabel();
            lblTitle.Font = Theme.MakeFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(238, 240, 244);

            lblInfo = MakeLabel();
            lblInfo.Font = Theme.MakeFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            lblInfo.ForeColor = Color.FromArgb(148, 154, 166);

            lblTime = MakeLabel();
            lblTime.Font = Theme.MakeFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            lblTime.ForeColor = Color.FromArgb(148, 154, 166);

            lblDragHint = MakeLabel();
            lblDragHint.Font = Theme.MakeFont("Microsoft YaHei UI", 8f, FontStyle.Regular);
            lblDragHint.ForeColor = Color.FromArgb(120, 126, 138);
            lblDragHint.Text = "拖动移动 · 双击展开";
            lblDragHint.TextAlign = ContentAlignment.MiddleLeft;
            lblDragHint.Cursor = Cursors.SizeAll;

            seek = new MiniSeekBar();
            seek.Height = S(20);
            seek.Cursor = Cursors.Hand;
            seek.Seek += delegate(double r) { host.MiniSeekTo(r); };

            btnPrev = new MiniButton(Glyphs.Prev, T, "上一首");
            btnPrev.Click += delegate(object s, EventArgs e) { host.MiniPrev(); };

            btnPlay = new MiniButton(Glyphs.Play, T, "播放/暂停");
            btnPlay.IsAccent = true;
            btnPlay.Click += delegate(object s, EventArgs e) { host.MiniPlayPause(); };

            btnNext = new MiniButton(Glyphs.Next, T, "下一首");
            btnNext.Click += delegate(object s, EventArgs e) { host.MiniNext(); };

            btnVolume = new MiniButton(Glyphs.Volume, T, "音量");
            btnVolume.Click += delegate(object s, EventArgs e) { ShowVolumeMenu(); };

            btnTop = new MiniButton(Glyphs.Up, T, "窗口置顶");
            btnTop.Click += delegate(object s, EventArgs e) { host.MiniSetAlwaysOnTop(!host.MiniAlwaysOnTop); };

            btnFull = new MiniButton(Glyphs.Open, T, "展开完整播放器");
            btnFull.Click += delegate(object s, EventArgs e) { host.MiniOpenFull(); };

            btnClose = new MiniButton(Glyphs.Clear, T, "隐藏到托盘");
            btnClose.Click += delegate(object s, EventArgs e) { host.MiniClose(); };

            Controls.Add(cover);
            Controls.Add(lblTitle);
            Controls.Add(lblInfo);
            Controls.Add(lblTime);
            Controls.Add(lblDragHint);
            Controls.Add(seek);
            Controls.Add(btnPrev);
            Controls.Add(btnPlay);
            Controls.Add(btnNext);
            Controls.Add(btnVolume);
            Controls.Add(btnTop);
            Controls.Add(btnFull);
            Controls.Add(btnClose);

            slideTimer = new System.Windows.Forms.Timer();
            slideTimer.Interval = 16;
            slideTimer.Tick += delegate(object s, EventArgs e) { SlideStep(); };
        }

        Label MakeLabel()
        {
            Label l = new Label();
            l.AutoSize = false;
            l.AutoEllipsis = true;
            l.BackColor = Color.Transparent;
            l.ForeColor = Color.White;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        // ---------------- 布局 ----------------
        public void SetDpi(int dpi)
        {
            if (dpi > 0) Sf = dpi;
            LayoutChildren();
        }

        public void ApplyTargetLocation(Point target)
        {
            targetLocation = target;
            int w = Width;
            Rectangle wa = Screen.FromPoint(target).WorkingArea;
            int edge = target.X + w;
            int top = target.Y;
            bool nearRight = wa.Right - edge < S(8);
            bool nearTop = top - wa.Top < S(8);
            int gap = S(8);
            // 不再隐藏到屏幕边缘（窗口本身很小），展开/收起只控制鼠标悬停时的轻微提亮
            hiddenLocation = target;
            if (!expanded)
            {
                Location = target;
                if (slideTimer.Enabled) slideTimer.Stop();
            }
        }

        public void SetExpanded(bool want)
        {
            if (expanded == want) return;
            expanded = want;
            StartSlide(want);
            if (want) BringToFront();
        }

        void StartSlide(bool show)
        {
            slideStep = show ? 1 : -1;
            if (!slideTimer.Enabled) slideTimer.Start();
        }

        void SlideStep()
        {
            int speed = S(26);
            Point now = Location;
            Point goal = expanded ? targetLocation : hiddenLocation;
            int dx = goal.X - now.X;
            int dy = goal.Y - now.Y;
            if (Math.Abs(dx) <= speed && Math.Abs(dy) <= speed)
            {
                Location = goal;
                slideTimer.Stop();
                return;
            }
            int nx = now.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), speed);
            int ny = now.Y + Math.Sign(dy) * Math.Min(Math.Abs(dy), speed);
            Location = new Point(nx, ny);
        }

        void LayoutChildren()
        {
            int pad = S(10);
            int w = Width, h = Height;
            int cs = S(48);
            cover.Location = new Point(pad, (h - cs) / 2);
            cover.Size = new Size(cs, cs);

            int bx = w - pad;
            btnClose.Size = new Size(S(22), S(22));
            bx -= btnClose.Width;
            btnClose.Location = new Point(bx, S(6));
            btnTop.Size = new Size(S(22), S(22));
            bx -= btnTop.Width + S(4);
            btnTop.Location = new Point(bx, S(6));

            int cx = pad + cs + S(10);
            int rightLimit = btnClose.Left - S(6);
            lblTitle.Location = new Point(cx, S(8));
            lblTitle.Size = new Size(Math.Max(S(60), rightLimit - cx), S(24));
            lblInfo.Location = new Point(cx, S(32));
            lblInfo.Size = new Size(Math.Max(S(60), rightLimit - cx), S(20));
            lblTime.Location = new Point(cx, S(52));
            lblTime.Size = new Size(S(120), S(18));
            lblDragHint.Location = new Point(cx + S(120), S(52));
            lblDragHint.Size = new Size(Math.Max(S(20), rightLimit - cx - S(120)), S(18));

            int sy = S(74);
            seek.Location = new Point(pad, sy);
            seek.Size = new Size(w - pad * 2, S(20));

            // 底部按钮行：居中
            int bs = S(28);
            int total = bs * 6 + S(6) * 5;
            int startX = Math.Max(pad, (w - total) / 2);
            int by = S(98);
            btnPrev.Size = new Size(bs, bs);
            btnPlay.Size = new Size(S(36), S(36));
            btnNext.Size = new Size(bs, bs);
            btnVolume.Size = new Size(bs, bs);
            btnFull.Size = new Size(bs, bs);

            int x = startX;
            btnPrev.Location = new Point(x, by + (S(36) - bs) / 2); x += bs + S(6);
            btnPlay.Location = new Point(x, by); x += S(36) + S(6);
            btnNext.Location = new Point(x, by + (S(36) - bs) / 2); x += bs + S(6);
            btnVolume.Location = new Point(x, by + (S(36) - bs) / 2); x += bs + S(6);
            btnFull.Location = new Point(x, by + (S(36) - bs) / 2);

            Invalidate();
        }

        public void RefreshFromHost()
        {
            lblTitle.Text = host.MiniTrackTitle;
            lblInfo.Text = host.MiniTrackInfo;
            btnPlay.Text = host.MiniIsPlaying ? Glyphs.Pause : Glyphs.Play;
            btnPlay.IsAccent = host.MiniIsPlaying;
            btnTop.IsAccent = host.MiniAlwaysOnTop;

            long dur = host.MiniDurationMs;
            long pos = host.MiniPositionMs;
            seek.DurationMs = dur;
            seek.PositionMs = pos;
            lblTime.Text = Library.FormatDuration(pos / 1000.0) + " / " + Library.FormatDuration(dur / 1000.0);
            Invalidate();
        }

        public void SetCover(Image img)
        {
            if (coverImage != null) { coverImage.Dispose(); coverImage = null; }
            coverImage = img;
            cover.Image = img;
            cover.Invalidate();
        }

        // ---------------- 自绘 ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            // 圆角深色底
            using (GraphicsPath p = Rounded(new Rectangle(0, 0, Width, Height), S(10)))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(255, 33, 35, 41), Color.FromArgb(255, 21, 22, 26), 90f))
                {
                    g.FillPath(b, p);
                }
                using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
                    g.DrawPath(pen, p);
            }
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 1) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void ShowVolumeMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.BackColor = Color.FromArgb(40, 42, 48);
            m.ForeColor = Color.FromArgb(232, 234, 237);
            m.Font = Theme.MakeFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
            m.Items.Add("音量：" + host.MiniVolume + "%");
            m.Items.Add(new ToolStripSeparator());
            for (int v = 100; v >= 0; v -= 20)
            {
                int val = v;
                ToolStripMenuItem it = new ToolStripMenuItem((val == 0 ? "静音" : (val + "%")), null,
                    delegate(object s, EventArgs e) { host.MiniVolume = val; RefreshFromHost(); });
                m.Items.Add(it);
            }
            m.Show(btnVolume, new Point(0, btnVolume.Height + 2));
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            host.MiniOpenFull();
            base.OnDoubleClick(e);
        }

        // 双击子控件（标题、封面等）同样还原
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null && !(e.Control is MiniSeekBar))
            {
                e.Control.DoubleClick += delegate(object s, EventArgs a) { host.MiniOpenFull(); };
            }
        }
    }

    // ---------------- 迷你窗的进度条（可拖动） ----------------
    public class MiniSeekBar : Control
    {
        public long DurationMs, PositionMs;
        public event Action<double> Seek;
        bool drag;
        int Sf = 96;
        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

        public MiniSeekBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        double Ratio { get { return DurationMs > 0 ? Math.Max(0, Math.Min(1.0, (double)PositionMs / DurationMs)) : 0; } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                drag = true;
                Apply(e.X);
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag) Apply(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (drag)
            {
                drag = false;
                Capture = false;
            }
            base.OnMouseUp(e);
        }

        void Apply(int x)
        {
            if (DurationMs <= 0) return;
            int left = S(2), right = Width - S(2);
            double r = (double)(x - left) / Math.Max(1, right - left);
            r = Math.Max(0, Math.Min(1, r));
            PositionMs = (long)(DurationMs * r);
            Invalidate();
            if (Seek != null) Seek(r);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            int h = S(4);
            int left = S(2), right = Width - S(2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 62, 65, 71)))
                Round(g, b, new Rectangle(left, cy - h / 2, right - left, h), h / 2);
            int fx = left + (int)((right - left) * Ratio);
            if (fx > left)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 79, 140, 255)))
                    Round(g, b, new Rectangle(left, cy - h / 2, fx - left, h), h / 2);
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, fx - S(4), cy - S(4), S(8), S(8));
        }

        static void Round(Graphics g, Brush b, Rectangle r, int radius)
        {
            if (r.Width <= 0) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                int d = radius * 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                g.FillPath(b, p);
            }
        }
    }

    // ---------------- 迷你窗的图标按钮 ----------------
    public class MiniButton : Control
    {
        string glyph;
        Theme T;
        bool hover, down;
        public bool IsAccent = false;
        string tip;

        public MiniButton(string g, Theme theme, string tooltip)
        {
            glyph = g;
            T = theme;
            tip = tooltip;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            if (!string.IsNullOrEmpty(tip))
            {
                ToolTip t = new ToolTip();
                t.SetToolTip(this, tip);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = Color.FromArgb(215, 220, 228);
            if (IsAccent) c = Color.FromArgb(79, 140, 255);
            if (hover && !down) c = Color.FromArgb(255, 255, 255);
            if (down) c = Color.FromArgb(150, 160, 175);
            if (Width >= 32)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(down ? 70 : (hover ? 50 : 30), 255, 255, 255)))
                    g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
            }
            using (Font f = Theme.MakeFont("Segoe MDL2 Assets", Width >= 32 ? 12f : 9.5f, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, f, b, new RectangleF(0, 0, Width, Height), sf);
            }
        }
    }
}
