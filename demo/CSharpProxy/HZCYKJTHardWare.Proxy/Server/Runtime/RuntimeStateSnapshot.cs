using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// 只用于运行状态观察和诊断，不得用于触发熔断、自动重启或请求阻断。
    /// Snapshot 只复制内存状态，不执行 HTTP、硬件、磁盘或恢复操作。
    /// </summary>
    internal sealed class RuntimeStateSnapshot
    {
        internal RuntimeStateSnapshot(DateTime generatedUtc, int currentTerminalIndex,
            IReadOnlyList<TerminalRuntimeStateSnapshot> terminals,
            IReadOnlyList<PreviewRuntimeStateSnapshot> previews)
        {
            GeneratedUtc = generatedUtc;
            CurrentTerminalIndex = currentTerminalIndex;
            Terminals = terminals ?? new TerminalRuntimeStateSnapshot[0];
            Previews = previews ?? new PreviewRuntimeStateSnapshot[0];
        }

        public DateTime GeneratedUtc { get; }
        public int CurrentTerminalIndex { get; }
        public IReadOnlyList<TerminalRuntimeStateSnapshot> Terminals { get; }
        public IReadOnlyList<PreviewRuntimeStateSnapshot> Previews { get; }

        internal string ToDiagnosticString()
        {
            var builder = new StringBuilder();
            builder.Append("runtime_state_current=T");
            builder.Append(CurrentTerminalIndex);
            builder.Append(" terminals=");

            for (var i = 0; i < Terminals.Count; i++)
            {
                if (i > 0)
                    builder.Append('|');

                var terminal = Terminals[i];
                builder.Append('T');
                builder.Append(terminal.TerminalIndex);
                builder.Append(" reachable=");
                builder.Append(FormatReachable(terminal.Reachable));
                builder.Append(" failures=");
                builder.Append(terminal.FailureCount);
                builder.Append(" consecutive=");
                builder.Append(terminal.ConsecutiveFailures);
                builder.Append(" latency_ms=");
                builder.Append(terminal.LastLatencyMs);
                builder.Append(" last_error=");
                builder.Append(string.IsNullOrEmpty(terminal.LastErrorCode)
                    ? "none" : terminal.LastErrorCode);
                builder.Append(" health=");
                builder.Append(FormatHealth(terminal.HealthHealthy));
            }

            builder.Append(" previews=");
            if (Previews.Count == 0)
            {
                builder.Append("none");
            }
            else
            {
                for (var i = 0; i < Previews.Count; i++)
                {
                    if (i > 0)
                        builder.Append('|');

                    var preview = Previews[i];
                    builder.Append(preview.ResourceType);
                    builder.Append('/');
                    builder.Append(preview.SessionType);
                    builder.Append(':');
                    builder.Append(preview.DesiredState);
                    builder.Append('/');
                    builder.Append(preview.RuntimeState);
                    builder.Append(" recovering=");
                    builder.Append(preview.Recovering ? '1' : '0');
                    builder.Append(" attempt=");
                    builder.Append(preview.RecoveryAttempt);
                }
            }

            return builder.ToString();
        }

        internal static string NormalizeError(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var normalized = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
            if (normalized.Length > 160)
                normalized = normalized.Substring(0, 160);
            return normalized;
        }

        private static string FormatReachable(bool? value)
        {
            if (!value.HasValue)
                return "unknown";
            return value.Value ? "1" : "0";
        }

        private static string FormatHealth(bool? value)
        {
            if (!value.HasValue)
                return "unknown";
            return value.Value ? "normal" : "abnormal";
        }
    }

    internal sealed class TerminalRuntimeStateSnapshot
    {
        internal TerminalRuntimeStateSnapshot(int terminalIndex, string terminalName,
            bool configured, string endpoint, bool? reachable,
            DateTime? lastSuccessUtc, DateTime? lastFailureUtc,
            int failureCount, int consecutiveFailures, string lastErrorCode,
            long lastLatencyMs, bool? healthHealthy, DateTime? lastHealthObservedUtc,
            string lastHealthError, IReadOnlyList<DeviceRuntimeStateSnapshot> devices)
        {
            TerminalIndex = terminalIndex;
            TerminalName = terminalName ?? "";
            Configured = configured;
            Endpoint = endpoint ?? "";
            Reachable = reachable;
            LastSuccessUtc = lastSuccessUtc;
            LastFailureUtc = lastFailureUtc;
            FailureCount = failureCount;
            ConsecutiveFailures = consecutiveFailures;
            LastErrorCode = lastErrorCode ?? "";
            LastLatencyMs = lastLatencyMs;
            HealthHealthy = healthHealthy;
            LastHealthObservedUtc = lastHealthObservedUtc;
            LastHealthError = lastHealthError ?? "";
            Devices = devices ?? new DeviceRuntimeStateSnapshot[0];
        }

        public int TerminalIndex { get; }
        public string TerminalName { get; }
        public bool Configured { get; }
        public string Endpoint { get; }
        public bool? Reachable { get; }
        public DateTime? LastSuccessUtc { get; }
        public DateTime? LastFailureUtc { get; }
        public int FailureCount { get; }
        public int ConsecutiveFailures { get; }
        public string LastErrorCode { get; }
        public long LastLatencyMs { get; }
        public bool? HealthHealthy { get; }
        public DateTime? LastHealthObservedUtc { get; }
        public string LastHealthError { get; }
        public IReadOnlyList<DeviceRuntimeStateSnapshot> Devices { get; }
    }

    internal sealed class DeviceRuntimeStateSnapshot
    {
        internal DeviceRuntimeStateSnapshot(string id, string status, string message,
            bool isOnline)
        {
            Id = id ?? "";
            Status = status ?? "";
            Message = message ?? "";
            IsOnline = isOnline;
        }

        public string Id { get; }
        public string Status { get; }
        public string Message { get; }
        public bool IsOnline { get; }
    }

    internal sealed class PreviewRuntimeStateSnapshot
    {
        internal PreviewRuntimeStateSnapshot(string key, PreviewResourceType resourceType,
            PreviewSessionType sessionType, int terminalIndex, bool terminalBound,
            string desiredState, string runtimeState, bool recovering,
            int recoveryAttempt, int recoveryFailureCount, DateTime? lastFailureUtc,
            string lastError)
        {
            Key = key ?? "";
            ResourceType = resourceType;
            SessionType = sessionType;
            TerminalIndex = terminalIndex;
            TerminalBound = terminalBound;
            DesiredState = desiredState ?? "";
            RuntimeState = runtimeState ?? "";
            Recovering = recovering;
            RecoveryAttempt = recoveryAttempt;
            RecoveryFailureCount = recoveryFailureCount;
            LastFailureUtc = lastFailureUtc;
            LastError = RuntimeStateSnapshot.NormalizeError(lastError);
        }

        public string Key { get; }
        public PreviewResourceType ResourceType { get; }
        public PreviewSessionType SessionType { get; }
        public int TerminalIndex { get; }
        public bool TerminalBound { get; }
        public string DesiredState { get; }
        public string RuntimeState { get; }
        public bool Recovering { get; }
        public int RecoveryAttempt { get; }
        public int RecoveryFailureCount { get; }
        public DateTime? LastFailureUtc { get; }
        public string LastError { get; }
    }

    /// <summary>
    /// Proxy 内部终端状态记录器。它只记录已发生的请求/健康观察结果，绝不参与请求决策。
    /// </summary>
    internal sealed class RuntimeStateTracker
    {
        private sealed class MutableTerminalState
        {
            internal int TerminalIndex;
            internal string Endpoint = "";
            internal bool? Reachable;
            internal DateTime? LastSuccessUtc;
            internal DateTime? LastFailureUtc;
            internal int FailureCount;
            internal int ConsecutiveFailures;
            internal string LastErrorCode = "";
            internal long LastLatencyMs;
            internal bool? HealthHealthy;
            internal DateTime? LastHealthObservedUtc;
            internal string LastHealthError = "";
            internal List<DeviceRuntimeStateSnapshot> Devices =
                new List<DeviceRuntimeStateSnapshot>();
        }

        private readonly TerminalManager _terminalManager;
        private readonly object _sync = new object();
        private readonly MutableTerminalState[] _states =
        {
            new MutableTerminalState { TerminalIndex = 1 },
            new MutableTerminalState { TerminalIndex = 2 }
        };

        internal RuntimeStateTracker(TerminalManager terminalManager)
        {
            _terminalManager = terminalManager ??
                throw new ArgumentNullException(nameof(terminalManager));
        }

        internal void ObserveRequest(TerminalRequestObservation observation)
        {
            if (observation.Ignored || string.IsNullOrWhiteSpace(observation.BaseUrl))
                return;

            var endpoint = NormalizeEndpoint(observation.BaseUrl);
            if (endpoint.Length == 0)
                return;

            // Prefer the route captured at the time of the observation. If a late
            // response arrives after a switch, fall back to an endpoint previously
            // associated with a terminal so it cannot be attributed to the new one.
            var currentRoute = _terminalManager.CurrentRoute;
            lock (_sync)
            {
                var terminalIndex = 0;
                if (currentRoute != null &&
                    string.Equals(endpoint, NormalizeEndpoint(currentRoute.BaseUrl),
                        StringComparison.OrdinalIgnoreCase))
                {
                    terminalIndex = currentRoute.TerminalIndex;
                }
                else
                {
                    terminalIndex = FindTerminalByEndpointLocked(endpoint);
                }

                if (terminalIndex >= 1 && terminalIndex <= _states.Length)
                    RecordRequestLocked(terminalIndex, observation, endpoint);
            }
        }

        internal void RecordRequest(int terminalIndex, TerminalRequestObservation observation)
        {
            if (observation.Ignored || terminalIndex < 1 || terminalIndex > _states.Length)
                return;

            lock (_sync)
            {
                RecordRequestLocked(terminalIndex, observation,
                    NormalizeEndpoint(observation.BaseUrl));
            }
        }

        internal void ObserveHealth(HealthStatus status)
        {
            if (status == null || status.TerminalIndex < 1 ||
                status.TerminalIndex > _states.Length)
                return;

            lock (_sync)
            {
                var state = _states[status.TerminalIndex - 1];
                state.HealthHealthy = status.IsHealthy;
                state.LastHealthObservedUtc = DateTime.UtcNow;
                state.LastHealthError = RuntimeStateSnapshot.NormalizeError(
                    status.ErrorMessage);
                state.Devices = CopyDevices(status.Devices);
            }
        }

        internal RuntimeStateSnapshot GetSnapshot(PreviewManager previewManager)
        {
            var currentRoute = _terminalManager.CurrentRoute;
            var names = new string[_states.Length];
            var configured = new bool[_states.Length];
            for (var i = 0; i < _states.Length; i++)
            {
                names[i] = _terminalManager.GetTerminalName(i + 1);
                configured[i] = _terminalManager.IsTerminalConfigured(i + 1);
            }

            var terminals = new TerminalRuntimeStateSnapshot[_states.Length];
            lock (_sync)
            {
                for (var i = 0; i < _states.Length; i++)
                {
                    var state = _states[i];
                    terminals[i] = new TerminalRuntimeStateSnapshot(
                        state.TerminalIndex, names[i], configured[i], state.Endpoint,
                        state.Reachable, state.LastSuccessUtc, state.LastFailureUtc,
                        state.FailureCount, state.ConsecutiveFailures,
                        state.LastErrorCode, state.LastLatencyMs,
                        state.HealthHealthy, state.LastHealthObservedUtc,
                        state.LastHealthError,
                        new ReadOnlyCollection<DeviceRuntimeStateSnapshot>(
                            new List<DeviceRuntimeStateSnapshot>(state.Devices)));
                }
            }

            IReadOnlyList<PreviewRuntimeStateSnapshot> previews =
                new PreviewRuntimeStateSnapshot[0];
            if (previewManager != null)
            {
                previews = previewManager.CaptureRuntimeStateSnapshot(
                    ResolvePreviewTerminalIndex);
            }

            return new RuntimeStateSnapshot(DateTime.UtcNow,
                currentRoute == null ? 0 : currentRoute.TerminalIndex,
                new ReadOnlyCollection<TerminalRuntimeStateSnapshot>(terminals),
                previews);
        }

        private int ResolvePreviewTerminalIndex(string endpoint)
        {
            var normalized = NormalizeEndpoint(endpoint);
            if (normalized.Length == 0)
                return 0;

            var currentRoute = _terminalManager.CurrentRoute;
            if (currentRoute != null &&
                string.Equals(normalized, NormalizeEndpoint(currentRoute.BaseUrl),
                    StringComparison.OrdinalIgnoreCase))
                return currentRoute.TerminalIndex;

            lock (_sync)
                return FindTerminalByEndpointLocked(normalized);
        }

        private int FindTerminalByEndpointLocked(string endpoint)
        {
            for (var i = 0; i < _states.Length; i++)
            {
                if (string.Equals(endpoint, _states[i].Endpoint,
                    StringComparison.OrdinalIgnoreCase))
                    return _states[i].TerminalIndex;
            }
            return 0;
        }

        private MutableTerminalState GetState(int terminalIndex)
        {
            return terminalIndex < 1 || terminalIndex > _states.Length
                ? null
                : _states[terminalIndex - 1];
        }

        private void RecordRequestLocked(int terminalIndex,
            TerminalRequestObservation observation, string endpoint)
        {
            var state = GetState(terminalIndex);
            if (state == null)
                return;

            if (!string.IsNullOrEmpty(endpoint))
                state.Endpoint = endpoint;

            var nowUtc = DateTime.UtcNow;
            state.Reachable = observation.ResponseReceived;
            state.LastLatencyMs = Math.Max(0L, observation.ElapsedMs);

            if (observation.RequestSucceeded)
            {
                state.LastSuccessUtc = nowUtc;
                state.ConsecutiveFailures = 0;
                return;
            }

            state.LastFailureUtc = nowUtc;
            if (state.FailureCount < int.MaxValue)
                state.FailureCount++;
            if (state.ConsecutiveFailures < int.MaxValue)
                state.ConsecutiveFailures++;
            state.LastErrorCode = RuntimeStateSnapshot.NormalizeError(
                string.IsNullOrWhiteSpace(observation.ErrorCode)
                    ? "request_failed"
                    : observation.ErrorCode);
        }

        private static List<DeviceRuntimeStateSnapshot> CopyDevices(
            IList<DeviceHealth> devices)
        {
            var copy = new List<DeviceRuntimeStateSnapshot>();
            if (devices == null)
                return copy;

            foreach (var device in devices)
            {
                if (device == null || string.IsNullOrWhiteSpace(device.Id))
                    continue;

                var existing = copy.FindIndex(item =>
                    string.Equals(item.Id, device.Id,
                        StringComparison.OrdinalIgnoreCase));
                var value = new DeviceRuntimeStateSnapshot(device.Id,
                    device.Status, device.Message, device.IsOnline);
                if (existing >= 0)
                    copy[existing] = value;
                else
                    copy.Add(value);
            }
            return copy;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            return string.IsNullOrWhiteSpace(endpoint)
                ? ""
                : endpoint.Trim().TrimEnd('/');
        }
    }
}
