using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MusicPlayerSetup
{
    public class SetupForm : Form
    {
        TextBox txtDir;
        CheckBox chkDesktop, chkStartMenu, chkLaunch;
        Button btnInstall, btnCancel, btnBrowse;
        ProgressBar bar;
        Label lblStatus, lblTitle, lblDir, lblTip, lblVer;
        public int ExitCode = 1;
        bool busy;
        int Sf = 96;

        int S(int v) { return (int)Math.Round(v * (Sf / 96.0)); }

        public SetupForm(string dir)
        {
            Sf = DetectDpi();
            Text = Installer.AppName + " 安装向导";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 31, 34);
            ForeColor = Color.FromArgb(232, 234, 237);
            Font = ThemeFont();
            AutoScaleMode = AutoScaleMode.None;

            lblTitle = MakeLabel(Installer.AppName + "  安装向导", 18, 16, 460, FontStyle.Bold, Color.FromArgb(255, 255, 255));
            lblVer = MakeLabel("版本 " + Installer.Version + "   ·   免安装绿色程序的安装版", 18, 46, 460, FontStyle.Regular, Color.FromArgb(154, 160, 166));

            lblDir = MakeLabel("安装位置：", 18, 86, 100, FontStyle.Regular, Color.FromArgb(200, 205, 212));
            txtDir = new TextBox();
            txtDir.Text = dir;
            txtDir.BackColor = Color.FromArgb(49, 51, 56);
            txtDir.ForeColor = Color.FromArgb(232, 234, 237);
            txtDir.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(txtDir);

            btnBrowse = MakeButton("浏览…", 84, false);
            btnBrowse.Click += delegate(object o, EventArgs e)
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                d.Description = "选择安装位置";
                d.SelectedPath = txtDir.Text;
                if (d.ShowDialog(this) == DialogResult.OK) txtDir.Text = d.SelectedPath;
            };
            Controls.Add(btnBrowse);

            chkDesktop = MakeCheck("创建桌面快捷方式", true);
            chkStartMenu = MakeCheck("创建开始菜单快捷方式", true);
            chkLaunch = MakeCheck("安装完成后启动播放器", true);

            bar = new ProgressBar();
            bar.Minimum = 0; bar.Maximum = 100; bar.Value = 0;
            bar.Style = ProgressBarStyle.Continuous;
            Controls.Add(bar);

            lblStatus = MakeLabel("准备安装。", 18, 296, 460, FontStyle.Regular, Color.FromArgb(154, 160, 166));

            btnInstall = MakeButton("安装", 96, true);
            btnInstall.Click += delegate(object o, EventArgs e) { StartInstall(); };
            Controls.Add(btnInstall);

            btnCancel = MakeButton("取消", 96, false);
            btnCancel.Click += delegate(object o, EventArgs e) { if (!busy) { DialogResult = DialogResult.Cancel; Close(); } };
            Controls.Add(btnCancel);

            lblTip = MakeLabel("提示：程序不联网、不写注册表；安装位置默认在当前用户目录，不需要管理员权限。", 18, 330, 480, FontStyle.Regular, Color.FromArgb(120, 126, 138));

            Controls.Add(lblTitle); Controls.Add(lblVer); Controls.Add(lblDir);
            Controls.Add(chkDesktop); Controls.Add(chkStartMenu); Controls.Add(chkLaunch);
            Controls.Add(lblStatus); Controls.Add(lblTip);
            ClientSize = new Size(S(520), S(370));
            LayoutAll();
            Resize += delegate(object o, EventArgs e) { LayoutAll(); };
        }

        static Font ThemeFont()
        {
            try { return new Font("Microsoft YaHei UI", 9.75f); } catch { return SystemFonts.MessageBoxFont; }
        }

        int DetectDpi()
        {
            try { int d = (int)Math.Round(CreateGraphics().DpiX); if (d >= 72 && d <= 480) return d; } catch { }
            try { int d = DeviceDpi; if (d >= 72 && d <= 480) return d; } catch { }
            return 96;
        }

        Label MakeLabel(string text, int x, int y, int w, FontStyle st, Color c)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font(Font.FontFamily, st == FontStyle.Bold ? 12f : 9.75f, st);
            l.ForeColor = c;
            l.AutoSize = false;
            l.Location = new Point(S(x), S(y));
            l.Size = new Size(S(w), S(st == FontStyle.Bold ? 28 : 22));
            return l;
        }

        CheckBox MakeCheck(string text, bool val)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = val;
            c.ForeColor = Color.FromArgb(220, 224, 230);
            c.FlatStyle = FlatStyle.Flat;
            return c;
        }

        Button MakeButton(string text, int w, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = accent ? Color.FromArgb(79, 140, 255) : Color.FromArgb(58, 60, 66);
            b.ForeColor = accent ? Color.White : Color.FromArgb(232, 234, 237);
            b.UseVisualStyleBackColor = false;
            return b;
        }

        void LayoutAll()
        {
            Sf = DetectDpi();
            int w = ClientSize.Width;
            lblTitle.Location = new Point(S(18), S(16));
            lblTitle.Size = new Size(w - S(36), S(28));
            lblVer.Location = new Point(S(18), S(46));
            lblVer.Size = new Size(w - S(36), S(22));
            lblDir.Location = new Point(S(18), S(88));
            lblDir.Size = new Size(S(84), S(24));
            btnBrowse.Size = new Size(S(84), S(28));
            btnBrowse.Location = new Point(w - S(18) - btnBrowse.Width, S(84));
            txtDir.Location = new Point(S(104), S(86));
            txtDir.Size = new Size(Math.Max(S(120), btnBrowse.Left - S(112)), S(26));
            chkDesktop.Location = new Point(S(18), S(128));
            chkDesktop.Size = new Size(S(300), S(26));
            chkStartMenu.Location = new Point(S(18), S(158));
            chkStartMenu.Size = new Size(S(300), S(26));
            chkLaunch.Location = new Point(S(18), S(188));
            chkLaunch.Size = new Size(S(300), S(26));
            bar.Location = new Point(S(18), S(228));
            bar.Size = new Size(w - S(36), S(20));
            lblStatus.Location = new Point(S(18), S(256));
            lblStatus.Size = new Size(w - S(36), S(22));
            btnInstall.Size = new Size(S(96), S(32));
            btnInstall.Location = new Point(w - S(18) - btnInstall.Width, S(320));
            btnCancel.Size = new Size(S(96), S(32));
            btnCancel.Location = new Point(btnInstall.Left - btnCancel.Width - S(10), S(320));
            lblTip.Location = new Point(S(18), S(288));
            lblTip.Size = new Size(w - S(36), S(24));
            ClientSize = new Size(w, S(370));
        }

        void StartInstall()
        {
            if (busy) return;
            string dir = txtDir.Text.Trim();
            if (dir.Length == 0) { MessageBox.Show(this, "请填写安装位置。", Installer.AppName); return; }
            try { dir = Path.GetFullPath(dir); } catch { MessageBox.Show(this, "安装位置无效。", Installer.AppName); return; }

            if (Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0)
            {
                if (MessageBox.Show(this, "该文件夹里已经有文件，继续安装会覆盖同名文件。\n\n继续吗？",
                        Installer.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
            }

            busy = true;
            btnInstall.Enabled = false;
            btnCancel.Enabled = false;
            txtDir.Enabled = false;
            string d = dir;
            bool desk = chkDesktop.Checked, sm = chkStartMenu.Checked, run = chkLaunch.Checked;

            Thread th = new Thread(delegate()
            {
                int rc = Installer.Install(d, desk, sm, false, true, delegate(string msg, int pct)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate()
                        {
                            lblStatus.Text = msg;
                            if (pct >= 0 && pct <= 100) bar.Value = Math.Min(100, pct);
                        });
                    }
                    catch { }
                });
                try
                {
                    BeginInvoke((MethodInvoker)delegate()
                    {
                        busy = false;
                        if (rc == 0)
                        {
                            bar.Value = 100;
                            lblStatus.Text = "安装完成，位置：" + d;
                            MessageBox.Show(this, "安装完成！\n\n程序位置：" + d +
                                "\n\n可点「完成」关闭，也可以在 Windows 的「已安装的应用」里卸载。",
                                Installer.AppName + " 安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (run) { try { System.Diagnostics.Process.Start(Path.Combine(d, "启动播放器.bat")); } catch { } }
                            ExitCode = 0;
                            Close();
                        }
                        else
                        {
                            lblStatus.Text = "安装失败。";
                            btnInstall.Enabled = true;
                            btnCancel.Enabled = true;
                            txtDir.Enabled = true;
                        }
                    });
                }
                catch { }
            });
            th.IsBackground = true;
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string exeName = Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLowerInvariant();
            bool uninstall = Installer.HasArg(args, "/uninstall") || exeName.Contains("uninstall");
            bool silent = Installer.HasArg(args, "/silent") || Installer.HasArg(args, "/s") || Installer.HasArg(args, "/quiet");
            string dir = Installer.GetArg(args, "/dir=");
            if (dir.Length == 0 && uninstall) dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (dir.Length == 0) dir = Installer.DefaultDir();

            if (uninstall) return Installer.Uninstall(dir, silent);

            bool desktop = !Installer.HasArg(args, "/nodesktop");
            bool startMenu = !Installer.HasArg(args, "/nostartmenu");
            bool launch = Installer.HasArg(args, "/launch");
            if (silent) return Installer.Install(dir, desktop, startMenu, launch, true, null);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SetupForm f = new SetupForm(dir);
            Application.Run(f);
            return f.ExitCode;
        }
    }
}
