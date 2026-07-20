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
    /// 封装 Proxy 进程的有序关闭流程。
    ///
    /// 顺序：取消令牌 → 取消注册表 → 排空任务 → 释放队列 →
    ///       停止预览 → 停止传输层 → 记录遥测信息
    ///
    /// 同时持有控制连接接收循环生命周期的 CancellationTokenSource。
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
        private readonly RuntimeMetricsReporter _metricsReporter;
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
            RuntimeMetricsReporter metricsReporter,
            Action<string> log)
        {
            _transport = transport;
            _registry = registry;
            _processRegistry = processRegistry;
            _taskTracker = taskTracker;
            _queueManager = queueManager;
            _previewManager = previewManager;
            _dllCallbackSender = dllCallbackSender;
            _metricsReporter = metricsReporter;
            _log = log;
        }

        /// <summary>
        /// 为本次运行会话创建新的 CancellationTokenSource，在 Start() 开始时调用。
        /// </summary>
        internal CancellationTokenSource BeginSession()
        {
            _cts = new CancellationTokenSource();
            return _cts;
        }

        /// <summary>
        /// 执行有序正常关闭，所有步骤共享 5 秒时限。
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

            try { _metricsReporter?.Stop(); }
            catch (Exception ex) { _log($"[Runtime] 长稳指标停止异常: {ex.Message}"); }

            // 1. 取消令牌，停止接收新连接
            try { _cts?.Cancel(); }
            catch (Exception ex) { _log($"[Runtime] 取消Token异常: {ex.Message}"); }

            try { _previewManager?.BeginShutdown(); }
            catch (Exception ex) { _log($"[Runtime] PreviewManager取消异常: {ex.Message}"); }

            // 排空注册表和任务跟踪器前，取消正在传输的一次性回调
            try { _dllCallbackSender?.Stop(); }
            catch (Exception ex) { _log($"[Runtime] callback sender stop failed: {ex.Message}"); }

            // 传输层排空与业务清理并行执行，并共享同一全局截止时间
            Task transportStopTask;
            try { transportStopTask = _transport.StopAsync(5000); }
            catch (Exception ex)
            {
                _log($"[Runtime] TransportLayer停止异常: {ex.Message}");
                transportStopTask = Task.CompletedTask;
            }

            // 2. 取消注册表中的全部等待请求
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

            // 3. 释放业务队列，工作线程共享 3 秒时限
            try
            {
                _queueManager?.Dispose();
                Logger.Debug($"[Runtime] QueueManager已释放");
            }
            catch (Exception ex) { _log($"[Runtime] QueueManager释放异常: {ex.Message}"); }

            // 4. 取消恢复任务、停止定时器并停止全部活动预览
            try
            {
                await _previewManager.ShutdownAsync(RemainingMs(deadline))
                    .ConfigureAwait(false);
                Logger.Debug($"[Runtime] PreviewManager已停止");
            }
            catch (Exception ex) { _log($"[Runtime] PreviewManager停止异常: {ex.Message}"); }

            // 5. 在剩余时限内排空有界后台任务
            try
            {
                await _taskTracker.WaitAllAsync(RemainingMs(deadline)).ConfigureAwait(false);
                Logger.Debug($"[Runtime] TaskTracker已排空: {_taskTracker.GetStats()}");
            }
            catch (Exception ex) { _log($"[Runtime] TaskTracker排空异常: {ex.Message}"); }

            // 6. 在同一截止时间内完成传输层排空
            try
            {
                var remaining = RemainingMs(deadline);
                if (!transportStopTask.IsCompleted && remaining > 0)
                    await Task.WhenAny(transportStopTask, Task.Delay(remaining)).ConfigureAwait(false);
                Logger.Debug($"[Runtime] TransportLayer已停止");
            }
            catch (Exception ex) { _log($"[Runtime] TransportLayer停止异常: {ex.Message}"); }

            sw.Stop();

            // 7. 记录关闭遥测信息
            _log("[Runtime] ====== 关闭遥测 ======");
            _log($"[Runtime] 总耗时: {sw.ElapsedMilliseconds}ms");
            _log($"[Runtime] 队列统计:\n" + (_queueManager?.GetAllStats() ?? "无"));
            _log($"[Runtime] 任务追踪: " + (_taskTracker?.GetStats() ?? "无"));
            _log($"[Runtime] Registry: 活跃={_registry.ActiveCount}, 容量={_registry.MaxActiveEntries}");
            _log($"[Runtime] ProcessRegistry: 当前路由={_processRegistry.CurrentCount}, 保留绑定={_processRegistry.BindingCount}");
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
