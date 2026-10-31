using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MusicPlayer
{
    public class MainForm : Form
    {
        // ---------- 状态 ----------
        Settings settings;
        string configPath = "";
        string cachePath = "";
        TagCache cache;
        AudioEngine engine;
        Playlist playlist = new Playlist();
        List<Track> allTracks = new List<Track>();
        List<Track> view = new List<Track>();
        Theme T;
        System.Windows.Forms.Timer timer;
        volatile bool scanning = false;
        volatile bool scanCancel = false;
        string coverPathLoaded = "";
        Image coverImage = null;
        DateTime sleepUntil = DateTime.MinValue;
        bool seeking = false;
        int sortColumn = 0;
        bool sortAsc = true;

        // ---------- 控件 ----------
        Panel topBar, bottomBar, statusBar, searchPanel;
        FlatButton btnAddFolder, btnAddFiles, btnRemove, btnClear, btnSortShuffle, btnSettings;
        FlatButton btnPrev, btnPlay, btnStop, btnNext, btnMode, btnMute, btnImport, btnExport, btnLyrics;
        LyricsForm lyricsForm;
        TextBox txtSearch;
        Label lblSearchIcon;
        TrackListView list;
        PictureBox cover;
        Label lblTitle, lblInfo, lblPos, lblDur, lblVolume, lblStatusLeft, lblStatusMid, lblStatusRight;
        SliderBar seekBar, volBar;
        NotifyIcon tray;
        ContextMenuStrip listMenu, trayMenu;
        ToolTip tips;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        const int EM_SETCUEBANNER = 0x1501;
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        public MainForm()
        {
            T = Theme.Create("dark");
            settings = new Settings();

            Text = "轻音播放器  -  Light Music Player";
            BackColor = T.Window;
            ForeColor = T.Text;
            Font = T.BaseFont;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(S(820), S(540));
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            KeyPreview = true;

            configPath = Path.Combine(AppDir(), "MusicPlayer.config.json");
            cachePath = Path.Combine(AppDir(), "MusicPlayer.cache.json");
            settings = Settings.Load(configPath);
            T = Theme.Create(settings.Theme);
            sortColumn = settings.SortColumn;
            sortAsc = settings.SortAsc;
            ApplyTheme();

            BuildUI();
            ApplySettingsToUI();

            engine = new AudioEngine(settings);
            engine.TrackOpened += delegate(string p) { OnTrackOpened(p); };
            engine.TrackEnded += delegate() { OnTrackEnded(); };
            engine.TrackFailed += delegate(string msg) { OnTrackFailed(msg); };
            engine.StateChanged += delegate() { UpdatePlayButton(); };

            playlist.Mode = settings.PlayMode;
            playlist.HistoryLength = settings.HistoryLength;
            playlist.StopAfterCurrent = settings.StopAfterCurrent;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 250;
            timer.Tick += delegate(object s, EventArgs e) { OnTick(); };
            timer.Start();

            cache = TagCache.Load(cachePath);

            RestoreWindow();
            RestoreState();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
            Resize += delegate(object s, EventArgs e) { LayoutAll(); };
            Shown += delegate(object s, EventArgs e)
            {
                LayoutAll();
                try
                {
                    int w = settings.WindowW >= 600 ? settings.WindowW : 1080;
                    int h = settings.WindowH >= 400 ? settings.WindowH : 680;
                    Size nsz = new Size(S(w), S(h));
                    if (Math.Abs(nsz.Width - Width) > 4 || Math.Abs(nsz.Height - Height) > 4)
                    {
                        Size = nsz;
                        LayoutAll();
                    }
                }
                catch { }
                if (settings.LyricsEnabled)
                {
                    EnsureLyrics();
                    lyricsForm.SetTrack(playlist.CurrentTrack);
                    lyricsForm.Show();
                    lyricsForm.BringToFront();
                }
                UpdateLyricsButton();
                if (settings.ScanOnStart && settings.Folders.Count > 0 && allTracks.Count == 0) StartScan();
            };
        }

        int Sf = 96;
        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

        static string AppDir()
        {
            try
            {
                string d = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(d)) return d;
            }
            catch { }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        // ================= 界面构建 =================
        void ApplyTheme()
        {
            BackColor = T.Window;
            ForeColor = T.Text;
            Font = T.BaseFont;
        }

        void BuildUI()
        {
            tips = new ToolTip();
            tips.AutoPopDelay = 6000;

            // ---- 顶部工具栏 ----
            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = S(46);
            topBar.BackColor = T.Panel;

            btnAddFolder = MakeToolButton(Glyphs.Folder, "添加文件夹", 128);
            btnAddFiles = MakeToolButton(Glyphs.Add, "添加文件", 104);
            btnRemove = MakeToolButton(Glyphs.Delete, "移除", 76);
            btnClear = MakeToolButton(Glyphs.Clear, "清空", 76);
            btnImport = MakeToolButton(Glyphs.Open, "导入列表", 96);
            btnExport = MakeToolButton(Glyphs.Save, "导出列表", 96);
            btnSortShuffle = MakeToolButton(Glyphs.Shuffle, "随机排序", 96);
            btnSettings = MakeToolButton(Glyphs.Settings, "设置", 88);

            btnAddFolder.Click += delegate(object s, EventArgs e) { AddFolders(); };
            btnAddFiles.Click += delegate(object s, EventArgs e) { AddFiles(); };
            btnRemove.Click += delegate(object s, EventArgs e) { RemoveSelected(); };
            btnClear.Click += delegate(object s, EventArgs e) { ClearList(); };
            btnImport.Click += delegate(object s, EventArgs e) { ImportPlaylist(); };
            btnExport.Click += delegate(object s, EventArgs e) { ExportPlaylist(); };
            btnSortShuffle.Click += delegate(object s, EventArgs e) { ShuffleList(); };
            btnSettings.Click += delegate(object s, EventArgs e) { OpenSettings(); };

            searchPanel = new Panel();
            searchPanel.Height = S(26);
            searchPanel.BackColor = T.PanelAlt;
            lblSearchIcon = new Label();
            lblSearchIcon.Text = Glyphs.Search;
            lblSearchIcon.Font = T.IconFontSized(10f);
            lblSearchIcon.ForeColor = T.SubText;
            lblSearchIcon.BackColor = T.PanelAlt;
            lblSearchIcon.TextAlign = ContentAlignment.MiddleCenter;
            txtSearch = new TextBox();
            txtSearch.BorderStyle = BorderStyle.None;
            txtSearch.BackColor = T.PanelAlt;
            txtSearch.ForeColor = T.Text;
            txtSearch.Font = T.BaseFont;
            txtSearch.TextChanged += delegate(object s, EventArgs e) { ApplyFilter(); };
            txtSearch.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { txtSearch.Text = ""; list.Focus(); }
                if (e.KeyCode == Keys.Down) { list.Focus(); }
            };
            searchPanel.Controls.Add(lblSearchIcon);
            searchPanel.Controls.Add(txtSearch);

            topBar.Controls.Add(btnAddFolder);
            topBar.Controls.Add(btnAddFiles);
            topBar.Controls.Add(btnRemove);
            topBar.Controls.Add(btnClear);
            topBar.Controls.Add(btnImport);
            topBar.Controls.Add(btnExport);
            topBar.Controls.Add(btnSortShuffle);
            topBar.Controls.Add(searchPanel);
            topBar.Controls.Add(btnSettings);

            // ---- 歌曲列表 ----
            list = new TrackListView(T);
            list.Dock = DockStyle.Fill;
            list.Provider = delegate(int i) { return (i >= 0 && i < view.Count) ? view[i] : null; };
            list.RetrieveVirtualItem += delegate(object s, RetrieveVirtualItemEventArgs e)
            {
                // 虚拟模式下必须为每一列提供子项，否则 ListView 会抛出异常
                e.Item = new ListViewItem(new string[Library.Columns.Length]);
            };
            list.MouseDoubleClick += delegate(object s, MouseEventArgs e) { PlaySelected(); };
            list.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { PlaySelected(); e.Handled = true; }
            };
            list.ColumnClick += delegate(object s, ColumnClickEventArgs e) { SortBy(e.Column); };
            list.SelectedIndexChanged += delegate(object s, EventArgs e) { UpdateStatus(); };
            BuildColumns();
            BuildListMenu();

            // ---- 底部播放控制 ----
            bottomBar = new Panel();
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = S(108);
            bottomBar.BackColor = T.Panel;

            cover = new PictureBox();
            cover.SizeMode = PictureBoxSizeMode.Zoom;
            cover.BackColor = T.PanelAlt;
            cover.Size = new Size(S(64), S(64));
            cover.Image = PlaceholderImage(S(64));

            lblTitle = new Label();
            lblTitle.AutoEllipsis = true;
            lblTitle.Font = T.BigFont;
            lblTitle.ForeColor = T.Text;
            lblTitle.BackColor = T.Panel;
            lblTitle.Text = "未在播放";

            lblInfo = new Label();
            lblInfo.AutoEllipsis = true;
            lblInfo.Font = T.SmallFont;
            lblInfo.ForeColor = T.SubText;
            lblInfo.BackColor = T.Panel;
            lblInfo.Text = "双击列表中的歌曲开始播放";

            lblPos = new Label();
            lblPos.Font = T.SmallFont;
            lblPos.ForeColor = T.SubText;
            lblPos.BackColor = T.Panel;
            lblPos.TextAlign = ContentAlignment.MiddleRight;
            lblPos.Text = "00:00";

            lblDur = new Label();
            lblDur.Font = T.SmallFont;
            lblDur.ForeColor = T.SubText;
            lblDur.BackColor = T.Panel;
            lblDur.TextAlign = ContentAlignment.MiddleLeft;
            lblDur.Text = "00:00";

            seekBar = new SliderBar(T);
            seekBar.Min = 0;
            seekBar.Max = 1000;
            seekBar.DragStarted += delegate(object s, EventArgs e) { seeking = true; };
            seekBar.DragFinished += delegate(object s, EventArgs e) { SeekTo(seekBar.Value / 1000.0); seeking = false; };
            seekBar.UserChanged += delegate(object s, EventArgs e)
            {
                if (!seeking) SeekTo(seekBar.Value / 1000.0);
            };

            btnPrev = MakeIconButton(Glyphs.Prev, 40, "上一首 (Ctrl+Left)");
            btnPlay = MakeIconButton(Glyphs.Play, 52, "播放/暂停 (空格)");
            btnStop = MakeIconButton(Glyphs.Stop, 40, "停止 (Ctrl+S)");
            btnNext = MakeIconButton(Glyphs.Next, 40, "下一首 (Ctrl+Right)");
            btnMode = MakeIconButton(T.ModeGlyph(settings.PlayMode), 40, "播放模式 (Ctrl+M)");
            btnMode.Font = T.IconFontSized(12f);
            btnMute = MakeIconButton(Glyphs.Volume, 34, "静音");

            btnLyrics = new FlatButton(T);
            btnLyrics.Text = "歌词";
            btnLyrics.Font = T.SmallFont;
            btnLyrics.IsFlatSquare = false;
            btnLyrics.Height = S(26);
            btnLyrics.Width = S(46);
            btnLyrics.Click += delegate(object s, EventArgs e) { ToggleLyrics(); };
            tips.SetToolTip(btnLyrics, "桌面歌词 (Ctrl+L)");

            btnPrev.Click += delegate(object s, EventArgs e) { PrevTrack(); };
            btnPlay.Click += delegate(object s, EventArgs e) { TogglePlay(); };
            btnStop.Click += delegate(object s, EventArgs e) { StopPlay(); };
            btnNext.Click += delegate(object s, EventArgs e) { NextTrack(false); };
            btnMode.Click += delegate(object s, EventArgs e) { CycleMode(); };
            btnMute.Click += delegate(object s, EventArgs e) { ToggleMute(); };

            volBar = new SliderBar(T);
            volBar.Min = 0; volBar.Max = 100; volBar.ShowKnob = true;
            volBar.Value = settings.Volume;
            volBar.UserChanged += delegate(object s, EventArgs e)
            {
                engine.Volume = volBar.Value;
                settings.Volume = volBar.Value;
                lblVolume.Text = volBar.Value + "%";
            };

            lblVolume = new Label();
            lblVolume.Font = T.SmallFont;
            lblVolume.ForeColor = T.SubText;
            lblVolume.BackColor = T.Panel;
            lblVolume.TextAlign = ContentAlignment.MiddleLeft;
            lblVolume.Text = settings.Volume + "%";

            bottomBar.Controls.Add(cover);
            bottomBar.Controls.Add(lblTitle);
            bottomBar.Controls.Add(lblInfo);
            bottomBar.Controls.Add(lblPos);
            bottomBar.Controls.Add(lblDur);
            bottomBar.Controls.Add(seekBar);
            bottomBar.Controls.Add(btnPrev);
            bottomBar.Controls.Add(btnPlay);
            bottomBar.Controls.Add(btnStop);
            bottomBar.Controls.Add(btnNext);
            bottomBar.Controls.Add(btnMode);
            bottomBar.Controls.Add(btnLyrics);
            bottomBar.Controls.Add(btnMute);
            bottomBar.Controls.Add(volBar);
            bottomBar.Controls.Add(lblVolume);

            // ---- 状态栏 ----
            statusBar = new Panel();
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = S(26);
            statusBar.BackColor = T.PanelAlt;

            lblStatusLeft = MakeStatusLabel();
            lblStatusMid = MakeStatusLabel();
            lblStatusMid.TextAlign = ContentAlignment.MiddleCenter;
            lblStatusRight = MakeStatusLabel();
            lblStatusRight.TextAlign = ContentAlignment.MiddleRight;
            statusBar.Controls.Add(lblStatusLeft);
            statusBar.Controls.Add(lblStatusMid);
            statusBar.Controls.Add(lblStatusRight);

            Controls.Add(list);
            Controls.Add(bottomBar);
            Controls.Add(statusBar);
            Controls.Add(topBar);

            foreach (Control c in topBar.Controls) c.Dock = DockStyle.None;

            BuildTray();
            LayoutAll();
        }

        Label MakeStatusLabel()
        {
            Label l = new Label();
            l.Font = T.SmallFont;
            l.ForeColor = T.SubText;
            l.BackColor = T.PanelAlt;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.AutoEllipsis = true;
            return l;
        }

        FlatButton MakeToolButton(string glyph, string text, int width)
        {
            FlatButton b = new FlatButton(T);
            b.Font = T.BaseFont;
            b.Text = text;
            b.Width = S(width);
            b.Height = S(30);
            b.IsAccent = false;
            return b;
        }

        FlatButton MakeIconButton(string glyph, int size, string tip)
        {
            FlatButton b = new FlatButton(T);
            b.Text = glyph;
            b.Font = T.IconFontSized(11f);
            b.Width = S(size) + S(8);
            b.Height = S(size);
            b.IsAccent = false;
            if (glyph == Glyphs.Play || glyph == Glyphs.Pause) b.IsAccent = true;
            tips.SetToolTip(b, tip);
            return b;
        }

        void BuildColumns()
        {
            list.Columns.Clear();
            list.Columns.Add(Library.Columns[0], S(44), HorizontalAlignment.Left);
            list.Columns.Add(Library.Columns[1], S(260), HorizontalAlignment.Left);
            list.Columns.Add(Library.Columns[2], S(150), HorizontalAlignment.Left);
            list.Columns.Add(Library.Columns[3], S(150), HorizontalAlignment.Left);
            list.Columns.Add(Library.Columns[4], S(70), HorizontalAlignment.Right);
            list.Columns.Add(Library.Columns[5], S(58), HorizontalAlignment.Left);
            list.Columns.Add(Library.Columns[6], S(78), HorizontalAlignment.Right);
            list.Columns.Add(Library.Columns[7], S(78), HorizontalAlignment.Right);
            list.Columns.Add(Library.Columns[8], S(300), HorizontalAlignment.Left);
        }

        void BuildListMenu()
        {
            listMenu = new ContextMenuStrip();
            listMenu.BackColor = T.Panel;
            listMenu.ForeColor = T.Text;
            listMenu.Font = T.BaseFont;
            listMenu.Items.Add("播放", null, delegate(object s, EventArgs e) { PlaySelected(); });
            listMenu.Items.Add("立即播放并设为当前", null, delegate(object s, EventArgs e) { PlaySelected(); });
            listMenu.Items.Add(new ToolStripSeparator());
            listMenu.Items.Add("从列表移除", null, delegate(object s, EventArgs e) { RemoveSelected(); });
            listMenu.Items.Add("在资源管理器中显示", null, delegate(object s, EventArgs e) { RevealSelected(); });
            listMenu.Items.Add("复制文件路径", null, delegate(object s, EventArgs e) { CopyPaths(); });
            listMenu.Items.Add(new ToolStripSeparator());
            listMenu.Items.Add("歌曲属性", null, delegate(object s, EventArgs e) { ShowProperties(); });
            listMenu.Items.Add(new ToolStripSeparator());
            listMenu.Items.Add("随机打乱列表", null, delegate(object s, EventArgs e) { ShuffleList(); });
            listMenu.Items.Add("导入播放列表 (m3u)", null, delegate(object s, EventArgs e) { ImportPlaylist(); });
            listMenu.Items.Add("导出播放列表 (m3u)", null, delegate(object s, EventArgs e) { ExportPlaylist(); });
            list.ContextMenuStrip = listMenu;
        }

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "轻音播放器";
            tray.Visible = true;
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = T.Panel;
            trayMenu.ForeColor = T.Text;
            trayMenu.Items.Add("显示主界面", null, delegate(object s, EventArgs e) { ShowFromTray(); });
            trayMenu.Items.Add("播放 / 暂停", null, delegate(object s, EventArgs e) { TogglePlay(); });
            trayMenu.Items.Add("上一首", null, delegate(object s, EventArgs e) { PrevTrack(); });
            trayMenu.Items.Add("下一首", null, delegate(object s, EventArgs e) { NextTrack(false); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("桌面歌词 开/关", null, delegate(object s, EventArgs e) { ToggleLyrics(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, delegate(object s, EventArgs e) { QuitApp(); });
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate(object s, EventArgs e) { ShowFromTray(); };
            Icon = tray.Icon;
        }

        Icon MakeIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(79, 140, 255)))
                    FlatButton.FillRounded(g, b, new Rectangle(1, 1, 30, 30), 8);
                using (Font f = T.IconFontSized(16f))
                using (SolidBrush b = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Glyphs.Music, f, b, new RectangleF(0, 0, 32, 32), sf);
                }
            }
            IntPtr h = bmp.GetHicon();
            try { Icon ic = (Icon)Icon.FromHandle(h).Clone(); return ic; }
            finally { DestroyIcon(h); bmp.Dispose(); }
        }

        Image PlaceholderImage(int size)
        {
            if (size <= 0) size = 64;
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(T.PanelAlt);
                using (Font f = T.IconFontSized(size * 0.4f))
                using (SolidBrush b = new SolidBrush(T.SubText))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Glyphs.Music, f, b, new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        // ================= 布局 =================
        void LayoutAll()
        {
            if (topBar == null) return;
            Sf = DetectDpi();
            if (Sf <= 0) Sf = 96;
            Size ms2 = new Size(S(820), S(540));
            if (MinimumSize != ms2) MinimumSize = ms2;
            ApplyMetrics();

            int y = S(8);
            int gap = S(6);

            // ---- 顶栏：设置按钮先靠右，左侧按钮空间不足时自动收起次要项 ----
            btnSettings.Location = new Point(Math.Max(S(10), topBar.Width - btnSettings.Width - S(10)), y);
            int searchMax = S(230), searchMin = S(120);
            int searchRight = btnSettings.Left - S(10);

            int x = S(10);
            btnAddFolder.Location = new Point(x, y); x += btnAddFolder.Width + gap;
            btnAddFiles.Location = new Point(x, y); x += btnAddFiles.Width + gap;
            btnRemove.Location = new Point(x, y); x += btnRemove.Width + gap;
            btnClear.Location = new Point(x, y); x += btnClear.Width + gap;

            int optNeed = btnImport.Width + gap + btnExport.Width + gap + btnSortShuffle.Width;
            bool showOptional = (searchRight - x - optNeed - S(8)) >= searchMin;
            btnImport.Visible = showOptional;
            btnExport.Visible = showOptional;
            btnSortShuffle.Visible = showOptional;
            if (showOptional)
            {
                btnImport.Location = new Point(x, y); x += btnImport.Width + gap;
                btnExport.Location = new Point(x, y); x += btnExport.Width + gap;
                btnSortShuffle.Location = new Point(x, y); x += btnSortShuffle.Width + gap;
            }

            int sw = Math.Max(searchMin, Math.Min(searchMax, searchRight - x - S(6)));
            searchPanel.Location = new Point(searchRight - sw, y + S(2));
            searchPanel.Size = new Size(sw, S(26));
            lblSearchIcon.Location = new Point(0, 0);
            lblSearchIcon.Size = new Size(S(22), S(24));
            txtSearch.Location = new Point(S(24), S(4));
            txtSearch.Width = Math.Max(S(50), sw - S(32));

            // ---- 底栏 ----
            int cy = S(10);
            cover.Location = new Point(S(12), cy);
            cover.Size = new Size(S(64), S(64));
            int lx = cover.Right + S(12);

            int right = bottomBar.Width - S(12);
            int cyb = cy + S(14);
            btnNext.Location = new Point(right - btnNext.Width, cyb); right = btnNext.Left - S(6);
            btnStop.Location = new Point(right - btnStop.Width, cyb); right = btnStop.Left - S(6);
            btnPlay.Location = new Point(right - btnPlay.Width, cyb - S(6)); right = btnPlay.Left - S(6);
            btnPrev.Location = new Point(right - btnPrev.Width, cyb); right = btnPrev.Left - S(6);
            btnMode.Location = new Point(right - btnMode.Width, cyb); right = btnMode.Left - S(6);
            btnLyrics.Location = new Point(right - btnLyrics.Width, cyb + S(6)); right = btnLyrics.Left - S(12);

            // 音量区：空间不足时先缩短滑条，再隐藏静音图标与数值
            int sliderW = S(116);
            int labelMin = S(130);
            btnMute.Visible = true;
            lblVolume.Visible = true;
            int volNeed = S(4) + btnMute.Width + S(4) + sliderW + S(6) + S(40);
            if (right - lx - volNeed < labelMin)
            {
                sliderW = S(116) - (labelMin - (right - lx - volNeed));
                if (sliderW < S(60)) sliderW = S(60);
                volNeed = S(4) + btnMute.Width + S(4) + sliderW + S(6) + S(40);
                if (right - lx - volNeed < S(80))
                {
                    btnMute.Visible = false;
                    lblVolume.Visible = false;
                    volNeed = sliderW;
                }
            }
            int volLeft = right - volNeed;
            btnMute.Location = new Point(volLeft, cyb + S(2));
            volBar.Size = new Size(sliderW, S(22));
            volBar.Location = new Point(btnMute.Visible ? btnMute.Right + S(4) : volLeft, cyb + S(4));
            lblVolume.Location = new Point(volBar.Right + S(6), cyb + S(6));
            lblVolume.Size = new Size(S(40), S(20));

            // 歌曲信息标签宽度跟随可用空间，避免与右侧控件重叠
            int labelW = Math.Max(S(90), volLeft - lx - S(10));
            lblTitle.Location = new Point(lx, cy + S(2));
            lblTitle.Size = new Size(labelW, S(26));
            lblInfo.Location = new Point(lx, cy + S(30));
            lblInfo.Size = new Size(labelW, S(20));

            int row2y = cy + S(72);
            lblPos.Location = new Point(S(12), row2y + S(2));
            lblPos.Size = new Size(S(44), S(18));
            lblDur.Location = new Point(bottomBar.Width - S(12) - S(44), row2y + S(2));
            lblDur.Size = new Size(S(44), S(18));
            seekBar.Location = new Point(lblPos.Right + S(2), row2y);
            seekBar.Size = new Size(Math.Max(S(80), lblDur.Left - lblPos.Right - S(6)), S(22));

            lblStatusLeft.Location = new Point(S(10), 0);
            lblStatusLeft.Size = new Size(statusBar.Width / 3, statusBar.Height);
            lblStatusMid.Location = new Point(statusBar.Width / 3, 0);
            lblStatusMid.Size = new Size(statusBar.Width / 3, statusBar.Height);
            lblStatusRight.Location = new Point(statusBar.Width * 2 / 3, 0);
            lblStatusRight.Size = new Size(statusBar.Width / 3 - S(10), statusBar.Height);
            FitColumns();
        }

        // DPI 确定后重算所有固定尺寸（构造时按 96 DPI 算的会偏小，导致文字被裁）
        void ApplyMetrics()
        {
            if (topBar.Height != S(46)) topBar.Height = S(46);
            if (bottomBar.Height != S(108)) bottomBar.Height = S(108);
            if (statusBar.Height != S(26)) statusBar.Height = S(26);
            if (searchPanel.Height != S(26)) searchPanel.Height = S(26);

            SizeSize(btnAddFolder, 128, 30); SizeSize(btnAddFiles, 104, 30);
            SizeSize(btnRemove, 76, 30); SizeSize(btnClear, 76, 30);
            SizeSize(btnImport, 96, 30); SizeSize(btnExport, 96, 30);
            SizeSize(btnSortShuffle, 96, 30); SizeSize(btnSettings, 88, 30);

            SizeSize(btnPrev, 48, 40); SizeSize(btnStop, 48, 40);
            SizeSize(btnNext, 48, 40); SizeSize(btnMode, 48, 40);
            SizeSize(btnPlay, 60, 52); SizeSize(btnMute, 42, 34);
            SizeSize(btnLyrics, 46, 26);

            if (cover.Width != S(64)) cover.Size = new Size(S(64), S(64));
            try
            {
                if (list.SmallImageList != null && list.SmallImageList.ImageSize.Height != S(27))
                {
                    list.SmallImageList.ImageSize = new Size(1, S(27));
                    list.Invalidate();
                }
            }
            catch { }
        }

        void SizeSize(Control c, int w, int h)
        {
            if (c == null) return;
            int nw = S(w), nh = S(h);
            if (c.Width != nw || c.Height != nh) c.Size = new Size(nw, nh);
        }

        // 列表列宽按窗口宽度自适应，保证最后一列不会被挤出去
        void FitColumns()
        {
            if (list == null || list.Columns.Count < 9) return;
            int avail = list.ClientSize.Width;
            if (avail <= 0) return;
            double[] weight = new double[] { 44, 260, 150, 150, 70, 58, 78, 78, 300 };
            int[] min = new int[] { S(34), S(120), S(72), S(70), S(50), S(42), S(56), S(56), S(90) };
            double totalW = 0;
            int sumMin = 0;
            for (int i = 0; i < 9; i++) { totalW += weight[i]; sumMin += min[i]; }
            int extra = avail - sumMin;
            if (extra < 0) extra = 0;
            int used = 0;
            for (int i = 0; i < 9; i++)
            {
                int v = min[i] + (int)Math.Round(extra * (weight[i] / totalW));
                if (i == 8) v = Math.Max(min[i], avail - used);
                list.Columns[i].Width = v;
                used += v;
            }
        }

        static int DetectDpiStatic() { return 96; }

        int DetectDpi()
        {
            try
            {
                if (IsHandleCreated)
                {
                    using (Graphics g = CreateGraphics())
                    {
                        int d = (int)Math.Round(g.DpiX);
                        if (d >= 72 && d <= 480) return d;
                    }
                }
            }
            catch { }
            try { int d = DeviceDpi; if (d >= 72 && d <= 480) return d; } catch { }
            return 96;
        }

        void ApplySettingsToUI()
        {
            volBar.Value = settings.Volume;
            lblVolume.Text = settings.Volume + "%";
            btnMode.Text = T.ModeGlyph(settings.PlayMode);
            tips.SetToolTip(btnMode, "播放模式：" + Playlist.ModeName(settings.PlayMode) + " (Ctrl+M)");
            tips.SetToolTip(btnMute, "音量：" + settings.Volume + "%");
            ApplyFilter();
        }

        void RestoreWindow()
        {
            try
            {
                int w = settings.WindowW, h = settings.WindowH;
                if (w < 600) w = 1080;
                if (h < 400) h = 680;
                Size = new Size(S(w), S(h));
                if (settings.WindowX >= 0 && settings.WindowY >= 0)
                {
                    Point p = new Point(settings.WindowX, settings.WindowY);
                    bool onScreen = false;
                    foreach (Screen sc in Screen.AllScreens)
                        if (sc.WorkingArea.Contains(new Rectangle(p, new Size(60, 60)))) onScreen = true;
                    if (onScreen) Location = p;
                }
                LayoutAll();
            }
            catch { }
        }

        // ================= 扫描 =================
        void StartScan()
        {
            if (scanning)
            {
                scanCancel = true;
                return;
            }
            if (settings.Folders.Count == 0)
            {
                MessageBox.Show(this, "请先在“设置 - 目录”中添加音乐文件夹，或点工具栏的“添加文件夹”。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            scanCancel = false;
            scanning = true;
            SetStatus("正在扫描音乐文件…");
            DateTime t0 = DateTime.Now;

            Thread th = new Thread(delegate()
            {
                List<Track> mine = new List<Track>();
                try
                {
                    List<string> files = Library.CollectFiles(settings.Folders, settings, delegate() { return scanCancel; },
                        delegate(string dir) { BeginInvoke((MethodInvoker)delegate { SetStatus("正在扫描：" + dir); }); });

                    // 第一阶段：先把文件加入列表（不含标签），界面立刻有内容
                    List<Track> batch = new List<Track>();
                    foreach (string f in files)
                    {
                        if (scanCancel) break;
                        Track t = Library.MakeTrack(f);
                        mine.Add(t);
                        batch.Add(t);
                        if (batch.Count >= 200)
                        {
                            List<Track> b2 = batch; batch = new List<Track>();
                            BeginInvoke((MethodInvoker)delegate { AddTracks(b2); });
                        }
                    }
                    if (batch.Count > 0)
                    {
                        List<Track> b2 = batch;
                        BeginInvoke((MethodInvoker)delegate { AddTracks(b2); });
                    }
                    int total = mine.Count;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (settings.SortColumn > 0) ApplySort();
                        SetStatus("正在读取音乐信息… 0/" + total);
                    });

                    // 第二阶段：逐个读取标签（只读文件头，不加载整首音乐）
                    int done = 0;
                    foreach (Track t in mine)
                    {
                        if (scanCancel) break;
                        Library.FillTags(t, settings.UseCache ? cache : null, settings);
                        done++;
                        if (done % 25 == 0)
                        {
                            int d0 = done;
                            BeginInvoke((MethodInvoker)delegate
                            {
                                list.Invalidate();
                                SetStatus(string.Format("正在读取音乐信息… {0}/{1}", d0, total));
                            });
                        }
                    }
                    int finalTotal = mine.Count;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        list.Invalidate();
                        if (settings.SortColumn > 0) ApplySort();
                        scanning = false;
                        cache.Save(cachePath);
                        UpdateStatus();
                        SetStatus(string.Format("扫描完成：{0} 首，用时 {1:0.0} 秒", finalTotal, (DateTime.Now - t0).TotalSeconds));
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        scanning = false;
                        SetStatus("扫描出错：" + ex.Message);
                    });
                }
            });
            th.IsBackground = true;
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        void AddTracks(List<Track> batch)
        {
            allTracks.AddRange(batch);
            int added = 0;
            string q = txtSearch.Text.Trim();
            foreach (Track t in batch)
            {
                if (q.Length == 0 || Matches(t, q)) { view.Add(t); added++; }
            }
            if (view.Count > 32766) list.VirtualListSize = view.Count;
            else list.VirtualListSize = view.Count;
            SyncPlaylist();
            list.Invalidate();
            UpdateStatus();
        }

        bool Matches(Track t, string q)
        {
            string s = q.ToLowerInvariant();
            return (t.DisplayTitle.ToLowerInvariant().Contains(s)
                 || t.Artist.ToLowerInvariant().Contains(s)
                 || t.Album.ToLowerInvariant().Contains(s)
                 || t.Path.ToLowerInvariant().Contains(s));
        }

        void ApplyFilter()
        {
            string q = txtSearch.Text.Trim();
            List<Track> nv = new List<Track>();
            foreach (Track t in allTracks)
                if (q.Length == 0 || Matches(t, q)) nv.Add(t);
            view = nv;
            list.VirtualListSize = view.Count;
            SyncPlaylist();
            list.Invalidate();
            UpdateStatus();
        }

        void SyncPlaylist()
        {
            string cur = (playlist.CurrentTrack != null) ? playlist.CurrentTrack.Path : "";
            playlist.Tracks = new List<Track>(view);
            playlist.Mode = settings.PlayMode;
            playlist.HistoryLength = settings.HistoryLength;
            playlist.StopAfterCurrent = settings.StopAfterCurrent;
            if (cur != "")
            {
                for (int i = 0; i < view.Count; i++) if (string.Equals(view[i].Path, cur, StringComparison.OrdinalIgnoreCase)) { playlist.Current = i; break; }
            }
            int pi = playlist.Current;
            list.PlayingIndex = pi;
        }

        void ApplySort()
        {
            if (sortColumn <= 0) return;
            view.Sort(Library.Comparer(sortColumn, sortAsc));
            SyncPlaylist();
            list.Invalidate();
        }

        void SortBy(int col)
        {
            if (col == 0) { sortColumn = 0; }
            else
            {
                if (sortColumn == col) sortAsc = !sortAsc;
                else { sortColumn = col; sortAsc = true; }
            }
            settings.SortColumn = sortColumn;
            settings.SortAsc = sortAsc;
            if (sortColumn > 0) view.Sort(Library.Comparer(sortColumn, sortAsc));
            else { view.Sort(Library.Comparer(8, true)); }
            SyncPlaylist();
            list.Invalidate();
        }

        void AddFolders()
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            d.Description = "选择音乐文件夹（会自动扫描其中的音乐文件）";
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                if (!settings.Folders.Contains(d.SelectedPath))
                {
                    settings.Folders.Add(d.SelectedPath);
                    settings.Save(configPath);
                }
                StartScan();
            }
        }

        void AddFiles()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true;
            d.Filter = "音频文件|*.mp3;*.wav;*.wma;*.flac;*.m4a;*.aac;*.ogg;*.oga;*.opus;*.aif;*.aiff;*.ape;*.wv;*.mp4;*.mka|所有文件|*.*";
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                List<Track> batch = new List<Track>();
                foreach (string f in d.FileNames)
                {
                    Track t = Library.MakeTrack(f);
                    Library.FillTags(t, settings.UseCache ? cache : null, settings);
                    batch.Add(t);
                }
                AddTracks(batch);
                if (settings.SortColumn > 0) ApplySort();
                SetStatus(string.Format("已添加 {0} 个文件", batch.Count));
            }
        }

        void RemoveSelected()
        {
            if (list.SelectedIndices.Count == 0) return;
            List<int> idx = new List<int>();
            foreach (int i in list.SelectedIndices) idx.Add(i);
            idx.Sort();
            string cur = (playlist.CurrentTrack != null) ? playlist.CurrentTrack.Path : "";
            for (int k = idx.Count - 1; k >= 0; k--)
            {
                int i = idx[k];
                if (i < 0 || i >= view.Count) continue;
                Track t = view[i];
                allTracks.Remove(t);
                view.RemoveAt(i);
            }
            list.VirtualListSize = view.Count;
            SyncPlaylist();
            if (cur != "" && playlist.Current < 0)
            {
                // 当前播放曲目已被移除
            }
            list.Invalidate();
            UpdateStatus();
        }

        void ClearList()
        {
            if (allTracks.Count == 0) return;
            if (MessageBox.Show(this, "确定清空当前播放列表吗？（不会删除磁盘文件）", "清空列表",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            playlist.StopAfterCurrent = false;
            engine.Stop();
            engine.Unload();
            allTracks.Clear();
            view.Clear();
            playlist.Clear();
            list.PlayingIndex = -1;
            list.VirtualListSize = 0;
            list.Invalidate();
            lblTitle.Text = "未在播放";
            lblInfo.Text = "双击列表中的歌曲开始播放";
            SetCover(null, "");
            UpdateStatus();
        }

        void ShuffleList()
        {
            if (view.Count == 0) return;
            playlist.Tracks = new List<Track>(view);
            playlist.ShuffleAll();
            view = new List<Track>(playlist.Tracks);
            list.VirtualListSize = view.Count;
            playlist.Current = -1;
            list.PlayingIndex = -1;
            list.Invalidate();
            SetStatus("已随机打乱列表顺序");
        }

        void ImportPlaylist()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "播放列表 (*.m3u;*.m3u8)|*.m3u;*.m3u8|所有文件|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            List<string> files = Playlist.ImportM3U(d.FileName);
            List<Track> batch = new List<Track>();
            foreach (string f in files)
            {
                Track t = Library.MakeTrack(f);
                Library.FillTags(t, settings.UseCache ? cache : null, settings);
                batch.Add(t);
            }
            AddTracks(batch);
            SetStatus(string.Format("已导入 {0} 首", batch.Count));
        }

        void ExportPlaylist()
        {
            if (view.Count == 0) return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "播放列表 (*.m3u)|*.m3u";
            d.FileName = "我的播放列表.m3u";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            Playlist.ExportM3U(d.FileName, view);
            SetStatus("已导出：" + d.FileName);
        }

        // ================= 播放控制 =================
        void PlaySelected()
        {
            if (list.SelectedIndices.Count == 0) return;
            PlayAt(list.SelectedIndices[0]);
        }

        void PlayAt(int index)
        {
            if (index < 0 || index >= view.Count) return;
            playlist.Current = index;
            list.PlayingIndex = index;
            Track t = view[index];
            engine.Volume = settings.Volume;
            engine.PlayTrack(t.Path, 0);
            list.EnsureVisible(index);
            list.Invalidate();
            UpdateNowPlayingUI(t);
            SaveStateSoon();
        }

        void TogglePlay()
        {
            if (!engine.HasTrack)
            {
                if (view.Count > 0)
                {
                    int idx = (list.SelectedIndices.Count > 0) ? list.SelectedIndices[0] : 0;
                    PlayAt(idx);
                }
                return;
            }
            engine.Toggle();
            UpdatePlayButton();
        }

        void StopPlay()
        {
            engine.Stop();
            seekBar.Value = 0;
            UpdatePlayButton();
        }

        void NextTrack(bool auto)
        {
            int n = -1;
            if (auto)
            {
                if (playlist.StopAfterCurrent) { StopPlay(); return; }
            }
            n = playlist.NextIndex(auto);
            if (n < 0)
            {
                StopPlay();
                SetStatus("播放列表已结束");
                return;
            }
            PlayAt(n);
        }

        void PrevTrack()
        {
            if (engine.PositionMs > 4000) { engine.PositionMs = 0; return; }
            int n = playlist.PrevIndex();
            if (n < 0) return;
            PlayAt(n);
        }

        void CycleMode()
        {
            settings.PlayMode = Playlist.ModeNext(settings.PlayMode);
            playlist.Mode = settings.PlayMode;
            btnMode.Text = T.ModeGlyph(settings.PlayMode);
            tips.SetToolTip(btnMode, "播放模式：" + Playlist.ModeName(settings.PlayMode) + " (Ctrl+M)");
            SetStatus("播放模式：" + Playlist.ModeName(settings.PlayMode));
            UpdateStatus();
            settings.Save(configPath);
        }

        bool muted = false;
        int volumeBeforeMute = 0;

        void ToggleMute()
        {
            if (!muted)
            {
                volumeBeforeMute = settings.Volume;
                engine.Volume = 0;
                muted = true;
                btnMute.Text = Glyphs.Mute;
                lblVolume.Text = "静音";
            }
            else
            {
                muted = false;
                engine.Volume = volumeBeforeMute;
                settings.Volume = volumeBeforeMute;
                volBar.Value = settings.Volume;
                btnMute.Text = Glyphs.Volume;
                lblVolume.Text = settings.Volume + "%";
            }
        }

        void SeekTo(double ratio)
        {
            long dur = engine.DurationMs;
            if (dur <= 0) dur = (playlist.CurrentTrack != null) ? (long)(playlist.CurrentTrack.Duration * 1000) : 0;
            if (dur <= 0) return;
            engine.PositionMs = (long)(dur * ratio);
            UpdateTimeLabels();
        }

        void OnTick()
        {
            if (engine != null) engine.Tick();

            if (engine.HasTrack && !seeking)
            {
                long dur = engine.DurationMs;
                long pos = engine.PositionMs;
                if (dur <= 0 && playlist.CurrentTrack != null)
                {
                    long meta = (long)(playlist.CurrentTrack.Duration * 1000);
                    if (meta > 0) dur = meta;
                }
                if (dur > 0)
                {
                    int v = (int)Math.Max(0, Math.Min(1000, pos * 1000 / dur));
                    seekBar.Value = v;
                }
                lblPos.Text = Library.FormatDuration(pos / 1000.0);

                // 时长未知的歌曲，播放后补全列表信息
                Track cur = playlist.CurrentTrack;
                if (cur != null && cur.Duration <= 0 && dur > 1000)
                {
                    cur.Duration = dur / 1000.0;
                    list.Invalidate();
                }
                if (lblDur.Text == "--:--" && dur > 0) lblDur.Text = Library.FormatDuration(dur / 1000.0);
            }

            if (sleepUntil != DateTime.MinValue)
            {
                if (DateTime.Now >= sleepUntil)
                {
                    sleepUntil = DateTime.MinValue;
                    engine.Stop();
                    tray.ShowBalloonTip(3000, "定时停止", "已按设定时间停止播放", ToolTipIcon.Info);
                    SetStatus("定时停止已触发");
                }
                else UpdateStatus();
            }

            if (scanning) list.Invalidate();
        }

        void OnTrackOpened(string path)
        {
            Track t = playlist.CurrentTrack;
            if (t == null || !string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                foreach (Track x in view) if (string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)) { t = x; break; }
            }
            if (t != null) UpdateNowPlayingUI(t);
            UpdatePlayButton();
            UpdateStatus();
        }

        void OnTrackEnded()
        {
            if (playlist.Mode == "repeatOne") { PlayAt(playlist.Current); return; }
            NextTrack(true);
        }

        void OnTrackFailed(string msg)
        {
            SetStatus(msg);
            tray.ShowBalloonTip(4000, "无法播放", msg, ToolTipIcon.Warning);
        }

        void UpdateNowPlayingUI(Track t)
        {
            lblTitle.Text = t.DisplayTitle;
            string s = t.DisplayArtist + (t.Album.Length > 0 ? ("  ·  " + t.Album) : "");
            if (t.Bitrate > 0) s += "   " + t.Bitrate + " kbps";
            if (t.Ext.Length > 0) s += "   " + t.Ext.TrimStart('.').ToUpperInvariant();
            lblInfo.Text = s.Trim();
            long dur = (long)(t.Duration * 1000);
            lblDur.Text = dur > 0 ? Library.FormatDuration(dur / 1000.0) : "--:--";
            lblPos.Text = "00:00";
            seekBar.Value = 0;
            Text = t.DisplayTitle + " - " + t.DisplayArtist + " | 轻音播放器";
            if (tray != null) tray.Text = Truncate("轻音播放器 - " + t.DisplayTitle, 62);
            LoadCoverAsync(t);
            SyncLyrics(t);
        }

        static string Truncate(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }

        void LoadCoverAsync(Track t)
        {
            if (!settings.ShowCover) { SetCover(null, t.Path); return; }
            if (string.Equals(coverPathLoaded, t.Path, StringComparison.OrdinalIgnoreCase)) return;
            coverPathLoaded = t.Path;
            string path = t.Path;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                byte[] data = null;
                try { data = TagReader.ReadCover(path); }
                catch { }
                Image img = null;
                if (data != null && data.Length > 0)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            using (Image tmp = Image.FromStream(ms))
                            {
                                img = new Bitmap(tmp);
                            }
                        }
                    }
                    catch { img = null; }
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!string.Equals(coverPathLoaded, path, StringComparison.OrdinalIgnoreCase)) { if (img != null) img.Dispose(); return; }
                        SetCover(img, path);
                    });
                }
                catch { if (img != null) img.Dispose(); }
            });
        }

        void SetCover(Image img, string path)
        {
            Image old = coverImage;
            coverImage = img;
            if (img != null) cover.Image = img;
            else cover.Image = PlaceholderImage(cover.Width);
            if (old != null) old.Dispose();
        }

        void UpdatePlayButton()
        {
            PlayState st = engine == null ? PlayState.Stopped : engine.State;
            bool playing = st == PlayState.Playing;
            btnPlay.Text = playing ? Glyphs.Pause : Glyphs.Play;
        }

        void UpdateTimeLabels()
        {
            long dur = engine.DurationMs;
            if (dur > 0)
            {
                int v = (int)Math.Max(0, Math.Min(1000, engine.PositionMs * 1000 / dur));
                seekBar.Value = v;
            }
            lblPos.Text = Library.FormatDuration(engine.PositionMs / 1000.0);
        }

        // ================= 状态 =================
        void SetStatus(string s)
        {
            if (lblStatusLeft != null) lblStatusLeft.Text = s;
        }

        void UpdateStatus()
        {
            if (lblStatusLeft == null) return;
            double total = 0;
            foreach (Track t in view) total += t.Duration;
            string left = string.Format("共 {0} 首 / {1}", view.Count, Library.FormatDuration(total));
            if (allTracks.Count != view.Count) left = string.Format("显示 {0} 首 / 全部 {1} 首", view.Count, allTracks.Count);

            string mid = "引擎：" + (engine != null && engine.BackendName != "" ? engine.BackendName : "待命")
                       + "   模式：" + Playlist.ModeName(settings.PlayMode);

            string right = "";
            if (scanning) right = "扫描中…";
            else if (sleepUntil != DateTime.MinValue)
            {
                TimeSpan ts = sleepUntil - DateTime.Now;
                if (ts.TotalSeconds > 0) right = string.Format("定时停止 {0:00}:{1:00}", (int)ts.TotalMinutes, ts.Seconds);
            }
            lblStatusMid.Text = mid;
            lblStatusRight.Text = right;
            if (!scanning && lblStatusLeft.Text.StartsWith("正在")) lblStatusLeft.Text = left;
            else if (!scanning) lblStatusLeft.Text = left;
        }

        // ================= 键盘 =================
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                if (keyData == Keys.Space) { TogglePlay(); return true; }
                if (keyData == (Keys.Control | Keys.O)) { AddFolders(); return true; }
                if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); txtSearch.SelectAll(); return true; }
                if (keyData == (Keys.Control | Keys.M)) { CycleMode(); return true; }
                if (keyData == (Keys.Control | Keys.L)) { ToggleLyrics(); return true; }
                if (keyData == (Keys.Control | Keys.S)) { StopPlay(); return true; }
                if (keyData == (Keys.Control | Keys.Right)) { NextTrack(false); return true; }
                if (keyData == (Keys.Control | Keys.Left)) { PrevTrack(); return true; }
                if (keyData == (Keys.Control | Keys.Up)) { SetVolume(settings.Volume + 5); return true; }
                if (keyData == (Keys.Control | Keys.Down)) { SetVolume(settings.Volume - 5); return true; }
                if (keyData == Keys.MediaPlayPause) { TogglePlay(); return true; }
                if (keyData == Keys.MediaNextTrack) { NextTrack(false); return true; }
                if (keyData == Keys.MediaPreviousTrack) { PrevTrack(); return true; }
                if (keyData == Keys.MediaStop) { StopPlay(); return true; }
                if (keyData == Keys.Delete && !txtSearch.Focused) { RemoveSelected(); return true; }
                if (keyData == Keys.Left && !txtSearch.Focused) { SeekRelative(-settings.SeekStep); return true; }
                if (keyData == Keys.Right && !txtSearch.Focused) { SeekRelative(settings.SeekStep); return true; }
                if (keyData == (Keys.Control | Keys.A) && !txtSearch.Focused) { list.Focus(); return true; }
            }
            catch { }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void SetVolume(int v)
        {
            settings.Volume = Settings.Clamp(v, 0, 100);
            engine.Volume = settings.Volume;
            volBar.Value = settings.Volume;
            muted = false;
            btnMute.Text = Glyphs.Volume;
            lblVolume.Text = settings.Volume + "%";
        }

        void SeekRelative(int seconds)
        {
            if (!engine.HasTrack) return;
            long pos = engine.PositionMs + seconds * 1000L;
            if (pos < 0) pos = 0;
            engine.PositionMs = pos;
            UpdateTimeLabels();
        }

        // ================= 拖放 =================
        void OnDragEnter(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void OnDragDrop(object s, DragEventArgs e)
        {
            string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (items == null || items.Length == 0) return;
            List<Track> batch = new List<Track>();
            foreach (string it in items)
            {
                if (Directory.Exists(it))
                {
                    List<string> files = Library.CollectFiles(new List<string>(new string[] { it }), settings, null, null);
                    foreach (string f in files)
                    {
                        Track t = Library.MakeTrack(f);
                        Library.FillTags(t, settings.UseCache ? cache : null, settings);
                        batch.Add(t);
                    }
                }
                else if (File.Exists(it))
                {
                    Track t = Library.MakeTrack(it);
                    Library.FillTags(t, settings.UseCache ? cache : null, settings);
                    batch.Add(t);
                }
            }
            if (batch.Count > 0)
            {
                AddTracks(batch);
                SetStatus(string.Format("已添加 {0} 首", batch.Count));
            }
        }

        void RevealSelected()
        {
            if (list.SelectedIndices.Count == 0) return;
            Track t = view[list.SelectedIndices[0]];
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + t.Path + "\""); }
            catch { }
        }

        void CopyPaths()
        {
            if (list.SelectedIndices.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (int i in list.SelectedIndices) if (i < view.Count) sb.AppendLine(view[i].Path);
            try { Clipboard.SetText(sb.ToString()); SetStatus("已复制路径"); }
            catch { }
        }

        void ShowProperties()
        {
            if (list.SelectedIndices.Count == 0) return;
            Track t = view[list.SelectedIndices[0]];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("标题：" + t.DisplayTitle);
            sb.AppendLine("艺术家：" + t.DisplayArtist);
            sb.AppendLine("专辑：" + t.DisplayAlbum);
            sb.AppendLine("流派：" + t.Genre);
            sb.AppendLine("年份：" + (t.Year > 0 ? t.Year.ToString() : "-"));
            sb.AppendLine("时长：" + Library.FormatDuration(t.Duration));
            sb.AppendLine("比特率：" + (t.Bitrate > 0 ? t.Bitrate + " kbps" : "-"));
            sb.AppendLine("采样率：" + (t.SampleRate > 0 ? t.SampleRate + " Hz" : "-"));
            sb.AppendLine("大小：" + Library.FormatSize(t.Size));
            sb.AppendLine("格式：" + t.Ext);
            sb.AppendLine("路径：" + t.Path);
            MessageBox.Show(this, sb.ToString(), "歌曲属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ================= 设置 =================
        void OpenSettings()
        {
            SettingsForm f = new SettingsForm(settings, cache);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                Theme before = T;
                bool themeChanged = (before.Name != settings.Theme);
                T = Theme.Create(settings.Theme);
                playlist.Mode = settings.PlayMode;
                playlist.HistoryLength = settings.HistoryLength;
                playlist.StopAfterCurrent = settings.StopAfterCurrent;
                engine.Volume = settings.Volume;
                volBar.Value = settings.Volume;
                lblVolume.Text = settings.Volume + "%";
                btnMode.Text = T.ModeGlyph(settings.PlayMode);
                btnMode.Font = T.IconFontSized(12f);
                settings.Save(configPath);
                if (settings.SleepTimerMinutes > 0)
                {
                    sleepUntil = DateTime.Now.AddMinutes(settings.SleepTimerMinutes);
                    SetStatus(string.Format("已设置 {0} 分钟后停止播放", settings.SleepTimerMinutes));
                }
                else sleepUntil = DateTime.MinValue;
                try
                {
                    if (settings.LyricsEnabled)
                    {
                        EnsureLyrics();
                        lyricsForm.ApplySettings();
                        if (!lyricsForm.Visible) lyricsForm.Show();
                        lyricsForm.SetTrack(playlist.CurrentTrack);
                    }
                    else if (lyricsForm != null && !lyricsForm.IsDisposed)
                    {
                        lyricsForm.Hide();
                    }
                    UpdateLyricsButton();
                }
                catch { }
                if (themeChanged)
                {
                    MessageBox.Show(this, "主题已保存，重新打开程序后完全生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                UpdateStatus();
            }
        }

        // ================= 状态保存 =================
        void SaveStateSoon() { }

        // ================= 桌面歌词 =================
        void EnsureLyrics()
        {
            if (lyricsForm != null && !lyricsForm.IsDisposed) return;
            lyricsForm = new LyricsForm(settings,
                delegate() { return engine == null ? 0 : engine.PositionMs; },
                delegate() { return engine != null && engine.HasTrack && engine.State == PlayState.Playing; });
            lyricsForm.Hidden += delegate()
            {
                settings.LyricsEnabled = false;
                UpdateLyricsButton();
                settings.Save(configPath);
            };
            lyricsForm.LockChanged += delegate() { UpdateLyricsButton(); SetStatus(lyricsForm.Locked ? "歌词已锁定（鼠标穿透），按 Ctrl+L 或托盘菜单可关闭" : "歌词已解锁，可拖动"); };
            lyricsForm.PositionChanged += delegate() { settings.Save(configPath); };
        }

        void UpdateLyricsButton()
        {
            if (btnLyrics == null || btnLyrics.IsDisposed) return;
            bool on = settings.LyricsEnabled && lyricsForm != null && !lyricsForm.IsDisposed && lyricsForm.Visible;
            btnLyrics.IsAccent = on;
            btnLyrics.Text = on ? "歌词" : "歌词";
            btnLyrics.Invalidate();
            tips.SetToolTip(btnLyrics, on ? "关闭桌面歌词 (Ctrl+L)" : "显示桌面歌词 (Ctrl+L)");
        }

        void SyncLyrics(Track t)
        {
            if (!settings.LyricsEnabled) return;
            try
            {
                EnsureLyrics();
                lyricsForm.SetTrack(t);
                if (Visible && !lyricsForm.Visible) lyricsForm.Show();
                UpdateLyricsButton();
            }
            catch { }
        }

        void ToggleLyrics()
        {
            try
            {
                if (lyricsForm != null && !lyricsForm.IsDisposed && lyricsForm.Visible)
                {
                    lyricsForm.Hide();
                    settings.LyricsEnabled = false;
                    SetStatus("桌面歌词已关闭");
                }
                else
                {
                    EnsureLyrics();
                    settings.LyricsEnabled = true;
                    lyricsForm.ApplySettings();
                    lyricsForm.SetTrack(playlist.CurrentTrack);
                    lyricsForm.Show();
                    lyricsForm.BringToFront();
                    SetStatus("桌面歌词已开启：拖动可移动，双击可锁定（鼠标穿透）");
                }
                UpdateLyricsButton();
                settings.Save(configPath);
            }
            catch (Exception ex) { SetStatus("歌词窗口出错：" + ex.Message); }
        }

        void SaveState()
        {
            try
            {
                settings.WindowW = (int)Math.Round(Width * 96.0 / Sf);
                settings.WindowH = (int)Math.Round(Height * 96.0 / Sf);
                settings.WindowX = (WindowState == FormWindowState.Normal) ? Left : settings.WindowX;
                settings.WindowY = (WindowState == FormWindowState.Normal) ? Top : settings.WindowY;
                settings.SortColumn = sortColumn;
                settings.SortAsc = sortAsc;
                settings.PlayMode = playlist.Mode;
                if (settings.RememberState)
                {
                    settings.LastTrack = playlist.CurrentTrack != null ? playlist.CurrentTrack.Path : "";
                    settings.LastPositionMs = engine != null ? engine.PositionMs : 0;
                    int max = 5000;
                    int start = 0;
                    if (view.Count > max) start = Math.Max(0, playlist.Current - max / 2);
                    List<string> pl = new List<string>();
                    for (int i = start; i < view.Count && pl.Count < max; i++) pl.Add(view[i].Path);
                    settings.LastPlaylist = pl;
                }
                settings.Save(configPath);
                if (cache != null) cache.Save(cachePath);
            }
            catch { }
        }

        void RestoreState()
        {
            try
            {
                if (!settings.RememberState) return;
                if (settings.LastPlaylist.Count == 0) return;
                List<Track> batch = new List<Track>();
                bool useCache = settings.UseCache;
                settings.UseCache = true;            // 恢复列表时优先使用缓存，避免启动变慢
                foreach (string p in settings.LastPlaylist)
                {
                    if (batch.Count >= 3000) break;
                    if (!File.Exists(p)) continue;
                    Track t = Library.MakeTrack(p);
                    Library.FillTags(t, cache, settings);
                    batch.Add(t);
                }
                settings.UseCache = useCache;
                if (batch.Count == 0) return;
                allTracks.AddRange(batch);
                view = new List<Track>(batch);
                list.VirtualListSize = view.Count;
                SyncPlaylist();
                foreach (Track t in view)
                {
                    if (string.Equals(t.Path, settings.LastTrack, StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = view.IndexOf(t);
                        playlist.Current = idx;
                        list.PlayingIndex = idx;
                        UpdateNowPlayingUI(t);
                        break;
                    }
                }
                UpdateStatus();
                SetStatus(string.Format("已恢复上次的播放列表（{0} 首）", view.Count));
            }
            catch { }
        }

        void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void QuitApp()
        {
            allowClose = true;
            Close();
        }

        bool allowClose = false;

        void OnFormClosing(object s, FormClosingEventArgs e)
        {
            if (!allowClose && settings.MinimizeToTrayInstead && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                tray.ShowBalloonTip(2000, "轻音播放器", "已最小化到托盘，双击图标可以恢复", ToolTipIcon.Info);
                return;
            }
            SaveState();
            try { if (lyricsForm != null && !lyricsForm.IsDisposed) { lyricsForm.Close(); lyricsForm.Dispose(); } } catch { }
            try { engine.Stop(); engine.Dispose(); } catch { }
            if (timer != null) timer.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }
    }
}