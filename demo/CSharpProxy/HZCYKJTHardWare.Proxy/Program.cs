using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HZCYKJTHardWare.Proxy
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // DPI awareness - safe on Win7+ (function exists since Vista)
            try { SetProcessDPIAware(); } catch { /* WinXP fallback */ }

            // Thread pool warm-up: prevent slow thread ramp-up under sudden high load
            // Default min threads is very low; without this, high-frequency requests queue up
            int minWorker, minIo;
            ThreadPool.GetMinThreads(out minWorker, out minIo);
            ThreadPool.SetMinThreads(Math.Max(minWorker, 20), Math.Max(minIo, 20));

            // === Global exception handlers for long-running stability ===
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
                // Do NOT re-throw — keep the process alive for long-running service
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

        /// <summary>
        /// Write crash-level log to file when normal logger may be unavailable.
        /// </summary>
        private static void CrashLog(string message)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSharpProxy_Logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"CSharpProxy_{DateTime.Now:yyyyMMdd}.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [致命] {message}{Environment.NewLine}";
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch { /* Last resort — must not throw */ }
        }
    }
}
