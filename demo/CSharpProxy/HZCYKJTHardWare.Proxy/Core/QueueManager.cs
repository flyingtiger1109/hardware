using System;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// 管理所有固定工作队列及终端切换批次。
    /// 队列模型：
    ///   switchQueue：仅允许 1 个任务执行，不保留待替换任务
    ///   业务队列：1 个任务执行，并保留 1 个最新待执行任务
    ///
    /// 终端切换具有最高优先级。每次切换递增批次号，旧批次中的排队任务将被丢弃。
    /// </summary>
    public class QueueManager : IDisposable
    {
        // 终端切换批次号；切换前入队的任务将被丢弃
        private int _terminalGeneration;
        private volatile bool _switchingTerminal;
        private int _disposed;

        private readonly WorkerQueue<object> _switchQueue;
        private readonly WorkerQueue<object> _faceCapQueue;
        private readonly WorkerQueue<object> _fingerCapQueue;
        private readonly WorkerQueue<object> _irisQueue;
        private readonly WorkerQueue<object> _ocrQueue;
        private readonly WorkerQueue<object> _nfcQueue;
        private readonly WorkerQueue<object> _authorizeQueue;

        public int TerminalGeneration => Volatile.Read(ref _terminalGeneration);
        public bool SwitchingTerminal => _switchingTerminal;

        /// <summary>
        /// 递增终端代次计数器并返回新值，供 SwitchCoordinator 执行终端切换时使用。
        /// </summary>
        public int IncrementGeneration()
        {
            return Interlocked.Increment(ref _terminalGeneration);
        }

        /// <summary>
        /// 设置终端切换标志，供 SwitchCoordinator 使用。
        /// </summary>
        public void SetSwitching(bool value)
        {
            _switchingTerminal = value;
        }

        // 各工作队列的访问入口
        public WorkerQueue<object> SwitchQueue => _switchQueue;
        public WorkerQueue<object> FaceCaptureQueue => _faceCapQueue;
        public WorkerQueue<object> FingerprintCaptureQueue => _fingerCapQueue;
        public WorkerQueue<object> IrisQueue => _irisQueue;
        public WorkerQueue<object> OcrQueue => _ocrQueue;
        public WorkerQueue<object> NfcQueue => _nfcQueue;
        public WorkerQueue<object> AuthorizeQueue => _authorizeQueue;

        public QueueManager(DeviceCapabilityManager capabilities = null)
        {
            capabilities = capabilities ?? DeviceCapabilityManager.Instance;
            // 切换队列：最大容量为 1，优先级最高
            _switchQueue = new WorkerQueue<object>("切换终端", 1, ExecuteSwitch, false, 30000,
                capabilities.IsSupported(DeviceCapability.TerminalControl));

            // 业务队列：1 个任务执行，并保留 1 个最新待执行任务
            _faceCapQueue = new WorkerQueue<object>("人脸抓拍", 2, ExecuteFaceCapture, true, 15000,
                capabilities.IsSupported(DeviceCapability.Face));
            _fingerCapQueue = new WorkerQueue<object>("指纹抓拍", 2, ExecuteFingerprintCapture, true, 15000,
                capabilities.IsSupported(DeviceCapability.Fingerprint));
            _irisQueue = new WorkerQueue<object>("虹膜抓拍", 2, ExecuteIris, true, 20000,
                capabilities.IsSupported(DeviceCapability.Iris));

            _ocrQueue = new WorkerQueue<object>("OCR识别", 2, ExecuteOcr, true, 20000,
                capabilities.IsSupported(DeviceCapability.OCR));
            _nfcQueue = new WorkerQueue<object>("NFC读卡", 2, ExecuteNfc, true, 20000,
                capabilities.IsSupported(DeviceCapability.NfcCard));
            _authorizeQueue = new WorkerQueue<object>("授权", 2, ExecuteAuthorize, true, 20000,
                capabilities.IsSupported(DeviceCapability.Authorize));

        }

        /// <summary>
        /// 将已由 SwitchCoordinator 准入并分配版本的切换任务加入队列。
        /// </summary>
        internal bool EnqueueSwitch(SwitchRequest request)
        {
            if (request == null) return false;
            return _switchQueue.Enqueue(request, request.Generation);
        }

        /// <summary>
        /// 终端切换完成后清除切换标志。
        /// </summary>
        public void ClearSwitching()
        {
            _switchingTerminal = false;
            Logger.Debug("[终端切换] 切换完成，清除切换中标志");
        }

        /// <summary>
        /// 检查任务是否属于当前终端切换批次。批次号小于当前值时应丢弃该任务。
        /// </summary>
        public bool IsGenerationValid(int taskGeneration)
        {
            return taskGeneration >= Volatile.Read(ref _terminalGeneration);
        }

        public string GetAllStats()
        {
            return string.Join("\n",
                _switchQueue.GetStats(),
                _faceCapQueue.GetStats(),
                _fingerCapQueue.GetStats(),
                _irisQueue.GetStats(),
                _ocrQueue.GetStats(),
                _nfcQueue.GetStats(),
                _authorizeQueue.GetStats()
            );
        }

        // ====== 工作线程处理函数（由 ProxyServer 绑定）======

        public Action<SwitchRequest> SwitchHandler { get; set; }
        public Action<QueueTask<object>> FaceCaptureHandler { get; set; }
        public Action<QueueTask<object>> FingerprintCaptureHandler { get; set; }
        public Action<QueueTask<object>> IrisHandler { get; set; }
        public Action<QueueTask<object>> OcrHandler { get; set; }
        public Action<QueueTask<object>> NfcHandler { get; set; }
        public Action<QueueTask<object>> AuthorizeHandler { get; set; }

        private void ExecuteSwitch(QueueTask<object> task)
        {
            var req = task.Data as SwitchRequest;
            if (req != null && SwitchHandler != null)
                SwitchHandler(req);
        }

        private void ExecuteFaceCapture(QueueTask<object> task)
        {
            if (RejectStale(task, "人脸抓拍")) return;
            FaceCaptureHandler?.Invoke(task);
        }

        private void ExecuteFingerprintCapture(QueueTask<object> task)
        {
            if (RejectStale(task, "指纹抓拍")) return;
            FingerprintCaptureHandler?.Invoke(task);
        }

        private void ExecuteIris(QueueTask<object> task)
        {
            if (RejectStale(task, "虹膜抓拍")) return;
            IrisHandler?.Invoke(task);
        }

        private void ExecuteOcr(QueueTask<object> task)
        {
            if (RejectStale(task, "OCR")) return;
            OcrHandler?.Invoke(task);
        }

        private void ExecuteNfc(QueueTask<object> task)
        {
            if (RejectStale(task, "NFC")) return;
            NfcHandler?.Invoke(task);
        }

        private void ExecuteAuthorize(QueueTask<object> task)
        {
            if (RejectStale(task, "授权")) return;
            AuthorizeHandler?.Invoke(task);
        }

        private bool RejectStale(QueueTask<object> task, string operation)
        {
            if (IsGenerationValid(task.Generation)) return false;
            Logger.Warn($"[{operation}] 请求批次 {task.Generation} 早于当前终端切换批次 {_terminalGeneration}，已丢弃");
            WorkerQueue<object>.TryCompleteTask(task, "terminal_switching");
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var queues = new[]
            {
                _switchQueue, _faceCapQueue, _fingerCapQueue, _irisQueue,
                _ocrQueue, _nfcQueue, _authorizeQueue
            };

            // 先通知全部工作线程，再共享同一等待时限，避免多次顺序 Join(3000) 累积为过长的关闭等待。
            foreach (var queue in queues)
                queue?.RequestStop();

            var deadline = DateTime.UtcNow.AddMilliseconds(3000);
            foreach (var queue in queues)
            {
                if (queue == null) continue;
                var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                queue.WaitForStop(Math.Max(0, remaining));
            }
        }
    }

    public class SwitchRequest
    {
        public int TerminalIndex { get; set; }
        public int Generation { get; set; }
    }
}
