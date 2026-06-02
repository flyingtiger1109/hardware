using System;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// A task item with terminal switch batch tracking.
    /// </summary>
    public class QueueTask<T>
    {
        public T Data { get; set; }
        public int Generation { get; set; }  // Terminal switch batch when enqueued
        public DateTime EnqueueTime { get; set; }
    }

    public interface IQueueResultSink
    {
        void TrySetQueueResult(string result);
    }

    /// <summary>
    /// Fixed-size worker queue with one dedicated thread.
    /// - Worker blocks on Take() when idle (no CPU spin)
    /// - Queue full → immediately returns (no waiting)
    /// - Preview queues use "replace" mode (new request replaces pending old one)
    /// - Terminal switch batch tracking for filtering old requests
    /// - Statistics: enqueued, dropped, completed, timed out
    /// </summary>
    public class WorkerQueue<T> : IDisposable
    {
        private readonly string _name;
        private readonly int _maxLength;
        private readonly bool _replaceOld;  // true = preview mode: replace old pending task
        private readonly Action<QueueTask<T>> _handler;
        private readonly int _timeoutMs;
        private Thread _worker;
        private readonly object _lock = new object();
        private volatile bool _disposed;

        // Queue using Monitor.Wait/Pulse for blocking consumer (no CPU spin)
        private QueueTask<T>[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        // Statistics
        private long _enqueued;
        private long _dropped;
        private long _completed;
        private long _timedOut;
        private long _replaced;

        public string Name => _name;
        public int Count { get { lock (_lock) return _count; } }
        public long Enqueued => Interlocked.Read(ref _enqueued);
        public long Dropped => Interlocked.Read(ref _dropped);
        public long Completed => Interlocked.Read(ref _completed);
        public long TimedOut => Interlocked.Read(ref _timedOut);
        public long Replaced => Interlocked.Read(ref _replaced);

        public WorkerQueue(string name, int maxLength, Action<QueueTask<T>> handler,
            bool replaceOld = false, int timeoutMs = 15000)
        {
            _name = name;
            _maxLength = maxLength;
            _replaceOld = replaceOld;
            _handler = handler;
            _timeoutMs = timeoutMs;
            _buffer = new QueueTask<T>[maxLength + 1]; // +1 for sentinel
            _head = 0;
            _tail = 0;
            _count = 0;

            _worker = new Thread(Run)
            {
                Name = name + "_Worker",
                IsBackground = true
            };
            _worker.Start();
            Logger.Info($"[队列] {_name} 已启动, 最大长度={_maxLength}, 替换模式={_replaceOld}");
        }

        /// <summary>
        /// Enqueue a task. Returns false if queue is full (task dropped).
        /// In replace mode, if queue is full, replaces the oldest pending task.
        /// </summary>
        public bool Enqueue(T data, int generation)
        {
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

                if (_count >= _maxLength)
                {
                    if (_replaceOld)
                    {
                        // Replace mode: discard oldest pending, immediately fail its waiter
                        var oldTask = _buffer[_head];
                        _buffer[_head] = task;
                        _head = (_head + 1) % _buffer.Length;
                        Interlocked.Increment(ref _replaced);

                        // Immediately complete the replaced task's TCS so its HTTP handler returns fast
                        // (otherwise it would wait full timeout, holding DLL's socket)
                        TryCompleteDroppedTask(oldTask);

                        Logger.Info($"[队列] {_name} 队列满, 新请求替换旧排队任务 (已替换={_replaced})");
                    }
                    else
                    {
                        Interlocked.Increment(ref _dropped);
                        Logger.Warn($"[队列] {_name} 队列满, 丢弃新任务 (已丢弃={_dropped})");
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
                return true;
            }
        }

        /// <summary>
        /// Worker loop: blocks on Monitor.Wait when idle, no CPU spin.
        /// </summary>
        private void Run()
        {
            var cts = new CancellationTokenSource();
            while (!_disposed)
            {
                QueueTask<T> task = null;
                lock (_lock)
                {
                    while (_count == 0 && !_disposed)
                    {
                        Monitor.Wait(_lock, 1000);  // Wake every 1s to check disposed
                    }
                    if (_disposed) break;
                    if (_count > 0)
                    {
                        task = _buffer[_head];
                        _buffer[_head] = null;
                        _head = (_head + 1) % _buffer.Length;
                        _count--;
                    }
                }

                if (task != null)
                    ExecuteWithTiming(task);
            }
            cts.Dispose();
        }

        private void ExecuteWithTiming(QueueTask<T> task)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var queueWaitMs = (DateTime.UtcNow - task.EnqueueTime).TotalMilliseconds;
                if (queueWaitMs > _timeoutMs)
                {
                    Interlocked.Increment(ref _timedOut);
                    Logger.Warn($"[队列] {_name} 任务排队已超时({_timeoutMs}ms), 已丢弃, 排队耗时={queueWaitMs:F0}ms");
                    TryCompleteDroppedTask(task);
                    return;
                }

                // The queue already owns a dedicated worker thread. Running the handler
                // directly avoids leaking ThreadPool tasks when a timed-out handler keeps running.
                _handler(task);
                Interlocked.Increment(ref _completed);
                sw.Stop();

                if (sw.ElapsedMilliseconds > 500 || queueWaitMs > 200)
                    Logger.Info($"[队列] {_name} 任务完成, 执行耗时={sw.ElapsedMilliseconds}ms, 排队耗时={queueWaitMs:F0}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[队列] {_name} 任务异常: {ex.Message}, 耗时={sw.ElapsedMilliseconds}ms");
                Interlocked.Increment(ref _completed);  // Count as completed (handled exception)
            }
        }

        private static void TryCompleteDroppedTask(QueueTask<T> task)
        {
            if (task == null)
                return;

            var data = task.Data;
            if (data is TaskCompletionSource<string> tcs)
                tcs.TrySetResult("{\"error\":true,\"code\":\"timeout\"}");
            else if (data is IQueueResultSink sink)
                sink.TrySetQueueResult("{\"error\":true,\"code\":\"timeout\"}");
        }

        public string GetStats()
        {
            return $"{_name}: 当前={Count}/{_maxLength} 入队={Enqueued} 完成={Completed} 丢弃={Dropped} 替换={Replaced} 超时={TimedOut}";
        }

        public void Dispose()
        {
            _disposed = true;
            lock (_lock)
            {
                Monitor.PulseAll(_lock);
            }
            if (_worker != null && _worker.IsAlive)
            {
                _worker.Join(3000);
                if (_worker.IsAlive)
                    Logger.Warn($"[队列] {_name} worker 线程未能及时退出");
            }
            _worker = null;
            _buffer = null;
            Logger.Info($"[队列] {_name} 已停止 " + GetStats());
        }
    }
}
