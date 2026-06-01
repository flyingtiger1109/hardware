using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class DllCallbackSender : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public DllCallbackSender()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _baseUrl = AppConfig.Instance.GetDllCallbackBaseUrl();
        }

        public async Task SendOcrResult(string requestId, string mrz, string savePath)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"mrz\":\"{JsonHelper.EscapeString(mrz)}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            await PostCallback("/ocr", body);
        }

        public async Task SendNfcResult(string requestId, string cardText)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"card_text\":\"{JsonHelper.EscapeString(cardText)}\"}}";
            await PostCallback("/nfc-card", body);
        }

        public async Task SendIrisResult(string requestId, string savePath)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"save_path\":\"{JsonHelper.EscapeString(savePath)}\"}}";
            await PostCallback("/iris", body);
        }

        public async Task SendPreviewReady(string requestId, string resourceType, IntPtr renderHwnd, IntPtr delphiHostHwnd)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"{resourceType}\",\"render_hwnd\":{renderHwnd.ToInt64()},\"delphi_host_hwnd\":{delphiHostHwnd.ToInt64()}}}";
            await PostCallback("/preview-ready", body);
        }

        public async Task SendAuthorizeResult(string requestId, string authResult, string message,
            string idNo, string docType, string nationality, string name, string sex, string birthday)
        {
            var body = $"{{\"request_id\":\"{requestId}\",\"resource_type\":\"authorization\",\"auth_result\":\"{authResult}\"," +
                $"\"message\":\"{JsonHelper.EscapeString(message)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"nationality\":\"{JsonHelper.EscapeString(nationality)}\",\"name\":\"{JsonHelper.EscapeString(name)}\"," +
                $"\"sex\":\"{JsonHelper.EscapeString(sex)}\",\"birthday\":\"{JsonHelper.EscapeString(birthday)}\"}}";
            await PostCallback("/authorize", body);
        }

        private async Task PostCallback(string path, string bodyUtf8)
        {
            var url = _baseUrl + path;
            try
            {
                var content = new StringContent(bodyUtf8, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                Logger.Info($"Callback POST {path} -> {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Callback POST {path} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Post a raw callback payload to a specific path (used by /authorize which sends pre-built payload).
        /// </summary>
        public async Task PostCallbackRaw(string path, string bodyUtf8)
        {
            await PostCallback(path, bodyUtf8);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
