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

        private static string FormatRequestId(string requestId)
        {
            return string.IsNullOrWhiteSpace(requestId) ? "<无>" : JsonHelper.ToLogValue(requestId);
        }

        private static string ExtractRequestIdForLog(string bodyUtf8)
        {
            try
            {
                return FormatRequestId(JsonHelper.ExtractString(bodyUtf8 ?? "", "request_id"));
            }
            catch
            {
                return "<无效>";
            }
        }

        public TerminalClient()
        {
            // 提高全局连接数上限，避免高频请求耗尽可用 Socket 连接
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);  // 终端 HTTP 超时时间
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HZCYKJTHardWare-Proxy/2.0");
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;  // 启用长连接
        }

        public async Task<(bool ok, string response)> PostJsonAsync(string baseUrl, string path,
            string bodyUtf8, int timeoutMs = 0,
            CancellationToken cancellationToken = default(CancellationToken),
            int expectedStatusCode = 0, bool isRecoveryAttempt = false)
        {
            var url = baseUrl.TrimEnd('/') + path;
            var requestTrace = ExtractRequestIdForLog(bodyUtf8);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Logger.Debug($"[终端请求] POST开始：路径={path}，request_id={requestTrace}");
            CancellationTokenSource timeoutCancellation = null;
            CancellationTokenSource linkedCancellation = null;
            try
            {
                var requestToken = cancellationToken;
                if (timeoutMs > 0)
                {
                    timeoutCancellation = new CancellationTokenSource(timeoutMs);
                    if (cancellationToken.CanBeCanceled)
                    {
                        linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken, timeoutCancellation.Token);
                        requestToken = linkedCancellation.Token;
                    }
                    else
                    {
                        requestToken = timeoutCancellation.Token;
                    }
                }

                using (var content = new StringContent(bodyUtf8, Encoding.UTF8, "application/json"))
                using (var response = await _httpClient.PostAsync(url, content, requestToken).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    sw.Stop();

                    var statusCode = (int)response.StatusCode;
                    if (response.IsSuccessStatusCode &&
                        (expectedStatusCode <= 0 || statusCode == expectedStatusCode))
                    {
                        if (sw.ElapsedMilliseconds > 500)
                            Logger.Warn($"[终端请求] POST响应较慢：路径={path}，request_id={requestTrace}，" +
                                        $"状态={(int)response.StatusCode}，耗时={sw.ElapsedMilliseconds}ms");
                        else
                            Logger.Debug($"[终端请求] POST完成：路径={path}，request_id={requestTrace}，" +
                                         $"状态={(int)response.StatusCode}，耗时={sw.ElapsedMilliseconds}ms，结果=成功");
                        return (true, responseBody);
                    }

                    if (response.IsSuccessStatusCode && expectedStatusCode > 0)
                    {
                        Logger.TryLogRateLimited(
                            "terminal|POST|" + path + "|unexpected_status_" + statusCode,
                            LogModules.TerminalCommunication,
                            isRecoveryAttempt ? "调试" : "警告",
                            "POST返回非预期状态码：" +
                            Logger.FormatContextMessage("POST " + path,
                                requestId: requestTrace, result: "失败",
                                errorCode: "unexpected_status_" + statusCode,
                                durationMs: sw.ElapsedMilliseconds) +
                            " ExpectedStatus=" + expectedStatusCode +
                            " Response=" + Logger.SanitizeLargePayloadForLog(
                                responseBody, requestTrace));
                        return (false, responseBody);
                    }

                    Logger.TryLogRateLimited(
                        "terminal|POST|" + path + "|http_" + statusCode,
                        LogModules.TerminalCommunication,
                        isRecoveryAttempt ? "调试" : "警告",
                        "POST失败：" +
                        Logger.FormatContextMessage("POST " + path,
                            requestId: requestTrace, result: "失败",
                            errorCode: "http_" + statusCode,
                            durationMs: sw.ElapsedMilliseconds) +
                        " Response=" + Logger.SanitizeLargePayloadForLog(
                            responseBody, requestTrace));
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                if (cancellationToken.IsCancellationRequested)
                    Logger.Warn($"[终端请求] POST因终端批次失效而取消：路径={path}，request_id={requestTrace}，" +
                                $"耗时={sw.ElapsedMilliseconds}ms");
                else
                {
                var timeoutText = timeoutMs > 0 ? timeoutMs + "ms" : _httpClient.Timeout.TotalSeconds + "s";
                Logger.TryLogRateLimited(
                    "terminal|POST|" + path + "|timeout",
                    LogModules.TerminalCommunication,
                    isRecoveryAttempt ? "调试" : "错误",
                    "POST超时：" + Logger.FormatContextMessage("POST " + path,
                        requestId: requestTrace, result: "失败", errorCode: "timeout",
                        durationMs: sw.ElapsedMilliseconds) +
                    " Timeout=" + timeoutText);
                }
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                Logger.TryLogRateLimited(
                    "terminal|POST|" + path + "|network_error",
                    LogModules.TerminalCommunication,
                    isRecoveryAttempt ? "调试" : "错误",
                    "POST网络错误：" + Logger.FormatContextMessage("POST " + path,
                        requestId: requestTrace, result: "失败", errorCode: "network_error",
                        durationMs: sw.ElapsedMilliseconds) +
                    " Error=" + JsonHelper.ToLogValue(ex.Message));
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.TryLogRateLimited(
                    "terminal|POST|" + path + "|exception",
                    LogModules.TerminalCommunication,
                    isRecoveryAttempt ? "调试" : "错误",
                    "POST异常：" + Logger.FormatContextMessage("POST " + path,
                        requestId: requestTrace, result: "失败", errorCode: "exception",
                        durationMs: sw.ElapsedMilliseconds) +
                    " Error=" + JsonHelper.ToLogValue(ex.Message));
                return (false, "{\"error\":true,\"code\":\"network_error\",\"message\":\"" + JsonHelper.EscapeString(ex.Message) + "\"}");
            }
            finally
            {
                linkedCancellation?.Dispose();
                timeoutCancellation?.Dispose();
            }
        }

        public async Task<(bool ok, string response)> GetJsonAsync(string baseUrl, string path,
            int timeoutMs = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var url = baseUrl.TrimEnd('/') + path;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CancellationTokenSource timeoutCancellation = null;
            CancellationTokenSource linkedCancellation = null;
            try
            {
                var requestToken = cancellationToken;
                if (timeoutMs > 0)
                {
                    timeoutCancellation = new CancellationTokenSource(timeoutMs);
                    if (cancellationToken.CanBeCanceled)
                    {
                        linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken, timeoutCancellation.Token);
                        requestToken = linkedCancellation.Token;
                    }
                    else
                    {
                        requestToken = timeoutCancellation.Token;
                    }
                }

                using (var response = await _httpClient.GetAsync(url, requestToken).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                            Logger.Warn($"[终端请求] GET {path} 响应较慢：状态={(int)response.StatusCode}，耗时={sw.ElapsedMilliseconds}ms");
                        return (true, responseBody);
                    }

                    Logger.TryLogRateLimited(
                        "terminal|GET|" + path + "|http_" + (int)response.StatusCode,
                        LogModules.TerminalCommunication, "调试",
                        "GET失败：" + Logger.FormatContextMessage("GET " + path,
                            result: "失败", errorCode: "http_" + (int)response.StatusCode,
                            durationMs: sw.ElapsedMilliseconds) +
                        " Response=" + Logger.SanitizeLargePayloadForLog(responseBody));
                    return (false, responseBody);
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                if (cancellationToken.IsCancellationRequested)
                    Logger.Warn($"[终端请求] GET {path} 因终端批次失效而取消，耗时={sw.ElapsedMilliseconds}ms");
                else
                    Logger.TryLogRateLimited(
                        "terminal|GET|" + path + "|timeout",
                        LogModules.TerminalCommunication, "调试",
                        "GET超时：" + Logger.FormatContextMessage("GET " + path,
                            result: "失败", errorCode: "timeout",
                            durationMs: sw.ElapsedMilliseconds));
                return (false, "{\"error\":true,\"code\":\"timeout\"}");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                Logger.TryLogRateLimited(
                    "terminal|GET|" + path + "|network_error",
                    LogModules.TerminalCommunication, "调试",
                    "GET网络错误：" + Logger.FormatContextMessage("GET " + path,
                        result: "失败", errorCode: "network_error",
                        durationMs: sw.ElapsedMilliseconds) +
                    " Error=" + JsonHelper.ToLogValue(ex.Message));
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.TryLogRateLimited(
                    "terminal|GET|" + path + "|exception",
                    LogModules.TerminalCommunication, "调试",
                    "GET异常：" + Logger.FormatContextMessage("GET " + path,
                        result: "失败", errorCode: "exception",
                        durationMs: sw.ElapsedMilliseconds) +
                    " Error=" + JsonHelper.ToLogValue(ex.Message));
                return (false, "{\"error\":true,\"code\":\"network_error\"}");
            }
            finally
            {
                linkedCancellation?.Dispose();
                timeoutCancellation?.Dispose();
            }
        }

        public void Dispose()
        {
            try { _httpClient?.CancelPendingRequests(); } catch { }
            _httpClient?.Dispose();
        }
    }
}
