using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Parsing;

namespace HZCYKJTHardWare.Proxy.Tests
{
    /// <summary>
    /// Simple mock terminal server that simulates the采集终端 (collection terminal).
    /// Used for integration tests without needing the real terminal hardware.
    /// </summary>
    public class MockTerminalServer : IDisposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly Action<string> _log;

        public string BaseUrl { get; private set; }
        public int Port { get; private set; }
        public bool IsRunning { get; private set; }

        // Callbacks for test assertions
        public string LastOcrRequestId { get; private set; }
        public string LastNfcRequestId { get; private set; }
        public string LastIrisRequestId { get; private set; }
        public string LastAuthorizeRequestId { get; private set; }
        public string LastProcessRequestId { get; private set; }
        public string LastProcessEndRequestId { get; private set; }

        // Configurable responses
        public string OcrCallbackBody { get; set; }
        public string NfcCallbackBody { get; set; }
        public string IrisCallbackBody { get; set; }
        public string AuthorizeResponseBody { get; set; }
        public string FaceCaptureResponseBody { get; set; }
        public string FingerprintCaptureResponseBody { get; set; }
        public string ProcessStartResponseBody { get; set; }
        public string ProcessEndResponseBody { get; set; }
        public int ProcessEndStatusCode { get; set; } = 202;

        // Control flags
        public bool SimulateTerminalUnreachable { get; set; }
        public bool AutoSendCallback { get; set; } = true;
        public string ProxyCallbackBaseUrl { get; set; }

        // Event: raised when terminal receives a request and needs to send a callback
        public event Action<string, string, string> OnAsyncRequestReceived; // requestId, resourceType, callbackUrl

        public MockTerminalServer(Action<string> log = null)
        {
            _log = log ?? (msg => { });
        }

        public void Start()
        {
            Port = FindFreePort();
            BaseUrl = $"http://127.0.0.1:{Port}";
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            IsRunning = true;
            _listenTask = Task.Run(() => AcceptLoop(_cts.Token));
            _log($"[Mock终端] 启动于 {BaseUrl}");
        }

        private int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (ct.IsCancellationRequested) break;
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) { break; }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private async void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var path = request.Url.AbsolutePath;

            try
            {
                if (SimulateTerminalUnreachable)
                {
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                    return;
                }

                // Read body
                string body = "";
                if (request.HasEntityBody)
                {
                    using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        body = await reader.ReadToEndAsync();
                    }
                }

                string requestId = JsonHelper.ExtractString(body, "request_id");

                string responseBody;
                var responseStatusCode = 200;
                switch (path)
                {
                    case "/ping":
                        responseBody = "{\"status\":\"ok\"}";
                        break;

                    case "/process/start":
                        LastProcessRequestId = requestId;
                        responseBody = ProcessStartResponseBody
                            ?? "{\"status\":\"accepted\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"}";
                        break;

                    case "/process/end":
                        LastProcessEndRequestId = requestId;
                        responseStatusCode = ProcessEndStatusCode;
                        responseBody = ProcessEndResponseBody
                            ?? "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                               "\",\"status\":\"accepted\",\"message\":\"flow ended\"}";
                        break;

                    case "/resources/ocr-document/request":
                        LastOcrRequestId = requestId;
                        OnAsyncRequestReceived?.Invoke(requestId, "ocr_document",
                            JsonHelper.ExtractString(body, "callback_url"));
                        responseBody = "{\"status\":\"accepted\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"}";
                        break;

                    case "/resources/nfc-card/request":
                        LastNfcRequestId = requestId;
                        OnAsyncRequestReceived?.Invoke(requestId, "nfc_card",
                            JsonHelper.ExtractString(body, "callback_url"));
                        responseBody = "{\"status\":\"accepted\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"}";
                        break;

                    case "/resources/iris/request":
                        LastIrisRequestId = requestId;
                        OnAsyncRequestReceived?.Invoke(requestId, "iris_image",
                            JsonHelper.ExtractString(body, "callback_url"));
                        responseBody = "{\"status\":\"accepted\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"}";
                        break;

                    case "/resources/protocol/request":
                        LastAuthorizeRequestId = requestId;
                        OnAsyncRequestReceived?.Invoke(requestId, "protocol",
                            JsonHelper.ExtractString(body, "callback_url"));
                        responseBody = AuthorizeResponseBody
                            ?? "{\"status\":\"accepted\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\"}";
                        break;

                    case "/resources/face-image/sync-request":
                        responseBody = FaceCaptureResponseBody
                            ?? "{\"status\":\"ok\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\",\"save_path\":\"C:\\\\captures\\\\face.jpg\"}";
                        break;

                    case "/resources/fingerprint/sync-request":
                        responseBody = FingerprintCaptureResponseBody
                            ?? "{\"status\":\"ok\",\"request_id\":\"" + JsonHelper.EscapeString(requestId) + "\",\"save_path\":\"C:\\\\captures\\\\fingerprint.jpg\"}";
                        break;

                    default:
                        responseBody = "{\"error\":true,\"code\":\"not_found\"}";
                        break;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.StatusCode = responseStatusCode;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                _log($"[Mock终端] 处理异常: {ex.Message}");
                try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
            }
        }

        /// <summary>
        /// Simulate sending a callback from terminal back to the proxy.
        /// </summary>
        public async Task<bool> SendCallback(string requestId, string resourceType, string body)
        {
            if (string.IsNullOrEmpty(ProxyCallbackBaseUrl))
                return false;

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
                    client.Encoding = Encoding.UTF8;
                    var url = ProxyCallbackBaseUrl.TrimEnd('/') + "/terminal-callback";
                    await client.UploadStringTaskAsync(url, "POST", body);
                    _log($"[Mock终端] 回调已发送: request_id={requestId}, resource_type={resourceType}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log($"[Mock终端] 回调发送失败: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            IsRunning = false;
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            _listener?.Close();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
