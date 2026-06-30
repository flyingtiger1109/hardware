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
        private readonly Action<string> _log;
        private readonly Func<string> _getCallbackBaseUrl;
        private readonly QueueManager _queueManager;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly SwitchCoordinator _switchCoordinator;
        private readonly Action<bool> _onProcessStateChanged;

        internal DllCommandHandler(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            DllCallbackSender dllCallback,
            PreviewManager previewManager,
            RequestRegistry requestRegistry,
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
                return "{\"error\":true,\"code\":\"terminal_switching\"}";

            // Parse request fields
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");
            if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            var gen = _queueManager.TerminalGeneration;

            switch (path)
            {
                // === Terminal Switch (highest priority, immediate response) ===
                case "/terminal/switch":
                    return HandleSwitch(bodyUtf8);

                // === Sync captures (wait for result, pass saveDir from third-party) ===
                case "/capture/face":
                    return await EnqueueCapture(_queueManager.FaceCaptureQueue, gen, saveDir);

                case "/capture/fingerprint":
                    return await EnqueueCapture(_queueManager.FingerprintCaptureQueue, gen, saveDir);

                // === Async operations (return "accepted" immediately after terminal forwards) ===
                case "/ocr":
                    return await EnqueueAsyncResource(_queueManager.OcrQueue, gen, path, 12000,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.OcrDocument);

                case "/nfc":
                    return await EnqueueAsyncResource(_queueManager.NfcQueue, gen, path, 12000,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.NfcCard);

                case "/capture/iris":
                    return await EnqueueIris(gen, requestId, saveDir, callbackUrl);

                // === Previews (replace mode, immediate "accepted") ===
                case "/preview/camera/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Camera, gen);

                case "/preview/fingerprint/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Fingerprint, gen);

                case "/preview/iris/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Iris, gen);

                case "/preview/camera/stop":
                    return HandlePreviewStop(PreviewResourceType.Camera);
                case "/preview/fingerprint/stop":
                    return HandlePreviewStop(PreviewResourceType.Fingerprint);
                case "/preview/iris/stop":
                    return HandlePreviewStop(PreviewResourceType.Iris);

                // === Preview URL queries (synchronous, no queue) ===
                case "/preview/camera/url":
                    return await HandlePreviewUrl(PreviewResourceType.Camera);
                case "/preview/fingerprint/url":
                    return await HandlePreviewUrl(PreviewResourceType.Fingerprint);
                case "/preview/iris/url":
                    return await HandlePreviewUrl(PreviewResourceType.Iris);

                // === Process and authorization ===
                case "/process/start":
                    return await HandleProcessStart(bodyUtf8, gen);
                case "/process/end":
                    return HandleProcessEnd();
                case "/authorize":
                    return await EnqueueAuthorize(gen, bodyUtf8, requestId, callbackUrl);

                default:
                    return "{\"error\":true,\"code\":\"not_found\"}";
            }
        }

        /// <summary>
        /// Enqueue a capture task with saveDir from the third-party request (matching Delphi logic).
        /// If saveDir has a file extension, it's used directly as the save path.
        /// </summary>
        private async Task<string> EnqueueCapture(WorkerQueue<object> queue, int generation, string saveDir)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new CaptureTaskData { Tcs = tcs, SaveDir = saveDir };
            if (!queue.Enqueue(data, generation))
            {
                Logger.Warn($"[队列] {queue.Name} 队列满");
                return "{\"error\":true,\"code\":\"busy\"}";
            }
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            if (completed == tcs.Task && tcs.Task.IsCompleted)
                return await tcs.Task;
            Logger.Error($"[队列] {queue.Name} 请求超时");
            return "{\"error\":true,\"code\":\"timeout\"}";
        }

        /// <summary>
        /// Queue an asynchronous iris capture while preserving the DLL request_id.
        /// The terminal posts the final iris_image result to the proxy callback server.
        /// </summary>
        private async Task<string> EnqueueIris(int generation, string requestId,
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
                DllCallbackUrl = callbackUrl
            };

            var context = _requestRegistry.Register(requestId, ProxyResourceTypes.IrisImage,
                resolvedSaveDir, callbackUrl, generation);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            if (!_queueManager.IrisQueue.Enqueue(data, generation))
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
            return "{\"error\":true,\"code\":\"timeout\"}";
        }

        private async Task<string> EnqueueAuthorize(int generation, string bodyUtf8,
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
                CallbackUrl = callbackUrl
            };

            var resolvedSaveDir = PathHelper.SafeResolveSaveDir(
                string.IsNullOrEmpty(_terminalManager.ProcessSaveDir)
                    ? AppConfig.Instance.DefaultSaveDir
                    : _terminalManager.ProcessSaveDir);
            var context = _requestRegistry.Register(requestId, ProxyResourceTypes.Protocol,
                resolvedSaveDir, callbackUrl, generation);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            if (!_queueManager.AuthorizeQueue.Enqueue(data, generation))
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
            return "{\"error\":true,\"code\":\"timeout\"}";
        }

        /// <summary>
        /// Enqueue an asynchronous terminal resource request while preserving the
        /// request_id generated by the DLL.
        /// </summary>
        private async Task<string> EnqueueAsyncResource(WorkerQueue<object> queue,
            int generation, string path, int timeoutMs, string requestId, string saveDir,
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
                DllCallbackUrl = callbackUrl
            };
            var context = _requestRegistry.Register(requestId, resourceType, data.SaveDir,
                callbackUrl, generation);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            if (!queue.Enqueue(data, generation))
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
            return "{\"error\":true,\"code\":\"timeout\"}";
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

        private async Task<string> HandleProcessStart(string bodyUtf8, int gen)
        {
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            _terminalManager.ProcessSaveDir = PathHelper.SafeResolveSaveDir(saveDir);

            var callbackBase = _getCallbackBaseUrl();
            var irisCallback = BuildIrisCallbackUrl(callbackBase);
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
            if (string.IsNullOrEmpty(requestId))
                requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

            var dllCallbacks = JsonHelper.ExtractObject(bodyUtf8, "callbacks");
            var ocrDllCallback = JsonHelper.ExtractString(dllCallbacks, "ocr");
            var nfcDllCallback = JsonHelper.ExtractString(dllCallbacks, "nfc");
            var irisDllCallback = JsonHelper.ExtractString(dllCallbacks, "iris");
            if (string.IsNullOrEmpty(ocrDllCallback))
                ocrDllCallback = AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr";
            if (string.IsNullOrEmpty(nfcDllCallback))
                nfcDllCallback = AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card";
            if (string.IsNullOrEmpty(irisDllCallback))
                irisDllCallback = AppConfig.Instance.GetDllCallbackBaseUrl() + "/iris";

            if (!RegisterProcessResource(requestId, ProxyResourceTypes.OcrDocument,
                    ocrDllCallback, gen) ||
                !RegisterProcessResource(requestId, ProxyResourceTypes.NfcCard,
                    nfcDllCallback, gen) ||
                !RegisterProcessResource(requestId, ProxyResourceTypes.IrisImage,
                    irisDllCallback, gen))
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.OcrDocument);
                _requestRegistry.Fail(requestId, ProxyResourceTypes.NfcCard);
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                return "{\"error\":true,\"code\":\"registry_full\"}";
            }
            var body = $"{{\"request_id\":\"{requestId}\"," +
                $"\"callbacks\":{{" +
                $"\"ocr_document\":\"{callbackBase}\"," +
                $"\"ocr_event_status\":\"{callbackBase}\"," +
                $"\"nfc_card\":\"{callbackBase}\"," +
                $"\"iris_image\":\"{irisCallback}\"}}}}";

            _log("[流程] 开始流程: url=" + _terminalManager.CurrentBaseUrl + "/process/start, save_dir=" + _terminalManager.ProcessSaveDir);

            var (ok, _) = await _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/process/start", body, 5000).ConfigureAwait(false);
            if (ok)
            {
                var accepted =
                    _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.OcrDocument) &
                    _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.NfcCard) &
                    _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.IrisImage);
                if (!accepted)
                    return "{\"error\":true,\"code\":\"terminal_switching\"}";
                _terminalManager.ProcessActive = true;
                _onProcessStateChanged?.Invoke(true);
                _log("[流程] 流程已开始, save_dir=" + _terminalManager.ProcessSaveDir);
                return "{\"status\":\"ok\"}";
            }
            _requestRegistry.Fail(requestId, ProxyResourceTypes.OcrDocument);
            _requestRegistry.Fail(requestId, ProxyResourceTypes.NfcCard);
            _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private bool RegisterProcessResource(string requestId, string resourceType,
            string dllCallbackUrl, int generation)
        {
            var context = _requestRegistry.Register(requestId, resourceType,
                _terminalManager.ProcessSaveDir, dllCallbackUrl, generation, processFlow: true);
            if (context == null)
                return false;
            context.TryMarkSubmitting();
            return true;
        }

        private string HandleProcessEnd()
        {
            _terminalManager.ProcessActive = false;
            _onProcessStateChanged?.Invoke(false);
            _terminalManager.ProcessSaveDir = "";
            _requestRegistry.CancelAll();
            _log("[流程] 流程已结束");
            return "{\"status\":\"ok\"}";
        }

        public void ClearAllMappings()
        {
            _requestRegistry.CancelAll();
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

        private async Task<string> HandlePreviewStart(string bodyUtf8, PreviewResourceType resType, int gen)
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
            var terminalBaseUrl = _terminalManager.CurrentBaseUrl;
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
                    Func<bool> shouldContinue = () => !_queueManager.SwitchingTerminal && _queueManager.IsGenerationValid(gen);

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

                        await MinimizeMainFormAsync().ConfigureAwait(false);

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

        private async Task<string> HandlePreviewUrl(PreviewResourceType resType)
        {
            var terminalBaseUrl = _terminalManager.CurrentBaseUrl;
            var previewUrl = await _previewManager.RequestPreviewUrl(resType, terminalBaseUrl);
            if (!string.IsNullOrEmpty(previewUrl))
                return "{\"status\":\"ok\",\"preview_url\":\"" + JsonHelper.EscapeString(previewUrl) + "\"}";
            return "{\"error\":true,\"code\":\"preview_url_failed\"}";
        }

        /// <summary>
        /// Minimize the main window before notifying the third-party UI that preview is ready.
        /// Waiting for this UI action keeps the external preview callback in the expected window state.
        /// </summary>
        private static async Task<bool> MinimizeMainFormAsync()
        {
            try
            {
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] as MainForm : null;
                if (form == null || form.IsDisposed || !form.IsHandleCreated)
                    return false;

                if (!form.InvokeRequired)
                {
                    form.SetMinimizeToTaskbar();
                    return true;
                }

                var tcs = new TaskCompletionSource<bool>();
                form.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!form.IsDisposed)
                            form.SetMinimizeToTaskbar();
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

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }
}
