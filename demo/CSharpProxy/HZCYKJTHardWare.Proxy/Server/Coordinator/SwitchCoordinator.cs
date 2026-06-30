using System;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Coordinator
{
    /// <summary>
    /// Orchestrates terminal switch with proper ordering:
    ///   1. Set switching flag
    ///   2. Increment generation batch
    ///   3. Cancel old-generation requests in Registry
    ///   4. Stop all active previews (preserving restart info)
    ///   5. Switch terminal in TerminalManager
    ///   6. Restart previews on new terminal
    ///   7. Clear switching flag
    ///
    /// Both UI and DLL requests enter through this coordinator.
    /// </summary>
    public sealed class SwitchCoordinator
    {
        private readonly TerminalManager _terminalManager;
        private readonly PreviewManager _previewManager;
        private readonly RequestRegistry _requestRegistry;
        private readonly QueueManager _queueManager;
        private readonly Action<string> _log;

        private int _isSwitching;

        public bool IsSwitching => Volatile.Read(ref _isSwitching) != 0;
        public int CurrentGeneration => _queueManager.TerminalGeneration;

        internal SwitchCoordinator(
            TerminalManager terminalManager,
            PreviewManager previewManager,
            RequestRegistry requestRegistry,
            QueueManager queueManager,
            Action<string> log)
        {
            _terminalManager = terminalManager;
            _previewManager = previewManager;
            _requestRegistry = requestRegistry;
            _queueManager = queueManager;
            _log = log;
        }

        /// <summary>
        /// Execute a full terminal switch. Returns false if another switch is
        /// already in progress.
        /// </summary>
        public async Task<bool> SwitchToAsync(int terminalIndex)
        {
            if (!TryBeginSwitch(terminalIndex, out var generation))
            {
                _log("[Coordinator] 终端切换被拒绝：已有切换正在执行");
                return false;
            }
            return await SwitchToCoreAsync(terminalIndex, generation).ConfigureAwait(false);
        }

        /// <summary>
        /// Request a switch from a queue worker (e.g., from DllCommandHandler).
        /// Sets the switching flag and increments generation. The actual switch
        /// execution happens on the dedicated switch worker.
        /// </summary>
        public bool RequestSwitch(int terminalIndex)
        {
            if (!TryBeginSwitch(terminalIndex, out var generation))
                return false;

            var request = new SwitchRequest
            {
                TerminalIndex = terminalIndex,
                Generation = generation
            };

            if (_queueManager.EnqueueSwitch(request))
                return true;

            FinishSwitch();
            _log("[Coordinator] 终端切换队列已停止或已满");
            return false;
        }

        internal void ExecuteQueuedSwitch(SwitchRequest request)
        {
            if (request == null)
            {
                FinishSwitch();
                return;
            }
            SwitchToCoreAsync(request.TerminalIndex, request.Generation).GetAwaiter().GetResult();
        }

        private bool TryBeginSwitch(int terminalIndex, out int generation)
        {
            generation = 0;
            if (Interlocked.Exchange(ref _isSwitching, 1) != 0)
                return false;

            _queueManager.SetSwitching(true);
            generation = _queueManager.IncrementGeneration();
            _requestRegistry.CancelOlderThan(generation);
            Logger.Info($"[Coordinator] 下发切换请求，批次={generation}，目标终端={terminalIndex}");
            return true;
        }

        private async Task<bool> SwitchToCoreAsync(int terminalIndex, int generation)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await _previewManager.StopAllAsync(preserveRestartInfo: true).ConfigureAwait(false);
                Logger.Debug($"[性能] 终端切换停止 耗时={sw.ElapsedMilliseconds}ms");

                var phase = sw.ElapsedMilliseconds;
                _terminalManager.SwitchTo(terminalIndex);
                Logger.Debug($"[性能] 终端管理器切换 耗时={sw.ElapsedMilliseconds - phase}ms");
                _log("[Coordinator] 当前终端=" + _terminalManager.CurrentName);

                phase = sw.ElapsedMilliseconds;
                await _previewManager.RestartPreviewsOnTerminalSwitch(
                    _terminalManager.CurrentBaseUrl,
                    () => _queueManager.IsGenerationValid(generation)).ConfigureAwait(false);
                Logger.Debug($"[性能] 终端切换启动 耗时={sw.ElapsedMilliseconds - phase}ms");
                Logger.Debug($"[性能] 终端切换总耗时={sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                _log("[Coordinator] 切换失败: " + ex.Message);
                return false;
            }
            finally
            {
                FinishSwitch();
            }
        }

        private void FinishSwitch()
        {
            _queueManager.ClearSwitching();
            Interlocked.Exchange(ref _isSwitching, 0);
        }
    }
}
