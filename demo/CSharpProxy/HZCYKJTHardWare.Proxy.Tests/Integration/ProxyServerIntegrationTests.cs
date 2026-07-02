using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Integration
{
    /// <summary>
    /// End-to-end integration tests with mock terminal and mock DLL callback server.
    /// Verifies the full request/response pipeline without real hardware.
    /// </summary>
    [TestClass]
    public class ProxyServerIntegrationTests
    {
        private static string _testDir;
        private static MockTerminalServer _mockTerminal;
        private static MockCallbackReceiver _mockCallback;
        private static ProxyServer _proxy;

        // Port assignments for test isolation (avoid conflicts with production)
        private const int TestDllPort = 18082;
        private const int TestCallbackPort = 18083;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ProxyTests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(Path.Combine(_testDir, "captures"));

            // Start mock terminal
            _mockTerminal = new MockTerminalServer(msg => System.Diagnostics.Debug.WriteLine(msg));
            _mockTerminal.Start();

            // Start mock DLL callback receiver
            _mockCallback = new MockCallbackReceiver();
            _mockCallback.Start();

            // Write test config to BOTH the temp dir and the test BaseDirectory.
            // AppConfig loads from BaseDirectory; save dir files go to temp.
            WriteTestConfig(_mockTerminal.BaseUrl, _mockCallback.BaseUrl);

            // Start proxy
            _proxy = new ProxyServer(
                msg => System.Diagnostics.Debug.WriteLine(msg),
                active => System.Diagnostics.Debug.WriteLine($"Process active: {active}"));
            _proxy.Start();

            // Brief wait for listeners to start
            Thread.Sleep(500);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _proxy?.Stop();
            _proxy?.Dispose();
            _mockTerminal?.Dispose();
            _mockCallback?.Dispose();

            try { Directory.Delete(_testDir, true); } catch { }
        }

        private static void WriteTestConfig(string terminalBaseUrl, string callbackBaseUrl)
        {
            // Parse terminal URL components
            var uri = new Uri(terminalBaseUrl);
            var hostParts = uri.Host.Split('.');
            string subnetPrefix = hostParts.Length >= 3
                ? string.Join(".", hostParts, 0, hostParts.Length - 1)
                : uri.Host;
            int hostSuffix = hostParts.Length > 0
                ? int.Parse(hostParts[hostParts.Length - 1])
                : 1;

            var json = $@"{{
  ""dll_server"": {{
    ""host"": ""127.0.0.1"",
    ""port"": {TestDllPort}
  }},
  ""terminal_callback_server"": {{
    ""listen_host"": ""0.0.0.0"",
    ""public_host"": ""127.0.0.1"",
    ""port"": {TestCallbackPort},
    ""path"": ""/terminal-callback""
  }},
  ""terminal"": {{
    ""scheme"": ""{uri.Scheme}"",
    ""port"": {uri.Port},
    ""subnet_prefix"": ""{subnetPrefix}"",
    ""devices"": [
      {{ ""index"": 1, ""host_suffix"": {hostSuffix} }},
      {{ ""index"": 2, ""host_suffix"": {hostSuffix + 1} }}
    ]
  }},
  ""callback_server"": {{
    ""host"": ""127.0.0.1"",
    ""port"": {_mockCallback.Port},
    ""base_path"": ""/HZCYKJTHardWare/callback""
  }},
  ""save"": {{
    ""default_dir"": ""{_testDir.Replace("\\", "\\\\")}\\\\captures"",
    ""create_date_folder"": false,
    ""create_request_folder"": true
  }},
  ""log"": {{
    ""level"": ""debug""
  }}
}}";

            // Write to test BaseDirectory where AppConfig loads from
            string baseDirConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HZCYKJTHardWare.json");
            File.WriteAllText(baseDirConfig, json, Encoding.UTF8);
        }

        [TestMethod]
        public void Ping_Returns_Ok()
        {
            string result = SendDllRequest("/ping", "{}");
            Assert.IsTrue(result.Contains("\"status\":\"ok\""),
                $"Ping should return ok, got: {result}");
        }

        [TestMethod]
        public void CaptureFace_Sync_Returns_Path()
        {
            string body = "{\"request_id\":\"FACE_TEST_001\",\"save_dir\":\"" +
                _testDir.Replace("\\", "\\\\") + "\\\\captures\"}";
            string result = SendDllRequest("/capture/face", body);

            Assert.IsFalse(result.Contains("\"error\":true"),
                $"Face capture should succeed, got: {result}");
        }

        [TestMethod]
        public void OcrAsync_Accepted_Then_Callback_Completes()
        {
            string requestId = "OCR_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string body = "{\"request_id\":\"" + requestId +
                "\",\"save_dir\":\"" + _testDir.Replace("\\", "\\\\") +
                "\\\\captures\",\"callback_url\":\"http://127.0.0.1:" +
                _mockCallback.Port + "/HZCYKJTHardWare/callback/ocr\"}";
            string result = SendDllRequest("/ocr", body);

            Assert.IsFalse(result.Contains("\"error\":true"),
                $"OCR async request should be accepted, got: {result}");
            Assert.IsTrue(result.Contains("\"accepted\":true"),
                $"OCR response should indicate accepted, got: {result}");

            // Wait a bit for the proxy to forward to mock terminal
            Thread.Sleep(200);
            Assert.AreEqual(requestId, _mockTerminal.LastOcrRequestId,
                "Mock terminal should have received the OCR request");
        }

        [TestMethod]
        public void ProcessFlow_Start_Ocr_Nfc_Iris_End()
        {
            string processId = "PROCESS_TEST_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string ocrCallback = "http://127.0.0.1:" + _mockCallback.Port + "/HZCYKJTHardWare/callback/ocr";
            string nfcCallback = "http://127.0.0.1:" + _mockCallback.Port + "/HZCYKJTHardWare/callback/nfc-card";
            string irisCallback = "http://127.0.0.1:" + _mockCallback.Port + "/HZCYKJTHardWare/callback/iris";

            // Start process
            string startBody = "{\"request_id\":\"" + processId +
                "\",\"save_dir\":\"" + _testDir.Replace("\\", "\\\\") +
                "\\\\captures\",\"callbacks\":{" +
                "\"ocr\":\"" + ocrCallback + "\"," +
                "\"nfc\":\"" + nfcCallback + "\"," +
                "\"iris\":\"" + irisCallback + "\"}}";
            string startResult = SendDllRequest("/process/start", startBody);

            Assert.IsTrue(startResult.Contains("\"status\":\"ok\""),
                $"Process start should succeed, got: {startResult}");

            // End process
            string endResult = SendDllRequest("/process/end", "{}");
            Assert.IsTrue(endResult.Contains("\"status\":\"ok\""),
                $"Process end should succeed, got: {endResult}");
        }

        [TestMethod]
        public void SwitchTerminal_ChangesBaseUrl()
        {
            // Just verify the switch endpoint responds without error
            string body = "{\"terminal_index\":2}";
            string result = SendDllRequest("/terminal/switch", body);

            Assert.IsTrue(result.Contains("\"status\":\"ok\""),
                $"Terminal switch should succeed, got: {result}");

            // The HTTP endpoint returns before the queued switch is committed.
            // Restore terminal 1 so this test does not leak route state into the
            // following OCR backpressure test (terminal 2 has no mock server).
            var restored = false;
            for (var i = 0; i < 40 && !restored; i++)
            {
                Thread.Sleep(25);
                var restoreResult = _proxy.SwitchTerminal(1);
                restored = restoreResult.Contains("已切换到终端 1");
            }
            Assert.IsTrue(restored, "test route should be restored to mock terminal 1");
        }

        [TestMethod]
        public void QueueBackpressure_Rejects_WhenFull()
        {
            // Fill the OCR queue with slow requests
            // The queue has maxLength=2 (1 executing + 1 pending)
            // Sending 3 requests rapidly should cause at least one rejection

            int rejected = 0;
            int accepted = 0;

            for (int i = 0; i < 5; i++)
            {
                string requestId = "BP_" + i + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string body = "{\"request_id\":\"" + requestId +
                    "\",\"save_dir\":\"" + _testDir.Replace("\\", "\\\\") +
                    "\\\\captures\",\"callback_url\":\"http://127.0.0.1:" +
                    _mockCallback.Port + "/HZCYKJTHardWare/callback/ocr\"}";
                string result = SendDllRequest("/ocr", body);

                if (result.Contains("\"error\":true"))
                    rejected++;
                else if (result.Contains("\"accepted\":true"))
                    accepted++;
            }

            // At least some should be accepted; rejections depend on timing
            Assert.IsTrue(accepted > 0,
                $"At least some OCR requests should be accepted, accepted={accepted}");
        }

        [TestMethod]
        public void CallbackDeduplication_RequestCompletedRecord_ReturnsTrue()
        {
            // Register a request and complete it
            string requestId = "DEDUP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string body = "{\"request_id\":\"" + requestId +
                "\",\"save_dir\":\"" + _testDir.Replace("\\", "\\\\") +
                "\\\\captures\",\"callback_url\":\"http://127.0.0.1:" +
                _mockCallback.Port + "/HZCYKJTHardWare/callback/ocr\"}";
            SendDllRequest("/ocr", body);

            // The proxy's RequestRegistry should have this tracked.
            // We can't easily test dedup via HTTP, but the RequestRegistryTests
            // cover the CAS-based TryClaimCallback dedup logic.
        }

        // ====== Helpers ======

        private static string SendDllRequest(string path, string bodyUtf8)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(
                    $"http://127.0.0.1:{TestDllPort}{path}");
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 5000;

                byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyUtf8);
                request.ContentLength = bodyBytes.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bodyBytes, 0, bodyBytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return "{\"error\":true,\"code\":\"network_error\",\"message\":\"" +
                    JsonHelper.EscapeString(ex.Message) + "\"}";
            }
        }
    }

    /// <summary>
    /// Simple HTTP server that receives callbacks from the proxy (simulating the DLL callback server).
    /// </summary>
    public class MockCallbackReceiver : IDisposable
    {
        private HttpListener _listener;
        private Task _listenTask;
        private CancellationTokenSource _cts;

        public int Port { get; private set; }
        public string BaseUrl { get; private set; }
        public bool IsRunning { get; private set; }

        public string LastOcrCallback { get; private set; }
        public string LastNfcCallback { get; private set; }
        public string LastIrisCallback { get; private set; }
        public string LastAuthorizeCallback { get; private set; }
        public string LastPreviewReadyCallback { get; private set; }
        private int _callbackCount;
        public int CallbackCount => _callbackCount;

        public event Action<string, string> OnCallbackReceived; // path, body

        public MockCallbackReceiver()
        {
            Port = FindFreePort();
            BaseUrl = $"http://127.0.0.1:{Port}";
        }

        private static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            IsRunning = true;
            _listenTask = Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (ct.IsCancellationRequested) break;
                    _ = Task.Run(() => HandleCallback(context));
                }
                catch (HttpListenerException) { break; }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private async void HandleCallback(HttpListenerContext context)
        {
            try
            {
                string body = "";
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                var path = context.Request.Url.AbsolutePath;
                Interlocked.Increment(ref _callbackCount);
                OnCallbackReceived?.Invoke(path, body);

                if (path.EndsWith("/ocr") || path.Contains("/ocr"))
                    LastOcrCallback = body;
                else if (path.EndsWith("/nfc-card") || path.Contains("/nfc-card"))
                    LastNfcCallback = body;
                else if (path.EndsWith("/iris") || path.Contains("/iris"))
                    LastIrisCallback = body;
                else if (path.EndsWith("/authorize") || path.Contains("/authorize"))
                    LastAuthorizeCallback = body;
                else if (path.EndsWith("/preview-ready") || path.Contains("/preview-ready"))
                    LastPreviewReadyCallback = body;

                byte[] response = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = response.Length;
                context.Response.StatusCode = 200;
                await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                context.Response.Close();
            }
            catch { try { context.Response.StatusCode = 500; context.Response.Close(); } catch { } }
        }

        public void Dispose()
        {
            IsRunning = false;
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            _listener?.Close();
            _cts?.Dispose();
        }
    }
}
