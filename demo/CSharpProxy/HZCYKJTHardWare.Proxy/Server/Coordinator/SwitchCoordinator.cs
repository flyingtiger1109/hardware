using System;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Server.Runtime;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Coordinator
{
    /// <summary>
    /// 按以下顺序协调终端切换：
    ///   1. 设置切换标志
    ///   2. 递增代次批次
    ///   3. 在 Registry 中取消旧代次请求
    ///   4. 停止全部活动预览并保留重启信息
    ///   5. 在 TerminalManager 中切换终端
    ///   6. 清除切换标志
    ///   7. 在后台使用新终端重启预览
    ///
    /// UI 与 DLL 请求均通过此协调器进入切换流程。
    /// </summary>
    public sealed class SwitchCoordinator
    {
        private readonly TerminalManager _terminalManager;
        private readonly PreviewManager _previewManager;
        private readonly RequestRegistry _requestRegistry;
        private readonly QueueManager _queueManager;
        private readonly Action<string> _log;
        private readonly Action<int> _onTerminalChanged;
        private readonly ControlOperationGate _controlGate;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly object _routeEpochSync = new object();

        private int _isSwitching;
        private ControlOperationGate.Lease _switchLease;
        private CancellationTokenSource _routeEpochCancellation =
            new CancellationTokenSource();

        public bool IsSwitching => Volatile.Read(ref _isSwitching) != 0;
        public int CurrentGeneration => _queueManager.TerminalGeneration;

        /// <summary>
        /// 原子捕获单次准入操作使用的终端路由和代次。切换操作在变更路由前取消令牌。
        /// </summary>
        internal bool TryCaptureRoute(out TerminalRouteEpochSnapshot snapshot)
        {
            lock (_routeEpochSync)
            {
                if (_isSwitching != 0 || _queueManager.SwitchingTerminal ||
                    _routeEpochCancellation == null ||
                    _routeEpochCancellation.IsCancellationRequested)
                {
                    snapshot = null;
                    return false;
                }

                snapshot = new TerminalRouteEpochSnapshot(
                    _terminalManager.CurrentRoute,
                    _queueManager.TerminalGeneration,
                    _routeEpochCancellation.Token);
                return true;
            }
        }

        internal SwitchCoordinator(
            TerminalManager terminalManager,
            PreviewManager previewManager,
            RequestRegistry requestRegistry,
            QueueManager queueManager,
            Action<string> log,
            Action<int> onTerminalChanged = null,
            ControlOperationGate controlGate = null,
            ActiveTasksTracker taskTracker = null)
        {
            _terminalManager = terminalManager;
            _previewManager = previewManager;
            _requestRegistry = requestRegistry;
            _queueManager = queueManager;
            _log = log;
            _onTerminalChanged = onTerminalChanged;
            _controlGate = controlGate ?? new ControlOperationGate();
            _taskTracker = taskTracker;
        }

        /// <summary>
        /// 执行完整终端切换。已有切换正在进行时返回 false。
        /// </summary>
        public async Task<bool> SwitchToAsync(int terminalIndex, string requestId = null)
        {
            if (!EnsureTargetTerminalConfigured(terminalIndex))
                return false;

            if (string.IsNullOrWhiteSpace(requestId))
                requestId = "SWITCH_" + Guid.NewGuid().ToString("N").Substring(0, 16);

            if (!TryBeginSwitch(terminalIndex, out var generation))
            {
                _log("[终端切换][警告] 终端切换被拒绝：已有切换正在执行");
                return false;
            }
            return await SwitchToCoreAsync(terminalIndex, generation, requestId).ConfigureAwait(false);
        }

        /// <summary>
        /// 从队列工作线程请求切换，例如 DllCommandHandler。
        /// 此方法设置切换标志并递增代次，实际切换由专用切换工作线程执行。
        /// </summary>
        public bool RequestSwitch(int terminalIndex, string requestId = null)
        {
            if (!EnsureTargetTerminalConfigured(terminalIndex))
                return false;

            if (string.IsNullOrWhiteSpace(requestId))
                requestId = "SWITCH_" + Guid.NewGuid().ToString("N").Substring(0, 16);

            if (!TryBeginSwitch(terminalIndex, out var generation))
                return false;

            var request = new SwitchRequest
            {
                TerminalIndex = terminalIndex,
                Generation = generation,
                RequestId = requestId
            };

            if (_queueManager.EnqueueSwitch(request))
                return true;

            FinishSwitch();
            _log("[终端切换][警告] 终端切换队列已停止或已满");
            return false;
        }

        private bool EnsureTargetTerminalConfigured(int terminalIndex)
        {
            if (_terminalManager.IsTerminalConfigured(terminalIndex))
                return true;

            var terminalName = _terminalManager.GetTerminalName(terminalIndex);
            if (string.IsNullOrWhiteSpace(terminalName))
                terminalName = "终端" + terminalIndex;
            var message = "[终端切换][警告] 拒绝切换：目标终端=" + terminalIndex +
                "(" + terminalName + ")未配置，code=terminal_not_configured";
            if (_log != null)
                _log(message);
            else
                Logger.Warn(message);
            return false;
        }

        internal void ExecuteQueuedSwitch(SwitchRequest request)
        {
            if (request == null)
            {
                FinishSwitch();
                return;
            }
            SwitchToCoreAsync(request.TerminalIndex, request.Generation, request.RequestId)
                .GetAwaiter().GetResult();
        }

        private bool TryBeginSwitch(int terminalIndex, out int generation)
        {
            generation = 0;
            var lease = _controlGate.TryEnter("switch_terminal");
            if (lease == null)
                return false;

            CancellationTokenSource previousEpoch;
            lock (_routeEpochSync)
            {
                if (_isSwitching != 0)
                {
                    lease.Dispose();
                    return false;
                }

                Volatile.Write(ref _isSwitching, 1);
                _switchLease = lease;
                _queueManager.SetSwitching(true);
                generation = _queueManager.IncrementGeneration();
                previousEpoch = _routeEpochCancellation;
                _routeEpochCancellation = null;
            }

            try
            {
                CancelEpoch(previousEpoch);
                _requestRegistry.CancelOlderThan(generation);
                Logger.Debug($"[终端切换] 下发切换请求，批次={generation}，目标终端={terminalIndex}");
                return true;
            }
            catch
            {
                FinishSwitch();
                throw;
            }
        }

        private async Task<bool> SwitchToCoreAsync(int terminalIndex, int generation,
            string requestId)
        {
            var switchFinished = false;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await _previewManager.StopTerminalBoundPreviewsForSwitchAsync().ConfigureAwait(false);
                Logger.Debug($"[性能] 终端切换停止 耗时={sw.ElapsedMilliseconds}ms");

                var phase = sw.ElapsedMilliseconds;
                _terminalManager.SwitchTo(terminalIndex);
                Logger.Debug($"[性能] 终端管理器切换 耗时={sw.ElapsedMilliseconds - phase}ms");
                _log("[终端切换][调试] 当前终端=" + _terminalManager.CurrentName);
                NotifyTerminalChanged(_terminalManager.CurrentIndex);

                var restartBaseUrl = _terminalManager.CurrentBaseUrl;
                var switchElapsedMs = sw.ElapsedMilliseconds;
                FinishSwitch();
                switchFinished = true;

                var completedMessage = $"[终端切换] 切换完成：当前={_terminalManager.CurrentName}，" +
                    $"预览进入后台恢复，Operation=SwitchTerminal RequestId={PreviewManager.FormatRequestId(requestId)} " +
                    $"Result=Success DurationMs={switchElapsedMs}";
                if (_log != null)
                    _log(completedMessage);
                else
                    Logger.Info(completedMessage);
                Logger.Debug($"[性能] 终端切换总耗时={switchElapsedMs}ms");
                StartPreviewRestartInBackground(restartBaseUrl, generation, switchElapsedMs);
                return true;
            }
            catch (Exception ex)
            {
                _log("[终端切换][错误] 切换失败：Operation=SwitchTerminal RequestId=" +
                    PreviewManager.FormatRequestId(requestId) + "，错误=" + ex.Message);
                return false;
            }
            finally
            {
                if (!switchFinished)
                    FinishSwitch();
            }
        }

        private void StartPreviewRestartInBackground(string terminalBaseUrl, int generation, long switchElapsedMs)
        {
            Func<Task> restartWork = async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Logger.Debug($"[终端切换] 预览后台恢复开始，批次={generation}，切换耗时={switchElapsedMs}ms");
                try
                {
                    await _previewManager.RestartPreviewsOnTerminalSwitch(
                        terminalBaseUrl,
                        () => _queueManager.IsGenerationValid(generation)).ConfigureAwait(false);
                    Logger.Debug($"[终端切换] 预览后台恢复完成，批次={generation}，耗时={sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[终端切换] 预览后台恢复异常，批次={generation}，耗时={sw.ElapsedMilliseconds}ms", ex);
                }
            };

            if (_taskTracker != null)
            {
                if (!_taskTracker.TryRun(restartWork,
                    "preview_switch_recovery_" + generation))
                    Logger.Warn($"[终端切换] 预览后台恢复未启动，任务容量已满，批次={generation}");
                return;
            }

            _ = Task.Run(restartWork);
        }

        private void FinishSwitch()
        {
            lock (_routeEpochSync)
            {
                if (_routeEpochCancellation == null)
                    _routeEpochCancellation = new CancellationTokenSource();
                _queueManager.ClearSwitching();
                Volatile.Write(ref _isSwitching, 0);
            }
            var lease = Interlocked.Exchange(ref _switchLease, null);
            lease?.Dispose();
        }

        private static void CancelEpoch(CancellationTokenSource cancellation)
        {
            if (cancellation == null) return;
            try
            {
                cancellation.Cancel();
            }
            catch (AggregateException ex)
            {
                Logger.Error("[终端切换] 取消旧终端批次时发生回调异常", ex);
            }
        }

        private void NotifyTerminalChanged(int terminalIndex)
        {
            try
            {
                _onTerminalChanged?.Invoke(terminalIndex);
            }
            catch (Exception ex)
            {
                Logger.Error("[终端切换] 通知当前终端变化失败", ex);
            }
        }
    }
}
