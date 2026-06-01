using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private volatile bool _switchingTerminal;
        private int _requestCount;  // For periodic dictionary cleanup

        public DllCommandHandler(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            DllCallbackSender dllCallback,
            PreviewManager previewManager,
            ConcurrentDictionary<string, string> requestSaveDirs,
            ConcurrentDictionary<string, string> requestCallbacks,
            Action<string> log,
            Func<string> getCallbackBaseUrl)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _dllCallback = dllCallback;
            _previewManager = previewManager;
            _requestSaveDirs = requestSaveDirs;
            _requestCallbacks = requestCallbacks;
            _log = log;
            _getCallbackBaseUrl = getCallbackBaseUrl;
        }

        public async Task<string> HandleAsync(string method, string path, string bodyUtf8)
        {
            // /ping
            if (path == "/ping")
                return "{\"status\":\"ok\"}";

            // Terminal switch guard: reject new operations during async switch (same as Delphi FSwitchingTerminal)
            if (_switchingTerminal)
            {
                _log("[终端切换] 正在切换终端，拦截请求: " + path);
                return "{\"error\":true,\"code\":\"terminal_switching\"}";
            }

            // Periodic cleanup: prevent unbounded dictionary growth (memory leak protection)
            if (++_requestCount % 500 == 0)
            {
                var maxEntries = 2000;
                if (_requestSaveDirs.Count > maxEntries) _requestSaveDirs.Clear();
                if (_requestCallbacks.Count > maxEntries) _requestCallbacks.Clear();
            }

            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
            var saveDir = JsonHelper.ExtractString(bodyUtf8, "save_dir");
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");

            if (string.IsNullOrEmpty(saveDir))
                saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir))
                saveDir = AppConfig.Instance.DefaultSaveDir;

            // Store request data
            if (!string.IsNullOrEmpty(callbackUrl) && !string.IsNullOrEmpty(requestId))
            {
                _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
                _requestCallbacks[requestId] = callbackUrl;
            }

            var terminalBaseUrl = _terminalManager.CurrentBaseUrl;

            // Route by path
            switch (path)
            {
                case "/terminal/switch":
                    return HandleTerminalSwitch(bodyUtf8);

                case "/process/start":
                    return await HandleProcessStart(saveDir);

                case "/process/end":
                    return HandleProcessEnd();

                case "/capture/face":
                    return await HandleCaptureFace(requestId, saveDir);

                case "/capture/fingerprint":
                    return await HandleCaptureFingerprint(requestId, saveDir);

                case "/capture/iris":
                    return await HandleCaptureIris(requestId, saveDir, callbackUrl, terminalBaseUrl);

                case "/ocr":
                    return await HandleOcr(requestId, saveDir, callbackUrl, terminalBaseUrl);

                case "/nfc":
                    return await HandleNfc(requestId, saveDir, callbackUrl, terminalBaseUrl);

                case "/authorize":
                    return await HandleAuthorize(bodyUtf8, requestId, callbackUrl, terminalBaseUrl);

                case "/preview/camera/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Camera, PreviewSessionType.External);

                case "/preview/camera/stop":
                    return HandlePreviewStop(PreviewResourceType.Camera, PreviewSessionType.External);

                case "/preview/fingerprint/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Fingerprint, PreviewSessionType.External);

                case "/preview/fingerprint/stop":
                    return HandlePreviewStop(PreviewResourceType.Fingerprint, PreviewSessionType.External);

                case "/preview/iris/start":
                    return await HandlePreviewStart(bodyUtf8, PreviewResourceType.Iris, PreviewSessionType.External);

                case "/preview/iris/stop":
                    return HandlePreviewStop(PreviewResourceType.Iris, PreviewSessionType.External);

                case "/preview/camera/url":
                    return await HandlePreviewUrl(PreviewResourceType.Camera, terminalBaseUrl);

                case "/preview/fingerprint/url":
                    return await HandlePreviewUrl(PreviewResourceType.Fingerprint, terminalBaseUrl);

                case "/preview/iris/url":
                    return await HandlePreviewUrl(PreviewResourceType.Iris, terminalBaseUrl);

                default:
                    return "{\"error\":true,\"code\":\"not_found\",\"message\":\"unknown:" + JsonHelper.EscapeString(path) + "\"}";
            }
        }

        private string HandleTerminalSwitch(string bodyUtf8)
        {
            var terminalIndex = JsonHelper.ExtractInt(bodyUtf8, "terminal_index");
            if (terminalIndex < 1 || terminalIndex > 2)
                return "{\"error\":true,\"code\":\"invalid_terminal_index\"}";

            var isSame = _terminalManager.IsSameTerminal(terminalIndex);
            if (isSame)
            {
                return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + ",\"same_terminal\":true}";
            }

            // Set switching guard to block new requests during async switch (same as Delphi FSwitchingTerminal)
            _switchingTerminal = true;
            _log("[终端切换] 正在切换到终端" + _terminalManager.CurrentIndex + " -> 终端" + terminalIndex);

            // Fire and forget: switch terminal and restart previews asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var stopWatch = System.Diagnostics.Stopwatch.StartNew();

                    // Phase 1: Stop all active previews
                    _previewManager.StopAll();
                    _log(string.Format("[性能] 终端切换停止 耗时={0}毫秒", stopWatch.ElapsedMilliseconds));

                    var phaseTick = stopWatch.ElapsedMilliseconds;
                    _terminalManager.SwitchTo(terminalIndex);
                    _log(string.Format("[性能] 终端管理器切换 耗时={0}毫秒", stopWatch.ElapsedMilliseconds - phaseTick));
                    _log("[终端切换] 当前终端已切换为：" + _terminalManager.CurrentName + " " + _terminalManager.CurrentBaseUrl);

                    // Phase 2: Restart previews on new terminal
                    phaseTick = stopWatch.ElapsedMilliseconds;
                    _log("[终端切换] 正在" + _terminalManager.CurrentName + "上恢复活动预览");
                    await _previewManager.RestartPreviewsOnTerminalSwitch(_terminalManager.CurrentBaseUrl);
                    _log(string.Format("[性能] 终端切换启动 耗时={0}毫秒", stopWatch.ElapsedMilliseconds - phaseTick));
                    _log(string.Format("[性能] 终端切换总耗时={0}毫秒", stopWatch.ElapsedMilliseconds));
                }
                catch (Exception ex)
                {
                    _log("终端切换失败: " + ex.Message);
                }
                finally
                {
                    _switchingTerminal = false;
                }
            });

            return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + "}";
        }

        private async Task<string> HandleProcessStart(string saveDir)
        {
            var callbackBase = _getCallbackBaseUrl();
            var requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"," +
                $"\"callbacks\":{{" +
                $"\"ocr_document\":\"{callbackBase}\"," +
                $"\"ocr_event_status\":\"{callbackBase}\"," +
                $"\"nfc_card\":\"{callbackBase}\"}}}}";

            _terminalManager.ProcessSaveDir = PathHelper.SafeResolveSaveDir(saveDir);
            _log("[流程] 正在向终端开始流程，url=" + _terminalManager.CurrentBaseUrl + "/process/start，save_dir=" + _terminalManager.ProcessSaveDir);

            var (ok, _) = await _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/process/start", body);
            if (ok)
            {
                _terminalManager.ProcessActive = true;
                _log("[流程] 终端流程已开始，save_dir=" + _terminalManager.ProcessSaveDir);
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

        private async Task<string> HandleCaptureFace(string requestId, string saveDir)
        {
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/face-image/sync-request", body);

            if (!ok) return "{\"error\":true,\"code\":\"capture_failed\"}";

            var savePath = ResultParser.ExtractSavePath(response);
            if (string.IsNullOrEmpty(savePath))
            {
                // Try resource-specific field names (same as Delphi)
                var result = CallbackParser.ParseImageCapture(response, "face_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                {
                    var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/bmp";
                    savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "face");
                }
            }

            _log($"[人脸抓拍] save_path={savePath}");
            return "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(savePath) + "\"}";
        }

        private async Task<string> HandleCaptureFingerprint(string requestId, string saveDir)
        {
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/fingerprint/sync-request", body);

            if (!ok) return "{\"error\":true,\"code\":\"capture_failed\"}";

            var savePath = ResultParser.ExtractSavePath(response);
            if (string.IsNullOrEmpty(savePath))
            {
                // Try resource-specific field names (same as Delphi)
                var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                {
                    var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/jpeg";
                    savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "fingerprint");
                }
            }

            _log($"[指纹抓拍] save_path={savePath}");
            return "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(savePath) + "\"}";
        }

        private async Task<string> HandleCaptureIris(string requestId, string saveDir, string callbackUrl, string terminalBaseUrl)
        {
            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
            _requestCallbacks[requestId] = callbackUrl;

            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            var (ok, _) = await _terminalClient.PostJsonAsync(terminalBaseUrl, "/resources/iris/request", body);
            if (ok) { _log($"Iris capture forwarded: request_id={requestId}"); return "{\"accepted\":true}"; }
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private async Task<string> HandleOcr(string requestId, string saveDir, string callbackUrl, string terminalBaseUrl)
        {
            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
            _requestCallbacks[requestId] = callbackUrl;

            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            var (ok, _) = await _terminalClient.PostJsonAsync(terminalBaseUrl, "/resources/ocr-document/request", body);
            if (ok) { _log($"OCR forwarded: request_id={requestId}"); return "{\"accepted\":true}"; }
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private async Task<string> HandleNfc(string requestId, string saveDir, string callbackUrl, string terminalBaseUrl)
        {
            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
            _requestCallbacks[requestId] = callbackUrl;

            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            var (ok, _) = await _terminalClient.PostJsonAsync(terminalBaseUrl, "/resources/nfc-card/request", body);
            if (ok) { _log($"NFC forwarded: request_id={requestId}"); return "{\"accepted\":true}"; }
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private async Task<string> HandleAuthorize(string bodyUtf8, string requestId, string callbackUrl, string terminalBaseUrl)
        {
            // 2.21 请求协议签订: 转发至终端 /resources/protocol/request
            // DLL 字段 (ZJHM,ZJLB,GJDQDM,XM,XB,CSRQ,KADM) → 终端协议字段 (id_no,doc_type,nationality,name,sex,birthday,port_code)
            // 2.22 协议签订结果推送由 TerminalCallbackHandler 处理并回传 DLL

            // Store DLL callback URL for later forwarding when terminal calls back with 2.22 result
            if (!string.IsNullOrEmpty(callbackUrl) && !string.IsNullOrEmpty(requestId))
            {
                _requestCallbacks[requestId] = callbackUrl;
            }

            // Map third-party fields to terminal protocol 2.21 fields
            var name = JsonHelper.ExtractString(bodyUtf8, "XM");
            var sex = JsonHelper.ExtractString(bodyUtf8, "XB");
            var idNo = JsonHelper.ExtractString(bodyUtf8, "ZJHM");
            var docType = JsonHelper.ExtractString(bodyUtf8, "ZJLB");
            var birthday = JsonHelper.ExtractString(bodyUtf8, "CSRQ");
            var nationality = JsonHelper.ExtractString(bodyUtf8, "GJDQDM");
            var portCode = JsonHelper.ExtractString(bodyUtf8, "KADM");  // KADM → port_code

            var callbackBase = _getCallbackBaseUrl();

            // Build 2.21 request body following protocol document field names
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

            _log("[授权] 转发协议签订请求至终端: request_id=" + requestId
                + ", name=" + name + ", id_no=" + idNo + ", port_code=" + portCode);

            var (ok, _) = await _terminalClient.PostJsonAsync(terminalBaseUrl, "/resources/protocol/request", terminalBody);
            if (ok)
            {
                _log("[授权] 协议签订请求已受理: request_id=" + requestId);
                return "{\"accepted\":true}";
            }
            return "{\"error\":true,\"code\":\"terminal_request_failed\"}";
        }

        private async Task<string> HandlePreviewStart(string bodyUtf8, PreviewResourceType resType, PreviewSessionType sessionType)
        {
            var hwndValue = JsonHelper.ExtractInt(bodyUtf8, "hwnd");
            var hwnd = new IntPtr(hwndValue);
            var callbackUrl = JsonHelper.ExtractString(bodyUtf8, "callback_url");
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                _log("[预览管理] 目标窗口句柄无效，hwnd=" + hwndValue);
                return "{\"error\":true,\"code\":\"invalid_target_hwnd\"}";
            }

            var resourceName = resType switch
            {
                PreviewResourceType.Camera => "face_image",
                PreviewResourceType.Fingerprint => "fingerprint_image",
                PreviewResourceType.Iris => "iris_image",
                _ => "unknown"
            };

            var terminalBaseUrl = _terminalManager.CurrentBaseUrl;
            var ok = await _previewManager.StartPreview(resType, sessionType, hwnd, terminalBaseUrl);

            if (ok)
            {
                // Success callback (same as Delphi TAsyncStartPreviewThread)
                if (!string.IsNullOrEmpty(callbackUrl))
                    await _dllCallback.SendPreviewReady(requestId, resourceName, hwnd, IntPtr.Zero);

                _log($"Preview started: {resType} external -> hwnd={hwnd}");
                return "{\"accepted\":true}";
            }

            // Failure callback (same as Delphi TAsyncStartPreviewThread error path)
            // DLL's ProcessPreviewReadyCallback checks error:true and dispatches PREVIEW_FAILED event
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                var errorPayload = "{" +
                    "\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"," +
                    "\"resource_type\":\"" + JsonHelper.EscapeString(resourceName) + "\"," +
                    "\"render_hwnd\":" + hwndValue + "," +
                    "\"error\":true," +
                    "\"code\":\"preview_failed\"" +
                    "}";
                await _dllCallback.PostCallbackRaw("/preview-ready", errorPayload);
            }

            _log($"Preview failed: {resType} external -> hwnd={hwnd}");
            return "{\"error\":true,\"code\":\"preview_failed\"}";
        }

        private string HandlePreviewStop(PreviewResourceType resType, PreviewSessionType sessionType)
        {
            _previewManager.StopPreview(resType, sessionType);
            return "{\"status\":\"ok\"}";
        }

        private async Task<string> HandlePreviewUrl(PreviewResourceType resType, string terminalBaseUrl)
        {
            var previewUrl = await _previewManager.RequestPreviewUrl(resType, terminalBaseUrl);
            if (!string.IsNullOrEmpty(previewUrl))
                return "{\"status\":\"ok\",\"preview_url\":\"" + JsonHelper.EscapeString(previewUrl) + "\"}";
            return "{\"error\":true,\"code\":\"preview_url_failed\"}";
        }
    }
}
