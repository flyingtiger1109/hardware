using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// 带终端切换批次信息的任务项。
    /// </summary>
    public class QueueTask<T>
    {
        public T Data { get; set; }
        public int Generation { get; set; }  // 入队时的终端切换批次
        public DateTime EnqueueTime { get; set; }
    }

    public interface IQueueResultSink
    {
        bool IsQueueResultCompleted { get; }
        void TrySetQueueResult(string result);
    }

    /// <summary>
    /// 使用单个专用线程的定长工作队列。
    /// - maxLength 表示未完成任务总上限，包含正在执行的任务
    /// - Replace 模式保留正在执行或即将执行的任务，仅替换后续等待任务
    /// - maxLength=2 时，队列模型为 1 个任务执行并保留 1 个最新待执行任务
    /// - 通过终端切换批次过滤旧请求
    /// - 统计入队、丢弃、完成和超时数量
    /// </summary>
    public class WorkerQueue<T> : IDisposable
    {
        private readonly string _name;
        private readonly int _maxLength;
        private readonly bool _replaceOld;  // true = preview mode: replace old pending task
        private readonly Action<QueueTask<T>> _handler;
        private readonly int _timeoutMs;
        private Thread _worker;
        private readonly bool _enabled;
        private readonly object _lock = new object();
        private volatile bool _disposed;
        private int _stopLogged;

        // 使用 Monitor.Wait/Pulse 阻塞消费线程，避免空闲轮询占用 CPU
        private QueueTask<T>[] _buffer;
        private int _head;
        private int _tail;
        private int _count;
        private bool _executing;

        // 队列统计
        private long _enqueued;
        private long _dropped;
        private long _completed;
        private long _timedOut;
        private long _replaced;

        public string Name => _name;
        public int Count { get { lock (_lock) return _count + (_executing ? 1 : 0); } }
        public int PendingCount { get { lock (_lock) return _count; } }
        public bool IsExecuting { get { lock (_lock) return _executing; } }
        public long Enqueued => Interlocked.Read(ref _enqueued);
        public long Dropped => Interlocked.Read(ref _dropped);
        public long Completed => Interlocked.Read(ref _completed);
        public long TimedOut => Interlocked.Read(ref _timedOut);
        public long Replaced => Interlocked.Read(ref _replaced);
        internal bool WorkerStarted => _worker != null;

        public WorkerQueue(string name, int maxLength, Action<QueueTask<T>> handler,
            bool replaceOld = false, int timeoutMs = 15000, bool enabled = true)
        {
            if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _name = name;
            _maxLength = maxLength;
            _replaceOld = replaceOld;
            _handler = handler;
            _timeoutMs = timeoutMs;
            _buffer = new QueueTask<T>[maxLength];
            _head = 0;
            _tail = 0;
            _count = 0;

            _enabled = enabled;
            if (!_enabled)
            {
                Logger.Info($"[硬件检测] 队列已禁用：{_name}，设备模式(DeviceMode)=" +
                    (int)DeviceCapabilityManager.Instance.Mode);
                return;
            }

            _worker = new Thread(Run)
            {
                Name = name + "_Worker",
                IsBackground = true
            };
            _worker.Start();
            Logger.Debug($"[队列] {_name} 已启动，最大长度={_maxLength}，替换模式={_replaceOld}");
        }

        /// <summary>
        /// 将任务加入队列。全部未完成名额均被占用且不存在可替换的等待任务时返回 false。
        /// Replace 模式下，新请求仅替换等待任务，不中断正在执行的任务。
        /// </summary>
        public bool Enqueue(T data, int generation)
        {
            if (!_enabled) return false;
            QueueTask<T> replacedTask = null;
            long replacedCount = 0;

            lock (_lock)
            {
                if (_disposed) return false;

                var task = new QueueTask<T>
                {
                    Data = data,
                    Generation = generation,
                    EnqueueTime = DateTime.UtcNow
                };

                Interlocked.Increment(ref _enqueued);

                var outstanding = _count + (_executing ? 1 : 0);
                if (outstanding >= _maxLength)
                {
                    if (_replaceOld && _count > 0)
                    {
                        // 尚无任务执行时，队首为受保护的下一执行任务，应改为替换其后的第一个任务。
                        var replaceIndex = _executing || _count == 1
                            ? _head
                            : (_head + 1) % _buffer.Length;
                        replacedTask = _buffer[replaceIndex];
                        _buffer[replaceIndex] = task;
                        replacedCount = Interlocked.Increment(ref _replaced);
                    }
                    else
                    {
                        Interlocked.Increment(ref _dropped);
                        Logger.Warn($"[队列] {_name} 队列已满，丢弃新任务（已丢弃={_dropped}）");
                        return false;
                    }
                }
                else
                {
                    _buffer[_tail] = task;
                    _tail = (_tail + 1) % _buffer.Length;
                    _count++;
                }

                Monitor.Pulse(_lock);  // Wake up worker
            }

            // 在队列锁外完成结果通知，防止自定义结果接收器重入队列锁。
            if (replacedTask != null)
            {
                TryCompleteTask(replacedTask, "queue_replaced");
                Logger.Info($"[队列] {_name} 新请求替换等待任务（已替换={replacedCount}）");
            }
            return true;
        }

        /// <summary>
        /// 工作线程循环：空闲时通过 Monitor.Wait 阻塞，不进行 CPU 空转。
        /// </summary>
        private void Run()
        {
            while (true)
            {
                QueueTask<T> task = null;
                lock (_lock)
                {
                    while (_count == 0 && !_disposed)
                    {
                Monitor.Wait(_lock, 1000);  // 每秒唤醒一次以检查释放状态
                    }
                    if (_disposed) break;
                    if (_count > 0)
                    {
                        task = _buffer[_head];
                        _buffer[_head] = null;
                        _head = (_head + 1) % _buffer.Length;
                        _count--;
                        _executing = true;
                    }
                }

                try
                {
                    if (task != null)
                        ExecuteWithTiming(task);
                }
                finally
                {
                    if (task != null)
                    {
                        lock (_lock)
                        {
                            _executing = false;
                            Monitor.PulseAll(_lock);
                        }
                    }
                }
            }
        }

        private void ExecuteWithTiming(QueueTask<T> task)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // HTTP 等待方可能在任务排队期间超时；调用方已收到队列终态后，不再启动硬件操作。
                var resultSink = task.Data as IQueueResultSink;
                if (resultSink != null && resultSink.IsQueueResultCompleted)
                {
                    Logger.Debug($"[队列] {_name} 已跳过已完成的等待任务");
                    return;
                }

                var queueWaitMs = (DateTime.UtcNow - task.EnqueueTime).TotalMilliseconds;
                if (queueWaitMs > _timeoutMs)
                {
                    Interlocked.Increment(ref _timedOut);
                    Logger.Warn($"[队列] {_name} 任务排队已超时（{_timeoutMs}ms），已丢弃，排队耗时={queueWaitMs:F0}ms");
                    TryCompleteTask(task, "timeout");
                    return;
                }

                // 队列已持有专用工作线程。直接执行处理函数可避免超时处理函数持续运行时遗留 ThreadPool 任务。
                _handler(task);
                Interlocked.Increment(ref _completed);
                sw.Stop();

                if (sw.ElapsedMilliseconds > 500 || queueWaitMs > 200)
                    Logger.Info($"[队列] {_name} 任务完成，执行耗时={sw.ElapsedMilliseconds}ms，排队耗时={queueWaitMs:F0}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[队列] {_name} 任务异常：{ex.Message}，耗时={sw.ElapsedMilliseconds}ms");
                TryCompleteTask(task, "failed");
                Interlocked.Increment(ref _completed);  // Count as completed (handled exception)
            }
        }

        internal static void TryCompleteTask(QueueTask<T> task, string code)
        {
            if (task == null)
                return;

            var result = "{\"error\":true,\"code\":\"" + code + "\"}";
            var data = task.Data;
            if (data is TaskCompletionSource<string> tcs)
                tcs.TrySetResult(result);
            else if (data is IQueueResultSink sink)
                sink.TrySetQueueResult(result);
        }

        public string GetStats()
        {
            if (!_enabled) return $"{_name}：已禁用";
            return $"{_name}: 当前={Count}/{_maxLength} 入队={Enqueued} 完成={Completed} 丢弃={Dropped} 替换={Replaced} 超时={TimedOut}";
        }

        internal void RequestStop()
        {
            List<QueueTask<T>> pendingTasks = null;
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                pendingTasks = new List<QueueTask<T>>(_count);
                while (_count > 0)
                {
                    var task = _buffer[_head];
                    _buffer[_head] = null;
                    _head = (_head + 1) % _buffer.Length;
                    _count--;
                    if (task != null) pendingTasks.Add(task);
                }
                _tail = _head;
                Monitor.PulseAll(_lock);
            }

            foreach (var task in pendingTasks)
                TryCompleteTask(task, "service_stopping");
        }

        internal bool WaitForStop(int timeoutMs)
        {
            var worker = _worker;
            if (worker != null && worker.IsAlive)
            {
                worker.Join(Math.Max(0, timeoutMs));
                if (worker.IsAlive)
                {
                    Logger.Warn($"[队列] {_name} 工作线程未能及时退出");
                    return false;
                }
            }
            _worker = null;
            _buffer = null;
            if (Interlocked.Exchange(ref _stopLogged, 1) == 0)
                Logger.Info($"[队列] {_name} 已停止 " + GetStats());
            return true;
        }

        public void Dispose()
        {
            RequestStop();
            WaitForStop(3000);
        }
    }
}
