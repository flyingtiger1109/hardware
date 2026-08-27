using System;
using System.Net;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Coordinator
{
    internal sealed class ProcessEndOutcome
    {
        internal ProcessEndOutcome(bool success, string code,
            string requestId, int terminalIndex)
        {
            Success = success;
            Code = code ?? "";
            RequestId = requestId ?? "";
            TerminalIndex = terminalIndex;
        }

        internal bool Success { get; }
        internal string Code { get; }
        internal string RequestId { get; }
        internal int TerminalIndex { get; }
    }

    /// <summary>
    /// 将 /process/end 同步转发到选定终端。EndProcess 控制终端硬件是否继续发出新回调，
    /// 不取消 Proxy 请求上下文，也不在本地阻断已传输的回调。
    /// </summary>
    internal sealed class ProcessEndCoordinator
    {
        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly ControlOperationGate _controlGate;
        private readonly SwitchCoordinator _switchCoordinator;
        private readonly Action<string> _log;
        private readonly Action<bool> _onProcessStateChanged;

        internal ProcessEndCoordinator(TerminalManager terminalManager,
            TerminalClient terminalClient, TerminalProcessRegistry processRegistry,
            ControlOperationGate controlGate, SwitchCoordinator switchCoordinator,
            Action<string> log, Action<bool> onProcessStateChanged)
        {
            _terminalManager = terminalManager;
            _terminalClient = terminalClient;
            _processRegistry = processRegistry;
            _controlGate = controlGate;
            _switchCoordinator = switchCoordinator;
            _log = log;
            _onProcessStateChanged = onProcessStateChanged;
        }

        internal async Task<ProcessEndOutcome> EndCurrentAsync(string requestedRequestId)
        {
            using (var controlLease = _controlGate.TryEnter("end_process"))
            {
                if (controlLease == null)
                    return new ProcessEndOutcome(false, "busy", "", 0);

                // 获得控制门后再捕获路由；完整的结束握手退出前，终端切换不能提交。
                if (!_switchCoordinator.TryCaptureRoute(out var routeEpoch))
                    return new ProcessEndOutcome(false, "terminal_switching", "", 0);

                var route = routeEpoch.Route;
                var requestId = string.IsNullOrEmpty(requestedRequestId)
                    ? "FLOW_END_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")
                    : requestedRequestId;
                var body = "{\"request_id\":\"" +
                    JsonHelper.EscapeString(requestId) + "\"}";

                _log("[流程] 正在通知终端停止推送：终端=" +
                    route.TerminalIndex + "，request_id=" + requestId);

                var (httpAccepted, response) = await _terminalClient.PostJsonAsync(
                    route.BaseUrl, "/process/end", body,
                    OperationTimeouts.ProcessEndTerminalRequestMs,
                    routeEpoch.CancellationToken,
                    expectedStatusCode: (int)HttpStatusCode.Accepted)
                    .ConfigureAwait(false);

                var responseStatus = JsonHelper.ExtractString(response, "status");
                var responseRequestId = JsonHelper.ExtractString(response, "request_id");
                var responseAccepted = httpAccepted &&
                    string.Equals(responseStatus, "accepted",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(responseRequestId) &&
                    string.Equals(responseRequestId, requestId,
                        StringComparison.Ordinal);

                if (responseAccepted)
                {
                    // 此处仅更新当前、默认及 UI 元数据。历史 request_id 绑定继续用于路由已经发出的回调。
                    _processRegistry.RecordEndAcknowledged(route.TerminalIndex);
                    UpdateLocalIndicator(false);
                    _log("[流程] 终端已确认停止推送：终端=" +
                        route.TerminalIndex + "，request_id=" + requestId);
                    return new ProcessEndOutcome(true, "", requestId,
                        route.TerminalIndex);
                }

                var errorCode = JsonHelper.ExtractString(response, "error_code");
                if (string.IsNullOrEmpty(errorCode))
                    errorCode = JsonHelper.ExtractString(response, "code");
                if (string.IsNullOrEmpty(errorCode))
                    errorCode = httpAccepted
                        ? "invalid_terminal_response"
                        : "terminal_request_failed";

                _log("[流程] 终端停止推送未确认，可直接重试：终端=" +
                    route.TerminalIndex + "，request_id=" + requestId +
                    "，错误码=" + errorCode);
                return new ProcessEndOutcome(false, errorCode, requestId,
                    route.TerminalIndex);
            }
        }

        private void UpdateLocalIndicator(bool active)
        {
            _terminalManager.ProcessActive = active;
            if (!active)
                _terminalManager.ProcessSaveDir = "";
            try
            {
                _onProcessStateChanged?.Invoke(active);
            }
            catch (Exception ex)
            {
                Logger.Error("[流程] 通知本地显示状态失败", ex);
            }
        }
    }
}
