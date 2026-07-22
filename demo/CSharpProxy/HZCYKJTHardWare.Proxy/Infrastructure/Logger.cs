using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        private static long _totalDroppedCount;
        private const string LogNamePrefix = "HZCYKJTHardWareExe_Logs";
        private static int _retentionDays = 30;
        private static long _maxTotalSizeBytes = 2048L * 1024L * 1024L;
        private static long _diskWarningFreeBytes = 2048L * 1024L * 1024L;
        private static int _flushIntervalMs = 500;
        private static int _flushBatchSize = 50;
        private static int _pendingLines;
        private static DateTime _lastFlushUtc = DateTime.UtcNow;
        private static string _lastCleanupDate = "";
        private static long _lastSuccessfulWriteUtcTicks;
        private static long _lastSuccessfulFlushUtcTicks;
        private static long _currentFileLength;
        private static long _writeFailureCount;
        private static string _lastWriteError = "";
        private static long _lastEmergencyReportUtcTicks;
        private static int _shutdownState;

        // 日志级别过滤：0=Debug，1=Info，2=Warn，3=Error
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
        internal static int PendingCount => _queue.Count;
        internal static long TotalDroppedCount => Interlocked.Read(ref _totalDroppedCount);
        internal static long WriteFailureCount => Interlocked.Read(ref _writeFailureCount);
        internal static long CurrentFileLength => Interlocked.Read(ref _currentFileLength);
        internal static long LastFlushAgeMs => GetLastFlushAgeMs();
        internal static bool IsStopping => Volatile.Read(ref _shutdownState) != 0;
        internal static string LastWriteError => Volatile.Read(ref _lastWriteError) ?? "";

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
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown(5000);
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
        /// 非阻塞日志写入器，支持跨日自动切换日志文件。
        /// 日志格式与 Delphi 保持一致：[yyyy-MM-dd HH:mm:ss.fff] [级别] message
        /// </summary>
        public static void Write(string level, string message, int levelNum)
        {
            if (levelNum < _minLevel) return;
            if (Volatile.Read(ref _shutdownState) != 0)
            {
                RecordDroppedEntry();
                return;
            }
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                if (!_queue.TryAdd(new LogEntry
                    { Date = date, Line = line, LevelNum = levelNum }, 0))
                {
                    RecordDroppedEntry();
                }
            }
            catch (InvalidOperationException)
            {
                RecordDroppedEntry();
            }
            catch
            {
                // 日志组件不得向外抛出异常，避免导致进程退出
            }
        }

        public static void Flush(int timeoutMs = 2000)
        {
            timeoutMs = Math.Max(0, timeoutMs);
            var stopwatch = Stopwatch.StartNew();
            var signal = new ManualResetEventSlim(false);
            try
            {
                if (Volatile.Read(ref _shutdownState) != 0)
                {
                    if (Thread.CurrentThread != _workerThread)
                        _workerThread.Join(timeoutMs);
                    return;
                }

                if (!_queue.TryAdd(new LogEntry { FlushSignal = signal }, timeoutMs))
                    return;

                var remaining = Math.Max(0, timeoutMs - (int)stopwatch.ElapsedMilliseconds);
                signal.Wait(remaining);
            }
            catch (InvalidOperationException)
            {
                // Shutdown may complete the collection between the state check and TryAdd.
            }
            catch (Exception ex)
            {
                RecordWriterFailure(ex);
            }
        }

        public static bool Shutdown(int timeoutMs = 5000)
        {
            timeoutMs = Math.Max(0, timeoutMs);
            if (Interlocked.CompareExchange(ref _shutdownState, 1, 0) == 0)
            {
                try { _queue.CompleteAdding(); }
                catch (InvalidOperationException) { }
            }

            if (Thread.CurrentThread == _workerThread)
                return false;

            var stopped = false;
            try { stopped = _workerThread.Join(timeoutMs); }
            catch (ThreadStateException) { stopped = true; }

            if (!stopped)
            {
                Trace.WriteLine("Logger shutdown timed out before the queue was fully drained.");
            }

            return stopped && _queue.Count == 0;
        }

        private static void WriterLoop()
        {
            while (!_queue.IsCompleted)
            {
                try
                {
                    LogEntry entry;
                    if (_queue.TryTake(out entry, _flushIntervalMs))
                    {
                        if (entry.FlushSignal != null)
                        {
                            try { FlushWriter(); }
                            finally { entry.FlushSignal.Set(); }
                        }
                        else
                        {
                            WriteToFile(entry);
                        }
                    }
                    else
                        FlushWriter();
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    RecordWriterFailure(ex);
                    // 日志组件不得向外抛出异常
                }
            }

            try
            {
                lock (_writerLock)
                {
                    FlushWriterLocked();
                    _writer?.Dispose();
                    _writer = null;
                }
            }
            catch (Exception ex)
            {
                RecordWriterFailure(ex);
            }
            finally
            {
                Volatile.Write(ref _shutdownState, 2);
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
                    _writer = CreateSharedWriter(filePath);
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
                Interlocked.Exchange(ref _lastSuccessfulWriteUtcTicks, DateTime.UtcNow.Ticks);
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
            if (_writer != null)
            {
                _writer.Flush();
                Interlocked.Exchange(ref _currentFileLength, _writer.BaseStream.Length);
                Interlocked.Exchange(ref _lastSuccessfulFlushUtcTicks, DateTime.UtcNow.Ticks);
            }
            _pendingLines = 0;
            _lastFlushUtc = DateTime.UtcNow;
        }

        internal static StreamWriter CreateSharedWriter(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write,
                FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            return new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
        }

        private static void RecordDroppedEntry()
        {
            Interlocked.Increment(ref _droppedCount);
            Interlocked.Increment(ref _totalDroppedCount);
        }

        private static long GetLastFlushAgeMs()
        {
            var ticks = Interlocked.Read(ref _lastSuccessfulFlushUtcTicks);
            if (ticks <= 0) return -1;
            return Math.Max(0L, (DateTime.UtcNow.Ticks - ticks) / TimeSpan.TicksPerMillisecond);
        }

        private static void RecordWriterFailure(Exception ex)
        {
            Interlocked.Increment(ref _writeFailureCount);
            var error = ex == null ? "unknown" : ex.ToString();
            Volatile.Write(ref _lastWriteError, error);
            Trace.WriteLine("Logger writer failure: " + error);

            var nowTicks = DateTime.UtcNow.Ticks;
            var previous = Interlocked.Read(ref _lastEmergencyReportUtcTicks);
            if (previous > 0 && nowTicks - previous < TimeSpan.FromSeconds(30).Ticks)
                return;
            if (Interlocked.CompareExchange(ref _lastEmergencyReportUtcTicks, nowTicks, previous) != previous)
                return;

            try
            {
                var emergencyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "HZCYKJTHardWareExe_Logs_Emergency_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                using (var stream = new FileStream(emergencyPath, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine("[{0:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] primary logger write failed: {1}",
                        DateTime.Now, error.Replace(Environment.NewLine, " | "));
                    writer.Flush();
                    stream.Flush(true);
                }
            }
            catch (Exception fallbackEx)
            {
                Trace.WriteLine("Logger emergency write failure: " + fallbackEx);
            }
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
                // 保留期清理失败不得中断业务日志写入
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
            catch (Exception ex)
            {
                RecordWriterFailure(ex);
            }
        }

        private struct LogEntry
        {
            public string Date;
            public string Line;
            public int LevelNum;
            public ManualResetEventSlim FlushSignal;
        }
    }
}
