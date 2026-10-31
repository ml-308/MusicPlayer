using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MusicPlayer
{
    public static class Glyphs
    {
        public const string Play = "\uE768";
        public const string Pause = "\uE769";
        public const string Stop = "\uE71A";
        public const string Prev = "\uE892";
        public const string Next = "\uE893";
        public const string Volume = "\uE767";
        public const string Mute = "\uE74F";
        public const string Shuffle = "\uE8B1";
        public const string RepeatAll = "\uE8EE";
        public const string RepeatOne = "\uE8ED";
        public const string Sequential = "\uE8FD";
        public const string Folder = "\uE838";
        public const string Music = "\uE8D6";
        public const string Add = "\uE710";
        public const string Delete = "\uE74D";
        public const string Settings = "\uE713";
        public const string Search = "\uE721";
        public const string Save = "\uE74E";
        public const string Open = "\uE8E5";
        public const string Speaker = "\uE7F5";
        public const string Clock = "\uE823";
        public const string Info = "\uE946";
        public const string Clear = "\uE894";
        public const string Up = "\uE74A";
        public const string Down = "\uE74B";
    }

    public class Theme
    {
        public string Name = "dark";
        public Color Window, Panel, PanelAlt, Text, SubText, Accent, AccentText, Border,
                     ListBack, ListAlt, SelectionBack, SelectionText, HeaderBack, HeaderText,
                     Groove, ButtonBack, ButtonHover, ButtonDown, TrackPlaying, ScrollBar;
        public Font BaseFont, BoldFont, SmallFont, BigFont, IconFont;
        public bool IsDark = true;

        public static Theme Create(string name)
        {
            Theme t = new Theme();
            t.Name = name;
            t.IsDark = !(name == "light");
            if (t.IsDark)
            {
                t.Window = Color.FromArgb(30, 31, 34);
                t.Panel = Color.FromArgb(43, 45, 49);
                t.PanelAlt = Color.FromArgb(49, 51, 56);
                t.Text = Color.FromArgb(232, 234, 237);
                t.SubText = Color.FromArgb(154, 160, 166);
                t.Accent = Color.FromArgb(79, 140, 255);
                t.AccentText = Color.FromArgb(255, 255, 255);
                t.Border = Color.FromArgb(58, 60, 66);
                t.ListBack = Color.FromArgb(35, 36, 40);
                t.ListAlt = Color.FromArgb(38, 40, 44);
                t.SelectionBack = Color.FromArgb(45, 74, 120);
                t.SelectionText = Color.FromArgb(255, 255, 255);
                t.HeaderBack = Color.FromArgb(43, 45, 49);
                t.HeaderText = Color.FromArgb(185, 187, 190);
                t.Groove = Color.FromArgb(62, 65, 71);
                t.ButtonBack = Color.FromArgb(58, 60, 66);
                t.ButtonHover = Color.FromArgb(72, 76, 84);
                t.ButtonDown = Color.FromArgb(46, 48, 53);
                t.TrackPlaying = Color.FromArgb(87, 214, 140);
                t.ScrollBar = Color.FromArgb(70, 73, 80);
            }
            else
            {
                t.Window = Color.FromArgb(245, 246, 248);
                t.Panel = Color.FromArgb(255, 255, 255);
                t.PanelAlt = Color.FromArgb(239, 241, 244);
                t.Text = Color.FromArgb(31, 35, 40);
                t.SubText = Color.FromArgb(107, 114, 128);
                t.Accent = Color.FromArgb(47, 111, 237);
                t.AccentText = Color.FromArgb(255, 255, 255);
                t.Border = Color.FromArgb(216, 219, 224);
                t.ListBack = Color.FromArgb(255, 255, 255);
                t.ListAlt = Color.FromArgb(247, 248, 250);
                t.SelectionBack = Color.FromArgb(216, 228, 255);
                t.SelectionText = Color.FromArgb(20, 33, 61);
                t.HeaderBack = Color.FromArgb(239, 241, 244);
                t.HeaderText = Color.FromArgb(90, 98, 110);
                t.Groove = Color.FromArgb(214, 218, 224);
                t.ButtonBack = Color.FromArgb(231, 234, 238);
                t.ButtonHover = Color.FromArgb(219, 223, 229);
                t.ButtonDown = Color.FromArgb(205, 210, 217);
                t.TrackPlaying = Color.FromArgb(21, 128, 61);
                t.ScrollBar = Color.FromArgb(190, 195, 202);
            }
            t.BaseFont = MakeFont("Microsoft YaHei UI", 9.75f, FontStyle.Regular);
            t.BoldFont = MakeFont("Microsoft YaHei UI", 9.75f, FontStyle.Bold);
            t.SmallFont = MakeFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            t.BigFont = MakeFont("Microsoft YaHei UI", 12f, FontStyle.Bold);
            t.IconFont = MakeFont("Segoe MDL2 Assets", 11f, FontStyle.Regular);
            return t;
        }

        public static Font MakeFont(string family, float size, FontStyle style)
        {
            try
            {
                Font f = new Font(family, size, style);
                if (f.FontFamily.Name != family)
                {
                    f.Dispose();
                    return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
                }
                return f;
            }
            catch
            {
                return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
            }
        }

        public Font IconFontSized(float size)
        {
            return MakeFont("Segoe MDL2 Assets", size, FontStyle.Regular);
        }

        public string ModeGlyph(string mode)
        {
            if (mode == "shuffle") return Glyphs.Shuffle;
            if (mode == "repeatOne") return Glyphs.RepeatOne;
            if (mode == "repeatAll") return Glyphs.RepeatAll;
            return Glyphs.Sequential;
        }
    }

    public class FlatButton : Button
    {
        public Theme T;
        public bool IsAccent = false;
        public bool IsRound = true;
        bool hover, down;

        public FlatButton(Theme t)
        {
            T = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Font = t.BaseFont;
            ForeColor = t.Text;
            BackColor = t.ButtonBack;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color back;
            if (!Enabled) back = T.PanelAlt;
            else if (IsAccent) back = down ? Darken(T.Accent) : (hover ? Lighten(T.Accent) : T.Accent);
            else back = down ? T.ButtonDown : (hover ? T.ButtonHover : T.ButtonBack);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color parentBack = (Parent != null) ? Parent.BackColor : T.Panel;
            using (SolidBrush b = new SolidBrush(parentBack)) g.FillRectangle(b, ClientRectangle);
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);
            if (!IsAccent && !IsFlatSquare)
            {
                using (SolidBrush b = new SolidBrush(back))
                {
                    FillRounded(g, b, r, Math.Min(6, Height / 2));
                }
            }
            Color fc = Enabled ? (IsAccent ? T.AccentText : T.Text) : T.SubText;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        public bool IsFlatSquare = false;

        static Color Lighten(Color c) { return Color.FromArgb(Math.Min(255, c.R + 18), Math.Min(255, c.G + 18), Math.Min(255, c.B + 18)); }
        static Color Darken(Color c) { return Color.FromArgb(Math.Max(0, c.R - 18), Math.Max(0, c.G - 18), Math.Max(0, c.B - 18)); }

        public static void FillRounded(Graphics g, Brush b, Rectangle r, int radius)
        {
            if (radius <= 1) { g.FillRectangle(b, r); return; }
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

    // ============ 可点击进度条 / 音量条 ============
    public class SliderBar : Control
    {
        public Theme T;
        public int Min = 0, Max = 100;
        int val = 0;
        public bool ShowKnob = true;
        public bool Dragging = false;
        public event EventHandler UserChanged;
        public event EventHandler DragStarted;
        public event EventHandler DragFinished;

        public SliderBar(Theme t)
        {
            T = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 22;
        }

        public int Value
        {
            get { return val; }
            set
            {
                int v = value;
                if (v < Min) v = Min;
                if (v > Max) v = Max;
                if (v == val) return;
                val = v;
                Invalidate();
            }
        }

        int ValueFromX(int x)
        {
            int w = Width - 12;
            if (w <= 0) return Min;
            int p = x - 6;
            if (p < 0) p = 0;
            if (p > w) p = w;
            double ratio = (double)p / w;
            return Min + (int)Math.Round(ratio * (Max - Min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Dragging = true;
                Value = ValueFromX(e.X);
                if (DragStarted != null) DragStarted(this, EventArgs.Empty);
                if (UserChanged != null) UserChanged(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Dragging)
            {
                Value = ValueFromX(e.X);
                if (UserChanged != null) UserChanged(this, EventArgs.Empty);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (Dragging)
            {
                Dragging = false;
                if (DragFinished != null) DragFinished(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            int knobW = ShowKnob ? 12 : 0;
            int left = 6, right = Width - 6;
            int grooveH = 4;
            double ratio = (Max > Min) ? (double)(val - Min) / (Max - Min) : 0;
            int fx = left + (int)Math.Round((right - left) * ratio);

            using (SolidBrush b = new SolidBrush(T.Groove))
                FillRounded(g, b, new Rectangle(left, cy - grooveH / 2, right - left, grooveH), grooveH / 2);
            if (fx > left)
            {
                using (SolidBrush b = new SolidBrush(T.Accent))
                    FillRounded(g, b, new Rectangle(left, cy - grooveH / 2, fx - left, grooveH), grooveH / 2);
            }
            if (ShowKnob && (Enabled))
            {
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.FillEllipse(b, fx - 6, cy - 6, 12, 12);
                using (Pen p = new Pen(T.Border, 1))
                    g.DrawEllipse(p, fx - 6, cy - 6, 12, 12);
            }
        }

        static void FillRounded(Graphics g, Brush b, Rectangle r, int radius)
        {
            if (r.Width <= 0) return;
            if (radius <= 1) { g.FillRectangle(b, r); return; }
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

    // ============ 自绘歌曲列表（虚拟模式，可承载数万首） ============
    public class TrackListView : ListView
    {
        public Theme T;
        public Func<int, Track> Provider;
        public int PlayingIndex = -1;
        public List<int> FilteredMap;     // 可选：视图索引 -> 原始索引

        public TrackListView(Theme t)
        {
            T = t;
            OwnerDraw = true;
            View = View.Details;
            FullRowSelect = true;
            VirtualMode = true;
            HideSelection = false;
            MultiSelect = true;
            BorderStyle = BorderStyle.None;
            HeaderStyle = ColumnHeaderStyle.Clickable;
            BackColor = t.ListBack;
            ForeColor = t.Text;
            Font = t.BaseFont;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SmallImageList = new ImageList();
            SmallImageList.ImageSize = new Size(1, 26);
        }

        Track TrackAt(int index)
        {
            if (Provider == null) return null;
            return Provider(index);
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(T.HeaderBack))
                e.Graphics.FillRectangle(b, e.Bounds);
            using (SolidBrush b = new SolidBrush(T.HeaderText))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                bool numeric = (e.ColumnIndex == 4 || e.ColumnIndex == 6 || e.ColumnIndex == 7);
                sf.Alignment = numeric ? StringAlignment.Far : StringAlignment.Near;
                Rectangle r = e.Bounds;
                r.X += 8; r.Width -= 12;
                e.Graphics.DrawString(ColumnsSafe(e.ColumnIndex), T.BoldFont, b, r, sf);
            }
            using (Pen p = new Pen(T.Border))
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
        }

        string ColumnsSafe(int idx)
        {
            if (idx >= 0 && idx < Library.Columns.Length) return Library.Columns[idx];
            return "";
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            Track t = TrackAt(e.ItemIndex);
            bool sel = e.Item.Selected;
            Color back = (e.ItemIndex % 2 == 0) ? T.ListBack : T.ListAlt;
            if (sel) back = T.SelectionBack;
            using (SolidBrush b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (e.ItemIndex == PlayingIndex && !sel)
            {
                using (SolidBrush b = new SolidBrush(T.TrackPlaying))
                using (Font f = T.IconFontSized(9f))
                    e.Graphics.DrawString(Glyphs.Speaker, f, b, 4, e.Bounds.Top + 5);
            }
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            Track t = TrackAt(e.ItemIndex);
            if (t == null) return;
            bool sel = e.Item.Selected;
            Color fore = sel ? T.SelectionText : T.Text;
            if (e.ColumnIndex == 1 && t.Artist.Length == 0) fore = T.SubText;
            if (e.ColumnIndex == 2 || e.ColumnIndex == 3) fore = sel ? T.SelectionText : T.SubText;
            if (e.ItemIndex == PlayingIndex && !sel) fore = T.TrackPlaying;
            string text = CellText(t, e.ColumnIndex, e.ItemIndex);
            if (text == null) return;
            Rectangle r = e.Bounds;
            bool numeric = (e.ColumnIndex == 4 || e.ColumnIndex == 6 || e.ColumnIndex == 7);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            flags |= numeric ? TextFormatFlags.Right : TextFormatFlags.Left;
            if (e.ColumnIndex == 0) { r.X += 16; }
            else r.X += 6;
            r.Width -= 10;
            TextRenderer.DrawText(e.Graphics, text, T.BaseFont, r, fore, flags);
        }

        string CellText(Track t, int col, int viewIndex)
        {
            switch (col)
            {
                case 0: return (viewIndex + 1).ToString();
                case 1: return t.DisplayTitle;
                case 2: return t.DisplayArtist;
                case 3: return t.DisplayAlbum;
                case 4: return Library.FormatDuration(t.Duration);
                case 5: return t.Ext.TrimStart('.').ToUpperInvariant();
                case 6: return Library.FormatBitrate(t.Bitrate);
                case 7: return Library.FormatSize(t.Size);
                case 8: return t.Path;
            }
            return "";
        }

        // ListView 列宽单击排序由外部处理
    }
}