using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HZCYKJTHardWare.Proxy.Server
{
    /// <summary>
    /// 用于 DLL↔Proxy 和 Proxy↔终端内部通信链路的无状态 HTTP/1.1 请求解析器及响应写入器。
    ///
    /// 从 ProxyServer 原样拆分，不改变既有行为。
    /// </summary>
    internal static class HttpProtocolHandler
    {
        private const int MaxHeaderBytes = 64 * 1024;
        // 限制并发回调正文大小，避免大量 Base64 图像突发时在 x86 或 x64 进程中造成过高的托管内存压力
        private const int MaxBodyBytes = 16 * 1024 * 1024;

        /// <summary>
        /// 从 NetworkStream 读取 HTTP 请求，返回 (method, path, body)。
        /// 实现与原 ProxyServer.ReadHttpRequest 保持一致。
        /// </summary>
        public static async Task<(string method, string path, string body)> ReadHttpRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // 审查建议：MemoryStream 实现了 IDisposable；建议改用 using，确保后续替换为持有外部资源的流时仍能及时释放。
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
        /// 向 NetworkStream 写入 HTTP JSON 响应。
        /// 实现与原 ProxyServer.WriteHttpResponse 保持一致。
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
        /// 向 NetworkStream 写入二进制响应，例如最新车牌 JPEG。
        /// </summary>
        public static async Task WriteHttpResponseAsync(NetworkStream stream,
            int statusCode, string contentType, byte[] body,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await WriteHttpResponseAsync(stream, statusCode, contentType, body,
                null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 向 NetworkStream 写入带内部诊断响应头的二进制响应。
        /// 这些响应头只服务于 DLL↔Proxy 内部链路，不改变 JPEG 正文或导出 ABI。
        /// </summary>
        public static async Task WriteHttpResponseAsync(NetworkStream stream,
            int statusCode, string contentType, byte[] body,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var statusText = statusCode == 200 ? "OK" : statusCode == 202 ? "Accepted" : "Error";
            var bodyBytes = body ?? new byte[0];
            var responseContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;
            var headerBuilder = new StringBuilder();
            headerBuilder.Append($"HTTP/1.1 {statusCode} {statusText}\r\n")
                .Append("Content-Type: ").Append(responseContentType).Append("\r\n")
                .Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;
                    var value = (pair.Value ?? string.Empty)
                        .Replace("\r", " ").Replace("\n", " ");
                    headerBuilder.Append(pair.Key).Append(": ").Append(value).Append("\r\n");
                }
            }
            headerBuilder.Append("Connection: close\r\n\r\n");
            var header = headerBuilder.ToString();
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 向 TcpClient 写入 503 Service Busy 响应并关闭连接。
        /// 实现与原 ProxyServer.RejectBusyClient 保持一致。
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
        /// 在字节数组中查找指定序列，与原 ProxyServer.IndexOf 实现一致。
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
