using System;
using System.Windows.Forms;

namespace MusicPlayer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created = false;
            System.Threading.Mutex mutex = new System.Threading.Mutex(true, "LightMusicPlayer_SingleInstance_Mutex", out created);
            if (!created)
            {
                MessageBox.Show("轻音播放器已经在运行了。", "轻音播放器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { LogError(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "程序出现错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        static void LogError(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string file = System.IO.Path.Combine(dir, "MusicPlayer.error.log");
                System.IO.File.AppendAllText(file,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]\r\n" + ex.ToString() + "\r\n\r\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }
            try
            {
                MessageBox.Show(ex.Message + "\r\n\r\n详细信息已记录到 MusicPlayer.error.log。",
                    "轻音播放器 - 出现错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }
    }
}