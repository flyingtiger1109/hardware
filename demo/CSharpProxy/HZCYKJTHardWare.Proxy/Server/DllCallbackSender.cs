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

    internal enum CallbackAttemptResult
    {
        Delivered,
        RetryableFailure,
        PermanentFailure,
        Cancelled
    }

    public class DllCallbackSender : IDisposable
    {
        private static readonly int[] RetryDelaysMs = { 50, 200 };
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
            var body = BuildOcrCallbackBody(requestId, mrz, savePath, null);
            return PostCallbackWithRetryAndLifetime("/ocr", body, cancellationToken);
        }

        internal Task<CallbackDeliveryResult> SendOcrResult(string requestId, string mrz,
            string savePath, OcrCallbackResult ocrResult, CancellationToken cancellationToken)
        {
            var body = BuildOcrCallbackBody(requestId, mrz, savePath, ocrResult);
            return PostCallbackWithRetryAndLifetime("/ocr", body, cancellationToken);
        }

        public async Task SendOcrResult(string requestId, string mrz, string savePath)
        {
            var body = BuildOcrCallbackBody(requestId, mrz, savePath, null);
            await PostCallbackWithRetry("/ocr", body, _shutdown.Token).ConfigureAwait(false);
        }

        private static string BuildOcrCallbackBody(string requestId, string mrz,
            string savePath, OcrCallbackResult ocrResult)
        {
            var callbackMrz = mrz ?? "";
            if (ocrResult?.CardType == 30)
            {
                callbackMrz = "$" + (ocrResult.CardId ?? "") +
                    "^" + ocrResult.AuthenScore +
                    "^" + (ocrResult.Birthday ?? "") +
                    "^" + (ocrResult.DateOfIssue ?? "") +
                    "^" + (ocrResult.Name ?? "") +
                    "^" + (ocrResult.Sex ?? "");
            }

            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"mrz\":\"{JsonHelper.EscapeString(callbackMrz)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"";
            if (ocrResult?.CardType == 30)
            {
                body += $",\"card_type\":30" +
                    $",\"name\":\"{JsonHelper.EscapeString(ocrResult.Name)}\"" +
                    $",\"sex\":\"{JsonHelper.EscapeString(ocrResult.Sex)}\"" +
                    $",\"cardId\":\"{JsonHelper.EscapeString(ocrResult.CardId)}\"" +
                    $",\"birthday\":\"{JsonHelper.EscapeString(ocrResult.Birthday)}\"" +
                    $",\"dateOfissue\":\"{JsonHelper.EscapeString(ocrResult.DateOfIssue)}\"" +
                    $",\"authen_score\":{ocrResult.AuthenScore}" +
                    $",\"optical_check_result\":{ocrResult.OpticalCheckResult}";
            }
            return body + "}";
        }

        internal Task<CallbackDeliveryResult> SendNfcResult(string requestId, string cardText,
            CancellationToken cancellationToken)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"card_text\":\"{JsonHelper.EscapeString(cardText)}\"}}";
            return PostCallbackWithRetryAndLifetime("/nfc-card", body, cancellationToken);
        }

        public async Task SendNfcResult(string requestId, string cardText)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"card_text\":\"{JsonHelper.EscapeString(cardText)}\"}}";
            await PostCallbackWithRetry("/nfc-card", body, _shutdown.Token).ConfigureAwait(false);
        }

        internal Task<CallbackDeliveryResult> SendIrisResult(string requestId, string savePath,
            CancellationToken cancellationToken)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            return PostCallbackWithRetryAndLifetime("/iris", body, cancellationToken);
        }

        public async Task SendIrisResult(string requestId, string savePath)
        {
            var body = $"{{\"request_id\":\"{JsonHelper.EscapeString(requestId)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            await PostCallbackWithRetry("/iris", body, _shutdown.Token).ConfigureAwait(false);
        }

        public async Task SendPreviewReady(string requestId, string resourceType, IntPtr renderHwnd, IntPtr delphiHostHwnd)
        {
            // 审查风险：requestId 和 resourceType 未进行 JSON 转义，包含引号或控制字符时会生成无效载荷。
            // 建议统一调用 JsonHelper.EscapeString，或改用 JSON 序列化器构建载荷。
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"{resourceType}\",\"render_hwnd\":{renderHwnd.ToInt64()},\"delphi_host_hwnd\":{delphiHostHwnd.ToInt64()}}}";
            await PostCallbackWithRetry("/preview-ready", body, _shutdown.Token).ConfigureAwait(false);
        }

        public async Task SendAuthorizeResult(string requestId, string authResult, string message,
            string idNo, string docType, string nationality, string name, string sex, string birthday)
        {
            // 审查风险：requestId 和 authResult 未进行 JSON 转义；建议与其余字符串字段采用相同转义策略。
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"authorization\",\"auth_result\":\"{authResult}\"," +
                $"\"message\":\"{JsonHelper.EscapeString(message)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"nationality\":\"{JsonHelper.EscapeString(nationality)}\",\"name\":\"{JsonHelper.EscapeString(name)}\"," +
                $"\"sex\":\"{JsonHelper.EscapeString(sex)}\",\"birthday\":\"{JsonHelper.EscapeString(birthday)}\"}}";
            await PostCallbackWithRetry("/authorize", body, _shutdown.Token).ConfigureAwait(false);
        }

        private async Task<CallbackDeliveryResult> PostCallbackWithRetryAndLifetime(string path,
            string bodyUtf8, CancellationToken requestCancellation)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                requestCancellation, _shutdown.Token))
            {
                return await PostCallbackWithRetry(path, bodyUtf8, linked.Token)
                    .ConfigureAwait(false);
            }
        }

        private async Task<CallbackDeliveryResult> PostCallbackWithRetry(string path,
            string bodyUtf8, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _disposed) != 0)
                return CallbackDeliveryResult.Cancelled;
            if (!_baseUrlValid)
            {
                Logger.Error($"[DLL callback] invalid callback base URL: {_baseUrl}");
                return CallbackDeliveryResult.Failed;
            }

            for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
            {
                var attemptResult = await PostCallbackAttempt(path, bodyUtf8, cancellationToken)
                    .ConfigureAwait(false);
                if (attemptResult == CallbackAttemptResult.Delivered)
                    return CallbackDeliveryResult.Delivered;
                if (attemptResult == CallbackAttemptResult.Cancelled)
                    return CallbackDeliveryResult.Cancelled;
                if (attemptResult == CallbackAttemptResult.PermanentFailure)
                    return CallbackDeliveryResult.Failed;
                if (attempt >= RetryDelaysMs.Length)
                {
                    Logger.Error($"[DLL回调] POST {path} 重试耗尽，永久投递失败");
                    return CallbackDeliveryResult.Failed;
                }

                var delayMs = RetryDelaysMs[attempt];
                Logger.Warn($"[DLL回调] POST {path} 暂时失败，将在 {delayMs}ms 后重试，attempt={attempt + 2}");
                try
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return CallbackDeliveryResult.Cancelled;
                }
            }

            return CallbackDeliveryResult.Failed;
        }

        private async Task<CallbackAttemptResult> PostCallbackAttempt(string path,
            string bodyUtf8, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _disposed) != 0)
                return CallbackAttemptResult.Cancelled;

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
                        return CallbackAttemptResult.Delivered;
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                        return CallbackAttemptResult.RetryableFailure;

                    Logger.Warn($"[DLL回调] POST {path} 投递失败，不重试: status={statusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                    return CallbackAttemptResult.PermanentFailure;
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return cancellationToken.IsCancellationRequested
                    ? CallbackAttemptResult.Cancelled
                    : CallbackAttemptResult.RetryableFailure;
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                Logger.Warn($"[DLL回调] POST {path} 网络失败，可重试: {ex.Message}");
                return CallbackAttemptResult.RetryableFailure;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[DLL回调] POST {path} 失败, 耗时={sw.ElapsedMilliseconds}ms", ex);
                return CallbackAttemptResult.PermanentFailure;
            }
        }

        /// <summary>
        /// 将原始回调载荷提交到指定路径，供发送预构建载荷的 /authorize 使用。
        /// </summary>
        public async Task PostCallbackRaw(string path, string bodyUtf8)
        {
            await PostCallbackWithRetry(path, bodyUtf8, _shutdown.Token).ConfigureAwait(false);
        }

        internal Task<CallbackDeliveryResult> PostCallbackRaw(string path, string bodyUtf8,
            CancellationToken cancellationToken)
        {
            return PostCallbackWithRetryAndLifetime(path, bodyUtf8, cancellationToken);
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
