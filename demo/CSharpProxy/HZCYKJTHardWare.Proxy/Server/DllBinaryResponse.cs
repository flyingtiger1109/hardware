using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Server
{
    /// <summary>
    /// DLL HTTP 链路的二进制响应。可选 release 回调用于在响应写完后归还并发租约。
    /// </summary>
    internal sealed class DllBinaryResponse : IDisposable
    {
        private Action _release;

        private DllBinaryResponse(int statusCode, string contentType,
            byte[] body, IDictionary<string, string> headers, Action release)
        {
            StatusCode = statusCode;
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;
            Body = body ?? new byte[0];
            Headers = NormalizeHeaders(headers);
            _release = release;
        }

        internal int StatusCode { get; }
        internal string ContentType { get; }
        internal byte[] Body { get; }
        internal IDictionary<string, string> Headers { get; }

        internal static DllBinaryResponse Json(string body, int statusCode = 200)
        {
            return new DllBinaryResponse(statusCode,
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(body ?? ""), null, null);
        }

        internal static DllBinaryResponse Binary(byte[] body, string contentType,
            Action release = null)
        {
            return new DllBinaryResponse(200, contentType, body, null, release);
        }

        internal static DllBinaryResponse Binary(byte[] body, string contentType,
            IDictionary<string, string> headers, Action release = null)
        {
            return new DllBinaryResponse(200, contentType, body, headers, release);
        }

        private static IDictionary<string, string> NormalizeHeaders(
            IDictionary<string, string> headers)
        {
            var normalized = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (headers == null)
                return normalized;

            foreach (var pair in headers)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                var value = (pair.Value ?? string.Empty)
                    .Replace("\r", " ").Replace("\n", " ");
                if (value.Length > 512)
                    value = value.Substring(0, 512);
                normalized[pair.Key] = value;
            }
            return normalized;
        }

        public void Dispose()
        {
            var release = Interlocked.Exchange(ref _release, null);
            if (release != null)
                release();
        }
    }
}
