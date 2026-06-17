using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
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
        private readonly SemaphoreSlim _dllRequestSlots = new SemaphoreSlim(64, 64);
        private readonly SemaphoreSlim _callbackRequestSlots = new SemaphoreSlim(32, 32);

        private readonly TerminalManager _terminalManager;
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly PreviewManager _previewManager;
        private readonly DllCommandHandler _commandHandler;
        private readonly TerminalCallbackHandler _callbackHandler;
        private readonly QueueManager _queueManager;

        private readonly ConcurrentDictionary<string, string> _requestSaveDirs = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _requestCallbacks = new ConcurrentDictionary<string, string>();

        private readonly Action<string> _log;
        private string _lanIp;

        public string LanIp => _lanIp;

        public class AuthorizeRequestResult
        {
            public bool Ok { get; set; }
            public string RequestId { get; set; }
            public string Message { get; set; }
        }
        public QueueManager QueueManager => _queueManager;

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
            _queueManager = new QueueManager();

            // Wire up queue worker handlers
            _queueManager.SwitchHandler = ExecuteSwitchInternal;
            _queueManager.FaceCaptureHandler = (task) => ExecuteCaptureFace(task);
            _queueManager.FingerprintCaptureHandler = (task) => ExecuteCaptureFingerprint(task);
            _queueManager.OcrHandler = (task) => ExecuteOcrInternal(task);
            _queueManager.NfcHandler = (task) => ExecuteNfcInternal(task);
            _queueManager.FacePreviewHandler = (task) => ExecuteFacePreview(task);
            _queueManager.FingerprintPreviewHandler = (task) => ExecuteFingerprintPreview(task);
            _queueManager.MiscHandler = (task) => ExecuteMiscInternal(task);

            _commandHandler = new DllCommandHandler(
                _terminalManager, _terminalClient, _dllCallback, _previewManager,
                _requestSaveDirs, _requestCallbacks, _log,
                GetTerminalCallbackBaseUrl, _queueManager);

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

            Task.Run(() => AcceptLoop(_dllListener, HandleDllRequest, _dllRequestSlots, _cts.Token));
            Task.Run(() => AcceptLoop(_callbackListener, HandleCallbackRequest, _callbackRequestSlots, _cts.Token));

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
            _queueManager?.Dispose();

            try { _dllListener?.Stop(); } catch { }
            try { _callbackListener?.Stop(); } catch { }

            _log("服务已停止");
            _log("[队列统计]\n" + (_queueManager?.GetAllStats() ?? "无"));
        }

        private async Task AcceptLoop(TcpListener listener, Func<TcpClient, Task> handler, SemaphoreSlim slots, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    if (!slots.Wait(0))
                    {
                        _ = Task.Run(() => RejectBusyClient(client));
                        continue;
                    }

                    _ = Task.Run(async () =>
                    {
                        try { await handler(client).ConfigureAwait(false); }
                        catch (Exception ex) { _log($"[服务] 请求处理异常: {ex.Message}"); }
                        finally { slots.Release(); }
                    }, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _log($"[服务] 接收连接异常: {ex.Message}"); }
            }
        }

        private static void RejectBusyClient(TcpClient client)
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

        private async Task HandleDllRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 2000;   // Fast fail: don't hang on slow clients
                    stream.WriteTimeout = 2000;

                    var (method, path, bodyUtf8) = await ReadHttpRequest(stream);

                    var result = await _commandHandler.HandleAsync(method, path, bodyUtf8);

                    await WriteHttpResponse(stream, 200, result);
                }

                // Queue pressure is handled by bounded business queues and connection slots.
                // Do not drain the TCP backlog here, otherwise valid burst requests are rejected.
            }
            catch (Exception ex)
            {
                _log($"[DLL请求] 处理异常: {ex.Message}");
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

                    _ = Task.Run(() =>
                    {
                        try { _callbackHandler.Handle(bodyUtf8); }
                        catch (Exception ex) { _log("[终端回调] 后台处理异常: " + ex.Message); }
                    });

                    await WriteHttpResponse(stream, 202, "{\"status\":\"accepted\"}");  // 202 Accepted, same as Delphi
                }
            }
            catch (Exception ex)
            {
                _log($"[终端回调] HTTP处理异常: {ex.Message}");
            }
        }

        private static async Task<(string method, string path, string body)> ReadHttpRequest(NetworkStream stream)
        {
            const int MaxHeaderBytes = 64 * 1024;
            const int MaxBodyBytes = 64 * 1024 * 1024;
            var raw = new MemoryStream();
            var buf = new byte[4096];
            var marker = Encoding.ASCII.GetBytes("\r\n\r\n");
            int headerEnd = -1;
            int contentLength = 0;
            string method = "GET";
            string path = "/";

            while (headerEnd < 0)
            {
                int bytesRead = await stream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                if (bytesRead == 0) break;
                raw.Write(buf, 0, bytesRead);
                headerEnd = IndexOf(raw.GetBuffer(), (int)raw.Length, marker);
                if (raw.Length > MaxHeaderBytes && headerEnd < 0)
                    throw new InvalidOperationException("HTTP请求头过大");
            }

            if (headerEnd < 0)
                return (method, path, "");

            var rawBytes = raw.ToArray();
            var headerSize = headerEnd + marker.Length;
            var headerStr = Encoding.ASCII.GetString(rawBytes, 0, headerSize);
            var lines = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
            }

            var firstLine = lines.Length > 0 ? lines[0] : "";
            var parts = firstLine.Split(' ');
            if (parts.Length >= 2)
            {
                method = parts[0];
                path = parts[1];
            }

            if (contentLength < 0 || contentLength > MaxBodyBytes)
                throw new InvalidOperationException("HTTP请求体大小异常");

            string body = "";
            if (contentLength > 0)
            {
                var bodyBuf = new byte[contentLength];
                var alreadyRead = Math.Min(contentLength, rawBytes.Length - headerSize);
                if (alreadyRead > 0)
                    Buffer.BlockCopy(rawBytes, headerSize, bodyBuf, 0, alreadyRead);

                int totalRead = alreadyRead;
                while (totalRead < contentLength)
                {
                    int read = await stream.ReadAsync(bodyBuf, totalRead, contentLength - totalRead).ConfigureAwait(false);
                    if (read == 0) break;
                    totalRead += read;
                }
                body = Encoding.UTF8.GetString(bodyBuf, 0, totalRead);
            }

            return (method, path, body);
        }

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

            var (ok, _) = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/process/start", body, 5000)
                .GetAwaiter().GetResult();
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
            _commandHandler.ClearAllMappings();
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
                    await _previewManager.StopAllAsync(preserveRestartInfo: true).ConfigureAwait(false);
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
            var (ok, response) = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/face-image/sync-request", body, 4500)
                .GetAwaiter().GetResult();
            if (!ok) return (false, "");

            // Delphi logic: if saveDir has file extension, save directly to that path
            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                var result = CallbackParser.ParseImageCapture(response, "face_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        Storage.PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = ResultParser.ExtractSavePath(response);
                if (string.IsNullOrEmpty(savePath))
                {
                    var result = CallbackParser.ParseImageCapture(response, "face_image");
                    if (!string.IsNullOrEmpty(result.ImageBase64))
                    {
                        var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/bmp";
                        savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "face");
                    }
                }
            }
            _log($"[人脸抓拍] 图片保存成功：{savePath}");
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        public (bool ok, string path) CaptureFingerprint(string saveDir)
        {
            var requestId = "FP_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var body = $"{{\"request_id\":\"{requestId}\"}}";
            var (ok, response) = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/fingerprint/sync-request", body, 4500)
                .GetAwaiter().GetResult();
            if (!ok) return (false, "");

            // Delphi logic: if saveDir has file extension, save directly to that path
            string savePath = "";
            if (!string.IsNullOrEmpty(saveDir) && System.IO.Path.HasExtension(saveDir))
            {
                var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                if (!string.IsNullOrEmpty(result.ImageBase64))
                    savePath = FileSaver.SaveBase64ImageToFile(result.ImageBase64,
                        Storage.PathHelper.ResolveExactSaveFile(saveDir));
            }
            else
            {
                savePath = ResultParser.ExtractSavePath(response);
                if (string.IsNullOrEmpty(savePath))
                {
                    var result = CallbackParser.ParseImageCapture(response, "fingerprint_image");
                    if (!string.IsNullOrEmpty(result.ImageBase64))
                    {
                        var mimeType = !string.IsNullOrEmpty(result.ImageMimeType) ? result.ImageMimeType : "image/jpeg";
                        savePath = FileSaver.SaveBase64Image(result.ImageBase64, mimeType, saveDir, requestId, "fingerprint");
                    }
                }
            }
            _log($"[指纹抓拍] 图片保存成功：{savePath}");
            return (!string.IsNullOrEmpty(savePath), savePath);
        }

        public string RequestOCR(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/ocr-document/request", body, 5000)
                .GetAwaiter().GetResult();
            return requestId;
        }

        public string RequestNfc(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/nfc-card/request", body, 5000)
                .GetAwaiter().GetResult();
            return requestId;
        }

        public string CaptureIris(string saveDir)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";

            _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);

            _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/iris/request", body, 5000)
                .GetAwaiter().GetResult();
            return requestId;
        }

        public AuthorizeRequestResult RequestAuthorize(string idNo, string docType, string nationality, string name, string sex, string birthday)
        {
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var cfg = AppConfig.Instance;
            var callbackBase = GetTerminalCallbackBaseUrl();
            var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"," +
                $"\"name\":\"{JsonHelper.EscapeString(name)}\",\"sex\":\"{JsonHelper.EscapeString(sex)}\"," +
                $"\"id_no\":\"{JsonHelper.EscapeString(idNo)}\",\"doc_type\":\"{JsonHelper.EscapeString(docType)}\"," +
                $"\"birthday\":\"{JsonHelper.EscapeString(birthday)}\",\"nationality\":\"{JsonHelper.EscapeString(nationality)}\"}}";

            var (ok, response) = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/protocol/request", body, 5000)
                .GetAwaiter().GetResult();
            if (ok)
            {
                _log("[授权] 已受理: request_id=" + requestId);
                return new AuthorizeRequestResult { Ok = true, RequestId = requestId, Message = "" };
            }

            var detail = ResultParser.FormatErrorDetail(response, "终端授权请求失败");
            _log("[授权] 下发失败: request_id=" + requestId + ", " + detail);
            return new AuthorizeRequestResult { Ok = false, RequestId = requestId, Message = detail };
        }

        public async Task<bool> StartLocalPreviewAsync(string resourceType, System.Windows.Forms.Control panel)
        {
            PreviewResourceType resType;
            switch (resourceType)
            {
                case "camera": resType = PreviewResourceType.Camera; break;
                case "fingerprint": resType = PreviewResourceType.Fingerprint; break;
                case "iris": resType = PreviewResourceType.Iris; break;
                default: throw new ArgumentException($"Unknown resource type: {resourceType}");
            }

            return await _previewManager.StartPreview(resType, PreviewSessionType.Local,
                IntPtr.Zero, _terminalManager.CurrentBaseUrl, panel);
        }

        public void StopLocalPreview(string resourceType)
        {
            PreviewResourceType resType;
            switch (resourceType)
            {
                case "camera": resType = PreviewResourceType.Camera; break;
                case "fingerprint": resType = PreviewResourceType.Fingerprint; break;
                case "iris": resType = PreviewResourceType.Iris; break;
                default: throw new ArgumentException($"Unknown resource type: {resourceType}");
            }

            _previewManager.StopPreview(resType, PreviewSessionType.Local);
        }

        // ====== Internal worker methods (run on queue worker threads) ======

        private ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResults
            = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        private void ExecuteSwitchInternal(SwitchRequest req)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _previewManager.StopAllAsync(preserveRestartInfo: true).GetAwaiter().GetResult();
                _log($"[性能] 终端切换停止 耗时={sw.ElapsedMilliseconds}ms");

                var phase = sw.ElapsedMilliseconds;
                _terminalManager.SwitchTo(req.TerminalIndex);
                _log($"[性能] 终端管理器切换 耗时={sw.ElapsedMilliseconds - phase}ms");
                _log("[终端切换] 当前终端=" + _terminalManager.CurrentName);

                phase = sw.ElapsedMilliseconds;
                _previewManager.RestartPreviewsOnTerminalSwitch(_terminalManager.CurrentBaseUrl,
                    () => _queueManager.IsGenerationValid(req.Generation)).GetAwaiter().GetResult();
                _log($"[性能] 终端切换启动 耗时={sw.ElapsedMilliseconds - phase}ms");
                _log($"[性能] 终端切换总耗时={sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _log("[终端切换] 失败: " + ex.Message);
            }
            finally
            {
                _queueManager.ClearSwitching();
            }
        }

        private void ExecuteCaptureFace(QueueTask<object> task)
        {
            var data = task.Data as CaptureTaskData;
            var tcs = data?.Tcs;
            try
            {
                var saveDir = data?.SaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
                var (ok, path) = CaptureFace(saveDir);
                tcs?.TrySetResult(ok ? "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(path) + "\"}"
                    : "{\"error\":true,\"code\":\"capture_failed\"}");
            }
            catch
            {
                tcs?.TrySetResult("{\"error\":true,\"code\":\"capture_failed\"}");
            }
        }

        private void ExecuteCaptureFingerprint(QueueTask<object> task)
        {
            var data = task.Data as CaptureTaskData;
            var tcs = data?.Tcs;
            try
            {
                var saveDir = data?.SaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = _terminalManager.ProcessSaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
                var (ok, path) = CaptureFingerprint(saveDir);
                tcs?.TrySetResult(ok ? "{\"status\":\"ok\",\"save_path\":\"" + JsonHelper.EscapeString(path) + "\"}"
                    : "{\"error\":true,\"code\":\"capture_failed\"}");
            }
            catch
            {
                tcs?.TrySetResult("{\"error\":true,\"code\":\"capture_failed\"}");
            }
        }

        private void ExecuteOcrInternal(QueueTask<object> task)
        {
            var tcs = task.Data as TaskCompletionSource<string>;
            try
            {
                var saveDir = _terminalManager.ProcessSaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
                var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
                var callbackBase = GetTerminalCallbackBaseUrl();
                var dllCallbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr";
                var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
                _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
                _requestCallbacks[requestId] = dllCallbackUrl;  // DLL callback, not terminal callback
                Logger.Info($"[OCR] 存储回调映射: {requestId} → {dllCallbackUrl}");
                var tt = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/ocr-document/request", body, 5000)
                    .GetAwaiter().GetResult();
                if (tt.ok)
                {
                    _log($"OCR 已转发至终端: request_id={requestId}");
                    tcs?.TrySetResult("{\"accepted\":true,\"request_id\":\"" + requestId + "\"}");
                }
                else
                {
                    tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
                }
            }
            catch (Exception)
            {
                tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
            }
        }

        private void ExecuteNfcInternal(QueueTask<object> task)
        {
            var tcs = task.Data as TaskCompletionSource<string>;
            try
            {
                var saveDir = _terminalManager.ProcessSaveDir;
                if (string.IsNullOrEmpty(saveDir)) saveDir = AppConfig.Instance.DefaultSaveDir;
                var requestId = Guid.NewGuid().ToString("N").Substring(0, 16);
                var callbackBase = GetTerminalCallbackBaseUrl();
                var dllCallbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card";
                var body = $"{{\"request_id\":\"{requestId}\",\"callback_url\":\"{callbackBase}\"}}";
                _requestSaveDirs[requestId] = PathHelper.SafeResolveSaveDir(saveDir);
                _requestCallbacks[requestId] = dllCallbackUrl;  // DLL callback, not terminal callback
                Logger.Info($"[NFC] 存储回调映射: {requestId} → {dllCallbackUrl}");
                var tt = _terminalClient.PostJsonAsync(_terminalManager.CurrentBaseUrl, "/resources/nfc-card/request", body, 5000)
                    .GetAwaiter().GetResult();
                if (tt.ok)
                {
                    _log($"NFC 已转发至终端: request_id={requestId}");
                    tcs?.TrySetResult("{\"accepted\":true,\"request_id\":\"" + requestId + "\"}");
                }
                else
                {
                    tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
                }
            }
            catch (Exception)
            {
                tcs?.TrySetResult("{\"error\":true,\"code\":\"terminal_request_failed\"}");
            }
        }

        private void ExecuteFacePreview(QueueTask<object> task)
        {
            var tcs = task.Data as TaskCompletionSource<string>;
            try
            {
                var result = "{\"accepted\":true}";
                tcs?.TrySetResult(result);
            }
            catch { tcs?.TrySetResult("{\"error\":true,\"code\":\"preview_failed\"}"); }
        }

        private void ExecuteFingerprintPreview(QueueTask<object> task)
        {
            var tcs = task.Data as TaskCompletionSource<string>;
            try
            {
                var result = "{\"accepted\":true}";
                tcs?.TrySetResult(result);
            }
            catch { tcs?.TrySetResult("{\"error\":true,\"code\":\"preview_failed\"}"); }
        }

        private void ExecuteMiscInternal(QueueTask<object> task)
        {
            var tcs = task.Data as TaskCompletionSource<string>;
            try
            {
                var result = "{\"accepted\":true}";
                tcs?.TrySetResult(result);
            }
            catch { tcs?.TrySetResult("{\"error\":true,\"code\":\"failed\"}"); }
        }

        public void Dispose()
        {
            Stop();
            _previewManager?.Dispose();
            _terminalClient?.Dispose();
            _dllCallback?.Dispose();
            _cts?.Dispose();
        }
    }
}
