using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Coordinator
{
    /// <summary>
    /// All business operations — fully async.
    /// Extracted from ProxyServer. Zero GetAwaiter().GetResult() calls.
    ///
    /// This is the public API surface that ProxyServer delegates to.
    /// </summary>
    public sealed class BizOperationHandler
    {
        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly ControlOperationGate _controlGate;
        private readonly QueueManager _queueManager;
        private readonly PreviewManager _previewManager;
        private readonly Action<string> _log;
        private readonly Func<string> _getCallbackBaseUrl;
        private readonly Func<string> _getIrisCallbackUrl;
        private readonly Action<bool> _onProcessStateChanged;
        private readonly SwitchCoordinator _switchCoordinator;
        private readonly DllCallbackSender _dllCallback;

        public class AuthorizeRequestResult
        {
            public bool Ok { get; set; }
            public string RequestId { get; set; }
            public string Message { get; set; }
        }

        internal BizOperationHandler(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            RequestRegistry requestRegistry,
            TerminalProcessRegistry processRegistry,
            ControlOperationGate controlGate,
            QueueManager queueManager,
            PreviewManager previewManager,
            Action<string> log,
            Func<string> getCallbackBaseUrl,
            Func<string> getIrisCallbackUrl,
            Action<bool> onProcessStateChanged,
            SwitchCoordinator switchCoordinator,
            DllCallbackSender dllCallback)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _requestRegistry = requestRegistry;
            _processRegistry = processRegistry;
            _controlGate = controlGate;
            _queueManager = queueManager;
            _previewManager = previewManager;
            _log = log;
            _getCallbackBaseUrl = getCallbackBaseUrl;
            _getIrisCallbackUrl = getIrisCallbackUrl;
            _onProcessStateChanged = onProcessStateChanged;
            _switchCoordinator = switchCoordinator;
            _dllCallback = dllCallback;
        }

        // ====== Process Control ======

        internal async Task<string> StartProcessAsync(string saveDir)
        {
            var requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using (var controlLease = _controlGate.TryEnter("start_process"))
            {
                if (controlLease == null)
                    return "Busy";
                if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                    return "Busy";

                var route = routeEpoch.Route;
                var callbackBase = _getCallbackBaseUrl();
                var irisCallback = _getIrisCallbackUrl();
                var resolvedSaveDir = PathHelper.SafeResolveSaveDir(saveDir);
                var body = $"{{\"request_id\":\"{requestId}\"," +
                    $"\"callbacks\":{{" +
                    $"\"ocr_document\":\"{callbackBase}\"," +
                    $"\"ocr_event_status\":\"{callbackBase}\"," +
                    $"\"nfc_card\":\"{callbackBase}\"," +
                    $"\"iris_image\":\"{irisCallback}\"}}}}";

                var registration = _processRegistry.Prepare(route.TerminalIndex,
                    route.BaseUrl, requestId, resolvedSaveDir,
                    routeEpoch.Generation);
                if (registration == null)
                    return "Busy";

                var committed = false;
                try
                {
                    _log("[流程] 正在向终端开始流程，url=" + route.BaseUrl +
                        "/process/start，save_dir=" + resolvedSaveDir);

                    var (ok, _) = await _terminalClient.PostJsonAsync(route.BaseUrl,
                        "/process/start", body, OperationTimeouts.ProcessStartTerminalRequestMs,
                        routeEpoch.CancellationToken)
                        .ConfigureAwait(false);
                    if (!ok || !_processRegistry.Commit(registration))
                        return "Failed";

                    committed = true;
                    _terminalManager.ProcessSaveDir = resolvedSaveDir;
                    _terminalManager.ProcessActive = true;
                    _onProcessStateChanged?.Invoke(true);
                    _log("[流程] 终端流程已开始，terminal=" + route.TerminalIndex +
                        ", request_id=" + requestId + ", save_dir=" + resolvedSaveDir);
                    return "OK";
                }
                finally
                {
                    if (!committed)
                        _processRegistry.Rollback(registration);
                }
            }
        }

        internal string EndProcess()
        {
            using (var controlLease = _controlGate.TryEnter("end_process"))
            {
                if (controlLease == null)
                    return "Busy";

                _processRegistry.ClearAll();
                _terminalManager.ProcessActive = false;
                _onProcessStateChanged?.Invoke(false);
                _terminalManager.ProcessSaveDir = "";
                _requestRegistry.CancelAll();
                _log("[流程] 流程已结束");
                return "OK";
            }
        }

        // ====== Terminal Switch ======

        internal async Task<string> SwitchTerminalAsync(int index)
        {
            if (_terminalManager.IsSameTerminal(index))
                return $"已在目标终端，无需切换";

            _log("[终端切换] 正在切换到终端" + _terminalManager.CurrentIndex + " -> 终端" + index);

            var ok = await _switchCoordinator.SwitchToAsync(index).ConfigureAwait(false);
            return ok ? $"已切换到终端 {index}" : "切换失败";
        }

        // ====== Sync Captures ======

        internal async Task<(bool ok, string path)> CaptureFaceAsync(string saveDir)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return (false, "");
            return await CaptureFaceAsync(saveDir, routeEpoch).ConfigureAwait(false);
        }

        internal async Task<(bool ok, string path)> CaptureFaceAsync(string saveDir,
            TerminalRouteEpochSnapshot routeEpoch)
        {
            if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                return (false, "");
            var requestId = "FACE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                routeEpoch.Route.BaseUrl, "/resources/face-image/sync-request", body,
                OperationTimeouts.FaceTerminalRequestMs,
                routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            if (!ok || routeEpoch.IsCancellationRequested) return (false, "");

            var result = CallbackParser.ParseImageCapture(response, "face_image");
            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = result.SavePath;
                if (string.IsNullOrEmpty(savePath))
                {
                    if (!string.IsNullOrEmpty(result.ImageBase64))
                    {
                        var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/bmp";
                        savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "face");
                    }
                }
            }
            if (!string.IsNullOrEmpty(savePath))
                _log("[人脸抓拍] 图片保存成功");
            else
                _log("[人脸抓拍] 抓拍失败：未获取有效图片或终端请求失败");
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        internal async Task<(bool ok, string path)> CaptureFingerprintAsync(string saveDir)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return (false, "");
            return await CaptureFingerprintAsync(saveDir, null, routeEpoch).ConfigureAwait(false);
        }

        internal async Task<(bool ok, string path)> CaptureFingerprintAsync(string saveDir, string saveDirHk,
            TerminalRouteEpochSnapshot routeEpoch)
        {
            if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                return (false, "");
            var requestId = "FP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                routeEpoch.Route.BaseUrl, "/resources/fingerprint/sync-request", body,
                OperationTimeouts.FingerprintTerminalRequestMs,
                routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            if (!ok || routeEpoch.IsCancellationRequested) return (false, "");

            var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = result.SavePath;
                if (string.IsNullOrEmpty(savePath))
                {
                    if (!string.IsNullOrEmpty(result.ImageBase64))
                    {
                        var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/jpeg";
                        savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "fingerprint");
                    }
                }
            }
            // 无畸变图保存（独立于主图，失败不影响主流程）
            string undistortedPath = "";
            if (!string.IsNullOrEmpty(saveDirHk))
                undistortedPath = SaveUndistortedFingerprintImage(
                    result.UndistortedImageBase64, saveDirHk, requestId);

            if (!string.IsNullOrEmpty(savePath))
            {
                var message = "[指纹抓拍] 图片保存成功";
                if (!string.IsNullOrEmpty(undistortedPath))
                    message += "，无畸变图保存成功: " + undistortedPath;
                _log(message);
            }
            else
            {
                _log("[指纹抓拍] 抓拍失败：未获取有效图片或终端请求失败");
            }

            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        private string SaveUndistortedFingerprintImage(string undistortedB64,
            string saveDirHk, string requestId)
        {
            try
            {
                if (string.IsNullOrEmpty(undistortedB64))
                {
                    _log("[无畸变] 图片保存跳过：终端响应中无 data.undistorted_image_base64 字段");
                    return "";
                }
                // 有扩展名 → 精确路径，直接覆盖；无扩展名 → 目录，自动命名
                var path = System.IO.Path.HasExtension(saveDirHk)
                    ? FileSaver.SaveRawGrayscaleAsBmpToFile(undistortedB64,
                        PathHelper.ResolveExactSaveFile(saveDirHk), 352, 544)
                    : FileSaver.SaveRawGrayscaleAsBmp(undistortedB64, saveDirHk, requestId, 352, 544);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            catch (Exception ex)
            {
                Logger.Debug("[无畸变] 图片保存异常: " + ex.Message);
            }

            return "";
        }

        // ====== Async Resources ======

        internal async Task<string> RequestOCRAsync(string saveDir)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return "";
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.OcrDocument, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr", routeEpoch))
                return "";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                routeEpoch.Route.BaseUrl, "/resources/ocr-document/request", body,
                OperationTimeouts.AsyncTerminalRequestMs,
                routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.OcrDocument);
                return "";
            }
            if (!IsAcceptedResponse(ok, response, requestId))
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.OcrDocument);
                return "";
            }
            if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.OcrDocument))
                return "";
            return requestId;
        }

        internal async Task<string> RequestNfcAsync(string saveDir)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return "";
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.NfcCard, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card", routeEpoch))
                return "";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                routeEpoch.Route.BaseUrl, "/resources/nfc-card/request", body,
                OperationTimeouts.AsyncTerminalRequestMs,
                routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.NfcCard);
                return "";
            }
            if (!IsAcceptedResponse(ok, response, requestId))
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.NfcCard);
                return "";
            }
            if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.NfcCard))
                return "";
            return requestId;
        }

        internal async Task<string> CaptureIrisAsync(string saveDir)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return "";
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getIrisCallbackUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.IrisImage, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/iris", routeEpoch))
                return "";

            var (ok, response) = await _terminalClient.PostJsonAsync(
                    routeEpoch.Route.BaseUrl, "/resources/iris/request", body,
                    OperationTimeouts.AsyncTerminalRequestMs,
                    routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                return "";
            }
            var status = JsonHelper.ExtractString(response, "status");
            if (!ok || !string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase))
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                _log("[虹膜抓拍] 终端未受理逐笔异步请求");
                return "";
            }
            if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.IrisImage))
                return "";
            return requestId;
        }

        internal async Task<AuthorizeRequestResult> RequestAuthorizeAsync(
            string idNo, string docType, string nationality,
            string name, string sex, string birthday)
        {
            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return new AuthorizeRequestResult
                {
                    Ok = false,
                    RequestId = "",
                    Message = "terminal switching"
                };
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"," +
                $"\"name\":\"{JsonHelper.EscapeString(name)}\",\"sex\":\"{JsonHelper.EscapeString(sex)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"birthday\":\"{JsonHelper.EscapeString(birthday)}\",\"nationality\":\"{JsonHelper.EscapeString(nationality)}\"}}";
            var originalAuthBody = $"{{\"request_id\":\"{requestId}\"," +
                $"\"ZJHM\":\"{JsonHelper.EscapeString(idNo)}\",\"ZJLB\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"GJDQDM\":\"{JsonHelper.EscapeString(nationality)}\",\"XM\":\"{JsonHelper.EscapeString(name)}\"," +
                $"\"XB\":\"{JsonHelper.EscapeString(sex)}\",\"CSRQ\":\"{JsonHelper.EscapeString(birthday)}\"," +
                "\"KADM\":\"\"}}";

            _log("[授权] EXE本机授权请求：请求ID=" + JsonHelper.ToLogValue(requestId) +
                "，终端=" + routeEpoch.Route.TerminalIndex +
                "，终端地址=" + JsonHelper.ToLogValue(routeEpoch.Route.BaseUrl) +
                "，回调地址=" + JsonHelper.ToLogValue(callbackBase) +
                "，证件号码=" + JsonHelper.ToLogValue(idNo) +
                "，证件类别=" + JsonHelper.ToLogValue(docType) +
                "，国家地区代码=" + JsonHelper.ToLogValue(nationality) +
                "，姓名=" + JsonHelper.ToLogValue(name) +
                "，性别=" + JsonHelper.ToLogValue(sex) +
                "，出生日期=" + JsonHelper.ToLogValue(birthday));

            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.Protocol,
                _processRegistry.GetActiveSaveDir(routeEpoch.Route.TerminalIndex),
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/authorize", routeEpoch,
                originalAuthBody))
                return new AuthorizeRequestResult { Ok = false, RequestId = requestId, Message = "registry full" };

            var (ok, response) = await _terminalClient.PostJsonAsync(
                routeEpoch.Route.BaseUrl, "/resources/protocol/request", body,
                OperationTimeouts.AuthorizeTerminalRequestMs,
                routeEpoch.CancellationToken)
                .ConfigureAwait(false);
            _log("[授权] 终端受理响应：请求ID=" + JsonHelper.ToLogValue(requestId) +
                "，HTTP提交=" + (ok ? "是" : "否") +
                "，响应请求ID=" + JsonHelper.ToLogValue(JsonHelper.ExtractString(response, "request_id")) +
                "，状态=" + JsonHelper.ToLogValue(JsonHelper.ExtractString(response, "status")) +
                "，错误码=" + JsonHelper.ToLogValue(ResultParser.ExtractErrorCode(response)) +
                "，消息=" + JsonHelper.ToLogValue(ResultParser.ExtractErrorMessage(response)));
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                return new AuthorizeRequestResult
                {
                    Ok = false,
                    RequestId = requestId,
                    Message = "terminal switching"
                };
            }
            if (ok)
            {
                if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.Protocol))
                    return new AuthorizeRequestResult { Ok = false, RequestId = requestId, Message = "request expired" };
                _log("[授权] 已受理");
                return new AuthorizeRequestResult { Ok = true, RequestId = requestId, Message = "" };
            }

            var detail = ResultParser.FormatErrorDetail(response, "终端授权请求失败");
            _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
            _log("[授权] 下发失败: " + detail);
            return new AuthorizeRequestResult { Ok = false, RequestId = requestId, Message = detail };
        }

        // ====== Previews ======

        private static bool TryResolvePlatePreview(string resourceType,
            out PreviewResourceType previewType, out string plateCode)
        {
            switch (resourceType)
            {
                case "plate_cj": previewType = PreviewResourceType.PlateCJ; plateCode = "cj"; return true;
                case "plate_rj2": previewType = PreviewResourceType.PlateRJ2; plateCode = "rj2"; return true;
                case "plate_rj3": previewType = PreviewResourceType.PlateRJ3; plateCode = "rj3"; return true;
                default: previewType = default(PreviewResourceType); plateCode = ""; return false;
            }
        }

        internal async Task<bool> StartLocalPreviewAsync(string resourceType, Control panel)
        {
            PreviewResourceType platePreviewType;
            string plateCode;
            if (TryResolvePlatePreview(resourceType, out platePreviewType, out plateCode))
            {
                var previewUrl = AppConfig.Instance.GetPlatePreviewUrl(plateCode);
                if (string.IsNullOrWhiteSpace(previewUrl))
                {
                    _log("[车牌预览] preview.plate 未启用或配置不完整");
                    return false;
                }

                return await _previewManager.StartPreview(platePreviewType,
                    PreviewSessionType.Local, IntPtr.Zero, "", panel,
                    explicitPreviewUrl: previewUrl, terminalBound: false,
                    directRenderTarget: false).ConfigureAwait(false);
            }

            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return false;
            PreviewResourceType resType;
            switch (resourceType)
            {
                case "camera": resType = PreviewResourceType.Camera; break;
                case "fingerprint": resType = PreviewResourceType.Fingerprint; break;
                case "iris": resType = PreviewResourceType.Iris; break;
                default: throw new ArgumentException($"Unknown resource type: {resourceType}");
            }

            return await _previewManager.StartPreview(resType, PreviewSessionType.Local,
                IntPtr.Zero, routeEpoch.Route.BaseUrl, panel,
                shouldContinue: () => !routeEpoch.IsCancellationRequested);
        }

        internal void StopLocalPreview(string resourceType)
        {
            PreviewResourceType platePreviewType;
            string plateCode;
            if (TryResolvePlatePreview(resourceType, out platePreviewType, out plateCode))
            {
                _previewManager.StopPreview(platePreviewType, PreviewSessionType.Local);
                return;
            }

            PreviewResourceType resType;
            switch (resourceType)
            {
                case "camera": resType = PreviewResourceType.Camera; break;
                case "fingerprint": resType = PreviewResourceType.Fingerprint; break;
                case "iris": resType = PreviewResourceType.Iris; break;
                default: throw new ArgumentException($"Unknown resource type: {resourceType}");
            }

            _previewManager.StopPreview(resType, PreviewSessionType.Local);
        }

        // ====== Private Helpers ======

        private bool RegisterDirectRequest(string requestId, string resourceType,
            string saveDir, string callbackUrl, TerminalRouteEpochSnapshot routeEpoch,
            string originalRequestBodyUtf8 = "")
        {
            if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                return false;
            var terminalIndex = routeEpoch.Route.TerminalIndex;
            if (string.IsNullOrEmpty(saveDir))
                saveDir = _processRegistry.GetActiveSaveDir(terminalIndex);
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
            var context = _requestRegistry.Register(requestId, resourceType,
                PathHelper.SafeResolveSaveDir(saveDir), callbackUrl,
                routeEpoch.Generation, terminalIndex: terminalIndex,
                originalRequestBodyUtf8: originalRequestBodyUtf8);
            if (context == null) return false;
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, resourceType);
                return false;
            }
            context.TryMarkSubmitting();
            return true;
        }

        private static bool IsAcceptedResponse(bool ok, string response, string requestId)
        {
            if (!ok) return false;
            var status = JsonHelper.ExtractString(response, "status");
            var responseRequestId = JsonHelper.ExtractString(response, "request_id");
            return (string.IsNullOrEmpty(status) ||
                    string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(responseRequestId) || responseRequestId == requestId);
        }
    }
}
