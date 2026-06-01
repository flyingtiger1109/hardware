using System;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// Manages all fixed worker queues and terminal generation tracking.
    /// Queues:
    ///   switchQueue:          1 thread, priority=highest, max=1
    ///   faceCaptureQueue:     1 thread, max=2
    ///   fingerprintCaptureQueue: 1 thread, max=2
    ///   ocrQueue:             1 thread, max=2
    ///   nfcQueue:             1 thread, max=2
    ///   facePreviewQueue:     1 thread, max=1 (replace mode)
    ///   fingerprintPreviewQueue: 1 thread, max=1 (replace mode)
    ///   miscQueue:            1 thread, max=3 (process start/end, authorize)
    ///
    /// Terminal switch is the highest priority. It increments a generation counter;
    /// all queued tasks with a lower generation are silently discarded.
    /// </summary>
    public class QueueManager : IDisposable
    {
        // Generation counter: incremented on each terminal switch
        // Tasks enqueued before the switch (lower gen) are discarded
        private int _terminalGeneration;
        private volatile bool _switchingTerminal;

        private readonly WorkerQueue<object> _switchQueue;
        private readonly WorkerQueue<object> _faceCapQueue;
        private readonly WorkerQueue<object> _fingerCapQueue;
        private readonly WorkerQueue<object> _ocrQueue;
        private readonly WorkerQueue<object> _nfcQueue;
        private readonly WorkerQueue<object> _facePreviewQueue;
        private readonly WorkerQueue<object> _fingerPreviewQueue;
        private readonly WorkerQueue<object> _miscQueue;

        public int TerminalGeneration => _terminalGeneration;
        public bool SwitchingTerminal => _switchingTerminal;

        // Accessors for each queue
        public WorkerQueue<object> SwitchQueue => _switchQueue;
        public WorkerQueue<object> FaceCaptureQueue => _faceCapQueue;
        public WorkerQueue<object> FingerprintCaptureQueue => _fingerCapQueue;
        public WorkerQueue<object> OcrQueue => _ocrQueue;
        public WorkerQueue<object> NfcQueue => _nfcQueue;
        public WorkerQueue<object> FacePreviewQueue => _facePreviewQueue;
        public WorkerQueue<object> FingerprintPreviewQueue => _fingerPreviewQueue;
        public WorkerQueue<object> MiscQueue => _miscQueue;

        public QueueManager()
        {
            // Switch queue: max 1, highest priority
            _switchQueue = new WorkerQueue<object>("切换终端", 1, ExecuteSwitch, false, 30000);

            // Capture queues: max 2 (one executing + one pending), replace old pending
            _faceCapQueue = new WorkerQueue<object>("人脸抓拍", 2, ExecuteFaceCapture, true, 15000);
            _fingerCapQueue = new WorkerQueue<object>("指纹抓拍", 2, ExecuteFingerprintCapture, true, 15000);

            // Async queues: max 2, replace old pending
            _ocrQueue = new WorkerQueue<object>("OCR识别", 2, ExecuteOcr, true, 20000);
            _nfcQueue = new WorkerQueue<object>("NFC读卡", 2, ExecuteNfc, true, 20000);

            // Preview queues: max 1, replace mode (new preview replaces old pending)
            _facePreviewQueue = new WorkerQueue<object>("人脸预览", 1, ExecuteFacePreview, true, 10000);
            _fingerPreviewQueue = new WorkerQueue<object>("指纹预览", 1, ExecuteFingerprintPreview, true, 10000);

            // Misc queue: process start/end, authorize
            _miscQueue = new WorkerQueue<object>("杂项任务", 3, ExecuteMisc, false, 15000);
        }

        /// <summary>
        /// Called when a terminal switch is requested.
        /// 1) Set switching flag immediately
        /// 2) Increment generation (old queued tasks will be discarded)
        /// 3) Enqueue switch task to switch worker
        /// </summary>
        public bool RequestSwitch(int terminalIndex, int currentGeneration)
        {
            _switchingTerminal = true;
            var newGen = Interlocked.Increment(ref _terminalGeneration);
            Logger.Info($"[终端切换] 设置切换中标志, 新世代={newGen}, 目标终端={terminalIndex}");

            var data = new SwitchRequest { TerminalIndex = terminalIndex, Generation = newGen };
            return _switchQueue.Enqueue(data, newGen);
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
        /// Check if a task with the given generation should be executed.
        /// If generation is stale (< current), task should be discarded.
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
                _ocrQueue.GetStats(),
                _nfcQueue.GetStats(),
                _facePreviewQueue.GetStats(),
                _fingerPreviewQueue.GetStats(),
                _miscQueue.GetStats()
            );
        }

        // ====== Worker handlers (to be wired by ProxyServer) ======

        public Action<SwitchRequest> SwitchHandler { get; set; }
        public Action<QueueTask<object>> FaceCaptureHandler { get; set; }
        public Action<QueueTask<object>> FingerprintCaptureHandler { get; set; }
        public Action<QueueTask<object>> OcrHandler { get; set; }
        public Action<QueueTask<object>> NfcHandler { get; set; }
        public Action<QueueTask<object>> FacePreviewHandler { get; set; }
        public Action<QueueTask<object>> FingerprintPreviewHandler { get; set; }
        public Action<QueueTask<object>> MiscHandler { get; set; }

        private void ExecuteSwitch(QueueTask<object> task)
        {
            var req = task.Data as SwitchRequest;
            if (req != null && SwitchHandler != null)
                SwitchHandler(req);
        }

        private void ExecuteFaceCapture(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[人脸抓拍] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            FaceCaptureHandler?.Invoke(task);
        }

        private void ExecuteFingerprintCapture(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[指纹抓拍] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            FingerprintCaptureHandler?.Invoke(task);
        }

        private void ExecuteOcr(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[OCR] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            OcrHandler?.Invoke(task);
        }

        private void ExecuteNfc(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[NFC] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            NfcHandler?.Invoke(task);
        }

        private void ExecuteFacePreview(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[人脸预览] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            FacePreviewHandler?.Invoke(task);
        }

        private void ExecuteFingerprintPreview(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[指纹预览] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            FingerprintPreviewHandler?.Invoke(task);
        }

        private void ExecuteMisc(QueueTask<object> task)
        {
            if (!IsGenerationValid(task.Generation))
            {
                Logger.Warn($"[杂项] 世代 {task.Generation} < {_terminalGeneration}, 已丢弃");
                return;
            }
            MiscHandler?.Invoke(task);
        }

        public void Dispose()
        {
            _switchQueue?.Dispose();
            _faceCapQueue?.Dispose();
            _fingerCapQueue?.Dispose();
            _ocrQueue?.Dispose();
            _nfcQueue?.Dispose();
            _facePreviewQueue?.Dispose();
            _fingerPreviewQueue?.Dispose();
            _miscQueue?.Dispose();
        }
    }

    public class SwitchRequest
    {
        public int TerminalIndex { get; set; }
        public int Generation { get; set; }
    }
}
