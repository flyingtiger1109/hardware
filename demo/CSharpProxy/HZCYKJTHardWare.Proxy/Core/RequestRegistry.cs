using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// Composite key for request registry lookups. Uses struct to avoid
    /// string-concatenation collisions (previous "\n" delimiter was fragile).
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

        internal ProxyRequestContext(string requestId, string resourceType, string saveDir,
            string dllCallbackUrl, int generation, bool processFlow, TimeSpan lifetime)
        {
            RequestId = requestId;
            ResourceType = ProxyResourceTypes.Normalize(resourceType);
            SaveDir = saveDir ?? "";
            DllCallbackUrl = dllCallbackUrl ?? "";
            Generation = generation;
            IsProcessFlow = processFlow;
            CreatedAtUtc = DateTime.UtcNow;
            ExpiresAtUtc = CreatedAtUtc.Add(lifetime);
            _state = (int)ProxyRequestState.Created;
        }

        internal string RequestId { get; }
        internal string ResourceType { get; }
        internal string SaveDir { get; }
        internal string DllCallbackUrl { get; }
        internal int Generation { get; }
        internal bool IsProcessFlow { get; }
        internal DateTime CreatedAtUtc { get; }
        internal DateTime ExpiresAtUtc { get; }
        internal ProxyRequestState State => (ProxyRequestState)Volatile.Read(ref _state);

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

        internal void MarkTerminal(ProxyRequestState state)
        {
            Interlocked.Exchange(ref _state, (int)state);
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

        // Capacity configurable via AppConfig (read at construction time)
        private readonly int _maxActiveEntries;
        private int _registerRejectedCount;

        private readonly ConcurrentDictionary<CompositeKey, ProxyRequestContext> _active =
            new ConcurrentDictionary<CompositeKey, ProxyRequestContext>();
        private readonly ConcurrentDictionary<CompositeKey, CompletedRequestRecord> _completed =
            new ConcurrentDictionary<CompositeKey, CompletedRequestRecord>();

        private readonly System.Threading.Timer _pruneTimer;
        private int _pruneRunning;
        private int _lastPruneActiveRemoved;
        private int _lastPruneCompletedRemoved;

        internal int ActiveCount => _active.Count;
        internal int MaxActiveEntries => _maxActiveEntries;
        internal int RegisterRejectedCount => _registerRejectedCount;
        internal int LastPruneActiveRemoved => _lastPruneActiveRemoved;
        internal int LastPruneCompletedRemoved => _lastPruneCompletedRemoved;

        public RequestRegistry() : this(5000) { }

        internal RequestRegistry(int maxActiveEntries)
        {
            _maxActiveEntries = maxActiveEntries > 0 ? maxActiveEntries : 5000;
            _pruneTimer = new System.Threading.Timer(
                _ => PruneExpiredCallback(),
                null,
                PruneInterval,
                PruneInterval);
        }

        /// <summary>
        /// Register a new request. Returns null if the registry is at capacity
        /// (caller must handle rejection and return an error response).
        /// </summary>
        internal ProxyRequestContext Register(string requestId, string resourceType,
            string saveDir, string dllCallbackUrl, int generation, bool processFlow = false)
        {
            if (string.IsNullOrEmpty(requestId))
                throw new ArgumentException("request_id is required", nameof(requestId));

            // Capacity check — reject early if full
            if (_active.Count >= _maxActiveEntries)
            {
                Interlocked.Increment(ref _registerRejectedCount);
                Logger.Warn($"[Registry] 容量已满({_maxActiveEntries})，拒绝注册: request_id={requestId}, resource={resourceType} (累计拒绝={_registerRejectedCount})");
                return null;
            }

            var normalized = ProxyResourceTypes.Normalize(resourceType);
            var key = new CompositeKey(requestId, normalized);
            var context = new ProxyRequestContext(requestId, normalized, saveDir,
                dllCallbackUrl, generation, processFlow,
                processFlow ? ProcessLifetime : DefaultLifetime);
            _completed.TryRemove(key, out _);
            _active[key] = context;
            return context;
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
                if (item.Value.Generation < generation)
                    Finish(item.Value.RequestId, item.Value.ResourceType, ProxyRequestState.Cancelled);
            }
        }

        /// <summary>
        /// Timer callback: prune expired active entries and stale completed records.
        /// Thread-safe via Interlocked guard (single execution at a time).
        /// </summary>
        private void PruneExpiredCallback()
        {
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
                Logger.Warn($"[Registry] Timer清理异常: {ex.Message}");
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
                    Finish(item.Value.RequestId, item.Value.ResourceType, ProxyRequestState.TimedOut);
                    activeRemoved++;
                }
            }

            var completedCutoff = now.Subtract(CompletedLifetime).Ticks;
            var completedRemoved = 0;
            foreach (var item in _completed)
            {
                if (item.Value.CompletedAtTicks < completedCutoff &&
                    _completed.TryRemove(item.Key, out _))
                    completedRemoved++;
            }
            return (activeRemoved, completedRemoved);
        }

        internal IReadOnlyCollection<ProxyRequestContext> Snapshot()
        {
            return new List<ProxyRequestContext>(_active.Values);
        }

        private void Finish(string requestId, string resourceType, ProxyRequestState state)
        {
            var key = new CompositeKey(requestId, resourceType);
            if (_active.TryRemove(key, out var context))
                context.MarkTerminal(state);
            _completed[key] = new CompletedRequestRecord(state);
        }

        public void Dispose()
        {
            _pruneTimer?.Dispose();
        }
    }
}
