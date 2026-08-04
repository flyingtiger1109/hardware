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
        private volatile bool _disposed;

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
            _log("[健康检测] 已启动，正常轮询间隔 5 分钟；异常状态按 5/10/20/40/60 秒退避复查");
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
                    nextDelayMs = ResolveNextDelayAndUpdateRetry(
                        CreateFailedStatus("终端地址为空"));
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
                    _log("[健康检测] 状态异常: 终端连接失败");
                }
                else
                {
                    status = ParseResponse(response, DateTime.Now);
                    nextDelayMs = ResolveNextDelayAndUpdateRetry(status);
                    if (!string.IsNullOrEmpty(status.ErrorMessage))
                    {
                        _log("[健康检测] 状态异常: " + status.ErrorMessage);
                    }
                    else if (!status.IsHealthy)
                    {
                        var unhealthyDevices = status.Devices
                            .Where(d => !d.IsOnline)
                            .Select(d => $"{d.Id}={d.Status}");
                        _log($"[健康检测] 部分硬件异常: {string.Join(", ", unhealthyDevices)}");
                    }
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
                Logger.Error("[健康检测] 轮询异常", ex);
                nextDelayMs = ResolveNextDelayAndUpdateRetry(
                    CreateFailedStatus("健康检测执行失败"));
                if (!cancellationToken.IsCancellationRequested && (route == null ||
                    _terminalManager.CurrentRoute.RouteEpoch == route.RouteEpoch))
                    NotifyStatus(CreateFailedStatus("健康检测执行失败"));
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
                _log("[健康检测] 快速复查已完成，后续每 5 分钟慢速探测一次");
                return delay;
            }

            Interlocked.Exchange(ref _retryAttempt, retryAttempt + 1);
            _log($"[健康检测] 将在 {delay / 1000} 秒后自动复查");
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
                Logger.Error("[健康检测] 响应解析异常", ex);
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
                _onStatusChanged?.Invoke(status);
            }
            catch (Exception ex)
            {
                Logger.Error("[健康检测] 状态通知异常", ex);
            }
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
