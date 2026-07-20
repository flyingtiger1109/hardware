using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// 启动并跟踪有界后台任务，支持正常关闭时排空。
    /// 在创建 Task.Run 前完成任务准入，因此配置容量是实际并发上限，而非仅用于跟踪。
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

        /// <param name="maxConcurrent">最大跟踪任务数，超过上限时 Register 返回 false。</param>
        /// <param name="defaultTimeoutMs">关闭排空时每个任务的默认超时时间。</param>
        public ActiveTasksTracker(int maxConcurrent = 32, int defaultTimeoutMs = 30000)
        {
            if (maxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
            _maxConcurrent = maxConcurrent;
            _defaultTimeoutMs = defaultTimeoutMs;
            _tasks = new ConcurrentDictionary<long, TrackedTask>();
            _slots = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        /// <summary>
        /// 启动有界同步后台任务。
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
        /// 启动有界异步后台任务。正在停止或达到容量上限时不启动任务并返回 false。
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

            // 任务完成后自动从跟踪集合中移除；使用 ContinueWith 避免阻塞任务自身的延续操作
            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    Logger.Error($"[TaskTracker] background task failed: {label}",
                        t.Exception.Flatten());
                if (_tasks.TryRemove(id, out _))
                    Interlocked.Increment(ref _totalCompleted);
                _slots.Release();
            }, TaskContinuationOptions.ExecuteSynchronously);

            return true;
        }

        /// <summary>
        /// 非阻塞清理：从跟踪集合中移除已完成任务，适用于无需等待的定期维护。
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
        /// 等待全部跟踪任务完成，最长等待到指定超时时间；用于正常关闭。
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
                // 达到超时时间后统计仍在运行的任务
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
        /// 获取遥测使用的统计快照。
        /// </summary>
        public string GetStats()
        {
            return $"ActiveTasksTracker: 活跃={ActiveCount}/{_maxConcurrent} 已注册={TotalRegistered} 已完成={TotalCompleted} 超时={TotalTimedOut} 拒绝={TotalRejected}";
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            // 不主动取消任务，由任务自然完成；正常排空应在 Dispose 前调用 WaitAllAsync
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
