using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);  // Terminal HTTP timeout
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HZCYKJTHardWare-Proxy/2.0");
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;  // Enable keep-alive
        }

        public async Task<(bool ok, string response)> PostJsonAsync(string baseUrl, string path, string bodyUtf8, int timeoutMs = 0)
        {
            var url = baseUrl.TrimEnd('/') + path;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CancellationTokenSource cts = null;
            try
            {
                if (timeoutMs > 0)
                    cts = new CancellationTokenSource(timeoutMs);

                using (var content = new StringContent(bodyUtf8, Encoding.UTF8, "application/json"))
                using (var response = await _httpClient.PostAsync(url, content, cts?.Token ?? CancellationToken.None).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                            Logger.Warn($"[终端请求] POST {path} 响应较慢: {(int)response.StatusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                        return (true, responseBody);
                    }

                    Logger.Warn($"[终端请求] POST {path} 失败: {(int)response.StatusCode}, 耗时={sw.ElapsedMilliseconds}ms, 内容={Truncate(responseBody, 256)}");
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                var timeoutText = timeoutMs > 0 ? timeoutMs + "ms" : _httpClient.Timeout.TotalSeconds + "s";
                Logger.Error($"[终端请求] POST {path} 超时: timeout={timeoutText}, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                Logger.Error($"[终端请求] POST {path} 网络错误: {ex.Message}, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[终端请求] POST {path} 异常: {ex.Message}, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
            finally
            {
                cts?.Dispose();
            }
        }

        public async Task<(bool ok, string response)> GetJsonAsync(string baseUrl, string path, int timeoutMs = 0)
        {
            var url = baseUrl.TrimEnd('/') + path;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CancellationTokenSource cts = null;
            try
            {
                if (timeoutMs > 0)
                    cts = new CancellationTokenSource(timeoutMs);

                using (var response = await _httpClient.GetAsync(url, cts?.Token ?? CancellationToken.None).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                            Logger.Warn($"[终端请求] GET {path} 响应较慢: {(int)response.StatusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                        return (true, responseBody);
                    }

                    Logger.Warn($"[终端请求] GET {path} 失败: {(int)response.StatusCode}, 耗时={sw.ElapsedMilliseconds}ms");
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                Logger.Error($"[终端请求] GET {path} 超时, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                Logger.Error($"[终端请求] GET {path} 网络错误: {ex.Message}, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"[终端请求] GET {path} 异常: {ex.Message}, 耗时={sw.ElapsedMilliseconds}ms");
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
            finally
            {
                cts?.Dispose();
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
