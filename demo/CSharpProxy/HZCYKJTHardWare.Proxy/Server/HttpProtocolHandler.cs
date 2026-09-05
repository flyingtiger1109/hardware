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
    /// HTTP 请求在进入路由或业务层之前读取失败。
    /// 仅用于传递边界诊断信息，不引入新的业务异常体系。
    /// </summary>
    internal sealed class HttpRequestReadException : InvalidOperationException
    {
        public HttpRequestReadException(string failureCode, string path,
            long receivedBytes, long expectedBytes)
            : base("HTTP请求读取失败：" + failureCode)
        {
            FailureCode = failureCode;
            Path = path;
            ReceivedBytes = receivedBytes;
            ExpectedBytes = expectedBytes;
        }

        public string FailureCode { get; }
        public string Path { get; }
        public long ReceivedBytes { get; }
        public long ExpectedBytes { get; }
    }

    /// <summary>
    /// 用于 DLL↔Proxy 和 Proxy↔终端内部通信链路的无状态 HTTP/1.1 请求解析器及响应写入器。
    ///
    /// 仅负责完整读取和解析请求，不负责路由或业务处理。
    /// </summary>
    internal static class HttpProtocolHandler
    {
        private const int MaxHeaderBytes = 64 * 1024;
        // 现有请求正文上限；本阶段不新增或调整业务级 Body Limit。
        private const int MaxBodyBytes = 16 * 1024 * 1024;
        private static readonly byte[] HeaderMarker = Encoding.ASCII.GetBytes("\r\n\r\n");

        private enum ContentLengthStatus
        {
            Missing,
            Empty,
            Negative,
            NonNumeric,
            Duplicate,
            TooLarge,
            Valid,
        }

        private static ContentLengthStatus ParseContentLength(string value,
            bool found, bool duplicate, out int contentLength)
        {
            contentLength = 0;
            if (!found) return ContentLengthStatus.Missing;
            if (duplicate) return ContentLengthStatus.Duplicate;
            if (string.IsNullOrEmpty(value)) return ContentLengthStatus.Empty;
            if (value[0] == '-') return ContentLengthStatus.Negative;

            long parsed = 0;
            foreach (var ch in value)
            {
                if (ch < '0' || ch > '9') return ContentLengthStatus.NonNumeric;
                var digit = ch - '0';
                if (parsed > (long.MaxValue - digit) / 10)
                    return ContentLengthStatus.TooLarge;
                parsed = parsed * 10 + digit;
            }

            if (parsed > MaxBodyBytes) return ContentLengthStatus.TooLarge;
            contentLength = (int)parsed;
            return ContentLengthStatus.Valid;
        }

        private static bool TryParseRequestLine(string firstLine,
            out string method, out string path)
        {
            method = "";
            path = "";
            var parts = firstLine.Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            method = parts[0];
            path = parts[1];
            return true;
        }

        /// <summary>
        /// 从 NetworkStream 读取 HTTP 请求，返回 (method, path, body)。
        /// 只有在 Header 完整、Content-Length 合法且 Body 完整读取后才返回。
        /// </summary>
        public static async Task<(string method, string path, string body)> ReadHttpRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var raw = new MemoryStream())
            {
                var buf = new byte[4096];
                int headerEnd = -1;

                while (headerEnd < 0)
                {
                    int bytesRead = await stream.ReadAsync(buf, 0, buf.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                        throw new HttpRequestReadException("header_incomplete", null,
                            raw.Length, -1);

                    raw.Write(buf, 0, bytesRead);
                    headerEnd = IndexOf(raw.GetBuffer(), (int)raw.Length, HeaderMarker);
                    if (headerEnd >= 0 && headerEnd + HeaderMarker.Length > MaxHeaderBytes)
                        throw new HttpRequestReadException("header_too_large", null,
                            headerEnd + HeaderMarker.Length, MaxHeaderBytes);
                    if (headerEnd < 0 && raw.Length > MaxHeaderBytes)
                        throw new HttpRequestReadException("header_too_large", null,
                            raw.Length, MaxHeaderBytes);
                }

                var rawBytes = raw.ToArray();
                var headerSize = headerEnd + HeaderMarker.Length;
                var headerStr = Encoding.ASCII.GetString(rawBytes, 0, headerSize);
                var lines = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);

                var firstLine = lines.Length > 0 ? lines[0] : "";
                if (!TryParseRequestLine(firstLine, out var method, out var path))
                    throw new HttpRequestReadException("request_line_invalid", null,
                        rawBytes.Length, -1);

                string contentLengthValue = null;
                bool contentLengthFound = false;
                bool contentLengthDuplicate = false;
                bool transferEncodingFound = false;
                foreach (var line in lines)
                {
                    var colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    var headerName = line.Substring(0, colon).Trim();
                    var headerValue = line.Substring(colon + 1).Trim();
                    if (string.Equals(headerName, "Content-Length",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (contentLengthFound)
                        {
                            contentLengthDuplicate = true;
                        }
                        else
                        {
                            contentLengthFound = true;
                            contentLengthValue = headerValue;
                        }
                    }
                    else if (string.Equals(headerName, "Transfer-Encoding",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        transferEncodingFound = true;
                    }
                }

                if (transferEncodingFound)
                    throw new HttpRequestReadException("unsupported_transfer_encoding",
                        path, rawBytes.Length - headerSize, -1);

                int contentLength;
                var contentLengthStatus = ParseContentLength(contentLengthValue,
                    contentLengthFound, contentLengthDuplicate, out contentLength);
                if (contentLengthStatus == ContentLengthStatus.Missing)
                {
                    // 保留现有无 Body 请求语义；若缺少长度却带有正文，则拒绝，避免吞掉未知长度数据。
                    if (rawBytes.Length > headerSize)
                        throw new HttpRequestReadException("content_length_missing",
                            path, rawBytes.Length - headerSize, -1);
                    return (method, path, "");
                }

                if (contentLengthStatus != ContentLengthStatus.Valid)
                {
                    var failureCode = contentLengthStatus == ContentLengthStatus.TooLarge
                        ? "content_length_too_large"
                        : "content_length_invalid";
                    throw new HttpRequestReadException(failureCode, path,
                        rawBytes.Length - headerSize, -1);
                }

                string body = "";
                if (contentLength > 0)
                {
                    // 仅在长度经过上限和整数范围校验后分配目标数组。
                    var bodyBuf = new byte[contentLength];
                    var alreadyRead = Math.Min(contentLength, rawBytes.Length - headerSize);
                    if (alreadyRead > 0)
                        Buffer.BlockCopy(rawBytes, headerSize, bodyBuf, 0, alreadyRead);

                    int totalRead = alreadyRead;
                    while (totalRead < contentLength)
                    {
                        int read = await stream.ReadAsync(bodyBuf, totalRead,
                            contentLength - totalRead, cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new HttpRequestReadException("body_incomplete",
                                path, totalRead, contentLength);
                        totalRead += read;
                    }
                    body = Encoding.UTF8.GetString(bodyBuf, 0, totalRead);
                }

                return (method, path, body);
            }
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
