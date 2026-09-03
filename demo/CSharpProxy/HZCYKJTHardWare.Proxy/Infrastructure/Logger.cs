using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using HZCYKJTHardWare.Proxy.Parsing;

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
        public const string PlateCapture = "车牌抓帧";
        public const string LogManagement = "日志管理";
        public const string DeviceCapability = "设备能力";
        public const string ConfigManagement = "配置管理";
        public const string Application = "应用程序";
        public const string UnrecognizedInterface = "未识别接口";
    }

    public static class Logger
    {
        private const int MaxQueueLength = 10000;
        private const int ReservedErrorQueueLength = 256;
        private const long MaxFileSizeBytes = 200L * 1024L * 1024L;
        private static readonly object _writerLock = new object();
        private static readonly BlockingCollection<LogEntry> _queue;
        private static readonly BlockingCollection<LogEntry> _errorQueue;
        private static readonly string _logDir;
        private static readonly Thread _workerThread;
        private static readonly LogRateLimiter _rateLimiter =
            new LogRateLimiter(TimeSpan.FromMinutes(1));
        private static readonly LogRateLimiter _writerFailureRateLimiter =
            new LogRateLimiter(TimeSpan.FromMinutes(1));
        private static StreamWriter _writer;
        private static string _currentLogPath;
        private static string _currentLogDate;
        private static int _rollIndex;
        private static long _pendingBytes;
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
        private static int _shutdownState;
        private static int _lowDiskMode;

        // 生产成功日志只保留一个统一的保存路径字段；路径可能包含中文、空格
        // 或较长的长路径前缀，因此不能复用普通的“按空格截断”字段解析。
        private const int MaxProductionSavePathLength = 2048;
        private static readonly string[] SavedPathFieldNames =
        {
            "SavePath", "SavedPath", "OutputPath", "FilePath",
            "SavePaths", "SavedPaths", "OutputPaths", "FilePaths",
            "Path", "保存路径", "路径"
        };
        private static readonly string[] LogFieldNamesForPathBoundary =
        {
            "Operation", "RequestId", "CaptureRequestId", "PreviewRequestId",
            "TerminalIndex", "Device", "Result", "ErrorCode", "ReturnCode",
            "Stage", "ProxyError", "DurationMs", "QueueWaitMs", "RouteEpoch",
            "Attempt", "RetryCount", "Count", "RecoveryEpisodeId", "Session",
            "SessionKey", "Generation", "PlayerState", "SnapshotRet", "DetectedFormat",
            "FileBytes", "Bytes", "Width", "Height", "FrameAgeMs", "LastGoodFrameAgeMs",
            "Source", "ProducerStatus", "Reason", "SavePath", "SavedPath",
            "OutputPath", "FilePath", "SavePaths", "SavedPaths", "OutputPaths",
            "FilePaths", "Path"
        };

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
        internal static int PendingCount => _queue.Count + _errorQueue.Count;
        internal static long TotalDroppedCount => Interlocked.Read(ref _totalDroppedCount);
        internal static long WriteFailureCount => Interlocked.Read(ref _writeFailureCount);
        internal static long CurrentFileLength => Interlocked.Read(ref _currentFileLength);
        internal static long LastFlushAgeMs => GetLastFlushAgeMs();
        internal static bool IsStopping => Volatile.Read(ref _shutdownState) != 0;
        internal static string LastWriteError => Volatile.Read(ref _lastWriteError) ?? "";

        static Logger()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogNamePrefix);
            try
            {
                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Logger log directory initialization failure: " + ex);
                Interlocked.Increment(ref _writeFailureCount);
            }

            lock (_writerLock)
                CleanupOldLogsLocked();

            _queue = new BlockingCollection<LogEntry>(
                new ConcurrentQueue<LogEntry>(), MaxQueueLength - ReservedErrorQueueLength);
            _errorQueue = new BlockingCollection<LogEntry>(
                new ConcurrentQueue<LogEntry>(), ReservedErrorQueueLength);
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
        public static void Error(string message, Exception ex)
        {
            // 生产 ERROR 只保留摘要；完整异常链路下沉到 DEBUG，避免默认日志被
            // 堆栈和重复上下文淹没。
            Write("错误", message, 3);
            if (ex == null)
                return;

            var parsed = ParseMessage(message, "错误");
            var detail = parsed.Body + "，异常=" +
                         ex.ToString().Replace(Environment.NewLine, " | ");
            Write("调试", FormatModuleMessage(parsed.Module, "调试", detail), 0);
        }

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

        /// <summary>
        /// 将 DLL 导出名、内部 HTTP 路径和历史操作名统一为短 Operation。
        /// 该方法只用于日志关联，不改变任何对外接口或请求正文。
        /// </summary>
        internal static string CanonicalOperationName(string operation)
        {
            var value = (operation ?? string.Empty).Trim();
            if (value.Length == 0)
                return value;

            if (value.StartsWith("POST ", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("GET ", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("Enqueue ", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(value.IndexOf(' ') + 1).Trim();
            }

            const string exportPrefix = "HZCYKJTHardWare_";
            if (value.StartsWith(exportPrefix, StringComparison.Ordinal))
                value = value.Substring(exportPrefix.Length);

            switch (value)
            {
                case "InitSdk": return "InitSdk";
                case "ReleaseSdk": return "ReleaseSdk";
                case "SwitchTerminal":
                case "switch_terminal":
                case "/terminal/switch": return "SwitchTerminal";
                case "StartProcess":
                case "start_process":
                case "/process/start": return "StartProcess";
                case "EndProcess":
                case "end_process":
                case "/process/end": return "EndProcess";
                case "StartCameraPreview":
                case "/preview/camera/start": return "StartCameraPreview";
                case "StopCameraPreview":
                case "/preview/camera/stop": return "StopCameraPreview";
                case "StartFingerprintPreview":
                case "/preview/fingerprint/start": return "StartFingerprintPreview";
                case "StopFingerprintPreview":
                case "/preview/fingerprint/stop": return "StopFingerprintPreview";
                case "StartIrisPreview":
                case "/preview/iris/start": return "StartIrisPreview";
                case "StopIrisPreview":
                case "/preview/iris/stop": return "StopIrisPreview";
                case "StartPlatePreviewCJ":
                case "/preview/plate/cj/start": return "StartPlatePreviewCJ";
                case "StopPlatePreviewCJ":
                case "/preview/plate/cj/stop": return "StopPlatePreviewCJ";
                case "StartPlatePreviewRJ2":
                case "/preview/plate/rj2/start": return "StartPlatePreviewRJ2";
                case "StopPlatePreviewRJ2":
                case "/preview/plate/rj2/stop": return "StopPlatePreviewRJ2";
                case "StartPlatePreviewRJ3":
                case "/preview/plate/rj3/start": return "StartPlatePreviewRJ3";
                case "StopPlatePreviewRJ3":
                case "/preview/plate/rj3/stop": return "StopPlatePreviewRJ3";
                case "SaveLatestPlateFrame":
                case "/preview/plate/latest-frame": return "SaveLatestPlateFrame";
                case "CaptureCameraImage":
                case "/capture/face": return "CaptureFace";
                case "CaptureFingerprintImage":
                case "/capture/fingerprint": return "CaptureFingerprint";
                case "CaptureIrisImage":
                case "/capture/iris": return "CaptureIris";
                case "RequestOCR":
                case "/ocr":
                case "/resources/ocr-document/request": return "RequestOCR";
                case "RequestNfcCard":
                case "/nfc":
                case "/resources/nfc-card/request": return "RequestNfcCard";
                case "RequestAuthorize":
                case "/authorize":
                case "/resources/protocol/request": return "Authorize";
                case "RegisterEventCallback": return "RegisterEventCallback";
                case "/preview-ready": return "PreviewReady";
                default: return operation.Trim();
            }
        }

        private static string CanonicalResultName(string result)
        {
            switch ((result ?? string.Empty).Trim())
            {
                case "成功": return "Success";
                case "失败": return "Failed";
                case "已受理": return "Accepted";
                case "已恢复": return "Recovered";
                case "已停止": return "Stopped";
                case "忽略":
                case "已忽略": return "Ignored";
                case "已发送": return "Delivered";
                case "已取消": return "Cancelled";
                case "重试": return "Retrying";
                case "收到": return "Received";
                case "开始": return "Started";
                default: return result;
            }
        }

        private static string CanonicalizeOperationField(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var marker = message.IndexOf("Operation=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return message;

            var valueStart = marker + "Operation=".Length;
            var valueEnd = valueStart;
            while (valueEnd < message.Length && message[valueEnd] != ' ' &&
                   message[valueEnd] != '\t' && message[valueEnd] != ',' &&
                   message[valueEnd] != '，' && message[valueEnd] != ';')
                valueEnd++;

            var rawOperation = message.Substring(valueStart, valueEnd - valueStart);
            var canonicalOperation = CanonicalOperationName(rawOperation);
            if (string.Equals(rawOperation, canonicalOperation, StringComparison.Ordinal))
                return message;

            return message.Substring(0, valueStart) + canonicalOperation +
                   message.Substring(valueEnd);
        }

        /// <summary>
        /// 将历史日志正文中的 request_id= 统一为 RequestId=。
        /// 仅处理独立字段；process_request_id=、capture_request_id= 等链路字段
        /// 不被误改，JSON 请求正文也不会在此处被重写。
        /// </summary>
        private static string CanonicalizeRequestIdFields(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            const string canonicalMarker = "RequestId=";
            var searchOffset = 0;
            var copyOffset = 0;
            var requestIdSeen = false;
            StringBuilder result = null;
            while (TryFindRequestIdMarker(message, searchOffset,
                out var markerPosition, out var markerLength))
            {
                var isPartOfCompoundField = markerPosition > 0 &&
                    (char.IsLetterOrDigit(message[markerPosition - 1]) ||
                     message[markerPosition - 1] == '_');
                if (isPartOfCompoundField)
                {
                    searchOffset = markerPosition + markerLength;
                    continue;
                }

                if (result == null)
                    result = new StringBuilder(message.Length);
                result.Append(message, copyOffset, markerPosition - copyOffset);

                var valueStart = markerPosition + markerLength;
                var valueEnd = valueStart;
                while (valueEnd < message.Length && message[valueEnd] != ' ' &&
                       message[valueEnd] != '\t' && message[valueEnd] != ',' &&
                       message[valueEnd] != '，' && message[valueEnd] != ';' &&
                       message[valueEnd] != '；' && message[valueEnd] != '\r' &&
                       message[valueEnd] != '\n')
                    valueEnd++;

                if (!requestIdSeen)
                {
                    result.Append(canonicalMarker);
                    result.Append(message, valueStart, valueEnd - valueStart);
                    requestIdSeen = true;
                }

                copyOffset = valueEnd;
                searchOffset = valueEnd;
            }

            if (result == null)
                return message;

            result.Append(message, copyOffset, message.Length - copyOffset);
            return result.ToString();
        }

        private static bool TryFindRequestIdMarker(string message, int offset,
            out int position, out int markerLength)
        {
            position = -1;
            markerLength = 0;
            for (var i = Math.Max(0, offset); i < message.Length; i++)
            {
                if (message.Length - i >= "request_id=".Length &&
                    string.Equals(message.Substring(i, "request_id=".Length),
                        "request_id=", StringComparison.OrdinalIgnoreCase))
                {
                    position = i;
                    markerLength = "request_id=".Length;
                    return true;
                }

                if (message.Length - i >= "RequestId=".Length &&
                    string.Equals(message.Substring(i, "RequestId=".Length),
                        "RequestId=", StringComparison.OrdinalIgnoreCase))
                {
                    position = i;
                    markerLength = "RequestId=".Length;
                    return true;
                }
            }
            return false;
        }

        internal static string ResourceDisplayName(string resourceType)
        {
            switch ((resourceType ?? string.Empty).Trim())
            {
                case "face_image": return "人脸";
                case "fingerprint_image": return "指纹";
                case "iris_image": return "虹膜";
                case "ocr_document": return "证件识别";
                case "nfc_card": return "IC卡";
                case "authorization":
                case "protocol": return "授权";
                case "Camera_External": return "摄像头/第三方";
                case "Fingerprint_External": return "指纹/第三方";
                case "Iris_External": return "虹膜/第三方";
                default: return string.IsNullOrWhiteSpace(resourceType) ? "未知" : resourceType;
            }
        }

        internal static string FormatContextMessage(string operation,
            string terminalIndex = null, string device = null, string requestId = null,
            string result = null, string errorCode = null, long? durationMs = null,
            long? queueWaitMs = null, int? attempt = null, long? routeEpoch = null,
            string savePath = null)
        {
            var fields = new List<string>();
            AppendContextField(fields, "Operation", CanonicalOperationName(operation));
            AppendContextField(fields, "TerminalIndex", terminalIndex);
            AppendContextField(fields, "Device", device);
            AppendContextField(fields, "RequestId", requestId);
            AppendContextField(fields, "Result", CanonicalResultName(result));
            AppendContextField(fields, "ErrorCode", errorCode);
            if (durationMs.HasValue)
                AppendContextField(fields, "DurationMs", durationMs.Value.ToString());
            if (queueWaitMs.HasValue)
                AppendContextField(fields, "QueueWaitMs", queueWaitMs.Value.ToString());
            if (attempt.HasValue)
                AppendContextField(fields, "Attempt", attempt.Value.ToString());
            if (routeEpoch.HasValue)
                AppendContextField(fields, "RouteEpoch", routeEpoch.Value.ToString());
            if (!string.IsNullOrWhiteSpace(savePath))
                fields.Add("SavePath=" + JsonHelper.ToLogValue(savePath,
                    MaxProductionSavePathLength));
            return string.Join(" ", fields);
        }

        internal static string SanitizeLargePayloadForLog(string payload,
            string requestId = null)
        {
            payload = payload ?? string.Empty;
            var requestIdForLog = requestId;
            JObject json = null;
            try
            {
                json = string.IsNullOrWhiteSpace(payload) ? null : JObject.Parse(payload);
                if (string.IsNullOrWhiteSpace(requestIdForLog))
                    requestIdForLog = json?["request_id"]?.ToString();
            }
            catch
            {
                json = null;
            }

            var text = "payload=<omitted chars=" + payload.Length +
                       " estimated_bytes=" + Encoding.UTF8.GetByteCount(payload) + ">";
            if (!string.IsNullOrWhiteSpace(requestIdForLog))
                text += " RequestId=" + JsonHelper.ToLogValue(requestIdForLog);

            if (json == null)
                return text;

            var safeKeys = new[]
            {
                "status", "error_code", "code", "accepted", "result",
                "resource_type", "message", "save_path", "mrz", "card_text"
            };
            foreach (var key in safeKeys)
            {
                var token = json[key];
                if (token == null || token.Type == JTokenType.Object ||
                    token.Type == JTokenType.Array || IsSensitivePayloadKey(key))
                    continue;

                var value = token.Type == JTokenType.String
                    ? token.Value<string>()
                    : token.ToString(Newtonsoft.Json.Formatting.None);
                if (string.IsNullOrEmpty(value))
                    continue;
                text += " " + key + "=" + JsonHelper.ToLogValue(value);
            }
            return text;
        }

        internal static string SanitizeUrlForLog(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            var result = value;
            var schemeEnd = result.IndexOf("://", StringComparison.Ordinal);
            var authorityStart = schemeEnd >= 0 ? schemeEnd + 3 : 0;
            if (schemeEnd >= 0)
            {
                var authorityEnd = result.IndexOfAny(new[] { '/', '?', '#' }, authorityStart);
                if (authorityEnd < 0) authorityEnd = result.Length;
                var at = authorityEnd > authorityStart
                    ? result.LastIndexOf('@', authorityEnd - 1,
                        authorityEnd - authorityStart)
                    : -1;
                if (at >= authorityStart)
                    result = result.Substring(0, authorityStart) + "***:***@" +
                             result.Substring(at + 1);
            }

            var queryStart = result.IndexOf('?', authorityStart);
            if (queryStart >= 0)
            {
                var fragmentStart = result.IndexOf('#', queryStart + 1);
                var queryEnd = fragmentStart >= 0 ? fragmentStart : result.Length;
                var query = result.Substring(queryStart + 1, queryEnd - queryStart - 1);
                var queryParts = query.Split(new[] { '&' }, StringSplitOptions.None);
                for (var i = 0; i < queryParts.Length; i++)
                {
                    var equal = queryParts[i].IndexOf('=');
                    if (equal >= 0)
                    {
                        var key = queryParts[i].Substring(0, equal).ToLowerInvariant();
                        if (key.Contains("password") || key.Contains("passwd") ||
                            key.Contains("token") || key.Contains("secret") ||
                            key.Contains("credential") || key == "key" || key == "auth")
                        {
                            queryParts[i] = queryParts[i].Substring(0, equal + 1) + "***";
                        }
                    }
                }
                var suffix = fragmentStart >= 0 ? result.Substring(fragmentStart) : string.Empty;
                result = result.Substring(0, queryStart + 1) +
                         string.Join("&", queryParts) + suffix;
            }
            return JsonHelper.ToLogValue(result, 512);
        }

        internal static bool TryLogRateLimited(string key, string module,
            string level, string message)
        {
            var normalizedLevel = NormalizeLevelName(level);
            var levelNumber = LevelToNumber(normalizedLevel);
            if (!IsLevelEnabled(levelNumber)) return false;

            var decision = _rateLimiter.Record(key, message, DateTime.UtcNow);
            if (!decision.EmitCurrent) return false;
            var output = LogRateLimiter.FormatMergedMessage(decision, message);
            Write(normalizedLevel, FormatModuleMessage(module, normalizedLevel,
                output), levelNumber);
            return true;
        }

        public static string NormalizeForDisplay(string message, string defaultLevel = "信息")
        {
            var parsed = ParseMessage(message, defaultLevel);
            var body = MinimizeProductionMessage(
                CanonicalizeRequestIdFields(CanonicalizeOperationField(parsed.Body)),
                parsed.LevelNumber);
            if (string.IsNullOrEmpty(parsed.Module))
                return $"[{parsed.Level}] {body}";
            return $"[{parsed.Module}][{parsed.Level}] {body}";
        }

        /// <summary>
        /// 非阻塞日志写入器，支持跨日自动切换日志文件。
        /// 日志格式与 Native DLL 保持一致：[yyyy-MM-dd HH:mm:ss.fff] [模块][级别] message
        /// </summary>
        public static void Write(string level, string message, int levelNum)
        {
            if (!IsLevelEnabled(levelNum)) return;
            string formattedLine = null;
            try
            {
                var now = DateTime.Now;
                var date = now.ToString("yyyyMMdd");
                var parsed = ParseMessage(message, level);
                var canonicalBody = CanonicalizeRequestIdFields(
                    CanonicalizeOperationField(parsed.Body));
                var productionBody = MinimizeProductionMessage(
                    canonicalBody, parsed.LevelNumber);
                formattedLine = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] " +
                                $"[{parsed.Module}][{parsed.Level}] {productionBody}";

                EnqueueLine(date, formattedLine, levelNum);

                // DEBUG 开启时保留同一条业务记录的完整技术字段，默认 INFO 运行时
                // 不额外放大日志量。直接入队，避免公共入口递归触发再次裁剪。
                if (parsed.LevelNumber > 0 &&
                    !string.Equals(productionBody, canonicalBody, StringComparison.Ordinal) &&
                    IsLevelEnabled(0))
                {
                    var debugLine = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] " +
                                    $"[{parsed.Module}][调试] {canonicalBody}";
                    EnqueueLine(date, debugLine, 0);
                }
            }
            catch (InvalidOperationException)
            {
                if (levelNum >= 3 && !TryWriteEmergencyLine(
                    formattedLine ?? message ?? string.Empty, "ERROR queue is closed"))
                    RecordDroppedEntry();
                else if (levelNum < 3)
                    RecordDroppedEntry();
            }
            catch
            {
                if (levelNum >= 3 && !TryWriteEmergencyLine(
                    formattedLine ?? message ?? string.Empty, "ERROR enqueue failed"))
                    RecordDroppedEntry();
                else if (levelNum < 3)
                    RecordDroppedEntry();
            }
        }

        private static void EnqueueLine(string date, string line, int levelNum)
        {
            var entry = new LogEntry
            {
                Date = date,
                Line = line,
                LevelNum = levelNum
            };
            if (levelNum >= 3)
            {
                if (!_errorQueue.TryAdd(entry, 0) &&
                    !TryWriteEmergencyLine(line, "ERROR queue full or logger stopping"))
                    RecordDroppedEntry();
            }
            else if (!_queue.TryAdd(entry, 0))
            {
                RecordDroppedEntry();
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
                try { _errorQueue.CompleteAdding(); }
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

            return stopped && _queue.Count == 0 && _errorQueue.Count == 0;
        }

        private static void WriterLoop()
        {
            while (!_queue.IsCompleted || !_errorQueue.IsCompleted)
            {
                LogEntry entry = default(LogEntry);
                var hasEntry = false;
                try
                {
                    if (_errorQueue.TryTake(out entry, 0))
                    {
                        hasEntry = true;
                    }
                    else if (_queue.TryTake(out entry, _flushIntervalMs))
                    {
                        hasEntry = true;
                    }
                    else
                    {
                        FlushWriter();
                    }

                    if (!hasEntry)
                        continue;

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
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    RecordWriterFailure(ex);
                    if (hasEntry && entry.FlushSignal != null)
                        entry.FlushSignal.Set();
                    else if (hasEntry && entry.LevelNum >= 3)
                        TryWriteEmergencyLine(entry.Line, "logger worker exception");
                }
            }

            try
            {
                lock (_writerLock)
                {
                    FlushWriterLocked();
                    _writer?.Dispose();
                    _writer = null;
                    _currentLogPath = null;
                    _currentLogDate = null;
                    _rollIndex = 0;
                    _pendingBytes = 0;
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
                if (entry.LevelNum == 0 && Volatile.Read(ref _lowDiskMode) != 0)
                    return;

                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);

                var basePath = Path.Combine(_logDir, $"{LogNamePrefix}_{entry.Date}.log");
                if (_writer == null || !string.Equals(_currentLogDate, entry.Date,
                    StringComparison.Ordinal))
                {
                    _writer?.Dispose();
                    _rollIndex = 0;
                    var filePath = SelectLogPath(entry.Date, basePath);
                    _writer = CreateSharedWriter(filePath);
                    _currentLogPath = filePath;
                    _currentLogDate = entry.Date;
                    _pendingLines = 0;
                    _pendingBytes = 0;
                    _lastFlushUtc = DateTime.UtcNow;
                    CleanupOldLogsLocked(true);
                    CheckDiskSpaceLocked();
                }

                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0)
                {
                    WriteLineLocked($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                        NormalizeForDisplay("日志队列已满，已丢弃 " + dropped + " 条日志", "警告"), 2);
                }

                if (!WriteLineLocked(entry.Line, entry.LevelNum))
                    throw new IOException("日志行写入失败");
                Interlocked.Exchange(ref _lastSuccessfulWriteUtcTicks, DateTime.UtcNow.Ticks);
                if (entry.LevelNum >= 3 || _pendingLines >= _flushBatchSize ||
                    (DateTime.UtcNow - _lastFlushUtc).TotalMilliseconds >= _flushIntervalMs)
                {
                    FlushWriterLocked();
                }
            }
        }

        private static string SelectLogPath(string date, string basePath)
        {
            var candidate = basePath;
            _rollIndex = 0;
            if (File.Exists(candidate) && new FileInfo(candidate).Length >= MaxFileSizeBytes)
            {
                for (var index = 1; index < 10000; index++)
                {
                    candidate = Path.Combine(_logDir,
                        $"{LogNamePrefix}_{date}_{index:000}.log");
                    if (!File.Exists(candidate) ||
                        new FileInfo(candidate).Length < MaxFileSizeBytes)
                    {
                        _rollIndex = index;
                        break;
                    }
                }
            }
            return candidate;
        }

        private static bool WriteLineLocked(string line, int levelNum)
        {
            if (_writer == null)
                return false;

            var text = line ?? string.Empty;
            var lineBytes = Encoding.UTF8.GetByteCount(text) +
                            Encoding.UTF8.GetByteCount(Environment.NewLine);
            var fileLength = File.Exists(_currentLogPath)
                ? new FileInfo(_currentLogPath).Length : 0L;
            if (MaxFileSizeBytes > 0 &&
                fileLength + _pendingBytes + lineBytes > MaxFileSizeBytes &&
                fileLength + _pendingBytes > 0)
            {
                _writer.Flush();
                _writer.Dispose();
                for (var index = _rollIndex + 1; index < 10000; index++)
                {
                    var candidate = Path.Combine(_logDir,
                        $"{LogNamePrefix}_{_currentLogDate}_{index:000}.log");
                    if (!File.Exists(candidate) ||
                        new FileInfo(candidate).Length < MaxFileSizeBytes)
                    {
                        _rollIndex = index;
                        _currentLogPath = candidate;
                        break;
                    }
                }
                _writer = CreateSharedWriter(_currentLogPath);
                _pendingBytes = 0;
                _pendingLines = 0;
                fileLength = File.Exists(_currentLogPath)
                    ? new FileInfo(_currentLogPath).Length : 0L;
                CleanupOldLogsLocked(true);
            }

            _writer.WriteLine(text);
            _pendingLines++;
            _pendingBytes += lineBytes;
            Interlocked.Exchange(ref _currentFileLength, fileLength + _pendingBytes);
            return true;
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
            _pendingBytes = 0;
            _lastFlushUtc = DateTime.UtcNow;
        }

        internal static StreamWriter CreateSharedWriter(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write,
                FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            return new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
        }

        private static bool TryWriteEmergencyLine(string line, string reason)
        {
            try
            {
                var emergencyDir = Path.GetTempPath();
                if (string.IsNullOrEmpty(emergencyDir))
                    emergencyDir = AppDomain.CurrentDomain.BaseDirectory;
                var emergencyPath = Path.Combine(emergencyDir,
                    "HZCYKJTHardWareExe_Logs_Emergency_" +
                    DateTime.Now.ToString("yyyyMMdd") + ".log");
                using (var stream = new FileStream(emergencyPath, FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine("[{0:yyyy-MM-dd HH:mm:ss.fff}] [日志管理][错误] " +
                        "应急写入原因={1}", DateTime.Now,
                        JsonHelper.ToLogValue(reason ?? "unknown"));
                    writer.WriteLine(line ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Logger emergency write failure: " + ex);
                return false;
            }
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
            if (levelNum == 0 && Volatile.Read(ref _lowDiskMode) != 0)
                return false;
            return levelNum >= Volatile.Read(ref _minLevel);
        }

        private static void RecordWriterFailure(Exception ex)
        {
            Interlocked.Increment(ref _writeFailureCount);
            var error = ex == null ? "unknown" : ex.ToString();
            Volatile.Write(ref _lastWriteError, error);
            Trace.WriteLine("Logger writer failure: " + error);

            var decision = _writerFailureRateLimiter.Record(
                "Logger|writer_failure", error, DateTime.UtcNow);
            if (!string.IsNullOrEmpty(decision.WindowSummary) && decision.EmitCurrent)
                TryWriteEmergencyLine(
                    "[日志管理][错误] " + decision.WindowSummary +
                    "，本次错误=" + decision.CurrentError,
                    "writer failure window");
            else if (decision.EmitCurrent)
                TryWriteEmergencyLine(
                    "[日志管理][错误] 日志主写入器失败：原因=" +
                    JsonHelper.ToLogValue(error.Replace(Environment.NewLine, " | ")),
                    "writer failure");
        }

        private static void CleanupOldLogsLocked(bool force = false)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyyMMdd");
                if (!force && string.Equals(today, _lastCleanupDate,
                    StringComparison.Ordinal)) return;
                _lastCleanupDate = today;

                if (!Directory.Exists(_logDir)) return;
                var cutoff = DateTime.Now.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_logDir,
                        LogNamePrefix + "_*.log", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .OrderBy(file => file.LastWriteTimeUtc)
                    .ToList();

                foreach (var file in files.Where(file => file.LastWriteTime < cutoff).ToList())
                {
                    if (string.Equals(file.FullName, _currentLogPath,
                        StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { file.Delete(); files.Remove(file); }
                    catch { }
                }

                long totalSize = files.Sum(file => file.Exists ? file.Length : 0L);
                foreach (var file in files)
                {
                    if (totalSize <= _maxTotalSizeBytes) break;
                    if (string.Equals(file.FullName, _currentLogPath,
                        StringComparison.OrdinalIgnoreCase))
                        continue;
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
            if (_diskWarningFreeBytes <= 0)
            {
                Volatile.Write(ref _lowDiskMode, 0);
                return;
            }
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(_logDir));
                if (string.IsNullOrEmpty(root)) return;
                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                    return;
                if (drive.AvailableFreeSpace >= _diskWarningFreeBytes)
                {
                    Volatile.Write(ref _lowDiskMode, 0);
                    return;
                }

                Volatile.Write(ref _lowDiskMode, 1);

                var warning = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    NormalizeForDisplay(
                    $"日志盘剩余空间不足：可用空间={drive.AvailableFreeSpace / 1024 / 1024}MB，" +
                    $"预警阈值={_diskWarningFreeBytes / 1024 / 1024}MB", "警告");
                if (_writer != null)
                {
                    WriteLineLocked(warning, 2);
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

        private static void AppendContextField(ICollection<string> fields,
            string name, string value)
        {
            if (fields == null || string.IsNullOrWhiteSpace(value))
                return;
            fields.Add(name + "=" + JsonHelper.ToLogValue(value));
        }

        private static bool IsSensitivePayloadKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var value = key.ToLowerInvariant();
            return value == "image_base64" || value == "imagedata" ||
                   value == "image_data" || value == "undistorted_image_base64" ||
                   value == "raw_json" || value == "raw_body" || value == "body" ||
                   value == "binary" || value == "frame" || value == "video" ||
                   value.Contains("password") || value.Contains("passwd") ||
                   value.Contains("token") || value.Contains("secret") ||
                   value.Contains("credential");
        }

        /// <summary>
        /// 将普通请求型业务日志渲染为生产摘要。原始字段由 Write 在 DEBUG 开启时
        /// 镜像保存，因此 Operation、耗时、尺寸和链路字段不会从内部日志中消失。
        /// 恢复、聚合、指标和健康诊断消息必须保留上下文，避免丢失故障定位信息。
        /// </summary>
        internal static string MinimizeProductionMessage(string body, int levelNumber)
        {
            body = CanonicalizeRequestIdFields(body);
            if (levelNumber <= 0 || string.IsNullOrWhiteSpace(body) ||
                IsDiagnosticBusinessMessage(body))
                return body;

            var operation = ExtractLogField(body, "Operation");
            if (string.IsNullOrWhiteSpace(operation))
                return body;

            var requestId = ExtractLogField(body, "RequestId");
            if (string.IsNullOrWhiteSpace(requestId))
                requestId = ExtractLogField(body, "CaptureRequestId");
            if (string.IsNullOrWhiteSpace(requestId))
                requestId = ExtractLogField(body, "PreviewRequestId");
            var hasRequestId = !IsMissingRequestId(requestId);

            var result = ExtractLogField(body, "Result");
            var isSuccess = levelNumber == 1 && IsSuccessfulBusinessResultName(result);
            var isFailure = (levelNumber == 2 || levelNumber == 3) &&
                            (string.Equals(result, "Failed", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(result, "失败", StringComparison.Ordinal));
            if (!isSuccess && !isFailure)
                return body;

            var description = ExtractBusinessDescription(body);
            if (string.IsNullOrWhiteSpace(description))
                description = isFailure ? "业务处理失败：" : "业务处理成功：";

            var output = description;
            var hasOutputField = false;
            if (hasRequestId)
            {
                output += "RequestId=" + requestId;
                hasOutputField = true;
            }
            if (isFailure)
            {
                var errorCode = ExtractLogField(body, "ErrorCode");
                if (!string.IsNullOrWhiteSpace(errorCode) &&
                    !string.Equals(errorCode, "none", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasOutputField)
                        output += " ";
                    output += "ErrorCode=" + errorCode;
                    hasOutputField = true;
                }
            }
            if (isSuccess)
            {
                var savePath = ExtractSavedPath(body);
                if (!IsMissingRequestId(savePath))
                {
                    if (hasOutputField)
                        output += " ";
                    output += "保存路径=" + JsonHelper.ToLogValue(savePath,
                        MaxProductionSavePathLength);
                    hasOutputField = true;
                }
            }
            return hasOutputField ? output : output.TrimEnd('：', ':');
        }

        private static bool IsMissingRequestId(string requestId)
        {
            var value = (requestId ?? string.Empty).Trim();
            return value.Length == 0 ||
                   string.Equals(value, "<无>", StringComparison.Ordinal) ||
                   string.Equals(value, "<none>", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSuccessfulBusinessResultName(string result)
        {
            switch ((result ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "success":
                case "accepted":
                case "stopped":
                case "recovered":
                case "delivered":
                case "ignored":
                case "started":
                case "成功":
                case "已受理":
                case "已停止":
                case "已恢复":
                case "已发送":
                case "忽略":
                case "已忽略":
                case "开始":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDiagnosticBusinessMessage(string body)
        {
            return body.IndexOf("RecoveryEpisodeId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   body.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   body.IndexOf("恢复", StringComparison.Ordinal) >= 0 ||
                   body.IndexOf("重复故障汇总", StringComparison.Ordinal) >= 0 ||
                   body.IndexOf("聚合", StringComparison.Ordinal) >= 0 ||
                   body.IndexOf("次数=", StringComparison.Ordinal) >= 0 ||
                   body.IndexOf("Attempts=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   body.IndexOf("DowntimeMs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   body.IndexOf("Telemetry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   body.IndexOf("指标", StringComparison.Ordinal) >= 0 ||
                   body.IndexOf("RateLimit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractBusinessDescription(string body)
        {
            var marker = body.IndexOf("Operation=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return string.Empty;

            var description = body.Substring(0, marker).Trim();
            description = description.TrimEnd(' ', '\t', ',', '，', ';', '；');
            var chineseColon = description.LastIndexOf('：');
            var asciiColon = description.LastIndexOf(':');
            var colon = Math.Max(chineseColon, asciiColon);
            if (colon >= 0 && description.Substring(colon + 1).IndexOf('=') >= 0)
                description = description.Substring(0, colon).TrimEnd(' ', '\t', ',', '，', ';', '；');

            if (description.Length == 0)
                return string.Empty;
            if (!description.EndsWith("：", StringComparison.Ordinal) &&
                !description.EndsWith(":", StringComparison.Ordinal))
                description += "：";
            return description;
        }

        private static string ExtractLogField(string body, string fieldName)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            var markerText = fieldName + "=";
            var marker = body.IndexOf(markerText, StringComparison.OrdinalIgnoreCase);
            while (marker >= 0 && marker > 0 &&
                   (char.IsLetterOrDigit(body[marker - 1]) || body[marker - 1] == '_'))
            {
                var nextStart = marker + 1;
                marker = body.IndexOf(markerText, nextStart,
                    StringComparison.OrdinalIgnoreCase);
            }
            if (marker < 0)
                return string.Empty;

            var start = marker + fieldName.Length + 1;
            var end = start;
            while (end < body.Length && body[end] != ' ' && body[end] != '\t' &&
                   body[end] != ',' && body[end] != '，' && body[end] != ';' &&
                   body[end] != '；' && body[end] != '\r' && body[end] != '\n')
                end++;
            return body.Substring(start, end - start).Trim();
        }

        /// <summary>
        /// 提取成功文件操作的最终路径。保存路径允许包含空格，因此值的结束位置
        /// 由后续已知结构化字段确定，而不是简单遇到第一个空格就截断。
        /// </summary>
        internal static string ExtractSavedPath(string body)
        {
            if (string.IsNullOrEmpty(body))
                return string.Empty;

            var markerPosition = -1;
            var markerLength = 0;
            foreach (var fieldName in SavedPathFieldNames)
            {
                var position = FindStandaloneLogField(body, fieldName, 0);
                if (position < 0 || (markerPosition >= 0 && position >= markerPosition))
                    continue;

                markerPosition = position;
                markerLength = fieldName.Length + 1;
            }

            if (markerPosition < 0)
                return string.Empty;

            var valueStart = markerPosition + markerLength;
            var valueEnd = body.Length;
            foreach (var fieldName in LogFieldNamesForPathBoundary)
            {
                var position = FindStandaloneLogField(body, fieldName, valueStart + 1);
                if (position > valueStart && position < valueEnd)
                    valueEnd = position;
            }

            return body.Substring(valueStart, valueEnd - valueStart).Trim();
        }

        private static int FindStandaloneLogField(string body, string fieldName,
            int offset)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(fieldName))
                return -1;

            var markerText = fieldName + "=";
            var position = Math.Max(0, offset);
            while (position < body.Length)
            {
                position = body.IndexOf(markerText, position,
                    StringComparison.OrdinalIgnoreCase);
                if (position < 0)
                    return -1;

                if (position == 0 || IsLogFieldBoundary(body, position))
                    return position;

                position += markerText.Length;
            }

            return -1;
        }

        private static bool IsLogFieldBoundary(string body, int position)
        {
            if (position <= 0 || position >= body.Length)
                return position == 0;

            var previous = body[position - 1];
            return char.IsWhiteSpace(previous) || previous == ',' || previous == '，' ||
                   previous == ';' || previous == '；' || previous == ':' ||
                   previous == '：';
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

            var operationModule = InferOperationModule(body);
            if (!string.IsNullOrEmpty(operationModule))
                return operationModule;

            if (body.StartsWith("HTTP MJPEG", StringComparison.OrdinalIgnoreCase) ||
                body.IndexOf("VLC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("MJPEG", StringComparison.OrdinalIgnoreCase) >= 0 ||
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

        private static string InferOperationModule(string body)
        {
            var marker = body.IndexOf("Operation=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return null;

            var start = marker + "Operation=".Length;
            var end = start;
            while (end < body.Length && body[end] != ' ' && body[end] != '\t' &&
                   body[end] != ',' && body[end] != '，' && body[end] != ';')
                end++;
            var operation = body.Substring(start, end - start).Trim();
            switch (CanonicalOperationName(operation))
            {
                case "SwitchTerminal": return LogModules.TerminalSwitch;
                case "StartProcess":
                case "EndProcess": return LogModules.ProcessControl;
                case "StartCameraPreview":
                case "StopCameraPreview":
                case "StartFingerprintPreview":
                case "StopFingerprintPreview":
                case "StartIrisPreview":
                case "StopIrisPreview":
                case "StartPlatePreviewCJ":
                case "StopPlatePreviewCJ":
                case "StartPlatePreviewRJ2":
                case "StopPlatePreviewRJ2":
                case "StartPlatePreviewRJ3":
                case "StopPlatePreviewRJ3":
                case "PreviewReady": return LogModules.Preview;
                case "SaveLatestPlateFrame": return LogModules.PlateCapture;
                case "CaptureFace": return LogModules.FaceCapture;
                case "CaptureFingerprint": return LogModules.FingerprintCapture;
                case "CaptureIris": return LogModules.IrisCapture;
                case "RequestOCR": return LogModules.DocumentRecognition;
                case "RequestNfcCard": return LogModules.NfcRead;
                case "Authorize": return LogModules.Authorization;
                case "InitSdk":
                case "ReleaseSdk": return LogModules.SdkLifecycle;
                case "RegisterEventCallback": return LogModules.TerminalCallback;
                default: return null;
            }
        }
    }
}
