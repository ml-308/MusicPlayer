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
        NumericUpDown numVolume, numSeek, numHistory, numSleep;
        ListBox lstFolders, lstExcludes;
        TextBox txtExt, txtEngineResult;
        CheckBox chkLyrics, chkLyricsLock;
        NumericUpDown numLyricsSize, numLyricsOpacity;
        public bool RequestScan = false;
        public bool CacheCleared = false;

        public SettingsForm(Settings settings, TagCache c)
        {
            s = settings;
            cache = c;
            T = Theme.Create(s.Theme);
            Text = "设置";
            Size = new Size(560, 520);
            MinimumSize = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = T.Panel;
            ForeColor = T.Text;
            Font = T.BaseFont;
            AutoScaleMode = AutoScaleMode.None;

            tabs = new TabControl();
            tabs.Location = new Point(12, 12);
            tabs.Size = new Size(520, 400);
            tabs.Appearance = TabAppearance.Normal;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(96, 30);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.DrawItem += OnDrawTab;
            tabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            tabs.TabPages.Add(BuildGeneral());
            tabs.TabPages.Add(BuildPlay());
            tabs.TabPages.Add(BuildFolders());
            tabs.TabPages.Add(BuildLyricsPage());
            tabs.TabPages.Add(BuildEngine());
            tabs.TabPages.Add(BuildAbout());
            Controls.Add(tabs);

            int by = 428;
            FlatButton ok = MakeButton("确定", 84, true);
            ok.Location = new Point(520 - 84 - 8, by);
            ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ok.Click += delegate(object o, EventArgs e) { Collect(); DialogResult = DialogResult.OK; Close(); };

            FlatButton cancel = MakeButton("取消", 84, false);
            cancel.Location = new Point(520 - 84 - 8 - 92, by);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.Click += delegate(object o, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); };

            FlatButton apply = MakeButton("应用", 84, false);
            apply.Location = new Point(520 - 84 - 8 - 184, by);
            apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            apply.Click += delegate(object o, EventArgs e) { Collect(); };

            Controls.Add(ok);
            Controls.Add(cancel);
            Controls.Add(apply);
            Resize += delegate(object o, EventArgs e)
            {
                tabs.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 24 - 48);
                ok.Location = new Point(ClientSize.Width - ok.Width - 12, ClientSize.Height - ok.Height - 12);
                cancel.Location = new Point(ok.Left - cancel.Width - 8, ok.Top);
                apply.Location = new Point(cancel.Left - apply.Width - 8, ok.Top);
            };
        }

        FlatButton MakeButton(string text, int w, bool accent)
        {
            FlatButton b = new FlatButton(T);
            b.Text = text;
            b.Width = w;
            b.Height = 32;
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
            l.Location = new Point(x, y);
            l.Size = new Size(w, 22);
            l.ForeColor = T.Text;
            l.BackColor = T.Panel;
            l.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(l);
            return l;
        }

        void AddHint(Control parent, string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 40);
            l.ForeColor = T.SubText;
            l.BackColor = T.Panel;
            parent.Controls.Add(l);
        }

        CheckBox AddCheck(Control parent, string text, int x, int y, int w, bool val)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Location = new Point(x, y);
            c.Size = new Size(w, 24);
            c.Checked = val;
            c.ForeColor = T.Text;
            c.BackColor = T.Panel;
            c.FlatStyle = FlatStyle.Flat;
            parent.Controls.Add(c);
            return c;
        }

        ComboBox AddCombo(Control parent, int x, int y, int w, string[] items, int sel)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(x, y);
            c.Size = new Size(w, 24);
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = T.PanelAlt;
            c.ForeColor = T.Text;
            foreach (string i in items) c.Items.Add(i);
            if (sel >= 0 && sel < c.Items.Count) c.SelectedIndex = sel;
            parent.Controls.Add(c);
            return c;
        }

        NumericUpDown AddNum(Control parent, int x, int y, int w, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Size = new Size(w, 24);
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
            l.Location = new Point(x, y);
            l.Size = new Size(w, h);
            l.BackColor = T.PanelAlt;
            l.ForeColor = T.Text;
            l.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(l);
            return l;
        }

        TextBox AddText(Control parent, int x, int y, int w, string val)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, 24);
            t.Text = val;
            t.BackColor = T.PanelAlt;
            t.ForeColor = T.Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(t);
            return t;
        }

        // ---------------- 常规 ----------------
        TabPage BuildGeneral()
        {
            TabPage p = NewPage("常规");
            AddLabel(p, "界面主题", 16, 18, 120);
            cboTheme = AddCombo(p, 150, 16, 160, new string[] { "深色（推荐）", "浅色" }, s.Theme == "light" ? 1 : 0);
            chkScanStart = AddCheck(p, "启动程序时自动扫描音乐文件夹", 16, 54, 380, s.ScanOnStart);
            chkRemember = AddCheck(p, "记住播放列表与上次播放位置", 16, 82, 380, s.RememberState);
            chkCover = AddCheck(p, "显示歌曲封面（按需读取，不影响扫描速度）", 16, 110, 420, s.ShowCover);
            chkTray = AddCheck(p, "点击关闭按钮时最小化到系统托盘", 16, 138, 420, s.MinimizeToTrayInstead);
            chkCache = AddCheck(p, "使用扫描缓存（第二次扫描明显更快）", 16, 166, 420, s.UseCache);
            AddHint(p, "提示：扫描时只读取每个音乐文件的标签信息（几 KB），不会把整首音乐读入内存；播放时才会按需读取。", 16, 200, 440);
            AddLabel(p, "界面字体", 16, 250, 120);
            AddHint(p, "主题修改在重新打开程序后完全生效。", 150, 250, 300);
            return p;
        }

        // ---------------- 播放 ----------------
        TabPage BuildPlay()
        {
            TabPage p = NewPage("播放");
            AddLabel(p, "默认播放模式", 16, 18, 130);
            cboMode = AddCombo(p, 160, 16, 170, new string[] { "列表循环", "顺序播放", "单曲循环", "随机播放" }, ModeIndex(s.PlayMode));
            AddLabel(p, "默认音量", 16, 52, 130);
            numVolume = AddNum(p, 160, 50, 80, 0, 100, s.Volume);
            AddHint(p, "0 - 100", 248, 54, 80);
            AddLabel(p, "快进 / 快退步长", 16, 86, 130);
            numSeek = AddNum(p, 160, 84, 80, 1, 60, s.SeekStep);
            AddHint(p, "秒（方向键 ← → 操作）", 248, 88, 180);
            AddLabel(p, "随机播放不重复深度", 16, 120, 130);
            numHistory = AddNum(p, 160, 118, 80, 2, 500, s.HistoryLength);
            AddHint(p, "首，随机模式下尽量避免短时间内重播", 248, 122, 220);
            chkStopAfter = AddCheck(p, "当前歌曲播放完就停止（播完这一首）", 16, 156, 400, s.StopAfterCurrent);
            AddLabel(p, "定时停止", 16, 194, 130);
            numSleep = AddNum(p, 160, 192, 80, 0, 600, s.SleepTimerMinutes);
            AddHint(p, "分钟后自动停止播放；0 表示关闭", 248, 196, 240);
            AddHint(p, "快捷键：空格 播放/暂停，Ctrl+方向键 切换歌曲，Ctrl+↑/↓ 音量，Delete 从列表移除。", 16, 240, 440);
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
            AddLabel(p, "音乐文件夹", 16, 12, 200);
            lstFolders = AddList(p, 16, 36, 460, 108);
            foreach (string f in s.Folders) lstFolders.Items.Add(f);

            FlatButton add = MakeButton("添加目录", 92, false);
            add.Location = new Point(16, 150); add.Width = 92; add.Height = 30;
            add.Click += delegate(object o, EventArgs e)
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                if (d.ShowDialog(this) == DialogResult.OK)
                    if (!lstFolders.Items.Contains(d.SelectedPath)) lstFolders.Items.Add(d.SelectedPath);
            };
            FlatButton del = MakeButton("移除选中", 92, false);
            del.Location = new Point(116, 150); del.Width = 92; del.Height = 30;
            del.Click += delegate(object o, EventArgs e)
            {
                while (lstFolders.SelectedIndices.Count > 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndices[0]);
            };
            FlatButton scan = MakeButton("保存并立即扫描", 130, true);
            scan.Location = new Point(216, 150); scan.Width = 130; scan.Height = 30;
            scan.Click += delegate(object o, EventArgs e)
            {
                Collect();
                RequestScan = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            p.Controls.Add(add); p.Controls.Add(del); p.Controls.Add(scan);

            chkRecursive = AddCheck(p, "包含子文件夹（递归扫描）", 16, 190, 380, s.Recursive);

            AddLabel(p, "排除的文件夹名（每行一个，可留空）", 16, 220, 380);
            lstExcludes = AddList(p, 16, 244, 300, 76);
            foreach (string f in s.ExcludeDirs) lstExcludes.Items.Add(f);
            FlatButton addEx = MakeButton("添加", 70, false);
            addEx.Location = new Point(324, 244); addEx.Width = 70; addEx.Height = 30;
            addEx.Click += delegate(object o, EventArgs e)
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    string name = Path.GetFileName(d.SelectedPath);
                    if (name != "" && !lstExcludes.Items.Contains(name)) lstExcludes.Items.Add(name);
                }
            };
            FlatButton delEx = MakeButton("移除", 70, false);
            delEx.Location = new Point(324, 280); delEx.Width = 70; delEx.Height = 30;
            delEx.Click += delegate(object o, EventArgs e)
            {
                while (lstExcludes.SelectedIndices.Count > 0) lstExcludes.Items.RemoveAt(lstExcludes.SelectedIndices[0]);
            };
            p.Controls.Add(addEx); p.Controls.Add(delEx);

            AddLabel(p, "识别为音乐的扩展名", 16, 328, 200);
            txtExt = AddText(p, 16, 352, 460, string.Join(", ", s.Extensions.ToArray()));
            AddHint(p, "用逗号分隔，例如 .mp3, .flac, .wav", 16, 378, 400);
            return p;
        }

        // ---------------- 桌面歌词 ----------------
        TabPage BuildLyricsPage()
        {
            TabPage p = NewPage("桌面歌词");
            chkLyrics = AddCheck(p, "显示桌面歌词（浮在其他窗口上方）", 16, 18, 420, s.LyricsEnabled);
            AddLabel(p, "歌词字号", 16, 56, 130);
            numLyricsSize = AddNum(p, 160, 54, 80, 14, 64, s.LyricsFontSize);
            AddHint(p, "14 - 64", 248, 58, 80);
            AddLabel(p, "背景不透明度", 16, 90, 130);
            numLyricsOpacity = AddNum(p, 160, 88, 80, 25, 100, s.LyricsOpacity);
            AddHint(p, "25 - 100（数值越大越不透明）", 248, 92, 240);
            chkLyricsLock = AddCheck(p, "锁定歌词（鼠标穿透，不会挡住点击）", 16, 124, 420, s.LyricsLocked);

            FlatButton reset = MakeButton("复位歌词位置", 120, false);
            reset.Location = new Point(16, 158);
            reset.Width = 120;
            reset.Height = 30;
            reset.Click += delegate(object o, EventArgs e)
            {
                s.LyricsX = -1;
                s.LyricsY = -1;
                MessageBox.Show(this, "位置已复位，确定后歌词窗口会回到屏幕下方居中。", "提示");
            };
            p.Controls.Add(reset);

            AddHint(p, "用法：直接拖动歌词窗口可移动；双击歌词窗口可锁定/解锁（锁定时鼠标会穿透，不会挡住下面的操作）；" +
                       "右侧菜单（右键）里也能调字号、隐藏歌词。", 16, 200, 440);
            AddHint(p, "歌词来源：优先读取歌曲旁边的同名 .lrc 文件（支持 UTF-8 / GBK 编码、双行翻译），" +
                       "没有 .lrc 时会尝试读取音频文件内嵌的歌词。", 16, 268, 440);
            return p;
        }

        // ---------------- 引擎 ----------------
        TabPage BuildEngine()
        {
            TabPage p = NewPage("音频引擎");
            AddLabel(p, "播放引擎", 16, 18, 130);
            cboEngine = AddCombo(p, 160, 16, 180,
                new string[] { "自动选择（推荐）", "Windows 媒体引擎（Media Foundation）", "MCI 兼容模式" }, EngineIndex(s.Engine));
            AddHint(p, "自动选择：优先使用 Windows 媒体引擎（支持 MP3 / FLAC / M4A / AAC / WMA / WAV 等），失败时自动退回 MCI。若某首歌播放异常，可手动切换引擎。", 16, 50, 460);

            FlatButton test = MakeButton("检测本机可用引擎", 150, true);
            test.Location = new Point(16, 108); test.Width = 150; test.Height = 32;
            test.Click += delegate(object o, EventArgs e)
            {
                txtEngineResult.Text = "正在检测…";
                Application.DoEvents();
                txtEngineResult.Text = AudioSelfTest.Run(s);
            };
            p.Controls.Add(test);

            txtEngineResult = new TextBox();
            txtEngineResult.Multiline = true;
            txtEngineResult.ReadOnly = true;
            txtEngineResult.ScrollBars = ScrollBars.Vertical;
            txtEngineResult.Location = new Point(16, 150);
            txtEngineResult.Size = new Size(460, 160);
            txtEngineResult.BackColor = T.PanelAlt;
            txtEngineResult.ForeColor = T.Text;
            txtEngineResult.BorderStyle = BorderStyle.FixedSingle;
            txtEngineResult.Font = T.SmallFont;
            p.Controls.Add(txtEngineResult);

            FlatButton clearCache = MakeButton("清除扫描缓存", 130, false);
            clearCache.Location = new Point(16, 320); clearCache.Width = 130; clearCache.Height = 30;
            clearCache.Click += delegate(object o, EventArgs e)
            {
                if (cache != null) cache.Clear();
                CacheCleared = true;
                MessageBox.Show(this, "缓存已清除，下次扫描会重新读取全部标签。", "提示");
            };
            p.Controls.Add(clearCache);
            AddHint(p, "缓存文件：MusicPlayer.cache.json（与程序放在同一文件夹）", 160, 325, 320);
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
            l.Location = new Point(16, 14);
            l.Size = new Size(460, 340);
            l.ForeColor = T.Text;
            l.BackColor = T.Panel;
            l.Text =
                "轻音播放器  Light Music Player  1.0\r\n" +
                "\r\n" +
                "一个不需要安装、双击即用的本地音乐播放器。\r\n" +
                "· 读取整个文件夹的音乐信息（只读标签，不加载整首音频）\r\n" +
                "· 边播边读：播放时按需读取当前歌曲，不预先加载全部音乐\r\n" +
                "· 支持 顺序 / 列表循环 / 单曲循环 / 随机 播放\r\n" +
                "· 支持 MP3、WAV、WMA、FLAC、M4A、AAC、OGG、Opus、AIFF、APE 等格式\r\n" +
                "· 支持 .m3u 播放列表导入导出、封面显示、托盘运行、定时停止\r\n" +
                "\r\n" +
                "快捷键：\r\n" +
                "  空格        播放 / 暂停\r\n" +
                "  Ctrl+→ / ←  下一首 / 上一首\r\n" +
                "  → / ←       快进 / 快退 5 秒\r\n" +
                "  Ctrl+↑ / ↓  音量增减\r\n" +
                "  Ctrl+M      切换播放模式\r\n" +
                "  Ctrl+O      添加文件夹\r\n" +
                "  Ctrl+F      搜索\r\n" +
                "  Delete      从列表移除\r\n" +
                "  回车        播放选中歌曲\r\n";
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

            s.LyricsEnabled = chkLyrics.Checked;
            s.LyricsFontSize = (int)numLyricsSize.Value;
            s.LyricsOpacity = (int)numLyricsOpacity.Value;
            s.LyricsLocked = chkLyricsLock.Checked;

            s.Engine = EngineFromIndex(cboEngine.SelectedIndex);
            s.Save(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "MusicPlayer.config.json"));
        }
    }
}