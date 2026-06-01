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
        public VlcPreviewPlayer Player { get; set; }
        public IntPtr TargetHwnd { get; set; }
        public Control LocalPanel { get; set; }
        public PreviewResourceType ResourceType { get; set; }
        public PreviewSessionType SessionType { get; set; }
        public bool IsRunning => Player?.IsRunning ?? false;
    }

    public class PreviewManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, PreviewSession> _sessions = new ConcurrentDictionary<string, PreviewSession>();
        private readonly ConcurrentDictionary<string, PreviewSession> _restartInfo = new ConcurrentDictionary<string, PreviewSession>();
        private readonly TerminalClient _terminalClient;
        private readonly int _networkCachingMs;
        private readonly int _liveCachingMs;
        private readonly SynchronizationContext _uiContext;  // Captured from UI thread at construction

        public PreviewManager(TerminalClient terminalClient)
        {
            _terminalClient = terminalClient;
            _uiContext = SynchronizationContext.Current;  // Capture UI thread sync context (same as Delphi MainThreadID)
            var cfg = AppConfig.Instance;
            _networkCachingMs = cfg.RtspNetworkCachingMs;
            _liveCachingMs = cfg.RtspLiveCachingMs;
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
            var path = ResourceToTerminalPath(resType);
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var body = $"{{\"request_id\":\"{requestId}\"}}";

            var (ok, response) = await _terminalClient.PostJsonAsync(terminalBaseUrl, path, body);
            if (!ok) return null;

            return ResultParser.ExtractPreviewUrl(response);
        }

        public async Task<bool> StartPreview(PreviewResourceType resType, PreviewSessionType sessionType,
            IntPtr targetHwnd, string terminalBaseUrl, Control localPanel = null)
        {
            var key = SessionKey(resType, sessionType);

            // If already running for same target, skip
            if (_sessions.TryGetValue(key, out var existing) && existing.IsRunning && existing.TargetHwnd == targetHwnd)
                return true;

            // Stop existing if running with different target
            if (existing != null && existing.IsRunning)
                StopPreview(resType, sessionType);

            // Get RTSP URL from terminal
            var rtspUrl = await RequestPreviewUrl(resType, terminalBaseUrl);
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

            // Create VLC player and play — MUST run on UI thread (same as Delphi RunOnMainThread)
            var player = new VlcPreviewPlayer();
            var ok2 = false;
            _uiContext.Send(_ =>
            {
                ok2 = player.Play(rtspUrl, parentHwnd, _networkCachingMs, _liveCachingMs, srcW, srcH, swap);
            }, null);

            if (!ok2)
            {
                _uiContext.Send(_ => { player.Dispose(); }, null);
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

            Logger.Info($"预览已启动: {ResourceToName(resType)} {sessionType} -> hwnd={parentHwnd}");
            return true;
        }

        public bool StopPreview(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            var key = SessionKey(resType, sessionType);
            if (_sessions.TryRemove(key, out var session))
            {
                // VLC dispose must run on UI thread (same as Delphi RunOnMainThread)
                _uiContext.Send(_ => { session.Player?.Dispose(); }, null);
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
            // VLC dispose must run on UI thread
            _uiContext.Send(_ =>
            {
                foreach (var kvp in _sessions)
                    kvp.Value.Player?.Dispose();
            }, null);
            _sessions.Clear();
            Logger.Info("所有预览已停止");
        }

        public async Task RestartPreviewsOnTerminalSwitch(string newTerminalBaseUrl)
        {
            var restartList = new List<PreviewSession>(_restartInfo.Values);

            foreach (var kvp in _sessions)
                kvp.Value.Player?.Dispose();
            _sessions.Clear();

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
                    await StartPreview(info.ResourceType, info.SessionType, info.TargetHwnd, newTerminalBaseUrl, info.LocalPanel);
                }
                catch (Exception ex)
                {
                    Logger.Error($"重启预览失败 {ResourceToName(info.ResourceType)}: {ex.Message}");
                }
            }
        }

        public void Dispose() { StopAll(); }
    }
}
