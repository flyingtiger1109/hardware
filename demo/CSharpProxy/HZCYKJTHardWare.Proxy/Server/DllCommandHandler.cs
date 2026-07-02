using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using HZCYKJTHardWare.Proxy.Server.Coordinator;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class DllCommandHandler
    {
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly PreviewManager _previewManager;
        private readonly RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly ControlOperationGate _controlGate;
        private readonly Action<string> _log;
        private readonly Func<string> _getCallbackBaseUrl;
        private readonly QueueManager _queueManager;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly SwitchCoordinator _switchCoordinator;
        private readonly Action<bool> _onProcessStateChanged;
        private const string TerminalSwitchingResult =
            "{\"error\":true,\"code\":\"terminal_switching\"}";

        internal DllCommandHandler(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            DllCallbackSender dllCallback,
            PreviewManager previewManager,
            RequestRegistry requestRegistry,
            TerminalProcessRegistry processRegistry,
            ControlOperationGate controlGate,
            Action<string> log,
            Func<string> getCallbackBaseUrl,
            QueueManager queueManager,
            ActiveTasksTracker taskTracker,
            SwitchCoordinator switchCoordinator,
            Action<bool> onProcessStateChanged = null)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _dllCallback = dllCallback;
            _previewManager = previewManager;
            _requestRegistry = requestRegistry;
            _processRegistry = processRegistry;
            _controlGate = controlGate;
            _log = log;
            _getCallbackBaseUrl = getCallbackBaseUrl;
            _queueManager = queueManager;
            _taskTracker = taskTracker;
            _switchCoordinator = switchCoordinator;
            _onProcessStateChanged = onProcessStateChanged;
        }

        public async Task<string> HandleAsync(string method, string path, string bodyUtf8)
        {
            // /ping — fast path, no queuing
            if (path == "/ping")
                return "{\"status\":\"ok\"}";

            // Fast reject during terminal switch
            if (_queueManager.SwitchingTerminal)
                return TerminalSwitchingResult;

            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return TerminalSwitchingResult;

            // Parse request fields
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");
            if (string.IsNullOrEmpty(saveDir))
                saveDir = _processRegistry.GetActiveSaveDir(routeEpoch.Route.TerminalIndex);
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            switch (path)
            {
                // === Terminal Switch (highest priority, immediate response) ===
                case "/terminal/switch":
                    return HandleSwitch(bodyUtf8);

                // === Sync captures (wait for result, pass saveDir from third-party) ===
                case "/capture/face":
                    return await EnqueueCapture(_queueManager.FaceCaptureQueue, routeEpoch, saveDir);

                case "/capture/fingerprint":
                    return await EnqueueCapture(_queueManager.FingerprintCaptureQueue, routeEpoch, saveDir);

                // === Async operations (return "accepted" immediately after terminal forwards) ===
                case "/ocr":
                    return await EnqueueAsyncResource(_queueManager.OcrQueue, routeEpoch, path, 12000,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.OcrDocument);

                case "/nfc":
                    return await EnqueueAsyncResource(_queueManager.NfcQueue, routeEpoch, path, 12000,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.NfcCard);

                case "/capture/iris":
                    return await EnqueueIris(routeEpoch, requestId, saveDir, callbackUrl);

                // === Previews (replace mode, immediate "accepted") ===
                case "/preview/camera/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Camera, routeEpoch);

                case "/preview/fingerprint/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Fingerprint, routeEpoch);

                case "/preview/iris/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Iris, routeEpoch);

                case "/preview/camera/stop":
                    return HandlePreviewStop(PreviewResourceType.Camera);
                case "/preview/fingerprint/stop":
                    return HandlePreviewStop(PreviewResourceType.Fingerprint);
                case "/preview/iris/stop":
                    return HandlePreviewStop(PreviewResourceType.Iris);

                // === Preview URL queries (synchronous, no queue) ===
                case "/preview/camera/url":
                    return await HandlePreviewUrl(PreviewResourceType.Camera, routeEpoch);
                case "/preview/fingerprint/url":
                    return await HandlePreviewUrl(PreviewResourceType.Fingerprint, routeEpoch);
                case "/preview/iris/url":
                    return await HandlePreviewUrl(PreviewResourceType.Iris, routeEpoch);

                // === Process and authorization ===
                case "/process/start":
                    return await HandleProcessStart(bodyUtf8);
                case "/process/end":
                    return HandleProcessEnd();
                case "/authorize":
                    return await EnqueueAuthorize(routeEpoch, bodyUtf8, requestId, callbackUrl);

                default:
                    return "{\"error\":true,\"code\":\"not_found\"}";
            }
        }

        /// <summary>
        /// Enqueue a capture task with saveDir from the third-party request (matching Delphi logic).
        /// If saveDir has a file extension, it's used directly as the save path.
        /// </summary>
        private async Task<string> EnqueueCapture(WorkerQueue<object> queue,
            TerminalRouteEpochSnapshot routeEpoch, string saveDir)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new CaptureTaskData
            {
                Tcs = tcs,
                SaveDir = saveDir,
                RouteEpoch = routeEpoch
            };
            using (routeEpoch.CancellationToken.Register(
                () => tcs.TrySetResult(TerminalSwitchingResult)))
            {
                if (!queue.Enqueue(data, routeEpoch.Generation))
                {
                    Logger.Warn($"[队列] {queue.Name} 队列满");
                    return "{\"error\":true,\"code\":\"busy\"}";
                }
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                    return await tcs.Task;
                Logger.Error($"[队列] {queue.Name} 请求超时");
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        /// <summary>
        /// Queue an asynchronous iris capture while preserving the DLL request_id.
        /// The terminal posts the final iris_image result to the proxy callback server.
        /// </summary>
        private async Task<string> EnqueueIris(TerminalRouteEpochSnapshot routeEpoch, string requestId,
            string saveDir, string callbackUrl)
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N").Substring(0, 16);

            var resolvedSaveDir = PathHelper.SafeResolveSaveDir(saveDir);
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new IrisTaskData
            {
                Tcs = tcs,
                RequestId = requestId,
                SaveDir = resolvedSaveDir,
                DllCallbackUrl = callbackUrl,
                RouteEpoch = routeEpoch
            };

            var context = _requestRegistry.Register(requestId, ProxyResourceTypes.IrisImage,
                resolvedSaveDir, callbackUrl, routeEpoch.Generation,
                terminalIndex: routeEpoch.Route.TerminalIndex);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            using (routeEpoch.CancellationToken.Register(() =>
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                tcs.TrySetResult(TerminalSwitchingResult);
            }))
            {
                if (!_queueManager.IrisQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                    Logger.Warn("[虹膜抓拍] 虹膜任务队列已满");
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, ProxyResourceTypes.IrisImage, result);
                    return result;
                }

                Logger.Error("[虹膜抓拍] 受理请求超时(10000ms)");
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        private async Task<string> EnqueueAuthorize(TerminalRouteEpochSnapshot routeEpoch, string bodyUtf8,
            string requestId, string callbackUrl)
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N").Substring(0, 16);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new AuthorizeTaskData
            {
                Tcs = tcs,
                BodyUtf8 = bodyUtf8,
                RequestId = requestId,
                CallbackUrl = callbackUrl,
                RouteEpoch = routeEpoch
            };

            var resolvedSaveDir = PathHelper.SafeResolveSaveDir(
                string.IsNullOrEmpty(_processRegistry.GetActiveSaveDir(
                    routeEpoch.Route.TerminalIndex))
                    ? AppConfig.Instance.DefaultSaveDir
                    : _processRegistry.GetActiveSaveDir(routeEpoch.Route.TerminalIndex));
            var context = _requestRegistry.Register(requestId, ProxyResourceTypes.Protocol,
                resolvedSaveDir, callbackUrl, routeEpoch.Generation,
                terminalIndex: routeEpoch.Route.TerminalIndex);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            using (routeEpoch.CancellationToken.Register(() =>
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                tcs.TrySetResult(TerminalSwitchingResult);
            }))
            {
                if (!_queueManager.AuthorizeQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                    Logger.Warn("[授权] 授权任务队列已满");
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(12000));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, ProxyResourceTypes.Protocol, result);
                    return result;
                }

                Logger.Error("[授权] 受理请求超时(12000ms)");
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        /// <summary>
        /// Enqueue an asynchronous terminal resource request while preserving the
        /// request_id generated by the DLL.
        /// </summary>
        private async Task<string> EnqueueAsyncResource(WorkerQueue<object> queue,
            TerminalRouteEpochSnapshot routeEpoch, string path, int timeoutMs, string requestId, string saveDir,
            string callbackUrl, string resourceType)
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N").Substring(0, 16);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new AsyncResourceTaskData
            {
                Tcs = tcs,
                RequestId = requestId,
                ResourceType = resourceType,
                SaveDir = PathHelper.SafeResolveSaveDir(saveDir),
                DllCallbackUrl = callbackUrl,
                RouteEpoch = routeEpoch
            };
            var context = _requestRegistry.Register(requestId, resourceType, data.SaveDir,
                callbackUrl, routeEpoch.Generation,
                terminalIndex: routeEpoch.Route.TerminalIndex);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            using (routeEpoch.CancellationToken.Register(() =>
            {
                _requestRegistry.Fail(requestId, resourceType);
                tcs.TrySetResult(TerminalSwitchingResult);
            }))
            {
                if (!queue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, resourceType);
                    Logger.Warn($"[队列] {queue.Name} 队列满, 拒绝请求: {path}");
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                // Wait for worker to complete (with timeout)
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, resourceType, result);
                    return result;
                }

                // Timeout
                Logger.Error($"[队列] {queue.Name} 请求超时({timeoutMs}ms): {path}");
                _requestRegistry.Fail(requestId, resourceType, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        // ====== Switch (immediate response, async execution) ======

        private string HandleSwitch(string bodyUtf8)
        {
            var terminalIndex = (int)JsonHelper.ExtractInt(bodyUtf8, "terminal_index");
            if (terminalIndex < 1 || terminalIndex > 2)
                return "{\"error\":true,\"code\":\"invalid_terminal_index\"}";

            if (_terminalManager.IsSameTerminal(terminalIndex))
                return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + ",\"same_terminal\":true}";

            _log("[终端切换] 下发切换请求: " + _terminalManager.CurrentIndex + " -> " + terminalIndex);

            // Enqueue to switch worker (immediate return, don't wait)
            if (!_switchCoordinator.RequestSwitch(terminalIndex))
                return "{\"error\":true,\"code\":\"terminal_switching\"}";

            return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + "}";
        }

        // ====== Process control ======

        private async Task<string> HandleProcessStart(string bodyUtf8)
        {
            using (var controlLease = _controlGate.TryEnter("start_process"))
            {
                if (controlLease == null)
                    return "{\"error\":true,\"code\":\"busy\"}";

                if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                    return TerminalSwitchingResult;
                var route = routeEpoch.Route;
                var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = _processRegistry.GetActiveSaveDir(route.TerminalIndex);
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = AppConfig.Instance.DefaultSaveDir;
                var resolvedSaveDir = PathHelper.SafeResolveSaveDir(saveDir);

                var callbackBase = _getCallbackBaseUrl();
                var irisCallback = BuildIrisCallbackUrl(callbackBase);
                var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
                if (string.IsNullOrEmpty(requestId))
                    requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

                var registration = _processRegistry.Prepare(route.TerminalIndex,
                    route.BaseUrl, requestId, resolvedSaveDir, routeEpoch.Generation);
                if (registration == null)
                    return "{\"error\":true,\"code\":\"busy\"}";

                var body = $"{{\"request_id\":\"{requestId}\"," +
                    $"\"callbacks\":{{" +
                    $"\"ocr_document\":\"{callbackBase}\"," +
                    $"\"ocr_event_status\":\"{callbackBase}\"," +
                    $"\"nfc_card\":\"{callbackBase}\"," +
                    $"\"iris_image\":\"{irisCallback}\"}}}}";

                var committed = false;
                try
                {
                    _log("[流程] 开始流程: terminal=" + route.TerminalIndex +
                        ", url=" + route.BaseUrl + "/process/start, save_dir=" +
                        resolvedSaveDir);

                    var (ok, _) = await _terminalClient.PostJsonAsync(route.BaseUrl,
                        "/process/start", body, 5000, routeEpoch.CancellationToken)
                        .ConfigureAwait(false);
                    if (!ok || !_processRegistry.Commit(registration))
                        return "{\"error\":true,\"code\":\"terminal_request_failed\"}";

                    committed = true;
                    _terminalManager.ProcessSaveDir = resolvedSaveDir;
                    _terminalManager.ProcessActive = true;
                    _onProcessStateChanged?.Invoke(true);
                    _log("[流程] 流程已开始, terminal=" + route.TerminalIndex +
                        ", request_id=" + requestId + ", save_dir=" + resolvedSaveDir);
                    return "{\"status\":\"ok\"}";
                }
                finally
                {
                    if (!committed)
                        _processRegistry.Rollback(registration);
                }
            }
        }

        private string HandleProcessEnd()
        {
            using (var controlLease = _controlGate.TryEnter("end_process"))
            {
                if (controlLease == null)
                    return "{\"error\":true,\"code\":\"busy\"}";

                _processRegistry.ClearAll();
                _terminalManager.ProcessActive = false;
                _onProcessStateChanged?.Invoke(false);
                _terminalManager.ProcessSaveDir = "";
                _requestRegistry.CancelAll();
                _log("[流程] 流程已结束");
                return "{\"status\":\"ok\"}";
            }
        }

        public void ClearAllMappings()
        {
            _requestRegistry.CancelAll();
            _processRegistry.ClearAll();
        }

        private void CleanupRegistryForQueueFailure(string requestId, string resourceType,
            string result)
        {
            var code = JsonHelper.ExtractString(result, "code");
            if (code == "queue_replaced" || code == "service_stopping" ||
                code == "terminal_switching")
            {
                _requestRegistry.Fail(requestId, resourceType);
            }
        }

        // ====== Preview Start (replace mode, immediate "accepted") ======

        private async Task<string> HandlePreviewStart(string bodyUtf8,
            PreviewResourceType resType, TerminalRouteEpochSnapshot routeEpoch)
        {
            var hwndValue = JsonHelper.ExtractInt(bodyUtf8, "hwnd");
            var hwnd = new IntPtr(hwndValue);
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                _log("[预览管理] 目标窗口句柄无效, hwnd=" + hwndValue);
                return "{\"error\":true,\"code\":\"invalid_target_hwnd\"}";
            }

            // Start preview on thread pool (not blocking HTTP), then send callback
            var terminalBaseUrl = routeEpoch.Route.BaseUrl;
            string resourceName;
            switch (resType)
            {
                case PreviewResourceType.Camera: resourceName = "face_image"; break;
                case PreviewResourceType.Fingerprint: resourceName = "fingerprint_image"; break;
                case PreviewResourceType.Iris: resourceName = "iris_image"; break;
                default: resourceName = "unknown"; break;
            }

            // Execute preview start asynchronously (don't block HTTP response)
            var taskAccepted = _taskTracker.TryRun(async () =>
            {
                try
                {
                    Func<bool> shouldContinue = () =>
                        !routeEpoch.IsCancellationRequested &&
                        !_queueManager.SwitchingTerminal &&
                        _queueManager.IsGenerationValid(routeEpoch.Generation);

                    if (!shouldContinue())
                    {
                        _log($"[预览管理] 外部预览已跳过: {resType}, 原因=终端正在切换或请求已过期, hwnd={hwnd}");
                        return;
                    }

                    var ok = await _previewManager.StartPreview(resType, PreviewSessionType.External, hwnd, terminalBaseUrl, shouldContinue: shouldContinue);
                    if (ok)
                    {
                        if (!shouldContinue())
                        {
                            _previewManager.StopPreview(resType, PreviewSessionType.External);
                            _log($"[预览管理] 外部预览启动后发现终端已切换，等待切换流程接管: {resType}, hwnd={hwnd}");
                            return;
                        }

                        await HideMainFormToTrayAsync().ConfigureAwait(false);

                        if (!shouldContinue())
                        {
                            _previewManager.StopPreview(resType, PreviewSessionType.External);
                            _log($"[预览管理] 外部预览最小化窗口后发现终端已切换，等待切换流程接管: {resType}, hwnd={hwnd}");
                            return;
                        }

                        if (!string.IsNullOrEmpty(callbackUrl))
                            await _dllCallback.SendPreviewReady(requestId, resourceName, hwnd, IntPtr.Zero).ConfigureAwait(false);
                        _log($"[预览管理] 外部预览已启动: {resType}, hwnd={hwnd}");
                    }
                    else
                    {
                        if (!shouldContinue())
                        {
                            _log($"[预览管理] 外部预览启动已过期，跳过失败回调: {resType}, hwnd={hwnd}");
                            return;
                        }

                        if (!string.IsNullOrEmpty(callbackUrl))
                        {
                            var errPayload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\",\"resource_type\":\"" + resourceName + "\",\"render_hwnd\":" + hwndValue + ",\"error\":true,\"code\":\"preview_failed\"}";
                            await _dllCallback.PostCallbackRaw("/preview-ready", errPayload).ConfigureAwait(false);
                        }
                        _log($"[预览管理] 外部预览启动失败: {resType}, hwnd={hwnd}");
                    }
                }
                catch (Exception ex)
                {
                    _log($"[预览管理] 外部预览启动异常: {ex.Message}");
                }
            }, "preview_start_external");

            if (!taskAccepted)
                return "{\"error\":true,\"code\":\"service_busy\"}";

            return "{\"accepted\":true}";
        }

        private string HandlePreviewStop(PreviewResourceType resType)
        {
            _previewManager.StopPreview(resType, PreviewSessionType.External);
            return "{\"status\":\"ok\"}";
        }

        private async Task<string> HandlePreviewUrl(PreviewResourceType resType,
            TerminalRouteEpochSnapshot routeEpoch)
        {
            if (routeEpoch.IsCancellationRequested)
                return TerminalSwitchingResult;
            var terminalBaseUrl = routeEpoch.Route.BaseUrl;
            var previewUrl = await _previewManager.RequestPreviewUrl(resType, terminalBaseUrl);
            if (routeEpoch.IsCancellationRequested)
                return TerminalSwitchingResult;
            if (!string.IsNullOrEmpty(previewUrl))
                return "{\"status\":\"ok\",\"preview_url\":\"" + JsonHelper.EscapeString(previewUrl) + "\"}";
            return "{\"error\":true,\"code\":\"preview_url_failed\"}";
        }

        /// <summary>
        /// Hide the main window to the tray before notifying the third-party UI that preview is ready.
        /// Waiting for this UI action keeps the external preview callback in the expected window state.
        /// </summary>
        private static async Task<bool> HideMainFormToTrayAsync()
        {
            try
            {
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] as MainForm : null;
                if (form == null || form.IsDisposed || !form.IsHandleCreated)
                    return false;

                if (!form.InvokeRequired)
                {
                    form.HideToTrayForExternalPreview();
                    return true;
                }

                var tcs = new TaskCompletionSource<bool>();
                form.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!form.IsDisposed)
                            form.HideToTrayForExternalPreview();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000)).ConfigureAwait(false);
                if (completed != tcs.Task)
                    return false;

                await tcs.Task.ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildIrisCallbackUrl(string callbackBase)
        {
            var origin = (callbackBase ?? "").TrimEnd('/');
            var configuredPath = (AppConfig.Instance.CallbackPath ?? "").TrimEnd('/');
            if (!string.IsNullOrEmpty(configuredPath) &&
                origin.EndsWith(configuredPath, StringComparison.OrdinalIgnoreCase))
            {
                origin = origin.Substring(0, origin.Length - configuredPath.Length).TrimEnd('/');
            }
            return origin + "/iris-image";
        }
    }

    /// <summary>
    /// Data passed to capture queue workers — includes third-party's saveDir.
    /// </summary>
    public class CaptureTaskData : IQueueResultSink
    {
        public TaskCompletionSource<string> Tcs { get; set; }
        public string SaveDir { get; set; }
        public TerminalRouteEpochSnapshot RouteEpoch { get; set; }
        public bool IsQueueResultCompleted => Tcs == null || Tcs.Task.IsCompleted;

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }

    /// <summary>
    /// Data required to submit one asynchronous iris capture to the terminal.
    /// </summary>
    public class IrisTaskData : IQueueResultSink
    {
        public TaskCompletionSource<string> Tcs { get; set; }
        public string RequestId { get; set; }
        public string SaveDir { get; set; }
        public string DllCallbackUrl { get; set; }
        public TerminalRouteEpochSnapshot RouteEpoch { get; set; }
        public bool IsQueueResultCompleted => Tcs == null || Tcs.Task.IsCompleted;

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }

    /// <summary>
    /// Data required to submit an OCR or NFC request without regenerating request_id.
    /// </summary>
    public class AsyncResourceTaskData : IQueueResultSink
    {
        public TaskCompletionSource<string> Tcs { get; set; }
        public string RequestId { get; set; }
        public string ResourceType { get; set; }
        public string SaveDir { get; set; }
        public string DllCallbackUrl { get; set; }
        public TerminalRouteEpochSnapshot RouteEpoch { get; set; }
        public bool IsQueueResultCompleted => Tcs == null || Tcs.Task.IsCompleted;

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }

    /// <summary>
    /// Data required to submit one asynchronous authorization request.
    /// </summary>
    public class AuthorizeTaskData : IQueueResultSink
    {
        public TaskCompletionSource<string> Tcs { get; set; }
        public string BodyUtf8 { get; set; }
        public string RequestId { get; set; }
        public string CallbackUrl { get; set; }
        public TerminalRouteEpochSnapshot RouteEpoch { get; set; }
        public bool IsQueueResultCompleted => Tcs == null || Tcs.Task.IsCompleted;

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }
}
