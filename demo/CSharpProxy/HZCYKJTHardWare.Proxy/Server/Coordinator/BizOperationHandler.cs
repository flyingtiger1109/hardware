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
            var callbackBase = _getCallbackBaseUrl();
            var irisCallback = _getIrisCallbackUrl();
            var requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"," +
                $"\"callbacks\":{{" +
                $"\"ocr_document\":\"{callbackBase}\"," +
                $"\"ocr_event_status\":\"{callbackBase}\"," +
                $"\"nfc_card\":\"{callbackBase}\"," +
                $"\"iris_image\":\"{irisCallback}\"}}}}";

            _terminalManager.ProcessSaveDir = PathHelper.SafeResolveSaveDir(saveDir);
            if (!RegisterProcessResources(requestId, _terminalManager.ProcessSaveDir,
                _queueManager.TerminalGeneration))
                return "Failed";

            _log("[流程] 正在向终端开始流程，url=" + _terminalManager.CurrentBaseUrl + "/process/start，save_dir=" + _terminalManager.ProcessSaveDir);

            var (ok, _) = await _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl,
                "/process/start", body, 5000).ConfigureAwait(false);
            if (ok)
            {
                if (!MarkProcessResourcesAccepted(requestId))
                    return "Failed";
                _terminalManager.ProcessActive = true;
                _onProcessStateChanged?.Invoke(true);
                _log("[流程] 终端流程已开始，save_dir=" + _terminalManager.ProcessSaveDir);
                return "OK";
            }
            FailProcessResources(requestId);
            return "Failed";
        }

        internal string EndProcess()
        {
            _terminalManager.ProcessActive = false;
            _onProcessStateChanged?.Invoke(false);
            _terminalManager.ProcessSaveDir = "";
            _requestRegistry.CancelAll();
            _log("[流程] 流程已结束");
            return "OK";
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
            var requestId = "FACE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/face-image/sync-request", body, 2500)
                .ConfigureAwait(false);
            if (!ok) return (false, "");

            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                var result = CallbackParser.ParseImageCapture(response, "face_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = ResultParser.ExtractSavePath(response);
                if (string.IsNullOrEmpty(savePath))
                {
                    var result = CallbackParser.ParseImageCapture(response, "face_image");
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
            var requestId = "FP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/fingerprint/sync-request", body, 2500)
                .ConfigureAwait(false);
            if (!ok) return (false, "");

            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = ResultParser.ExtractSavePath(response);
                if (string.IsNullOrEmpty(savePath))
                {
                    var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                    if (!string.IsNullOrEmpty(result.ImageBase64))
                    {
                        var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/jpeg";
                        savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "fingerprint");
                    }
                }
            }
            if (!string.IsNullOrEmpty(savePath))
                _log("[指纹抓拍] 图片保存成功");
            else
                _log("[指纹抓拍] 抓拍失败：未获取有效图片或终端请求失败");
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        // ====== Async Resources ======

        internal async Task<string> RequestOCRAsync(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.OcrDocument, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr"))
                return "";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/ocr-document/request", body, 5000)
                .ConfigureAwait(false);
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
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.NfcCard, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card"))
                return "";
            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/nfc-card/request", body, 5000)
                .ConfigureAwait(false);
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
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getIrisCallbackUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.IrisImage, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/iris"))
                return "";

            var (ok, response) = await _terminalClient.PostJsonAsync(
                    _terminalManager.CurrentBaseUrl, "/resources/iris/request", body, 5000)
                .ConfigureAwait(false);
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
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var callbackBase = _getCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"," +
                $"\"name\":\"{JsonHelper.EscapeString(name)}\",\"sex\":\"{JsonHelper.EscapeString(sex)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"birthday\":\"{JsonHelper.EscapeString(birthday)}\",\"nationality\":\"{JsonHelper.EscapeString(nationality)}\"}}";

            if (!RegisterDirectRequest(requestId, ProxyResourceTypes.Protocol,
                _terminalManager.ProcessSaveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/authorize"))
                return new AuthorizeRequestResult { Ok = false, RequestId = requestId, Message = "registry full" };

            var (ok, response) = await _terminalClient.PostJsonAsync(
                _terminalManager.CurrentBaseUrl, "/resources/protocol/request", body, 5000)
                .ConfigureAwait(false);
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

        internal async Task<bool> StartLocalPreviewAsync(string resourceType, Control panel)
        {
            PreviewResourceType resType;
            switch (resourceType)
            {
                case "camera": resType = PreviewResourceType.Camera; break;
                case "fingerprint": resType = PreviewResourceType.Fingerprint; break;
                case "iris": resType = PreviewResourceType.Iris; break;
                default: throw new ArgumentException($"Unknown resource type: {resourceType}");
            }

            return await _previewManager.StartPreview(resType, PreviewSessionType.Local,
                IntPtr.Zero, _terminalManager.CurrentBaseUrl, panel);
        }

        internal void StopLocalPreview(string resourceType)
        {
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

        private bool RegisterProcessResources(string requestId, string saveDir, int generation)
        {
            return RegisterProcessResource(requestId, ProxyResourceTypes.OcrDocument, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr", generation) &&
                RegisterProcessResource(requestId, ProxyResourceTypes.NfcCard, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card", generation) &&
                RegisterProcessResource(requestId, ProxyResourceTypes.IrisImage, saveDir,
                AppConfig.Instance.GetDllCallbackBaseUrl() + "/iris", generation);
        }

        private bool RegisterProcessResource(string requestId, string resourceType,
            string saveDir, string callbackUrl, int generation)
        {
            var context = _requestRegistry.Register(requestId, resourceType, saveDir,
                callbackUrl, generation, processFlow: true);
            if (context == null) return false;
            context.TryMarkSubmitting();
            return true;
        }

        private bool MarkProcessResourcesAccepted(string requestId)
        {
            return _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.OcrDocument) &
                _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.NfcCard) &
                _requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.IrisImage);
        }

        private void FailProcessResources(string requestId)
        {
            _requestRegistry.Fail(requestId, ProxyResourceTypes.OcrDocument);
            _requestRegistry.Fail(requestId, ProxyResourceTypes.NfcCard);
            _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
        }

        private bool RegisterDirectRequest(string requestId, string resourceType,
            string saveDir, string callbackUrl)
        {
            if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
            var context = _requestRegistry.Register(requestId, resourceType,
                PathHelper.SafeResolveSaveDir(saveDir), callbackUrl,
                _queueManager.TerminalGeneration);
            if (context == null) return false;
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
