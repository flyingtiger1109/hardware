using System;
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
            byte[] body, Action release)
        {
            StatusCode = statusCode;
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;
            Body = body ?? new byte[0];
            _release = release;
        }

        internal int StatusCode { get; }
        internal string ContentType { get; }
        internal byte[] Body { get; }

        internal static DllBinaryResponse Json(string body, int statusCode = 200)
        {
            return new DllBinaryResponse(statusCode,
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(body ?? ""), null);
        }

        internal static DllBinaryResponse Binary(byte[] body, string contentType,
            Action release = null)
        {
            return new DllBinaryResponse(200, contentType, body, release);
        }

        public void Dispose()
        {
            var release = Interlocked.Exchange(ref _release, null);
            if (release != null)
                release();
        }
    }
}
