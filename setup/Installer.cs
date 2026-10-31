using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace MusicPlayerSetup
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    static class Installer
    {
        public const string AppName = "轻音播放器";
        public const string Version = "1.0.0";
        public const string Publisher = "Light Music Player";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LightMusicPlayer";
        public static string LastDir = "";

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "MusicPlayerSetup.log"),
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static string DefaultDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                Path.Combine("Programs", "LightMusicPlayer"));
        }

        public static bool HasArg(string[] a, string k)
        {
            foreach (string s in a) if (string.Equals(s, k, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string GetArg(string[] a, string prefix)
        {
            foreach (string s in a)
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return s.Substring(prefix.Length).Trim('"');
            return "";
        }

        public static string DesktopShortcut()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
        }

        public static string StartMenuShortcut()
        {
            string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     @"Microsoft\Windows\Start Menu\Programs");
            return Path.Combine(sm, AppName + ".lnk");
        }

        // 用 VB 的 CallByName（基于 IDispatch）创建快捷方式，失败再退回 wscript+VBS
        public static void CreateShortcut(string lnkPath, string target, string workDir, string icon, string desc)
        {
            try
            {
                string dir = Path.GetDirectoryName(lnkPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { }
            try
            {
                // 标准做法：直接用 IShellLink / IPersistFile 接口，避免 IDispatch 晚绑定问题
                ShellLink link = new ShellLink();
                IShellLinkW sl = (IShellLinkW)link;
                sl.SetPath(target);
                sl.SetWorkingDirectory(workDir);
                if (!string.IsNullOrEmpty(icon)) sl.SetIconLocation(icon, 0);
                if (!string.IsNullOrEmpty(desc)) sl.SetDescription(desc);
                ((IPersistFile)link).Save(lnkPath, true);
                Log("shortcut ok (shelllink): " + lnkPath);
                return;
            }
            catch (Exception ex)
            {
                string inner = ex.InnerException != null ? (ex.InnerException.GetType().Name + ": " + ex.InnerException.Message) : "";
                Log("shortcut shelllink FAILED: " + ex.GetType().Name + ": " + ex.Message + (inner.Length > 0 ? " | inner " + inner : ""));
            }
            try
            {
                string q = ((char)34).ToString();
                string vbs = Path.Combine(Path.GetTempPath(), "mpshortcut.vbs");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Set sh = CreateObject(" + q + "WScript.Shell" + q + ")");
                sb.AppendLine("Set lnk = sh.CreateShortcut(" + q + lnkPath + q + ")");
                sb.AppendLine("lnk.TargetPath = " + q + target + q);
                sb.AppendLine("lnk.WorkingDirectory = " + q + workDir + q);
                if (!string.IsNullOrEmpty(icon)) sb.AppendLine("lnk.IconLocation = " + q + icon + q);
                if (!string.IsNullOrEmpty(desc)) sb.AppendLine("lnk.Description = " + q + desc + q);
                sb.AppendLine("lnk.Save");
                File.WriteAllText(vbs, sb.ToString(), Encoding.Default);
                System.Diagnostics.Process p = System.Diagnostics.Process.Start("wscript.exe", "//nologo " + q + vbs + q);
                p.WaitForExit(15000);
                try { File.Delete(vbs); } catch { }
                Log("shortcut " + (File.Exists(lnkPath) ? "ok (vbs)" : "FAILED (vbs)") + ": " + lnkPath);
            }
            catch (Exception ex2)
            {
                Log("shortcut vbs FAILED: " + ex2.GetType().Name + ": " + ex2.Message);
            }
        }

        public static int Install(string dir, bool desktop, bool startMenu, bool launch, bool silent, Action<string, int> report)
        {
            try
            {
                if (report != null) report("正在准备安装目录…", 0);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
                if (payload == null) { Fail("安装包数据缺失，请重新下载 setup.exe。", silent); return 1; }

                List<ZipArchiveEntry> entries = new List<ZipArchiveEntry>();
                ZipArchive zip = new ZipArchive(payload, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry e in zip.Entries) if (!string.IsNullOrEmpty(e.Name)) entries.Add(e);
                int total = entries.Count;
                int done = 0;

                foreach (ZipArchiveEntry e in entries)
                {
                    string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string pre = "MusicPlayer" + Path.DirectorySeparatorChar;
                    if (rel.StartsWith(pre)) rel = rel.Substring(pre.Length);
                    string target = Path.Combine(dir, rel);
                    string td = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(td) && !Directory.Exists(td)) Directory.CreateDirectory(td);
                    e.ExtractToFile(target, true);
                    done++;
                    if (report != null) report("正在安装：" + e.Name, (int)(done * 80.0 / Math.Max(1, total)));
                }
                zip.Dispose();
                payload.Dispose();

                string exe = Path.Combine(dir, "MusicPlayer.exe");
                if (report != null) report("正在创建快捷方式…", 85);
                if (desktop) CreateShortcut(DesktopShortcut(), exe, dir, exe, AppName + " 本地音乐播放器");
                if (startMenu) CreateShortcut(StartMenuShortcut(), exe, dir, exe, AppName + " 本地音乐播放器");

                if (report != null) report("正在注册卸载信息…", 92);
                string uninstaller = Path.Combine(dir, "Uninstall.exe");
                try { File.Copy(Application.ExecutablePath, uninstaller, true); } catch (Exception ex) { Log("copy uninstaller failed: " + ex.Message); }
                WriteUninstallRegistry(dir, uninstaller);

                if (report != null) report("安装完成", 100);
                Log("install ok -> " + dir);
                LastDir = dir;
                if (!silent)
                {
                    MessageBox.Show("安装完成！\n\n程序位置：" + dir +
                        "\n\n可以用桌面 / 开始菜单的快捷方式启动，也可以在 Windows 的「已安装的应用」里卸载。",
                        AppName + " 安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                if (launch)
                {
                    try { System.Diagnostics.Process.Start(Path.Combine(dir, "启动播放器.bat")); }
                    catch { try { System.Diagnostics.Process.Start(exe); } catch { } }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log("install FAILED: " + ex.GetType().Name + ": " + ex.Message);
                Fail("安装失败：" + ex.Message, silent);
                return 1;
            }
        }

        static void WriteUninstallRegistry(string dir, string uninstaller)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    if (k == null) return;
                    k.SetValue("DisplayName", AppName + " " + Version);
                    k.SetValue("DisplayVersion", Version);
                    k.SetValue("Publisher", Publisher);
                    k.SetValue("DisplayIcon", Path.Combine(dir, "MusicPlayer.exe"));
                    k.SetValue("InstallLocation", dir);
                    k.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + uninstaller + "\" /uninstall /silent");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    long size = 0;
                    try { foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) size += new FileInfo(f).Length; }
                    catch { }
                    k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                }
                Log("registry ok: " + RegKey);
            }
            catch (Exception ex) { Log("registry FAILED: " + ex.GetType().Name + ": " + ex.Message); }
        }

        public static int Uninstall(string dir, bool silent)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    try
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegKey))
                        {
                            if (k != null) { object v = k.GetValue("InstallLocation"); if (v != null) dir = Convert.ToString(v); }
                        }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !silent)
                {
                    if (MessageBox.Show("确定要卸载 " + AppName + " 吗？\n\n安装位置：" + dir + "\n（你的音乐文件不受影响）",
                            AppName + " 卸载", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                        return 1;
                }

                try { File.Delete(DesktopShortcut()); } catch { }
                try { File.Delete(StartMenuShortcut()); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); } catch { }

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    try
                    {
                        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        {
                            try { File.Delete(f); } catch { }
                        }
                        foreach (string d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                        {
                            try { Directory.Delete(d, true); } catch { }
                        }
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    catch { }
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            string q = ((char)34).ToString();
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
                                "/c timeout /t 2 /nobreak >nul & rd /s /q " + q + dir + q)
                            { CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
                        }
                        catch { }
                    }
                }
                Log("uninstall ok -> " + dir);
                if (!silent)
                    MessageBox.Show(AppName + " 已卸载。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                Log("uninstall FAILED: " + ex.GetType().Name + ": " + ex.Message);
                Fail("卸载失败：" + ex.Message, silent);
                return 1;
            }
        }

        static void Fail(string msg, bool silent)
        {
            if (!silent) MessageBox.Show(msg, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
