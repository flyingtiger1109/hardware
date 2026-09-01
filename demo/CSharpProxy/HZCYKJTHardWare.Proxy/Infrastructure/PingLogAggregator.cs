using System;
using System.Collections.Generic;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    /// <summary>
    /// 聚合 DLL 到 Proxy 的 /ping 日志。
    /// 正常请求按一分钟汇总；故障、异常、故障原因变化和恢复立即输出。
    /// </summary>
    internal sealed class PingLogAggregator : IDisposable
    {
        private static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RepeatedFailureNoticeInterval = TimeSpan.FromMinutes(1);

        private readonly object _sync = new object();
        private readonly Action<string> _emit;
        private readonly Func<DateTime> _utcNow;
        private DateTime _windowStartedUtc;
        private DateTime _failureStartedUtc;
        private DateTime _lastFailureNoticeUtc = DateTime.MinValue;
        private long _requestCount;
        private long _successCount;
        private long _failureCount;
        private long _exceptionCount;
        private long _totalElapsedMs;
        private long _maxElapsedMs;
        private long _consecutiveFailureCount;
        private bool _failureActive;
        private string _lastFailureReason;
        private bool _disposed;

        public PingLogAggregator(Action<string> emit, Func<DateTime> utcNow = null)
        {
            _emit = emit;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _windowStartedUtc = _utcNow();
        }

        public void RecordSuccess(long elapsedMs)
        {
            var messages = new List<string>();
            var now = _utcNow();
            lock (_sync)
            {
                if (_disposed)
                    return;

                AddDueSummaryLocked(now, messages);
                AddTimingLocked(elapsedMs);
                _successCount++;

                if (_failureActive)
                {
                    var durationMs = Math.Max(0L, (long)(now - _failureStartedUtc).TotalMilliseconds);
                    messages.Add(Logger.FormatModuleMessage(
                        LogModules.HealthCheck,
                        "信息",
                        "/ping恢复：故障持续时间=" + durationMs + "毫秒，连续失败次数=" +
                        _consecutiveFailureCount + "，当前状态=正常"));
                    _failureActive = false;
                    _lastFailureReason = null;
                    _consecutiveFailureCount = 0;
                }
            }

            Emit(messages);
        }

        public void RecordFailure(string reason, bool exception, long elapsedMs)
        {
            var messages = new List<string>();
            var now = _utcNow();
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason;
            lock (_sync)
            {
                if (_disposed)
                    return;

                var summaryAdded = AddDueSummaryLocked(now, messages);
                AddTimingLocked(elapsedMs);
                _failureCount++;
                if (exception)
                    _exceptionCount++;
                _consecutiveFailureCount++;

                var reasonChanged = !string.Equals(_lastFailureReason, normalizedReason,
                    StringComparison.Ordinal);
                if (!_failureActive)
                {
                    _failureActive = true;
                    _failureStartedUtc = now;
                }

                var shouldNotice = reasonChanged ||
                    (!summaryAdded && now - _lastFailureNoticeUtc >= RepeatedFailureNoticeInterval);
                if (shouldNotice)
                {
                    var level = exception ? "错误" : "警告";
                    var kind = exception ? "异常" : "失败";
                    messages.Add(Logger.FormatModuleMessage(
                        LogModules.HealthCheck,
                        level,
                        "/ping" + kind + "：原因=" + normalizedReason +
                        "，耗时=" + Math.Max(0L, elapsedMs) + "毫秒，连续失败次数=" +
                        _consecutiveFailureCount));
                    _lastFailureNoticeUtc = now;
                    _lastFailureReason = normalizedReason;
                }
                else if (summaryAdded)
                {
                    // 窗口边界的汇总已经表达了同一原因的持续故障，避免紧接着再打印一条当前错误。
                    _lastFailureNoticeUtc = now;
                }
            }

            Emit(messages);
        }

        public void Flush()
        {
            string message = null;
            lock (_sync)
            {
                if (_disposed || _requestCount == 0)
                    return;

                var now = _utcNow();
                message = BuildSummaryLocked(now);
                ResetWindowLocked(now);
            }

            Emit(message);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            FlushAfterDispose();
        }

        private void FlushAfterDispose()
        {
            string message = null;
            lock (_sync)
            {
                if (_requestCount > 0)
                {
                    var now = _utcNow();
                    message = BuildSummaryLocked(now);
                    ResetWindowLocked(now);
                }
            }
            Emit(message);
        }

        private bool AddDueSummaryLocked(DateTime now, ICollection<string> messages)
        {
            if (_requestCount == 0 || now - _windowStartedUtc < SummaryInterval)
                return false;

            messages.Add(BuildSummaryLocked(now));
            ResetWindowLocked(now);
            return true;
        }

        private void AddTimingLocked(long elapsedMs)
        {
            var value = Math.Max(0L, elapsedMs);
            _requestCount++;
            _totalElapsedMs += value;
            if (value > _maxElapsedMs)
                _maxElapsedMs = value;
        }

        private string BuildSummaryLocked(DateTime now)
        {
            var average = _requestCount == 0 ? 0 : _totalElapsedMs / _requestCount;
            var status = _failureActive ? "故障" : "正常";
            var reason = string.IsNullOrWhiteSpace(_lastFailureReason) ? "无" : _lastFailureReason;
            return Logger.FormatModuleMessage(
                LogModules.HealthCheck,
                _failureActive ? "警告" : "调试",
                "统计周期=" + Math.Max(0L, (long)(now - _windowStartedUtc).TotalSeconds) +
                "秒，请求次数=" + _requestCount +
                "，成功次数=" + _successCount +
                "，失败次数=" + _failureCount +
                "，异常次数=" + _exceptionCount +
                "，平均耗时=" + average +
                "毫秒，最大耗时=" + _maxElapsedMs +
                "毫秒，当前状态=" + status +
                "，最近原因=" + reason);
        }

        private void ResetWindowLocked(DateTime now)
        {
            _windowStartedUtc = now;
            _requestCount = 0;
            _successCount = 0;
            _failureCount = 0;
            _exceptionCount = 0;
            _totalElapsedMs = 0;
            _maxElapsedMs = 0;
        }

        private void Emit(IEnumerable<string> messages)
        {
            if (messages == null)
                return;
            foreach (var message in messages)
                Emit(message);
        }

        private void Emit(string message)
        {
            if (string.IsNullOrEmpty(message) || _emit == null)
                return;
            try { _emit(message); }
            catch { /* 日志聚合器不得影响 HTTP 请求处理 */ }
        }
    }
}
