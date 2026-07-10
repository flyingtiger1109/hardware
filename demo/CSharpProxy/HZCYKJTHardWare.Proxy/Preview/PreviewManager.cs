using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
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
        internal string ExplicitPreviewUrl { get; set; }
        internal bool TerminalBound { get; set; }
        internal bool DirectRenderTarget { get; set; }
    }

    public class PreviewManager : IDisposable
    {
        private const int PreviewUrlTimeoutMs = 5000;
        private const int ColdStartWarmupMs = 800;
        private const int VlcPlayTimeoutMs = 2500;
        private const int VlcStopTimeoutMs = 1500;
        private const int VlcReleaseSettleMs = 500;
        private const int PreviewUrlValidationIntervalMs = 60000;
        private readonly ConcurrentDictionary<string, PreviewSession> _sessions = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, PreviewSession> _restartInfo = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, byte> _coldStartWarmups = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, CachedPreviewUrl> _previewUrlCache = new ConcurrentDictionary<string, CachedPreviewUrl>();
        private readonly ConcurrentDictionary<string, byte> _activeRecoveries = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, Task> _recoveryTasks = new ConcurrentDictionary<string, Task>();
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private readonly TerminalClient _terminalClient;
        private readonly int _networkCachingMs;
        private readonly int _liveCachingMs;
        private readonly string _rtspTransport;
        private readonly SynchronizationContext _uiContext;  // Captured from UI thread at construction
        private readonly System.Threading.Timer _previewUrlValidationTimer;
        private readonly System.Threading.Timer _externalHostValidationTimer;
        private int _previewUrlValidationRunning;
        private int _externalHostValidationRunning;
        private long _sessionGeneration;
        private bool _disposed;

        private sealed class CachedPreviewUrl
        {
            public PreviewResourceType ResourceType { get; set; }
            public string TerminalBaseUrl { get; set; }
            public string Url { get; set; }
            public DateTime UpdatedUtc { get; set; }
            public DateTime LastValidatedUtc { get; set; }
        }

        public PreviewManager(TerminalClient terminalClient)
        {
            _terminalClient = terminalClient;
            _uiContext = SynchronizationContext.Current;  // Capture UI thread sync context (same as Delphi MainThreadID)
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

        private static int CompareRestartPriority(PreviewSession left, PreviewSession right)
        {
            var result = GetRestartPriority(left).CompareTo(GetRestartPriority(right));
            if (result != 0)
                return result;

            result = left.SessionType.CompareTo(right.SessionType);
            if (result != 0)
                return result;

            return left.TargetHwnd.ToInt64().CompareTo(right.TargetHwnd.ToInt64());
        }

        private static int GetRestartPriority(PreviewSession session)
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
                    return (1920, 1080, false);
                default: return (640, 480, false);
            }
        }

        private static string PreviewUrlCacheKey(PreviewResourceType resType, string terminalBaseUrl)
        {
            return $"{terminalBaseUrl}|{resType}";
        }

        public async Task<string> RequestPreviewUrl(PreviewResourceType resType, string terminalBaseUrl, bool forceRefresh = false)
        {
            var cacheKey = PreviewUrlCacheKey(resType, terminalBaseUrl);
            if (!forceRefresh &&
                _previewUrlCache.TryGetValue(cacheKey, out var cached) &&
                !string.IsNullOrEmpty(cached.Url))
            {
                if (IsHttpPreviewUrl(cached.Url))
                {
                    Logger.Debug($"预览URL缓存跳过：resource={ResourceToName(resType)}");
                }
                else
                {
                    Logger.Debug($"预览URL缓存命中：resource={ResourceToName(resType)}");
                    return cached.Url;
                }
            }

            var previewUrl = await FetchPreviewUrl(resType, terminalBaseUrl).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(previewUrl))
                UpdatePreviewUrlCache(resType, terminalBaseUrl, previewUrl);
            return previewUrl;
        }

        private async Task<string> FetchPreviewUrl(PreviewResourceType resType, string terminalBaseUrl)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var path = ResourceToTerminalPath(resType);
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var body = $"{{\"request_id\":\"{requestId}\"}}";

            var (ok, response) = await _terminalClient.PostJsonAsync(terminalBaseUrl, path, body, PreviewUrlTimeoutMs).ConfigureAwait(false);
            sw.Stop();
            if (!ok)
            {
                Logger.Warn($"预览URL请求失败：resource={ResourceToName(resType)}，terminal={terminalBaseUrl}，耗时={sw.ElapsedMilliseconds}ms");
                return null;
            }

            var previewUrl = ResultParser.ExtractPreviewUrl(response);
            Logger.Debug($"预览URL请求完成：resource={ResourceToName(resType)}，耗时={sw.ElapsedMilliseconds}ms");
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

                    // HTTP MJPEG URLs can represent terminal-side temporary streams. Asking for a
                    // replacement URL as a health check may invalidate the stream currently in use.
                    // Their URL is refreshed only after the reader reports an actual stream fault.
                    if (!ShouldValidatePreviewUrl(cached.Url))
                        continue;

                    var latestUrl = await FetchPreviewUrl(cached.ResourceType, cached.TerminalBaseUrl).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(latestUrl))
                    {
                        Logger.Warn($"Preview URL validation failed, keeping old cache: resource={ResourceToName(cached.ResourceType)}, terminal={cached.TerminalBaseUrl}");
                        continue;
                    }

                    cached.LastValidatedUtc = DateTime.UtcNow;
                    if (!string.Equals(cached.Url, latestUrl, StringComparison.Ordinal))
                    {
                        Logger.Warn($"Preview URL changed, updating cache: resource={ResourceToName(cached.ResourceType)}, terminal={cached.TerminalBaseUrl}");
                        UpdatePreviewUrlCache(cached.ResourceType, cached.TerminalBaseUrl, latestUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Preview URL validation error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _previewUrlValidationRunning, 0);
            }
        }

        public async Task<bool> StartPreview(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null, Func<bool> shouldContinue = null,
            string explicitPreviewUrl = null, bool terminalBound = true, bool directRenderTarget = false)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await StartPreviewCore(resType, sessionType, targetHwnd, terminalBaseUrl, localPanel,
                    shouldContinue, explicitPreviewUrl, terminalBound, directRenderTarget)
                    .ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<bool> StartPreviewCore(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null, Func<bool> shouldContinue = null,
            string explicitPreviewUrl = null, bool terminalBound = true, bool directRenderTarget = false)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var key = SessionKey(resType, sessionType);
            uint ownerProcessId = 0;
            long ownerProcessStartTimeUtcTicks = 0;

            if (shouldContinue != null && !shouldContinue())
                return false;

            if (sessionType == PreviewSessionType.External &&
                !TryGetWindowOwnerIdentity(targetHwnd, out ownerProcessId,
                    out ownerProcessStartTimeUtcTicks))
            {
                Logger.Warn($"External preview target HWND is invalid: resource={ResourceToName(resType)}, hwnd={targetHwnd}");
                return false;
            }

            // If already running for same target, skip
            if (_sessions.TryGetValue(key, out var existing) && existing.IsRunning &&
                existing.TargetHwnd == targetHwnd &&
                (sessionType != PreviewSessionType.External || IsExternalHostCurrent(existing)))
                return true;

            // A faulted session remains registered while it is recovering. An explicit start
            // supersedes that recovery and must first retire the old generation.
            if (existing != null)
                await StopPreviewCore(resType, sessionType, preserveRestartInfo: false).ConfigureAwait(false);

            // Terminal-bound resources request a URL from the selected terminal. The lane-level
            // plate camera uses the explicit URL from the shared Proxy configuration.
            var urlTick = totalSw.ElapsedMilliseconds;
            var rtspUrl = !string.IsNullOrWhiteSpace(explicitPreviewUrl)
                ? explicitPreviewUrl
                : await RequestPreviewUrl(resType, terminalBaseUrl).ConfigureAwait(false);
            var urlElapsed = totalSw.ElapsedMilliseconds - urlTick;
            if (string.IsNullOrEmpty(rtspUrl))
            {
                Logger.Error($"获取预览URL失败: {ResourceToName(resType)}");
                return false;
            }

            if (shouldContinue != null && !shouldContinue())
                return false;

            // Determine parent HWND
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
                Logger.Error($"无效的HWND: {sessionType} {ResourceToName(resType)}");
                return false;
            }

            // Get source dimensions
            var (srcW, srcH, swap) = GetSourceDimensions(resType);
            var isHttpPreview = IsHttpPreviewUrl(rtspUrl);
            if (!isHttpPreview && string.IsNullOrWhiteSpace(explicitPreviewUrl))
                await WarmupPreviewStreamIfNeeded(resType, rtspUrl, parentHwnd, srcW, srcH, swap).ConfigureAwait(false);

            if (shouldContinue != null && !shouldContinue())
                return false;

            // HTTP MJPEG uses a dedicated low-latency reader. Other protocols keep the VLC path.
            IPreviewController player = null;
            var playTick = totalSw.ElapsedMilliseconds;
            var description = $"{ResourceToName(resType)} {sessionType}";
            player = await StartPreviewPlayerAsync(description, rtspUrl, parentHwnd, srcW, srcH, swap,
                isHttpPreview, directRenderTarget)
                .ConfigureAwait(false);
            var playElapsed = totalSw.ElapsedMilliseconds - playTick;
            var ok2 = player != null && player.IsRunning;

            if (!ok2)
            {
                if (player != null)
                    await player.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(explicitPreviewUrl))
                {
                    Logger.Error($"Preview play failed: {ResourceToName(resType)}");
                    return false;
                }

                ClearPreviewUrlCache(resType, terminalBaseUrl);
                var retryUrl = await RequestPreviewUrl(resType, terminalBaseUrl, forceRefresh: true).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(retryUrl) && (shouldContinue == null || shouldContinue()))
                {
                    rtspUrl = retryUrl;
                    isHttpPreview = IsHttpPreviewUrl(rtspUrl);
                    if (!isHttpPreview)
                        await WarmupPreviewStreamIfNeeded(resType, rtspUrl, parentHwnd, srcW, srcH, swap).ConfigureAwait(false);
                    playTick = totalSw.ElapsedMilliseconds;
                    player = await StartPreviewPlayerAsync(description, rtspUrl, parentHwnd, srcW, srcH, swap,
                        isHttpPreview, directRenderTarget)
                        .ConfigureAwait(false);
                    playElapsed = totalSw.ElapsedMilliseconds - playTick;
                    ok2 = player != null && player.IsRunning;
                    if (ok2)
                        goto PreviewStarted;
                }
                Logger.Error($"Preview play failed detail: resource={ResourceToName(resType)}, session={sessionType}, player={(isHttpPreview ? "mjpeg+vlc_fallback" : "vlc")}, url_elapsed={urlElapsed}ms, play_elapsed={playElapsed}ms, total_elapsed={totalSw.ElapsedMilliseconds}ms");
                Logger.Error($"Preview play failed: {ResourceToName(resType)}");
                return false;
            }

        PreviewStarted:
            if (shouldContinue != null && !shouldContinue())
            {
                await player.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
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
                ExplicitPreviewUrl = explicitPreviewUrl,
                TerminalBound = terminalBound,
                DirectRenderTarget = directRenderTarget
            };
            _sessions[key] = session;
            _restartInfo[key] = session;
            AttachMjpegFaultHandler(key, session, player);
            totalSw.Stop();
            Logger.Debug($"预览启动明细：resource={ResourceToName(resType)}，session={sessionType}，hwnd={parentHwnd}，耗时={totalSw.ElapsedMilliseconds}ms");

            Logger.Info($"预览已启动: {ResourceToName(resType)} {sessionType}");
            return true;
        }

        private async Task<IPreviewController> StartPreviewPlayerAsync(string description, string previewUrl,
            IntPtr parentHwnd, int srcW, int srcH, bool swap, bool isHttpPreview,
            bool directRenderTarget = false)
        {
            if (isHttpPreview)
            {
                var mjpegPlayer = await MjpegPreviewController.StartAsync(description, previewUrl, parentHwnd,
                    srcW, srcH, swap, visible: true, timeoutMs: VlcPlayTimeoutMs).ConfigureAwait(false);
                if (mjpegPlayer != null && mjpegPlayer.IsRunning)
                {
                    Logger.Debug($"预览播放器选择: mjpeg");
                    return mjpegPlayer;
                }

                Logger.Debug($"HTTP MJPEG预览失败，回退到VLC: {description}");
            }

            var vlcPlayer = await VlcPreviewController.StartAsync(description, previewUrl, parentHwnd,
                _networkCachingMs, _liveCachingMs, _rtspTransport, srcW, srcH, swap,
                visible: true, timeoutMs: VlcPlayTimeoutMs,
                directRenderTarget: directRenderTarget).ConfigureAwait(false);
            if (vlcPlayer != null && vlcPlayer.IsRunning)
                Logger.Debug($"预览播放器选择: vlc");

            return vlcPlayer;
        }

        private void AttachMjpegFaultHandler(string key, PreviewSession session, IPreviewController player)
        {
            var mjpegPlayer = player as MjpegPreviewController;
            if (mjpegPlayer == null)
                return;

            var generation = session.Generation;
            mjpegPlayer.SetStreamFaultHandler((faultedPlayer, reason) =>
                ScheduleMjpegRecovery(key, generation, faultedPlayer, reason));
        }

        private void ScheduleMjpegRecovery(string key, long generation,
            MjpegPreviewController faultedPlayer, string reason)
        {
            if (_disposed || _lifetimeCts.IsCancellationRequested)
                return;

            var recoveryKey = key + "#" + generation;
            if (!_activeRecoveries.TryAdd(recoveryKey, 0))
                return;

            Logger.Warn($"HTTP MJPEG流故障，启动受控恢复: session={key}, generation={generation}, error={reason}");
            var task = Task.Run(() => RecoverMjpegPreviewAsync(key, generation, faultedPlayer, _lifetimeCts.Token));
            _recoveryTasks[recoveryKey] = task;
            task.ContinueWith(completedTask =>
            {
                _activeRecoveries.TryRemove(recoveryKey, out _);
                _recoveryTasks.TryRemove(recoveryKey, out _);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task RecoverMjpegPreviewAsync(string key, long generation,
            MjpegPreviewController faultedPlayer, CancellationToken cancellationToken)
        {
            int failedAttempts = 0;
            PreviewSession recoverySession = null;

            while (!_disposed && !cancellationToken.IsCancellationRequested)
            {
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
                    if (_disposed || !_sessions.TryGetValue(key, out var current) ||
                        current.Generation != generation)
                        return;

                    if (recoverySession == null)
                    {
                        if (!ReferenceEquals(current.Player, faultedPlayer))
                            return;

                        recoverySession = current;
                        current.Player = null;
                        await faultedPlayer.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                    }
                    else if (!ReferenceEquals(current, recoverySession) || current.Player != null)
                    {
                        return;
                    }

                    if (!CanContinueRecovery(current))
                        return;

                    if (current.HostHwnd == IntPtr.Zero || !IsWindow(current.HostHwnd) ||
                        (current.SessionType == PreviewSessionType.External && !IsExternalHostCurrent(current)))
                    {
                        Logger.Warn($"HTTP MJPEG恢复取消，目标HWND已失效: session={key}, hwnd={current.HostHwnd}");
                        RemoveSessionIfCurrent(key, current);
                        return;
                    }

                    ClearPreviewUrlCache(current.ResourceType, current.TerminalBaseUrl);
                    var attempt = failedAttempts + 1;
                    Logger.Info($"HTTP MJPEG恢复申请新URL: session={key}, attempt={attempt}");
                    var previewUrl = await RequestPreviewUrl(current.ResourceType, current.TerminalBaseUrl,
                        forceRefresh: true).ConfigureAwait(false);

                    if (!CanContinueRecovery(current))
                        return;

                    if (!string.IsNullOrEmpty(previewUrl))
                    {
                        var (srcW, srcH, swap) = GetSourceDimensions(current.ResourceType);
                        var isHttpPreview = IsHttpPreviewUrl(previewUrl);
                        if (!isHttpPreview)
                            await WarmupPreviewStreamIfNeeded(current.ResourceType, previewUrl,
                                current.HostHwnd, srcW, srcH, swap).ConfigureAwait(false);

                        var description = $"{ResourceToName(current.ResourceType)} {current.SessionType}";
                        var replacement = await StartPreviewPlayerAsync(description, previewUrl,
                            current.HostHwnd, srcW, srcH, swap, isHttpPreview).ConfigureAwait(false);
                        if (replacement != null && replacement.IsRunning)
                        {
                            // A replacement is a new lifecycle generation. If it fails immediately,
                            // its recovery must not be suppressed by the task completing this generation.
                            current.Generation = Interlocked.Increment(ref _sessionGeneration);
                            current.Player = replacement;
                            _sessions[key] = current;
                            _restartInfo[key] = current;
                            AttachMjpegFaultHandler(key, current, replacement);
                            Logger.Info($"HTTP MJPEG预览已恢复: session={key}, attempt={attempt}");
                            return;
                        }

                        if (replacement != null)
                            await replacement.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                    }

                    failedAttempts++;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    failedAttempts++;
                    Logger.Error($"HTTP MJPEG恢复异常: session={key}, attempt={failedAttempts}, error={ex.Message}", ex);
                }
                finally
                {
                    _operationLock.Release();
                }

                var delayMs = GetRecoveryDelayMs(failedAttempts);
                Logger.Warn($"HTTP MJPEG恢复未成功，{delayMs}ms后重试: session={key}, attempt={failedAttempts}");
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

        internal static bool ShouldValidatePreviewUrl(string previewUrl)
        {
            return !IsHttpPreviewUrl(previewUrl);
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

                        Logger.Warn($"External preview host has exited or HWND was reused; stopping stale session: resource={ResourceToName(session.ResourceType)}, hwnd={session.TargetHwnd}, owner_pid={session.OwnerProcessId}");
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
                // Proxy is shutting down.
            }
            catch (Exception ex)
            {
                Logger.Warn($"External preview HWND validation failed: {ex.Message}");
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
                // HWND and PID validation still prevents most stale-window reuse cases.
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

        private async Task<bool> StopPreviewCore(PreviewResourceType resType, PreviewSessionType sessionType, bool preserveRestartInfo)
        {
            var key = SessionKey(resType, sessionType);
            if (_sessions.TryRemove(key, out var session))
            {
                if (session.Player != null)
                    await session.Player.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                if (!preserveRestartInfo)
                    _restartInfo.TryRemove(key, out _);
                Logger.Info($"预览已停止: {ResourceToName(resType)} {sessionType}");
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

        private async Task StopAllCore(bool preserveRestartInfo)
        {
            var sessions = new List<PreviewSession>(_sessions.Values);
            var stopTasks = new List<Task>();
            foreach (var session in sessions)
            {
                if (session.Player != null)
                    stopTasks.Add(session.Player.DisposeAsync(VlcStopTimeoutMs));
            }

            if (stopTasks.Count > 0)
                await Task.WhenAll(stopTasks).ConfigureAwait(false);

            _sessions.Clear();
            if (!preserveRestartInfo)
                _restartInfo.Clear();
            Logger.Debug("所有预览已停止");
        }

        public async Task RestartPreviewsOnTerminalSwitch(string newTerminalBaseUrl, Func<bool> shouldContinue = null)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (shouldContinue != null && !shouldContinue())
                    return;

                var restartList = new List<PreviewSession>();
                foreach (var session in _restartInfo.Values)
                {
                    if (session.TerminalBound)
                        restartList.Add(session);
                }
                restartList.Sort(CompareRestartPriority);

                // The lane-level plate camera is not tied to terminal 1/2. Stop and restart
                // terminal sessions individually so plate local/external sessions remain live.
                var stopTasks = new List<Task>();
                foreach (var session in restartList)
                {
                    var key = SessionKey(session.ResourceType, session.SessionType);
                    if (_sessions.TryRemove(key, out var active) && active.Player != null)
                        stopTasks.Add(active.Player.DisposeAsync(VlcStopTimeoutMs));
                }
                if (stopTasks.Count > 0)
                    await Task.WhenAll(stopTasks).ConfigureAwait(false);

                await Task.Delay(VlcReleaseSettleMs).ConfigureAwait(false);

                if (shouldContinue != null && !shouldContinue())
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

                    if (shouldContinue != null && !shouldContinue())
                        return;

                    var previewSw = System.Diagnostics.Stopwatch.StartNew();
                    var resourceName = ResourceToName(info.ResourceType);
                    Logger.Info($"预览后台恢复开始: resource={resourceName}, session={info.SessionType}");
                    try
                    {
                        var started = await StartPreviewCore(info.ResourceType, info.SessionType, info.TargetHwnd,
                            newTerminalBaseUrl, info.LocalPanel, shouldContinue,
                            info.ExplicitPreviewUrl, info.TerminalBound, info.DirectRenderTarget)
                            .ConfigureAwait(false);
                        if (started)
                            Logger.Info($"预览后台恢复完成: resource={resourceName}, session={info.SessionType}, 耗时={previewSw.ElapsedMilliseconds}ms");
                        else
                            Logger.Warn($"预览后台恢复未完成: resource={resourceName}, session={info.SessionType}, 耗时={previewSw.ElapsedMilliseconds}ms");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"预览后台恢复失败: resource={resourceName}, session={info.SessionType}, 耗时={previewSw.ElapsedMilliseconds}ms, error={ex.Message}");
                    }

                    if (i < restartList.Count - 1)
                        await Task.Delay(VlcReleaseSettleMs).ConfigureAwait(false);
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try { _lifetimeCts.Cancel(); } catch { }
            _previewUrlValidationTimer?.Dispose();
            _externalHostValidationTimer?.Dispose();
            StopAll();
        }

        private async Task WarmupPreviewStreamIfNeeded(PreviewResourceType resType, string rtspUrl,
            IntPtr parentHwnd, int srcW, int srcH, bool swap)
        {
            var key = resType.ToString();
            if (!_coldStartWarmups.TryAdd(key, 1))
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            VlcPreviewController warmupPlayer = null;
            var ok = false;
            try
            {
                warmupPlayer = await VlcPreviewController.StartAsync($"{ResourceToName(resType)} 预热",
                    rtspUrl, parentHwnd, _networkCachingMs, _liveCachingMs, _rtspTransport,
                    srcW, srcH, swap, visible: false, timeoutMs: VlcPlayTimeoutMs).ConfigureAwait(false);
                ok = warmupPlayer != null && warmupPlayer.IsRunning;

                if (ok)
                    await Task.Delay(ColdStartWarmupMs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _coldStartWarmups.TryRemove(key, out _);
                Logger.Warn($"首次预览预热异常：resource={ResourceToName(resType)}，error={ex.Message}");
            }
            finally
            {
                if (warmupPlayer != null)
                    await warmupPlayer.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                sw.Stop();
                Logger.Info($"首次预览预热完成：resource={ResourceToName(resType)}，ok={ok}，耗时={sw.ElapsedMilliseconds}ms");
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
