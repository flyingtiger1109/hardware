using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public class TerminalHealthChecker : IDisposable
    {
        private const int PollIntervalMs = 5 * 60 * 1000; // 5 分钟
        private const int InitialDelayMs = 1000;
        private const int RetryBaseDelayMs = 5000;
        private const int RetryMaxDelayMs = 60000;
        private const int MaxRetrySchedules = 5;
        private static readonly TimeSpan FailureSummaryWindow = TimeSpan.FromMinutes(1);
        internal static readonly string[] RequiredDeviceIds =
        {
            "ocr", "nfc", "fingerprint", "iris", "face"
        };

        private readonly TerminalClient _terminalClient;
        private readonly TerminalManager _terminalManager;
        private readonly Action<string> _log;
        private readonly Action<HealthStatus> _onStatusChanged;
        private readonly object _lifecycleLock = new object();
        private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();
        private System.Threading.Timer _timer;
        private Task _activePollTask = Task.CompletedTask;
        private int _running;
        private int _refreshPending;
        private int _retryAttempt;
        private HealthObservationState _lastHealthState;
        private DateTime _healthFailureFirstUtc;
        private DateTime _healthFailureWindowStartUtc;
        private int _healthFailureTotalCount;
        private int _healthFailureWindowCount;
        private int _healthTerminalIndex = -1;
        private volatile bool _disposed;

        private enum HealthObservationState
        {
            Unknown,
            Normal,
            Abnormal
        }

        public TerminalHealthChecker(TerminalClient client, TerminalManager terminalManager,
            Action<string> log, Action<HealthStatus> onStatusChanged)
        {
            _terminalClient = client;
            _terminalManager = terminalManager;
            _log = log;
            _onStatusChanged = onStatusChanged;
        }

        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TerminalHealthChecker));
                if (_timer != null)
                    return;

                _timer = new System.Threading.Timer(PollCallback, null,
                    InitialDelayMs, Timeout.Infinite);
            }
            _log(Logger.FormatModuleMessage(LogModules.HealthCheck, "调试",
                "健康检查已启动：正常轮询间隔=5分钟，异常退避=5/10/20/40/60秒"));
        }

        public void RequestCheck()
        {
            RequestCheck(resetRetryAttempt: true);
        }

        public void RequestCheck(bool resetRetryAttempt)
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                    return;

                Interlocked.Exchange(ref _refreshPending, 1);
                if (resetRetryAttempt)
                    Interlocked.Exchange(ref _retryAttempt, 0);
                _timer?.Change(0, Timeout.Infinite);
            }
        }

        private void PollCallback(object state)
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                    return;
                if (Interlocked.Exchange(ref _running, 1) == 1)
                {
                    Interlocked.Exchange(ref _refreshPending, 1);
                    return;
                }

                Interlocked.Exchange(ref _refreshPending, 0);
                _activePollTask = PollAsync(_stopCts.Token);
            }
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            TerminalRouteSnapshot route = null;
            var nextDelayMs = PollIntervalMs;
            try
            {
                route = _terminalManager.CurrentRoute;
                if (string.IsNullOrEmpty(route.BaseUrl))
                {
                    var failedStatus = CreateFailedStatus("终端地址为空");
                    nextDelayMs = ResolveNextDelayAndUpdateRetry(failedStatus);
                    LogHealthState(failedStatus);
                    return;
                }

                var (ok, response) = await _terminalClient.GetJsonAsync(
                    route.BaseUrl, "/resources/devices/status", 5000,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                HealthStatus status;

                if (!ok || string.IsNullOrEmpty(response))
                {
                    status = CreateFailedStatus("终端连接失败或超时");
                    nextDelayMs = ResolveNextDelayAndUpdateRetry(status);
                }
                else
                {
                    status = ParseResponse(response, DateTime.Now);
                    nextDelayMs = ResolveNextDelayAndUpdateRetry(status);
                }

                if (_terminalManager.CurrentRoute.RouteEpoch != route.RouteEpoch)
                {
                    RequestCheck();
                    return;
                }

                NotifyStatus(status);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 服务停止时的预期取消，不记录为运行异常。
            }
            catch (Exception ex)
            {
                // 状态聚合器会输出一次 WARN；这里仅保留 DEBUG 技术明细，避免同一故障双重告警。
                Logger.Debug("[健康检测][调试] 轮询异常，交由健康状态聚合：错误=" +
                    (ex.Message ?? "未知异常"));
                var failedStatus = CreateFailedStatus("健康检测执行失败");
                nextDelayMs = ResolveNextDelayAndUpdateRetry(failedStatus);
                if (!cancellationToken.IsCancellationRequested && (route == null ||
                    _terminalManager.CurrentRoute.RouteEpoch == route.RouteEpoch))
                    NotifyStatus(failedStatus);
                else if (!cancellationToken.IsCancellationRequested)
                    RequestCheck();
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
                ScheduleNext(nextDelayMs, cancellationToken);
            }
        }

        private void ScheduleNext(int nextDelayMs, CancellationToken cancellationToken)
        {
            lock (_lifecycleLock)
            {
                if (_disposed || cancellationToken.IsCancellationRequested || _timer == null)
                    return;

                if (Interlocked.Exchange(ref _refreshPending, 0) == 1)
                    _timer.Change(0, Timeout.Infinite);
                else if (nextDelayMs == Timeout.Infinite)
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                else
                    _timer.Change(nextDelayMs, Timeout.Infinite);
            }
        }

        internal static int GetNextDelayMs(HealthStatus status)
            => GetNextDelayMs(status, 0);

        internal static int GetNextDelayMs(HealthStatus status, int retryAttempt)
        {
            if (IsHealthyStatus(status))
                return PollIntervalMs;

            if (retryAttempt < 0)
                retryAttempt = 0;

            if (retryAttempt >= MaxRetrySchedules)
                return PollIntervalMs;

            var delay = RetryBaseDelayMs;
            for (var i = 0; i < retryAttempt; i++)
            {
                delay *= 2;
                if (delay >= RetryMaxDelayMs)
                    return RetryMaxDelayMs;
            }

            return Math.Min(delay, RetryMaxDelayMs);
        }

        private int ResolveNextDelayAndUpdateRetry(HealthStatus status)
        {
            var retryAttempt = Volatile.Read(ref _retryAttempt);
            var delay = GetNextDelayMs(status, retryAttempt);

            if (IsHealthyStatus(status))
            {
                Interlocked.Exchange(ref _retryAttempt, 0);
                return delay;
            }

            if (retryAttempt >= MaxRetrySchedules)
            {
                Interlocked.Exchange(ref _retryAttempt, MaxRetrySchedules);
                _log(Logger.FormatModuleMessage(LogModules.HealthCheck, "调试",
                    "快速复查已完成，后续每5分钟慢速探测一次"));
                return delay;
            }

            Interlocked.Exchange(ref _retryAttempt, retryAttempt + 1);
            _log(Logger.FormatModuleMessage(LogModules.HealthCheck, "调试",
                $"将在{delay / 1000}秒后自动复查"));
            return delay;
        }

        private static bool IsHealthyStatus(HealthStatus status)
        {
            return status != null &&
                   string.IsNullOrEmpty(status.ErrorMessage) &&
                   status.IsHealthy;
        }

        internal static HealthStatus ParseResponse(string json, DateTime timestamp)
        {
            var result = new HealthStatus
            {
                Timestamp = timestamp,
                Devices = new List<DeviceHealth>()
            };

            try
            {
                var obj = JObject.Parse(json);
                result.RequestId = obj.Value<string>("request_id") ?? "";
                result.ResponseStatus = obj.Value<string>("status") ?? "";

                if (!string.Equals(result.ResponseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = string.IsNullOrEmpty(result.ResponseStatus)
                        ? "终端响应缺少 status"
                        : "终端返回状态异常: " + result.ResponseStatus;
                    return result;
                }

                var data = obj["data"] as JArray;
                if (data == null || data.Count == 0)
                {
                    result.ErrorMessage = data == null
                        ? "终端响应缺少 data 设备列表"
                        : "终端未返回设备状态";
                    return result;
                }

                foreach (var item in data)
                {
                    var id = (item["id"]?.ToString() ?? "").Trim();
                    var deviceStatus = (item["status"]?.ToString() ?? "unknown").Trim();
                    var msg = item["msg"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        var existing = result.Devices.FirstOrDefault(d =>
                            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.Status = deviceStatus;
                            existing.Message = msg;
                            existing.IsOnline = IsOnline(deviceStatus);
                            continue;
                        }

                        result.Devices.Add(new DeviceHealth
                        {
                            Id = id,
                            Status = deviceStatus,
                            Message = msg,
                            IsOnline = IsOnline(deviceStatus)
                        });
                    }
                }

                foreach (var requiredId in RequiredDeviceIds)
                {
                    if (result.Devices.Any(d => string.Equals(
                        d.Id, requiredId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    result.Devices.Add(new DeviceHealth
                    {
                        Id = requiredId,
                        Status = "unknown",
                        Message = "not_reported",
                        IsOnline = false
                    });
                }

                result.IsHealthy = result.Devices.Count > 0 &&
                                   result.Devices.All(d => d.IsOnline);
            }
            catch (Exception ex)
            {
                result.IsHealthy = false;
                result.ErrorMessage = $"解析响应失败: {ex.Message}";
                Logger.Debug("[健康检测][调试] 响应解析异常，交由健康状态聚合：错误=" +
                    (ex.Message ?? "未知异常"));
            }

            return result;
        }

        private static bool IsOnline(string status)
        {
            return string.Equals(status, "online", StringComparison.OrdinalIgnoreCase);
        }

        private void NotifyStatus(HealthStatus status)
        {
            try
            {
                LogHealthState(status);
                _onStatusChanged?.Invoke(status);
            }
            catch (Exception ex)
            {
                Logger.Error("[健康检测] 状态通知异常", ex);
            }
        }

        private void LogHealthState(HealthStatus status)
        {
            var nowUtc = DateTime.UtcNow;
            var terminalIndex = _terminalManager?.CurrentIndex ?? 0;
            if (_healthTerminalIndex != terminalIndex)
            {
                _healthTerminalIndex = terminalIndex;
                _lastHealthState = HealthObservationState.Unknown;
                _healthFailureFirstUtc = DateTime.MinValue;
                _healthFailureWindowStartUtc = DateTime.MinValue;
                _healthFailureTotalCount = 0;
                _healthFailureWindowCount = 0;
            }

            if (IsHealthyStatus(status))
            {
                if (_lastHealthState == HealthObservationState.Abnormal)
                {
                    var durationSeconds = _healthFailureFirstUtc == DateTime.MinValue
                        ? 0
                        : Math.Max(0, (long)(nowUtc - _healthFailureFirstUtc).TotalSeconds);
                    _log(Logger.FormatModuleMessage(LogModules.TerminalCommunication, "信息",
                        $"终端连接已恢复：终端={GetCurrentTerminalDisplay()}，持续={durationSeconds}秒，累计失败={_healthFailureTotalCount}次 " +
                        Logger.FormatContextMessage("TerminalHealth",
                            terminalIndex: terminalIndex.ToString(), result: "Recovered")));
                }

                _lastHealthState = HealthObservationState.Normal;
                _healthFailureFirstUtc = DateTime.MinValue;
                _healthFailureWindowStartUtc = DateTime.MinValue;
                _healthFailureTotalCount = 0;
                _healthFailureWindowCount = 0;
                return;
            }

            var error = DescribeHealthFailure(status);
            if (_lastHealthState != HealthObservationState.Abnormal)
            {
                _lastHealthState = HealthObservationState.Abnormal;
                _healthFailureFirstUtc = nowUtc;
                _healthFailureWindowStartUtc = nowUtc;
                _healthFailureTotalCount = 1;
                _healthFailureWindowCount = 1;
                _log(Logger.FormatModuleMessage(LogModules.TerminalCommunication, "警告",
                    $"终端连接异常：终端={GetCurrentTerminalDisplay()}，错误={error}，准备自动恢复 " +
                    Logger.FormatContextMessage("TerminalHealth",
                        terminalIndex: terminalIndex.ToString(), result: "Failed",
                        errorCode: "health_check_failed")));
                return;
            }

            _healthFailureTotalCount++;
            _healthFailureWindowCount++;
            if (nowUtc - _healthFailureWindowStartUtc >= FailureSummaryWindow)
            {
                _log(Logger.FormatModuleMessage(LogModules.TerminalCommunication, "警告",
                    $"终端连接异常持续：60秒内失败{_healthFailureWindowCount}次，最近错误={error} " +
                    Logger.FormatContextMessage("TerminalHealth",
                        terminalIndex: terminalIndex.ToString(), result: "Failed",
                        errorCode: "health_check_failed")));
                _healthFailureWindowStartUtc = nowUtc;
                _healthFailureWindowCount = 1;
            }
        }

        private string GetCurrentTerminalDisplay()
        {
            var name = _terminalManager?.CurrentName;
            var index = _terminalManager?.CurrentIndex ?? 0;
            return string.IsNullOrWhiteSpace(name)
                ? "终端" + index
                : name + "（" + index + "）";
        }

        private static string DescribeHealthFailure(HealthStatus status)
        {
            if (status == null)
                return "健康检查未返回状态";
            if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
                return status.ErrorMessage;

            var unhealthyDevices = (status.Devices ?? new List<DeviceHealth>())
                .Where(d => d != null && !d.IsOnline)
                .Select(d => d.Id + "=" + d.Status)
                .ToArray();
            return unhealthyDevices.Length == 0
                ? "终端设备状态异常"
                : "设备异常：" + string.Join("、", unhealthyDevices);
        }

        private static HealthStatus CreateFailedStatus(string message)
        {
            return new HealthStatus
            {
                Timestamp = DateTime.Now,
                IsHealthy = false,
                ErrorMessage = message,
                Devices = new List<DeviceHealth>()
            };
        }

        public async Task StopAsync(int timeoutMs = 5000)
        {
            Timer timer;
            Task activePollTask;
            var initiateStop = false;

            lock (_lifecycleLock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    initiateStop = true;
                }
                timer = _timer;
                _timer = null;
                activePollTask = _activePollTask;
            }

            if (initiateStop)
            {
                try { timer?.Change(Timeout.Infinite, Timeout.Infinite); }
                catch (ObjectDisposedException) { }
                timer?.Dispose();
                try { _stopCts.Cancel(); }
                catch (ObjectDisposedException) { }
            }

            if (activePollTask == null || activePollTask.IsCompleted)
            {
                if (activePollTask != null)
                    await ObserveStoppedPollAsync(activePollTask).ConfigureAwait(false);
                return;
            }

            var boundedTimeoutMs = Math.Max(0, timeoutMs);
            var completed = await Task.WhenAny(activePollTask,
                Task.Delay(boundedTimeoutMs)).ConfigureAwait(false);
            if (completed != activePollTask)
            {
                Logger.Warn($"[健康检测] 停止等待超时: timeout_ms={boundedTimeoutMs}");
                return;
            }

            await ObserveStoppedPollAsync(activePollTask).ConfigureAwait(false);
        }

        private static async Task ObserveStoppedPollAsync(Task activePollTask)
        {
            try
            {
                await activePollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 预期取消。
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }

    public class HealthStatus
    {
        public DateTime Timestamp { get; set; }
        public string RequestId { get; set; }
        public string ResponseStatus { get; set; }
        public bool IsHealthy { get; set; }
        public string ErrorMessage { get; set; }
        public List<DeviceHealth> Devices { get; set; } = new List<DeviceHealth>();
    }

    public class DeviceHealth
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public bool IsOnline { get; set; }
    }
}
