using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public static class Logger
    {
        private const int MaxQueueLength = 10000;
        private static readonly object _writerLock = new object();
        private static readonly BlockingCollection<LogEntry> _queue;
        private static readonly string _logDir;
        private static readonly Thread _workerThread;
        private static StreamWriter _writer;
        private static string _currentLogPath;
        private static long _droppedCount;
        private const string LogNamePrefix = "HZCYKJTHardWareExe_Logs";

        static Logger()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogNamePrefix);
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            _queue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>(), MaxQueueLength);
            _workerThread = new Thread(WriterLoop)
            {
                Name = "CSharpProxy_Logger",
                IsBackground = true
            };
            _workerThread.Start();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Flush(1000);
        }

        public static void Info(string message) => Write("信息", message);
        public static void Warn(string message) => Write("警告", message);
        public static void Error(string message) => Write("错误", message);
        public static void Error(string message, Exception ex) => Write("错误", $"{message}: {ex}");

        /// <summary>
        /// Non-blocking log writer with automatic cross-day file rollover.
        /// Log format matches Delphi: [yyyy-MM-dd HH:mm:ss.fff] [级别] message
        /// </summary>
        public static void Write(string level, string message)
        {
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                if (!_queue.TryAdd(new LogEntry { Date = date, Line = line }, 0))
                    Interlocked.Increment(ref _droppedCount);
            }
            catch
            {
                // Logger must not throw — would crash the process
            }
        }

        public static void Flush(int timeoutMs = 2000)
        {
            var deadline = Environment.TickCount + timeoutMs;
            while (_queue.Count > 0 && Environment.TickCount < deadline)
                Thread.Sleep(20);

            try
            {
                lock (_writerLock)
                {
                    _writer?.Flush();
                }
            }
            catch { }
        }

        private static void WriterLoop()
        {
            foreach (var entry in _queue.GetConsumingEnumerable())
            {
                try
                {
                    WriteToFile(entry);
                }
                catch
                {
                    // Logger must never throw.
                }
            }
        }

        private static void WriteToFile(LogEntry entry)
        {
            lock (_writerLock)
            {
                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);

                var filePath = Path.Combine(_logDir, $"{LogNamePrefix}_{entry.Date}.log");
                if (_writer == null || !string.Equals(_currentLogPath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _writer?.Dispose();
                    _writer = new StreamWriter(filePath, true, Encoding.UTF8) { AutoFlush = false };
                    _currentLogPath = filePath;
                }

                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0)
                    _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [警告] 日志队列已满，已丢弃 {dropped} 条日志");

                _writer.WriteLine(entry.Line);
                _writer.Flush();
            }
        }

        private struct LogEntry
        {
            public string Date;
            public string Line;
        }
    }
}
