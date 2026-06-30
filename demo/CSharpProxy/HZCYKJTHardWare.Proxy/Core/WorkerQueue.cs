using System;
using System.Collections.Generic;
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
    /// - maxLength is the total outstanding limit, including the executing task
    /// - Replace mode keeps the executing/next task and replaces only a pending task
    /// - With maxLength=2, the model is one executing plus one latest pending task
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
        private int _stopLogged;

        // Queue using Monitor.Wait/Pulse for blocking consumer (no CPU spin)
        private QueueTask<T>[] _buffer;
        private int _head;
        private int _tail;
        private int _count;
        private bool _executing;

        // Statistics
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

        public WorkerQueue(string name, int maxLength, Action<QueueTask<T>> handler,
            bool replaceOld = false, int timeoutMs = 15000)
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

            _worker = new Thread(Run)
            {
                Name = name + "_Worker",
                IsBackground = true
            };
            _worker.Start();
            Logger.Debug($"[队列] {_name} 已启动, 最大长度={_maxLength}, 替换模式={_replaceOld}");
        }

        /// <summary>
        /// Enqueue a task. Returns false if all outstanding slots are occupied and
        /// there is no pending task that may be replaced. In replace mode, a new
        /// request replaces the pending task but never interrupts the executing task.
        /// </summary>
        public bool Enqueue(T data, int generation)
        {
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
                        // If no task is executing yet, the head item is the protected
                        // next-to-run task. Replace the first item behind it instead.
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
            }

            // Complete outside the queue lock so a custom result sink cannot re-enter it.
            if (replacedTask != null)
            {
                TryCompleteTask(replacedTask, "queue_replaced");
                Logger.Info($"[队列] {_name} 新请求替换等待任务 (已替换={replacedCount})");
            }
            return true;
        }

        /// <summary>
        /// Worker loop: blocks on Monitor.Wait when idle, no CPU spin.
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
                        Monitor.Wait(_lock, 1000);  // Wake every 1s to check disposed
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
                var queueWaitMs = (DateTime.UtcNow - task.EnqueueTime).TotalMilliseconds;
                if (queueWaitMs > _timeoutMs)
                {
                    Interlocked.Increment(ref _timedOut);
                    Logger.Warn($"[队列] {_name} 任务排队已超时({_timeoutMs}ms), 已丢弃, 排队耗时={queueWaitMs:F0}ms");
                    TryCompleteTask(task, "timeout");
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
                    Logger.Warn($"[队列] {_name} worker 线程未能及时退出");
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
