using System;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Scheduler
{
    /// <summary>
    /// Synchronous execution methods that run on WorkerQueue dedicated threads.
    /// Extracted from ProxyServer — all methods are designed for the "executing
    /// thread per queue" model and should NOT be called from ThreadPool or UI.
    ///
    /// Async terminal calls are synchronously bridged only on dedicated worker
    /// threads, never on the UI thread.
    /// </summary>
    public sealed class WorkerExecutionEngine
    {
        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly Action<string> _log;
        private readonly Func<string> _getCallbackBaseUrl;
        private readonly Func<string> _getIrisCallbackUrl;
        private const string TerminalSwitchingResult =
            "{\"error\":true,\"code\":\"terminal_switching\"}";

        // Synchronous adapters invoked only by dedicated capture workers.
        public Func<string, TerminalRouteEpochSnapshot, (bool ok, string path)> CaptureFaceFunc { get; set; }
        public Func<string, TerminalRouteEpochSnapshot, (bool ok, string path)> CaptureFingerprintFunc { get; set; }

        internal WorkerExecutionEngine(
            TerminalManager terminalManager,
            TerminalClient terminalClient,
            RequestRegistry requestRegistry,
            TerminalProcessRegistry processRegistry,
            Action<string> log,
            Func<string> getCallbackBaseUrl,
            Func<string> getIrisCallbackUrl)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _requestRegistry = requestRegistry;
            _processRegistry = processRegistry;
            _log = log;
            _getCallbackBaseUrl = getCallbackBaseUrl;
            _getIrisCallbackUrl = getIrisCallbackUrl;
        }

        // ====== Sync Captures ======

        internal void ExecuteCaptureFace(QueueTask<object> task)
        {
            var data = task.Data as CaptureTaskData;
            var tcs = data?.Tcs;
            try
            {
                var routeEpoch = data?.RouteEpoch;
                if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                {
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }
                var saveDir = data?.SaveDir;
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = _processRegistry.GetActiveSaveDir(routeEpoch.Route.TerminalIndex);
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

                var captureFunc = CaptureFaceFunc;
                var (ok, path) = captureFunc != null
                    ? captureFunc(saveDir, routeEpoch)
                    : (false, "");

                tcs?.TrySetResult(routeEpoch.IsCancellationRequested
                    ? TerminalSwitchingResult
                    : ok ? "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(path) + "\"}"
                    : "{\"error\":true,\"code\":\"capture_failed\"}");
            }
            catch (Exception ex)
            {
                Logger.Error("[人脸抓拍] 队列执行异常", ex);
                tcs?.TrySetResult("{\"error\":true,\"code\":\"capture_failed\"}");
            }
        }

        internal void ExecuteCaptureFingerprint(QueueTask<object> task)
        {
            var data = task.Data as CaptureTaskData;
            var tcs = data?.Tcs;
            try
            {
                var routeEpoch = data?.RouteEpoch;
                if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                {
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }
                var saveDir = data?.SaveDir;
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = _processRegistry.GetActiveSaveDir(routeEpoch.Route.TerminalIndex);
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

                var captureFunc = CaptureFingerprintFunc;
                var (ok, path) = captureFunc != null
                    ? captureFunc(saveDir, routeEpoch)
                    : (false, "");

                tcs?.TrySetResult(routeEpoch.IsCancellationRequested
                    ? TerminalSwitchingResult
                    : ok ? "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(path) + "\"}"
                    : "{\"error\":true,\"code\":\"capture_failed\"}");
            }
            catch (Exception ex)
            {
                Logger.Error("[指纹抓拍] 队列执行异常", ex);
                tcs?.TrySetResult("{\"error\":true,\"code\":\"capture_failed\"}");
            }
        }

        // ====== Async Resources ======

        internal void ExecuteOcrInternal(QueueTask<object> task)
        {
            ExecuteAsyncResourceInternal(task, "/resources/ocr-document/request", "OCR");
        }

        internal void ExecuteNfcInternal(QueueTask<object> task)
        {
            ExecuteAsyncResourceInternal(task, "/resources/nfc-card/request", "NFC");
        }

        internal void ExecuteAsyncResourceInternal(QueueTask<object> task,
            string terminalPath, string operation)
        {
            var data = task.Data as AsyncResourceTaskData;
            var tcs = data?.Tcs;
            if (data == null || string.IsNullOrEmpty(data.RequestId))
            {
                WorkerQueue<object>.TryCompleteTask(task, "invalid_request_id");
                return;
            }

            try
            {
                var routeEpoch = data.RouteEpoch;
                if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                {
                    _requestRegistry.Fail(data.RequestId, data.ResourceType);
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }
                if (!_requestRegistry.TryMarkSubmitting(data.RequestId, data.ResourceType))
                {
                    tcs?.TrySetResult("{\"error\":true,\"code\":\"request_expired\"}");
                    return;
                }

                var callbackBase = _getCallbackBaseUrl();
                var body = "{\"request_id\":\"" + JsonHelper.EscapeString(data.RequestId) +
                    "\",\"callback_url\":\"" + JsonHelper.EscapeString(callbackBase) + "\"}";
                var terminalResult = _terminalClient.PostJsonAsync(
                        routeEpoch.Route.BaseUrl, terminalPath, body, 5000,
                        routeEpoch.CancellationToken)
                    .GetAwaiter().GetResult();
                if (routeEpoch.IsCancellationRequested)
                {
                    _requestRegistry.Fail(data.RequestId, data.ResourceType);
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }
                if (IsAcceptedResponse(terminalResult.ok, terminalResult.response, data.RequestId))
                {
                    if (!_requestRegistry.TryMarkAccepted(data.RequestId, data.ResourceType))
                    {
                        tcs?.TrySetResult("{\"error\":true,\"code\":\"request_expired\"}");
                        return;
                    }
                    _log($"{operation} 已转发至终端: request_id={data.RequestId}");
                    tcs?.TrySetResult("{\"accepted\":true,\"request_id\":\"" +
                        JsonHelper.EscapeString(data.RequestId) + "\"}");
                }
                else
                {
                    _requestRegistry.Fail(data.RequestId, data.ResourceType);
                    tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
                }
            }
            catch (Exception ex)
            {
                _requestRegistry.Fail(data.RequestId, data.ResourceType);
                Logger.Error($"[{operation}] 提交终端异常", ex);
                tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
            }
        }

        // ====== Iris ======

        internal void ExecuteIrisInternal(IrisTaskData data)
        {
            var tcs = data?.Tcs;
            if (data == null || string.IsNullOrEmpty(data.RequestId))
            {
                tcs?.TrySetResult("{\"error\":true,\"code\":\"invalid_request_id\"}");
                return;
            }

            try
            {
                var routeEpoch = data.RouteEpoch;
                if (routeEpoch == null || routeEpoch.IsCancellationRequested)
                {
                    _requestRegistry.Fail(data.RequestId, ProxyResourceTypes.IrisImage);
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }
                if (!_requestRegistry.TryMarkSubmitting(data.RequestId,
                    ProxyResourceTypes.IrisImage))
                {
                    tcs?.TrySetResult("{\"error\":true,\"code\":\"request_expired\"}");
                    return;
                }

                var callbackBase = _getIrisCallbackUrl();
                var requestId = data.RequestId;

                var body = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                    "\",\"callback_url\":\"" + JsonHelper.EscapeString(callbackBase) + "\"}";
                var terminalResult = _terminalClient.PostJsonAsync(
                        routeEpoch.Route.BaseUrl,
                        "/resources/iris/request",
                        body,
                        5000,
                        routeEpoch.CancellationToken)
                    .GetAwaiter().GetResult();

                if (routeEpoch.IsCancellationRequested)
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                    tcs?.TrySetResult(TerminalSwitchingResult);
                    return;
                }

                var responseRequestId = JsonHelper.ExtractString(terminalResult.response, "request_id");
                var status = JsonHelper.ExtractString(terminalResult.response, "status");
                var accepted = terminalResult.ok &&
                    string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(responseRequestId) || responseRequestId == requestId);

                if (accepted)
                {
                    if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.IrisImage))
                    {
                        tcs?.TrySetResult("{\"error\":true,\"code\":\"request_expired\"}");
                        return;
                    }
                    _log($"虹膜抓拍已转发至终端: request_id={requestId}");
                    tcs?.TrySetResult("{\"accepted\":true,\"request_id\":\"" +
                        JsonHelper.EscapeString(requestId) + "\"}");
                    return;
                }

                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);

                var errorCode = ResultParser.ExtractErrorCode(terminalResult.response);
                var message = ResultParser.ExtractErrorMessage(terminalResult.response);
                if (string.IsNullOrEmpty(errorCode))
                {
                    errorCode = !string.IsNullOrEmpty(responseRequestId) && responseRequestId != requestId
                        ? "request_id_mismatch"
                        : "terminal_request_failed";
                }

                Logger.Error($"[虹膜抓拍] 终端拒绝或受理响应无效: request_id={requestId}, code={errorCode}, status={status}");
                tcs?.TrySetResult("{\"error\":true,\"code\":\"" +
                    JsonHelper.EscapeString(errorCode) + "\",\"message\":\"" +
                    JsonHelper.EscapeString(message) + "\"}");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(data.RequestId))
                    _requestRegistry.Fail(data.RequestId, ProxyResourceTypes.IrisImage);
                Logger.Error("[虹膜抓拍] 提交终端异常", ex);
                tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
            }
        }

        // ====== Authorize (moved from DllCommandHandler) ======

        internal void ExecuteAuthorizeInternal(QueueTask<object> task)
        {
            var data = task?.Data as AuthorizeTaskData;
            if (data == null)
            {
                WorkerQueue<object>.TryCompleteTask(task, "invalid_request");
                return;
            }

            try
            {
                if (data.RouteEpoch == null || data.RouteEpoch.IsCancellationRequested)
                {
                    _requestRegistry.Fail(data.RequestId, ProxyResourceTypes.Protocol);
                    data.TrySetQueueResult(TerminalSwitchingResult);
                    return;
                }
                var result = HandleAuthorizeDirect(
                        data.BodyUtf8, data.RequestId, data.CallbackUrl,
                        data.RouteEpoch)
                    .GetAwaiter().GetResult();
                data.TrySetQueueResult(result);
            }
            catch (Exception ex)
            {
                _requestRegistry.Fail(data.RequestId, ProxyResourceTypes.Protocol);
                Logger.Error("[授权] 队列任务执行异常", ex);
                data.TrySetQueueResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
            }
        }

        private async Task<string> HandleAuthorizeDirect(string bodyUtf8, string requestId,
            string callbackUrl, TerminalRouteEpochSnapshot routeEpoch)
        {
            if (routeEpoch.IsCancellationRequested)
                return TerminalSwitchingResult;
            if (!_requestRegistry.TryMarkSubmitting(requestId, ProxyResourceTypes.Protocol))
                return "{\"error\":true,\"code\":\"request_expired\"}";

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

            _log("[授权] 转发至终端");

            var (ok, response) = await _terminalClient.PostJsonAsync(routeEpoch.Route.BaseUrl,
                "/resources/protocol/request", terminalBody, 5000,
                routeEpoch.CancellationToken).ConfigureAwait(false);
            if (routeEpoch.IsCancellationRequested)
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                return TerminalSwitchingResult;
            }
            if (ok)
            {
                if (!_requestRegistry.TryMarkAccepted(requestId, ProxyResourceTypes.Protocol))
                    return "{\"error\":true,\"code\":\"request_expired\"}";
                _log("[授权] 已受理");
                return "{\"accepted\":true}";
            }

            var code = ResultParser.ExtractErrorCode(response);
            var message = ResultParser.ExtractErrorMessage(response);
            var detail = ResultParser.FormatErrorDetail(response, "终端授权请求失败");
            _log("[授权] 下发失败: " + detail);

            if (string.IsNullOrEmpty(code))
                code = "terminal_request_failed";
            _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
            return "{\"error\":true,\"code\":\"" + JsonHelper.EscapeString(code) +
                "\",\"message\":\"" + JsonHelper.EscapeString(message) + "\"}";
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
