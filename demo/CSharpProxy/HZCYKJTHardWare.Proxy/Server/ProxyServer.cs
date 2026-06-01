using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Preview;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class ProxyServer : IDisposable
    {
        private TcpListener _dllListener;
        private TcpListener _callbackListener;
        private CancellationTokenSource _cts;

        // Concurrency limit: prevent thread-pool exhaustion under high-frequency DLL requests
        private readonly SemaphoreSlim _requestLimit = new SemaphoreSlim(20, 20);

        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly PreviewManager _previewManager;
        private readonly DllCommandHandler _commandHandler;
        private readonly TerminalCallbackHandler _callbackHandler;

        private readonly ConcurrentDictionary<string, string> _requestSaveDirs = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _requestCallbacks = new ConcurrentDictionary<string, string>();

        private readonly Action<string> _log;
        private string _lanIp;

        public string LanIp => _lanIp;

        public string GetTerminalCallbackBaseUrl()
        {
            return AppConfig.Instance.GetTerminalCallbackBaseUrl(_lanIp);
        }

        public ProxyServer(Action<string> log)
        {
            _log = log;
            _terminalManager = new TerminalManager();
            _terminalClient = new TerminalClient();
            _dllCallback = new DllCallbackSender();
            _previewManager = new PreviewManager(_terminalClient);

            _commandHandler = new DllCommandHandler(
                _terminalManager, _terminalClient, _dllCallback, _previewManager,
                _requestSaveDirs, _requestCallbacks, _log,
                GetTerminalCallbackBaseUrl);

            _callbackHandler = new TerminalCallbackHandler(
                _terminalClient, _dllCallback,
                _requestSaveDirs, _requestCallbacks, _log);
        }

        public void Start()
        {
            var cfg = AppConfig.Instance;
            _cts = new CancellationTokenSource();

            _lanIp = NetworkDetector.DetectLanIp(cfg.SubnetPrefix);

            // DLL command server (larger backlog for high-frequency request bursts)
            _dllListener = new TcpListener(IPAddress.Parse(cfg.DllServerHost), cfg.DllServerPort);
            _dllListener.Start(200);  // Backlog: 200 pending connections before TCP RST
            _log($"DLL 服务监听: {cfg.DllServerHost}:{cfg.DllServerPort}");

            // Terminal callback server
            var callbackIp = cfg.CallbackListenHost == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(cfg.CallbackListenHost);
            _callbackListener = new TcpListener(callbackIp, cfg.CallbackListenPort);
            _callbackListener.Start(50);
            _log($"回调服务监听: {cfg.CallbackListenHost}:{cfg.CallbackListenPort}");

            // ServicePoint connection lease: recycle connections every 60s to prevent stale connections
            // after network interruptions. Default is infinite (connections never recycled).
            ServicePointManager.MaxServicePointIdleTime = 60000;  // 60s idle timeout
            ServicePointManager.DnsRefreshTimeout = 120000;       // 2min DNS refresh

            Task.Run(() => AcceptLoop(_dllListener, HandleDllRequest, _cts.Token));
            Task.Run(() => AcceptLoop(_callbackListener, HandleCallbackRequest, _cts.Token));

            // VLC warmup: pre-load VLC to reduce first-playback latency (same as Delphi TVlcWarmupThread)
            // IMPORTANT: Keep DLLs loaded after warmup — do NOT FreeLibrary, or libvlc_new will fail on reload
            Task.Run(() =>
            {
                _log("[VLC预热] 正在启动...");
                var warmupPlayer = new Preview.VlcPreviewPlayer();
                try
                {
                    warmupPlayer.Warmup();
                    _log($"[VLC预热] 已完成, 耗时={warmupPlayer.WarmupMs}ms");
                }
                finally
                {
                    // Only release VLC instance, keep DLL handles loaded (no FreeLibrary)
                    warmupPlayer.StopKeepDlls();
                }
            });

            _log($"服务已启动。本机IP: {_lanIp}, 当前终端: {_terminalManager.CurrentName} ({_terminalManager.CurrentBaseUrl})");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _previewManager?.StopAll();

            try { _dllListener?.Stop(); } catch { }
            try { _callbackListener?.Stop(); } catch { }

            // Wait for in-flight requests to complete (up to 5s)
            try
            {
                var waitMs = 0;
                while (_requestLimit.CurrentCount < 20 && waitMs < 5000)
                {
                    System.Threading.Thread.Sleep(100);
                    waitMs += 100;
                }
            }
            catch { }

            _log("服务已停止");
        }

        private async Task AcceptLoop(TcpListener listener, Func<TcpClient, Task> handler, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();

                    // Acquire concurrency slot before spawning task (prevents thread-pool exhaustion)
                    await _requestLimit.WaitAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await handler(client);
                        }
                        catch (Exception ex)
                        {
                            _log($"Request handler error: {ex.Message}");
                        }
                        finally
                        {
                            _requestLimit.Release();
                        }
                    }, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log($"AcceptLoop error: {ex.Message}");
                }
            }
        }

        private async Task HandleDllRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;

                    var (method, path, bodyUtf8) = await ReadHttpRequest(stream);

                    _log($"[DLL请求] {method} {path}");

                    var result = await _commandHandler.HandleAsync(method, path, bodyUtf8);

                    await WriteHttpResponse(stream, 200, result);
                }

                // Drain queued connections to prevent backlog overflow (same as Delphi connection drain)
                DrainQueuedConnections(_dllListener);
            }
            catch (Exception ex)
            {
                _log($"HandleDllRequest error: {ex.Message}");
            }
        }

        /// <summary>
        /// Drain queued TCP connections by returning 503 Service Busy.
        /// Same behavior as Delphi's non-blocking accept drain loop.
        /// </summary>
        private void DrainQueuedConnections(TcpListener listener)
        {
            try
            {
                while (listener.Pending())
                {
                    var drainClient = listener.AcceptTcpClient();
                    Task.Run(() =>
                    {
                        try
                        {
                            using (drainClient)
                            using (var stream = drainClient.GetStream())
                            {
                                var resp = "HTTP/1.1 503 Service Busy\r\n" +
                                    "Content-Type: application/json; charset=utf-8\r\n" +
                                    "Content-Length: 25\r\n" +
                                    "Connection: close\r\n\r\n" +
                                    "{\"error\":true,\"code\":\"busy\"}";
                                var respBytes = System.Text.Encoding.UTF8.GetBytes(resp);
                                stream.Write(respBytes, 0, respBytes.Length);
                                stream.Flush();
                            }
                        }
                        catch { /* Ignore drain errors */ }
                    });
                }
            }
            catch { /* Ignore drain errors */ }
        }

        private async Task HandleCallbackRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;

                    var (_, path, bodyUtf8) = await ReadHttpRequest(stream);

                    _log($"[终端回调] {path}");

                    var result = _callbackHandler.Handle(bodyUtf8);

                    await WriteHttpResponse(stream, 202, result);  // 202 Accepted, same as Delphi
                }
            }
            catch (Exception ex)
            {
                _log($"HandleCallbackRequest error: {ex.Message}");
            }
        }

        private static async Task<(string method, string path, string body)> ReadHttpRequest(NetworkStream stream)
        {
            var headerBuilder = new StringBuilder();
            var buf = new byte[1];
            int contentLength = 0;
            string method = "GET";
            string path = "/";

            // Read headers until \r\n\r\n
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buf, 0, 1);
                if (bytesRead == 0) break;
                headerBuilder.Append((char)buf[0]);

                var headerStr = headerBuilder.ToString();
                if (headerStr.EndsWith("\r\n\r\n"))
                {
                    // Parse Content-Length
                    var lines = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                        }
                    }
                    // Parse method and path from first line
                    var firstLine = lines[0];
                    var parts = firstLine.Split(' ');
                    if (parts.Length >= 2)
                    {
                        method = parts[0];
                        path = parts[1];
                    }
                    break;
                }
            }

            // Read body
            string body = "";
            if (contentLength > 0)
            {
                var bodyBuf = new byte[contentLength];
                int totalRead = 0;
                while (totalRead < contentLength)
                {
                    int read = await stream.ReadAsync(bodyBuf, totalRead, contentLength - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                body = Encoding.UTF8.GetString(bodyBuf, 0, totalRead);
            }

            return (method, path, body);
        }

        private static async Task WriteHttpResponse(NetworkStream stream, int statusCode, string body)
        {
            var statusText = statusCode == 200 ? "OK" : statusCode == 202 ? "Accepted" : "Error";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            await stream.FlushAsync();
        }

        // --- Direct operations (called from UI) ---

        public string StartProcess(string saveDir)
        {
            var callbackBase = GetTerminalCallbackBaseUrl();
            var requestId = "PROCESS_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"," +
                $"\"callbacks\":{{" +
                $"\"ocr_document\":\"{callbackBase}\"," +
                $"\"ocr_event_status\":\"{callbackBase}\"," +
                $"\"nfc_card\":\"{callbackBase}\"}}}}";

            _terminalManager.ProcessSaveDir = Storage.PathHelper.SafeResolveSaveDir(saveDir);
            _log("[流程] 正在向终端开始流程，url=" + _terminalManager.CurrentBaseUrl + "/process/start，save_dir=" + _terminalManager.ProcessSaveDir);

            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/process/start", body);
            task.Wait();
            var (ok, _) = task.Result;
            if (ok)
            {
                _terminalManager.ProcessActive = true;
                _log("[流程] 终端流程已开始，save_dir=" + _terminalManager.ProcessSaveDir);
                return "OK";
            }
            return "Failed";
        }

        public string EndProcess()
        {
            _terminalManager.ProcessActive = false;
            _terminalManager.ProcessSaveDir = "";
            _requestSaveDirs.Clear();
            _requestCallbacks.Clear();
            _log("[流程] 流程已结束");
            return "OK";
        }

        public string SwitchTerminal(int index)
        {
            if (_terminalManager.IsSameTerminal(index))
                return $"已在目标终端，无需切换";

            // Run switch with performance timing (matching HandleTerminalSwitch)
            Task.Run(async () =>
            {
                try
                {
                    var stopWatch = System.Diagnostics.Stopwatch.StartNew();

                    _log("[终端切换] 正在切换到终端" + _terminalManager.CurrentIndex + " -> 终端" + index);
                    _previewManager.StopAll();
                    _log(string.Format("[性能] 终端切换停止 耗时={0}毫秒", stopWatch.ElapsedMilliseconds));

                    var phaseTick = stopWatch.ElapsedMilliseconds;
                    _terminalManager.SwitchTo(index);
                    _log(string.Format("[性能] 终端管理器切换 耗时={0}毫秒", stopWatch.ElapsedMilliseconds - phaseTick));
                    _log("[终端切换] 当前终端已切换为：" + _terminalManager.CurrentName + " " + _terminalManager.CurrentBaseUrl);

                    phaseTick = stopWatch.ElapsedMilliseconds;
                    _log("[终端切换] 正在" + _terminalManager.CurrentName + "上恢复活动预览");
                    await _previewManager.RestartPreviewsOnTerminalSwitch(_terminalManager.CurrentBaseUrl);
                    _log(string.Format("[性能] 终端切换启动 耗时={0}毫秒", stopWatch.ElapsedMilliseconds - phaseTick));
                    _log(string.Format("[性能] 终端切换总耗时={0}毫秒", stopWatch.ElapsedMilliseconds));
                }
                catch (Exception ex)
                {
                    _log($"终端切换失败: {ex.Message}");
                }
            });
            return $"已切换到终端 {index}";
        }

        public (bool ok, string path) CaptureFace(string saveDir)
        {
            var requestId = "FACE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/face-image/sync-request", body);
            task.Wait();
            var (ok, response) = task.Result;
            if (!ok) return (false, "");

            var savePath = ResultParser.ExtractSavePath(response);
            if (string.IsNullOrEmpty(savePath))
            {
                var result = CallbackParser.ParseImageCapture(response, "face_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                {
                    var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/bmp";
                    savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "face");
                }
            }
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        public (bool ok, string path) CaptureFingerprint(string saveDir)
        {
            var requestId = "FP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/fingerprint/sync-request", body);
            task.Wait();
            var (ok, response) = task.Result;
            if (!ok) return (false, "");

            var savePath = ResultParser.ExtractSavePath(response);
            if (string.IsNullOrEmpty(savePath))
            {
                var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                {
                    var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/jpeg";
                    savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "fingerprint");
                }
            }
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        public string RequestOCR(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/ocr-document/request", body);
            task.Wait();
            return requestId;
        }

        public string RequestNfc(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/nfc-card/request", body);
            task.Wait();
            return requestId;
        }

        public string CaptureIris(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/iris/request", body);
            task.Wait();
            return requestId;
        }

        public string RequestAuthorize(string idNo, string docType, string nationality, string name, string sex, string birthday)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"," +
                $"\"name\":\"{JsonHelper.EscapeString(name)}\",\"sex\":\"{JsonHelper.EscapeString(sex)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"birthday\":\"{JsonHelper.EscapeString(birthday)}\",\"nationality\":\"{JsonHelper.EscapeString(nationality)}\"}}";

            var task = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/protocol/request", body);
            task.Wait();
            return requestId;
        }

        public async Task<bool> StartLocalPreviewAsync(string resourceType, System.Windows.Forms.Control panel)
        {
            var resType = resourceType switch
            {
                "camera" => PreviewResourceType.Camera,
                "fingerprint" => PreviewResourceType.Fingerprint,
                "iris" => PreviewResourceType.Iris,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };

            return await _previewManager.StartPreview(resType, PreviewSessionType.Local,
                IntPtr.Zero, _terminalManager.CurrentBaseUrl, panel);
        }

        public void StopLocalPreview(string resourceType)
        {
            var resType = resourceType switch
            {
                "camera" => PreviewResourceType.Camera,
                "fingerprint" => PreviewResourceType.Fingerprint,
                "iris" => PreviewResourceType.Iris,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };

            _previewManager.StopPreview(resType, PreviewSessionType.Local);
        }

        public void Dispose()
        {
            Stop();
            _previewManager?.Dispose();
            _terminalClient?.Dispose();
            _dllCallback?.Dispose();
            _cts?.Dispose();
            _requestLimit?.Dispose();
        }
    }
}
