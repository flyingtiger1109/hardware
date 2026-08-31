using System;
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
                    return await EnqueueCapture(_queueManager.FaceCaptureQueue, routeEpoch, saveDir);

                case "/capture/fingerprint":
                    {
                        var saveDirHk = request.GetString("save_dir_hk");
                        return await EnqueueCapture(_queueManager.FingerprintCaptureQueue, routeEpoch, saveDir, saveDirHk);
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

        private static bool TryGetLatestPlateFrameRoute(string path,
            out PreviewResourceType resourceType, out string plateCode)
        {
            resourceType = default(PreviewResourceType);
            plateCode = null;
            var normalizedPath = (path ?? string.Empty).Split('?')[0];
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
            if (!TryGetLatestPlateFrameRoute(path,
                out var resourceType, out var plateCode))
            {
                return BuildLatestPlateFrameError("not_found", "未知车牌最新帧路由");
            }

            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                return BuildLatestPlateFrameError("invalid_method", "最新车牌帧接口只支持POST");

            if (_capabilities.TryGetRequiredCapability(path, out var required) &&
                !_capabilities.IsSupported(required))
            {
                return DllBinaryResponse.Json(
                    _capabilities.BuildNotSupportedResult(path, required));
            }

            var request = ParsedJsonBody.Parse(bodyUtf8);
            var requestId = request.GetString("request_id");
            if (string.IsNullOrWhiteSpace(requestId))
                return BuildLatestPlateFrameError("invalid_request_id", "request_id为空");

            if (!_latestFrameResponseGate.Wait(0))
                return BuildLatestPlateFrameError("frame_busy", "最新车牌帧响应并发数已达上限");

            var leaseOwned = true;
            try
            {
                if (!_previewManager.TryGetLatestPlateFrame(resourceType, requestId,
                    out var snapshot, out var errorCode, out var errorMessage))
                {
                    return BuildLatestPlateFrameError(errorCode, errorMessage);
                }

                if (snapshot == null || snapshot.Jpeg == null || snapshot.Jpeg.Length == 0)
                    return BuildLatestPlateFrameError("frame_data_invalid", "最新车牌帧数据为空");

                var response = DllBinaryResponse.Binary(snapshot.Jpeg, "image/jpeg",
                    () => _latestFrameResponseGate.Release());
                _log(Logger.FormatModuleMessage(LogModules.Preview, "调试",
                    $"车牌最新帧已准备：资源={FormatPreviewResource(resourceType)}，" +
                    $"request_id={FormatRequestId(requestId)}，尺寸={snapshot.Width}x{snapshot.Height}，" +
                    $"序列号={snapshot.Sequence}，字节数={snapshot.Jpeg.Length}"));
                leaseOwned = false;
                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"车牌最新帧处理异常：资源={FormatPreviewResource(resourceType)}，" +
                    $"request_id={FormatRequestId(requestId)}", ex);
                return BuildLatestPlateFrameError("frame_data_invalid", "获取最新车牌帧异常");
            }
            finally
            {
                if (leaseOwned)
                    _latestFrameResponseGate.Release();
            }
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
            TerminalRouteEpochSnapshot routeEpoch, string saveDir, string saveDirHk = null)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var data = new CaptureTaskData
            {
                Tcs = tcs,
                SaveDir = saveDir,
                SaveDirHk = saveDirHk,
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
                var completed = await Task.WhenAny(tcs.Task,
                    Task.Delay(OperationTimeouts.CaptureProxyWaitMs));
                if (completed == tcs.Task && tcs.Task.IsCompleted)
                    return await tcs.Task;
                Logger.Error($"[队列] {queue.Name} 请求超时");
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
                if (!_queueManager.IrisQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.IrisImage);
                    Logger.Warn("[虹膜抓拍] 虹膜任务队列已满");
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

                Logger.Error($"[虹膜抓拍] 受理请求超时({OperationTimeouts.AsyncProxyWaitMs}ms)");
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
            _log("[授权] 收到DLL授权请求：请求ID=" + JsonHelper.ToLogValue(requestId) +
                "，终端=" + routeEpoch.Route.TerminalIndex +
                "，终端地址=" + JsonHelper.ToLogValue(routeEpoch.Route.BaseUrl) +
                "，回调地址=" + JsonHelper.ToLogValue(callbackUrl) +
                "，证件号码=" + authIdNo +
                "，证件类别=" + authDocType +
                "，国家地区代码=" + authNationality +
                "，姓名=" + authName +
                "，性别=" + authSex +
                "，出生日期=" + authBirthday +
                "，口岸代码=" + authPortCode);

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
                if (!_queueManager.AuthorizeQueue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, ProxyResourceTypes.Protocol);
                    Logger.Warn("[授权] 授权任务队列已满");
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

                Logger.Error($"[授权] 受理请求超时({OperationTimeouts.AuthorizeProxyWaitMs}ms)");
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
                if (!queue.Enqueue(data, routeEpoch.Generation))
                {
                    _requestRegistry.Fail(requestId, resourceType);
                    Logger.Warn($"[队列] {queue.Name} 队列满, 拒绝请求: {path}");
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
                Logger.Error($"[队列] {queue.Name} 请求超时({timeoutMs}ms): {path}");
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

            _log("[终端切换] 下发切换请求: " + _terminalManager.CurrentIndex + " -> " + terminalIndex);

            // 仅在 PreviewManager 停止终端绑定预览且 TerminalManager 提交新路由后返回成功。
            // 预览重启仍由 SwitchCoordinator 在后台执行。
            if (!await _switchCoordinator.SwitchToAsync(terminalIndex).ConfigureAwait(false))
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
                    _log("[流程] 开始流程：终端=" + route.TerminalIndex +
                        "，地址=" + route.BaseUrl + "/process/start，保存目录=" +
                        resolvedSaveDir);

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
                    _log("[流程] 流程已开始：终端=" + route.TerminalIndex +
                        "，request_id=" + requestId + "，保存目录=" + resolvedSaveDir);
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
                _log($"[预览管理][错误] 目标窗口句柄无效：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                     $"HWND={FormatHwnd(hwndValue)}");
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
                try
                {
                    Func<bool> shouldContinue = () =>
                        !routeEpoch.IsCancellationRequested &&
                        !_queueManager.SwitchingTerminal &&
                        _queueManager.IsGenerationValid(routeEpoch.Generation);

                    if (!shouldContinue())
                    {
                        _log($"[预览管理] 外部预览已跳过：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
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
                        _log($"[预览管理] 外部预览已启动：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                             $"HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
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

                        if (!string.IsNullOrEmpty(callbackUrl))
                        {
                            var errPayload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\",\"resource_type\":\"" + resourceName + "\",\"render_hwnd\":" + hwndValue + ",\"error\":true,\"code\":\"preview_failed\"}";
                            await _dllCallback.PostCallbackRaw("/preview-ready", errPayload).ConfigureAwait(false);
                        }
                        _log($"[预览管理][错误] 外部预览启动失败：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                             $"HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    await TryCleanupFailedPreviewAsync(
                        resType, requestId).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        var errPayload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"resource_type\":\"" + resourceName + "\",\"render_hwnd\":" +
                            hwndValue + ",\"error\":true,\"code\":\"preview_exception\"}";
                        await _dllCallback.PostCallbackRaw("/preview-ready", errPayload)
                            .ConfigureAwait(false);
                    }
                    _log($"[预览管理][错误] 外部预览启动异常：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                         $"HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms，错误={ex.Message}");
                }
            }, "preview_start_external");

            if (!taskAccepted)
            {
                _log($"[预览管理][警告] 外部预览任务未受理：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                     $"HWND={FormatHwnd(hwndValue)}");
                return "{\"error\":true,\"code\":\"service_busy\"}";
            }

            _log($"[预览请求] EXE已受理外部预览请求：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                 $"HWND={FormatHwnd(hwndValue)}");

            return "{\"accepted\":true}";
        }

        private string HandlePreviewStop(PreviewResourceType resType, string requestId = null)
        {
            var requestTrace = FormatRequestId(requestId);
            var stopSw = Stopwatch.StartNew();
            _log($"[预览请求][调试] EXE收到外部停止预览请求：资源={FormatPreviewResource(resType)}，request_id={requestTrace}");
            var stopped = _previewManager.StopPreview(resType, PreviewSessionType.External);
            _log($"[预览管理] 外部预览停止完成：资源={FormatPreviewResource(resType)}，request_id={requestTrace}，" +
                 $"结果={(stopped ? "成功" : "未找到")}，耗时={stopSw.ElapsedMilliseconds}ms");
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
                _log($"[预览管理][错误] 车牌预览目标窗口句柄无效：车牌={plateCode.ToUpperInvariant()}，" +
                     $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}");
                return "{\"error\":true,\"code\":\"invalid_target_hwnd\"}";
            }
            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                _log($"[预览管理][警告] 车牌预览地址未配置：车牌={plateCode.ToUpperInvariant()}，" +
                     $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}");
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
                        _log($"[预览管理] 外部车牌{plateCode.ToUpperInvariant()}预览已直接绑定目标HWND：" +
                             $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                        return;
                    }

                    await TryCleanupFailedPreviewAsync(
                        resourceType, requestId).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        var errPayload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"resource_type\":\"plate_image\",\"render_hwnd\":" + hwndValue +
                            ",\"error\":true,\"code\":\"preview_failed\"}";
                        await _dllCallback.PostCallbackRaw("/preview-ready", errPayload).ConfigureAwait(false);
                    }
                    _log($"[预览管理][错误] 外部车牌{plateCode.ToUpperInvariant()}预览启动失败：" +
                         $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    await TryCleanupFailedPreviewAsync(
                        resourceType, requestId).ConfigureAwait(false);
                    _log($"[预览管理][错误] 外部车牌{plateCode.ToUpperInvariant()}预览启动异常：" +
                         $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}，耗时={previewSw.ElapsedMilliseconds}ms，错误={ex.Message}");
                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        var errPayload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                            "\",\"resource_type\":\"plate_image\",\"render_hwnd\":" + hwndValue +
                            ",\"error\":true,\"code\":\"preview_exception\"}";
                        await _dllCallback.PostCallbackRaw("/preview-ready", errPayload).ConfigureAwait(false);
                    }
                }
            }, "preview_start_plate_" + plateCode + "_external");

            if (!taskAccepted)
            {
                _log($"[预览管理][警告] 外部车牌{plateCode.ToUpperInvariant()}预览任务未受理：" +
                     $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}");
                return "{\"error\":true,\"code\":\"service_busy\"}";
            }

            _log($"[预览请求] EXE已受理外部车牌预览请求：车牌={plateCode.ToUpperInvariant()}，" +
                 $"request_id={requestTrace}，HWND={FormatHwnd(hwndValue)}");
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
