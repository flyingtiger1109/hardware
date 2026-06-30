using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// Starts and tracks bounded background work for graceful shutdown drain.
    /// Work is admitted before Task.Run is created, so the configured capacity
    /// is a real concurrency limit rather than only a tracking limit.
    /// </summary>
    public sealed class ActiveTasksTracker : IDisposable
    {
        private readonly int _maxConcurrent;
        private readonly int _defaultTimeoutMs;
        private readonly ConcurrentDictionary<long, TrackedTask> _tasks;
        private readonly SemaphoreSlim _slots;
        private long _nextId;
        private long _totalRegistered;
        private long _totalCompleted;
        private long _totalTimedOut;
        private long _totalRejected;
        private int _disposed;

        public int ActiveCount => _tasks.Count;
        public long TotalRegistered => Interlocked.Read(ref _totalRegistered);
        public long TotalCompleted => Interlocked.Read(ref _totalCompleted);
        public long TotalTimedOut => Interlocked.Read(ref _totalTimedOut);
        public long TotalRejected => Interlocked.Read(ref _totalRejected);

        /// <param name="maxConcurrent">Max tracked tasks. Beyond this, Register returns false.</param>
        /// <param name="defaultTimeoutMs">Default per-task timeout for shutdown drain.</param>
        public ActiveTasksTracker(int maxConcurrent = 32, int defaultTimeoutMs = 30000)
        {
            if (maxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
            _maxConcurrent = maxConcurrent;
            _defaultTimeoutMs = defaultTimeoutMs;
            _tasks = new ConcurrentDictionary<long, TrackedTask>();
            _slots = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        /// <summary>
        /// Start bounded synchronous background work.
        /// </summary>
        public bool TryRun(Action action, string label)
        {
            if (action == null) return false;
            return TryRun(() =>
            {
                action();
                return Task.CompletedTask;
            }, label);
        }

        /// <summary>
        /// Start bounded asynchronous background work.
        /// Returns false without starting work when stopping or at capacity.
        /// </summary>
        public bool TryRun(Func<Task> work, string label)
        {
            if (work == null || Volatile.Read(ref _disposed) != 0)
                return false;

            if (!_slots.Wait(0))
            {
                Interlocked.Increment(ref _totalRejected);
                Logger.Warn($"[TaskTracker] 容量已满({_maxConcurrent})，拒绝启动: {label} (累计拒绝={TotalRejected})");
                return false;
            }

            if (Volatile.Read(ref _disposed) != 0)
            {
                _slots.Release();
                return false;
            }

            Task task;
            try
            {
                task = Task.Run(work);
            }
            catch
            {
                _slots.Release();
                throw;
            }

            var id = Interlocked.Increment(ref _nextId);
            var tracked = new TrackedTask
            {
                Id = id,
                Task = task,
                Label = label,
                RegisteredAt = DateTime.UtcNow
            };

            if (!_tasks.TryAdd(id, tracked))
            {
                Interlocked.Increment(ref _totalRejected);
                _slots.Release();
                return false;
            }

            Interlocked.Increment(ref _totalRegistered);

            // Auto-cleanup: when the task completes, remove from tracking.
            // Using ContinueWith to avoid blocking the task's own continuation.
            task.ContinueWith(t =>
            {
                if (_tasks.TryRemove(id, out _))
                    Interlocked.Increment(ref _totalCompleted);
                _slots.Release();
            }, TaskContinuationOptions.ExecuteSynchronously);

            return true;
        }

        /// <summary>
        /// Non-blocking cleanup: remove already-completed tasks from tracking.
        /// Useful for periodic maintenance without waiting.
        /// </summary>
        public int CleanupCompleted()
        {
            int removed = 0;
            foreach (var kv in _tasks)
            {
                if (kv.Value.Task.IsCompleted &&
                    _tasks.TryRemove(kv.Key, out _))
                {
                    Interlocked.Increment(ref _totalCompleted);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Wait for all tracked tasks to complete, up to the specified timeout.
        /// Used during graceful shutdown.
        /// </summary>
        public async Task WaitAllAsync(int timeoutMs)
        {
            if (_tasks.Count == 0) return;
            if (timeoutMs <= 0) timeoutMs = _defaultTimeoutMs;

            var snapshot = new System.Collections.Generic.List<Task>(_tasks.Count);
            foreach (var kv in _tasks)
                snapshot.Add(kv.Value.Task);
            if (snapshot.Count == 0) return;

            var allTask = Task.WhenAll(snapshot);
            var completed = await Task.WhenAny(allTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completed != allTask)
            {
                // Timeout reached: count still-active tasks
                int stillActive = 0;
                foreach (var kv in _tasks)
                {
                    if (!kv.Value.Task.IsCompleted)
                    {
                        Interlocked.Increment(ref _totalTimedOut);
                        stillActive++;
                    }
                }
                if (stillActive > 0)
                    Logger.Warn($"[TaskTracker] WaitAllAsync超时({timeoutMs}ms): {stillActive}个Task仍活跃");
            }
        }

        /// <summary>
        /// Get a snapshot of statistics for telemetry.
        /// </summary>
        public string GetStats()
        {
            return $"ActiveTasksTracker: 活跃={ActiveCount}/{_maxConcurrent} 已注册={TotalRegistered} 已完成={TotalCompleted} 超时={TotalTimedOut} 拒绝={TotalRejected}";
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            // Don't cancel tasks — they should complete naturally.
            // Call WaitAllAsync before Dispose for graceful drain.
        }

        private sealed class TrackedTask
        {
            public long Id;
            public Task Task;
            public string Label;
            public DateTime RegisteredAt;
        }
    }
}
