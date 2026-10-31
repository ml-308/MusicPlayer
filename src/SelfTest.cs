using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MusicPlayer
{
    public static class AudioSelfTest
    {
        public static string Run(Settings s)
        {
            StringBuilder sb = new StringBuilder();
            string tmp = Path.Combine(Path.GetTempPath(), "MusicPlayer_selftest.wav");
            try { WriteTone(tmp, 440, 0.5); }
            catch (Exception ex) { sb.AppendLine("无法创建测试音频：" + ex.Message); return sb.ToString(); }

            sb.AppendLine("测试音频：" + tmp);
            sb.AppendLine();

            // ---- Windows 媒体引擎（WinRT / Media Foundation）----
            sb.AppendLine("【Windows 媒体引擎】推荐，支持 MP3 / WMA / WAV / FLAC / M4A / AAC / ALAC 等");
            try
            {
                WinRtBackend b = new WinRtBackend();
                b.Load(tmp);
                bool ok = false, fail = false;
                string err = "";
                for (int i = 0; i < 24; i++)
                {
                    Application.DoEvents();
                    Thread.Sleep(60);
                    if (b.DurationMs > 0) { ok = true; break; }
                    if (b.Failed) { fail = true; err = b.LastError; break; }
                }
                if (ok) sb.AppendLine("    结果：可用    （测试时长 " + (b.DurationMs / 1000.0).ToString("0.00") + " 秒）");
                else if (fail) sb.AppendLine("    结果：不可用    " + err);
                else sb.AppendLine("    结果：打开超时（可能被系统策略或安全软件限制）");
                b.Dispose();
            }
            catch (Exception ex) { sb.AppendLine("    结果：不可用    " + ex.GetType().Name + " " + ex.Message); }

            sb.AppendLine();

            // ---- MCI ----
            sb.AppendLine("【MCI 兼容模式】备用，通常支持 WAV；部分系统也支持 MP3 / WMA");
            try
            {
                MciBackend b = new MciBackend();
                b.Load(tmp);
                if (b.Loaded)
                {
                    long d = b.DurationMs;
                    sb.AppendLine("    结果：可用    （测试时长 " + (d / 1000.0).ToString("0.00") + " 秒）");
                    b.Volume = 50;
                    sb.AppendLine("    音量控制：" + (b.VolumeUnsupported ? "该系统不支持，将使用系统音量" : "支持"));
                }
                else sb.AppendLine("    结果：不可用    " + b.LastError);
                b.Dispose();
            }
            catch (Exception ex) { sb.AppendLine("    结果：不可用    " + ex.GetType().Name + " " + ex.Message); }

            sb.AppendLine();
            sb.AppendLine("说明：以上结果只反映当前系统环境。若某项不可用，程序会自动使用另一项。");
            sb.AppendLine("提示：如果两个引擎都不可用，通常是系统缺少媒体功能组件，或播放被安全软件拦截。");
            return sb.ToString();
        }

        static void WriteTone(string path, double freq, double seconds)
        {
            int rate = 22050;
            int n = (int)(rate * seconds);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                BinaryWriter w = new BinaryWriter(fs);
                int dataLen = n * 2;
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write((int)(36 + dataLen));
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataLen);
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Sin(2 * Math.PI * freq * i / rate);
                    short s = (short)(v * 8000);
                    w.Write(s);
                }
                w.Flush();
            }
        }
    }
}