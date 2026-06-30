using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server.Coordinator;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using HZCYKJTHardWare.Proxy.Server.Scheduler;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server
{
    /// <summary>
    /// Composition root. Creates the four core components (Runtime, Coordinator,
    /// Scheduler, Registry) and their supporting modules, wires dependencies,
    /// and delegates the public API surface.
    ///
    /// Post-Phase-4: ~200 lines (down from 980).
    /// </summary>
    public class ProxyServer : IDisposable
    {
        // === Four core components ===
        private readonly Runtime.TransportLayer _transport;
        private readonly Coordinator.SwitchCoordinator _coordinator;
        private readonly Scheduler.WorkerExecutionEngine _engine;
        private readonly Core.RequestRegistry _requestRegistry;

        // === Supporting modules ===
        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly PreviewManager _previewManager;
        private readonly QueueManager _queueManager;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly BizOperationHandler _bizOps;
        private readonly DllCommandHandler _commandHandler;
        private readonly TerminalCallbackHandler _callbackHandler;

        private readonly ProxyRuntime _runtime;
        private readonly Action<string> _log;
        private readonly Action<bool> _onProcessStateChanged;
        private string _lanIp;
        private int _stopped;
        private int _disposed;

        public string LanIp => _lanIp;
        public QueueManager QueueManager => _queueManager;

        public class AuthorizeRequestResult
        {
            public bool Ok { get; set; }
            public string RequestId { get; set; }
            public string Message { get; set; }
        }

        // === URL helpers (used during wiring) ===

        public string GetTerminalCallbackBaseUrl()
        {
            return AppConfig.Instance.GetTerminalCallbackBaseUrl(_lanIp);
        }

        private string GetTerminalIrisCallbackUrl()
        {
            var baseUrl = GetTerminalCallbackBaseUrl().TrimEnd('/');
            var configuredPath = (AppConfig.Instance.CallbackPath ?? "").TrimEnd('/');
            if (!string.IsNullOrEmpty(configuredPath) &&
                baseUrl.EndsWith(configuredPath, StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - configuredPath.Length).TrimEnd('/');
            return baseUrl + "/iris-image";
        }

        // === Constructor: create all components, wire dependencies ===

        public ProxyServer(Action<string> log, Action<bool> onProcessStateChanged = null)
        {
            _log = log;
            _onProcessStateChanged = onProcessStateChanged;

            // Infrastructure
            _transport = new Runtime.TransportLayer(log);
            _taskTracker = new ActiveTasksTracker(32, 30000);

            // Core modules
            _terminalManager = new TerminalManager();
            _terminalClient = new TerminalClient();
            _dllCallback = new DllCallbackSender();
            _previewManager = new PreviewManager(_terminalClient);
            _queueManager = new QueueManager();
            _requestRegistry = new RequestRegistry();

            // === Four components ===

            // Scheduler
            _engine = new WorkerExecutionEngine(
                _terminalManager, _terminalClient, _requestRegistry,
                log, GetTerminalCallbackBaseUrl, GetTerminalIrisCallbackUrl);

            // Runtime: lifecycle
            _runtime = new ProxyRuntime(
                _transport, _requestRegistry, _taskTracker,
                _queueManager, _previewManager, log);

            // Coordinator: SwitchCoordinator
            _coordinator = new SwitchCoordinator(
                _terminalManager, _previewManager, _requestRegistry,
                _queueManager, log);

            // Coordinator: BizOperationHandler
            _bizOps = new BizOperationHandler(
                _terminalManager, _terminalClient, _requestRegistry,
                _queueManager, _previewManager, log,
                GetTerminalCallbackBaseUrl, GetTerminalIrisCallbackUrl,
                onProcessStateChanged, _coordinator, _dllCallback);

            // Wire Scheduler handlers
            _queueManager.SwitchHandler = _coordinator.ExecuteQueuedSwitch;
            _queueManager.FaceCaptureHandler = (t) => _engine.ExecuteCaptureFace(t);
            _queueManager.FingerprintCaptureHandler = (t) => _engine.ExecuteCaptureFingerprint(t);
            _queueManager.IrisHandler = (t) => _engine.ExecuteIrisInternal(t.Data as IrisTaskData);
            _queueManager.OcrHandler = (t) => _engine.ExecuteOcrInternal(t);
            _queueManager.NfcHandler = (t) => _engine.ExecuteNfcInternal(t);
            _queueManager.AuthorizeHandler = _engine.ExecuteAuthorizeInternal;

            // Wire capture delegates (WorkerExecutionEngine → BizOperationHandler async)
            _engine.CaptureFaceFunc = (d) => _bizOps.CaptureFaceAsync(d).GetAwaiter().GetResult();
            _engine.CaptureFingerprintFunc = (d) => _bizOps.CaptureFingerprintAsync(d).GetAwaiter().GetResult();

            // Supporting modules
            _commandHandler = new DllCommandHandler(
                _terminalManager, _terminalClient, _dllCallback, _previewManager,
                _requestRegistry, log,
                GetTerminalCallbackBaseUrl, _queueManager, _taskTracker,
                _coordinator, onProcessStateChanged);

            _callbackHandler = new TerminalCallbackHandler(
                _terminalClient, _terminalManager, _dllCallback,
                _requestRegistry, log);
        }

        // === Lifecycle ===

        public void Start()
        {
            var cfg = AppConfig.Instance;
            var cts = _runtime.BeginSession();

            _lanIp = NetworkDetector.DetectLanIp(cfg.SubnetPrefix);

            _transport.AddListener("DLL服务", cfg.DllServerHost, cfg.DllServerPort,
                HandleDllRequest, maxConcurrent: 64, backlog: 200);
            _transport.AddListener("终端回调", cfg.CallbackListenHost, cfg.CallbackListenPort,
                HandleCallbackRequest, maxConcurrent: 8, backlog: 50);

            _log($"DLL 服务监听: {cfg.DllServerHost}:{cfg.DllServerPort}");
            _log($"回调服务监听: {cfg.CallbackListenHost}:{cfg.CallbackListenPort}");

            _transport.StartAll(cts.Token);

            // VLC warmup
            if (!_taskTracker.TryRun(() =>
            {
                _log("[VLC预热] 正在启动...");
                var player = new VlcPreviewPlayer();
                try { player.Warmup(); _log($"[VLC预热] 已完成, 耗时={player.WarmupMs}ms"); }
                finally { player.StopKeepDlls(); }
            }, "vlc_warmup"))
                _log("[VLC预热] 后台任务容量已满，跳过本次预热");

            _log($"服务已启动。本机IP: {_lanIp}, 当前终端: {_terminalManager.CurrentName} ({_terminalManager.CurrentBaseUrl})");
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;
            // Ordered shutdown via Runtime (one shared ~5s budget)
            try { _runtime.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { _log($"[服务] 关闭异常: {ex.Message}"); }

            _log("服务已停止");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            Stop();
            try { _requestRegistry?.Dispose(); } catch { }
            try { _taskTracker?.Dispose(); } catch { }
            try { _transport?.Dispose(); } catch { }
            try { _previewManager?.Dispose(); } catch { }
            try { _terminalClient?.Dispose(); } catch { }
            try { _dllCallback?.Dispose(); } catch { }
        }

        // === HTTP handlers (delegated from TransportLayer) ===

        private async Task HandleDllRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;
                    var (method, path, body) = await HttpProtocolHandler.ReadHttpRequestAsync(stream);
                    var result = await _commandHandler.HandleAsync(method, path, body);
                    await HttpProtocolHandler.WriteHttpResponseAsync(stream, 200, result);
                }
            }
            catch (Exception ex) { _log($"[DLL请求] 处理异常: {ex.Message}"); }
        }

        private async Task HandleCallbackRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;
                    var (_, path, body) = await HttpProtocolHandler.ReadHttpRequestAsync(stream);
                    var requestId = JsonHelper.ExtractString(body, "request_id");
                    var resourceType = JsonHelper.ExtractString(body, "resource_type");
                    var callbackPath = (path ?? "").Split('?')[0];

                    if (string.Equals(callbackPath, "/iris-image", StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(requestId) || resourceType != "iris_image"))
                    {
                        await HttpProtocolHandler.WriteHttpResponseAsync(stream, 400,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"status\":\"rejected\",\"message\":\"invalid iris callback\"," +
                            "\"error_code\":\"invalid_callback\"}");
                        return;
                    }

                    var accepted = _taskTracker.TryRun(() =>
                    {
                        try { _callbackHandler.Handle(body); }
                        catch (Exception ex) { _log("[终端回调] 后台处理异常: " + ex.Message); }
                    }, "terminal_callback_handler");

                    if (!accepted)
                    {
                        await HttpProtocolHandler.WriteHttpResponseAsync(stream, 503,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"status\":\"rejected\",\"message\":\"service busy\"," +
                            "\"error_code\":\"service_busy\"}");
                        return;
                    }

                    await HttpProtocolHandler.WriteHttpResponseAsync(stream, 202,
                        "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                        "\",\"status\":\"accepted\"}");
                }
            }
            catch (Exception ex) { _log($"[终端回调] HTTP处理异常: {ex.Message}"); }
        }

        // === Public API (delegated to Coordinator) ===
        // Methods are synchronous for backward compatibility with MainForm.
        // They call async BizOperationHandler methods via .GetAwaiter().GetResult()
        // which is safe because MainForm calls them from Task.Run workers (no UI SyncCtx).

        public string StartProcess(string saveDir)
            => _bizOps.StartProcessAsync(saveDir).GetAwaiter().GetResult();

        public string EndProcess()
            => _bizOps.EndProcess();

        public string SwitchTerminal(int index)
            => _bizOps.SwitchTerminalAsync(index).GetAwaiter().GetResult();

        public (bool ok, string path) CaptureFace(string saveDir)
            => _bizOps.CaptureFaceAsync(saveDir).GetAwaiter().GetResult();

        public (bool ok, string path) CaptureFingerprint(string saveDir)
            => _bizOps.CaptureFingerprintAsync(saveDir).GetAwaiter().GetResult();

        public string RequestOCR(string saveDir)
            => _bizOps.RequestOCRAsync(saveDir).GetAwaiter().GetResult();

        public string RequestNfc(string saveDir)
            => _bizOps.RequestNfcAsync(saveDir).GetAwaiter().GetResult();

        public string CaptureIris(string saveDir)
            => _bizOps.CaptureIrisAsync(saveDir).GetAwaiter().GetResult();

        public AuthorizeRequestResult RequestAuthorize(string idNo, string docType,
            string nationality, string name, string sex, string birthday)
        {
            var r = _bizOps.RequestAuthorizeAsync(idNo, docType, nationality, name, sex, birthday)
                .GetAwaiter().GetResult();
            return new AuthorizeRequestResult { Ok = r.Ok, RequestId = r.RequestId, Message = r.Message };
        }

        public Task<bool> StartLocalPreviewAsync(string resourceType, System.Windows.Forms.Control panel)
            => _bizOps.StartLocalPreviewAsync(resourceType, panel);

        public void StopLocalPreview(string resourceType)
            => _bizOps.StopLocalPreview(resourceType);
    }
}
