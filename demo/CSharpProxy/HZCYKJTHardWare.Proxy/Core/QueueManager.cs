using System;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// Manages all fixed worker queues and terminal switch batch tracking.
    /// Queues:
    ///   switchQueue:          1 executing, no pending replacement
    ///   business queues:      1 executing + 1 latest pending
    ///
    /// Terminal switch is the highest priority. Each switch increments a batch number;
    /// queued tasks from an older batch are discarded.
    /// </summary>
    public class QueueManager : IDisposable
    {
        // Terminal switch batch number. Tasks enqueued before the switch are discarded.
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

        public int TerminalGeneration => _terminalGeneration;
        public bool SwitchingTerminal => _switchingTerminal;

        /// <summary>
        /// Increment the terminal generation counter and return the new value.
        /// Used by SwitchCoordinator during terminal switch.
        /// </summary>
        public int IncrementGeneration()
        {
            return Interlocked.Increment(ref _terminalGeneration);
        }

        /// <summary>
        /// Set the switching flag. Used by SwitchCoordinator.
        /// </summary>
        public void SetSwitching(bool value)
        {
            _switchingTerminal = value;
        }

        // Accessors for each queue
        public WorkerQueue<object> SwitchQueue => _switchQueue;
        public WorkerQueue<object> FaceCaptureQueue => _faceCapQueue;
        public WorkerQueue<object> FingerprintCaptureQueue => _fingerCapQueue;
        public WorkerQueue<object> IrisQueue => _irisQueue;
        public WorkerQueue<object> OcrQueue => _ocrQueue;
        public WorkerQueue<object> NfcQueue => _nfcQueue;
        public WorkerQueue<object> AuthorizeQueue => _authorizeQueue;

        public QueueManager()
        {
            // Switch queue: max 1, highest priority
            _switchQueue = new WorkerQueue<object>("切换终端", 1, ExecuteSwitch, false, 30000);

            // Business queues: one executing + one latest pending task.
            _faceCapQueue = new WorkerQueue<object>("人脸抓拍", 2, ExecuteFaceCapture, true, 15000);
            _fingerCapQueue = new WorkerQueue<object>("指纹抓拍", 2, ExecuteFingerprintCapture, true, 15000);
            _irisQueue = new WorkerQueue<object>("虹膜抓拍", 2, ExecuteIris, true, 20000);

            _ocrQueue = new WorkerQueue<object>("OCR识别", 2, ExecuteOcr, true, 20000);
            _nfcQueue = new WorkerQueue<object>("NFC读卡", 2, ExecuteNfc, true, 20000);
            _authorizeQueue = new WorkerQueue<object>("授权", 2, ExecuteAuthorize, true, 20000);

        }

        /// <summary>
        /// Enqueue a switch already admitted and versioned by SwitchCoordinator.
        /// </summary>
        internal bool EnqueueSwitch(SwitchRequest request)
        {
            if (request == null) return false;
            return _switchQueue.Enqueue(request, request.Generation);
        }

        /// <summary>
        /// Clear switching flag after switch completes.
        /// </summary>
        public void ClearSwitching()
        {
            _switchingTerminal = false;
            Logger.Info("[终端切换] 切换完成, 清除切换中标志");
        }

        /// <summary>
        /// Check if a task belongs to the current terminal switch batch.
        /// If its batch is stale (< current), the task should be discarded.
        /// </summary>
        public bool IsGenerationValid(int taskGeneration)
        {
            return taskGeneration >= _terminalGeneration;
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

        // ====== Worker handlers (to be wired by ProxyServer) ======

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
            Logger.Warn($"[{operation}] 请求批次 {task.Generation} 早于当前终端切换批次 {_terminalGeneration}, 已丢弃");
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

            // Signal every worker first, then share one global wait budget. This
            // prevents ten sequential Join(3000) calls from turning into 30 seconds.
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
