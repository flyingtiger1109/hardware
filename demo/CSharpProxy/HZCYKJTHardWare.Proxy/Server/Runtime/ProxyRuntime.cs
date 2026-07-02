using System;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// Encapsulates the ordered shutdown sequence for the proxy process.
    ///
    /// Order: Cancel token → Cancel registry → Drain tasks → Dispose queues →
    ///        Stop previews → Stop transport → Log telemetry
    ///
    /// Also holds the CancellationTokenSource that controls the accept loop lifetime.
    /// </summary>
    public sealed class ProxyRuntime
    {
        private readonly TransportLayer _transport;
        private readonly RequestRegistry _registry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly QueueManager _queueManager;
        private readonly PreviewManager _previewManager;
        private readonly DllCallbackSender _dllCallbackSender;
        private readonly Action<string> _log;
        private readonly object _stopLock = new object();
        private CancellationTokenSource _cts;
        private Task _stopTask;

        internal CancellationToken Token => _cts?.Token ?? CancellationToken.None;

        internal ProxyRuntime(
            TransportLayer transport,
            RequestRegistry registry,
            TerminalProcessRegistry processRegistry,
            ActiveTasksTracker taskTracker,
            QueueManager queueManager,
            PreviewManager previewManager,
            DllCallbackSender dllCallbackSender,
            Action<string> log)
        {
            _transport = transport;
            _registry = registry;
            _processRegistry = processRegistry;
            _taskTracker = taskTracker;
            _queueManager = queueManager;
            _previewManager = previewManager;
            _dllCallbackSender = dllCallbackSender;
            _log = log;
        }

        /// <summary>
        /// Create a fresh CancellationTokenSource for this run session.
        /// Called at the beginning of Start().
        /// </summary>
        internal CancellationTokenSource BeginSession()
        {
            _cts = new CancellationTokenSource();
            return _cts;
        }

        /// <summary>
        /// Ordered graceful shutdown with one shared five-second budget.
        /// </summary>
        internal Task StopAsync()
        {
            lock (_stopLock)
            {
                if (_stopTask == null)
                    _stopTask = StopCoreAsync();
                return _stopTask;
            }
        }

        private async Task StopCoreAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var deadline = DateTime.UtcNow.AddMilliseconds(5000);
            _log("[Runtime] 开始有序关闭...");

            // 1. Cancel token — stops accepting new connections
            try { _cts?.Cancel(); }
            catch (Exception ex) { _log($"[Runtime] 取消Token异常: {ex.Message}"); }

            // Cancel any in-flight one-shot callback before draining the
            // registry and task tracker.
            try { _dllCallbackSender?.Stop(); }
            catch (Exception ex) { _log($"[Runtime] callback sender stop failed: {ex.Message}"); }

            // Transport drain runs in parallel with business cleanup and shares
            // the same global deadline.
            Task transportStopTask;
            try { transportStopTask = _transport.StopAsync(5000); }
            catch (Exception ex)
            {
                _log($"[Runtime] TransportLayer停止异常: {ex.Message}");
                transportStopTask = Task.CompletedTask;
            }

            // 2. Cancel all pending requests in registry
            try
            {
                _registry.CancelAll();
                Logger.Debug($"[Runtime] Registry已取消, 活跃={_registry.ActiveCount}");
            }
            catch (Exception ex) { _log($"[Runtime] Registry取消异常: {ex.Message}"); }

            try
            {
                _processRegistry.ClearAll();
                Logger.Debug("[Runtime] TerminalProcessRegistry已清空");
            }
            catch (Exception ex) { _log($"[Runtime] 流程会话清理异常: {ex.Message}"); }

            // 3. Dispose business queues (one shared 3s worker budget)
            try
            {
                _queueManager?.Dispose();
                Logger.Debug($"[Runtime] QueueManager已释放");
            }
            catch (Exception ex) { _log($"[Runtime] QueueManager释放异常: {ex.Message}"); }

            // 4. Stop all active previews
            try
            {
                _previewManager?.StopAll();
                Logger.Debug($"[Runtime] PreviewManager已停止");
            }
            catch (Exception ex) { _log($"[Runtime] PreviewManager停止异常: {ex.Message}"); }

            // 5. Drain bounded background work within the remaining budget.
            try
            {
                await _taskTracker.WaitAllAsync(RemainingMs(deadline)).ConfigureAwait(false);
                Logger.Debug($"[Runtime] TaskTracker已排空: {_taskTracker.GetStats()}");
            }
            catch (Exception ex) { _log($"[Runtime] TaskTracker排空异常: {ex.Message}"); }

            // 6. Finish transport drain within the same deadline.
            try
            {
                var remaining = RemainingMs(deadline);
                if (!transportStopTask.IsCompleted && remaining > 0)
                    await Task.WhenAny(transportStopTask, Task.Delay(remaining)).ConfigureAwait(false);
                Logger.Debug($"[Runtime] TransportLayer已停止");
            }
            catch (Exception ex) { _log($"[Runtime] TransportLayer停止异常: {ex.Message}"); }

            sw.Stop();

            // 7. Shutdown telemetry
            _log("[Runtime] ====== 关闭遥测 ======");
            _log($"[Runtime] 总耗时: {sw.ElapsedMilliseconds}ms");
            _log($"[Runtime] 队列统计:\n" + (_queueManager?.GetAllStats() ?? "无"));
            _log($"[Runtime] 任务追踪: " + (_taskTracker?.GetStats() ?? "无"));
            _log($"[Runtime] Registry: 活跃={_registry.ActiveCount}, 容量={_registry.MaxActiveEntries}");
            _log($"[Runtime] ProcessRegistry: 活跃终端={_processRegistry.ActiveCount}");
            _log("[Runtime] 有序关闭完成");

            _cts?.Dispose();
            _cts = null;
        }

        private static int RemainingMs(DateTime deadline)
        {
            return Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
        }
    }
}
