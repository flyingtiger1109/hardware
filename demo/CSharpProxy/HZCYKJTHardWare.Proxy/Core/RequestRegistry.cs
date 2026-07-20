using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// 请求注册表使用的复合键。采用结构体可避免字符串拼接产生键冲突，旧版换行分隔方式可靠性不足。
    /// </summary>
    internal readonly struct CompositeKey : IEquatable<CompositeKey>
    {
        internal readonly string RequestId;
        internal readonly string ResourceType;

        internal CompositeKey(string requestId, string resourceType)
        {
            RequestId = requestId ?? "";
            ResourceType = ProxyResourceTypes.Normalize(resourceType);
        }

        public bool Equals(CompositeKey other)
        {
            return string.Equals(RequestId, other.RequestId, StringComparison.Ordinal) &&
                   string.Equals(ResourceType, other.ResourceType, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CompositeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RequestId != null ? RequestId.GetHashCode() : 0) * 397) ^
                       (ResourceType != null ? ResourceType.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return RequestId + "|" + ResourceType;
        }
    }

    internal static class ProxyResourceTypes
    {
        internal const string OcrDocument = "ocr_document";
        internal const string NfcCard = "nfc_card";
        internal const string IrisImage = "iris_image";
        internal const string Protocol = "protocol";

        internal static string Normalize(string resourceType)
        {
            var value = (resourceType ?? "").Trim().ToLowerInvariant();
            switch (value)
            {
                case "ocr": return OcrDocument;
                case "nfc": return NfcCard;
                case "iris": return IrisImage;
                case "authorization": return Protocol;
                default: return value;
            }
        }
    }

    internal enum ProxyRequestState
    {
        Created = 0,
        Queued = 1,
        Submitting = 2,
        Accepted = 3,
        CallbackReceived = 4,
        Completed = 5,
        Failed = 6,
        Cancelled = 7,
        TimedOut = 8
    }

    internal sealed class ProxyRequestContext
    {
        private int _state;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        internal ProxyRequestContext(string requestId, string resourceType, string saveDir,
            string dllCallbackUrl, int generation, bool processFlow, TimeSpan lifetime,
            int terminalIndex, string originalRequestBodyUtf8)
        {
            RequestId = requestId;
            ResourceType = ProxyResourceTypes.Normalize(resourceType);
            SaveDir = saveDir ?? "";
            DllCallbackUrl = dllCallbackUrl ?? "";
            OriginalRequestBodyUtf8 = originalRequestBodyUtf8 ?? "";
            Generation = generation;
            IsProcessFlow = processFlow;
            TerminalIndex = terminalIndex;
            CreatedAtUtc = DateTime.UtcNow;
            ExpiresAtUtc = CreatedAtUtc.Add(lifetime);
            _state = (int)ProxyRequestState.Created;
        }

        internal string RequestId { get; }
        internal string ResourceType { get; }
        internal string SaveDir { get; }
        internal string DllCallbackUrl { get; }
        internal string OriginalRequestBodyUtf8 { get; }
        internal int Generation { get; }
        internal bool IsProcessFlow { get; }
        internal int TerminalIndex { get; }
        internal DateTime CreatedAtUtc { get; }
        internal DateTime ExpiresAtUtc { get; }
        internal ProxyRequestState State => (ProxyRequestState)Volatile.Read(ref _state);
        internal CancellationToken CancellationToken => _lifetimeCancellation.Token;

        internal bool TryMarkQueued()
        {
            return TryAdvance(ProxyRequestState.Created, ProxyRequestState.Queued);
        }

        internal bool TryMarkSubmitting()
        {
            return TryAdvance(ProxyRequestState.Queued, ProxyRequestState.Submitting) ||
                TryAdvance(ProxyRequestState.Created, ProxyRequestState.Submitting);
        }

        internal bool TryMarkAccepted()
        {
            while (true)
            {
                var current = State;
                if (current == ProxyRequestState.Accepted ||
                    current == ProxyRequestState.CallbackReceived ||
                    current == ProxyRequestState.Completed)
                    return true;
                if (IsTerminal(current)) return false;
                if (Interlocked.CompareExchange(ref _state, (int)ProxyRequestState.Accepted,
                    (int)current) == (int)current)
                    return true;
            }
        }

        internal bool TryClaimCallback()
        {
            while (true)
            {
                var current = State;
                if (current == ProxyRequestState.CallbackReceived || IsTerminal(current))
                    return false;
                if (Interlocked.CompareExchange(ref _state, (int)ProxyRequestState.CallbackReceived,
                    (int)current) == (int)current)
                    return true;
            }
        }

        internal bool TryMarkTerminal(ProxyRequestState state)
        {
            while (true)
            {
                var current = State;
                if (IsTerminal(current))
                    return false;
                if (Interlocked.CompareExchange(ref _state, (int)state,
                    (int)current) != (int)current)
                    continue;

                try { _lifetimeCancellation.Cancel(); }
                catch (ObjectDisposedException) { }
                return true;
            }
        }

        private bool TryAdvance(ProxyRequestState from, ProxyRequestState to)
        {
            return Interlocked.CompareExchange(ref _state, (int)to, (int)from) == (int)from;
        }

        private static bool IsTerminal(ProxyRequestState state)
        {
            return state == ProxyRequestState.Completed ||
                state == ProxyRequestState.Failed ||
                state == ProxyRequestState.Cancelled ||
                state == ProxyRequestState.TimedOut;
        }
    }

    internal sealed class RequestRegistry : IDisposable
    {
        private sealed class CompletedRequestRecord
        {
            internal CompletedRequestRecord(ProxyRequestState state)
            {
                State = state;
                CompletedAtTicks = DateTime.UtcNow.Ticks;
            }

            internal ProxyRequestState State { get; }
            internal long CompletedAtTicks { get; }
        }

        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ProcessLifetime = TimeSpan.FromHours(8);
        private static readonly TimeSpan CompletedLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(30);
        private const int MaxCompletedEntries = 8192;

        // 容量由 AppConfig 配置，并在构造时读取
        private readonly int _maxActiveEntries;
        private int _registerRejectedCount;
        private int _duplicateRejectedCount;
        private int _disposed;
        private readonly object _mutationLock = new object();
        private readonly SemaphoreSlim _capacity;

        private readonly ConcurrentDictionary<CompositeKey, ProxyRequestContext> _active =
            new ConcurrentDictionary<CompositeKey, ProxyRequestContext>();
        private readonly ConcurrentDictionary<CompositeKey, CompletedRequestRecord> _completed =
            new ConcurrentDictionary<CompositeKey, CompletedRequestRecord>();
        private readonly Queue<CompositeKey> _completedOrder = new Queue<CompositeKey>();

        private readonly System.Threading.Timer _pruneTimer;
        private int _pruneRunning;
        private int _lastPruneActiveRemoved;
        private int _lastPruneCompletedRemoved;

        internal int ActiveCount => _active.Count;
        internal int CompletedCount => _completed.Count;
        internal int MaxActiveEntries => _maxActiveEntries;
        internal int RegisterRejectedCount => Volatile.Read(ref _registerRejectedCount);
        internal int DuplicateRejectedCount => Volatile.Read(ref _duplicateRejectedCount);
        internal int LastPruneActiveRemoved => _lastPruneActiveRemoved;
        internal int LastPruneCompletedRemoved => _lastPruneCompletedRemoved;

        public RequestRegistry() : this(5000) { }

        internal RequestRegistry(int maxActiveEntries)
        {
            _maxActiveEntries = maxActiveEntries > 0 ? maxActiveEntries : 5000;
            _capacity = new SemaphoreSlim(_maxActiveEntries, _maxActiveEntries);
            _pruneTimer = new System.Threading.Timer(
                _ => PruneExpiredCallback(),
                null,
                PruneInterval,
                PruneInterval);
        }

        /// <summary>
        /// 注册新请求。注册表达到容量上限时返回 null，调用方负责拒绝请求并返回错误响应。
        /// </summary>
        internal ProxyRequestContext Register(string requestId, string resourceType,
            string saveDir, string dllCallbackUrl, int generation,
            bool processFlow = false, int terminalIndex = 0,
            string originalRequestBodyUtf8 = "")
        {
            if (string.IsNullOrEmpty(requestId))
                throw new ArgumentException("request_id is required", nameof(requestId));

            var normalized = ProxyResourceTypes.Normalize(resourceType);
            var key = new CompositeKey(requestId, normalized);

            lock (_mutationLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return null;

                if (_active.ContainsKey(key) || _completed.ContainsKey(key))
                {
                    var rejected = Interlocked.Increment(ref _duplicateRejectedCount);
                    Logger.Warn($"[Registry] duplicate registration rejected: request_id={requestId}, resource={normalized}, total={rejected}");
                    return null;
                }

                if (!_capacity.Wait(0))
                {
                    var rejected = Interlocked.Increment(ref _registerRejectedCount);
                    Logger.Warn($"[Registry] capacity reached ({_maxActiveEntries}), request_id={requestId}, resource={normalized}, total={rejected}");
                    return null;
                }

                var context = new ProxyRequestContext(requestId, normalized, saveDir,
                    dllCallbackUrl, generation, processFlow,
                    processFlow ? ProcessLifetime : DefaultLifetime, terminalIndex,
                    originalRequestBodyUtf8);
                if (_active.TryAdd(key, context))
                    return context;

                _capacity.Release();
                Interlocked.Increment(ref _duplicateRejectedCount);
                return null;
            }
        }

        internal bool TryGet(string requestId, string resourceType, out ProxyRequestContext context)
        {
            return _active.TryGetValue(new CompositeKey(requestId, resourceType), out context);
        }

        internal bool TryMarkSubmitting(string requestId, string resourceType)
        {
            return TryGet(requestId, resourceType, out var context) && context.TryMarkSubmitting();
        }

        internal bool TryMarkAccepted(string requestId, string resourceType)
        {
            var key = new CompositeKey(requestId, resourceType);
            if (_active.TryGetValue(key, out var context))
                return context.TryMarkAccepted();
            return _completed.TryGetValue(key, out var completed) &&
                completed.State == ProxyRequestState.Completed;
        }

        internal bool TryClaimCallback(string requestId, string resourceType,
            out ProxyRequestContext context)
        {
            context = null;
            if (string.IsNullOrEmpty(requestId)) return false;
            if (!TryGet(requestId, resourceType, out var found)) return false;
            if (!found.TryClaimCallback()) return false;
            context = found;
            return true;
        }

        internal void Complete(string requestId, string resourceType)
        {
            Finish(requestId, resourceType, ProxyRequestState.Completed);
        }

        internal void Fail(string requestId, string resourceType, bool timedOut = false)
        {
            Finish(requestId, resourceType,
                timedOut ? ProxyRequestState.TimedOut : ProxyRequestState.Failed);
        }

        internal void CancelAll()
        {
            foreach (var item in _active)
                Finish(item.Value.RequestId, item.Value.ResourceType, ProxyRequestState.Cancelled);
        }

        internal void CancelOlderThan(int generation)
        {
            foreach (var item in _active)
            {
                // 旧版流程条目必须跨路由代次保留。新版条目存入 TerminalProcessRegistry；
                // 此保护分支用于保障滚动升级期间尚未结束的会话。
                if (!item.Value.IsProcessFlow && item.Value.Generation < generation)
                    Finish(item.Value.RequestId, item.Value.ResourceType, ProxyRequestState.Cancelled);
            }
        }

        /// <summary>
        /// 定时器回调函数：清理过期的活动条目和陈旧的完成记录。
        /// 通过 Interlocked 防止并发执行，同一时刻仅允许一个清理任务运行。
        /// </summary>
        private void PruneExpiredCallback()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            if (Interlocked.Exchange(ref _pruneRunning, 1) != 0)
                return; // Already running

            try
            {
                var result = PruneExpired();
                _lastPruneActiveRemoved = result.activeRemoved;
                _lastPruneCompletedRemoved = result.completedRemoved;

                if (result.activeRemoved > 0 || result.completedRemoved > 0)
                    Logger.Debug($"[Registry] Timer清理: 活跃移除={result.activeRemoved}, 已完成移除={result.completedRemoved}, 当前活跃={_active.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error("[Registry] Timer清理异常", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _pruneRunning, 0);
            }
        }

        internal (int activeRemoved, int completedRemoved) PruneExpired()
        {
            var now = DateTime.UtcNow;
            var activeRemoved = 0;
            foreach (var item in _active)
            {
                if (item.Value.ExpiresAtUtc <= now)
                {
                    if (Finish(item.Value.RequestId, item.Value.ResourceType,
                        ProxyRequestState.TimedOut))
                        activeRemoved++;
                }
            }

            var completedCutoff = now.Subtract(CompletedLifetime).Ticks;
            var completedRemoved = 0;
            lock (_mutationLock)
            {
                while (_completedOrder.Count > 0)
                {
                    var key = _completedOrder.Peek();
                    if (!_completed.TryGetValue(key, out var record))
                    {
                        _completedOrder.Dequeue();
                        continue;
                    }

                    // 完成记录在同一锁内按时间戳顺序入队，因此后续记录不会早于当前清理边界。
                    if (record.CompletedAtTicks >= completedCutoff)
                        break;

                    _completedOrder.Dequeue();
                    if (_completed.TryRemove(key, out _))
                        completedRemoved++;
                }
            }
            return (activeRemoved, completedRemoved);
        }

        internal IReadOnlyCollection<ProxyRequestContext> Snapshot()
        {
            return new List<ProxyRequestContext>(_active.Values);
        }

        private bool Finish(string requestId, string resourceType, ProxyRequestState state)
        {
            var key = new CompositeKey(requestId, resourceType);
            ProxyRequestContext context;
            lock (_mutationLock)
            {
                if (!_active.TryRemove(key, out context))
                    return false;

                _completed[key] = new CompletedRequestRecord(state);
                _completedOrder.Enqueue(key);
                while (_completed.Count > MaxCompletedEntries &&
                    _completedOrder.Count > 0)
                {
                    var oldest = _completedOrder.Dequeue();
                    _completed.TryRemove(oldest, out _);
                }
                _capacity.Release();
            }

            // 取消回调函数可能同步执行，不得在持有注册表变更锁时触发。
            context.TryMarkTerminal(state);
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            using (var stopped = new ManualResetEvent(false))
            {
                try
                {
                    if (_pruneTimer != null && _pruneTimer.Dispose(stopped))
                        stopped.WaitOne(2000);
                }
                catch (Exception ex)
                {
                    Logger.Error("[Registry] timer dispose failed", ex);
                }
            }

            CancelAll();
        }
    }
}
