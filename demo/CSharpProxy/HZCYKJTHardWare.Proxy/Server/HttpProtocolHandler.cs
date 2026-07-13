using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HZCYKJTHardWare.Proxy.Server
{
    /// <summary>
    /// Stateless HTTP/1.1 request parser and response writer for the internal
    /// DLL↔Proxy and Proxy↔Terminal communication channels.
    ///
    /// This is a pure extraction from ProxyServer — zero behavioral changes.
    /// </summary>
    internal static class HttpProtocolHandler
    {
        private const int MaxHeaderBytes = 64 * 1024;
        // Keep concurrent callback bodies bounded so a burst of large Base64 images
        // cannot cause excessive managed-memory pressure in either process architecture.
        private const int MaxBodyBytes = 16 * 1024 * 1024;

        /// <summary>
        /// Read an HTTP request from a NetworkStream. Returns (method, path, body).
        /// Same implementation as the original ProxyServer.ReadHttpRequest.
        /// </summary>
        public static async Task<(string method, string path, string body)> ReadHttpRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var raw = new MemoryStream();
            var buf = new byte[4096];
            var marker = Encoding.ASCII.GetBytes("\r\n\r\n");
            int headerEnd = -1;
            int contentLength = 0;
            string method = "GET";
            string path = "/";

            while (headerEnd < 0)
            {
                int bytesRead = await stream.ReadAsync(buf, 0, buf.Length,
                    cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) break;
                raw.Write(buf, 0, bytesRead);
                headerEnd = IndexOf(raw.GetBuffer(), (int)raw.Length, marker);
                if (raw.Length > MaxHeaderBytes && headerEnd < 0)
                    throw new InvalidOperationException("HTTP请求头过大");
            }

            if (headerEnd < 0)
                return (method, path, "");

            var rawBytes = raw.ToArray();
            var headerSize = headerEnd + marker.Length;
            var headerStr = Encoding.ASCII.GetString(rawBytes, 0, headerSize);
            var lines = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
            }

            var firstLine = lines.Length > 0 ? lines[0] : "";
            var parts = firstLine.Split(' ');
            if (parts.Length >= 2)
            {
                method = parts[0];
                path = parts[1];
            }

            if (contentLength < 0 || contentLength > MaxBodyBytes)
                throw new InvalidOperationException("HTTP请求体大小异常");

            string body = "";
            if (contentLength > 0)
            {
                var bodyBuf = new byte[contentLength];
                var alreadyRead = Math.Min(contentLength, rawBytes.Length - headerSize);
                if (alreadyRead > 0)
                    Buffer.BlockCopy(rawBytes, headerSize, bodyBuf, 0, alreadyRead);

                int totalRead = alreadyRead;
                while (totalRead < contentLength)
                {
                    int read = await stream.ReadAsync(bodyBuf, totalRead,
                        contentLength - totalRead, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    totalRead += read;
                }
                body = Encoding.UTF8.GetString(bodyBuf, 0, totalRead);
            }

            return (method, path, body);
        }

        /// <summary>
        /// Write an HTTP JSON response to a NetworkStream.
        /// Same implementation as the original ProxyServer.WriteHttpResponse.
        /// </summary>
        public static async Task WriteHttpResponseAsync(NetworkStream stream,
            int statusCode, string body,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var statusText = statusCode == 200 ? "OK" : statusCode == 202 ? "Accepted" : "Error";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a 503 Service Busy response to a TcpClient and close it.
        /// Same implementation as the original ProxyServer.RejectBusyClient.
        /// </summary>
        public static void Write503ServiceBusy(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var body = "{\"error\":true,\"code\":\"busy\"}";
                    var bodyBytes = Encoding.UTF8.GetBytes(body);
                    var header = $"HTTP/1.1 503 Service Busy\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                    var headerBytes = Encoding.UTF8.GetBytes(header);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(bodyBytes, 0, bodyBytes.Length);
                    stream.Flush();
                }
            }
            catch { }
        }

        /// <summary>
        /// String search in byte array. Same as original ProxyServer.IndexOf.
        /// </summary>
        private static int IndexOf(byte[] source, int sourceLength, byte[] pattern)
        {
            if (source == null || pattern == null || pattern.Length == 0 || sourceLength < pattern.Length)
                return -1;
            for (int i = 0; i <= sourceLength - pattern.Length; i++)
            {
                var matched = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return i;
            }
            return -1;
        }
    }
}
