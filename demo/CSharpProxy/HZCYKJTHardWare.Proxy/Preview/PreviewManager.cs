using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public enum PreviewResourceType
    {
        Camera,
        Fingerprint,
        Iris,
        PlateCJ,
        PlateRJ2,
        PlateRJ3
    }

    public enum PreviewSessionType
    {
        Local,
        External
    }

    public class PreviewSession
    {
        public IPreviewController Player { get; set; }
        public IntPtr TargetHwnd { get; set; }
        public Control LocalPanel { get; set; }
        public PreviewResourceType ResourceType { get; set; }
        public PreviewSessionType SessionType { get; set; }
        public bool IsRunning => Player?.IsRunning ?? false;
        internal IntPtr HostHwnd { get; set; }
        internal string TerminalBaseUrl { get; set; }
        internal Func<bool> ShouldContinue { get; set; }
        internal long Generation { get; set; }
        internal uint OwnerProcessId { get; set; }
        internal long OwnerProcessStartTimeUtcTicks { get; set; }
        internal string RequestId { get; set; }
        internal string ExplicitPreviewUrl { get; set; }
        internal bool TerminalBound { get; set; }
        internal bool DirectRenderTarget { get; set; }
    }

    internal sealed class PreviewRestartInfo
    {
        internal IntPtr TargetHwnd { get; private set; }
        internal Control LocalPanel { get; private set; }
        internal PreviewResourceType ResourceType { get; private set; }
        internal PreviewSessionType SessionType { get; private set; }
        internal string RequestId { get; private set; }
        internal string ExplicitPreviewUrl { get; private set; }
        internal bool TerminalBound { get; private set; }
        internal bool DirectRenderTarget { get; private set; }

        internal static PreviewRestartInfo FromSession(PreviewSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            return new PreviewRestartInfo
            {
                TargetHwnd = session.TargetHwnd,
                LocalPanel = session.LocalPanel,
                ResourceType = session.ResourceType,
                SessionType = session.SessionType,
                RequestId = session.RequestId,
                ExplicitPreviewUrl = session.ExplicitPreviewUrl,
                TerminalBound = session.TerminalBound,
                DirectRenderTarget = session.DirectRenderTarget
            };
        }
    }

    public class PreviewManager : IDisposable
    {
        private const int PreviewUrlTimeoutMs = 5000;
        private const int ColdStartWarmupMs = 800;
        private const int VlcPlayTimeoutMs = 2500;
        private const int VlcStopTimeoutMs = 1500;
        private const int VlcReleaseSettleMs = 500;
        private const int PreviewUrlValidationIntervalMs = 60000;
        private const int MaxMjpegRecoveryAttempts = 2;
        private const int VlcRecoveryLowFrequencyDelayMs = 15000;
        private readonly ConcurrentDictionary<string, PreviewSession> _sessions = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, PreviewRestartInfo> _restartInfo = new ConcurrentDictionary<string, PreviewRestartInfo>();
        private readonly ConcurrentDictionary<string, MjpegPreviewController> _mjpegWorkers = new ConcurrentDictionary<string, MjpegPreviewController>();
        private readonly ConcurrentDictionary<string, byte> _coldStartWarmups = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, CachedPreviewUrl> _previewUrlCache = new ConcurrentDictionary<string, CachedPreviewUrl>();
        private readonly ConcurrentDictionary<string, byte> _activeRecoveries = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, Task> _recoveryTasks = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, Task> _deferredMjpegDisposals = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, MjpegRecoveryEpisode> _mjpegRecoveryEpisodes =
            new ConcurrentDictionary<string, MjpegRecoveryEpisode>();
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private readonly TerminalClient _terminalClient;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly int _networkCachingMs;
        private readonly int _liveCachingMs;
        private readonly string _rtspTransport;
        private readonly SynchronizationContext _uiContext;  // Captured from UI thread at construction
        private readonly System.Threading.Timer _previewUrlValidationTimer;
        private readonly System.Threading.Timer _externalHostValidationTimer;
        private Action<PreviewResourceType, string, string> _externalPreviewFailureHandler;
        private int _previewUrlValidationRunning;
        private int _externalHostValidationRunning;
        private long _sessionGeneration;
        private long _nextMjpegRecoveryEpisodeId;
        private int _stopping;
        private int _shutdownCompleted;
        private int _finalResourcesDisposed;
        private int _deferredFinalCleanupScheduled;
        private bool _disposed;

        private sealed class CachedPreviewUrl
        {
            public PreviewResourceType ResourceType { get; set; }
            public string TerminalBaseUrl { get; set; }
            public string Url { get; set; }
            public DateTime UpdatedUtc { get; set; }
            public DateTime LastValidatedUtc { get; set; }
        }

        private sealed class MjpegRecoveryEpisode
        {
            internal long Id;
            internal DateTime FirstFailureUtc;
            internal DateTime LastFailureUtc;
            internal int FailureCount;
            internal int Attempt;
            internal MjpegFailureKind LastFailureKind;
            internal string LastError;
        }

        public PreviewManager(TerminalClient terminalClient,
            ActiveTasksTracker taskTracker = null)
        {
            _terminalClient = terminalClient;
            _taskTracker = taskTracker;
            _uiContext = SynchronizationContext.Current;  // 捕获 UI 线程同步上下文，与 Delphi MainThreadID 语义一致
            System.Diagnostics.Debug.Assert(_uiContext != null, "PreviewManager must be constructed on the UI thread");
            var cfg = AppConfig.Instance;
            _networkCachingMs = cfg.RtspNetworkCachingMs;
            _liveCachingMs = cfg.RtspLiveCachingMs;
            _rtspTransport = cfg.RtspTransport ?? "";
            _previewUrlValidationTimer = new System.Threading.Timer(ValidatePreviewUrlCacheCallback,
                null, PreviewUrlValidationIntervalMs, PreviewUrlValidationIntervalMs);
            _externalHostValidationTimer = new System.Threading.Timer(ValidateExternalHostsCallback,
                null, cfg.PreviewCheckHwndIntervalMs, cfg.PreviewCheckHwndIntervalMs);
        }

        internal void SetExternalPreviewFailureHandler(
            Action<PreviewResourceType, string, string> handler)
        {
            _externalPreviewFailureHandler = handler;
        }

        private static string SessionKey(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            return $"{resType}_{sessionType}";
        }

        private static string ResourceToTerminalPath(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera: return "/resources/face-preview/request";
                case PreviewResourceType.Fingerprint: return "/resources/fingerprint-preview/request";
                case PreviewResourceType.Iris: return "/resources/iris-preview/request";
                default: return "";
            }
        }

        private static string ResourceToName(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera: return "摄像头";
                case PreviewResourceType.Fingerprint: return "指纹";
                case PreviewResourceType.Iris: return "虹膜";
                case PreviewResourceType.PlateCJ: return "车牌CJ";
                case PreviewResourceType.PlateRJ2: return "车牌RJ2";
                case PreviewResourceType.PlateRJ3: return "车牌RJ3";
                default: return "未知";
            }
        }

        private static string RecoveryOperationName(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera: return "RecoverCameraPreview";
                case PreviewResourceType.Fingerprint: return "RecoverFingerprintPreview";
                case PreviewResourceType.Iris: return "RecoverIrisPreview";
                case PreviewResourceType.PlateCJ: return "RecoverPlatePreviewCJ";
                case PreviewResourceType.PlateRJ2: return "RecoverPlatePreviewRJ2";
                case PreviewResourceType.PlateRJ3: return "RecoverPlatePreviewRJ3";
                default: return "RecoverPreview";
            }
        }

        private static string PreviewOperationName(PreviewResourceType resType,
            bool start)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera:
                    return start ? "StartCameraPreview" : "StopCameraPreview";
                case PreviewResourceType.Fingerprint:
                    return start ? "StartFingerprintPreview" : "StopFingerprintPreview";
                case PreviewResourceType.Iris:
                    return start ? "StartIrisPreview" : "StopIrisPreview";
                case PreviewResourceType.PlateCJ:
                    return start ? "StartPlatePreviewCJ" : "StopPlatePreviewCJ";
                case PreviewResourceType.PlateRJ2:
                    return start ? "StartPlatePreviewRJ2" : "StopPlatePreviewRJ2";
                case PreviewResourceType.PlateRJ3:
                    return start ? "StartPlatePreviewRJ3" : "StopPlatePreviewRJ3";
                default:
                    return start ? "StartPreview" : "StopPreview";
            }
        }

        private static string FailureKindDisplayName(MjpegFailureKind failureKind)
        {
            switch (failureKind)
            {
                case MjpegFailureKind.StreamFailure: return "视频流中断";
                case MjpegFailureKind.DecodeFailure: return "画面解码失败";
                case MjpegFailureKind.RenderTargetFailure: return "绘制目标异常";
                default: return "预览故障";
            }
        }

        private static string SessionToName(PreviewSessionType sessionType)
        {
            switch (sessionType)
            {
                case PreviewSessionType.Local: return "本地";
                case PreviewSessionType.External: return "第三方";
                default: return "未知";
            }
        }

        internal static string FormatRequestId(string requestId)
        {
            return string.IsNullOrWhiteSpace(requestId) ? "<无>" : JsonHelper.ToLogValue(requestId);
        }

        internal static string FormatHwnd(IntPtr hwnd)
        {
            var value = unchecked((ulong)hwnd.ToInt64());
            return $"0x{value:X}";
        }

        private static string BuildTraceDescription(string description, string requestId)
        {
            return $"{description} [request_id={FormatRequestId(requestId)}]";
        }

        private static string TraceRequest(string requestId)
        {
            return $"request_id={FormatRequestId(requestId)}";
        }

        private static int CompareRestartPriority(PreviewRestartInfo left, PreviewRestartInfo right)
        {
            var result = GetRestartPriority(left).CompareTo(GetRestartPriority(right));
            if (result != 0)
                return result;

            result = left.SessionType.CompareTo(right.SessionType);
            if (result != 0)
                return result;

            return left.TargetHwnd.ToInt64().CompareTo(right.TargetHwnd.ToInt64());
        }

        private static int GetRestartPriority(PreviewRestartInfo session)
        {
            if (session == null)
                return int.MaxValue;

            switch (session.ResourceType)
            {
                case PreviewResourceType.Camera: return 0;
                case PreviewResourceType.Fingerprint: return 10;
                case PreviewResourceType.Iris: return 20;
                default: return 100;
            }
        }

        private static (int srcW, int srcH, bool swap) GetSourceDimensions(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera: return (480, 640, true);
                case PreviewResourceType.Fingerprint: return (640, 640, false);
                case PreviewResourceType.PlateCJ:
                case PreviewResourceType.PlateRJ2:
                case PreviewResourceType.PlateRJ3:
                    // 车牌流的实际尺寸由 VLC 解码结果/JPEG SOF 提取，不能写死。
                    return (0, 0, false);
                default: return (640, 480, false);
            }
        }

        private static bool IsPlateResource(PreviewResourceType resType)
        {
            return resType == PreviewResourceType.PlateCJ ||
                   resType == PreviewResourceType.PlateRJ2 ||
                   resType == PreviewResourceType.PlateRJ3;
        }

        /// <summary>
        /// 读取 DLL 对应的外部车牌预览会话中的最新完整 JPEG。
        /// 没有帧或缓存已过期时，只向同一播放器请求有界刷新；已有旧帧且首次
        /// 刷新返回 frame_stale 时，最多再快速重试一次，不创建新播放器。
        /// </summary>
        internal bool TryGetLatestPlateFrame(PreviewResourceType resType,
            string requestId, out LatestPlateFrameSnapshot snapshot,
            out string errorCode, out string errorMessage)
        {
            return TryGetLatestPlateFrame(resType, requestId, out snapshot,
                out errorCode, out errorMessage, out _, out _, out _);
        }

        internal bool TryGetLatestPlateFrame(PreviewResourceType resType,
            string requestId, out LatestPlateFrameSnapshot snapshot,
            out string errorCode, out string errorMessage,
            out string source, out long frameAgeMs)
        {
            return TryGetLatestPlateFrame(resType, requestId, out snapshot,
                out errorCode, out errorMessage, out source, out frameAgeMs,
                out _);
        }

        internal bool TryGetLatestPlateFrame(PreviewResourceType resType,
            string requestId, out LatestPlateFrameSnapshot snapshot,
            out string errorCode, out string errorMessage,
            out string source, out long frameAgeMs, out int retryCount)
        {
            snapshot = null;
            errorCode = "frame_not_ready";
            errorMessage = "车牌视频尚未产生可用帧";
            source = "unknown";
            frameAgeMs = -1;
            retryCount = 0;

            if (!IsPlateResource(resType))
            {
                errorCode = "invalid_camera";
                errorMessage = "不是车牌镜头资源";
                LogLatestFrameLookupDiagnostic(resType, requestId, "<not-created>",
                    "PlateLookup", errorCode, false, false, false, false,
                    false, -1, -1, "not_plate", false);
                return false;
            }
            if (string.IsNullOrWhiteSpace(requestId))
            {
                errorCode = "invalid_request_id";
                errorMessage = "request_id为空";
                LogLatestFrameLookupDiagnostic(resType, requestId, "<not-created>",
                    "RequestIdValidation", errorCode, false, false, false, false,
                    false, -1, -1, "unknown", false);
                return false;
            }

            var key = SessionKey(resType, PreviewSessionType.External);
            if (!_sessions.TryGetValue(key, out var session) || session == null)
            {
                errorCode = "preview_not_running";
                errorMessage = "对应车牌预览未运行";
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "SessionLookup", errorCode, false, false, false, false,
                    false, -1, -1, "missing", false);
                return false;
            }
            if (!string.Equals(session.RequestId, requestId, StringComparison.Ordinal))
            {
                errorCode = "preview_not_running";
                errorMessage = "车牌预览请求已变更或已停止";
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "RequestIdLookup", errorCode, true, false, false, false,
                    false, -1, session.Generation, "request_id_mismatch", false);
                return false;
            }

            var vlc = session.Player as VlcPreviewController;
            if (vlc == null)
            {
                snapshot = null;
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "PlayerLookup", errorCode, true, true, false, false,
                    false, -1, session.Generation, "not_vlc", false);
                return false;
            }

            var refreshedBySnapshot = false;
            var hasSnapshot = vlc.TryGetLatestFrame(out snapshot);
            if (!hasSnapshot && vlc.IsLatestFrameSourceRunning)
            {
                source = "OnDemandSnapshot";
                hasSnapshot = vlc.TryRefreshLatestFrame(
                    VlcPreviewController.LatestPlateFrameRefreshTimeoutMs,
                    VlcPreviewController.LatestPlateFrameMaxAgeMs,
                    out snapshot, out refreshedBySnapshot);
                if (hasSnapshot &&
                    !IsLatestPlateFrameSessionCurrent(key, session, vlc, requestId))
                {
                    snapshot = null;
                    hasSnapshot = false;
                }
                if (hasSnapshot)
                    source = refreshedBySnapshot ? "OnDemandSnapshot" : "Cache";
            }
            else if (hasSnapshot)
            {
                source = "Cache";
            }

            if (!hasSnapshot)
            {
                if (vlc.LatestFrameFailure ==
                    VlcPreviewController.LatestFrameFailureTooLarge)
                {
                    errorCode = "frame_too_large";
                    errorMessage = "车牌视频JPEG超过8MB限制";
                }
                else if (vlc.LatestFrameFailure ==
                    VlcPreviewController.LatestFrameFailureDataInvalid)
                {
                    errorCode = "frame_data_invalid";
                    errorMessage = "车牌视频未产生有效JPEG帧";
                }
                else
                {
                    errorCode = "frame_not_ready";
                    errorMessage = "车牌视频尚未产生完整JPEG帧";
                }
                frameAgeMs = GetFrameAgeMs(snapshot);
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "LatestFrameLookup", errorCode, true, true, true,
                    hasSnapshot, IsFrameStructurallyValid(snapshot),
                    GetFrameAgeMs(snapshot), session.Generation,
                    vlc.IsRunning ? "running" : "stopped",
                    vlc.IsLatestFrameSourceRunning);
                snapshot = null;
                return false;
            }

            if (!session.IsRunning || !vlc.IsLatestFrameSourceRunning)
            {
                errorCode = "frame_stale";
                errorMessage = "车牌视频帧已过期或视频已断开";
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "ProducerState", errorCode, true, true, true, true,
                    IsFrameStructurallyValid(snapshot), GetFrameAgeMs(snapshot),
                    session.Generation, vlc.IsRunning ? "running" : "stopped",
                    vlc.IsLatestFrameSourceRunning);
                snapshot = null;
                return false;
            }

            if (!IsLatestPlateFrameFresh(snapshot, DateTime.UtcNow))
            {
                source = "OnDemandSnapshot";
                var refreshStopwatch = Stopwatch.StartNew();
                if (vlc.TryRefreshLatestFrame(
                    VlcPreviewController.LatestPlateFrameRefreshTimeoutMs,
                    VlcPreviewController.LatestPlateFrameMaxAgeMs,
                    out var refreshed, out var refreshedBySnapshot2) &&
                    IsLatestPlateFrameFresh(refreshed, DateTime.UtcNow) &&
                    IsLatestPlateFrameSessionCurrent(key, session, vlc, requestId))
                {
                    snapshot = refreshed;
                    source = refreshedBySnapshot2 ? "OnDemandSnapshot" : "Cache";
                    frameAgeMs = GetFrameAgeMs(snapshot);
                    return true;
                }

                // 仅对“已有合法旧帧但本次刷新未及时产出”的 frame_stale 做一次有限容错。
                // 重试前后都重新确认同一 session/generation/player 仍有效，避免 Stop 或
                // 终端切换后的旧请求把新会话的帧误返回给 DLL。
                if (retryCount < VlcPreviewController.LatestPlateFrameMaxRetries &&
                    vlc.LatestFrameFailure ==
                        VlcPreviewController.LatestFrameFailureNotReady &&
                    IsLatestPlateFrameSessionCurrent(key, session, vlc, requestId))
                {
                    var elapsedMs = (int)Math.Min(int.MaxValue,
                        refreshStopwatch.ElapsedMilliseconds);
                    var remainingMs = VlcPreviewController.LatestPlateFrameRetryBudgetMs -
                        elapsedMs;
                    if (remainingMs > 0)
                    {
                        Thread.Sleep(Math.Min(
                            VlcPreviewController.LatestPlateFrameRetryDelayMs,
                            remainingMs));

                        if (IsLatestPlateFrameSessionCurrent(key, session, vlc, requestId))
                        {
                            retryCount = 1;
                            elapsedMs = (int)Math.Min(int.MaxValue,
                                refreshStopwatch.ElapsedMilliseconds);
                            remainingMs = VlcPreviewController.LatestPlateFrameRetryBudgetMs -
                                elapsedMs;
                            if (remainingMs > 0 &&
                                vlc.TryRefreshLatestFrame(
                                    Math.Min(
                                        VlcPreviewController.LatestPlateFrameRefreshTimeoutMs,
                                        remainingMs),
                                    VlcPreviewController.LatestPlateFrameMaxAgeMs,
                                    out var retried, out var retriedBySnapshot) &&
                                IsLatestPlateFrameFresh(retried, DateTime.UtcNow) &&
                                IsLatestPlateFrameSessionCurrent(key, session, vlc, requestId))
                            {
                                snapshot = retried;
                                source = retriedBySnapshot
                                    ? "OnDemandSnapshot" : "Cache";
                                frameAgeMs = GetFrameAgeMs(snapshot);
                                return true;
                            }
                        }
                    }
                }

                errorCode = "frame_stale";
                errorMessage = "车牌视频帧已过期或视频已断开";
                frameAgeMs = GetFrameAgeMs(snapshot);
                LogLatestFrameLookupDiagnostic(resType, requestId, key,
                    "FreshnessValidation", errorCode, true, true, true, true,
                    IsFrameStructurallyValid(snapshot), GetFrameAgeMs(snapshot),
                    session.Generation, vlc.IsRunning ? "running" : "stopped",
                    vlc.IsLatestFrameSourceRunning);
                snapshot = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(source))
                source = "Cache";
            frameAgeMs = GetFrameAgeMs(snapshot);
            return true;
        }

        private bool IsLatestPlateFrameSessionCurrent(string key,
            PreviewSession expectedSession, VlcPreviewController expectedPlayer,
            string requestId)
        {
            if (!_sessions.TryGetValue(key, out var currentSession) ||
                currentSession == null ||
                !ReferenceEquals(currentSession, expectedSession) ||
                !ReferenceEquals(currentSession.Player, expectedPlayer))
                return false;

            return currentSession.Generation == expectedSession.Generation &&
                   string.Equals(currentSession.RequestId, requestId,
                       StringComparison.Ordinal) &&
                   currentSession.IsRunning &&
                   expectedPlayer.IsLatestFrameSourceRunning;
        }

        private static void LogLatestFrameLookupDiagnostic(
            PreviewResourceType resType, string requestId, string cacheKey,
            string stage, string errorCode, bool sessionFound,
            bool requestIdMatched, bool frameStateFound, bool lastGoodFrameFound,
            bool frameValid, long frameAgeMs, long generation,
            string playerState, bool producerStatus)
        {
            var plateCode = PlateCodeForLatestFrame(resType);
            Logger.TryLogRateLimited(
                "LatestFrameLookup|" + resType + "|" + errorCode,
                LogModules.PlateCapture, "警告",
                "LatestFrameDiagnostic " +
                "RouteMatched=true RouteDispatch=binary " +
                $"PlateInput={plateCode.ToLowerInvariant()} " +
                $"NormalizedPlate={plateCode} " +
                $"RequestId={FormatRequestId(requestId)} " +
                $"SessionFound={sessionFound.ToString().ToLowerInvariant()} " +
                $"RequestIdMatched={requestIdMatched.ToString().ToLowerInvariant()} " +
                $"FrameStateFound={frameStateFound.ToString().ToLowerInvariant()} " +
                $"LastGoodFrameFound={lastGoodFrameFound.ToString().ToLowerInvariant()} " +
                $"FrameValid={frameValid.ToString().ToLowerInvariant()} " +
                $"FrameAgeMs={frameAgeMs} Generation={generation} " +
                $"PlayerState={playerState} CacheKey={cacheKey} " +
                $"ProducerStatus={producerStatus.ToString().ToLowerInvariant()} " +
                $"Stage={stage} Error={errorCode}");
        }

        private static string PlateCodeForLatestFrame(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.PlateCJ: return "CJ";
                case PreviewResourceType.PlateRJ2: return "RJ2";
                case PreviewResourceType.PlateRJ3: return "RJ3";
                default: return "unknown";
            }
        }

        private static bool IsFrameStructurallyValid(LatestPlateFrameSnapshot snapshot)
        {
            return snapshot != null && snapshot.Jpeg != null &&
                   snapshot.Jpeg.Length > 0 && snapshot.Width > 0 &&
                   snapshot.Height > 0 &&
                   string.Equals(snapshot.Format, "jpeg",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static long GetFrameAgeMs(LatestPlateFrameSnapshot snapshot)
        {
            if (snapshot == null)
                return -1;
            var ageMs = (DateTime.UtcNow - snapshot.CapturedUtc).TotalMilliseconds;
            return ageMs < 0 ? 0 : (long)ageMs;
        }

        internal static bool IsLatestPlateFrameFresh(
            LatestPlateFrameSnapshot snapshot, DateTime nowUtc)
        {
            if (snapshot == null || snapshot.Jpeg == null || snapshot.Jpeg.Length == 0 ||
                snapshot.Width <= 0 || snapshot.Height <= 0 ||
                !string.Equals(snapshot.Format, "jpeg", StringComparison.OrdinalIgnoreCase))
                return false;

            var ageMs = (nowUtc - snapshot.CapturedUtc).TotalMilliseconds;
            return ageMs >= 0 && ageMs <= VlcPreviewController.LatestPlateFrameMaxAgeMs;
        }

        private static string PreviewUrlCacheKey(PreviewResourceType resType, string terminalBaseUrl)
        {
            return $"{terminalBaseUrl}|{resType}";
        }

        public async Task<string> RequestPreviewUrl(PreviewResourceType resType, string terminalBaseUrl,
            bool forceRefresh = false, string requestId = null,
            bool isRecoveryAttempt = false)
        {
            var cacheKey = PreviewUrlCacheKey(resType, terminalBaseUrl);
            if (!forceRefresh &&
                _previewUrlCache.TryGetValue(cacheKey, out var cached) &&
                !string.IsNullOrEmpty(cached.Url))
            {
                if (IsHttpPreviewUrl(cached.Url))
                {
                    Logger.Debug($"预览URL缓存跳过：资源={ResourceToName(resType)}，{TraceRequest(requestId)}");
                }
                else
                {
                    Logger.Debug($"预览URL缓存命中：资源={ResourceToName(resType)}，{TraceRequest(requestId)}");
                    return cached.Url;
                }
            }

            var previewUrl = await FetchPreviewUrl(resType, terminalBaseUrl, requestId,
                isRecoveryAttempt).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(previewUrl))
                UpdatePreviewUrlCache(resType, terminalBaseUrl, previewUrl);
            return previewUrl;
        }

        private async Task<string> FetchPreviewUrl(PreviewResourceType resType, string terminalBaseUrl,
            string requestId = null, bool isRecoveryAttempt = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var path = ResourceToTerminalPath(resType);
            var terminalRequestId = string.IsNullOrWhiteSpace(requestId)
                ? Guid.NewGuid().ToString("N").Substring(0, 16)
                : requestId;
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(terminalRequestId)}\"}}";

            var (ok, response) = await _terminalClient.PostJsonAsync(
                terminalBaseUrl, path, body, PreviewUrlTimeoutMs,
                isRecoveryAttempt: isRecoveryAttempt).ConfigureAwait(false);
            sw.Stop();
            if (!ok)
            {
                Logger.TryLogRateLimited(
                    "PreviewUrl|failure|" + resType,
                    LogModules.Preview, "警告",
                    $"预览地址请求失败：资源={ResourceToName(resType)}，" +
                    $"RequestId={FormatRequestId(terminalRequestId)}，耗时={sw.ElapsedMilliseconds}ms");
                return null;
            }

            var previewUrl = ResultParser.ExtractPreviewUrl(response);
            Logger.Debug($"预览URL请求完成：资源={ResourceToName(resType)}，request_id={FormatRequestId(terminalRequestId)}，" +
                         $"耗时={sw.ElapsedMilliseconds}ms");
            return previewUrl;
        }

        private void UpdatePreviewUrlCache(PreviewResourceType resType, string terminalBaseUrl, string previewUrl)
        {
            var now = DateTime.UtcNow;
            _previewUrlCache[PreviewUrlCacheKey(resType, terminalBaseUrl)] = new CachedPreviewUrl
            {
                ResourceType = resType,
                TerminalBaseUrl = terminalBaseUrl,
                Url = previewUrl,
                UpdatedUtc = now,
                LastValidatedUtc = now
            };
        }

        private void ClearPreviewUrlCache(PreviewResourceType resType, string terminalBaseUrl)
        {
            _previewUrlCache.TryRemove(PreviewUrlCacheKey(resType, terminalBaseUrl), out _);
        }

        private static bool IsHttpPreviewUrl(string previewUrl)
        {
            return !string.IsNullOrEmpty(previewUrl) &&
                   (previewUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    previewUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTerminalMjpegResource(PreviewResourceType resType)
        {
            return resType == PreviewResourceType.Camera ||
                   resType == PreviewResourceType.Fingerprint ||
                   resType == PreviewResourceType.Iris;
        }

        private void ValidatePreviewUrlCacheCallback(object state)
        {
            if (_disposed)
                return;

            _ = ValidatePreviewUrlCacheAsync();
        }

        private async Task ValidatePreviewUrlCacheAsync()
        {
            if (Interlocked.Exchange(ref _previewUrlValidationRunning, 1) == 1)
                return;

            try
            {
                foreach (var pair in _previewUrlCache)
                {
                    var cached = pair.Value;
                    if (cached == null || string.IsNullOrEmpty(cached.Url) || string.IsNullOrEmpty(cached.TerminalBaseUrl))
                        continue;

                    // HTTP MJPEG URL 可能对应终端侧临时数据流。以申请替换 URL 的方式执行健康检查，
                    // 可能使当前数据流失效，因此仅在读取器报告实际故障后刷新 URL。
                    if (!ShouldValidatePreviewUrl(cached.Url))
                        continue;

                    var latestUrl = await FetchPreviewUrl(cached.ResourceType, cached.TerminalBaseUrl).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(latestUrl))
                    {
                        Logger.TryLogRateLimited(
                            "PreviewUrlValidation|failed|" + cached.TerminalBaseUrl + "|" +
                            ResourceToName(cached.ResourceType),
                            LogModules.Preview, "警告",
                            $"预览URL校验失败，保留旧缓存：资源={ResourceToName(cached.ResourceType)}，" +
                            $"终端={Logger.SanitizeUrlForLog(cached.TerminalBaseUrl)}");
                        continue;
                    }

                    cached.LastValidatedUtc = DateTime.UtcNow;
                    if (!string.Equals(cached.Url, latestUrl, StringComparison.Ordinal))
                    {
                        Logger.TryLogRateLimited(
                            "PreviewUrlValidation|changed|" + cached.TerminalBaseUrl + "|" +
                            ResourceToName(cached.ResourceType),
                            LogModules.Preview, "信息",
                            $"检测到预览URL变更，正在更新缓存：资源={ResourceToName(cached.ResourceType)}，" +
                            $"终端={Logger.SanitizeUrlForLog(cached.TerminalBaseUrl)}");
                        UpdatePreviewUrlCache(cached.ResourceType, cached.TerminalBaseUrl, latestUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.TryLogRateLimited("PreviewUrlValidation|exception", LogModules.Preview,
                    "警告", "预览URL校验异常：" + JsonHelper.ToLogValue(ex.Message));
            }
            finally
            {
                Interlocked.Exchange(ref _previewUrlValidationRunning, 0);
            }
        }

        public async Task<bool> StartPreview(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null, Func<bool> shouldContinue = null,
            string explicitPreviewUrl = null, bool terminalBound = true, bool directRenderTarget = false,
            string requestId = null)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await StartPreviewCore(resType, sessionType, targetHwnd, terminalBaseUrl, localPanel,
                    shouldContinue, explicitPreviewUrl, terminalBound, directRenderTarget, requestId)
                    .ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<bool> StartPreviewCore(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null, Func<bool> shouldContinue = null,
            string explicitPreviewUrl = null, bool terminalBound = true, bool directRenderTarget = false,
            string requestId = null)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var key = SessionKey(resType, sessionType);
            Logger.Debug($"预览启动开始：资源={ResourceToName(resType)}，会话={SessionToName(sessionType)}，" +
                         $"目标HWND={FormatHwnd(targetHwnd)}，直绘={directRenderTarget}，{TraceRequest(requestId)}");
            var terminalMjpegResource = sessionType == PreviewSessionType.External &&
                                        IsTerminalMjpegResource(resType);
            var effectiveDirectRenderTarget = directRenderTarget || terminalMjpegResource;
            var allowVlcFallback = !terminalMjpegResource;
            uint ownerProcessId = 0;
            long ownerProcessStartTimeUtcTicks = 0;

            if (shouldContinue != null && !shouldContinue())
                return false;

            if (sessionType == PreviewSessionType.External &&
                !TryGetWindowOwnerIdentity(targetHwnd, out ownerProcessId,
                    out ownerProcessStartTimeUtcTicks))
            {
                Logger.Warn($"外部预览目标HWND无效：资源={ResourceToName(resType)}，" +
                            $"hwnd={FormatHwnd(targetHwnd)}，{TraceRequest(requestId)}");
                return false;
            }

            // 相同目标已在运行时跳过重复启动
            if (_sessions.TryGetValue(key, out var existing) && existing.IsRunning &&
                existing.TargetHwnd == targetHwnd &&
                (sessionType != PreviewSessionType.External || IsExternalHostCurrent(existing)))
            {
                Logger.Debug($"预览启动请求复用现有会话：资源={ResourceToName(resType)}，会话={SessionToName(sessionType)}，" +
                             $"HWND={FormatHwnd(targetHwnd)}，{TraceRequest(requestId)}，现有会话_{TraceRequest(existing.RequestId)}");
                return true;
            }

            // 故障会话在恢复期间仍保留登记。显式启动将取代该恢复流程，因此必须先终止旧代次。
            if (existing != null)
                await StopPreviewCore(resType, sessionType, preserveRestartInfo: false).ConfigureAwait(false);

            // 终端绑定资源从选定终端请求 URL；车道级车牌相机使用 Proxy 公共配置中的显式 URL。
            var urlTick = totalSw.ElapsedMilliseconds;
            var rtspUrl = !string.IsNullOrWhiteSpace(explicitPreviewUrl)
                ? explicitPreviewUrl
                : await RequestPreviewUrl(resType, terminalBaseUrl, requestId: requestId).ConfigureAwait(false);
            var urlElapsed = totalSw.ElapsedMilliseconds - urlTick;
            if (string.IsNullOrEmpty(rtspUrl))
            {
                Logger.Error($"获取预览URL失败：资源={ResourceToName(resType)}，{TraceRequest(requestId)}");
                return false;
            }

            if (shouldContinue != null && !shouldContinue())
                return false;

            // 确定父窗口句柄
            IntPtr parentHwnd;
            if (sessionType == PreviewSessionType.External && targetHwnd != IntPtr.Zero)
            {
                parentHwnd = targetHwnd;
            }
            else if (localPanel != null && localPanel.Handle != IntPtr.Zero)
            {
                parentHwnd = localPanel.Handle;
            }
            else
            {
                Logger.Error($"无效的HWND：{SessionToName(sessionType)} {ResourceToName(resType)}，" +
                             $"hwnd={FormatHwnd(targetHwnd)}，{TraceRequest(requestId)}");
                return false;
            }

            // 获取视频源尺寸
            var (srcW, srcH, swap) = GetSourceDimensions(resType);
            var isHttpPreview = IsHttpPreviewUrl(rtspUrl);
            if (!terminalMjpegResource && !isHttpPreview && string.IsNullOrWhiteSpace(explicitPreviewUrl))
                await WarmupPreviewStreamIfNeeded(resType, rtspUrl, parentHwnd, srcW, srcH, swap, requestId).ConfigureAwait(false);

            if (shouldContinue != null && !shouldContinue())
                return false;

            // HTTP MJPEG 使用专用低延迟读取器，其他协议继续使用 VLC 链路。
            IPreviewController player = null;
            var playTick = totalSw.ElapsedMilliseconds;
            var description = BuildTraceDescription($"{ResourceToName(resType)} {SessionToName(sessionType)}", requestId);
            player = await StartPreviewPlayerAsync(key, description, rtspUrl, parentHwnd, srcW, srcH, swap,
                isHttpPreview, effectiveDirectRenderTarget, allowVlcFallback, requestId,
                captureLatestFrame: IsPlateResource(resType))
                .ConfigureAwait(false);
            var playElapsed = totalSw.ElapsedMilliseconds - playTick;
            var ok2 = player != null && player.IsRunning;

            if (!ok2)
            {
                if (player != null)
                    await ReleasePlayerAsync(key, player, preserveMjpegWorker: false).ConfigureAwait(false);
                else
                    await CleanupMjpegWorkerForRequestAsync(key, requestId).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(explicitPreviewUrl))
                {
                    Logger.Error($"预览播放失败：{ResourceToName(resType)}，会话={SessionToName(sessionType)}，" +
                                 $"{TraceRequest(requestId)}");
                    return false;
                }

                ClearPreviewUrlCache(resType, terminalBaseUrl);
                var retryUrl = await RequestPreviewUrl(resType, terminalBaseUrl,
                    forceRefresh: true, requestId: requestId).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(retryUrl) && (shouldContinue == null || shouldContinue()))
                {
                    rtspUrl = retryUrl;
                    isHttpPreview = IsHttpPreviewUrl(rtspUrl);
                    if (!terminalMjpegResource && !isHttpPreview)
                        await WarmupPreviewStreamIfNeeded(resType, rtspUrl, parentHwnd, srcW, srcH, swap, requestId).ConfigureAwait(false);
                    playTick = totalSw.ElapsedMilliseconds;
                    player = await StartPreviewPlayerAsync(key, description, rtspUrl, parentHwnd, srcW, srcH, swap,
                        isHttpPreview, effectiveDirectRenderTarget, allowVlcFallback, requestId,
                        captureLatestFrame: IsPlateResource(resType))
                        .ConfigureAwait(false);
                    playElapsed = totalSw.ElapsedMilliseconds - playTick;
                    ok2 = player != null && player.IsRunning;
                    if (ok2)
                        goto PreviewStarted;

                    if (player != null)
                        await ReleasePlayerAsync(key, player, preserveMjpegWorker: false).ConfigureAwait(false);
                    else
                        await CleanupMjpegWorkerForRequestAsync(key, requestId).ConfigureAwait(false);
                }
                var playerPipeline = allowVlcFallback
                    ? (isHttpPreview ? "MJPEG+VLC回退" : "VLC")
                    : "仅MJPEG";
                Logger.Error($"预览播放失败明细：资源={ResourceToName(resType)}，会话={SessionToName(sessionType)}，" +
                             $"播放链路={playerPipeline}，获取地址耗时={urlElapsed}ms，播放耗时={playElapsed}ms，" +
                             $"总耗时={totalSw.ElapsedMilliseconds}ms，{TraceRequest(requestId)}");
                Logger.Error($"预览播放失败：{ResourceToName(resType)}，{TraceRequest(requestId)}");
                return false;
            }

        PreviewStarted:
            if (shouldContinue != null && !shouldContinue())
            {
                await ReleasePlayerAsync(key, player, preserveMjpegWorker: true).ConfigureAwait(false);
                return false;
            }

            var session = new PreviewSession
            {
                Player = player,
                TargetHwnd = targetHwnd,
                LocalPanel = localPanel,
                ResourceType = resType,
                SessionType = sessionType,
                HostHwnd = parentHwnd,
                TerminalBaseUrl = terminalBaseUrl,
                ShouldContinue = shouldContinue,
                Generation = Interlocked.Increment(ref _sessionGeneration),
                OwnerProcessId = ownerProcessId,
                OwnerProcessStartTimeUtcTicks = ownerProcessStartTimeUtcTicks,
                RequestId = requestId,
                ExplicitPreviewUrl = explicitPreviewUrl,
                TerminalBound = terminalBound,
                DirectRenderTarget = effectiveDirectRenderTarget
            };
            _sessions[key] = session;
            _restartInfo[key] = PreviewRestartInfo.FromSession(session);
            AttachPlayerFaultHandler(key, session, player);
            totalSw.Stop();
            Logger.Debug($"预览启动明细：资源={ResourceToName(resType)}，会话={SessionToName(sessionType)}，" +
                         $"hwnd={FormatHwnd(parentHwnd)}，耗时={totalSw.ElapsedMilliseconds}ms，{TraceRequest(requestId)}");

            var startedMessage = $"{ResourceToName(resType)}预览已启动：" +
                                 $"Operation={PreviewOperationName(resType, true)} " +
                                 $"RequestId={FormatRequestId(requestId)} Result=Success";
            if (sessionType == PreviewSessionType.External)
                Logger.Debug(startedMessage);
            else
                Logger.Info(startedMessage);
            return true;
        }

        private async Task CleanupMjpegWorkerForRequestAsync(string key, string requestId)
        {
            if (!_mjpegWorkers.TryGetValue(key, out var worker) || worker == null)
                return;

            if (!string.Equals(worker.RequestId, requestId, StringComparison.Ordinal))
            {
                Logger.Warn($"预览失败清理跳过：会话={key}，当前Worker请求ID={FormatRequestId(worker.RequestId)}，" +
                            $"目标请求ID={FormatRequestId(requestId)}");
                return;
            }

            await ReleasePlayerAsync(key, worker, preserveMjpegWorker: false).ConfigureAwait(false);
        }

        private async Task<IPreviewController> StartPreviewPlayerAsync(string key, string description, string previewUrl,
            IntPtr parentHwnd, int srcW, int srcH, bool swap, bool isHttpPreview,
            bool directRenderTarget = false, bool allowVlcFallback = true, string requestId = null,
            bool captureLatestFrame = false, bool isRecoveryAttempt = false)
        {
            if (isHttpPreview && !await WaitForMjpegWorkerCleanupAsync(key, VlcPlayTimeoutMs)
                    .ConfigureAwait(false))
            {
                if (isRecoveryAttempt)
                    Logger.Warn($"预览Worker仍在释放，暂缓创建新Worker：会话={key}，{TraceRequest(requestId)}");
                else
                    Logger.Error($"预览Worker仍在释放，拒绝创建重叠Worker：会话={key}，{TraceRequest(requestId)}");
                return null;
            }

            if (isHttpPreview)
            {
                MjpegPreviewController mjpegPlayer;
                if (_mjpegWorkers.TryGetValue(key, out mjpegPlayer))
                {
                    var switched = await mjpegPlayer.SwitchStreamAsync(previewUrl, parentHwnd,
                        VlcPlayTimeoutMs, requestId).ConfigureAwait(false);
                    if (switched && mjpegPlayer.IsRunning)
                    {
                        Logger.Debug($"预览播放器选择：复用MJPEG，会话={key}，{TraceRequest(requestId)}");
                        return mjpegPlayer;
                    }
                }
                else
                {
                    mjpegPlayer = await MjpegPreviewController.StartAsync(description, previewUrl, parentHwnd,
                        srcW, srcH, swap, visible: true, timeoutMs: VlcPlayTimeoutMs,
                        directRenderTarget: directRenderTarget, requestId: requestId).ConfigureAwait(false);
                    if (mjpegPlayer != null && mjpegPlayer.IsRunning)
                        _mjpegWorkers[key] = mjpegPlayer;
                }

                if (mjpegPlayer != null && mjpegPlayer.IsRunning)
                {
                    Logger.Debug($"预览播放器选择：新建MJPEG，会话={key}，{TraceRequest(requestId)}");
                    return mjpegPlayer;
                }

                if (!allowVlcFallback)
                {
                    if (isRecoveryAttempt)
                        Logger.Warn($"HTTP MJPEG恢复暂未成功，已等待后续重试：{description}，{TraceRequest(requestId)}");
                    else
                        Logger.Error($"HTTP MJPEG预览失败，已禁止VLC回退：{description}，{TraceRequest(requestId)}");
                    return null;
                }

                Logger.Debug($"HTTP MJPEG预览失败，回退到VLC：{description}，{TraceRequest(requestId)}");
            }
            else if (!allowVlcFallback)
            {
                if (isRecoveryAttempt)
                    Logger.Warn($"外部MJPEG恢复暂未成功，地址类型不支持回退：{description}，{TraceRequest(requestId)}");
                else
                    Logger.Error($"外部MJPEG预览URL不是HTTP地址，已禁止VLC回退：{description}，地址={Logger.SanitizeUrlForLog(previewUrl)}，{TraceRequest(requestId)}");
                return null;
            }

            var vlcPlayer = await VlcPreviewController.StartAsync(description, previewUrl, parentHwnd,
                _networkCachingMs, _liveCachingMs, _rtspTransport, srcW, srcH, swap,
                visible: true, timeoutMs: VlcPlayTimeoutMs,
                directRenderTarget: directRenderTarget, requestId: requestId,
                captureLatestFrame: captureLatestFrame).ConfigureAwait(false);
            if (vlcPlayer != null && vlcPlayer.IsRunning)
                Logger.Debug($"预览播放器选择：VLC，会话={key}，{TraceRequest(requestId)}");

            return vlcPlayer;
        }

        private async Task<bool> WaitForMjpegWorkerCleanupAsync(string key, int timeoutMs)
        {
            var tasks = new List<Task>();
            foreach (var pair in _deferredMjpegDisposals)
            {
                if (pair.Key.StartsWith(key + "#", StringComparison.Ordinal) && pair.Value != null)
                    tasks.Add(pair.Value);
            }

            if (tasks.Count == 0)
                return true;

            var all = Task.WhenAll(tasks);
            var completed = await Task.WhenAny(all, Task.Delay(Math.Max(1, timeoutMs)))
                .ConfigureAwait(false);
            return completed == all && !_deferredMjpegDisposals.Keys.Any(
                value => value.StartsWith(key + "#", StringComparison.Ordinal));
        }

        private void AttachPlayerFaultHandler(string key, PreviewSession session, IPreviewController player)
        {
            if (player is MjpegPreviewController mjpeg)
            {
                AttachMjpegFaultHandler(key, session, mjpeg);
                return;
            }
            if (player is VlcPreviewController vlc)
            {
                vlc.SetStreamFaultHandler((faulted, reason) =>
                    ScheduleVlcRecovery(key, faulted, reason));
            }
        }

        private void ScheduleVlcRecovery(string key, VlcPreviewController faultedPlayer, string reason)
        {
            if (_disposed || Volatile.Read(ref _stopping) != 0 ||
                _lifetimeCts.IsCancellationRequested)
                return;

            var recoveryKey = key + "#vlc";
            if (!_activeRecoveries.TryAdd(recoveryKey, 0))
                return;

            Logger.Warn($"VLC预览流故障，启动受控恢复：会话={key}，错误={reason}");
            if (_taskTracker != null)
            {
                var accepted = _taskTracker.TryRun(async () =>
                {
                    try
                    {
                        await RecoverVlcPreviewAsync(key, faultedPlayer, reason,
                            _lifetimeCts.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _activeRecoveries.TryRemove(recoveryKey, out _);
                    }
                }, "preview_vlc_recovery_" + recoveryKey);
                if (!accepted)
                {
                    _activeRecoveries.TryRemove(recoveryKey, out _);
                    Logger.Warn($"VLC预览恢复未启动，后台任务容量已满：会话={key}");
                }
                return;
            }

            var task = Task.Run(() => RecoverVlcPreviewAsync(key, faultedPlayer, reason,
                _lifetimeCts.Token));
            _recoveryTasks[recoveryKey] = task;
            task.ContinueWith(completedTask =>
            {
                _activeRecoveries.TryRemove(recoveryKey, out _);
                _recoveryTasks.TryRemove(recoveryKey, out _);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task RecoverVlcPreviewAsync(string key, VlcPreviewController faultedPlayer,
            string faultReason, CancellationToken cancellationToken)
        {
            int failedAttempts = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_sessions.TryGetValue(key, out var current))
                        return;

                    if (!ReferenceEquals(current.Player, faultedPlayer))
                        return;
                    if (!CanContinueRecovery(current))
                        return;

                    var recoveryHwnd = current.SessionType == PreviewSessionType.External
                        ? current.TargetHwnd
                        : current.HostHwnd;
                    if (recoveryHwnd == IntPtr.Zero || !IsWindow(recoveryHwnd) ||
                        (current.SessionType == PreviewSessionType.External && !IsExternalHostCurrent(current)))
                    {
                        Logger.Warn($"VLC预览恢复取消，目标HWND已失效：会话={key}，hwnd={FormatHwnd(recoveryHwnd)}");
                        RemoveSessionIfCurrent(key, current);
                        await ReleasePlayerAsync(key, faultedPlayer, preserveMjpegWorker: false).ConfigureAwait(false);
                        return;
                    }

                    string previewUrl;
                    if (!string.IsNullOrWhiteSpace(current.ExplicitPreviewUrl))
                    {
                        previewUrl = SelectRecoveryPreviewUrl(current.ExplicitPreviewUrl, null);
                        Logger.Debug($"VLC预览恢复复用显式URL：会话={JsonHelper.ToLogValue(key)}，" +
                                     $"使用原第三方HWND={FormatHwnd(recoveryHwnd)}");
                    }
                    else
                    {
                        ClearPreviewUrlCache(current.ResourceType, current.TerminalBaseUrl);
                        previewUrl = SelectRecoveryPreviewUrl(null,
                            await RequestPreviewUrl(current.ResourceType, current.TerminalBaseUrl,
                                forceRefresh: true, requestId: current.RequestId,
                                isRecoveryAttempt: true).ConfigureAwait(false));
                    }

                    if (!CanContinueRecovery(current))
                        return;

                    if (!string.IsNullOrEmpty(previewUrl))
                    {
                        var (srcW, srcH, swap) = GetSourceDimensions(current.ResourceType);
                        var isHttpPreview = IsHttpPreviewUrl(previewUrl);
                        var description = BuildTraceDescription(
                            $"{ResourceToName(current.ResourceType)} {SessionToName(current.SessionType)}", current.RequestId);
                        var replacement = await StartPreviewPlayerAsync(key, description, previewUrl,
                            recoveryHwnd, srcW, srcH, swap, isHttpPreview,
                            directRenderTarget: true, allowVlcFallback: false,
                            current.RequestId,
                            captureLatestFrame: IsPlateResource(current.ResourceType),
                            isRecoveryAttempt: true).ConfigureAwait(false);
                        if (replacement != null && replacement.IsRunning)
                        {
                            current.Generation = Interlocked.Increment(ref _sessionGeneration);
                            current.Player = replacement;
                            _sessions[key] = current;
                            _restartInfo[key] = PreviewRestartInfo.FromSession(current);
                            AttachPlayerFaultHandler(key, current, replacement);
                            Logger.Info($"{ResourceToName(current.ResourceType)}预览已恢复：" +
                                        $"Operation={RecoveryOperationName(current.ResourceType)} " +
                                        $"RequestId={FormatRequestId(current.RequestId)} " +
                                        $"Result=Success Attempt={failedAttempts + 1}");
                            return;
                        }

                        if (replacement != null)
                            await ReleasePlayerAsync(key, replacement, preserveMjpegWorker: true).ConfigureAwait(false);
                    }

                    failedAttempts++;
                    if (failedAttempts <= 3)
                        Logger.TryLogRateLimited(
                            "VLC|recovery|" + key + "|retry",
                            LogModules.Preview, "警告",
                            $"VLC预览恢复中：会话={JsonHelper.ToLogValue(key)}，累计尝试次数={failedAttempts}，" +
                            $"错误={JsonHelper.ToLogValue(faultReason)}");
                    else if (failedAttempts % 10 == 0)
                        Logger.TryLogRateLimited(
                            "VLC|recovery|" + key + "|waiting",
                            LogModules.Preview, "警告",
                            $"VLC预览仍在等待网络恢复：会话={JsonHelper.ToLogValue(key)}，" +
                            $"累计尝试次数={failedAttempts}");
                    await DelayVlcRecoveryAsync(failedAttempts, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    failedAttempts++;
                    if (failedAttempts <= 3)
                        Logger.TryLogRateLimited(
                            "VLC|recovery|" + key + "|exception",
                            LogModules.Preview, "警告",
                            $"VLC预览恢复异常：会话={JsonHelper.ToLogValue(key)}，" +
                            $"累计尝试次数={failedAttempts}，错误={JsonHelper.ToLogValue(ex.Message)}");
                    await DelayVlcRecoveryAsync(failedAttempts, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 前 3 次快速重试（1s/2s/5s），之后降频为固定低频，直到网络恢复后重建成功。
        /// </summary>
        private static async Task DelayVlcRecoveryAsync(int failedAttempts, CancellationToken cancellationToken)
        {
            var delayMs = failedAttempts <= 3
                ? GetRecoveryDelayMs(failedAttempts)
                : VlcRecoveryLowFrequencyDelayMs;
            try
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void AttachMjpegFaultHandler(string key, PreviewSession session, IPreviewController player)
        {
            var mjpegPlayer = player as MjpegPreviewController;
            if (mjpegPlayer == null)
                return;

            var generation = session.Generation;
            mjpegPlayer.SetFailureHandler((faultedPlayer, failureKind, reason) =>
            {
                if (failureKind == MjpegFailureKind.RenderTargetFailure)
                {
                    ScheduleMjpegRenderTargetFailure(key, generation, faultedPlayer, reason);
                    return;
                }

                ScheduleMjpegRecovery(key, generation, faultedPlayer, failureKind, reason);
            });
        }

        private void ScheduleMjpegRecovery(string key, long generation,
            MjpegPreviewController faultedPlayer, MjpegFailureKind failureKind,
            string reason)
        {
            if (_disposed || Volatile.Read(ref _stopping) != 0 ||
                _lifetimeCts.IsCancellationRequested)
                return;

            var episode = RecordMjpegFailure(key, failureKind, reason);
            var recoveryKey = GetMjpegRecoveryKey(key);
            if (!_activeRecoveries.TryAdd(recoveryKey, 0))
                return;

            _sessions.TryGetValue(key, out var currentSession);
            var resourceName = currentSession == null
                ? "预览"
                : ResourceToName(currentSession.ResourceType);
            var recoveryOperation = currentSession == null
                ? "RecoverPreview"
                : RecoveryOperationName(currentSession.ResourceType);
            Logger.Warn($"{resourceName}预览出现{FailureKindDisplayName(failureKind)}，正在自动恢复：" +
                        $"Operation={recoveryOperation} RequestId={FormatRequestId(currentSession?.RequestId)} " +
                        $"RecoveryEpisodeId={episode.Id}，错误={JsonHelper.ToLogValue(reason)}");
            if (_taskTracker != null)
            {
                var accepted = _taskTracker.TryRun(async () =>
                {
                    try
                    {
                        await RecoverMjpegPreviewAsync(key, generation, faultedPlayer,
                            failureKind, reason, episode.Id, _lifetimeCts.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _activeRecoveries.TryRemove(recoveryKey, out _);
                    }
                }, "preview_mjpeg_recovery_" + recoveryKey);
                if (!accepted)
                {
                    _activeRecoveries.TryRemove(recoveryKey, out _);
                    Logger.Warn($"HTTP MJPEG恢复未启动，后台任务容量已满：会话={key}");
                }
                return;
            }

            var task = Task.Run(() => RecoverMjpegPreviewAsync(key, generation, faultedPlayer,
                failureKind, reason, episode.Id, _lifetimeCts.Token));
            _recoveryTasks[recoveryKey] = task;
            task.ContinueWith(completedTask =>
            {
                _activeRecoveries.TryRemove(recoveryKey, out _);
                _recoveryTasks.TryRemove(recoveryKey, out _);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private void ScheduleMjpegRenderTargetFailure(string key, long generation,
            MjpegPreviewController faultedPlayer, string reason)
        {
            if (_disposed || Volatile.Read(ref _stopping) != 0 ||
                _lifetimeCts.IsCancellationRequested)
                return;

            var recoveryKey = GetMjpegRecoveryKey(key);
            if (!_activeRecoveries.TryAdd(recoveryKey, 0))
                return;

            _sessions.TryGetValue(key, out var currentSession);
            var resourceName = currentSession == null
                ? "预览"
                : ResourceToName(currentSession.ResourceType);
            var recoveryOperation = currentSession == null
                ? "RecoverPreview"
                : RecoveryOperationName(currentSession.ResourceType);
            Logger.Warn($"{resourceName}预览绘制目标失效，停止当前预览会话：" +
                        $"Operation={recoveryOperation} RequestId={FormatRequestId(currentSession?.RequestId)} " +
                        $"Result=Stopped ErrorCode=render_target_invalid，原因={JsonHelper.ToLogValue(reason)}");
            if (_taskTracker != null)
            {
                var accepted = _taskTracker.TryRun(async () =>
                {
                    try
                    {
                        await FailRecoveryAndReleaseAsync(key, generation, faultedPlayer, reason)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _activeRecoveries.TryRemove(recoveryKey, out _);
                    }
                }, "preview_mjpeg_render_target_failure_" + recoveryKey);
                if (!accepted)
                {
                    _activeRecoveries.TryRemove(recoveryKey, out _);
                    Logger.Warn($"HTTP MJPEG绘制目标故障未能及时清理：会话={key}");
                }
                return;
            }

            var task = Task.Run(() => FailRecoveryAndReleaseAsync(
                key, generation, faultedPlayer, reason));
            _recoveryTasks[recoveryKey] = task;
            task.ContinueWith(completedTask =>
            {
                _activeRecoveries.TryRemove(recoveryKey, out _);
                _recoveryTasks.TryRemove(recoveryKey, out _);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static string GetMjpegRecoveryKey(string key)
        {
            return key + "#mjpeg";
        }

        private MjpegRecoveryEpisode RecordMjpegFailure(string key,
            MjpegFailureKind failureKind, string reason)
        {
            var episode = _mjpegRecoveryEpisodes.GetOrAdd(key, value =>
                new MjpegRecoveryEpisode
                {
                    Id = Interlocked.Increment(ref _nextMjpegRecoveryEpisodeId),
                    FirstFailureUtc = DateTime.UtcNow
                });
            lock (episode)
            {
                var now = DateTime.UtcNow;
                if (episode.FailureCount == 0)
                    episode.FirstFailureUtc = now;
                episode.LastFailureUtc = now;
                episode.FailureCount++;
                episode.LastFailureKind = failureKind;
                episode.LastError = string.IsNullOrWhiteSpace(reason)
                    ? "MJPEG故障"
                    : reason;
            }
            return episode;
        }

        private bool TryAdvanceMjpegRecoveryAttempt(string key, long episodeId,
            MjpegFailureKind failureKind, string reason, out int attempt)
        {
            attempt = 0;
            if (!_mjpegRecoveryEpisodes.TryGetValue(key, out var episode) || episode == null)
                return false;

            lock (episode)
            {
                if (episode.Id != episodeId || episode.Attempt >= MaxMjpegRecoveryAttempts)
                    return false;

                episode.Attempt++;
                episode.Attempt = Math.Min(episode.Attempt, MaxMjpegRecoveryAttempts);
                episode.LastFailureKind = failureKind;
                episode.LastError = string.IsNullOrWhiteSpace(reason)
                    ? episode.LastError
                    : reason;
                attempt = episode.Attempt;
                return true;
            }
        }

        private void CompleteMjpegRecoveryEpisode(string key, long episodeId)
        {
            if (!_mjpegRecoveryEpisodes.TryGetValue(key, out var episode) || episode == null)
                return;

            lock (episode)
            {
                if (episode.Id == episodeId)
                    _mjpegRecoveryEpisodes.TryRemove(key, out _);
            }
        }

        private long GetMjpegRecoveryDurationMs(string key, long episodeId)
        {
            if (!_mjpegRecoveryEpisodes.TryGetValue(key, out var episode) ||
                episode == null)
                return 0;

            lock (episode)
            {
                if (episode.Id != episodeId || episode.FirstFailureUtc == default(DateTime))
                    return 0;
                return Math.Max(0, (long)(DateTime.UtcNow - episode.FirstFailureUtc)
                    .TotalMilliseconds);
            }
        }

        private bool IsMjpegRecoveryAttemptExhausted(string key, long episodeId)
        {
            if (!_mjpegRecoveryEpisodes.TryGetValue(key, out var episode) || episode == null)
                return true;

            lock (episode)
                return episode.Id != episodeId || episode.Attempt >= MaxMjpegRecoveryAttempts;
        }

        private async Task RecoverMjpegPreviewAsync(string key, long generation,
            MjpegPreviewController faultedPlayer, MjpegFailureKind failureKind,
            string faultReason, long episodeId, CancellationToken cancellationToken)
        {
            PreviewSession recoverySession = null;
            var lastFailureKind = failureKind;
            var lastFailureReason = string.IsNullOrWhiteSpace(faultReason)
                ? "MJPEG流不可用"
                : faultReason;

            while (!_disposed && !cancellationToken.IsCancellationRequested)
            {
                int attempt;
                if (!TryAdvanceMjpegRecoveryAttempt(key, episodeId,
                    lastFailureKind, lastFailureReason, out attempt))
                {
                    var recoveryResource = recoverySession == null
                        ? "未知"
                        : ResourceToName(recoverySession.ResourceType);
                    var recoveryOperation = recoverySession == null
                        ? "RecoverPreview"
                        : RecoveryOperationName(recoverySession.ResourceType);
                    var recoveryRequestId = recoverySession == null
                        ? null
                        : recoverySession.RequestId;
                    var recoveryDurationMs = GetMjpegRecoveryDurationMs(key, episodeId);
                    Logger.Error($"{recoveryResource}预览恢复失败：连续{MaxMjpegRecoveryAttempts}次尝试均未成功，" +
                                 $"Operation={recoveryOperation} RequestId={FormatRequestId(recoveryRequestId)} " +
                                 $"RecoveryEpisodeId={episodeId} Attempts={MaxMjpegRecoveryAttempts} " +
                                 $"Result=Failed ErrorCode=recovery_exhausted DurationMs={recoveryDurationMs}");
                    await FailRecoveryAndReleaseAsync(key, generation, faultedPlayer,
                        lastFailureReason).ConfigureAwait(false);
                    CompleteMjpegRecoveryEpisode(key, episodeId);
                    return;
                }

                IPreviewController replacement = null;
                MjpegPreviewController mjpegReplacement = null;
                PreviewSession current = null;
                var invalidTarget = false;
                var committed = false;

                try
                {
                    await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    if (_disposed || !_sessions.TryGetValue(key, out current) ||
                        current.Generation != generation)
                        return;

                    if (recoverySession == null)
                    {
                        if (!ReferenceEquals(current.Player, faultedPlayer))
                            return;

                        recoverySession = current;
                        current.Player = null;
                        await faultedPlayer.PauseAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                    }
                    else if (!ReferenceEquals(current, recoverySession) || current.Player != null)
                    {
                        return;
                    }

                    if (!CanContinueRecovery(current))
                        return;

                    var recoveryHwnd = current.SessionType == PreviewSessionType.External
                        ? current.TargetHwnd
                        : current.HostHwnd;
                    if (recoveryHwnd == IntPtr.Zero || !IsWindow(recoveryHwnd) ||
                        (current.SessionType == PreviewSessionType.External && !IsExternalHostCurrent(current)))
                    {
                        invalidTarget = true;
                        lastFailureKind = MjpegFailureKind.RenderTargetFailure;
                        lastFailureReason = "预览窗口已销毁或宿主进程已变更";
                    }
                    else
                    {
                        string previewUrl;
                        if (!string.IsNullOrWhiteSpace(current.ExplicitPreviewUrl))
                        {
                            previewUrl = SelectRecoveryPreviewUrl(current.ExplicitPreviewUrl, null);
                            Logger.Debug($"HTTP MJPEG恢复复用保存的显式URL：会话={JsonHelper.ToLogValue(key)}，" +
                                         $"尝试次数={attempt}，使用原第三方HWND={FormatHwnd(recoveryHwnd)}");
                        }
                        else
                        {
                            ClearPreviewUrlCache(current.ResourceType, current.TerminalBaseUrl);
                            Logger.Debug($"HTTP MJPEG恢复申请新URL：会话={JsonHelper.ToLogValue(key)}，" +
                                         $"尝试次数={attempt}");
                            previewUrl = SelectRecoveryPreviewUrl(null,
                                await RequestPreviewUrl(current.ResourceType, current.TerminalBaseUrl,
                                    forceRefresh: true, requestId: current.RequestId,
                                    isRecoveryAttempt: true).ConfigureAwait(false));
                        }

                        if (!CanContinueRecovery(current))
                            return;

                        if (!string.IsNullOrEmpty(previewUrl))
                        {
                            var (srcW, srcH, swap) = GetSourceDimensions(current.ResourceType);
                            var isHttpPreview = IsHttpPreviewUrl(previewUrl);
                            var terminalMjpegResource = current.SessionType == PreviewSessionType.External &&
                                                        IsTerminalMjpegResource(current.ResourceType);
                            if (!terminalMjpegResource && !isHttpPreview)
                                await WarmupPreviewStreamIfNeeded(current.ResourceType, previewUrl,
                                    recoveryHwnd, srcW, srcH, swap, current.RequestId).ConfigureAwait(false);

                            var description = BuildTraceDescription(
                                $"{ResourceToName(current.ResourceType)} {SessionToName(current.SessionType)}", current.RequestId);
                            var effectiveDirectRenderTarget = current.DirectRenderTarget || terminalMjpegResource;
                            var allowVlcFallback = !terminalMjpegResource;
                            replacement = await StartPreviewPlayerAsync(key, description, previewUrl,
                                recoveryHwnd, srcW, srcH, swap, isHttpPreview,
                                effectiveDirectRenderTarget, allowVlcFallback, current.RequestId,
                                captureLatestFrame: IsPlateResource(current.ResourceType),
                                isRecoveryAttempt: true).ConfigureAwait(false);
                            mjpegReplacement = replacement as MjpegPreviewController;

                            if (mjpegReplacement != null)
                            {
                                // 在真实绘制完成前保持原会话代次，避免首帧到达就制造新的恢复窗口。
                                current.Player = mjpegReplacement;
                                _sessions[key] = current;
                                _restartInfo[key] = PreviewRestartInfo.FromSession(current);
                                AttachMjpegFaultHandler(key, current, mjpegReplacement);
                            }
                            else if (replacement != null && replacement.IsRunning)
                            {
                                current.Generation = Interlocked.Increment(ref _sessionGeneration);
                                current.Player = replacement;
                                _sessions[key] = current;
                                _restartInfo[key] = PreviewRestartInfo.FromSession(current);
                                AttachPlayerFaultHandler(key, current, replacement);
                                committed = true;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    lastFailureKind = MjpegFailureKind.StreamFailure;
                    lastFailureReason = ex.Message;
                    Logger.TryLogRateLimited(
                        "Mjpeg|Recovery|" + key + "|Exception",
                        LogModules.Preview, "警告",
                        $"{(recoverySession == null ? "预览" : ResourceToName(recoverySession.ResourceType))}" +
                        $"恢复过程出现异常：Operation={(recoverySession == null ? "RecoverPreview" : RecoveryOperationName(recoverySession.ResourceType))} " +
                        $"RequestId={FormatRequestId(recoverySession?.RequestId)} RecoveryEpisodeId={episodeId} " +
                        $"Attempt={attempt}，错误={JsonHelper.ToLogValue(ex.Message)}");
                }
                finally
                {
                    _operationLock.Release();
                }

                if (invalidTarget)
                {
                    Logger.Warn($"{(recoverySession == null ? "预览" : ResourceToName(recoverySession.ResourceType))}" +
                                $"预览目标窗口已失效，停止当前会话：" +
                                $"Operation={(recoverySession == null ? "RecoverPreview" : RecoveryOperationName(recoverySession.ResourceType))} " +
                                $"RequestId={FormatRequestId(recoverySession?.RequestId)} " +
                                $"Result=Stopped ErrorCode=invalid_target_hwnd");
                    await FailRecoveryAndReleaseAsync(key, generation, faultedPlayer,
                        lastFailureReason).ConfigureAwait(false);
                    CompleteMjpegRecoveryEpisode(key, episodeId);
                    return;
                }

                if (committed)
                {
                    var recoveryDurationMs = GetMjpegRecoveryDurationMs(key, episodeId);
                    CompleteMjpegRecoveryEpisode(key, episodeId);
                    Logger.Info($"{ResourceToName(recoverySession.ResourceType)}预览已恢复：共尝试{attempt}次，" +
                                $"Operation={RecoveryOperationName(recoverySession.ResourceType)} " +
                                $"RequestId={FormatRequestId(recoverySession.RequestId)} " +
                                $"RecoveryEpisodeId={episodeId} Attempts={attempt} " +
                                $"Result=Success DurationMs={recoveryDurationMs}");
                    return;
                }

                if (mjpegReplacement != null)
                {
                    var readiness = await mjpegReplacement.WaitForRenderedFrameAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (readiness != null && readiness.Succeeded)
                    {
                        var committedPlayer = false;
                        var lockTaken = false;
                        try
                        {
                            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                            lockTaken = true;
                            if (!_disposed && _sessions.TryGetValue(key, out var readySession) &&
                                ReferenceEquals(readySession, recoverySession) &&
                                readySession.Generation == generation &&
                                ReferenceEquals(readySession.Player, mjpegReplacement))
                            {
                                readySession.Generation = Interlocked.Increment(ref _sessionGeneration);
                                _sessions[key] = readySession;
                                _restartInfo[key] = PreviewRestartInfo.FromSession(readySession);
                                // 首次等待期间的处理器捕获旧 generation；提交后重新绑定到新代次。
                                AttachMjpegFaultHandler(key, readySession, mjpegReplacement);
                                committedPlayer = true;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                _operationLock.Release();
                        }

                        if (committedPlayer)
                        {
                            var recoveryDurationMs = GetMjpegRecoveryDurationMs(key, episodeId);
                            CompleteMjpegRecoveryEpisode(key, episodeId);
                            Logger.Info($"{ResourceToName(recoverySession.ResourceType)}预览已恢复：共尝试{attempt}次，" +
                                        $"Operation={RecoveryOperationName(recoverySession.ResourceType)} " +
                                        $"RequestId={FormatRequestId(recoverySession.RequestId)} " +
                                        $"RecoveryEpisodeId={episodeId} Attempts={attempt} " +
                                        $"Result=Success DurationMs={recoveryDurationMs}");
                            return;
                        }

                        return;
                    }

                    lastFailureKind = readiness == null
                        ? MjpegFailureKind.StreamFailure
                        : readiness.FailureKind;
                    lastFailureReason = readiness == null || string.IsNullOrWhiteSpace(readiness.FailureReason)
                        ? "MJPEG绘制未就绪"
                        : readiness.FailureReason;

                    if (lastFailureKind == MjpegFailureKind.RenderTargetFailure)
                    {
                        await FailRecoveryAndReleaseAsync(key, generation, mjpegReplacement,
                            lastFailureReason).ConfigureAwait(false);
                        CompleteMjpegRecoveryEpisode(key, episodeId);
                        return;
                    }
                }

                if (replacement != null)
                {
                    var lockTaken = false;
                    try
                    {
                        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                        lockTaken = true;
                        if (_sessions.TryGetValue(key, out var failedSession) &&
                            ReferenceEquals(failedSession, recoverySession) &&
                            failedSession.Generation == generation &&
                            ReferenceEquals(failedSession.Player, replacement))
                        {
                            failedSession.Player = null;
                            await ReleasePlayerAsync(key, replacement,
                                preserveMjpegWorker: true).ConfigureAwait(false);
                        }
                        else
                        {
                            return;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            _operationLock.Release();
                    }
                }

                RecordMjpegFailure(key, lastFailureKind, lastFailureReason);
                if (GetMjpegRecoveryFailureLevel(attempt) == "错误")
                {
                    var recoveryDurationMs = GetMjpegRecoveryDurationMs(key, episodeId);
                    var recoveryResource = recoverySession == null
                        ? "未知"
                        : ResourceToName(recoverySession.ResourceType);
                    var recoveryOperation = recoverySession == null
                        ? "RecoverPreview"
                        : RecoveryOperationName(recoverySession.ResourceType);
                    var recoveryRequestId = recoverySession == null
                        ? null
                        : recoverySession.RequestId;
                    Logger.Error($"{recoveryResource}预览恢复失败：连续{MaxMjpegRecoveryAttempts}次尝试均未成功，" +
                                 $"Operation={recoveryOperation} RequestId={FormatRequestId(recoveryRequestId)} " +
                                 $"RecoveryEpisodeId={episodeId} Attempts={attempt} " +
                                 $"Result=Failed ErrorCode=recovery_exhausted DurationMs={recoveryDurationMs}");
                    await FailRecoveryAndReleaseAsync(key, generation, faultedPlayer,
                        lastFailureReason).ConfigureAwait(false);
                    CompleteMjpegRecoveryEpisode(key, episodeId);
                    return;
                }

                var delayMs = GetRecoveryDelayMs(attempt);
                Logger.TryLogRateLimited(
                    "Mjpeg|Recovery|" + key + "|Retry",
                    LogModules.Preview, "警告",
                    $"{(recoverySession == null ? "预览" : ResourceToName(recoverySession.ResourceType))}" +
                    $"预览暂未恢复，{delayMs}ms后继续尝试：Operation={(recoverySession == null ? "RecoverPreview" : RecoveryOperationName(recoverySession.ResourceType))} " +
                    $"RequestId={FormatRequestId(recoverySession?.RequestId)} RecoveryEpisodeId={episodeId} " +
                    $"Attempt={attempt}，故障类别={FailureKindDisplayName(lastFailureKind)}");
                try
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task FailRecoveryAndReleaseAsync(string key, long generation,
            MjpegPreviewController faultedPlayer, string faultReason)
        {
            PreviewResourceType resourceType = default(PreviewResourceType);
            PreviewSessionType sessionType = default(PreviewSessionType);
            string requestId = null;
            Action<PreviewResourceType, string, string> failureHandler = null;

            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_sessions.TryGetValue(key, out var current) &&
                    current.Generation == generation &&
                    (current.Player == null || ReferenceEquals(current.Player, faultedPlayer)))
                {
                    resourceType = current.ResourceType;
                    sessionType = current.SessionType;
                    requestId = current.RequestId;
                    failureHandler = _externalPreviewFailureHandler;
                    _sessions.TryRemove(key, out _);
                    _restartInfo.TryRemove(key, out _);
                }

                if (faultedPlayer != null)
                {
                    try
                    {
                        await ReleasePlayerAsync(key, faultedPlayer, preserveMjpegWorker: false)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"预览运行时播放器释放异常：会话={key}，错误={ex.Message}", ex);
                    }
                }
            }
            finally
            {
                _operationLock.Release();
            }

            if (failureHandler != null && sessionType == PreviewSessionType.External &&
                !string.IsNullOrWhiteSpace(requestId))
            {
                try
                {
                    failureHandler(resourceType, requestId,
                        string.IsNullOrWhiteSpace(faultReason) ? "MJPEG预览恢复失败" : faultReason);
                }
                catch (Exception ex)
                {
                    Logger.Error($"预览运行时失败通知异常：会话={key}，request_id={FormatRequestId(requestId)}，" +
                                 $"错误={ex.Message}", ex);
                }
            }
        }

        private static bool CanContinueRecovery(PreviewSession session)
        {
            if (session.ShouldContinue == null)
                return true;

            try
            {
                return session.ShouldContinue();
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG恢复条件检查失败: {ex.Message}");
                return false;
            }
        }

        private void RemoveSessionIfCurrent(string key, PreviewSession expected)
        {
            if (_sessions.TryGetValue(key, out var current) && ReferenceEquals(current, expected))
            {
                _sessions.TryRemove(key, out _);
                _restartInfo.TryRemove(key, out _);
            }
        }

        internal static int GetRecoveryDelayMs(int failedAttempts)
        {
            if (failedAttempts <= 1) return 1000;
            if (failedAttempts == 2) return 2000;
            if (failedAttempts == 3) return 5000;
            return 10000;
        }

        internal static string GetMjpegRecoveryFailureLevel(int attempt)
        {
            return attempt >= MaxMjpegRecoveryAttempts ? "错误" : "警告";
        }

        internal static bool ShouldValidatePreviewUrl(string previewUrl)
        {
            return !IsHttpPreviewUrl(previewUrl);
        }

        internal static string SelectRecoveryPreviewUrl(string explicitPreviewUrl,
            string requestedPreviewUrl)
        {
            return !string.IsNullOrWhiteSpace(explicitPreviewUrl)
                ? explicitPreviewUrl
                : requestedPreviewUrl;
        }

        private void ValidateExternalHostsCallback(object state)
        {
            if (_disposed)
                return;

            _ = ValidateExternalHostsAsync();
        }

        private async Task ValidateExternalHostsAsync()
        {
            if (Interlocked.Exchange(ref _externalHostValidationRunning, 1) != 0)
                return;

            try
            {
                await _operationLock.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
                try
                {
                    foreach (var pair in _sessions)
                    {
                        var session = pair.Value;
                        if (session == null || session.SessionType != PreviewSessionType.External ||
                            IsExternalHostCurrent(session))
                            continue;

                        if (!_sessions.TryGetValue(pair.Key, out var current) ||
                            !ReferenceEquals(current, session))
                            continue;

                        Logger.Warn($"外部预览宿主进程已退出或HWND已被复用，正在停止失效会话：" +
                                    $"资源={ResourceToName(session.ResourceType)}，HWND={FormatHwnd(session.TargetHwnd)}，" +
                                    $"宿主进程ID={session.OwnerProcessId}");
                        await StopPreviewCore(session.ResourceType, session.SessionType,
                            preserveRestartInfo: false).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _operationLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Proxy 正在关闭
            }
            catch (Exception ex)
            {
                Logger.Warn($"外部预览HWND校验失败：{ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _externalHostValidationRunning, 0);
            }
        }

        internal static bool IsExternalHostCurrent(PreviewSession session)
        {
            if (session == null || session.TargetHwnd == IntPtr.Zero || !IsWindow(session.TargetHwnd))
                return false;

            if (!TryGetWindowOwnerIdentity(session.TargetHwnd, out var processId,
                out var processStartTimeUtcTicks))
                return false;

            if (session.OwnerProcessId != 0 && processId != session.OwnerProcessId)
                return false;

            return session.OwnerProcessStartTimeUtcTicks == 0 || processStartTimeUtcTicks == 0 ||
                   session.OwnerProcessStartTimeUtcTicks == processStartTimeUtcTicks;
        }

        internal static bool TryGetWindowOwnerIdentity(IntPtr hwnd, out uint processId,
            out long processStartTimeUtcTicks)
        {
            processId = 0;
            processStartTimeUtcTicks = 0;
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) ||
                GetWindowThreadProcessId(hwnd, out processId) == 0 || processId == 0)
                return false;

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                    processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            }
            catch
            {
                // HWND 和 PID 联合校验仍可防止多数失效窗口句柄复用场景
            }
            return true;
        }

        public bool StopPreview(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            if (_uiContext != null && SynchronizationContext.Current == _uiContext)
            {
                _ = StopPreviewAsync(resType, sessionType);
                return true;
            }

            return StopPreviewAsync(resType, sessionType).GetAwaiter().GetResult();
        }

        public async Task<bool> StopPreviewAsync(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await StopPreviewCore(resType, sessionType, preserveRestartInfo: false).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        internal async Task<bool> CleanupFailedPreviewAsync(PreviewResourceType resType,
            PreviewSessionType sessionType, string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return false;

            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var key = SessionKey(resType, sessionType);
                if (_sessions.TryGetValue(key, out var session))
                {
                    if (!string.Equals(session.RequestId, requestId, StringComparison.Ordinal))
                        return false;

                    return await StopPreviewCore(resType, sessionType,
                        preserveRestartInfo: false).ConfigureAwait(false);
                }

                if (_mjpegWorkers.TryGetValue(key, out var worker) && worker != null &&
                    string.Equals(worker.RequestId, requestId, StringComparison.Ordinal))
                {
                    await ReleasePlayerAsync(key, worker, preserveMjpegWorker: false)
                        .ConfigureAwait(false);
                    _restartInfo.TryRemove(key, out _);
                    return true;
                }

                return false;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<bool> StopPreviewCore(PreviewResourceType resType, PreviewSessionType sessionType, bool preserveRestartInfo)
        {
            var key = SessionKey(resType, sessionType);
            if (_sessions.TryRemove(key, out var session))
            {
                await ReleasePlayerAsync(key, session.Player, preserveRestartInfo).ConfigureAwait(false);
                if (!preserveRestartInfo)
                    _restartInfo.TryRemove(key, out _);
                var stoppedMessage = $"{ResourceToName(resType)}预览已停止：" +
                                     $"Operation={PreviewOperationName(resType, false)} " +
                                     $"RequestId={FormatRequestId(session.RequestId)} Result=Success，" +
                                     $"会话={SessionToName(sessionType)}，HWND={FormatHwnd(session.HostHwnd)}";
                if (sessionType == PreviewSessionType.External)
                    Logger.Debug(stoppedMessage);
                else
                    Logger.Info(stoppedMessage);
                return true;
            }

            if (!preserveRestartInfo && _mjpegWorkers.TryGetValue(key, out var orphanWorker))
            {
                await ReleasePlayerAsync(key, orphanWorker, preserveMjpegWorker: false)
                    .ConfigureAwait(false);
                _restartInfo.TryRemove(key, out _);
                return true;
            }
            return false;
        }

        public bool IsPreviewRunning(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            var key = SessionKey(resType, sessionType);
            return _sessions.TryGetValue(key, out var session) && session.IsRunning;
        }

        public void StopAll()
        {
            if (_uiContext != null && SynchronizationContext.Current == _uiContext)
            {
                _ = StopAllAsync(preserveRestartInfo: false);
                return;
            }

            StopAllAsync(preserveRestartInfo: false).GetAwaiter().GetResult();
        }

        public async Task StopAllAsync(bool preserveRestartInfo = false)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopAllCore(preserveRestartInfo).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// 终端切换前只停止绑定当前终端的预览。
        /// 车道级车牌相机不绑定终端 1/2，必须保持现有会话和 HWND 渲染不变。
        /// </summary>
        internal async Task StopTerminalBoundPreviewsForSwitchAsync()
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var sessions = new List<KeyValuePair<string, PreviewSession>>(_sessions);
                var stopTasks = new List<Task>();
                foreach (var pair in sessions)
                {
                    if (!ShouldStopForTerminalSwitch(pair.Value))
                        continue;

                    if (_sessions.TryRemove(pair.Key, out var active) && active.Player != null)
                        stopTasks.Add(ReleasePlayerAsync(pair.Key, active.Player,
                            preserveMjpegWorker: true));
                }

                if (stopTasks.Count > 0)
                    await Task.WhenAll(stopTasks).ConfigureAwait(false);

                Logger.Debug($"终端切换已停止 {stopTasks.Count} 个终端绑定预览，车牌预览保持运行");
            }
            finally
            {
                _operationLock.Release();
            }
        }

        internal static bool ShouldStopForTerminalSwitch(PreviewSession session)
        {
            return session != null && session.TerminalBound;
        }

        private async Task StopAllCore(bool preserveRestartInfo)
        {
            var sessions = new List<KeyValuePair<string, PreviewSession>>(_sessions);
            var stopTasks = new List<Task>();
            foreach (var pair in sessions)
            {
                stopTasks.Add(ReleasePlayerAsync(pair.Key, pair.Value.Player, preserveRestartInfo));
            }

            if (stopTasks.Count > 0)
                await Task.WhenAll(stopTasks).ConfigureAwait(false);

            _sessions.Clear();
            if (!preserveRestartInfo)
            {
                _restartInfo.Clear();
                await DisposeRemainingMjpegWorkersAsync().ConfigureAwait(false);
            }
            Logger.Debug("所有预览已停止");
        }

        public async Task RestartPreviewsOnTerminalSwitch(string newTerminalBaseUrl,
            Func<bool> shouldContinue = null)
        {
            if (!CanContinuePreviewRecovery(shouldContinue))
                return;

            try
            {
                await _operationLock.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                if (!CanContinuePreviewRecovery(shouldContinue))
                    return;

                var restartList = new List<PreviewRestartInfo>();
                foreach (var session in _restartInfo.Values)
                {
                    if (session.TerminalBound)
                        restartList.Add(session);
                }
                restartList.Sort(CompareRestartPriority);

                // 车道级车牌相机不绑定终端 1/2。逐个停止并重启终端会话，保持车牌本地及外部会话运行。
                var stopTasks = new List<Task>();
                foreach (var session in restartList)
                {
                    var key = SessionKey(session.ResourceType, session.SessionType);
                    if (_sessions.TryRemove(key, out var active) && active.Player != null)
                        stopTasks.Add(ReleasePlayerAsync(key, active.Player, preserveMjpegWorker: true));
                }
                if (stopTasks.Count > 0)
                    await Task.WhenAll(stopTasks).ConfigureAwait(false);

                await Task.Delay(VlcReleaseSettleMs, _lifetimeCts.Token).ConfigureAwait(false);

                if (!CanContinuePreviewRecovery(shouldContinue))
                    return;

                if (restartList.Count == 0)
                {
                    Logger.Debug("无活跃预览需要重启");
                    return;
                }

                Logger.Info($"终端切换，后台恢复 {restartList.Count} 个预览");

                for (int i = 0; i < restartList.Count; i++)
                {
                    var info = restartList[i];

                    if (!CanContinuePreviewRecovery(shouldContinue))
                        return;

                    var previewSw = System.Diagnostics.Stopwatch.StartNew();
                    var resourceName = ResourceToName(info.ResourceType);
                    Logger.Debug($"预览后台恢复开始：资源={resourceName}，会话={SessionToName(info.SessionType)}，" +
                                 $"{TraceRequest(info.RequestId)}");
                    try
                    {
                        var started = await StartPreviewCore(info.ResourceType, info.SessionType, info.TargetHwnd,
                            newTerminalBaseUrl, info.LocalPanel, shouldContinue,
                            info.ExplicitPreviewUrl, info.TerminalBound, info.DirectRenderTarget, info.RequestId)
                            .ConfigureAwait(false);
                        if (started)
                            Logger.Debug($"预览后台恢复完成：资源={resourceName}，会话={SessionToName(info.SessionType)}，" +
                                         $"耗时={previewSw.ElapsedMilliseconds}ms，{TraceRequest(info.RequestId)}");
                        else
                            Logger.Warn($"预览后台恢复未完成：资源={resourceName}，会话={SessionToName(info.SessionType)}，" +
                                        $"耗时={previewSw.ElapsedMilliseconds}ms，{TraceRequest(info.RequestId)}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"预览后台恢复失败：资源={resourceName}，会话={SessionToName(info.SessionType)}，" +
                                     $"耗时={previewSw.ElapsedMilliseconds}ms，{TraceRequest(info.RequestId)}，错误={ex.Message}");
                    }

                    if (i < restartList.Count - 1)
                        await Task.Delay(VlcReleaseSettleMs, _lifetimeCts.Token)
                            .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 服务关闭期间出现此情况属于正常流程
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task ReleasePlayerAsync(string key, IPreviewController player,
            bool preserveMjpegWorker)
        {
            var mjpegPlayer = player as MjpegPreviewController;
            if (mjpegPlayer != null && _mjpegWorkers.TryGetValue(key, out var pooled) &&
                ReferenceEquals(mjpegPlayer, pooled))
            {
                if (preserveMjpegWorker)
                {
                    var paused = await mjpegPlayer.PauseAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                    if (paused)
                        return;

                    Logger.Error($"预览Worker暂停超时，改为完整释放：会话={key}，" +
                                 $"request_id={FormatRequestId(mjpegPlayer.RequestId)}");
                }

                await mjpegPlayer.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                RemoveOrDeferMjpegWorker(key, mjpegPlayer);
                return;
            }

            if (player != null)
                await player.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);

            if (_mjpegWorkers.TryGetValue(key, out var orphanWorker) && orphanWorker != null)
            {
                if (preserveMjpegWorker)
                {
                    var paused = await orphanWorker.PauseAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                    if (paused)
                        return;

                    Logger.Error($"预览Worker暂停超时，改为完整释放：会话={key}，" +
                                 $"request_id={FormatRequestId(orphanWorker.RequestId)}");
                }

                await orphanWorker.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                RemoveOrDeferMjpegWorker(key, orphanWorker);
            }
        }

        private async Task DisposeRemainingMjpegWorkersAsync()
        {
            var workers = new List<KeyValuePair<string, MjpegPreviewController>>(_mjpegWorkers);
            var tasks = new List<Task>(workers.Count);
            foreach (var pair in workers)
            {
                if (pair.Value != null)
                    tasks.Add(ReleasePlayerAsync(pair.Key, pair.Value, preserveMjpegWorker: false));
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);

            await WaitForDeferredMjpegDisposalsAsync(VlcStopTimeoutMs).ConfigureAwait(false);
        }

        private void RemoveOrDeferMjpegWorker(string key, MjpegPreviewController worker)
        {
            if (worker == null)
                return;

            if (worker.ResourcesDisposed)
            {
                if (_mjpegWorkers.TryGetValue(key, out var current) &&
                    ReferenceEquals(current, worker))
                    _mjpegWorkers.TryRemove(key, out _);
                return;
            }

            var cleanupKey = key + "#" + RuntimeHelpers.GetHashCode(worker);
            if (_deferredMjpegDisposals.ContainsKey(cleanupKey))
                return;

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_deferredMjpegDisposals.TryAdd(cleanupKey, completion.Task))
                return;

            _ = FinalizeDeferredMjpegWorkerAsync(key, worker, cleanupKey, completion);
        }

        private async Task FinalizeDeferredMjpegWorkerAsync(string key,
            MjpegPreviewController worker, string cleanupKey,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await worker.WaitForExitAsync().ConfigureAwait(false);
                await worker.DisposeAsync(0).ConfigureAwait(false);
                if (_mjpegWorkers.TryGetValue(key, out var current) &&
                    ReferenceEquals(current, worker))
                    _mjpegWorkers.TryRemove(key, out _);
                Logger.Debug($"预览Worker延迟释放完成：会话={key}，request_id={FormatRequestId(worker.RequestId)}");
                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error($"预览Worker延迟释放失败：会话={key}，request_id={FormatRequestId(worker.RequestId)}，错误={ex.Message}", ex);
                completion.TrySetResult(false);
            }
            finally
            {
                _deferredMjpegDisposals.TryRemove(cleanupKey, out _);
            }
        }

        private async Task WaitForDeferredMjpegDisposalsAsync(int timeoutMs)
        {
            var tasks = new List<Task>(_deferredMjpegDisposals.Values);
            if (tasks.Count == 0)
                return;

            var all = Task.WhenAll(tasks);
            await Task.WhenAny(all, Task.Delay(Math.Max(1, timeoutMs))).ConfigureAwait(false);
        }

        private bool CanContinuePreviewRecovery(Func<bool> shouldContinue)
        {
            return !_disposed && Volatile.Read(ref _stopping) == 0 &&
                   !_lifetimeCts.IsCancellationRequested &&
                   (shouldContinue == null || shouldContinue());
        }

        internal int ActiveSessionCount => _sessions.Count;
        internal int ActiveRecoveryCount => _activeRecoveries.Count;
        internal int MjpegWorkerCount => _mjpegWorkers.Count;

        internal void BeginShutdown()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
                return;

            try { _lifetimeCts.Cancel(); } catch (ObjectDisposedException) { }
            try { _previewUrlValidationTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
            try { _externalHostValidationTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
        }

        internal async Task ShutdownAsync(int timeoutMs)
        {
            if (Volatile.Read(ref _shutdownCompleted) != 0)
                return;

            BeginShutdown();
            await StopAllAsync(preserveRestartInfo: false).ConfigureAwait(false);

            var recoveryTasks = new List<Task>(_recoveryTasks.Values);
            if (recoveryTasks.Count > 0)
            {
                var all = Task.WhenAll(recoveryTasks);
                await Task.WhenAny(all, Task.Delay(Math.Max(1, timeoutMs)))
                    .ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _shutdownCompleted, 1);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BeginShutdown();
            if (Volatile.Read(ref _shutdownCompleted) == 0)
            {
                try { ShutdownAsync(5000).GetAwaiter().GetResult(); }
            catch (Exception ex) { Logger.Error("预览管理器关闭异常", ex); }
            }
            ScheduleFinalResourceCleanup();
            // 此处不释放 _operationLock。共享关闭时限耗尽后，延迟的播放器清理仍可能执行 finally/Release。
            // 进程仅持有一个 PreviewManager 实例，保留该小型同步对象可避免与延迟清理任务发生竞争。
        }

        private void ScheduleFinalResourceCleanup()
        {
            if (_activeRecoveries.IsEmpty &&
                Volatile.Read(ref _previewUrlValidationRunning) == 0 &&
                Volatile.Read(ref _externalHostValidationRunning) == 0)
            {
                DisposeFinalResources();
                return;
            }

            if (Interlocked.Exchange(ref _deferredFinalCleanupScheduled, 1) == 0)
            {
                Logger.Warn("预览管理器仍有后台校验或恢复任务，定时器和生命周期资源将在任务退出后释放");
                _ = DisposeFinalResourcesWhenIdleAsync();
            }
        }

        private async Task DisposeFinalResourcesWhenIdleAsync()
        {
            try
            {
                while (!_activeRecoveries.IsEmpty ||
                       Volatile.Read(ref _previewUrlValidationRunning) != 0 ||
                       Volatile.Read(ref _externalHostValidationRunning) != 0)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"预览管理器等待后台任务退出异常：{ex.Message}");
            }

            DisposeFinalResources();
        }

        private void DisposeFinalResources()
        {
            if (Interlocked.Exchange(ref _finalResourcesDisposed, 1) != 0)
                return;

            _previewUrlValidationTimer?.Dispose();
            _externalHostValidationTimer?.Dispose();
            _lifetimeCts.Dispose();
        }

        private async Task WarmupPreviewStreamIfNeeded(PreviewResourceType resType, string rtspUrl,
            IntPtr parentHwnd, int srcW, int srcH, bool swap, string requestId = null)
        {
            var key = resType.ToString();
            if (!_coldStartWarmups.TryAdd(key, 1))
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            VlcPreviewController warmupPlayer = null;
            var ok = false;
            try
            {
                warmupPlayer = await VlcPreviewController.StartAsync(
                    BuildTraceDescription($"{ResourceToName(resType)} 预热", requestId),
                    rtspUrl, parentHwnd, _networkCachingMs, _liveCachingMs, _rtspTransport,
                    srcW, srcH, swap, visible: false, timeoutMs: VlcPlayTimeoutMs).ConfigureAwait(false);
                ok = warmupPlayer != null && warmupPlayer.IsRunning;

                if (ok)
                    await Task.Delay(ColdStartWarmupMs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _coldStartWarmups.TryRemove(key, out _);
                Logger.Warn($"首次预览预热异常：资源={ResourceToName(resType)}，{TraceRequest(requestId)}，错误={ex.Message}");
            }
            finally
            {
                if (warmupPlayer != null)
                    await warmupPlayer.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                sw.Stop();
                Logger.Debug($"首次预览预热完成：资源={ResourceToName(resType)}，结果={(ok ? "成功" : "失败")}，" +
                             $"耗时={sw.ElapsedMilliseconds}ms，{TraceRequest(requestId)}");
            }
        }

        private Task RunOnUiAsync(Action action)
        {
            if (_uiContext == null || SynchronizationContext.Current == _uiContext)
            {
                action();
                return Task.FromResult(true);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _uiContext.Post(_ =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Logger.Error($"UI线程预览操作异常: {ex.Message}");
                    tcs.TrySetException(ex);
                }
            }, null);
            return tcs.Task;
        }

        private void PostToUi(Action action)
        {
            if (_uiContext == null || SynchronizationContext.Current == _uiContext)
            {
                action();
                return;
            }

            _uiContext.Post(_ =>
            {
                try { action(); }
                catch (Exception ex) { Logger.Error($"UI线程预览释放异常: {ex.Message}"); }
            }, null);
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
