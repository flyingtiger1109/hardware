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
            _log("[运行时] 开始有序关闭...");

            try { _metricsReporter?.Stop(); }
            catch (Exception ex) { LogException("[运行时] 长稳指标停止异常", ex); }

            // 1. 取消令牌，停止接收新连接
            try { _cts?.Cancel(); }
            catch (Exception ex) { LogException("[运行时] 取消 Token 异常", ex); }

            try { _previewManager?.BeginShutdown(); }
            catch (Exception ex) { LogException("[运行时] 预览管理器取消异常", ex); }

            // 排空注册表和任务跟踪器前，取消正在传输的一次性回调
            try { _dllCallbackSender?.Stop(); }
            catch (Exception ex) { LogException("[运行时] 回调发送器停止失败", ex); }

            // 传输层排空与业务清理并行执行，并共享同一全局截止时间
            Task transportStopTask;
            try { transportStopTask = _transport.StopAsync(5000); }
            catch (Exception ex)
            {
                LogException("[运行时] 传输层停止异常", ex);
                transportStopTask = Task.CompletedTask;
            }

            // 2. 取消注册表中的全部等待请求
            try
            {
                _registry.CancelAll();
                Logger.Debug($"[运行时] 请求登记器已取消，活跃={_registry.ActiveCount}");
            }
            catch (Exception ex) { LogException("[运行时] 请求登记器取消异常", ex); }

            try
            {
                _processRegistry.ClearAll();
                Logger.Debug("[运行时] 终端流程登记器已清空");
            }
            catch (Exception ex) { LogException("[运行时] 流程会话清理异常", ex); }

            // 3. 释放业务队列，工作线程共享 3 秒时限
            try
            {
                _queueManager?.Dispose();
                Logger.Debug("[运行时] 队列管理器已释放");
            }
            catch (Exception ex) { LogException("[运行时] 队列管理器释放异常", ex); }

            // 4. 取消恢复任务、停止定时器并停止全部活动预览
            try
            {
                await _previewManager.ShutdownAsync(RemainingMs(deadline))
                    .ConfigureAwait(false);
                Logger.Debug("[运行时] 预览管理器已停止");
            }
            catch (Exception ex) { LogException("[运行时] 预览管理器停止异常", ex); }

            // 5. 在剩余时限内排空有界后台任务
            try
            {
                await _taskTracker.WaitAllAsync(RemainingMs(deadline)).ConfigureAwait(false);
                Logger.Debug($"[运行时] 任务跟踪器已排空：{_taskTracker.GetStats()}");
            }
            catch (Exception ex) { LogException("[运行时] 任务跟踪器排空异常", ex); }

            // 6. 在同一截止时间内完成传输层排空
            try
            {
                var remaining = RemainingMs(deadline);
                Task completed = transportStopTask.IsCompleted ? transportStopTask : null;
                if (completed == null && remaining > 0)
                    completed = await Task.WhenAny(transportStopTask, Task.Delay(remaining))
                        .ConfigureAwait(false);

                if (completed == transportStopTask)
                {
                    // 必须真正 await，否则 StopAsync 的异常只会成为未观察任务。
                    await transportStopTask.ConfigureAwait(false);
                Logger.Debug("[运行时] 传输层已停止");
                }
                else
                {
                    const string message = "[运行时] 传输层在全局关闭时限内未完成";
                    Logger.Warn(message);
                    _log(message);
                }
            }
            catch (Exception ex) { LogException("[运行时] 传输层停止异常", ex); }

            sw.Stop();

            // 7. 记录关闭遥测信息
            _log("[运行时] ====== 关闭遥测 ======");
            _log($"[运行时] 总耗时：{sw.ElapsedMilliseconds}ms");
            _log($"[运行时] 队列统计：\n" + (_queueManager?.GetAllStats() ?? "无"));
            _log("[运行时] 任务追踪：" + (_taskTracker?.GetStats() ?? "无"));
            _log($"[运行时] 请求登记：活跃={_registry.ActiveCount}，容量={_registry.MaxActiveEntries}");
            _log($"[运行时] 流程登记：当前路由={_processRegistry.CurrentCount}，保留绑定={_processRegistry.BindingCount}");
            _log("[运行时] 有序关闭完成");

            _cts?.Dispose();
            _cts = null;
        }

        private static int RemainingMs(DateTime deadline)
        {
            return Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
        }

        private void LogException(string context, Exception ex)
        {
            // ERROR 文件日志保留堆栈，UI 仅显示精简摘要。
            Logger.Error(context, ex);
            _log($"{context}：{ex.Message}");
        }
    }
}
