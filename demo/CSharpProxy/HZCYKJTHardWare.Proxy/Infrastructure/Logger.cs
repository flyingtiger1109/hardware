using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
        private static int _retentionDays = 30;
        private static long _maxTotalSizeBytes = 2048L * 1024L * 1024L;
        private static long _diskWarningFreeBytes = 2048L * 1024L * 1024L;
        private static int _flushIntervalMs = 500;
        private static int _flushBatchSize = 50;
        private static int _pendingLines;
        private static DateTime _lastFlushUtc = DateTime.UtcNow;
        private static string _lastCleanupDate = "";

        // Log level filtering: 0=Debug, 1=Info, 2=Warn, 3=Error
        private static int _minLevel = 1; // default Info

        public static void SetMinLevel(string level)
        {
            switch (level?.ToLower())
            {
                case "debug": _minLevel = 0; break;
                case "info":  _minLevel = 1; break;
                case "warn":  _minLevel = 2; break;
                case "error": _minLevel = 3; break;
            }
        }

        public static string LogDirectory => _logDir;

        static Logger()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogNamePrefix);
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            lock (_writerLock)
                CleanupOldLogsLocked();

            _queue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>(), MaxQueueLength);
            _workerThread = new Thread(WriterLoop)
            {
                Name = "CSharpProxy_Logger",
                IsBackground = true
            };
            _workerThread.Start();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Flush(1000);
        }

        public static void Configure(int retentionDays, int maxTotalSizeMb,
            int diskWarningFreeMb, int flushIntervalMs, int flushBatchSize)
        {
            lock (_writerLock)
            {
                _retentionDays = Math.Max(1, Math.Min(3650, retentionDays));
                _maxTotalSizeBytes = (long)Math.Max(16,
                    Math.Min(102400, maxTotalSizeMb)) * 1024L * 1024L;
                _diskWarningFreeBytes = (long)Math.Max(0,
                    Math.Min(102400, diskWarningFreeMb)) * 1024L * 1024L;
                _flushIntervalMs = Math.Max(50, Math.Min(10000, flushIntervalMs));
                _flushBatchSize = Math.Max(1, Math.Min(10000, flushBatchSize));
                _lastCleanupDate = "";
                CleanupOldLogsLocked();
                CheckDiskSpaceLocked();
            }
        }

        public static void Debug(string message) => Write("调试", message, 0);
        public static void Info(string message) => Write("信息", message, 1);
        public static void Warn(string message) => Write("警告", message, 2);
        public static void Error(string message) => Write("错误", message, 3);
        public static void Error(string message, Exception ex) => Write("错误", $"{message}: {ex}", 3);

        /// <summary>
        /// Non-blocking log writer with automatic cross-day file rollover.
        /// Log format matches Delphi: [yyyy-MM-dd HH:mm:ss.fff] [级别] message
        /// </summary>
        public static void Write(string level, string message, int levelNum)
        {
            if (levelNum < _minLevel) return;
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                if (!_queue.TryAdd(new LogEntry
                    { Date = date, Line = line, LevelNum = levelNum }, 0))
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
                    if (_queue.TryTake(out entry, _flushIntervalMs))
                        WriteToFile(entry);
                    else
                        FlushWriter();
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
                    _pendingLines = 0;
                    _lastFlushUtc = DateTime.UtcNow;
                    CleanupOldLogsLocked();
                    CheckDiskSpaceLocked();
                }

                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0)
                {
                    _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [警告] 日志队列已满，已丢弃 {dropped} 条日志");
                    _pendingLines++;
                }

                _writer.WriteLine(entry.Line);
                _pendingLines++;
                if (entry.LevelNum >= 3 || _pendingLines >= _flushBatchSize ||
                    (DateTime.UtcNow - _lastFlushUtc).TotalMilliseconds >= _flushIntervalMs)
                {
                    FlushWriterLocked();
                }
            }
        }

        private static void FlushWriter()
        {
            lock (_writerLock)
                FlushWriterLocked();
        }

        private static void FlushWriterLocked()
        {
            _writer?.Flush();
            _pendingLines = 0;
            _lastFlushUtc = DateTime.UtcNow;
        }

        private static void CleanupOldLogsLocked()
        {
            try
            {
                var today = DateTime.Now.ToString("yyyyMMdd");
                if (string.Equals(today, _lastCleanupDate,
                    StringComparison.Ordinal)) return;
                _lastCleanupDate = today;

                if (!Directory.Exists(_logDir)) return;
                var cutoff = DateTime.Now.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_logDir,
                        LogNamePrefix + "_*.log", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .Where(file => !string.Equals(file.FullName,
                        _currentLogPath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => file.LastWriteTimeUtc)
                    .ToList();

                foreach (var file in files.Where(file => file.LastWriteTime < cutoff).ToList())
                {
                    try { file.Delete(); files.Remove(file); }
                    catch { }
                }

                long totalSize = files.Sum(file => file.Exists ? file.Length : 0L);
                foreach (var file in files)
                {
                    if (totalSize <= _maxTotalSizeBytes) break;
                    try
                    {
                        var length = file.Exists ? file.Length : 0L;
                        file.Delete();
                        totalSize = Math.Max(0L, totalSize - length);
                    }
                    catch { }
                }
            }
            catch
            {
                // Retention cleanup must never stop business logging.
            }
        }

        private static void CheckDiskSpaceLocked()
        {
            if (_diskWarningFreeBytes <= 0) return;
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(_logDir));
                if (string.IsNullOrEmpty(root)) return;
                var drive = new DriveInfo(root);
                if (!drive.IsReady || drive.AvailableFreeSpace >= _diskWarningFreeBytes)
                    return;

                var warning = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [警告] " +
                    $"日志盘剩余空间不足: available_mb={drive.AvailableFreeSpace / 1024 / 1024}, " +
                    $"threshold_mb={_diskWarningFreeBytes / 1024 / 1024}";
                if (_writer != null)
                {
                    _writer.WriteLine(warning);
                    _pendingLines++;
                    FlushWriterLocked();
                }
                System.Diagnostics.Debug.WriteLine(warning);
            }
            catch { }
        }

        private struct LogEntry
        {
            public string Date;
            public string Line;
            public int LevelNum;
        }
    }
}
