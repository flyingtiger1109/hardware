using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly DeviceCapabilityManager _capabilities;

        private readonly ProxyRuntime _runtime;
        private readonly Action<string> _log;
        private readonly PingLogAggregator _pingLogAggregator;
        private readonly LogRateLimiter _callbackIngressRateLimiter =
            new LogRateLimiter(TimeSpan.FromMinutes(1));
        private readonly Action<bool> _onProcessStateChanged;
        private readonly Action<int> _onTerminalChanged;
        private static long _nextHttpTraceSequence;
        private string _lanIp;
        private int _started;
        private int _stopped;
        private int _disposed;
        private int _latestFrameRouteDiagnosticLogged;

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
            _pingLogAggregator = new PingLogAggregator(log);
            _onProcessStateChanged = onProcessStateChanged;
            _onTerminalChanged = onTerminalChanged;
            _capabilities = DeviceCapabilityManager.Instance;

            // 基础设施组件
            _transport = new Runtime.TransportLayer(log);
            _taskTracker = new ActiveTasksTracker(32, 30000);

            // 核心模块
            _terminalManager = new TerminalManager();
            _terminalClient = new TerminalClient();
            _dllCallback = new DllCallbackSender();
            _previewManager = new PreviewManager(_terminalClient, _taskTracker);
            _previewManager.SetExternalPreviewFailureHandler(NotifyExternalPreviewFailure);
            _queueManager = new QueueManager(_capabilities);
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
            _engine.CaptureFaceWithRequestIdFunc = (requestId, d, route) =>
                _bizOps.CaptureFaceAsync(d, route, requestId).GetAwaiter().GetResult();
            _engine.CaptureFingerprintWithRequestIdFunc = (requestId, d, hk, route) =>
                _bizOps.CaptureFingerprintAsync(d, hk, route, requestId).GetAwaiter().GetResult();

            // 辅助模块
            _commandHandler = new DllCommandHandler(
                _terminalManager, _terminalClient, _dllCallback, _previewManager,
                _requestRegistry, _processRegistry, _controlGate, log,
                GetTerminalCallbackBaseUrl, _queueManager, _taskTracker,
                _coordinator, _processEndCoordinator, onProcessStateChanged,
                _capabilities);

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

            _lanIp = _capabilities.IsSupported(DeviceCapability.TerminalControl)
                ? NetworkDetector.DetectLanIp(cfg.SubnetPrefix)
                : "127.0.0.1";

            _transport.AddListener("DLL服务", cfg.DllServerHost, cfg.DllServerPort,
                HandleDllRequest, maxConcurrent: 64, backlog: 200);
            if (_capabilities.IsSupported(DeviceCapability.TerminalControl))
                _transport.AddListener("终端回调", cfg.CallbackListenHost, cfg.CallbackListenPort,
                    HandleCallbackRequest, maxConcurrent: 8, backlog: 50);

            _transport.StartAll(cts.Token);

            // 终端硬件健康检查
            if (_capabilities.IsSupported(DeviceCapability.TerminalControl))
                _healthChecker.Start();
            _metricsReporter.Start();

            // VLC 预热
            if (!_taskTracker.TryRun(() =>
            {
                _log(Logger.FormatModuleMessage("VLC预热", "调试", "正在启动"));
                var player = new VlcPreviewPlayer();
                try
                {
                    player.Warmup();
                    _log(Logger.FormatModuleMessage("VLC预热", "信息",
                        $"初始化完成：耗时={player.WarmupMs}ms"));
                }
                finally { player.StopKeepDlls(); }
            }, "vlc_warmup"))
                _log(Logger.FormatModuleMessage("VLC预热", "警告",
                    "后台任务容量已满，跳过本次预热"));

            _log(Logger.FormatModuleMessage(LogModules.ServiceListener, "信息",
                $"服务启动成功：DLL={cfg.DllServerHost}:{cfg.DllServerPort}，" +
                (_capabilities.IsSupported(DeviceCapability.TerminalControl)
                    ? $"终端回调={cfg.CallbackListenHost}:{cfg.CallbackListenPort}，"
                    : "") +
                $"本机IP={_lanIp}，当前终端={_terminalManager.CurrentName}（{_terminalManager.CurrentBaseUrl}）"));
            if (_capabilities.IsSupported(DeviceCapability.TerminalControl))
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

        private static string GetPreviewCallbackResourceName(PreviewResourceType resourceType)
        {
            switch (resourceType)
            {
                case PreviewResourceType.Camera:
                    return "face_image";
                case PreviewResourceType.Fingerprint:
                    return "fingerprint_image";
                case PreviewResourceType.Iris:
                    return "iris_image";
                default:
                    return "plate_image";
            }
        }

        private void NotifyExternalPreviewFailure(
            PreviewResourceType resourceType, string requestId, string reason)
        {
            _ = NotifyExternalPreviewFailureAsync(resourceType, requestId, reason);
        }

        private async Task NotifyExternalPreviewFailureAsync(
            PreviewResourceType resourceType, string requestId, string reason)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return;

            try
            {
                var resourceName = GetPreviewCallbackResourceName(resourceType);
                var message = string.IsNullOrWhiteSpace(reason)
                    ? "MJPEG预览恢复失败"
                    : reason;
                var payload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                    "\",\"resource_type\":\"" + resourceName +
                    "\",\"render_hwnd\":0,\"error\":true," +
                    "\"code\":\"preview_runtime_failed\",\"message\":\"" +
                    JsonHelper.EscapeString(message) + "\"}";

                _log(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    "预览运行时失败，已通知DLL清理租约：资源=" + resourceName +
                    "，request_id=" + PreviewManager.FormatRequestId(requestId)));
                await _dllCallback.PostCallbackRaw("/preview-ready", payload)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    "预览运行时失败通知DLL异常：request_id=" +
                    PreviewManager.FormatRequestId(requestId)), ex);
            }
        }

        public void RequestHealthCheck()
        {
            if (_capabilities.IsSupported(DeviceCapability.TerminalControl))
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

            _pingLogAggregator.Flush();
            _log(Logger.FormatModuleMessage(LogModules.ServiceListener, "信息", "服务已停止"));
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
            SafeDispose(_pingLogAggregator, nameof(PingLogAggregator));
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
            var requestPath = "<未知>";
            var requestTrace = CreateHttpTraceId();
            var requestLogId = "<未知>";
            var requestSw = Stopwatch.StartNew();
            var isPing = false;
            var pingRecorded = false;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;
                    var (method, path, body) = await ReadHttpRequestWithDeadlineAsync(
                        client, stream, 2000).ConfigureAwait(false);
                    requestPath = path ?? "<未知>";
                    var rawRequestId = JsonHelper.ExtractString(body, "request_id");
                    if (!string.IsNullOrWhiteSpace(rawRequestId))
                        requestTrace = rawRequestId;
                    requestLogId = FormatRequestIdForLog(rawRequestId, requestTrace);
                    isPing = string.Equals((requestPath ?? string.Empty).Split('?')[0],
                        "/ping", StringComparison.OrdinalIgnoreCase);
                    var requestModule = GetDllRequestLogModule(requestPath);
                    if (!isPing)
                    {
                        _log(Logger.FormatModuleMessage(requestModule, "调试",
                            "来源=DLL，" + Logger.FormatContextMessage(
                                method + " " + requestPath,
                                requestId: requestLogId, result: "收到") +
                            $" BodyChars={(body ?? "").Length}"));
                    }

                    var isLatestPlateFramePath =
                        DllCommandHandler.IsLatestPlateFramePath(requestPath);
                    if (isLatestPlateFramePath)
                    {
                        var binaryResponse = await _commandHandler
                            .HandleLatestPlateFrameAsync(method, path, body)
                            .ConfigureAwait(false);
                        try
                        {
                            await WriteBinaryHttpResponseWithDeadlineAsync(client, stream,
                                binaryResponse.StatusCode, binaryResponse.ContentType,
                                binaryResponse.Body, binaryResponse.Headers,
                                5000).ConfigureAwait(false);
                        }
                        finally
                        {
                            binaryResponse.Dispose();
                        }

                        _log(Logger.FormatModuleMessage(requestModule, "调试",
                            "来源=DLL，" + Logger.FormatContextMessage(
                                method + " " + requestPath,
                                requestId: requestLogId,
                                result: binaryResponse.StatusCode >= 200 &&
                                        binaryResponse.StatusCode < 300 ? "成功" : "失败",
                                errorCode: binaryResponse.StatusCode >= 200 &&
                                           binaryResponse.StatusCode < 300 ? null :
                                           "http_" + binaryResponse.StatusCode,
                                durationMs: requestSw.ElapsedMilliseconds) +
                            $" HttpStatus={binaryResponse.StatusCode} ResponseBytes={binaryResponse.Body.Length}"));
                        return;
                    }

                    LogLatestFrameRouteMissIfNeeded(requestPath, requestLogId);
                    var result = await _commandHandler.HandleAsync(method, path, body);
                    await WriteHttpResponseWithDeadlineAsync(client, stream, 200,
                        result, 2000).ConfigureAwait(false);

                    if (isPing)
                    {
                        pingRecorded = true;
                        if (IsSuccessfulPingResponse(result))
                            _pingLogAggregator.RecordSuccess(requestSw.ElapsedMilliseconds);
                        else
                            _pingLogAggregator.RecordFailure("响应内容异常", false,
                                requestSw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _log(Logger.FormatModuleMessage(requestModule, "调试",
                            "来源=DLL，" + Logger.FormatContextMessage(
                                method + " " + requestPath,
                                requestId: requestLogId,
                                result: IsSuccessfulBusinessResult(result) ? "成功" : "失败",
                                errorCode: IsSuccessfulBusinessResult(result) ? null : "business_error",
                                durationMs: requestSw.ElapsedMilliseconds) +
                            $" HttpStatus=200 ResponseChars={(result ?? "").Length}"));
                    }
                }
            }
            catch (Exception ex)
            {
                if (isPing && !pingRecorded)
                {
                    _pingLogAggregator.RecordFailure(ex.GetType().Name + "：" + ex.Message,
                        true, requestSw.ElapsedMilliseconds);
                }
                else
                {
                    var requestModule = GetDllRequestLogModule(requestPath);
                    LogException(Logger.FormatModuleMessage(requestModule, "错误",
                        $"来源=DLL，HTTP处理异常：路径={requestPath}，request_id={requestLogId}，" +
                        $"耗时={requestSw.ElapsedMilliseconds}毫秒"), ex);
                }
            }
        }

        private void LogLatestFrameRouteMissIfNeeded(string path, string requestId)
        {
            if (!DllCommandHandler.IsLatestPlateFrameCandidatePath(path) ||
                Interlocked.Exchange(ref _latestFrameRouteDiagnosticLogged, 1) != 0)
                return;

            var normalizedPath = DllCommandHandler.NormalizeLatestPlateFramePath(path);
            _log(Logger.FormatModuleMessage(LogModules.PlateCapture, "警告",
                "LatestFrameDiagnostic " +
                "RouteMatched=false RouteDispatch=generic " +
                $"RawPath={JsonHelper.ToLogValue(path)} " +
                $"NormalizedPath={JsonHelper.ToLogValue(normalizedPath)} " +
                "PlateInput=unknown NormalizedPlate=unknown " +
                $"RequestId={requestId} SessionFound=unknown FrameStateFound=unknown " +
                "LastGoodFrameFound=unknown FrameValid=unknown FrameAgeMs=-1 " +
                "Generation=unknown PlayerState=unknown CacheKey=unknown " +
                "ProducerStatus=unknown Stage=RouteDispatch"));
        }

        private static bool IsSuccessfulPingResponse(string result)
        {
            return string.Equals(JsonHelper.ExtractString(result, "status"),
                "ok", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSuccessfulBusinessResult(string result)
        {
            var error = JsonHelper.ExtractString(result ?? string.Empty, "error");
            return !string.Equals(error, "true", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(error, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDllRequestLogModule(string path)
        {
            var normalizedPath = (path ?? string.Empty).Split('?')[0];
            if (normalizedPath == "/ping") return LogModules.HealthCheck;
            if (normalizedPath == "/authorize") return LogModules.Authorization;
            if (normalizedPath == "/capture/face") return LogModules.FaceCapture;
            if (normalizedPath == "/capture/fingerprint") return LogModules.FingerprintCapture;
            if (normalizedPath == "/capture/iris") return LogModules.IrisCapture;
            if (normalizedPath == "/ocr") return LogModules.DocumentRecognition;
            if (normalizedPath == "/nfc") return LogModules.NfcRead;
            if (normalizedPath == "/terminal/switch") return LogModules.TerminalSwitch;
            if (normalizedPath == "/process/start" || normalizedPath == "/process/end")
                return LogModules.ProcessControl;
            if (normalizedPath.StartsWith("/preview/", StringComparison.OrdinalIgnoreCase))
                return LogModules.Preview;
            return LogModules.UnrecognizedInterface;
        }

        private async Task HandleCallbackRequest(TcpClient client)
        {
            var callbackPath = "<未知>";
            var callbackTrace = CreateHttpTraceId();
            var callbackLogId = "<未知>";
            var resourceType = "<未知>";
            var callbackSw = Stopwatch.StartNew();
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
                    var rawRequestId = JsonHelper.ExtractString(body, "request_id");
                    if (!string.IsNullOrWhiteSpace(rawRequestId))
                        callbackTrace = rawRequestId;
                    callbackLogId = FormatRequestIdForLog(rawRequestId, callbackTrace);
                    resourceType = JsonHelper.ExtractString(body, "resource_type");
                    callbackPath = (path ?? "").Split('?')[0];

                    _log(Logger.FormatModuleMessage(LogModules.TerminalCallback, "调试",
                        $"EXE收到HTTP回调：路径={JsonHelper.ToLogValue(callbackPath)}，request_id={callbackLogId}，" +
                        $"资源={JsonHelper.ToLogValue(resourceType)}，正文长度={(body ?? "").Length}"));

                    if (string.Equals(callbackPath, "/iris-image", StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(rawRequestId) || resourceType != "iris_image"))
                    {
                        LogCallbackIngressRateLimited(
                            "callback|invalid|" + callbackPath + "|" + resourceType,
                            $"EXE拒绝回调：路径={JsonHelper.ToLogValue(callbackPath)}，" +
                            $"request_id={callbackLogId}，资源={JsonHelper.ToLogValue(resourceType)}，" +
                            "原因=invalid_iris_callback");
                        await WriteHttpResponseWithDeadlineAsync(client, stream, 400,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(rawRequestId) +
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
                        LogCallbackIngressRateLimited(
                            "callback|busy|" + callbackPath + "|" + resourceType,
                            $"EXE未受理回调：路径={JsonHelper.ToLogValue(callbackPath)}，" +
                            $"request_id={callbackLogId}，资源={JsonHelper.ToLogValue(resourceType)}，" +
                            $"耗时={callbackSw.ElapsedMilliseconds}ms，原因=service_busy");
                        await WriteHttpResponseWithDeadlineAsync(client, stream, 503,
                            "{\"request_id\":\"" + JsonHelper.EscapeString(rawRequestId) +
                            "\",\"status\":\"rejected\",\"message\":\"service busy\"," +
                            "\"error_code\":\"service_busy\"}", 30000).ConfigureAwait(false);
                        return;
                    }

                    _log(Logger.FormatModuleMessage(LogModules.TerminalCallback, "调试",
                        $"EXE已受理回调：路径={JsonHelper.ToLogValue(callbackPath)}，request_id={callbackLogId}，" +
                        $"资源={JsonHelper.ToLogValue(resourceType)}，耗时={callbackSw.ElapsedMilliseconds}ms"));
                    await WriteHttpResponseWithDeadlineAsync(client, stream, 202,
                        "{\"request_id\":\"" + JsonHelper.EscapeString(rawRequestId) +
                        "\",\"status\":\"accepted\"}", 30000).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogException(Logger.FormatModuleMessage(LogModules.TerminalCallback, "错误",
                    $"HTTP处理异常：路径={callbackPath}，request_id={callbackLogId}，" +
                    $"资源={resourceType}，耗时={callbackSw.ElapsedMilliseconds}ms"), ex);
            }
        }

        private static string FormatRequestId(string requestId)
        {
            return string.IsNullOrWhiteSpace(requestId) ? "<无>" : requestId;
        }

        private static string CreateHttpTraceId()
        {
            var sequence = Interlocked.Increment(ref _nextHttpTraceSequence);
            return "EXE_HTTP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") +
                "_" + sequence.ToString("D4");
        }

        private static string FormatRequestIdForLog(string requestId, string traceId)
        {
            if (!string.IsNullOrWhiteSpace(requestId))
                return JsonHelper.ToLogValue(requestId);
            return "<无>，日志追踪ID=" + JsonHelper.ToLogValue(traceId);
        }

        private void LogCallbackIngressRateLimited(string key, string message)
        {
            var decision = _callbackIngressRateLimiter.Record(key, message, DateTime.UtcNow);
            if (decision.EmitCurrent)
            {
                var output = LogRateLimiter.FormatMergedMessage(decision, message);
                _log(Logger.FormatModuleMessage(LogModules.TerminalCallback, "警告",
                    output));
            }
        }

        private void LogException(string context, Exception ex)
        {
            Logger.Error(context, ex);
            _log($"{context}：{ex.Message}");
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

        private static async Task WriteBinaryHttpResponseWithDeadlineAsync(TcpClient client,
            NetworkStream stream, int statusCode, string contentType, byte[] body,
            IDictionary<string, string> headers,
            int timeoutMs)
        {
            using (var cancellation = new CancellationTokenSource(timeoutMs))
            using (cancellation.Token.Register(() => CloseClient(client)))
            {
                await HttpProtocolHandler.WriteHttpResponseAsync(stream, statusCode,
                    contentType, body, headers, cancellation.Token).ConfigureAwait(false);
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
            => Require(DeviceCapability.ProcessControl, nameof(StartProcess))
                ?? _bizOps.StartProcessAsync(saveDir).GetAwaiter().GetResult();

        public string EndProcess()
            => Require(DeviceCapability.ProcessControl, nameof(EndProcess))
                ?? _bizOps.EndProcessAsync().GetAwaiter().GetResult();

        public string SwitchTerminal(int index)
            => Require(DeviceCapability.TerminalControl, nameof(SwitchTerminal))
                ?? _bizOps.SwitchTerminalAsync(index).GetAwaiter().GetResult();

        public (bool ok, string path) CaptureFace(string saveDir)
        {
            if (Require(DeviceCapability.Face, nameof(CaptureFace)) != null)
                return (false, "not_supported");
            return _bizOps.CaptureFaceAsync(saveDir).GetAwaiter().GetResult();
        }

        public (bool ok, string path) CaptureFingerprint(string saveDir)
        {
            if (Require(DeviceCapability.Fingerprint, nameof(CaptureFingerprint)) != null)
                return (false, "not_supported");
            return _bizOps.CaptureFingerprintAsync(saveDir).GetAwaiter().GetResult();
        }

        public string RequestOCR(string saveDir)
            => Require(DeviceCapability.OCR, nameof(RequestOCR))
                ?? _bizOps.RequestOCRAsync(saveDir).GetAwaiter().GetResult();

        public string RequestNfc(string saveDir)
            => Require(DeviceCapability.NfcCard, nameof(RequestNfc))
                ?? _bizOps.RequestNfcAsync(saveDir).GetAwaiter().GetResult();

        public string CaptureIris(string saveDir)
            => Require(DeviceCapability.Iris, nameof(CaptureIris))
                ?? _bizOps.CaptureIrisAsync(saveDir).GetAwaiter().GetResult();

        public AuthorizeRequestResult RequestAuthorize(string idNo, string docType,
            string nationality, string name, string sex, string birthday)
        {
            if (Require(DeviceCapability.Authorize, nameof(RequestAuthorize)) != null)
                return new AuthorizeRequestResult { Ok = false, Message = "not_supported" };
            var r = _bizOps.RequestAuthorizeAsync(idNo, docType, nationality, name, sex, birthday)
                .GetAwaiter().GetResult();
            return new AuthorizeRequestResult { Ok = r.Ok, RequestId = r.RequestId, Message = r.Message };
        }

        public Task<bool> StartLocalPreviewAsync(string resourceType, System.Windows.Forms.Control panel)
        {
            if (_capabilities.TryGetPreviewCapability(resourceType, out var capability) &&
                Require(capability, nameof(StartLocalPreviewAsync) + ":" + resourceType) != null)
                return Task.FromResult(false);
            return _bizOps.StartLocalPreviewAsync(resourceType, panel);
        }

        public void StopLocalPreview(string resourceType)
        {
            if (_capabilities.TryGetPreviewCapability(resourceType, out var capability) &&
                Require(capability, nameof(StopLocalPreview) + ":" + resourceType) != null)
                return;
            _bizOps.StopLocalPreview(resourceType);
        }

        private string Require(DeviceCapability capability, string interfaceName)
        {
            return _capabilities.IsSupported(capability) ? null :
                _capabilities.BuildNotSupportedResult(interfaceName, capability);
        }
    }
}
