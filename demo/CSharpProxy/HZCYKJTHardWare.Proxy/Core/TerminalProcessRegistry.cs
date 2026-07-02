using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HZCYKJTHardWare.Proxy.Core
{
    internal enum TerminalProcessState
    {
        Starting = 0,
        Active = 1,
        Stopped = 2,
        Failed = 3
    }

    /// <summary>
    /// Persistent process subscription owned by one hardware terminal. Unlike a
    /// one-shot request, one process can produce multiple OCR/NFC callbacks.
    /// </summary>
    internal sealed class TerminalProcessSession
    {
        private int _state;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _activation =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TerminalProcessSession(int terminalIndex, string terminalBaseUrl,
            string processRequestId, string saveDir, int routeGeneration)
        {
            TerminalIndex = terminalIndex;
            TerminalBaseUrl = terminalBaseUrl ?? "";
            ProcessRequestId = processRequestId ?? "";
            SaveDir = saveDir ?? "";
            RouteGeneration = routeGeneration;
            CreatedAtUtc = DateTime.UtcNow;
            _state = (int)TerminalProcessState.Starting;
        }

        internal int TerminalIndex { get; }
        internal string TerminalBaseUrl { get; }
        internal string ProcessRequestId { get; }
        internal string SaveDir { get; }
        internal int RouteGeneration { get; }
        internal DateTime CreatedAtUtc { get; }
        internal TerminalProcessState State =>
            (TerminalProcessState)Volatile.Read(ref _state);
        internal CancellationToken CancellationToken => _lifetimeCancellation.Token;

        internal bool TryActivate()
        {
            if (Interlocked.CompareExchange(ref _state,
                (int)TerminalProcessState.Active,
                (int)TerminalProcessState.Starting) !=
                (int)TerminalProcessState.Starting)
                return State == TerminalProcessState.Active;

            _activation.TrySetResult(true);
            return true;
        }

        internal async Task<bool> WaitUntilActiveAsync(int timeoutMs)
        {
            var state = State;
            if (state == TerminalProcessState.Active) return true;
            if (state != TerminalProcessState.Starting) return false;

            var completed = await Task.WhenAny(_activation.Task,
                Task.Delay(Math.Max(1, timeoutMs))).ConfigureAwait(false);
            return completed == _activation.Task &&
                await _activation.Task.ConfigureAwait(false);
        }

        internal void Stop(bool failed)
        {
            var target = failed ? TerminalProcessState.Failed : TerminalProcessState.Stopped;
            var previous = (TerminalProcessState)Interlocked.Exchange(
                ref _state, (int)target);
            if (previous == TerminalProcessState.Stopped ||
                previous == TerminalProcessState.Failed)
                return;

            _activation.TrySetResult(false);
            try { _lifetimeCancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    internal sealed class ProcessStartRegistration
    {
        internal ProcessStartRegistration(TerminalProcessSession candidate,
            TerminalProcessSession previous)
        {
            Candidate = candidate;
            Previous = previous;
        }

        internal TerminalProcessSession Candidate { get; }
        internal TerminalProcessSession Previous { get; }
    }

    /// <summary>
    /// Stores long-lived StartProcess bindings separately from RequestRegistry.
    /// Sessions survive terminal switches and are removed only by replacement,
    /// EndProcess, shutdown or an explicit start failure.
    /// </summary>
    internal sealed class TerminalProcessRegistry : IDisposable
    {
        private static readonly TimeSpan DuplicateEventWindow = TimeSpan.FromSeconds(2);
        private const int MaxRecentEvents = 8192;

        private readonly object _sync = new object();
        private readonly Dictionary<int, TerminalProcessSession> _activeByTerminal =
            new Dictionary<int, TerminalProcessSession>();
        private readonly Dictionary<int, TerminalProcessSession> _startingByTerminal =
            new Dictionary<int, TerminalProcessSession>();
        private readonly Dictionary<string, TerminalProcessSession> _byRequestId =
            new Dictionary<string, TerminalProcessSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _recentEvents =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Queue<KeyValuePair<string, long>> _recentEventOrder =
            new Queue<KeyValuePair<string, long>>();

        private long _deliverySequence;
        private int _disposed;

        internal int ActiveCount
        {
            get { lock (_sync) return _activeByTerminal.Count; }
        }

        internal ProcessStartRegistration Prepare(int terminalIndex,
            string terminalBaseUrl, string processRequestId, string saveDir,
            int routeGeneration)
        {
            if (terminalIndex <= 0 || string.IsNullOrEmpty(processRequestId))
                return null;

            lock (_sync)
            {
                if (Volatile.Read(ref _disposed) != 0 ||
                    _startingByTerminal.ContainsKey(terminalIndex) ||
                    _byRequestId.ContainsKey(processRequestId))
                    return null;

                _activeByTerminal.TryGetValue(terminalIndex, out var previous);
                var candidate = new TerminalProcessSession(terminalIndex,
                    terminalBaseUrl, processRequestId, saveDir, routeGeneration);
                _startingByTerminal[terminalIndex] = candidate;
                _byRequestId[processRequestId] = candidate;
                return new ProcessStartRegistration(candidate, previous);
            }
        }

        internal bool Commit(ProcessStartRegistration registration)
        {
            if (registration == null) return false;

            TerminalProcessSession previous = null;
            lock (_sync)
            {
                var candidate = registration.Candidate;
                if (!_startingByTerminal.TryGetValue(candidate.TerminalIndex,
                    out var current) || !ReferenceEquals(current, candidate))
                    return false;

                _startingByTerminal.Remove(candidate.TerminalIndex);
                _activeByTerminal.TryGetValue(candidate.TerminalIndex, out previous);
                _activeByTerminal[candidate.TerminalIndex] = candidate;
                if (previous != null && !ReferenceEquals(previous, candidate))
                    _byRequestId.Remove(previous.ProcessRequestId);
            }

            var activated = registration.Candidate.TryActivate();
            if (previous != null && !ReferenceEquals(previous, registration.Candidate))
                previous.Stop(false);
            return activated;
        }

        internal void Rollback(ProcessStartRegistration registration)
        {
            if (registration == null) return;
            var candidate = registration.Candidate;
            lock (_sync)
            {
                if (_startingByTerminal.TryGetValue(candidate.TerminalIndex,
                    out var current) && ReferenceEquals(current, candidate))
                    _startingByTerminal.Remove(candidate.TerminalIndex);

                if (_byRequestId.TryGetValue(candidate.ProcessRequestId,
                    out var mapped) && ReferenceEquals(mapped, candidate))
                    _byRequestId.Remove(candidate.ProcessRequestId);
            }
            candidate.Stop(true);
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
                return _byRequestId.TryGetValue(processRequestId, out session);
        }

        internal bool TryGetActive(int terminalIndex,
            out TerminalProcessSession session)
        {
            lock (_sync)
                return _activeByTerminal.TryGetValue(terminalIndex, out session) &&
                    session.State == TerminalProcessState.Active;
        }

        internal string GetActiveSaveDir(int terminalIndex)
        {
            return TryGetActive(terminalIndex, out var session)
                ? session.SaveDir
                : "";
        }

        internal bool IsActive(int terminalIndex)
        {
            return TryGetActive(terminalIndex, out _);
        }

        /// <summary>
        /// Reserve one persistent process event. Exact transport retries within
        /// two seconds are suppressed, while later legitimate scans remain valid.
        /// A unique delivery request id prevents the DLL's one-shot de-dup table
        /// from collapsing multiple events produced by one process id.
        /// </summary>
        internal bool TryReserveEvent(TerminalProcessSession session,
            string resourceType, string callbackBody, out string deliveryRequestId)
        {
            deliveryRequestId = "";
            if (session == null || session.State != TerminalProcessState.Active)
                return false;

            var bodyHash = ComputeSha256(callbackBody ?? "");
            var eventKey = session.ProcessRequestId + "\n" +
                ProxyResourceTypes.Normalize(resourceType) + "\n" + bodyHash;
            var nowTicks = DateTime.UtcNow.Ticks;
            var cutoffTicks = nowTicks - DuplicateEventWindow.Ticks;
            long sequence;

            lock (_sync)
            {
                if (!_activeByTerminal.TryGetValue(session.TerminalIndex,
                    out var active) || !ReferenceEquals(active, session))
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
                foreach (var item in _activeByTerminal.Values) unique.Add(item);
                foreach (var item in _startingByTerminal.Values) unique.Add(item);
                sessions = new List<TerminalProcessSession>(unique);
                _activeByTerminal.Clear();
                _startingByTerminal.Clear();
                _byRequestId.Clear();
                _recentEvents.Clear();
                _recentEventOrder.Clear();
            }

            foreach (var session in sessions)
                session.Stop(false);
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
