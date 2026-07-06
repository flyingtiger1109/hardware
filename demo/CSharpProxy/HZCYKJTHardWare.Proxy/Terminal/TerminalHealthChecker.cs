using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public class TerminalHealthChecker : IDisposable
    {
        private const int PollIntervalMs = 5 * 60 * 1000; // 5 minutes

        private readonly TerminalClient _terminalClient;
        private readonly TerminalManager _terminalManager;
        private readonly Action<string> _log;
        private readonly Action<HealthStatus> _onStatusChanged;
        private System.Threading.Timer _timer;
        private int _running;
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
            _timer = new System.Threading.Timer(PollCallback, null,
                PollIntervalMs, PollIntervalMs);
            _log("[健康检测] 已启动，轮询间隔 5 分钟");
        }

        private async void PollCallback(object state)
        {
            if (_disposed || Interlocked.Exchange(ref _running, 1) == 1)
                return;

            try
            {
                var route = _terminalManager.CurrentRoute;
                if (string.IsNullOrEmpty(route.BaseUrl)) return;

                var (ok, response) = await _terminalClient.GetJsonAsync(
                    route.BaseUrl, "/resources/devices/status", 5000).ConfigureAwait(false);

                var status = new HealthStatus
                {
                    Timestamp = DateTime.Now,
                    Devices = new List<DeviceHealth>()
                };

                if (!ok || string.IsNullOrEmpty(response))
                {
                    status.IsHealthy = false;
                    status.ErrorMessage = "终端不可达或超时";
                    _log("[健康检测] 状态异常: 终端不可达");
                }
                else
                {
                    ParseHealthResponse(response, status);
                }

                _onStatusChanged?.Invoke(status);
            }
            catch (Exception ex)
            {
                Logger.Error("[健康检测] 轮询异常", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private void ParseHealthResponse(string json, HealthStatus status)
        {
            try
            {
                var obj = JObject.Parse(json);
                var data = obj["data"] as JArray;
                bool allOnline = true;
                if (data != null)
                {
                    foreach (var item in data)
                    {
                        var id = item["id"]?.ToString();
                        var deviceStatus = item["status"]?.ToString();
                        var msg = item["msg"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(id))
                        {
                            bool isOnline = string.Equals(deviceStatus, "online",
                                StringComparison.OrdinalIgnoreCase);
                            status.Devices.Add(new DeviceHealth
                            {
                                Id = id,
                                Status = deviceStatus ?? "unknown",
                                Message = msg,
                                IsOnline = isOnline
                            });
                            if (!isOnline) allOnline = false;
                        }
                    }
                }
                status.IsHealthy = allOnline;
                if (!allOnline)
                {
                    var offlineDevices = status.Devices
                        .Where(d => !d.IsOnline)
                        .Select(d => $"{d.Id}={d.Status}");
                    _log($"[健康检测] 部分硬件异常: {string.Join(", ", offlineDevices)}");
                }
            }
            catch (Exception ex)
            {
                status.IsHealthy = false;
                status.ErrorMessage = $"解析响应失败: {ex.Message}";
                Logger.Error("[健康检测] 响应解析异常", ex);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;
        }
    }

    public class HealthStatus
    {
        public DateTime Timestamp { get; set; }
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
