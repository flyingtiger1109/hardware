using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public static class LogModules
    {
        public const string HealthCheck = "健康检查";
        public const string Authorization = "授权";
        public const string FaceCapture = "人脸抓拍";
        public const string FingerprintCapture = "指纹抓拍";
        public const string IrisCapture = "虹膜抓拍";
        public const string DocumentRecognition = "证件识别";
        public const string NfcRead = "NFC读卡";
        public const string Preview = "预览";
        public const string TerminalSwitch = "终端切换";
        public const string ProcessControl = "流程控制";
        public const string TerminalCommunication = "终端通信";
        public const string TerminalCallback = "终端回调";
        public const string DllCallback = "DLL回调";
        public const string SdkLifecycle = "SDK生命周期";
        public const string ServiceListener = "服务监听";
        public const string RuntimeMetrics = "运行指标";
        public const string TaskQueue = "任务队列";
        public const string LogManagement = "日志管理";
        public const string DeviceCapability = "设备能力";
        public const string ConfigManagement = "配置管理";
        public const string Application = "应用程序";
        public const string UnrecognizedInterface = "未识别接口";
    }

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
            var minLevel = -1;
            switch (level?.ToLower())
            {
                case "debug": minLevel = 0; break;
                case "info":  minLevel = 1; break;
                case "warn":  minLevel = 2; break;
                case "error": minLevel = 3; break;
            }
            if (minLevel >= 0)
                Volatile.Write(ref _minLevel, minLevel);
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
        /// 判断指定级别是否会写入当前日志。UI 和其他日志入口也必须复用该判断，
        /// 避免文件日志已过滤而实时日志窗口仍显示大量调试明细。
        /// </summary>
        public static bool IsLevelEnabled(string level)
        {
            return IsLevelEnabled(LevelToNumber(NormalizeLevelName(level)));
        }

        /// <summary>
        /// 判断一条带有 [模块][级别] 前缀的消息是否允许显示或写入。
        /// </summary>
        public static bool IsMessageEnabled(string message, string defaultLevel = "信息")
        {
            var parsed = ParseMessage(message, defaultLevel);
            return IsLevelEnabled(parsed.LevelNumber);
        }

        public static void WriteMessage(string message, string defaultLevel = "信息")
        {
            var parsed = ParseMessage(message, defaultLevel);
            Write(parsed.Level, message, parsed.LevelNumber);
        }

        public static string FormatModuleMessage(string module, string level, string message)
        {
            var normalizedModule = NormalizeModuleName(module);
            var normalizedLevel = NormalizeLevelName(level);
            return $"[{normalizedModule}][{normalizedLevel}] {message ?? string.Empty}";
        }

        public static string NormalizeForDisplay(string message, string defaultLevel = "信息")
        {
            var parsed = ParseMessage(message, defaultLevel);
            if (string.IsNullOrEmpty(parsed.Module))
                return $"[{parsed.Level}] {parsed.Body}";
            return $"[{parsed.Module}][{parsed.Level}] {parsed.Body}";
        }

        /// <summary>
        /// 非阻塞日志写入器，支持跨日自动切换日志文件。
        /// 日志格式与 Native DLL 保持一致：[yyyy-MM-dd HH:mm:ss.fff] [模块][级别] message
        /// </summary>
        public static void Write(string level, string message, int levelNum)
        {
            if (!IsLevelEnabled(levelNum)) return;
            if (Volatile.Read(ref _shutdownState) != 0)
            {
                RecordDroppedEntry();
                return;
            }
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] {NormalizeForDisplay(message, level)}";

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
                    _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                        NormalizeForDisplay("日志队列已满，已丢弃 " + dropped + " 条日志", "警告"));
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

        private static bool IsLevelEnabled(int levelNum)
        {
            return levelNum >= Volatile.Read(ref _minLevel);
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
                    writer.WriteLine("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}",
                        DateTime.Now,
                        NormalizeForDisplay("日志主写入器失败：" + error.Replace(Environment.NewLine, " | "), "错误"));
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

                var warning = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    NormalizeForDisplay(
                    $"日志盘剩余空间不足：可用空间={drive.AvailableFreeSpace / 1024 / 1024}MB，" +
                    $"预警阈值={_diskWarningFreeBytes / 1024 / 1024}MB", "警告");
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

        private sealed class ParsedLogMessage
        {
            public string Module;
            public string Level;
            public int LevelNumber;
            public string Body;
        }

        private static ParsedLogMessage ParseMessage(string message, string defaultLevel)
        {
            var body = (message ?? string.Empty).Trim();
            var module = (string)null;
            var level = NormalizeLevelName(defaultLevel);

            for (var i = 0; i < 2; i++)
            {
                if (!TryTakeTag(ref body, out var tag))
                    break;

                if (IsLevelName(tag))
                    level = NormalizeLevelName(tag);
                else if (module == null)
                    module = NormalizeModuleName(tag);
                else
                {
                    body = "[" + tag + "] " + body;
                    break;
                }
            }

            var inferredModule = InferModule(body);
            if (module == null)
                module = inferredModule ?? LogModules.Application;
            else if ((string.Equals(module, LogModules.UnrecognizedInterface,
                         StringComparison.Ordinal) ||
                      string.Equals(module, LogModules.TerminalCommunication,
                         StringComparison.Ordinal) ||
                      string.Equals(module, LogModules.TerminalCallback,
                         StringComparison.Ordinal)) &&
                     !string.IsNullOrEmpty(inferredModule))
                module = inferredModule;

            return new ParsedLogMessage
            {
                Module = module,
                Level = level,
                LevelNumber = LevelToNumber(level),
                Body = body.TrimStart(' ', '\t', ':', '：')
            };
        }

        private static bool TryTakeTag(ref string text, out string tag)
        {
            tag = null;
            if (string.IsNullOrEmpty(text) || text[0] != '[')
                return false;

            var end = text.IndexOf(']');
            if (end <= 1 || end > 64)
                return false;

            tag = text.Substring(1, end - 1);
            text = text.Substring(end + 1).TrimStart();
            return true;
        }

        private static bool IsLevelName(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "debug":
                case "info":
                case "warn":
                case "error":
                case "调试":
                case "信息":
                case "警告":
                case "错误":
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeLevelName(string level)
        {
            switch ((level ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "debug":
                case "调试": return "调试";
                case "warn":
                case "warning":
                case "警告": return "警告";
                case "error":
                case "错误": return "错误";
                default: return "信息";
            }
        }

        private static int LevelToNumber(string level)
        {
            switch (NormalizeLevelName(level))
            {
                case "调试": return 0;
                case "警告": return 2;
                case "错误": return 3;
                default: return 1;
            }
        }

        private static string NormalizeModuleName(string module)
        {
            var value = (module ?? string.Empty).Trim();
            switch (value)
            {
                case "预览请求":
                case "预览管理":
                case "预览窗口":
                case "预览租约":
                case "PreviewMgr":
                    return LogModules.Preview;
                case "健康检测":
                    return LogModules.HealthCheck;
                case "硬件检测":
                    return LogModules.DeviceCapability;
                case "服务":
                    return LogModules.ServiceListener;
                case "流程":
                    return LogModules.ProcessControl;
                case "队列":
                    return LogModules.TaskQueue;
                case "事件分发":
                    return LogModules.TerminalCallback;
                case "TerminalMgr":
                    return LogModules.TerminalCommunication;
                case "接口":
                case "DLL请求":
                    return LogModules.UnrecognizedInterface;
                case "HTTP请求":
                case "代理服务":
                    return LogModules.TerminalCommunication;
                case "回调服务":
                    return LogModules.ServiceListener;
                case "NFC":
                    return LogModules.NfcRead;
                case "SDK":
                    return LogModules.SdkLifecycle;
                case "能力检查":
                    return LogModules.DeviceCapability;
                case "配置管理":
                    return LogModules.ConfigManagement;
                case "日志":
                    return LogModules.LogManagement;
                default:
                    return value;
            }
        }

        private static string InferModule(string body)
        {
            if (string.IsNullOrEmpty(body))
                return null;
            if (body.StartsWith("HTTP MJPEG", StringComparison.OrdinalIgnoreCase) ||
                body.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("预览", StringComparison.Ordinal) >= 0)
                return LogModules.Preview;
            if (body.IndexOf("/ping", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("健康检查", StringComparison.Ordinal) >= 0)
                return LogModules.HealthCheck;
            if (body.StartsWith("终端切换", StringComparison.Ordinal))
                return LogModules.TerminalSwitch;
            if (body.StartsWith("授权", StringComparison.Ordinal))
                return LogModules.Authorization;
            if (body.StartsWith("人脸抓拍", StringComparison.Ordinal))
                return LogModules.FaceCapture;
            if (body.StartsWith("指纹抓拍", StringComparison.Ordinal))
                return LogModules.FingerprintCapture;
            if (body.StartsWith("虹膜抓拍", StringComparison.Ordinal))
                return LogModules.IrisCapture;
            if (body.StartsWith("OCR", StringComparison.OrdinalIgnoreCase))
                return LogModules.DocumentRecognition;
            if (body.StartsWith("NFC", StringComparison.OrdinalIgnoreCase) ||
                body.StartsWith("IC卡", StringComparison.Ordinal) ||
                body.StartsWith("IC 卡", StringComparison.Ordinal))
                return LogModules.NfcRead;
            if (body.IndexOf("/capture/face", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.FaceCapture;
            if (body.IndexOf("/capture/fingerprint", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.FingerprintCapture;
            if (body.IndexOf("/capture/iris", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.IrisCapture;
            if (body.IndexOf("/ocr", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.DocumentRecognition;
            if (body.IndexOf("/nfc", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.NfcRead;
            if (body.IndexOf("/authorize", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.Authorization;
            if (body.IndexOf("/terminal/switch", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.TerminalSwitch;
            if (body.IndexOf("/process", StringComparison.OrdinalIgnoreCase) >= 0)
                return LogModules.ProcessControl;
            if (body.IndexOf("HTTP MJPEG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("预览", StringComparison.Ordinal) >= 0)
                return LogModules.Preview;
            if (body.IndexOf("人脸", StringComparison.Ordinal) >= 0 ||
                body.IndexOf("摄像头", StringComparison.Ordinal) >= 0)
                return LogModules.FaceCapture;
            if (body.IndexOf("指纹", StringComparison.Ordinal) >= 0)
                return LogModules.FingerprintCapture;
            if (body.IndexOf("虹膜", StringComparison.Ordinal) >= 0)
                return LogModules.IrisCapture;
            if (body.IndexOf("流程", StringComparison.Ordinal) >= 0)
                return LogModules.ProcessControl;
            if (body.IndexOf("切换", StringComparison.Ordinal) >= 0)
                return LogModules.TerminalSwitch;
            if (body.IndexOf("服务", StringComparison.Ordinal) >= 0 ||
                body.IndexOf("关闭窗口", StringComparison.Ordinal) >= 0)
                return LogModules.ServiceListener;
            if (body.IndexOf("队列", StringComparison.Ordinal) >= 0)
                return LogModules.TaskQueue;
            if (body.IndexOf("日志", StringComparison.Ordinal) >= 0)
                return LogModules.LogManagement;
            return null;
        }
    }
}
