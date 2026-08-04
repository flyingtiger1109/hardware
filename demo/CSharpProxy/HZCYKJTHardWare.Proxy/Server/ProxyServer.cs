using System;
using System.Net;
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
    /// 组合根：创建 Runtime、Coordinator、Scheduler、Registry 四个核心组件及其辅助模块，
    /// 完成依赖绑定并转发公共 API。
    ///
    /// 第四阶段重构后约 200 行，重构前约 980 行。
    /// </summary>
    public class ProxyServer : IDisposable
    {
        // === 四个核心组件 ===
        private readonly Runtime.TransportLayer _transport;
        private readonly Coordinator.SwitchCoordinator _coordinator;
        private readonly Scheduler.WorkerExecutionEngine _engine;
        private readonly Core.RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly ControlOperationGate _controlGate;

        // === 辅助模块 ===
        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly PreviewManager _previewManager;
        private readonly QueueManager _queueManager;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly ProcessEndCoordinator _processEndCoordinator;
        private readonly BizOperationHandler _bizOps;
        private readonly DllCommandHandler _commandHandler;
        private readonly TerminalCallbackHandler _callbackHandler;
        private readonly TerminalHealthChecker _healthChecker;
        private readonly RuntimeMetricsReporter _metricsReporter;

        private readonly ProxyRuntime _runtime;
        private readonly Action<string> _log;
        private readonly Action<bool> _onProcessStateChanged;
        private readonly Action<int> _onTerminalChanged;
        private string _lanIp;
        private int _started;
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

        // === URL 辅助属性（依赖绑定阶段使用）===

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

        // === 构造函数：创建全部组件并绑定依赖 ===

        public ProxyServer(
            Action<string> log,
            Action<bool> onProcessStateChanged = null,
            Action<int> onTerminalChanged = null,
            Action<HealthStatus> onHealthChanged = null)
        {
            _log = log;
            _onProcessStateChanged = onProcessStateChanged;
            _onTerminalChanged = onTerminalChanged;

            // 基础设施组件
            _transport = new Runtime.TransportLayer(log);
            _taskTracker = new ActiveTasksTracker(32, 30000);

            // 核心模块
            _terminalManager = new TerminalManager();
            _terminalClient = new TerminalClient();
            _dllCallback = new DllCallbackSender();
            _previewManager = new PreviewManager(_terminalClient, _taskTracker);
            _queueManager = new QueueManager();
            _requestRegistry = new RequestRegistry();
            _processRegistry = new TerminalProcessRegistry();
            _controlGate = new ControlOperationGate();
            _metricsReporter = new RuntimeMetricsReporter(
                _queueManager, _taskTracker, _previewManager,
                _requestRegistry, _processRegistry);

            // === 四个核心组件 ===

            // 调度器
            _engine = new WorkerExecutionEngine(
                _terminalManager, _terminalClient, _requestRegistry,
                _processRegistry,
                log, GetTerminalCallbackBaseUrl, GetTerminalIrisCallbackUrl);

            // Runtime：生命周期管理
            _runtime = new ProxyRuntime(
                _transport, _requestRegistry, _processRegistry, _taskTracker,
                _queueManager, _previewManager, _dllCallback,
                _metricsReporter, log);

            // Coordinator：终端切换协调器
            _coordinator = new SwitchCoordinator(
                _terminalManager, _previewManager, _requestRegistry,
                _queueManager, log, NotifyTerminalChanged, _controlGate,
                _taskTracker);

            _processEndCoordinator = new ProcessEndCoordinator(
                _terminalManager, _terminalClient, _processRegistry,
                _controlGate, _coordinator, log,
                onProcessStateChanged);

            // Coordinator：业务操作处理器
            _bizOps = new BizOperationHandler(
                _terminalManager, _terminalClient, _requestRegistry,
                _processRegistry, _controlGate,
                _queueManager, _previewManager, log,
                GetTerminalCallbackBaseUrl, GetTerminalIrisCallbackUrl,
                onProcessStateChanged, _coordinator, _processEndCoordinator,
                _dllCallback);

            // 绑定调度器处理函数
            _queueManager.SwitchHandler = _coordinator.ExecuteQueuedSwitch;
            _queueManager.FaceCaptureHandler = (t) => _engine.ExecuteCaptureFace(t);
            _queueManager.FingerprintCaptureHandler = (t) => _engine.ExecuteCaptureFingerprint(t);
            _queueManager.IrisHandler = (t) => _engine.ExecuteIrisInternal(t.Data as IrisTaskData);
            _queueManager.OcrHandler = (t) => _engine.ExecuteOcrInternal(t);
            _queueManager.NfcHandler = (t) => _engine.ExecuteNfcInternal(t);
            _queueManager.AuthorizeHandler = _engine.ExecuteAuthorizeInternal;

            // 绑定采集委托（WorkerExecutionEngine → BizOperationHandler 异步方法）
            _engine.CaptureFaceFunc = (d, route) =>
                _bizOps.CaptureFaceAsync(d, route).GetAwaiter().GetResult();
            _engine.CaptureFingerprintFunc = (d, hk, route) =>
                _bizOps.CaptureFingerprintAsync(d, hk, route).GetAwaiter().GetResult();

            // 辅助模块
            _commandHandler = new DllCommandHandler(
                _terminalManager, _terminalClient, _dllCallback, _previewManager,
                _requestRegistry, _processRegistry, _controlGate, log,
                GetTerminalCallbackBaseUrl, _queueManager, _taskTracker,
                _coordinator, _processEndCoordinator, onProcessStateChanged);

            _callbackHandler = new TerminalCallbackHandler(
                _terminalClient, _terminalManager, _dllCallback,
                _requestRegistry, _processRegistry, log);

            _healthChecker = new TerminalHealthChecker(
                _terminalClient, _terminalManager, log, onHealthChanged);
        }

        // === 生命周期 ===

        public void Start()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ProxyServer));
            // 审查风险：启动标志在监听器和后台组件启动前置位；后续步骤抛出异常时无法重试，且可能遗留部分资源。
            // 建议为 Start 增加失败回滚，在全部组件启动成功后提交状态，或显式进入故障状态并执行 Stop/Dispose。
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException("ProxyServer has already been started.");

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

            // 终端硬件健康检查
            _healthChecker.Start();
            _metricsReporter.Start();

            // VLC 预热
            if (!_taskTracker.TryRun(() =>
            {
                _log("[VLC预热] 正在启动...");
                var player = new VlcPreviewPlayer();
                try { player.Warmup(); _log($"[VLC预热] 已完成, 耗时={player.WarmupMs}ms"); }
                finally { player.StopKeepDlls(); }
            }, "vlc_warmup"))
                _log("[VLC预热] 后台任务容量已满，跳过本次预热");

            _log($"服务已启动。本机IP: {_lanIp}, 当前终端: {_terminalManager.CurrentName} ({_terminalManager.CurrentBaseUrl})");
            NotifyTerminalChanged(_terminalManager.CurrentIndex);
        }

        private void NotifyTerminalChanged(int terminalIndex)
        {
            try
            {
                var processActive = _processRegistry.TryGetCurrent(terminalIndex,
                    out var processSession);
                _terminalManager.ProcessActive = processActive;
                _terminalManager.ProcessSaveDir = processActive
                    ? processSession.SaveDir
                    : "";
                _onTerminalChanged?.Invoke(terminalIndex);
                _onProcessStateChanged?.Invoke(processActive);
                _healthChecker?.RequestCheck();
            }
            catch (Exception ex)
            {
                Logger.Error("[服务] 通知当前终端变化失败", ex);
            }
        }

        public void RequestHealthCheck()
        {
            _healthChecker?.RequestCheck(resetRetryAttempt: false);
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;
            // 先取消并收敛健康轮询，确保后续释放 TerminalClient 时
            // 不再存在访问共享 HttpClient 或发送 UI 通知的在途任务。
            try { _healthChecker.StopAsync(5000).GetAwaiter().GetResult(); }
            catch (Exception ex) { Logger.Error("[服务] 健康检测器停止异常", ex); }
            // 通过 Runtime 有序关闭，所有步骤共享约 5 秒时限
            try { _runtime.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { Logger.Error("[服务] 关闭异常", ex); }

            _log("服务已停止");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            Stop();
            SafeDispose(_metricsReporter, nameof(RuntimeMetricsReporter));
            SafeDispose(_processRegistry, nameof(TerminalProcessRegistry));
            SafeDispose(_requestRegistry, nameof(RequestRegistry));
            SafeDispose(_taskTracker, nameof(ActiveTasksTracker));
            SafeDispose(_transport, nameof(TransportLayer));
            SafeDispose(_previewManager, nameof(PreviewManager));
            SafeDispose(_healthChecker, nameof(TerminalHealthChecker));
            SafeDispose(_terminalClient, nameof(TerminalClient));
            SafeDispose(_dllCallback, nameof(DllCallbackSender));
        }

        private static void SafeDispose(IDisposable component, string name)
        {
            if (component == null) return;
            try { component.Dispose(); }
            catch (Exception ex) { Logger.Error($"[服务] 释放{name}异常", ex); }
        }

        // === HTTP 处理函数（由 TransportLayer 转发）===

        private async Task HandleDllRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;
                    var (method, path, body) = await ReadHttpRequestWithDeadlineAsync(
                        client, stream, 2000).ConfigureAwait(false);
                    var result = await _commandHandler.HandleAsync(method, path, body);
                    await WriteHttpResponseWithDeadlineAsync(client, stream, 200,
                        result, 2000).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { LogException("[DLL请求] 处理异常", ex); }
        }

        private async Task HandleCallbackRequest(TcpClient client)
        {
            try
            {
                var remoteAddress =
                    (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;
                    var (_, path, body) = await ReadHttpRequestWithDeadlineAsync(
                        client, stream, 30000).ConfigureAwait(false);
                    var requestId = JsonHelper.ExtractString(body, "request_id");
                    var resourceType = JsonHelper.ExtractString(body, "resource_type");
                    var callbackPath = (path ?? "").Split('?')[0];

                    if (string.Equals(callbackPath, "/iris-image", StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(requestId) || resourceType != "iris_image"))
                    {
                        await WriteHttpResponseWithDeadlineAsync(client, stream, 400,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"status\":\"rejected\",\"message\":\"invalid iris callback\"," +
                            "\"error_code\":\"invalid_callback\"}", 30000).ConfigureAwait(false);
                        return;
                    }

                    var accepted = _taskTracker.TryRun(async () =>
                    {
                        try
                        {
                            await _callbackHandler.HandleAsync(body, remoteAddress, callbackPath)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) { LogException("[终端回调] 后台处理异常", ex); }
                    }, "terminal_callback_handler");

                    if (!accepted)
                    {
                        await WriteHttpResponseWithDeadlineAsync(client, stream, 503,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"status\":\"rejected\",\"message\":\"service busy\"," +
                            "\"error_code\":\"service_busy\"}", 30000).ConfigureAwait(false);
                        return;
                    }

                    await WriteHttpResponseWithDeadlineAsync(client, stream, 202,
                        "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                        "\",\"status\":\"accepted\"}", 30000).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { LogException("[终端回调] HTTP处理异常", ex); }
        }

        private void LogException(string context, Exception ex)
        {
            Logger.Error(context, ex);
            _log($"{context}: {ex.Message}");
        }

        private static async Task<(string method, string path, string body)>
            ReadHttpRequestWithDeadlineAsync(TcpClient client, NetworkStream stream,
                int timeoutMs)
        {
            using (var cancellation = new CancellationTokenSource(timeoutMs))
            using (cancellation.Token.Register(() => CloseClient(client)))
            {
                return await HttpProtocolHandler.ReadHttpRequestAsync(
                    stream, cancellation.Token).ConfigureAwait(false);
            }
        }

        private static async Task WriteHttpResponseWithDeadlineAsync(TcpClient client,
            NetworkStream stream, int statusCode, string body, int timeoutMs)
        {
            using (var cancellation = new CancellationTokenSource(timeoutMs))
            using (cancellation.Token.Register(() => CloseClient(client)))
            {
                await HttpProtocolHandler.WriteHttpResponseAsync(stream, statusCode,
                    body, cancellation.Token).ConfigureAwait(false);
            }
        }

        private static void CloseClient(TcpClient client)
        {
            try { client?.Close(); } catch { }
        }

        // === 公共 API（转发到 Coordinator）===
        // 为兼容 MainForm 保留同步方法。方法通过 GetAwaiter().GetResult() 调用 BizOperationHandler 异步方法；
        // MainForm 从不含 UI 同步上下文的 Task.Run 工作任务中调用这些方法。

        public string StartProcess(string saveDir)
            => _bizOps.StartProcessAsync(saveDir).GetAwaiter().GetResult();

        public string EndProcess()
            => _bizOps.EndProcessAsync().GetAwaiter().GetResult();

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
