using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Preview;
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
        private readonly ConcurrentDictionary<string, string> _requestSaveDirs;
        private readonly ConcurrentDictionary<string, string> _requestCallbacks;
        private readonly Action<string> _log;
        private readonly Func<string> _getCallbackBaseUrl;
        private readonly QueueManager _queueManager;
        private int _requestCount;

        public DllCommandHandler(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            DllCallbackSender dllCallback,
            PreviewManager previewManager,
            ConcurrentDictionary<string, string> requestSaveDirs,
            ConcurrentDictionary<string, string> requestCallbacks,
            Action<string> log,
            Func<string> getCallbackBaseUrl,
            QueueManager queueManager)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _dllCallback = dllCallback;
            _previewManager = previewManager;
            _requestSaveDirs = requestSaveDirs;
            _requestCallbacks = requestCallbacks;
            _log = log;
            _getCallbackBaseUrl = getCallbackBaseUrl;
            _queueManager = queueManager;
        }

        public async Task<string> HandleAsync(string method, string path, string bodyUtf8)
        {
            // /ping — fast path, no queuing
            if (path == "/ping")
                return "{\"status\":\"ok\"}";

            // Fast reject during terminal switch
            if (_queueManager.SwitchingTerminal)
                return "{\"error\":true,\"code\":\"terminal_switching\"}";

            // Dictionary cleanup
            if (++_requestCount % 500 == 0)
            {
                if (_requestSaveDirs.Count > 2000) _requestSaveDirs.Clear();
                if (_requestCallbacks.Count > 2000) _requestCallbacks.Clear();
            }

            // Parse request fields
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");
            if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            if (!string.IsNullOrEmpty(callbackUrl) && !string.IsNullOrEmpty(requestId))
            {
                _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
                _requestCallbacks[requestId] = callbackUrl;
            }

            var gen = _queueManager.TerminalGeneration;

            switch (path)
            {
                // === Terminal Switch (highest priority, immediate response) ===
                case "/terminal/switch":
                    return HandleSwitch(bodyUtf8, gen);

                // === Sync captures (wait for result, pass saveDir from third-party) ===
                case "/capture/face":
                    return await EnqueueCapture(_queueManager.FaceCaptureQueue, gen, saveDir);

                case "/capture/fingerprint":
                    return await EnqueueCapture(_queueManager.FingerprintCaptureQueue, gen, saveDir);

                // === Async operations (return "accepted" immediately after terminal forwards) ===
                case "/ocr":
                    return await EnqueueWithResult(_queueManager.OcrQueue, gen, path, 10000);

                case "/nfc":
                    return await EnqueueWithResult(_queueManager.NfcQueue, gen, path, 10000);

                case "/capture/iris":
                    return await EnqueueWithResult(_queueManager.MiscQueue, gen, path, 10000);

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

                // === Misc (process, authorize) ===
                case "/process/start":
                    return await HandleProcessStart(bodyUtf8, gen);
                case "/process/end":
                    return HandleProcessEnd();
                case "/authorize":
                    return await HandleAuthorizeDirect(bodyUtf8, requestId, callbackUrl);

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
        /// Enqueue a task to a worker queue and wait for the result.
        /// </summary>
        private async Task<string> EnqueueWithResult(WorkerQueue<object> queue, int generation, string path, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!queue.Enqueue(tcs, generation))
            {
                // Queue full — immediate busy response
                Logger.Warn($"[队列] {queue.Name} 队列满, 拒绝请求: {path}");
                return "{\"error\":true,\"code\":\"busy\"}";
            }

            // Wait for worker to complete (with timeout)
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (completed == tcs.Task && tcs.Task.IsCompleted)
            {
                return await tcs.Task;
            }

            // Timeout
            Logger.Error($"[队列] {queue.Name} 请求超时({timeoutMs}ms): {path}");
            return "{\"error\":true,\"code\":\"timeout\"}";
        }

        // ====== Switch (immediate response, async execution) ======

        private string HandleSwitch(string bodyUtf8, int gen)
        {
            var terminalIndex = (int)JsonHelper.ExtractInt(bodyUtf8, "terminal_index");
            if (terminalIndex < 1 || terminalIndex > 2)
                return "{\"error\":true,\"code\":\"invalid_terminal_index\"}";

            if (_terminalManager.IsSameTerminal(terminalIndex))
                return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + ",\"same_terminal\":true}";

            _log("[终端切换] 下发切换请求: " + _terminalManager.CurrentIndex + " -> " + terminalIndex);

            // Enqueue to switch worker (immediate return, don't wait)
            _queueManager.RequestSwitch(terminalIndex, gen);

            return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + "}";
        }

        // ====== Process / Authorize (direct execution, no queue needed — they're quick) ======

        private async Task<string> HandleProcessStart(string bodyUtf8, int gen)
        {
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            _terminalManager.ProcessSaveDir = PathHelper.SafeResolveSaveDir(saveDir);

            var callbackBase = _getCallbackBaseUrl();
            var requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"," +
                $"\"callbacks\":{{" +
                $"\"ocr_document\":\"{callbackBase}\"," +
                $"\"ocr_event_status\":\"{callbackBase}\"," +
                $"\"nfc_card\":\"{callbackBase}\"}}}}";

            _log("[流程] 开始流程: url=" + _terminalManager.CurrentBaseUrl + "/process/start, save_dir=" + _terminalManager.ProcessSaveDir);

            var (ok, _) = await _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/process/start", body, 5000).ConfigureAwait(false);
            if (ok)
            {
                _terminalManager.ProcessActive = true;
                _log("[流程] 流程已开始, save_dir=" + _terminalManager.ProcessSaveDir);
                return "{\"status\":\"ok\"}";
            }
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private string HandleProcessEnd()
        {
            _terminalManager.ProcessActive = false;
            _terminalManager.ProcessSaveDir = "";
            _requestSaveDirs.Clear();
            _requestCallbacks.Clear();
            _log("[流程] 流程已结束");
            return "{\"status\":\"ok\"}";
        }

        private async Task<string> HandleAuthorizeDirect(string bodyUtf8, string requestId, string callbackUrl)
        {
            if (!string.IsNullOrEmpty(callbackUrl) && !string.IsNullOrEmpty(requestId))
                _requestCallbacks[requestId] = callbackUrl;

            var name = JsonHelper.ExtractString(bodyUtf8, "XM");
            var sex = JsonHelper.ExtractString(bodyUtf8, "XB");
            var idNo = JsonHelper.ExtractString(bodyUtf8, "ZJHM");
            var docType = JsonHelper.ExtractString(bodyUtf8, "ZJLB");
            var birthday = JsonHelper.ExtractString(bodyUtf8, "CSRQ");
            var nationality = JsonHelper.ExtractString(bodyUtf8, "GJDQDM");
            var portCode = JsonHelper.ExtractString(bodyUtf8, "KADM");

            var callbackBase = _getCallbackBaseUrl();
            var terminalBody = "{" +
                "\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"," +
                "\"name\":\"" + JsonHelper.EscapeString(name) + "\"," +
                "\"sex\":\"" + JsonHelper.EscapeString(sex) + "\"," +
                "\"id_no\":\"" + JsonHelper.EscapeString(idNo) + "\"," +
                "\"doc_type\":\"" + JsonHelper.EscapeString(docType) + "\"," +
                "\"birthday\":\"" + JsonHelper.EscapeString(birthday) + "\"," +
                "\"nationality\":\"" + JsonHelper.EscapeString(nationality) + "\"," +
                "\"port_code\":\"" + JsonHelper.EscapeString(portCode) + "\"," +
                "\"callback_url\":\"" + JsonHelper.EscapeString(callbackBase) + "\"" +
                "}";

            _log("[授权] 转发至终端: request_id=" + requestId);

            var (ok, response) = await _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/protocol/request", terminalBody, 5000).ConfigureAwait(false);
            if (ok)
            {
                _log("[授权] 已受理: request_id=" + requestId);
                return "{\"accepted\":true}";
            }

            var code = ResultParser.ExtractErrorCode(response);
            var message = ResultParser.ExtractErrorMessage(response);
            var detail = ResultParser.FormatErrorDetail(response, "终端授权请求失败");
            _log("[授权] 下发失败: request_id=" + requestId + ", " + detail);

            if (string.IsNullOrEmpty(code))
                code = "terminal_request_failed";
            return "{\"error\":true,\"code\":\"" + JsonHelper.EscapeString(code) + "\",\"message\":\"" + JsonHelper.EscapeString(message) + "\"}";
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
            var resourceName = resType switch
            {
                PreviewResourceType.Camera => "face_image",
                PreviewResourceType.Fingerprint => "fingerprint_image",
                PreviewResourceType.Iris => "iris_image",
                _ => "unknown"
            };

            // Execute preview start asynchronously (don't block HTTP response)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_queueManager.SwitchingTerminal || !_queueManager.IsGenerationValid(gen))
                    {
                        _log($"[预览管理] 外部预览已跳过: {resType}, 原因=终端正在切换或请求已过期, hwnd={hwnd}");
                        return;
                    }

                    var ok = await _previewManager.StartPreview(resType, PreviewSessionType.External, hwnd, terminalBaseUrl);
                    if (ok)
                    {
                        if (_queueManager.SwitchingTerminal || !_queueManager.IsGenerationValid(gen))
                        {
                            _log($"[预览管理] 外部预览启动后发现终端已切换，等待切换流程接管: {resType}, hwnd={hwnd}");
                            return;
                        }

                        if (!string.IsNullOrEmpty(callbackUrl))
                            await _dllCallback.SendPreviewReady(requestId, resourceName, hwnd, IntPtr.Zero).ConfigureAwait(false);
                        _log($"[预览管理] 外部预览已启动: {resType}, hwnd={hwnd}");

                        // DLL-triggered preview success → minimize proxy window to taskbar
                        MinimizeMainForm();
                    }
                    else
                    {
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
            });

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
        /// Minimize the main window after DLL-triggered preview starts successfully.
        /// MainForm handles minimized state by hiding itself to the system tray.
        /// </summary>
        private static void MinimizeMainForm()
        {
            try
            {
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (form != null && form.InvokeRequired)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        form.WindowState = FormWindowState.Minimized;
                    }));
                }
                else if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                }
            }
            catch { /* Best-effort, must not crash */ }
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
}
