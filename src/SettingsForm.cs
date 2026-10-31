using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MusicPlayer
{
    public class SettingsForm : Form
    {
        Settings s;
        TagCache cache;
        Theme T;
        TabControl tabs;
        ComboBox cboTheme, cboMode, cboEngine;
        CheckBox chkScanStart, chkRemember, chkCover, chkTray, chkCache, chkRecursive, chkStopAfter;
        CheckBox chkLyrics, chkLyricsLock;
        NumericUpDown numVolume, numSeek, numHistory, numSleep, numLyricsSize, numLyricsOpacity;
        ListBox lstFolders, lstExcludes;
        TextBox txtExt, txtEngineResult;
        FlatButton okBtn, cancelBtn, applyBtn;
        public bool RequestScan = false;
        public bool CacheCleared = false;

        int Sf = 96;
        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

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

        public SettingsForm(Settings settings, TagCache c)
        {
            s = settings;
            cache = c;
            T = Theme.Create(s.Theme);
            Sf = DetectDpi();

            Text = "设置";
            BackColor = T.Panel;
            ForeColor = T.Text;
            Font = T.BaseFont;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;

            tabs = new TabControl();
            tabs.Appearance = TabAppearance.Normal;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.DrawItem += OnDrawTab;
            Controls.Add(tabs);

            okBtn = MakeButton("确定", 84, true);
            okBtn.Click += delegate(object o, EventArgs e) { Collect(); DialogResult = DialogResult.OK; Close(); };
            cancelBtn = MakeButton("取消", 84, false);
            cancelBtn.Click += delegate(object o, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); };
            applyBtn = MakeButton("应用", 84, false);
            applyBtn.Click += delegate(object o, EventArgs e) { Collect(); };
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);
            Controls.Add(applyBtn);

            Resize += delegate(object o, EventArgs e) { LayoutChrome(); };
            RebuildAll();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int d = DetectDpi();
            if (d != Sf) RebuildAll();
        }

        void RebuildAll()
        {
            Sf = DetectDpi();
            if (Sf <= 0) Sf = 96;
            Size = new Size(S(660), S(620));
            MinimumSize = new Size(S(620), S(560));
            tabs.ItemSize = new Size(S(104), S(32));
            tabs.Location = new Point(S(12), S(12));
            tabs.Size = new Size(S(636), S(545));

            ComboBox keepTheme = cboTheme, keepMode = cboMode, keepEngine = cboEngine;
            int themeSel = keepTheme != null ? keepTheme.SelectedIndex : (s.Theme == "light" ? 1 : 0);
            int modeSel = keepMode != null ? keepMode.SelectedIndex : ModeIndex(s.PlayMode);
            int engineSel = keepEngine != null ? keepEngine.SelectedIndex : EngineIndex(s.Engine);
            bool syncFromUi = keepTheme != null;

            tabs.TabPages.Clear();
            tabs.TabPages.Add(BuildGeneral());
            tabs.TabPages.Add(BuildPlay());
            tabs.TabPages.Add(BuildFolders());
            tabs.TabPages.Add(BuildLyricsPage());
            tabs.TabPages.Add(BuildEngine());
            tabs.TabPages.Add(BuildAbout());
            if (cboTheme != null && themeSel >= 0 && themeSel < cboTheme.Items.Count) cboTheme.SelectedIndex = themeSel;
            if (cboMode != null && modeSel >= 0 && modeSel < cboMode.Items.Count) cboMode.SelectedIndex = modeSel;
            if (cboEngine != null && engineSel >= 0 && engineSel < cboEngine.Items.Count) cboEngine.SelectedIndex = engineSel;
            LayoutChrome();
        }

        void LayoutChrome()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            tabs.Size = new Size(Math.Max(S(400), w - S(24)), Math.Max(S(360), h - S(24) - S(48)));
            int by = h - S(32) - S(12);
            okBtn.Location = new Point(w - okBtn.Width - S(12), by);
            cancelBtn.Location = new Point(okBtn.Left - cancelBtn.Width - S(8), by);
            applyBtn.Location = new Point(cancelBtn.Left - applyBtn.Width - S(8), by);
        }

        FlatButton MakeButton(string text, int w, bool accent)
        {
            FlatButton b = new FlatButton(T);
            b.Text = text;
            b.Width = S(w);
            b.Height = S(32);
            b.IsAccent = accent;
            return b;
        }

        void OnDrawTab(object sender, DrawItemEventArgs e)
        {
            bool sel = (e.Index == tabs.SelectedIndex);
            Rectangle r = tabs.GetTabRect(e.Index);
            using (SolidBrush b = new SolidBrush(sel ? T.PanelAlt : T.Panel))
                e.Graphics.FillRectangle(b, r);
            using (SolidBrush b = new SolidBrush(sel ? T.Text : T.SubText))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(tabs.TabPages[e.Index].Text, T.BaseFont, b, r, sf);
            }
        }

        TabPage NewPage(string title)
        {
            TabPage p = new TabPage(title);
            p.BackColor = T.Panel;
            p.ForeColor = T.Text;
            return p;
        }

        Label AddLabel(Control parent, string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(S(x), S(y));
            l.Size = new Size(S(w), S(24));
            l.ForeColor = T.Text;
            l.BackColor = T.Panel;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.AutoEllipsis = true;
            parent.Controls.Add(l);
            return l;
        }

        Label AddHint(Control parent, string text, int x, int y, int w, int h)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(S(x), S(y));
            l.Size = new Size(S(w), S(h));
            l.ForeColor = T.SubText;
            l.BackColor = T.Panel;
            parent.Controls.Add(l);
            return l;
        }

        CheckBox AddCheck(Control parent, string text, int x, int y, int w, bool val)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Location = new Point(S(x), S(y));
            c.Size = new Size(S(w), S(26));
            c.Checked = val;
            c.ForeColor = T.Text;
            c.BackColor = T.Panel;
            c.FlatStyle = FlatStyle.Flat;
            c.AutoEllipsis = true;
            parent.Controls.Add(c);
            return c;
        }

        ComboBox AddCombo(Control parent, int x, int y, int w, string[] items, int sel)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(S(x), S(y));
            c.Size = new Size(S(w), S(26));
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = T.PanelAlt;
            c.ForeColor = T.Text;
            c.DropDownWidth = S(w + 120);
            foreach (string i in items) c.Items.Add(i);
            if (sel >= 0 && sel < c.Items.Count) c.SelectedIndex = sel;
            parent.Controls.Add(c);
            return c;
        }

        NumericUpDown AddNum(Control parent, int x, int y, int w, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(S(x), S(y));
            n.Size = new Size(S(w), S(26));
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, val));
            n.BackColor = T.PanelAlt;
            n.ForeColor = T.Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(n);
            return n;
        }

        ListBox AddList(Control parent, int x, int y, int w, int h)
        {
            ListBox l = new ListBox();
            l.Location = new Point(S(x), S(y));
            l.Size = new Size(S(w), S(h));
            l.BackColor = T.PanelAlt;
            l.ForeColor = T.Text;
            l.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(l);
            return l;
        }

        TextBox AddText(Control parent, int x, int y, int w, string val)
        {
            TextBox t = new TextBox();
            t.Location = new Point(S(x), S(y));
            t.Size = new Size(S(w), S(26));
            t.Text = val;
            t.BackColor = T.PanelAlt;
            t.ForeColor = T.Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(t);
            return t;
        }

        FlatButton AddPageButton(Control parent, string text, int x, int y, int w, bool accent)
        {
            FlatButton b = MakeButton(text, w, accent);
            b.Location = new Point(S(x), S(y));
            parent.Controls.Add(b);
            return b;
        }

        // ---------------- 常规 ----------------
        TabPage BuildGeneral()
        {
            TabPage p = NewPage("常规");
            AddLabel(p, "界面主题", 16, 18, 120);
            cboTheme = AddCombo(p, 150, 16, 170, new string[] { "深色（推荐）", "浅色" }, s.Theme == "light" ? 1 : 0);
            chkScanStart = AddCheck(p, "启动程序时自动扫描音乐文件夹", 16, 54, 570, s.ScanOnStart);
            chkRemember = AddCheck(p, "记住播放列表与上次播放位置", 16, 82, 570, s.RememberState);
            chkCover = AddCheck(p, "显示歌曲封面（按需读取，不影响扫描速度）", 16, 110, 570, s.ShowCover);
            chkTray = AddCheck(p, "点击关闭按钮时最小化到系统托盘", 16, 138, 570, s.MinimizeToTrayInstead);
            chkCache = AddCheck(p, "使用扫描缓存（第二次扫描明显更快）", 16, 166, 570, s.UseCache);
            AddHint(p, "提示：扫描时只读取每个音乐文件的标签信息（几 KB），不会把整首音乐读入内存；播放时才按需读取。", 16, 202, 580, 48);
            AddLabel(p, "界面字体", 16, 258, 120);
            AddHint(p, "主题修改在重新打开程序后完全生效。", 150, 258, 440, 26);
            return p;
        }

        // ---------------- 播放 ----------------
        TabPage BuildPlay()
        {
            TabPage p = NewPage("播放");
            AddLabel(p, "默认播放模式", 16, 18, 130);
            cboMode = AddCombo(p, 160, 16, 180, new string[] { "列表循环", "顺序播放", "单曲循环", "随机播放" }, ModeIndex(s.PlayMode));
            AddLabel(p, "默认音量", 16, 52, 130);
            numVolume = AddNum(p, 160, 50, 80, 0, 100, s.Volume);
            AddHint(p, "0 - 100", 252, 52, 90, 26);
            AddLabel(p, "快进 / 快退步长", 16, 86, 130);
            numSeek = AddNum(p, 160, 84, 80, 1, 60, s.SeekStep);
            AddHint(p, "秒（方向键左右操作）", 252, 86, 200, 26);
            AddLabel(p, "随机不重复深度", 16, 120, 130);
            numHistory = AddNum(p, 160, 118, 80, 2, 500, s.HistoryLength);
            AddHint(p, "首，随机模式下避免短时间内重播", 252, 120, 330, 26);
            chkStopAfter = AddCheck(p, "当前歌曲播放完就停止（只播这一首）", 16, 156, 560, s.StopAfterCurrent);
            AddLabel(p, "定时停止", 16, 194, 130);
            numSleep = AddNum(p, 160, 192, 80, 0, 600, s.SleepTimerMinutes);
            AddHint(p, "分钟后自动停止；0 表示关闭", 252, 194, 300, 26);
            AddHint(p, "快捷键：空格 播放/暂停，Ctrl+方向键 切换歌曲，Ctrl+上下 音量，Delete 从列表移除。", 16, 240, 580, 48);
            return p;
        }

        static int ModeIndex(string mode)
        {
            if (mode == "sequential") return 1;
            if (mode == "repeatOne") return 2;
            if (mode == "shuffle") return 3;
            return 0;
        }

        static string ModeFromIndex(int i)
        {
            if (i == 1) return "sequential";
            if (i == 2) return "repeatOne";
            if (i == 3) return "shuffle";
            return "repeatAll";
        }

        // ---------------- 目录 ----------------
        TabPage BuildFolders()
        {
            TabPage p = NewPage("目录");
            AddLabel(p, "音乐文件夹（会自动扫描其中的音乐）", 16, 10, 560);
            lstFolders = AddList(p, 16, 36, 560, 108);
            foreach (string f in s.Folders) lstFolders.Items.Add(f);

            FlatButton add = AddPageButton(p, "添加目录", 16, 152, 92, false);
            add.Click += delegate(object o, EventArgs e)
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                if (d.ShowDialog(this) == DialogResult.OK)
                    if (!lstFolders.Items.Contains(d.SelectedPath)) lstFolders.Items.Add(d.SelectedPath);
            };
            FlatButton del = AddPageButton(p, "移除选中", 116, 152, 92, false);
            del.Click += delegate(object o, EventArgs e)
            {
                while (lstFolders.SelectedIndices.Count > 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndices[0]);
            };
            FlatButton scan = AddPageButton(p, "保存并立即扫描", 216, 152, 140, true);
            scan.Click += delegate(object o, EventArgs e)
            {
                Collect();
                RequestScan = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            chkRecursive = AddCheck(p, "包含子文件夹（递归扫描）", 16, 192, 560, s.Recursive);

            AddLabel(p, "排除的文件夹名（每行一个，可留空）", 16, 222, 420);
            lstExcludes = AddList(p, 16, 248, 380, 76);
            foreach (string f in s.ExcludeDirs) lstExcludes.Items.Add(f);
            FlatButton addEx = AddPageButton(p, "添加", 410, 248, 76, false);
            addEx.Click += delegate(object o, EventArgs e)
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    string name = Path.GetFileName(d.SelectedPath);
                    if (name != "" && !lstExcludes.Items.Contains(name)) lstExcludes.Items.Add(name);
                }
            };
            FlatButton delEx = AddPageButton(p, "移除", 410, 286, 76, false);
            delEx.Click += delegate(object o, EventArgs e)
            {
                while (lstExcludes.SelectedIndices.Count > 0) lstExcludes.Items.RemoveAt(lstExcludes.SelectedIndices[0]);
            };

            AddLabel(p, "识别为音乐的扩展名", 16, 334, 300);
            txtExt = AddText(p, 16, 358, 570, string.Join(", ", s.Extensions.ToArray()));
            AddHint(p, "用逗号分隔，例如 .mp3, .flac, .wav；留空表示使用默认列表。", 16, 390, 580, 48);
            return p;
        }

        // ---------------- 桌面歌词 ----------------
        TabPage BuildLyricsPage()
        {
            TabPage p = NewPage("桌面歌词");
            chkLyrics = AddCheck(p, "显示桌面歌词（浮在其他窗口上方）", 16, 18, 560, s.LyricsEnabled);
            AddLabel(p, "歌词字号", 16, 56, 130);
            numLyricsSize = AddNum(p, 160, 54, 80, 14, 64, s.LyricsFontSize);
            AddHint(p, "14 - 64", 252, 56, 120, 26);
            AddLabel(p, "背景不透明度", 16, 90, 130);
            numLyricsOpacity = AddNum(p, 160, 88, 80, 25, 100, s.LyricsOpacity);
            AddHint(p, "25 - 100（数值越大越不透明）", 252, 90, 320, 26);
            chkLyricsLock = AddCheck(p, "锁定歌词（鼠标穿透，不挡住下面的操作）", 16, 124, 560, s.LyricsLocked);

            FlatButton reset = AddPageButton(p, "复位歌词位置", 130, 158, 130, false);
            reset.Click += delegate(object o, EventArgs e)
            {
                s.LyricsX = -1;
                s.LyricsY = -1;
                MessageBox.Show(this, "位置已复位，确定后歌词窗口会回到屏幕下方居中。", "提示");
            };

            AddHint(p, "用法：直接拖动歌词窗口可移动；双击歌词窗口可锁定/解锁（锁定时鼠标会穿透，不会挡住下面的操作）；右键歌词窗口可调字号、隐藏。", 16, 202, 580, 48);
            AddHint(p, "歌词来源：优先读取歌曲旁边的同名 .lrc 文件（支持 UTF-8 / GBK、双语对照行），没有 .lrc 时尝试读取音频内嵌歌词。", 16, 258, 580, 48);
            return p;
        }

        // ---------------- 引擎 ----------------
        TabPage BuildEngine()
        {
            TabPage p = NewPage("音频引擎");
            AddLabel(p, "播放引擎", 16, 18, 90);
            cboEngine = AddCombo(p, 110, 16, 300,
                new string[] { "自动选择（推荐）", "Windows 媒体引擎（Media Foundation）", "MCI 兼容模式" }, EngineIndex(s.Engine));
            AddHint(p, "自动选择：优先使用 Windows 媒体引擎（支持 MP3 / FLAC / M4A / AAC / WMA / WAV 等），失败时自动退回 MCI。若某首歌播放异常，可手动指定引擎。", 16, 52, 580, 56);

            FlatButton test = AddPageButton(p, "检测本机可用引擎", 16, 116, 160, true);
            test.Click += delegate(object o, EventArgs e)
            {
                txtEngineResult.Text = "正在检测…";
                Application.DoEvents();
                txtEngineResult.Text = AudioSelfTest.Run(s);
            };

            txtEngineResult = new TextBox();
            txtEngineResult.Multiline = true;
            txtEngineResult.ReadOnly = true;
            txtEngineResult.ScrollBars = ScrollBars.Vertical;
            txtEngineResult.Location = new Point(S(16), S(156));
            txtEngineResult.Size = new Size(S(580), S(170));
            txtEngineResult.BackColor = T.PanelAlt;
            txtEngineResult.ForeColor = T.Text;
            txtEngineResult.BorderStyle = BorderStyle.FixedSingle;
            txtEngineResult.Font = T.SmallFont;
            p.Controls.Add(txtEngineResult);

            FlatButton clearCache = AddPageButton(p, "清除扫描缓存", 16, 338, 130, false);
            clearCache.Click += delegate(object o, EventArgs e)
            {
                if (cache != null) cache.Clear();
                CacheCleared = true;
                MessageBox.Show(this, "缓存已清除，下次扫描会重新读取全部标签。", "提示");
            };
            AddHint(p, "缓存文件：MusicPlayer.cache.json（与程序放在同一文件夹）", 156, 340, 440, 26);
            return p;
        }

        static int EngineIndex(string e)
        {
            if (e == "winrt") return 1;
            if (e == "mci") return 2;
            return 0;
        }

        static string EngineFromIndex(int i)
        {
            if (i == 1) return "winrt";
            if (i == 2) return "mci";
            return "auto";
        }

        // ---------------- 关于 ----------------
        TabPage BuildAbout()
        {
            TabPage p = NewPage("关于");
            Label l = new Label();
            l.Location = new Point(S(16), S(14));
            l.Size = new Size(S(580), S(440));
            l.ForeColor = T.Text;
            l.BackColor = T.Panel;
            l.Text =
                "轻音播放器  Light Music Player  1.0" + Environment.NewLine +
                Environment.NewLine +
                "一个不需要安装、双击即用的本地音乐播放器。" + Environment.NewLine +
                "· 读取整个文件夹的音乐信息（只读标签，不加载整首音频）" + Environment.NewLine +
                "· 边播边读：播放时按需读取当前歌曲，不预先加载全部音乐" + Environment.NewLine +
                "· 支持 顺序 / 列表循环 / 单曲循环 / 随机 播放" + Environment.NewLine +
                "· 桌面歌词：逐字卡拉 OK 高亮、双语对照、下一句预告" + Environment.NewLine +
                "· 支持 MP3、WAV、WMA、FLAC、M4A、AAC、OGG、Opus、AIFF、APE 等格式" + Environment.NewLine +
                "· 支持 .m3u 播放列表导入导出、封面显示、托盘运行、定时停止" + Environment.NewLine +
                Environment.NewLine +
                "快捷键：" + Environment.NewLine +
                "  空格        播放 / 暂停" + Environment.NewLine +
                "  Ctrl+右/左  下一首 / 上一首" + Environment.NewLine +
                "  右 / 左     快进 / 快退（默认 5 秒）" + Environment.NewLine +
                "  Ctrl+上/下  音量增减" + Environment.NewLine +
                "  Ctrl+L      开关桌面歌词" + Environment.NewLine +
                "  Ctrl+M      切换播放模式" + Environment.NewLine +
                "  Ctrl+O      添加文件夹" + Environment.NewLine +
                "  Ctrl+F      搜索" + Environment.NewLine +
                "  Delete      从列表移除" + Environment.NewLine +
                "  回车        播放选中歌曲";
            p.Controls.Add(l);
            return p;
        }

        // ---------------- 收集 ----------------
        void Collect()
        {
            s.Theme = (cboTheme.SelectedIndex == 1) ? "light" : "dark";
            s.ScanOnStart = chkScanStart.Checked;
            s.RememberState = chkRemember.Checked;
            s.ShowCover = chkCover.Checked;
            s.MinimizeToTrayInstead = chkTray.Checked;
            s.UseCache = chkCache.Checked;

            s.PlayMode = ModeFromIndex(cboMode.SelectedIndex);
            s.Volume = (int)numVolume.Value;
            s.SeekStep = (int)numSeek.Value;
            s.HistoryLength = (int)numHistory.Value;
            s.StopAfterCurrent = chkStopAfter.Checked;
            s.SleepTimerMinutes = (int)numSleep.Value;

            s.LyricsEnabled = chkLyrics.Checked;
            s.LyricsFontSize = (int)numLyricsSize.Value;
            s.LyricsOpacity = (int)numLyricsOpacity.Value;
            s.LyricsLocked = chkLyricsLock.Checked;

            s.Engine = EngineFromIndex(cboEngine.SelectedIndex);

            s.Recursive = chkRecursive.Checked;
            s.Folders = new List<string>();
            foreach (object o in lstFolders.Items) s.Folders.Add(Convert.ToString(o));
            s.ExcludeDirs = new List<string>();
            foreach (object o in lstExcludes.Items) s.ExcludeDirs.Add(Convert.ToString(o));

            List<string> ext = new List<string>();
            foreach (string part in txtExt.Text.Split(','))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                if (!p.StartsWith(".")) p = "." + p;
                if (!ext.Contains(p)) ext.Add(p);
            }
            if (ext.Count > 0) s.Extensions = ext;

            s.Save(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "MusicPlayer.config.json"));
        }
    }
}