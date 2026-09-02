using System;
using System.Collections.Generic;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    internal sealed class LogRateLimitDecision
    {
        internal bool EmitCurrent { get; set; }
        internal string WindowSummary { get; set; }
        internal string CurrentError { get; set; }
    }

    /// <summary>
    /// 按设备、接口和错误类别对重复故障做窗口聚合。
    /// 第一次故障立即输出；窗口结束时将旧窗口汇总和本次故障合并为一条日志。
    /// </summary>
    internal sealed class LogRateLimiter
    {
        private sealed class Bucket
        {
            internal DateTime FirstUtc;
            internal DateTime LastUtc;
            internal long Count;
            internal string LastError;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, Bucket> _buckets =
            new Dictionary<string, Bucket>(StringComparer.Ordinal);
        private readonly TimeSpan _window;

        internal LogRateLimiter(TimeSpan window)
        {
            _window = window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : window;
        }

        internal LogRateLimitDecision Record(string key, string error, DateTime utcNow)
        {
            var normalizedKey = Sanitize(key, 160);
            if (string.IsNullOrEmpty(normalizedKey))
                normalizedKey = "<unknown>";

            var normalizedError = Sanitize(error, 256);
            utcNow = utcNow.ToUniversalTime();
            lock (_sync)
            {
                Bucket bucket;
                if (!_buckets.TryGetValue(normalizedKey, out bucket) ||
                    utcNow - bucket.FirstUtc >= _window)
                {
                    var summary = bucket == null || bucket.Count <= 0
                        ? null
                        : "重复故障汇总：类别=" + DescribeCategory(normalizedKey) +
                          "，次数=" + bucket.Count +
                          "，首次=" + FormatTime(bucket.FirstUtc) +
                          "，最近=" + FormatTime(bucket.LastUtc) +
                          "，最近错误=" + bucket.LastError;

                    _buckets[normalizedKey] = new Bucket
                    {
                        FirstUtc = utcNow,
                        LastUtc = utcNow,
                        Count = 1,
                        LastError = normalizedError
                    };
                    TrimIfNeeded(normalizedKey);
                    return new LogRateLimitDecision
                    {
                        EmitCurrent = true,
                        WindowSummary = summary,
                        CurrentError = normalizedError
                    };
                }

                bucket.LastUtc = utcNow;
                bucket.Count++;
                bucket.LastError = normalizedError;
                return new LogRateLimitDecision { EmitCurrent = false };
            }
        }

        internal static string FormatMergedMessage(LogRateLimitDecision decision,
            string currentMessage)
        {
            if (decision == null || string.IsNullOrEmpty(decision.WindowSummary))
                return currentMessage;
            return decision.WindowSummary + "，本次错误=" + decision.CurrentError;
        }

        internal void Clear()
        {
            lock (_sync)
                _buckets.Clear();
        }

        private void TrimIfNeeded(string currentKey)
        {
            if (_buckets.Count <= 1024)
                return;

            string oldestKey = null;
            var oldest = DateTime.MaxValue;
            foreach (var pair in _buckets)
            {
                if (pair.Key == currentKey || pair.Value.LastUtc >= oldest)
                    continue;
                oldest = pair.Value.LastUtc;
                oldestKey = pair.Key;
            }
            if (oldestKey != null)
                _buckets.Remove(oldestKey);
        }

        private static string FormatTime(DateTime value)
        {
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        private static string DescribeCategory(string key)
        {
            var lower = (key ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("mjpeg") &&
                (lower.Contains("render") || lower.Contains("target")))
                return "MJPEG绘制失败";
            if (lower.Contains("mjpeg") && lower.Contains("decode"))
                return "MJPEG解码失败";
            if (lower.Contains("mjpeg"))
                return "MJPEG流故障";
            if (lower.Contains("callback"))
                return "回调投递失败";
            if (lower.Contains("ping") || lower.Contains("connect") ||
                lower.Contains("network") || lower.Contains("timeout") ||
                lower.Contains("12029"))
                return "连接失败";
            if (lower.Contains("preview"))
                return "预览失败";
            if (lower.Contains("queue"))
                return "任务队列失败";
            return "重复故障";
        }

        private static string Sanitize(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            var builder = new System.Text.StringBuilder(Math.Min(value.Length, maxLength));
            foreach (var ch in value)
            {
                builder.Append(char.IsControl(ch) ? ' ' : ch);
                if (builder.Length >= maxLength)
                    break;
            }
            if (value.Length > maxLength)
                builder.Append("...");
            return builder.ToString();
        }
    }
}
