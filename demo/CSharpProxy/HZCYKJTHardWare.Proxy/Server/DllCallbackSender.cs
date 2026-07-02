using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;

namespace HZCYKJTHardWare.Proxy.Server
{
    internal enum CallbackDeliveryResult
    {
        Delivered,
        Failed,
        Cancelled
    }

    public class DllCallbackSender : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly bool _baseUrlValid;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private int _disposed;

        public DllCallbackSender()
            : this(new HttpClient(), AppConfig.Instance.GetDllCallbackBaseUrl())
        {
        }

        internal DllCallbackSender(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _baseUrlValid = Uri.TryCreate(_baseUrl, UriKind.Absolute, out var callbackUri) &&
                (callbackUri.Scheme == Uri.UriSchemeHttp ||
                 callbackUri.Scheme == Uri.UriSchemeHttps);
        }

        internal Task<CallbackDeliveryResult> SendOcrResult(string requestId, string mrz,
            string savePath, CancellationToken cancellationToken)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"mrz\":\"{JsonHelper.EscapeString(mrz)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            return PostCallbackOnceWithLifetime("/ocr", body, cancellationToken);
        }

        public async Task SendOcrResult(string requestId, string mrz, string savePath)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"mrz\":\"{JsonHelper.EscapeString(mrz)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            await PostCallbackOnce("/ocr", body, _shutdown.Token).ConfigureAwait(false);
        }

        internal Task<CallbackDeliveryResult> SendNfcResult(string requestId, string cardText,
            CancellationToken cancellationToken)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"card_text\":\"{JsonHelper.EscapeString(cardText)}\"}}";
            return PostCallbackOnceWithLifetime("/nfc-card", body, cancellationToken);
        }

        public async Task SendNfcResult(string requestId, string cardText)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"card_text\":\"{JsonHelper.EscapeString(cardText)}\"}}";
            await PostCallbackOnce("/nfc-card", body, _shutdown.Token).ConfigureAwait(false);
        }

        internal Task<CallbackDeliveryResult> SendIrisResult(string requestId, string savePath,
            CancellationToken cancellationToken)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            return PostCallbackOnceWithLifetime("/iris", body, cancellationToken);
        }

        public async Task SendIrisResult(string requestId, string savePath)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            await PostCallbackOnce("/iris", body, _shutdown.Token).ConfigureAwait(false);
        }

        public async Task SendPreviewReady(string requestId, string resourceType, IntPtr renderHwnd, IntPtr delphiHostHwnd)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"{resourceType}\",\"render_hwnd\":{renderHwnd.ToInt64()},\"delphi_host_hwnd\":{delphiHostHwnd.ToInt64()}}}";
            await PostCallbackOnce("/preview-ready", body, _shutdown.Token).ConfigureAwait(false);
        }

        public async Task SendAuthorizeResult(string requestId, string authResult, string message,
            string idNo, string docType, string nationality, string name, string sex, string birthday)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"authorization\",\"auth_result\":\"{authResult}\"," +
                $"\"message\":\"{JsonHelper.EscapeString(message)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"nationality\":\"{JsonHelper.EscapeString(nationality)}\",\"name\":\"{JsonHelper.EscapeString(name)}\"," +
                $"\"sex\":\"{JsonHelper.EscapeString(sex)}\",\"birthday\":\"{JsonHelper.EscapeString(birthday)}\"}}";
            await PostCallbackOnce("/authorize", body, _shutdown.Token).ConfigureAwait(false);
        }

        private async Task<CallbackDeliveryResult> PostCallbackOnceWithLifetime(string path,
            string bodyUtf8, CancellationToken requestCancellation)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                requestCancellation, _shutdown.Token))
            {
                return await PostCallbackOnce(path, bodyUtf8, linked.Token)
                    .ConfigureAwait(false);
            }
        }

        private async Task<CallbackDeliveryResult> PostCallbackOnce(string path,
            string bodyUtf8, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _disposed) != 0)
                return CallbackDeliveryResult.Cancelled;
            if (!_baseUrlValid)
            {
                Logger.Error($"[DLL callback] invalid callback base URL: {_baseUrl}");
                return CallbackDeliveryResult.Failed;
            }

            var url = _baseUrl + path;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (var content = new StringContent(bodyUtf8, Encoding.UTF8, "application/json"))
                using (var response = await _httpClient.PostAsync(url, content, cancellationToken)
                    .ConfigureAwait(false))
                {
                    sw.Stop();
                    var statusCode = (int)response.StatusCode;
                    if (response.IsSuccessStatusCode)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                            Logger.Warn($"[DLL回调] POST {path} 响应较慢: {statusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                        return CallbackDeliveryResult.Delivered;
                    }

                    Logger.Warn($"[DLL回调] POST {path} 投递失败，不重试: status={statusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                    return CallbackDeliveryResult.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return cancellationToken.IsCancellationRequested
                    ? CallbackDeliveryResult.Cancelled
                    : CallbackDeliveryResult.Failed;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[DLL回调] POST {path} 失败, 耗时={sw.ElapsedMilliseconds}ms", ex);
                return CallbackDeliveryResult.Failed;
            }
        }

        /// <summary>
        /// Post a raw callback payload to a specific path (used by /authorize which sends pre-built payload).
        /// </summary>
        public async Task PostCallbackRaw(string path, string bodyUtf8)
        {
            await PostCallbackOnce(path, bodyUtf8, _shutdown.Token).ConfigureAwait(false);
        }

        internal Task<CallbackDeliveryResult> PostCallbackRaw(string path, string bodyUtf8,
            CancellationToken cancellationToken)
        {
            return PostCallbackOnceWithLifetime(path, bodyUtf8, cancellationToken);
        }

        internal void Stop()
        {
            try { _shutdown.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            Stop();
            _httpClient.Dispose();
            _shutdown.Dispose();
        }
    }
}
