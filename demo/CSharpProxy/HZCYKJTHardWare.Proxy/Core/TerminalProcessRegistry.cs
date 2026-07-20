using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Core
{
    internal enum TerminalProcessState
    {
        Registering = 0,
        Confirmed = 1,
        Retained = 2,
        Stopped = 3
    }

    /// <summary>
    /// StartProcess 向终端声明的回调路由元数据。
    /// 此对象不作为本地回调准入开关，EndProcess 不会将其取消。
    /// 已被替代或未经确认的绑定会短暂保留，以便安全路由终端已经发出的回调。
    /// </summary>
    internal sealed class TerminalProcessSession
    {
        private int _state;
        private long _retainedAtUtcTicks;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        internal TerminalProcessSession(int terminalIndex, string terminalBaseUrl,
            string processRequestId, string saveDir, int routeGeneration)
        {
            TerminalIndex = terminalIndex;
            TerminalBaseUrl = terminalBaseUrl ?? "";
            ProcessRequestId = processRequestId ?? "";
            SaveDir = saveDir ?? "";
            RouteGeneration = routeGeneration;
            CreatedAtUtc = DateTime.UtcNow;
            _state = (int)TerminalProcessState.Registering;
        }

        internal int TerminalIndex { get; }
        internal string TerminalBaseUrl { get; }
        internal string ProcessRequestId { get; }
        internal string SaveDir { get; }
        internal int RouteGeneration { get; }
        internal DateTime CreatedAtUtc { get; }
        internal DateTime RetainedAtUtc
        {
            get
            {
                var ticks = Volatile.Read(ref _retainedAtUtcTicks);
                return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : CreatedAtUtc;
            }
        }
        internal TerminalProcessState State =>
            (TerminalProcessState)Volatile.Read(ref _state);
        internal bool IsRoutable => State != TerminalProcessState.Stopped;
        internal CancellationToken CancellationToken => _lifetimeCancellation.Token;

        internal bool TryConfirm()
        {
            return Interlocked.CompareExchange(ref _state,
                (int)TerminalProcessState.Confirmed,
                (int)TerminalProcessState.Registering) ==
                (int)TerminalProcessState.Registering;
        }

        internal bool TryRetain()
        {
            while (true)
            {
                var current = State;
                if (current == TerminalProcessState.Retained)
                {
                    Interlocked.CompareExchange(ref _retainedAtUtcTicks,
                        DateTime.UtcNow.Ticks, 0);
                    return true;
                }
                if (current == TerminalProcessState.Stopped)
                    return false;
                if (Interlocked.CompareExchange(ref _state,
                    (int)TerminalProcessState.Retained,
                    (int)current) == (int)current)
                {
                    Interlocked.Exchange(ref _retainedAtUtcTicks,
                        DateTime.UtcNow.Ticks);
                    return true;
                }
            }
        }

        internal void Stop()
        {
            if ((TerminalProcessState)Interlocked.Exchange(ref _state,
                (int)TerminalProcessState.Stopped) == TerminalProcessState.Stopped)
                return;
            try { _lifetimeCancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    internal sealed class ProcessStartRegistration
    {
        internal ProcessStartRegistration(TerminalProcessSession candidate)
        {
            Candidate = candidate;
        }

        internal TerminalProcessSession Candidate { get; }
    }

    /// <summary>
    /// 存储 StartProcess 创建的有界回调路由绑定。
    /// 是否发出回调以终端硬件状态为准；本注册表仅验证请求来源与路由，并对即时传输重试进行去重。
    /// </summary>
    internal sealed class TerminalProcessRegistry : IDisposable
    {
        private static readonly TimeSpan DuplicateEventWindow = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RetainedBindingWindow = TimeSpan.FromMinutes(10);
        private const int MaxRetainedBindings = 256;
        private const int MaxRecentEvents = 8192;

        private readonly object _sync = new object();
        private readonly Dictionary<int, TerminalProcessSession> _currentByTerminal =
            new Dictionary<int, TerminalProcessSession>();
        private readonly Dictionary<int, TerminalProcessSession> _registeringByTerminal =
            new Dictionary<int, TerminalProcessSession>();
        private readonly Dictionary<string, TerminalProcessSession> _byRequestId =
            new Dictionary<string, TerminalProcessSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _recentEvents =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Queue<KeyValuePair<string, long>> _recentEventOrder =
            new Queue<KeyValuePair<string, long>>();

        private long _deliverySequence;
        private int _disposed;

        internal int CurrentCount
        {
            get { lock (_sync) return _currentByTerminal.Count; }
        }

        internal int BindingCount
        {
            get { lock (_sync) return _byRequestId.Count; }
        }

        internal ProcessStartRegistration Prepare(int terminalIndex,
            string terminalBaseUrl, string processRequestId, string saveDir,
            int routeGeneration)
        {
            if (terminalIndex <= 0 || string.IsNullOrEmpty(processRequestId))
                return null;

            lock (_sync)
            {
                PruneRetainedBindingsLocked(DateTime.UtcNow);
                if (Volatile.Read(ref _disposed) != 0 ||
                    _registeringByTerminal.ContainsKey(terminalIndex) ||
                    _byRequestId.ContainsKey(processRequestId))
                    return null;

                var candidate = new TerminalProcessSession(terminalIndex,
                    terminalBaseUrl, processRequestId, saveDir, routeGeneration);
                _registeringByTerminal[terminalIndex] = candidate;
                _byRequestId[processRequestId] = candidate;
                return new ProcessStartRegistration(candidate);
            }
        }

        internal bool Commit(ProcessStartRegistration registration)
        {
            if (registration == null) return false;

            TerminalProcessSession previous = null;
            lock (_sync)
            {
                var candidate = registration.Candidate;
                if (!_registeringByTerminal.TryGetValue(candidate.TerminalIndex,
                    out var current) || !ReferenceEquals(current, candidate) ||
                    !candidate.TryConfirm())
                    return false;

                _registeringByTerminal.Remove(candidate.TerminalIndex);
                _currentByTerminal.TryGetValue(candidate.TerminalIndex, out previous);
                _currentByTerminal[candidate.TerminalIndex] = candidate;
                if (previous != null && !ReferenceEquals(previous, candidate))
                    previous.TryRetain();
                PruneRetainedBindingsLocked(DateTime.UtcNow);
            }
            return true;
        }

        /// <summary>
        /// StartProcess 响应未获确认时临时保留回调路由，因为终端仍可能已接受命令。
        /// 该绑定不会提升为当前保存目录或默认状态。
        /// </summary>
        internal void RetainUnconfirmed(ProcessStartRegistration registration)
        {
            if (registration == null) return;
            var candidate = registration.Candidate;
            lock (_sync)
            {
                if (_registeringByTerminal.TryGetValue(candidate.TerminalIndex,
                    out var current) && ReferenceEquals(current, candidate))
                    _registeringByTerminal.Remove(candidate.TerminalIndex);
                candidate.TryRetain();
                PruneRetainedBindingsLocked(DateTime.UtcNow);
            }
        }

        internal bool TryGetByRequestId(string processRequestId,
            out TerminalProcessSession session)
        {
            if (string.IsNullOrEmpty(processRequestId))
            {
                session = null;
                return false;
            }
            lock (_sync)
            {
                PruneRetainedBindingsLocked(DateTime.UtcNow);
                return _byRequestId.TryGetValue(processRequestId, out session) &&
                    session.IsRoutable;
            }
        }

        internal bool TryGetCurrent(int terminalIndex,
            out TerminalProcessSession session)
        {
            lock (_sync)
                return _currentByTerminal.TryGetValue(terminalIndex, out session) &&
                    session.State == TerminalProcessState.Confirmed;
        }

        internal string GetCurrentSaveDir(int terminalIndex)
        {
            return TryGetCurrent(terminalIndex, out var session)
                ? session.SaveDir
                : "";
        }

        /// <summary>
        /// 记录终端成功确认 EndProcess，仅更新 UI 和默认状态。
        /// 请求 ID 绑定仍可用于路由传输中的数据。
        /// </summary>
        internal void RecordEndAcknowledged(int terminalIndex)
        {
            TerminalProcessSession previous = null;
            lock (_sync)
            {
                if (_currentByTerminal.TryGetValue(terminalIndex, out previous))
                    _currentByTerminal.Remove(terminalIndex);
                previous?.TryRetain();
                PruneRetainedBindingsLocked(DateTime.UtcNow);
            }
        }

        internal bool TryReserveEvent(TerminalProcessSession session,
            string resourceType, string callbackBody, out string deliveryRequestId)
        {
            deliveryRequestId = "";
            if (session == null || !session.IsRoutable)
                return false;

            var bodyHash = ComputeSha256(callbackBody ?? "");
            var eventKey = session.ProcessRequestId + "\n" +
                ProxyResourceTypes.Normalize(resourceType) + "\n" + bodyHash;
            var now = DateTime.UtcNow;
            var nowTicks = now.Ticks;
            var cutoffTicks = nowTicks - DuplicateEventWindow.Ticks;
            long sequence;

            lock (_sync)
            {
                PruneRetainedBindingsLocked(now);
                if (!_byRequestId.TryGetValue(session.ProcessRequestId,
                    out var mapped) || !ReferenceEquals(mapped, session) ||
                    !session.IsRoutable)
                    return false;

                PruneRecentEventsLocked(cutoffTicks);
                if (_recentEvents.TryGetValue(eventKey, out var previousTicks) &&
                    previousTicks >= cutoffTicks)
                    return false;

                _recentEvents[eventKey] = nowTicks;
                _recentEventOrder.Enqueue(
                    new KeyValuePair<string, long>(eventKey, nowTicks));
                while (_recentEvents.Count > MaxRecentEvents &&
                    _recentEventOrder.Count > 0)
                {
                    var oldest = _recentEventOrder.Dequeue();
                    if (_recentEvents.TryGetValue(oldest.Key, out var storedTicks) &&
                        storedTicks == oldest.Value)
                        _recentEvents.Remove(oldest.Key);
                }
                sequence = Interlocked.Increment(ref _deliverySequence);
            }

            deliveryRequestId = session.ProcessRequestId + "_EVENT_" +
                sequence.ToString("D8");
            return true;
        }

        internal void ClearAll()
        {
            List<TerminalProcessSession> sessions;
            lock (_sync)
            {
                var unique = new HashSet<TerminalProcessSession>();
                foreach (var item in _byRequestId.Values) unique.Add(item);
                foreach (var item in _currentByTerminal.Values) unique.Add(item);
                foreach (var item in _registeringByTerminal.Values) unique.Add(item);
                sessions = new List<TerminalProcessSession>(unique);
                _currentByTerminal.Clear();
                _registeringByTerminal.Clear();
                _byRequestId.Clear();
                _recentEvents.Clear();
                _recentEventOrder.Clear();
            }

            foreach (var session in sessions)
                session.Stop();
        }

        private void PruneRetainedBindingsLocked(DateTime nowUtc)
        {
            var cutoff = nowUtc - RetainedBindingWindow;
            var removable = new List<TerminalProcessSession>();
            foreach (var session in _byRequestId.Values)
            {
                if (IsCurrentOrRegisteringLocked(session))
                    continue;
                if (session.RetainedAtUtc < cutoff)
                    removable.Add(session);
            }
            foreach (var session in removable)
                RemoveBindingLocked(session);

            if (_byRequestId.Count <= MaxRetainedBindings)
                return;

            removable.Clear();
            foreach (var session in _byRequestId.Values)
            {
                if (!IsCurrentOrRegisteringLocked(session))
                    removable.Add(session);
            }
            removable.Sort((left, right) =>
                left.RetainedAtUtc.CompareTo(right.RetainedAtUtc));
            foreach (var session in removable)
            {
                if (_byRequestId.Count <= MaxRetainedBindings)
                    break;
                RemoveBindingLocked(session);
            }
        }

        private bool IsCurrentOrRegisteringLocked(TerminalProcessSession session)
        {
            return (_currentByTerminal.TryGetValue(session.TerminalIndex,
                        out var current) && ReferenceEquals(current, session)) ||
                (_registeringByTerminal.TryGetValue(session.TerminalIndex,
                        out var registering) && ReferenceEquals(registering, session));
        }

        private void RemoveBindingLocked(TerminalProcessSession session)
        {
            if (_byRequestId.TryGetValue(session.ProcessRequestId, out var mapped) &&
                ReferenceEquals(mapped, session))
                _byRequestId.Remove(session.ProcessRequestId);
            session.Stop();
        }

        private void PruneRecentEventsLocked(long cutoffTicks)
        {
            while (_recentEventOrder.Count > 0)
            {
                var oldest = _recentEventOrder.Peek();
                if (oldest.Value >= cutoffTicks)
                    break;
                _recentEventOrder.Dequeue();
                if (_recentEvents.TryGetValue(oldest.Key, out var storedTicks) &&
                    storedTicks == oldest.Value)
                    _recentEvents.Remove(oldest.Key);
            }
        }

        private static string ComputeSha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            ClearAll();
        }
    }
}
