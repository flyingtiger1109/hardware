using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public class TerminalClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TerminalClient()
        {
            // Increase global connection limit to prevent socket exhaustion under high-frequency requests
            ServicePointManager.DefaultConnectionLimit = 50;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.ReusePort = true;

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                MaxConnectionsPerServer = 20,  // Limit concurrent connections per terminal
                UseProxy = false
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);  // Terminal HTTP timeout
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HZCYKJTHardWare-Proxy/2.0");
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;  // Enable keep-alive
        }

        public async Task<(bool ok, string response)> PostJsonAsync(string baseUrl, string path, string bodyUtf8)
        {
            var url = baseUrl.TrimEnd('/') + path;
            try
            {
                var content = new StringContent(bodyUtf8, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Logger.Info($"Terminal POST {path} -> {(int)response.StatusCode}");
                    return (true, responseBody);
                }
                else
                {
                    Logger.Warn($"Terminal POST {path} -> {(int)response.StatusCode}: {Truncate(responseBody, 256)}");
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Error($"Terminal POST {path} -> 超时(timeout={_httpClient.Timeout.TotalSeconds}s)");
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"Terminal POST {path} -> 网络错误: {ex.Message}");
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Terminal POST {path} -> 异常: {ex.Message}");
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
        }

        public async Task<(bool ok, string response)> GetJsonAsync(string baseUrl, string path)
        {
            var url = baseUrl.TrimEnd('/') + path;
            try
            {
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, responseBody);
                }
                else
                {
                    Logger.Warn($"Terminal GET {path} -> {(int)response.StatusCode}");
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Error($"Terminal GET {path} -> 超时");
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"Terminal GET {path} -> 网络错误: {ex.Message}");
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Terminal GET {path} -> 异常: {ex.Message}");
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }

        public void Dispose()
        {
            try { _httpClient?.CancelPendingRequests(); } catch { }
            _httpClient?.Dispose();
        }
    }
}
