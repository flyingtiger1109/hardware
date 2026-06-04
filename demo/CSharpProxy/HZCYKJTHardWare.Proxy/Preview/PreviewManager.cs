using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        Iris
    }

    public enum PreviewSessionType
    {
        Local,
        External
    }

    public class PreviewSession
    {
        public VlcPreviewController Player { get; set; }
        public IntPtr TargetHwnd { get; set; }
        public Control LocalPanel { get; set; }
        public PreviewResourceType ResourceType { get; set; }
        public PreviewSessionType SessionType { get; set; }
        public bool IsRunning => Player?.IsRunning ?? false;
    }

    public class PreviewManager : IDisposable
    {
        private const int PreviewUrlTimeoutMs = 5000;
        private const int ColdStartWarmupMs = 800;
        private const int VlcPlayTimeoutMs = 2500;
        private const int VlcStopTimeoutMs = 1500;
        private readonly ConcurrentDictionary<string, PreviewSession> _sessions = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, PreviewSession> _restartInfo = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, byte> _coldStartWarmups = new ConcurrentDictionary<string, byte>();
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private readonly TerminalClient _terminalClient;
        private readonly int _networkCachingMs;
        private readonly int _liveCachingMs;
        private readonly string _rtspTransport;
        private readonly SynchronizationContext _uiContext;  // Captured from UI thread at construction

        public PreviewManager(TerminalClient terminalClient)
        {
            _terminalClient = terminalClient;
            _uiContext = SynchronizationContext.Current;  // Capture UI thread sync context (same as Delphi MainThreadID)
            var cfg = AppConfig.Instance;
            _networkCachingMs = cfg.RtspNetworkCachingMs;
            _liveCachingMs = cfg.RtspLiveCachingMs;
            _rtspTransport = cfg.RtspTransport ?? "";
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
                default: return "未知";
            }
        }

        private static (int srcW, int srcH, bool swap) GetSourceDimensions(PreviewResourceType resType)
        {
            switch (resType)
            {
                case PreviewResourceType.Camera: return (480, 640, true);
                case PreviewResourceType.Fingerprint: return (640, 640, false);
                default: return (640, 480, false);
            }
        }

        public async Task<string> RequestPreviewUrl(PreviewResourceType resType, string terminalBaseUrl)
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
            Logger.Info($"预览URL请求完成：resource={ResourceToName(resType)}，terminal={terminalBaseUrl}，耗时={sw.ElapsedMilliseconds}ms，url_empty={string.IsNullOrEmpty(previewUrl)}");
            return previewUrl;
        }

        public async Task<bool> StartPreview(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await StartPreviewCore(resType, sessionType, targetHwnd, terminalBaseUrl, localPanel)
                    .ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<bool> StartPreviewCore(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var key = SessionKey(resType, sessionType);

            // If already running for same target, skip
            if (_sessions.TryGetValue(key, out var existing) && existing.IsRunning && existing.TargetHwnd == targetHwnd)
                return true;

            // Stop existing if running with different target
            if (existing != null && existing.IsRunning)
                await StopPreviewCore(resType, sessionType, preserveRestartInfo: false).ConfigureAwait(false);

            // Get RTSP URL from terminal
            var urlTick = totalSw.ElapsedMilliseconds;
            var rtspUrl = await RequestPreviewUrl(resType, terminalBaseUrl);
            var urlElapsed = totalSw.ElapsedMilliseconds - urlTick;
            if (string.IsNullOrEmpty(rtspUrl))
            {
                Logger.Error($"获取预览URL失败: {ResourceToName(resType)}");
                return false;
            }

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
            await WarmupPreviewStreamIfNeeded(resType, rtspUrl, parentHwnd, srcW, srcH, swap).ConfigureAwait(false);

            // Start VLC on a dedicated preview thread so a native VLC stall cannot freeze the UI.
            VlcPreviewController player = null;
            var playTick = totalSw.ElapsedMilliseconds;
            var description = $"{ResourceToName(resType)} {sessionType}";
            player = await VlcPreviewController.StartAsync(description, rtspUrl, parentHwnd,
                _networkCachingMs, _liveCachingMs, _rtspTransport, srcW, srcH, swap,
                visible: true, timeoutMs: VlcPlayTimeoutMs).ConfigureAwait(false);
            var playElapsed = totalSw.ElapsedMilliseconds - playTick;
            var ok2 = player != null && player.IsRunning;

            if (!ok2)
            {
                if (player != null)
                    await player.DisposeAsync(VlcStopTimeoutMs).ConfigureAwait(false);
                Logger.Error($"VLC播放失败明细：resource={ResourceToName(resType)}，session={sessionType}，取URL耗时={urlElapsed}ms，播放耗时={playElapsed}ms，总耗时={totalSw.ElapsedMilliseconds}ms");
                Logger.Error($"VLC播放失败: {ResourceToName(resType)}");
                return false;
            }

            var session = new PreviewSession
            {
                Player = player,
                TargetHwnd = targetHwnd,
                LocalPanel = localPanel,
                ResourceType = resType,
                SessionType = sessionType
            };
            _sessions[key] = session;
            _restartInfo[key] = session;
            totalSw.Stop();
            Logger.Info($"预览启动明细：resource={ResourceToName(resType)}，session={sessionType}，hwnd={parentHwnd}，取URL耗时={urlElapsed}ms，播放耗时={playElapsed}ms，总耗时={totalSw.ElapsedMilliseconds}ms，network_cache={_networkCachingMs}ms，live_cache={_liveCachingMs}ms，transport={_rtspTransport}");

            Logger.Info($"预览已启动: {ResourceToName(resType)} {sessionType} -> hwnd={parentHwnd}");
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
            Logger.Info("所有预览已停止");
        }

        public async Task RestartPreviewsOnTerminalSwitch(string newTerminalBaseUrl)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var restartList = new List<PreviewSession>(_restartInfo.Values);
                await StopAllCore(preserveRestartInfo: true).ConfigureAwait(false);

                if (restartList.Count == 0)
                {
                    Logger.Info("无活跃预览需要重启");
                    return;
                }

                Logger.Info($"终端切换，重启 {restartList.Count} 个预览");

                foreach (var info in restartList)
                {
                    try
                    {
                        await StartPreviewCore(info.ResourceType, info.SessionType, info.TargetHwnd, newTerminalBaseUrl, info.LocalPanel)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"重启预览失败 {ResourceToName(info.ResourceType)}: {ex.Message}");
                    }
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public void Dispose() { StopAll(); }

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
    }
}
