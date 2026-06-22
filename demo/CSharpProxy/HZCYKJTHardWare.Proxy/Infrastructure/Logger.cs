using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public sealed class LogWrittenEventArgs : EventArgs
    {
        public LogWrittenEventArgs(string date, string line)
            : this(date, line, false)
        {
        }

        public LogWrittenEventArgs(string date, string line, bool isUiVisible)
        {
            Date = date;
            Line = line;
            IsUiVisible = isUiVisible;
        }

        public string Date { get; private set; }
        public string Line { get; private set; }
        public bool IsUiVisible { get; private set; }
    }

    public static class Logger
    {
        private const int MaxQueueLength = 10000;
        private const int FlushBatchSize = 100;
        private const int FlushIntervalMs = 1000;
        private static readonly object _writerLock = new object();
        private static readonly BlockingCollection<LogEntry> _queue;
        private static readonly string _logDir;
        private static readonly Thread _workerThread;
        private static StreamWriter _writer;
        private static string _currentLogPath;
        private static long _droppedCount;
        private static int _pendingFlushCount;
        private static DateTime _lastFlushUtc = DateTime.UtcNow;
        private static volatile bool _debugEnabled;
        private const string LogNamePrefix = "HZCYKJTHardWareExe_Logs";

        // Raised by the logger worker after a line is accepted for file output.
        // Subscribers must return quickly and must never touch WinForms controls directly.
        public static event EventHandler<LogWrittenEventArgs> LogWritten;

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

        public static void Info(string message) => Write("信息", message, false);
        public static void InfoForUi(string message) => Write("信息", message, true);
        public static void Debug(string message)
        {
            if (_debugEnabled)
                Write("调试", message, false);
        }
        public static void Warn(string message) => Write("警告", message);
        public static void WarnForUi(string message) => Write("警告", message, true);
        public static void Error(string message) => Write("错误", message);
        public static void Error(string message, Exception ex) => Write("错误", $"{message}: {ex}");

        public static void SetDebugEnabled(bool enabled)
        {
            _debugEnabled = enabled;
        }

        public static string GetLogFilePath(string date)
        {
            if (string.IsNullOrEmpty(date))
                date = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDir, $"{LogNamePrefix}_{date}.log");
        }

        /// <summary>
        /// Non-blocking log writer with automatic cross-day file rollover.
        /// Log format matches Delphi: [yyyy-MM-dd HH:mm:ss.fff] [级别] message
        /// </summary>
        public static void Write(string level, string message)
        {
            Write(level, message, false);
        }

        private static void Write(string level, string message, bool isUiVisible)
        {
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                if (!_queue.TryAdd(new LogEntry { Date = date, Line = line, IsUiVisible = isUiVisible }, 0))
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
                    FlushWriterLocked();
                }
            }
            catch { }
        }

        private static void WriterLoop()
        {
            while (true)
            {
                try
                {
                    LogEntry entry;
                    if (_queue.TryTake(out entry, FlushIntervalMs))
                    {
                        WriteToFile(entry);
                    }
                    else
                    {
                        FlushIdleWriter();
                    }
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch
                {
                    // Logger must never throw.
                }
            }
        }

        private static void WriteToFile(LogEntry entry)
        {
            string droppedLine = null;
            lock (_writerLock)
            {
                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);

                var filePath = GetLogFilePath(entry.Date);
                if (_writer == null || !string.Equals(_currentLogPath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _writer?.Dispose();
                    _writer = new StreamWriter(filePath, true, Encoding.UTF8) { AutoFlush = false };
                    _currentLogPath = filePath;
                }

                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0)
                {
                    droppedLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [警告] 日志队列已满，已丢弃 {dropped} 条日志";
                    _writer.WriteLine(droppedLine);
                }

                _writer.WriteLine(entry.Line);
                _pendingFlushCount++;

                var elapsedMs = (DateTime.UtcNow - _lastFlushUtc).TotalMilliseconds;
                if (_pendingFlushCount >= FlushBatchSize || elapsedMs >= FlushIntervalMs)
                    FlushWriterLocked();

            }

            PublishLineWritten(entry.Date, droppedLine, false);
            PublishLineWritten(entry.Date, entry.Line, entry.IsUiVisible);
        }

        private static void FlushIdleWriter()
        {
            lock (_writerLock)
            {
                if (_pendingFlushCount > 0)
                    FlushWriterLocked();
            }
        }

        private static void FlushWriterLocked()
        {
            _writer?.Flush();
            _pendingFlushCount = 0;
            _lastFlushUtc = DateTime.UtcNow;
        }

        private static void PublishLineWritten(string date, string line, bool isUiVisible)
        {
            if (string.IsNullOrEmpty(line))
                return;

            var handler = LogWritten;
            if (handler == null)
                return;

            try
            {
                handler(null, new LogWrittenEventArgs(date, line, isUiVisible));
            }
            catch
            {
                // Observers are optional and must never affect the file logger.
            }
        }

        private struct LogEntry
        {
            public string Date;
            public string Line;
            public bool IsUiVisible;
        }
    }
}
