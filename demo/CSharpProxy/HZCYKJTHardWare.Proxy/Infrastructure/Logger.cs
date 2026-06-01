using System;
using System.IO;
using System.Text;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logDir;
        private static string _currentDate;  // Track date for cross-day rollover detection

        static Logger()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSharpProxy_Logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
            _currentDate = DateTime.Now.ToString("yyyyMMdd");
        }

        public static void Info(string message) => Write("信息", message);
        public static void Warn(string message) => Write("警告", message);
        public static void Error(string message) => Write("错误", message);
        public static void Error(string message, Exception ex) => Write("错误", $"{message}: {ex}");

        /// <summary>
        /// Thread-safe log writer with automatic cross-day file rollover.
        /// Log format matches Delphi: [yyyy-MM-dd HH:mm:ss.fff] [级别] message
        /// </summary>
        public static void Write(string level, string message)
        {
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                lock (_lock)
                {
                    // Cross-day rollover: switch to new log file at midnight
                    if (date != _currentDate)
                    {
                        _currentDate = date;
                    }
                    var fileName = $"CSharpProxy_{date}.log";
                    var filePath = Path.Combine(_logDir, fileName);

                    // Ensure directory still exists (defensive)
                    if (!Directory.Exists(_logDir))
                        Directory.CreateDirectory(_logDir);

                    File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logger must not throw — would crash the process
            }
        }
    }
}
