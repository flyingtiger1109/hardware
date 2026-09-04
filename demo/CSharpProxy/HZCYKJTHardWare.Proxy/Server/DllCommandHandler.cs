using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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
        private readonly ProcessEndCoordinator _processEndCoordinator;
        private readonly Action<bool> _onProcessStateChanged;
        private readonly DeviceCapabilityManager _capabilities;
        private readonly SemaphoreSlim _latestFrameResponseGate =
            new SemaphoreSlim(4, 4);
        private readonly string _proxyInstanceId = CreateProxyInstanceId();
        private const string TerminalSwitchingResult =
            "{\"error\":true,\"code\":\"terminal_switching\"}";

        private static string FormatRequestId(string requestId)
        {
            return PreviewManager.FormatRequestId(requestId);
        }

        private static string FormatHwnd(long hwndValue)
        {
            return PreviewManager.FormatHwnd(new IntPtr(hwndValue));
        }

        internal static string BuildPreviewStartFailurePayload(string requestId,
            string resourceName, long renderHwnd, string errorCode, bool recovering)
        {
            var effectiveErrorCode = recovering
                ? "preview_recovering"
                : (string.IsNullOrWhiteSpace(errorCode) ? "preview_failed" : errorCode);
            var payload = "{\"request_id\":\"" +
                JsonHelper.EscapeString(requestId) +
                "\",\"resource_type\":\"" +
                JsonHelper.EscapeString(resourceName) +
                "\",\"render_hwnd\":" + renderHwnd +
                ",\"error\":true,\"code\":\"" +
                JsonHelper.EscapeString(effectiveErrorCode) + "\"";
            if (recovering)
                payload += ",\"recovering\":true";
            return payload + "}";
        }

        private static string FormatPreviewResource(PreviewResourceType resourceType)
        {
            switch (resourceType)
            {
                case PreviewResourceType.Camera: return "摄像头";
                case PreviewResourceType.Fingerprint: return "指纹";
                case PreviewResourceType.Iris: return "虹膜";
                case PreviewResourceType.PlateCJ: return "车牌CJ";
                case PreviewResourceType.PlateRJ2: return "车牌RJ2";
                case PreviewResourceType.PlateRJ3: return "车牌RJ3";
                default: return "未知";
            }
        }

        private static string PreviewOperation(PreviewResourceType resourceType,
            bool start)
        {
            switch (resourceType)
            {
                case PreviewResourceType.Camera:
                    return start ? "StartCameraPreview" : "StopCameraPreview";
                case PreviewResourceType.Fingerprint:
                    return start ? "StartFingerprintPreview" : "StopFingerprintPreview";
                case PreviewResourceType.Iris:
                    return start ? "StartIrisPreview" : "StopIrisPreview";
                case PreviewResourceType.PlateCJ:
                    return start ? "StartPlatePreviewCJ" : "StopPlatePreviewCJ";
                case PreviewResourceType.PlateRJ2:
                    return start ? "StartPlatePreviewRJ2" : "StopPlatePreviewRJ2";
                case PreviewResourceType.PlateRJ3:
                    return start ? "StartPlatePreviewRJ3" : "StopPlatePreviewRJ3";
                default:
                    return start ? "StartPreview" : "StopPreview";
            }
        }

        private async Task NotifyPreviewStartOutcomeAsync(
            PreviewResourceType resourceType, string resourceName, long hwndValue,
            string callbackUrl, string requestId, string failureCode,
            string failureDetail, long durationMs)
        {
            var startupState = _previewManager.GetExternalPreviewStartupState(
                resourceType, PreviewSessionType.External, requestId);
            if (startupState == ExternalPreviewStartupState.Running)
            {
                if (!string.IsNullOrEmpty(callbackUrl))
                    await _dllCallback.SendPreviewReady(requestId, resourceName,
                        new IntPtr(hwndValue), IntPtr.Zero).ConfigureAwait(false);

                _log(Logger.FormatModuleMessage(LogModules.Preview, "信息",
                    $"{FormatPreviewResource(resourceType)}预览已在恢复期间就绪：" +
                    $"Operation={PreviewOperation(resourceType, true)} RequestId={FormatRequestId(requestId)} " +
                    $"Result=Success DurationMs={durationMs}，HWND={FormatHwnd(hwndValue)}"));
                return;
            }

            var recovering = startupState == ExternalPreviewStartupState.Recovering;
            var effectiveErrorCode = recovering
                ? "preview_recovering"
                : (string.IsNullOrWhiteSpace(failureCode) ? "preview_failed" : failureCode);
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                await _dllCallback.PostCallbackRaw("/preview-ready",
                    BuildPreviewStartFailurePayload(requestId, resourceName, hwndValue,
                        effectiveErrorCode, recovering)).ConfigureAwait(false);
            }

            var level = recovering ? "警告" : "错误";
            var result = recovering ? "Recovering" : "Failed";
            var description = recovering
                ? $"{FormatPreviewResource(resourceType)}预览启动暂未就绪，已保留Preview Lease并继续自动恢复："
                : $"{FormatPreviewResource(resourceType)}预览启动失败：";
            var detail = !recovering && !string.IsNullOrWhiteSpace(failureDetail)
                ? "，错误=" + JsonHelper.ToLogValue(failureDetail)
                : "";
            _log(Logger.FormatModuleMessage(LogModules.Preview, level,
                description +
                $"Operation={PreviewOperation(resourceType, true)} RequestId={FormatRequestId(requestId)} " +
                $"Result={result} ErrorCode={effectiveErrorCode} DurationMs={durationMs}，" +
                $"HWND={FormatHwnd(hwndValue)}" + detail));
        }

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
            ProcessEndCoordinator processEndCoordinator,
            Action<bool> onProcessStateChanged = null,
            DeviceCapabilityManager capabilities = null)
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
            _processEndCoordinator = processEndCoordinator;
            _onProcessStateChanged = onProcessStateChanged;
            _capabilities = capabilities ?? DeviceCapabilityManager.Instance;
        }

        public async Task<string> HandleAsync(string method, string path, string bodyUtf8)
        {
            // /ping：快速路径，不进入队列
            if (path == "/ping")
                return BuildPingResponse(_proxyInstanceId);

            if (_capabilities.TryGetRequiredCapability(path, out var required) &&
                !_capabilities.IsSupported(required))
                return _capabilities.BuildNotSupportedResult(path, required);

            // 扁平化车牌相机路由：Proxy 不解析 Direction。每条路由持有一个相机或会话，
            // 由第三方调用方选择 CJ 或 RJ2+RJ3。
            if (path == "/preview/plate/cj/start")
                return HandlePlatePreviewStart(ParsedJsonBody.Parse(bodyUtf8),
                    PreviewResourceType.PlateCJ, "cj");
            if (path == "/preview/plate/cj/stop")
                return HandlePreviewStop(PreviewResourceType.PlateCJ,
                    ParsedJsonBody.Parse(bodyUtf8).GetString("request_id"));
            if (path == "/preview/plate/rj2/start")
                return HandlePlatePreviewStart(ParsedJsonBody.Parse(bodyUtf8),
                    PreviewResourceType.PlateRJ2, "rj2");
            if (path == "/preview/plate/rj2/stop")
                return HandlePreviewStop(PreviewResourceType.PlateRJ2,
                    ParsedJsonBody.Parse(bodyUtf8).GetString("request_id"));
            if (path == "/preview/plate/rj3/start")
                return HandlePlatePreviewStart(ParsedJsonBody.Parse(bodyUtf8),
                    PreviewResourceType.PlateRJ3, "rj3");
            if (path == "/preview/plate/rj3/stop")
                return HandlePreviewStop(PreviewResourceType.PlateRJ3,
                    ParsedJsonBody.Parse(bodyUtf8).GetString("request_id"));

            // 终端切换期间快速拒绝请求
            if (_queueManager.SwitchingTerminal)
                return TerminalSwitchingResult;

            if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                return TerminalSwitchingResult;

            // 请求正文仅解析一次，并在各路由处理函数间复用
            var request = ParsedJsonBody.Parse(bodyUtf8);
            var requestId = request.GetString("request_id");
            var saveDir = request.GetString("save_dir");
            var callbackUrl = request.GetString("callback_url");
            if (string.IsNullOrEmpty(saveDir))
                saveDir = _processRegistry.GetCurrentSaveDir(routeEpoch.Route.TerminalIndex);
            if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;

            switch (path)
            {
                // === 终端切换（最高优先级，等待路由提交）===
                case "/terminal/switch":
                    return await HandleSwitch(request).ConfigureAwait(false);

                // === 同步采集（等待结果，传入第三方提供的 saveDir）===
                case "/capture/face":
                    return await EnqueueCapture(_queueManager.FaceCaptureQueue, routeEpoch,
                        saveDir, requestId: requestId);

                case "/capture/fingerprint":
                    {
                        var saveDirHk = request.GetString("save_dir_hk");
                        return await EnqueueCapture(_queueManager.FingerprintCaptureQueue, routeEpoch,
                            saveDir, saveDirHk, requestId);
                    }

                // === 异步操作（终端转发成功后立即返回 "accepted"）===
                case "/ocr":
                    return await EnqueueAsyncResource(_queueManager.OcrQueue, routeEpoch, path,
                        OperationTimeouts.AsyncProxyWaitMs,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.OcrDocument);

                case "/nfc":
                    return await EnqueueAsyncResource(_queueManager.NfcQueue, routeEpoch, path,
                        OperationTimeouts.AsyncProxyWaitMs,
                        requestId, saveDir, callbackUrl, ProxyResourceTypes.NfcCard);

                case "/capture/iris":
                    return await EnqueueIris(routeEpoch, requestId, saveDir, callbackUrl);

                // === 预览（Replace 模式，立即返回 "accepted"）===
                case "/preview/camera/start":
                    return await HandlePreviewStart(request, PreviewResourceType.Camera, routeEpoch);

                case "/preview/fingerprint/start":
                    return await HandlePreviewStart(request, PreviewResourceType.Fingerprint, routeEpoch);

                case "/preview/iris/start":
                    return await HandlePreviewStart(request, PreviewResourceType.Iris, routeEpoch);

                case "/preview/camera/stop":
                    return HandlePreviewStop(PreviewResourceType.Camera, requestId);
                case "/preview/fingerprint/stop":
                    return HandlePreviewStop(PreviewResourceType.Fingerprint, requestId);
                case "/preview/iris/stop":
                    return HandlePreviewStop(PreviewResourceType.Iris, requestId);

                // === 预览 URL 查询（同步执行，不进入队列）===
                case "/preview/camera/url":
                    return await HandlePreviewUrl(PreviewResourceType.Camera, routeEpoch, requestId);
                case "/preview/fingerprint/url":
                    return await HandlePreviewUrl(PreviewResourceType.Fingerprint, routeEpoch, requestId);
                case "/preview/iris/url":
                    return await HandlePreviewUrl(PreviewResourceType.Iris, routeEpoch, requestId);

                // === 流程控制与授权 ===
                case "/process/start":
                    return await HandleProcessStart(requestId, routeEpoch, saveDir);
                case "/process/end":
                    return await HandleProcessEnd(requestId).ConfigureAwait(false);
                case "/authorize":
                    return await EnqueueAuthorize(routeEpoch, request, requestId, callbackUrl);

                default:
                    return "{\"error\":true,\"code\":\"not_found\"}";
            }
        }

        internal static bool IsLatestPlateFramePath(string path)
        {
            return TryGetLatestPlateFrameRoute(path,
                out _, out _);
        }

        /// <summary>
        /// 判断请求是否看起来像最新车牌帧路由但没有通过规范化匹配。
        /// 仅用于诊断，不能把未知 Plate 路由误当成有效路由。
        /// </summary>
        internal static bool IsLatestPlateFrameCandidatePath(string path)
        {
            var normalizedPath = NormalizeLatestPlateFramePath(path);
            return normalizedPath.IndexOf("/preview/plate/",
                       StringComparison.OrdinalIgnoreCase) >= 0 &&
                   normalizedPath.IndexOf("/latest-frame",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 在最新帧入口统一处理 Request-Target 的常见变体。
        /// Native 正常发送的是 origin-form 小写路径；这里额外兼容大小写、查询串、
        /// 首尾空格、绝对 URI 和单个尾斜杠，避免路由分派与业务处理使用不同约定。
        /// </summary>
        internal static string NormalizeLatestPlateFramePath(string path)
        {
            var normalizedPath = (path ?? string.Empty).Trim();
            var queryIndex = normalizedPath.IndexOf('?');
            if (queryIndex >= 0)
                normalizedPath = normalizedPath.Substring(0, queryIndex);

            if (normalizedPath.IndexOf("://", StringComparison.Ordinal) >= 0 &&
                Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri))
            {
                normalizedPath = absoluteUri.AbsolutePath;
            }

            try
            {
                normalizedPath = Uri.UnescapeDataString(normalizedPath);
            }
            catch (UriFormatException)
            {
                // 保留原始值，让后续严格路由匹配自然失败。
            }

            return normalizedPath.Length > 1
                ? normalizedPath.TrimEnd('/')
                : normalizedPath;
        }

        private static bool TryGetLatestPlateFrameRoute(string path,
            out PreviewResourceType resourceType, out string plateCode)
        {
            resourceType = default(PreviewResourceType);
            plateCode = null;
            var normalizedPath = NormalizeLatestPlateFramePath(path);
            switch (normalizedPath.ToLowerInvariant())
            {
                case "/preview/plate/cj/latest-frame":
                    resourceType = PreviewResourceType.PlateCJ;
                    plateCode = "cj";
                    return true;
                case "/preview/plate/rj2/latest-frame":
                    resourceType = PreviewResourceType.PlateRJ2;
                    plateCode = "rj2";
                    return true;
                case "/preview/plate/rj3/latest-frame":
                    resourceType = PreviewResourceType.PlateRJ3;
                    plateCode = "rj3";
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 处理只返回 JPEG 二进制的最新车牌帧请求。
        /// 成功响应持有一个并发租约，调用方必须在 HTTP 响应写完后 Dispose。
        /// </summary>
        internal Task<DllBinaryResponse> HandleLatestPlateFrameAsync(
            string method, string path, string bodyUtf8)
        {
            return Task.FromResult(HandleLatestPlateFrame(method, path, bodyUtf8));
        }

        private DllBinaryResponse HandleLatestPlateFrame(string method,
            string path, string bodyUtf8)
        {
            var captureStopwatch = Stopwatch.StartNew();
            string captureRequestId = null;
            string requestId = null;
            string plateCode = "unknown";
            string source = "unknown";
            long frameAgeMs = -1;
            var retryCount = 0;
            LatestPlateFrameSnapshot snapshot = null;

            if (!TryGetLatestPlateFrameRoute(path,
                out var resourceType, out var routePlateCode))
            {
                return BuildLatestPlateFrameError("not_found", "未知车牌最新帧路由");
            }

            plateCode = routePlateCode.ToUpperInvariant();
            var canonicalPath = "/preview/plate/" + routePlateCode + "/latest-frame";
            var request = ParsedJsonBody.Parse(bodyUtf8);
            requestId = request.GetString("request_id");
            captureRequestId = request.GetString("capture_request_id");
            if (string.IsNullOrWhiteSpace(captureRequestId))
                captureRequestId = CreateCaptureRequestId();

            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Failed", "invalid_method", captureStopwatch.ElapsedMilliseconds,
                    null, source, frameAgeMs);
                return BuildLatestPlateFrameError("invalid_method", "最新车牌帧接口只支持POST");
            }

            if (_capabilities.TryGetRequiredCapability(canonicalPath, out var required) &&
                !_capabilities.IsSupported(required))
            {
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Failed", "not_supported", captureStopwatch.ElapsedMilliseconds,
                    null, source, frameAgeMs);
                return DllBinaryResponse.Json(
                    _capabilities.BuildNotSupportedResult(canonicalPath, required));
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Failed", "invalid_request_id", captureStopwatch.ElapsedMilliseconds,
                    null, source, frameAgeMs);
                return BuildLatestPlateFrameError("invalid_request_id", "request_id为空");
            }

            if (!_latestFrameResponseGate.Wait(0))
            {
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Failed", "frame_busy", captureStopwatch.ElapsedMilliseconds,
                    null, source, frameAgeMs);
                return BuildLatestPlateFrameError("frame_busy", "最新车牌帧响应并发数已达上限");
            }

            var leaseOwned = true;
            try
            {
                if (!_previewManager.TryGetLatestPlateFrame(resourceType, requestId,
                    out snapshot, out var errorCode, out var errorMessage,
                    out source, out frameAgeMs, out retryCount))
                {
                    LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                        "Failed", errorCode, captureStopwatch.ElapsedMilliseconds,
                        snapshot, source, frameAgeMs, retryCount);
                    return BuildLatestPlateFrameError(errorCode, errorMessage);
                }

                if (snapshot == null || snapshot.Jpeg == null || snapshot.Jpeg.Length == 0)
                {
                    LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                        "Failed", "frame_data_invalid", captureStopwatch.ElapsedMilliseconds,
                        snapshot, source, frameAgeMs);
                    return BuildLatestPlateFrameError("frame_data_invalid", "最新车牌帧数据为空");
                }

                var responseHeaders = BuildLatestPlateFrameHeaders(
                    plateCode, captureRequestId, requestId, snapshot, source, frameAgeMs);
                var response = DllBinaryResponse.Binary(snapshot.Jpeg, "image/jpeg",
                    responseHeaders,
                    () => _latestFrameResponseGate.Release());
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Success", "none", captureStopwatch.ElapsedMilliseconds,
                    snapshot, source, frameAgeMs, retryCount);
                leaseOwned = false;
                return response;
            }
            catch (Exception ex)
            {
                LogLatestPlateFrameOperation(plateCode, captureRequestId, requestId,
                    "Failed", "frame_data_invalid", captureStopwatch.ElapsedMilliseconds,
                    snapshot, source, frameAgeMs, retryCount);
                Logger.Debug($"车牌最新帧处理异常明细：资源={FormatPreviewResource(resourceType)}，" +
                    $"request_id={FormatRequestId(requestId)}，错误={JsonHelper.ToLogValue(ex.Message)}");
                return BuildLatestPlateFrameError("frame_data_invalid", "获取最新车牌帧异常");
            }
            finally
            {
                if (leaseOwned)
                    _latestFrameResponseGate.Release();
            }
        }

        private static string CreateCaptureRequestId()
        {
            return "PROXY_CAPTURE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static IDictionary<string, string> BuildLatestPlateFrameHeaders(
            string plateCode, string captureRequestId, string requestId,
            LatestPlateFrameSnapshot snapshot, string source, long frameAgeMs)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-HZCY-Capture-Request-Id"] = captureRequestId ?? string.Empty,
                ["X-HZCY-Preview-Request-Id"] = requestId ?? string.Empty,
                ["X-HZCY-Plate"] = plateCode ?? "unknown",
                ["X-HZCY-Frame-Source"] = source ?? "unknown",
                ["X-HZCY-Frame-Width"] = snapshot == null ? "-1" : snapshot.Width.ToString(),
                ["X-HZCY-Frame-Height"] = snapshot == null ? "-1" : snapshot.Height.ToString(),
                ["X-HZCY-Frame-Age-Ms"] = frameAgeMs.ToString()
            };
        }

        private void LogLatestPlateFrameOperation(string plateCode,
            string captureRequestId, string requestId, string result,
            string errorCode, long durationMs, LatestPlateFrameSnapshot snapshot,
            string source, long frameAgeMs, int retryCount = 0)
        {
            var success = string.Equals(result, "Success",
                StringComparison.OrdinalIgnoreCase);
            var stage = LatestPlateFrameStage(result, errorCode);
            var description = success
                ? "最新车牌帧已返回DLL" : "DLL最新车牌帧请求失败";
            var fields = Logger.FormatContextMessage("GetLatestPlateFrame",
                requestId: captureRequestId,
                result: result,
                errorCode: string.IsNullOrWhiteSpace(errorCode) ? "unknown" : errorCode,
                durationMs: durationMs);
            fields += " CaptureRequestId=" + FormatRequestId(captureRequestId) +
                " PreviewRequestId=" + FormatRequestId(requestId) +
                " Plate=" + JsonHelper.ToLogValue(plateCode ?? "unknown") +
                " Source=" + JsonHelper.ToLogValue(source ?? "unknown") +
                " Bytes=" + (snapshot?.Jpeg?.Length ?? 0) +
                " Width=" + (snapshot == null ? -1 : snapshot.Width) +
                " Height=" + (snapshot == null ? -1 : snapshot.Height) +
                " FrameAgeMs=" + frameAgeMs +
                " Stage=" + stage +
                " RetryCount=" + retryCount;
            fields = description + "：" + fields;
            _log(Logger.FormatModuleMessage(LogModules.PlateCapture,
                success ? "信息" : "错误", fields));
        }

        private static string LatestPlateFrameStage(string result, string errorCode)
        {
            if (string.Equals(result, "Success", StringComparison.OrdinalIgnoreCase))
                return "GetLatestFrame";
            if (string.Equals(errorCode, "frame_stale", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "frame_not_ready", StringComparison.OrdinalIgnoreCase))
                return "OnDemandSnapshot";
            if (string.Equals(errorCode, "frame_busy", StringComparison.OrdinalIgnoreCase))
                return "ConcurrencyGate";
            if (string.Equals(errorCode, "not_supported", StringComparison.OrdinalIgnoreCase))
                return "Capability";
            if (string.Equals(errorCode, "invalid_method", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "invalid_request_id", StringComparison.OrdinalIgnoreCase))
                return "RequestValidation";
            return "GetLatestFrame";
        }

        private static DllBinaryResponse BuildLatestPlateFrameError(string code,
            string message)
        {
            var safeCode = string.IsNullOrWhiteSpace(code) ? "frame_data_invalid" : code;
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "获取最新车牌帧失败" : message;
            return DllBinaryResponse.Json(
                "{\"error\":true,\"code\":\"" + JsonHelper.EscapeString(safeCode) +
                "\",\"message\":\"" + JsonHelper.EscapeString(safeMessage) + "\"}");
        }

        /// <summary>
        /// 将采集任务加入队列，并使用第三方请求中的 saveDir，与 Delphi 逻辑一致。
        /// saveDir 包含文件扩展名时直接作为保存路径。
        /// </summary>
        private async Task<string> EnqueueCapture(WorkerQueue<object> queue,
            TerminalRouteEpochSnapshot routeEpoch, string saveDir, string saveDirHk = null,
            string requestId = null)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new CaptureTaskData
            {
                Tcs = tcs,
                SaveDir = saveDir,
                SaveDirHk = saveDirHk,
                RequestId = requestId,
                RouteEpoch = routeEpoch
            };
            var queueWaitSw = Stopwatch.StartNew();
            using (routeEpoch.CancellationToken.Register(
                () => tcs.TrySetResult(TerminalSwitchingResult)))
            {
                if (!queue.Enqueue(data, routeEpoch.Generation))
                {
                    _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "警告",
                        Logger.FormatContextMessage("Enqueue " + queue.Name,
                            terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                            result: "失败", errorCode: "busy",
                            queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                            routeEpoch: routeEpoch.Route.RouteEpoch)));
                    return "{\"error\":true,\"code\":\"busy\"}";
                }
                var completed = await Task.WhenAny(tcs.Task,
                    Task.Delay(OperationTimeouts.CaptureProxyWaitMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                    return await tcs.Task;
                _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "错误",
                    Logger.FormatContextMessage("Enqueue " + queue.Name,
                        terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                        result: "失败", errorCode: "timeout",
                        queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                        routeEpoch: routeEpoch.Route.RouteEpoch)));
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        internal static string CreateProxyInstanceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string BuildPingResponse(string proxyInstanceId)
        {
            return "{\"status\":\"ok\",\"proxy_instance_id\":\"" +
                JsonHelper.EscapeString(proxyInstanceId ?? "") + "\"}";
        }

        /// <summary>
        /// 将异步虹膜采集任务加入队列并保留 DLL 的 request_id。
        /// 终端将最终 iris_image 结果提交到 Proxy 回调服务。
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
                var queueWaitSw = Stopwatch.StartNew();
                if (!_queueManager.IrisQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                    _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "警告",
                        Logger.FormatContextMessage("Enqueue iris",
                            terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                            requestId: requestId, result: "失败", errorCode: "busy",
                            queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                            routeEpoch: routeEpoch.Route.RouteEpoch)));
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                var completed = await Task.WhenAny(tcs.Task,
                    Task.Delay(OperationTimeouts.AsyncProxyWaitMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, ProxyResourceTypes.IrisImage, result);
                    return result;
                }

                _log(Logger.FormatModuleMessage(LogModules.IrisCapture, "错误",
                    Logger.FormatContextMessage("/capture/iris",
                        terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                        requestId: requestId, result: "失败", errorCode: "timeout",
                        queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                        routeEpoch: routeEpoch.Route.RouteEpoch) +
                    " TimeoutMs=" + OperationTimeouts.AsyncProxyWaitMs));
                _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        private async Task<string> EnqueueAuthorize(TerminalRouteEpochSnapshot routeEpoch,
            ParsedJsonBody request, string requestId, string callbackUrl)
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N").Substring(0, 16);

            var authIdNo = JsonHelper.ToLogValue(request.GetString("ZJHM"));
            var authDocType = JsonHelper.ToLogValue(request.GetString("ZJLB"));
            var authNationality = JsonHelper.ToLogValue(request.GetString("GJDQDM"));
            var authName = JsonHelper.ToLogValue(request.GetString("XM"));
            var authSex = JsonHelper.ToLogValue(request.GetString("XB"));
            var authBirthday = JsonHelper.ToLogValue(request.GetString("CSRQ"));
            var authPortCode = JsonHelper.ToLogValue(request.GetString("KADM"));
            _log(Logger.FormatModuleMessage(LogModules.Authorization, "调试",
                "授权请求：来源=DLL，Operation=Authorize RequestId=" +
                JsonHelper.ToLogValue(requestId) +
                "，TerminalIndex=" + routeEpoch.Route.TerminalIndex +
                "，证件号码=" + authIdNo +
                "，证件类别=" + authDocType +
                "，国家地区代码=" + authNationality +
                "，姓名=" + authName +
                "，性别=" + authSex +
                "，出生日期=" + authBirthday +
                "，口岸代码=" + authPortCode));
            _log(Logger.FormatModuleMessage(LogModules.Authorization, "调试",
                "授权请求通信上下文：RequestId=" + JsonHelper.ToLogValue(requestId) +
                "，终端地址=" + Logger.SanitizeUrlForLog(routeEpoch.Route.BaseUrl) +
                "，回调地址=" + Logger.SanitizeUrlForLog(callbackUrl)));

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new AuthorizeTaskData
            {
                Tcs = tcs,
                BodyUtf8 = request.RawBody,
                RequestId = requestId,
                CallbackUrl = callbackUrl,
                RouteEpoch = routeEpoch
            };

            var resolvedSaveDir = PathHelper.SafeResolveSaveDir(
                string.IsNullOrEmpty(_processRegistry.GetCurrentSaveDir(
                    routeEpoch.Route.TerminalIndex))
                    ? AppConfig.Instance.DefaultSaveDir
                    : _processRegistry.GetCurrentSaveDir(routeEpoch.Route.TerminalIndex));
            var context = _requestRegistry.Register(requestId, ProxyResourceTypes.Protocol,
                resolvedSaveDir, callbackUrl, routeEpoch.Generation,
                terminalIndex: routeEpoch.Route.TerminalIndex,
                originalRequestBodyUtf8: request.RawBody);
            if (context == null)
                return "{\"error\":true,\"code\":\"registry_full\"}";
            context.TryMarkQueued();

            using (routeEpoch.CancellationToken.Register(() =>
            {
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                tcs.TrySetResult(TerminalSwitchingResult);
            }))
            {
                var queueWaitSw = Stopwatch.StartNew();
                if (!_queueManager.AuthorizeQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                    _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "警告",
                        Logger.FormatContextMessage("Enqueue authorize",
                            terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                            requestId: requestId, result: "失败", errorCode: "busy",
                            queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                            routeEpoch: routeEpoch.Route.RouteEpoch)));
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                var completed = await Task.WhenAny(tcs.Task,
                    Task.Delay(OperationTimeouts.AuthorizeProxyWaitMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, ProxyResourceTypes.Protocol, result);
                    return result;
                }

                _log(Logger.FormatModuleMessage(LogModules.Authorization, "错误",
                    Logger.FormatContextMessage("/authorize",
                        terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                        requestId: requestId, result: "失败", errorCode: "timeout",
                        queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                        routeEpoch: routeEpoch.Route.RouteEpoch) +
                    " TimeoutMs=" + OperationTimeouts.AuthorizeProxyWaitMs));
                _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        /// <summary>
        /// 将异步终端资源请求加入队列，并保留 DLL 生成的 request_id。
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
                var queueWaitSw = Stopwatch.StartNew();
                if (!queue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, resourceType);
                    _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "警告",
                        Logger.FormatContextMessage("Enqueue " + path,
                            terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                            requestId: requestId, result: "失败", errorCode: "busy",
                            queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                            routeEpoch: routeEpoch.Route.RouteEpoch) +
                        " Queue=" + JsonHelper.ToLogValue(queue.Name)));
                    return "{\"error\":true,\"code\":\"busy\"}";
                }

                // 等待工作线程完成，并受超时时限约束
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                {
                    var result = await tcs.Task;
                    CleanupRegistryForQueueFailure(requestId, resourceType, result);
                    return result;
                }

                // 等待超时
                _log(Logger.FormatModuleMessage(LogModules.TaskQueue, "错误",
                    Logger.FormatContextMessage("Enqueue " + path,
                        terminalIndex: routeEpoch.Route.TerminalIndex.ToString(),
                        requestId: requestId, result: "失败", errorCode: "timeout",
                        queueWaitMs: queueWaitSw.ElapsedMilliseconds,
                        routeEpoch: routeEpoch.Route.RouteEpoch) +
                    " TimeoutMs=" + timeoutMs + " Queue=" + JsonHelper.ToLogValue(queue.Name)));
                _requestRegistry.Fail(requestId, resourceType, timedOut: true);
                const string timeoutResult = "{\"error\":true,\"code\":\"timeout\"}";
                if (tcs.TrySetResult(timeoutResult))
                    return timeoutResult;
                return await tcs.Task;
            }
        }

        // ====== 终端切换（终端路由提交后响应）======

        private async Task<string> HandleSwitch(ParsedJsonBody request)
        {
            var terminalIndex = request.GetInt("terminal_index");
            if (terminalIndex < 1 || terminalIndex > 2)
                return "{\"error\":true,\"code\":\"invalid_terminal_index\"}";

            if (!_terminalManager.IsTerminalConfigured(terminalIndex))
                return BuildTerminalNotConfiguredResult(terminalIndex);

            if (_terminalManager.IsSameTerminal(terminalIndex))
                return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + ",\"same_terminal\":true}";

            var requestId = request.GetString("request_id");
            if (string.IsNullOrWhiteSpace(requestId))
                requestId = "SWITCH_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            _log(Logger.FormatModuleMessage(LogModules.TerminalSwitch, "信息",
                "开始切换终端：" + _terminalManager.CurrentName + " → " +
                _terminalManager.GetTerminalName(terminalIndex) + "，来源=DLL，" +
                "Operation=SwitchTerminal RequestId=" + FormatRequestId(requestId)));

            // 仅在 PreviewManager 停止终端绑定预览且 TerminalManager 提交新路由后返回成功。
            // 预览重启仍由 SwitchCoordinator 在后台执行。
            if (!await _switchCoordinator.SwitchToAsync(terminalIndex, requestId).ConfigureAwait(false))
                return "{\"error\":true,\"code\":\"terminal_switching\"}";

            return "{\"status\":\"ok\",\"terminal_index\":" + terminalIndex + "}";
        }

        private string BuildTerminalNotConfiguredResult(int terminalIndex)
        {
            var terminalName = _terminalManager.GetTerminalName(terminalIndex);
            if (string.IsNullOrWhiteSpace(terminalName))
                terminalName = "终端" + terminalIndex;

            var message = "未配备" + terminalName + "终端，无法切换";
            var logMessage = "[终端切换] 拒绝第三方切换：当前终端=" +
                _terminalManager.CurrentIndex + "(" + _terminalManager.CurrentName + ")，" +
                "目标终端=" + terminalIndex + "(" + terminalName + ")未配置，" +
                "code=terminal_not_configured";
            if (_log != null)
                _log(logMessage);
            else
                Logger.Warn(logMessage);

            return "{\"error\":true,\"code\":\"terminal_not_configured\",\"terminal_index\":" +
                terminalIndex + ",\"terminal_name\":\"" + JsonHelper.EscapeString(terminalName) +
                "\",\"message\":\"" + JsonHelper.EscapeString(message) + "\"}";
        }

        // ====== 流程控制 ======

        private async Task<string> HandleProcessStart(string requestId,
            TerminalRouteEpochSnapshot routeEpoch, string saveDir)
        {
            if (string.IsNullOrEmpty(requestId))
                requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

            using (var controlLease = _controlGate.TryEnter("start_process"))
            {
                if (controlLease == null)
                    return "{\"error\":true,\"code\":\"busy\"}";

                var route = routeEpoch.Route;
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = _processRegistry.GetCurrentSaveDir(route.TerminalIndex);
                if (string.IsNullOrEmpty(saveDir))
                    saveDir = AppConfig.Instance.DefaultSaveDir;
                var resolvedSaveDir = PathHelper.SafeResolveSaveDir(saveDir);

                var callbackBase = _getCallbackBaseUrl();
                var irisCallback = BuildIrisCallbackUrl(callbackBase);

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
                    Logger.Debug("[流程] 开始流程通信：Operation=StartProcess RequestId=" +
                        JsonHelper.ToLogValue(requestId) + "，TerminalIndex=" +
                        route.TerminalIndex + "，终端地址=" +
                        Logger.SanitizeUrlForLog(route.BaseUrl) + "，保存目录=" +
                        JsonHelper.ToLogValue(resolvedSaveDir));

                    var (ok, _) = await _terminalClient.PostJsonAsync(route.BaseUrl,
                        "/process/start", body, OperationTimeouts.ProcessStartTerminalRequestMs,
                        routeEpoch.CancellationToken)
                        .ConfigureAwait(false);
                    if (!ok || !_processRegistry.Commit(registration))
                        return "{\"error\":true,\"code\":\"terminal_request_failed\"}";

                    committed = true;
                    _terminalManager.ProcessSaveDir = resolvedSaveDir;
                    _terminalManager.ProcessActive = true;
                    _onProcessStateChanged?.Invoke(true);
                    _log("[流程] 流程已开始：Operation=StartProcess RequestId=" +
                        JsonHelper.ToLogValue(requestId) + "，TerminalIndex=" +
                        route.TerminalIndex + "，Result=Success，保存目录=" +
                        JsonHelper.ToLogValue(resolvedSaveDir));
                    return "{\"status\":\"ok\"}";
                }
                finally
                {
                    if (!committed)
                        _processRegistry.RetainUnconfirmed(registration);
                }
            }
        }

        private async Task<string> HandleProcessEnd(string requestId)
        {
            var outcome = await _processEndCoordinator.EndCurrentAsync(requestId)
                .ConfigureAwait(false);
            if (outcome.Success)
                return "{\"status\":\"ok\"}";
            return "{\"error\":true,\"code\":\"" +
                JsonHelper.EscapeString(outcome.Code) + "\"}";
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

        // ====== 启动预览（Replace 模式，立即返回 "accepted"）======

        private async Task<string> HandlePreviewStart(ParsedJsonBody request,
            PreviewResourceType resType, TerminalRouteEpochSnapshot routeEpoch)
        {
            var hwndValue = request.GetInt64("hwnd");
            var hwnd = new IntPtr(hwndValue);
            var callbackUrl = request.GetString("callback_url");
            var requestId = request.GetString("request_id");
            var requestTrace = FormatRequestId(requestId);

            _log($"[预览请求][调试] EXE收到外部预览请求：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                 $"HWND={FormatHwnd(hwndValue)}，回调地址={callbackUrl}");

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                _log(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    $"目标窗口句柄无效：资源={FormatPreviewResource(resType)}，" +
                    $"Operation={PreviewOperation(resType, true)} RequestId={requestTrace} " +
                    $"Result=Failed ErrorCode=invalid_target_hwnd，HWND={FormatHwnd(hwndValue)}"));
                return "{\"error\":true,\"code\":\"invalid_target_hwnd\"}";
            }

            // 在线程池启动预览，避免阻塞 HTTP；随后发送回调
            var terminalBaseUrl = routeEpoch.Route.BaseUrl;
            string resourceName;
            switch (resType)
            {
                case PreviewResourceType.Camera: resourceName = "face_image"; break;
                case PreviewResourceType.Fingerprint: resourceName = "fingerprint_image"; break;
                case PreviewResourceType.Iris: resourceName = "iris_image"; break;
                default: resourceName = "unknown"; break;
            }

            // 异步执行预览启动，不阻塞 HTTP 响应
            var previewSw = Stopwatch.StartNew();
            var taskAccepted = _taskTracker.TryRun(async () =>
            {
                Func<bool> shouldContinue = () =>
                    !routeEpoch.IsCancellationRequested &&
                    !_queueManager.SwitchingTerminal &&
                    _queueManager.IsGenerationValid(routeEpoch.Generation);

                try
                {
                    if (!shouldContinue())
                    {
                        _log($"[预览管理][调试] 外部预览已跳过：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                             $"原因=终端正在切换或请求已过期，HWND={FormatHwnd(hwndValue)}");
                        return;
                    }

                    var ok = await _previewManager.StartPreview(resType, PreviewSessionType.External, hwnd, terminalBaseUrl,
                        shouldContinue: shouldContinue, directRenderTarget: true, requestId: requestId);
                    if (ok)
                    {
                        if (!shouldContinue())
                        {
                            await TryCleanupFailedPreviewAsync(
                                resType, requestId).ConfigureAwait(false);
                        _log($"[预览管理][调试] 外部预览启动后发现终端已切换，等待切换流程接管：资源={FormatPreviewResource(resType)}，" +
                                 $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                            return;
                        }

                        await HideMainFormToTrayAsync().ConfigureAwait(false);

                        if (!shouldContinue())
                        {
                            await TryCleanupFailedPreviewAsync(
                                resType, requestId).ConfigureAwait(false);
                            _log($"[预览管理][调试] 外部预览最小化窗口后发现终端已切换，等待切换流程接管：资源={FormatPreviewResource(resType)}，" +
                                 $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                            return;
                        }

                        if (!string.IsNullOrEmpty(callbackUrl))
                            await _dllCallback.SendPreviewReady(requestId, resourceName, hwnd, IntPtr.Zero).ConfigureAwait(false);
                        _log(Logger.FormatModuleMessage(LogModules.Preview, "信息",
                            $"{FormatPreviewResource(resType)}画面已开始显示：" +
                            $"Operation={PreviewOperation(resType, true)} RequestId={requestTrace} " +
                            $"Result=Success DurationMs={previewSw.ElapsedMilliseconds}，" +
                            $"HWND={FormatHwnd(hwndValue)}"));
                    }
                    else
                    {
                        await TryCleanupFailedPreviewAsync(
                            resType, requestId).ConfigureAwait(false);
                        if (!shouldContinue())
                        {
                            _log($"[预览管理][调试] 外部预览启动已过期，跳过失败回调：资源={FormatPreviewResource(resType)}，" +
                                 $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                            return;
                        }

                        await NotifyPreviewStartOutcomeAsync(
                            resType, resourceName, hwndValue, callbackUrl, requestId,
                            "preview_failed", null,
                            previewSw.ElapsedMilliseconds).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    await TryCleanupFailedPreviewAsync(
                        resType, requestId).ConfigureAwait(false);
                    if (!shouldContinue())
                    {
                        _log($"[预览管理][调试] 外部预览启动异常已过期，跳过失败回调：资源={FormatPreviewResource(resType)}，" +
                             $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                        return;
                    }
                    await NotifyPreviewStartOutcomeAsync(
                        resType, resourceName, hwndValue, callbackUrl, requestId,
                        "preview_exception", ex.Message,
                        previewSw.ElapsedMilliseconds).ConfigureAwait(false);
                }
            }, "preview_start_external");

            if (!taskAccepted)
            {
                _log($"[预览管理][警告] 外部预览任务未受理：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                     $"HWND={FormatHwnd(hwndValue)}");
                return "{\"error\":true,\"code\":\"service_busy\"}";
            }

            _log(Logger.FormatModuleMessage(LogModules.Preview, "调试",
                $"EXE已受理外部预览请求：资源={FormatPreviewResource(resType)}，" +
                $"Operation={PreviewOperation(resType, true)} RequestId={requestTrace}，" +
                $"HWND={FormatHwnd(hwndValue)}"));

            return "{\"accepted\":true}";
        }

        private string HandlePreviewStop(PreviewResourceType resType, string requestId = null)
        {
            var requestTrace = FormatRequestId(requestId);
            var stopSw = Stopwatch.StartNew();
            _log($"[预览请求][调试] EXE收到外部停止预览请求：资源={FormatPreviewResource(resType)}，request_id={requestTrace}");
            var stopped = _previewManager.StopPreview(resType, PreviewSessionType.External);
            _log(Logger.FormatModuleMessage(LogModules.Preview, "信息",
                $"{FormatPreviewResource(resType)}{(stopped ? "预览已停止" : "当前没有运行中的预览")}：" +
                $"Operation={PreviewOperation(resType, false)} RequestId={requestTrace} " +
                $"Result={(stopped ? "Success" : "Ignored")} DurationMs={stopSw.ElapsedMilliseconds}"));
            return "{\"status\":\"ok\"}";
        }

        private string HandlePlatePreviewStart(ParsedJsonBody request,
            PreviewResourceType resourceType, string plateCode)
        {
            var hwndValue = request.GetInt64("hwnd");
            var hwnd = new IntPtr(hwndValue);
            var callbackUrl = request.GetString("callback_url");
            var requestId = request.GetString("request_id");
            var requestTrace = FormatRequestId(requestId);
            var previewUrl = AppConfig.Instance.GetPlatePreviewUrl(plateCode);

            _log($"[预览请求][调试] EXE收到外部车牌预览请求：资源={FormatPreviewResource(resourceType)}，车牌={plateCode.ToUpperInvariant()}，" +
                 $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，回调地址={callbackUrl}");

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                _log(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    $"车牌{plateCode.ToUpperInvariant()}预览目标窗口句柄无效：" +
                    $"Operation={PreviewOperation(resourceType, true)} RequestId={requestTrace} " +
                    $"Result=Failed ErrorCode=invalid_target_hwnd，HWND={FormatHwnd(hwndValue)}"));
                return "{\"error\":true,\"code\":\"invalid_target_hwnd\"}";
            }
            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                _log(Logger.FormatModuleMessage(LogModules.Preview, "警告",
                    $"车牌{plateCode.ToUpperInvariant()}预览地址未配置：" +
                    $"Operation={PreviewOperation(resourceType, true)} RequestId={requestTrace} " +
                    $"Result=Failed ErrorCode=plate_preview_not_configured，HWND={FormatHwnd(hwndValue)}"));
                return "{\"error\":true,\"code\":\"plate_preview_not_configured\"}";
            }

            var previewSw = Stopwatch.StartNew();
            var taskAccepted = _taskTracker.TryRun(async () =>
            {
                try
                {
                    Func<bool> shouldContinue = () => IsWindow(hwnd);
                    var ok = await _previewManager.StartPreview(resourceType,
                        PreviewSessionType.External, hwnd, "", shouldContinue: shouldContinue,
                        explicitPreviewUrl: previewUrl, terminalBound: false,
                        directRenderTarget: true, requestId: requestId).ConfigureAwait(false);

                    if (ok && shouldContinue())
                    {
                        if (!string.IsNullOrEmpty(callbackUrl))
                            await _dllCallback.SendPreviewReady(requestId, "plate_image", hwnd,
                                IntPtr.Zero).ConfigureAwait(false);
                        _log(Logger.FormatModuleMessage(LogModules.Preview, "信息",
                            $"车牌{plateCode.ToUpperInvariant()}画面已开始显示：" +
                            $"Operation={PreviewOperation(resourceType, true)} RequestId={requestTrace} " +
                            $"Result=Success DurationMs={previewSw.ElapsedMilliseconds}，" +
                            $"HWND={FormatHwnd(hwndValue)}"));
                        return;
                    }

                    await TryCleanupFailedPreviewAsync(
                        resourceType, requestId).ConfigureAwait(false);

                    await NotifyPreviewStartOutcomeAsync(
                        resourceType, "plate_image", hwndValue, callbackUrl, requestId,
                        "preview_failed", null,
                        previewSw.ElapsedMilliseconds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await TryCleanupFailedPreviewAsync(
                        resourceType, requestId).ConfigureAwait(false);
                    await NotifyPreviewStartOutcomeAsync(
                        resourceType, "plate_image", hwndValue, callbackUrl, requestId,
                        "preview_exception", ex.Message,
                        previewSw.ElapsedMilliseconds).ConfigureAwait(false);
                }
            }, "preview_start_plate_" + plateCode + "_external");

            if (!taskAccepted)
            {
                _log(Logger.FormatModuleMessage(LogModules.Preview, "警告",
                    $"车牌{plateCode.ToUpperInvariant()}预览任务未受理：" +
                    $"Operation={PreviewOperation(resourceType, true)} RequestId={requestTrace} " +
                    $"Result=Failed ErrorCode=service_busy，HWND={FormatHwnd(hwndValue)}"));
                return "{\"error\":true,\"code\":\"service_busy\"}";
            }

            _log(Logger.FormatModuleMessage(LogModules.Preview, "调试",
                $"EXE已受理外部车牌预览请求：车牌={plateCode.ToUpperInvariant()}，" +
                $"Operation={PreviewOperation(resourceType, true)} RequestId={requestTrace}，" +
                $"HWND={FormatHwnd(hwndValue)}"));
            return "{\"accepted\":true}";
        }

        private async Task TryCleanupFailedPreviewAsync(PreviewResourceType resourceType,
            string requestId)
        {
            try
            {
                await _previewManager.CleanupFailedPreviewAsync(
                    resourceType, PreviewSessionType.External, requestId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    "预览失败清理异常：资源=" + FormatPreviewResource(resourceType) +
                    "，request_id=" + FormatRequestId(requestId)), ex);
            }
        }

        private async Task<string> HandlePreviewUrl(PreviewResourceType resType,
            TerminalRouteEpochSnapshot routeEpoch, string requestId)
        {
            if (routeEpoch.IsCancellationRequested)
                return TerminalSwitchingResult;
            var terminalBaseUrl = routeEpoch.Route.BaseUrl;
            var requestTrace = FormatRequestId(requestId);
            _log($"[预览请求][调试] EXE收到预览地址请求：资源={FormatPreviewResource(resType)}，request_id={requestTrace}");
            var previewUrl = await _previewManager.RequestPreviewUrl(resType, terminalBaseUrl,
                requestId: requestId);
            if (routeEpoch.IsCancellationRequested)
                return TerminalSwitchingResult;
            if (!string.IsNullOrEmpty(previewUrl))
            {
                _log($"[预览管理][调试] 预览地址请求完成：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，结果=成功");
                return "{\"status\":\"ok\",\"preview_url\":\"" + JsonHelper.EscapeString(previewUrl) + "\"}";
            }
            _log($"[预览管理][警告] 预览地址请求失败：资源={FormatPreviewResource(resType)}，request_id={requestTrace}");
            return "{\"error\":true,\"code\":\"preview_url_failed\"}";
        }

        /// <summary>
        /// 通知第三方 UI 预览就绪前，将主窗口隐藏到托盘。
        /// 等待该 UI 操作完成，确保外部预览回调触发时窗口处于预期状态。
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
    /// 传递给采集队列工作线程的数据，包含第三方提供的 saveDir。
    /// </summary>
    public class CaptureTaskData : IQueueResultSink
    {
        public TaskCompletionSource<string> Tcs { get; set; }
        public string SaveDir { get; set; }
        public string SaveDirHk { get; set; }
        public string RequestId { get; set; }
        public TerminalRouteEpochSnapshot RouteEpoch { get; set; }
        public bool IsQueueResultCompleted => Tcs == null || Tcs.Task.IsCompleted;

        public void TrySetQueueResult(string result)
        {
            Tcs?.TrySetResult(result);
        }
    }

    /// <summary>
    /// 向终端提交单次异步虹膜采集所需的数据。
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
    /// 提交 OCR 或 NFC 请求所需的数据，不重新生成 request_id。
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
    /// 提交单次异步授权请求所需的数据。
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
