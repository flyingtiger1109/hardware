using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HZCYKJTHardWare.Proxy
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\HZCYKJTHardWare.Proxy.SingleInstance";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // 启用 DPI 感知；该函数自 Windows Vista 起可用，兼容 Windows 7 及以上版本
            try { SetProcessDPIAware(); } catch { /* 兼容 Windows XP */ }

            bool createdNew;
            using (var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    Application.EnableVisualStyles();
                    MessageBox.Show("程序已在运行，请勿重复打开。", ProductVersionInfo.DisplayName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 预热线程池，避免突发高负载时线程扩容过慢
                // 默认最小线程数较低，未调整时高频请求可能积压
                int minWorker, minIo;
                ThreadPool.GetMinThreads(out minWorker, out minIo);
                ThreadPool.SetMinThreads(Math.Max(minWorker, 20), Math.Max(minIo, 20));

                // ServicePointManager 连接池与 DNS 配置为进程级设置，仅初始化一次
                ServicePointManager.DefaultConnectionLimit = 50;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.MaxServicePointIdleTime = 60000;  // 60s idle timeout
                ServicePointManager.DnsRefreshTimeout = 120000;       // 2min DNS refresh

                // === 长期运行使用的全局异常处理函数 ===
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    var msg = $"[全局异常] UnhandledException: {(ex != null ? ex.ToString() : args.ExceptionObject?.ToString() ?? "未知")}";
                    try { CrashLog(msg); } catch { }
                };

                Application.ThreadException += (sender, args) =>
                {
                    var msg = $"[全局异常] ThreadException: {args.Exception}";
                    try { CrashLog(msg); } catch { }
                    // 不重新抛出异常，保持长期运行服务进程存活
                };

                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    var msg = $"[全局异常] UnobservedTaskException: {args.Exception}";
                    try { CrashLog(msg); } catch { }
                    args.SetObserved();  // Prevent process crash
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        /// <summary>
        /// 正常日志组件不可用时，将崩溃级日志直接写入文件。
        /// </summary>
        private static void CrashLog(string message)
        {
            try
            {
                const string logNamePrefix = "HZCYKJTHardWareExe_Logs";
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logNamePrefix);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"{logNamePrefix}_{DateTime.Now:yyyyMMdd}.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [致命] {message}{Environment.NewLine}";
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch { /* 最终回退路径不得抛出异常 */ }
        }
    }
}
