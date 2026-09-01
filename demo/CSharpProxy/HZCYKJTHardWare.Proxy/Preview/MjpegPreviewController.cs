using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class MjpegPreviewController : IPreviewController
    {
        private const int ReadBufferSize = 8192;
        private const int MaxBufferedBytes = 8 * 1024 * 1024;
        private const int MaxFrameBytes = 8 * 1024 * 1024;
        private const int RenderIntervalMs = 33;
        private const int SameUrlMaxFailures = 2;
        private const int RenderFailureThreshold = 3;
        private const int ReconnectDelayMs = 1000;
        private const int ConnectTimeoutMs = 5000;
        private const int StreamReadTimeoutMs = 5000;

        private readonly TaskCompletionSource<bool> _workerStartTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _renderExitTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _readerExitTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;
        private readonly Thread _readerThread;
        private readonly string _description;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;
        private readonly bool _swapDimensions;
        private readonly bool _visible;
        private readonly bool _directRenderTarget;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly AutoResetEvent _streamChanged = new AutoResetEvent(false);
        private readonly object _stateLock = new object();
        private readonly object _requestLock = new object();
        private readonly object _faultLock = new object();

        private volatile bool _running;
        private volatile bool _stopRequested;
        private bool _streamActive;
        private string _url;
        private IntPtr _requestedParentHwnd;
        private long _streamGeneration;
        private TaskCompletionSource<bool> _activationTcs;
        private TaskCompletionSource<bool> _pauseTcs;
        private int _disposeStarted;
        private int _resourcesDisposed;
        private int _deferredCleanupScheduled;
        private int _streamFaulted;
        private int _consecutiveRenderFailures;
        private bool _faultCallbackIssued;
        private string _streamFaultReason;
        private Action<MjpegPreviewController, string> _streamFaultHandler;
        private HttpWebRequest _request;
        private string _requestId;
        private IntPtr _videoHwnd = IntPtr.Zero;
        private IntPtr _currentParentHwnd = IntPtr.Zero;
        private byte[] _latestFrame;
        private int _latestFrameSeq;
        private int _renderedFrameSeq;
        private int _lastHostW;
        private int _lastHostH;
        private static int _createdWorkerCount;
        private static int _liveRenderThreadCount;
        private static int _liveReaderThreadCount;

        private MjpegPreviewController(string description, IntPtr parentHwnd,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible,
            bool directRenderTarget, string requestId)
        {
            _description = description;
            _requestedParentHwnd = parentHwnd;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _visible = visible;
            _directRenderTarget = directRenderTarget;
            _requestId = requestId;

            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "MJPEG Preview Thread-" + description
            };
            _thread.SetApartmentState(ApartmentState.STA);

            _readerThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "MJPEG Reader Thread-" + description
            };
            Interlocked.Increment(ref _createdWorkerCount);
        }

        internal static int CreatedWorkerCount => Volatile.Read(ref _createdWorkerCount);
        internal static int LiveRenderThreadCount => Volatile.Read(ref _liveRenderThreadCount);
        internal static int LiveReaderThreadCount => Volatile.Read(ref _liveReaderThreadCount);

        internal string RequestId
        {
            get
            {
                lock (_stateLock)
                    return _requestId;
            }
        }

        internal bool ResourcesDisposed => Volatile.Read(ref _resourcesDisposed) != 0;

        internal Task WaitForExitAsync()
        {
            return Task.WhenAll(_renderExitTcs.Task, _readerExitTcs.Task);
        }

        public bool IsRunning
        {
            get
            {
                return _running && IsStreamActive() && Volatile.Read(ref _streamFaulted) == 0 &&
                       _videoHwnd != IntPtr.Zero && IsWindow(_videoHwnd);
            }
        }

        internal void SetStreamFaultHandler(Action<MjpegPreviewController, string> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_faultLock)
                _streamFaultHandler = handler;

            // 首帧到达后、PreviewManager 登记会话前数据流仍可能立即失败；此处转发故障以消除登记竞争窗口。
            TryDispatchStreamFault();
        }

        public static async Task<MjpegPreviewController> StartAsync(string description, string url,
            IntPtr parentHwnd, int sourceWidth, int sourceHeight, bool swapDimensions,
            bool visible, int timeoutMs, bool directRenderTarget = false, string requestId = null)
        {
            var controller = new MjpegPreviewController(description, parentHwnd,
                sourceWidth, sourceHeight, swapDimensions, visible, directRenderTarget, requestId);

            controller._thread.Start();
            controller._readerThread.Start();

            var workerStarted = await WaitWithTimeoutAsync(controller._workerStartTcs.Task, timeoutMs)
                .ConfigureAwait(false);
            if (!workerStarted)
            {
                Logger.Warn($"HTTP MJPEG工作线程启动超时：{description}，超时={timeoutMs}ms");
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            var ok = await controller.SwitchStreamAsync(url, parentHwnd, timeoutMs, requestId)
                .ConfigureAwait(false);
            if (!ok)
            {
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            return controller;
        }

        internal async Task<bool> SwitchStreamAsync(string url, IntPtr parentHwnd, int timeoutMs,
            string requestId = null)
        {
            if (Volatile.Read(ref _disposeStarted) != 0 || _stopRequested ||
                string.IsNullOrWhiteSpace(url) || parentHwnd == IntPtr.Zero || !IsWindow(parentHwnd))
                return false;

            var activation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            long generation;
            lock (_stateLock)
            {
                if (_stopRequested)
                    return false;

                _activationTcs?.TrySetResult(false);
                _pauseTcs?.TrySetResult(true);
                _pauseTcs = null;
                generation = ++_streamGeneration;
                _url = url;
                _requestedParentHwnd = parentHwnd;
                _requestId = requestId;
                _streamActive = true;
                _running = false;
                _activationTcs = activation;
                Interlocked.Exchange(ref _latestFrame, null);
                _renderedFrameSeq = Interlocked.CompareExchange(ref _latestFrameSeq, 0, 0);
                Interlocked.Exchange(ref _consecutiveRenderFailures, 0);
            }

            lock (_faultLock)
            {
                _streamFaultHandler = null;
                _streamFaultReason = null;
                _faultCallbackIssued = false;
                Volatile.Write(ref _streamFaulted, 0);
            }

            AbortRequest();
            _streamChanged.Set();

            var completed = await Task.WhenAny(activation.Task, Task.Delay(Math.Max(1, timeoutMs)))
                .ConfigureAwait(false);
            if (completed == activation.Task && await activation.Task.ConfigureAwait(false))
            {
                Logger.Debug($"HTTP MJPEG工作线程已切换媒体：{_description}，代次={generation}，已创建线程数={CreatedWorkerCount}，" +
                             $"渲染线程存活数={LiveRenderThreadCount}，读取线程存活数={LiveReaderThreadCount}");
                return true;
            }

            await PauseAsync(Math.Min(1000, Math.Max(1, timeoutMs))).ConfigureAwait(false);
            Logger.Warn($"HTTP MJPEG预览启动超时：{_description}，超时={timeoutMs}ms，地址={Logger.SanitizeUrlForLog(url)}");
            return false;
        }

        internal async Task<bool> PauseAsync(int timeoutMs)
        {
            TaskCompletionSource<bool> pause;
            lock (_stateLock)
            {
                _activationTcs?.TrySetResult(false);
                _activationTcs = null;
                _streamActive = false;
                _running = false;
                ++_streamGeneration;
                pause = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pauseTcs = pause;
            }

            lock (_faultLock)
                _streamFaultHandler = null;

            Interlocked.Exchange(ref _latestFrame, null);

            var requestIdle = false;
            lock (_requestLock)
                requestIdle = _request == null;
            if (requestIdle)
                pause.TrySetResult(true);

            AbortRequest();
            _streamChanged.Set();

            var completed = await Task.WhenAny(pause.Task, Task.Delay(Math.Max(1, timeoutMs)))
                .ConfigureAwait(false);
            if (completed == pause.Task)
                return true;

            Logger.Warn($"HTTP MJPEG工作线程暂停超时：{_description}，超时={timeoutMs}ms");
            return false;
        }

        public async Task DisposeAsync(int timeoutMs)
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) == 0)
                BeginStop();

            var allExited = Task.WhenAll(_renderExitTcs.Task, _readerExitTcs.Task);
            var completed = await Task.WhenAny(allExited, Task.Delay(Math.Max(1, timeoutMs)))
                .ConfigureAwait(false);
            if (completed != allExited)
            {
                Logger.Warn($"HTTP MJPEG工作线程停止超时：{_description}，超时={timeoutMs}ms，" +
                            $"渲染线程已退出={_renderExitTcs.Task.IsCompleted}，读取线程已退出={_readerExitTcs.Task.IsCompleted}");
                ScheduleDeferredCleanup();
                return;
            }

            if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 0)
            {
                _streamChanged.Dispose();
                _cts.Dispose();
            }
        }

        public void Dispose()
        {
            DisposeAsync(1000).GetAwaiter().GetResult();
        }

        private void ScheduleDeferredCleanup()
        {
            if (Interlocked.Exchange(ref _deferredCleanupScheduled, 1) != 0)
                return;

            _ = CompleteDeferredCleanupAsync();
        }

        private async Task CompleteDeferredCleanupAsync()
        {
            try
            {
                await WaitForExitAsync().ConfigureAwait(false);
                await DisposeAsync(0).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP MJPEG资源延迟释放失败：{_description}，错误={ex.Message}", ex);
            }
        }

        private void ThreadMain()
        {
            var liveCount = Interlocked.Increment(ref _liveRenderThreadCount);
            Logger.Debug($"HTTP MJPEG渲染线程已启动：{_description}，存活数={liveCount}，已创建线程数={CreatedWorkerCount}");
            try
            {
                if (!CreateRenderWindow())
                {
                    _workerStartTcs.TrySetResult(false);
                    BeginStop();
                    return;
                }

                _workerStartTcs.TrySetResult(true);

                while (!_stopRequested)
                {
                    Application.DoEvents();
                    ApplyRequestedParentAndVisibility();
                    ApplyFillLayout();
                    RenderLatestFrame();
                    Thread.Sleep(RenderIntervalMs);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP MJPEG预览线程异常：{_description}，错误={ex.Message}", ex);
                _workerStartTcs.TrySetResult(false);
            }
            finally
            {
                BeginStop();
                DestroyRenderWindow();
                _running = false;
                var remaining = Interlocked.Decrement(ref _liveRenderThreadCount);
                _renderExitTcs.TrySetResult(true);
                Logger.Debug($"HTTP MJPEG渲染线程已退出：{_description}，存活数={remaining}，读取线程存活数={LiveReaderThreadCount}");
            }
        }

        private bool CreateRenderWindow()
        {
            IntPtr parentHwnd;
            lock (_stateLock)
                parentHwnd = _requestedParentHwnd;

            if (parentHwnd == IntPtr.Zero || !IsWindow(parentHwnd))
            {
                Logger.Error($"HTTP MJPEG预览父HWND无效：{_description}，" +
                             $"HWND={PreviewManager.FormatHwnd(parentHwnd)}");
                return false;
            }

            _currentParentHwnd = parentHwnd;
            if (_directRenderTarget)
            {
                // 外部目标窗口由第三方进程创建和管理。MJPEG 只借用其客户区 HDC 绘制，
                // 不创建子窗口，也不改变第三方窗口的父子关系、位置或生命周期。
                _videoHwnd = parentHwnd;
                Logger.Info($"HTTP MJPEG预览使用第三方HWND直绘：{_description}，" +
                            $"HWND={PreviewManager.FormatHwnd(parentHwnd)}");
                return true;
            }

            // 预览窗口仅用于显示。禁用跨进程子窗口，避免鼠标操作将输入焦点从第三方宿主切换到 Proxy。
            var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_DISABLED;
            if (_visible)
                windowStyle |= WS_VISIBLE;

            _videoHwnd = CreateWindowEx(WS_EX_NOPARENTNOTIFY | WS_EX_NOACTIVATE,
                "STATIC", "", windowStyle,
                0, 0, 1, 1, parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_videoHwnd == IntPtr.Zero)
            {
                Logger.Error($"HTTP MJPEG预览创建子窗口失败：{_description}");
                return false;
            }

            ApplyFillLayout();
            Logger.Debug($"HTTP MJPEG预览窗口创建：{_description}");
            return true;
        }

        private void DestroyRenderWindow()
        {
            if (_videoHwnd == IntPtr.Zero)
                return;

            try
            {
                if (!_directRenderTarget && IsWindow(_videoHwnd))
                    DestroyWindow(_videoHwnd);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG预览窗口销毁失败：{_description}，错误={ex.Message}");
            }
            finally
            {
                _videoHwnd = IntPtr.Zero;
                _currentParentHwnd = IntPtr.Zero;
            }
        }

        private void ReadLoop()
        {
            var liveCount = Interlocked.Increment(ref _liveReaderThreadCount);
            Logger.Debug($"HTTP MJPEG读取线程已启动：{_description}，存活数={liveCount}，已创建线程数={CreatedWorkerCount}");
            try
            {
                while (!_stopRequested && !_cts.IsCancellationRequested)
                {
                    string url;
                    long generation;
                    if (!TryGetActiveStream(out url, out generation))
                    {
                        CompletePauseIfIdle();
                        _streamChanged.WaitOne(250);
                        continue;
                    }

                    var buffer = new List<byte>(ReadBufferSize * 4);
                    var failureCount = 0;
                    while (!_stopRequested && IsCurrentStream(generation))
                    {
                        string failureReason = null;
                        HttpWebRequest request = null;
                        try
                        {
                            request = CreateRequest(url);
                            lock (_requestLock)
                            {
                                if (!IsCurrentStream(generation))
                                {
                                    request.Abort();
                                    break;
                                }
                                _request = request;
                            }

                            using (var response = (HttpWebResponse)request.GetResponse())
                            using (var stream = response.GetResponseStream())
                            {
                                Logger.Debug($"HTTP MJPEG流已打开：{_description}，代次={generation}");

                                if (stream == null)
                                    throw new IOException("MJPEG响应流为空");

                                var readBuffer = new byte[ReadBufferSize];
                                while (!_stopRequested && !_cts.IsCancellationRequested &&
                                       IsCurrentStream(generation))
                                {
                                    var read = stream.Read(readBuffer, 0, readBuffer.Length);
                                    if (read <= 0)
                                        break;

                                    if (!AppendBytes(buffer, readBuffer, read, generation))
                                        break;
                                    ExtractFrames(buffer, generation);
                                }
                            }

                            if (IsCurrentStream(generation))
                                failureReason = "MJPEG流已结束";
                        }
                        catch (WebException ex)
                        {
                            if (_stopRequested || _cts.IsCancellationRequested ||
                                !IsCurrentStream(generation))
                                break;
                            failureReason = ex.Message;
                        }
                        catch (Exception ex)
                        {
                            if (_stopRequested || _cts.IsCancellationRequested ||
                                !IsCurrentStream(generation))
                                break;
                            failureReason = ex.Message;
                        }
                        finally
                        {
                            lock (_requestLock)
                            {
                                if (ReferenceEquals(_request, request))
                                    _request = null;
                            }
                            CompletePauseIfIdle();
                        }

                        if (_stopRequested || _cts.IsCancellationRequested ||
                            !IsCurrentStream(generation))
                            break;

                        failureCount++;
                        buffer.Clear();
                        if (failureCount >= SameUrlMaxFailures)
                        {
                            Logger.TryLogRateLimited(
                                "Mjpeg|same_url_failure|" + _description,
                                LogModules.Preview, "错误",
                                $"HTTP MJPEG同一地址恢复失败（{SameUrlMaxFailures}次）：" +
                                $"{JsonHelper.ToLogValue(_description)}，错误={JsonHelper.ToLogValue(failureReason)}");
                            SignalStreamFault(generation, failureReason);
                            break;
                        }

                        Logger.TryLogRateLimited(
                            "Mjpeg|same_url_reconnect|" + _description,
                            LogModules.Preview, "警告",
                            $"HTTP MJPEG连接断开，{ReconnectDelayMs / 1000}秒后使用同一地址重连" +
                            $"（{failureCount}/{SameUrlMaxFailures}）：{JsonHelper.ToLogValue(_description)}，" +
                            $"错误={JsonHelper.ToLogValue(failureReason)}");
                        _streamChanged.WaitOne(ReconnectDelayMs);
                    }
                }
            }
            finally
            {
                CompletePauseIfIdle();
                var remaining = Interlocked.Decrement(ref _liveReaderThreadCount);
                _readerExitTcs.TrySetResult(true);
                Logger.Debug($"HTTP MJPEG读取线程已退出：{_description}，存活数={remaining}，渲染线程存活数={LiveRenderThreadCount}");
            }
        }

        private void SignalStreamFault(long generation, string reason)
        {
            TaskCompletionSource<bool> activation;
            lock (_stateLock)
            {
                if (_stopRequested || !_streamActive || generation != _streamGeneration)
                    return;

                _streamActive = false;
                _running = false;
                activation = _activationTcs;
                _activationTcs = null;
            }

            if (Interlocked.Exchange(ref _streamFaulted, 1) != 0)
                return;

            lock (_faultLock)
                _streamFaultReason = string.IsNullOrWhiteSpace(reason) ? "MJPEG流不可用" : reason;

            activation?.TrySetResult(false);
            TryDispatchStreamFault();
        }

        private void TryDispatchStreamFault()
        {
            Action<MjpegPreviewController, string> handler;
            string reason;
            lock (_faultLock)
            {
                if (_faultCallbackIssued || _streamFaultHandler == null || string.IsNullOrEmpty(_streamFaultReason))
                    return;

                _faultCallbackIssued = true;
                handler = _streamFaultHandler;
                reason = _streamFaultReason;
            }

            try
            {
                handler(this, reason);
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP MJPEG流故障回调失败：{_description}，错误={ex.Message}", ex);
            }
        }

        private HttpWebRequest CreateRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "multipart/x-mixed-replace,image/jpeg,*/*";
            request.UserAgent = "HZCYKJTHardWare.Proxy MJPEG Preview";
            request.KeepAlive = false;
            request.Proxy = null;
            request.Timeout = ConnectTimeoutMs;
            request.ReadWriteTimeout = StreamReadTimeoutMs;
            request.AllowReadStreamBuffering = false;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            request.Headers[HttpRequestHeader.Pragma] = "no-cache";
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return request;
        }

        private bool AppendBytes(List<byte> buffer, byte[] bytes, int count, long generation)
        {
            if (count <= 0 || count > MaxBufferedBytes || buffer.Count > MaxBufferedBytes - count)
            {
                buffer.Clear();
                Logger.Error(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                    $"MJPEG帧缓冲超过限制：{_description}，限制={MaxBufferedBytes}字节，代次={generation}"));
                SignalStreamFault(generation, "MJPEG帧缓冲超过限制");
                return false;
            }

            for (int i = 0; i < count; i++)
                buffer.Add(bytes[i]);
            return true;
        }

        private void ExtractFrames(List<byte> buffer, long generation)
        {
            while (buffer.Count >= 4)
            {
                var soi = FindMarker(buffer, 0, 0xD8);
                if (soi < 0)
                {
                    KeepMarkerPrefix(buffer);
                    return;
                }

                if (soi > 0)
                    buffer.RemoveRange(0, soi);

                var eoi = FindMarker(buffer, 2, 0xD9);
                if (eoi < 0)
                    return;

                var frameLength = eoi + 2;
                if (frameLength > MaxFrameBytes)
                {
                    buffer.Clear();
                    Logger.Error(Logger.FormatModuleMessage(LogModules.Preview, "错误",
                        $"MJPEG单帧超过限制：{_description}，帧长度={frameLength}字节，限制={MaxFrameBytes}字节，代次={generation}"));
                    SignalStreamFault(generation, "MJPEG单帧超过限制");
                    return;
                }

                var frame = new byte[frameLength];
                buffer.CopyTo(0, frame, 0, frameLength);
                buffer.RemoveRange(0, frameLength);
                PublishFrame(frame, generation);
            }
        }

        private static int FindMarker(List<byte> buffer, int start, byte marker)
        {
            for (int i = Math.Max(0, start); i < buffer.Count - 1; i++)
            {
                if (buffer[i] == 0xFF && buffer[i + 1] == marker)
                    return i;
            }
            return -1;
        }

        private static void KeepMarkerPrefix(List<byte> buffer)
        {
            if (buffer.Count == 0)
                return;

            var keepLast = buffer[buffer.Count - 1] == 0xFF;
            buffer.Clear();
            if (keepLast)
                buffer.Add(0xFF);
        }

        private void PublishFrame(byte[] frame, long generation)
        {
            TaskCompletionSource<bool> activation;
            lock (_stateLock)
            {
                if (!_streamActive || generation != _streamGeneration)
                    return;
                activation = _activationTcs;
                _running = true;
            }

            Interlocked.Exchange(ref _latestFrame, frame);
            Interlocked.Increment(ref _latestFrameSeq);
            activation?.TrySetResult(true);
        }

        private void RenderLatestFrame()
        {
            if (_videoHwnd == IntPtr.Zero || !IsWindow(_videoHwnd))
                return;

            var seq = Interlocked.CompareExchange(ref _latestFrameSeq, 0, 0);
            if (seq == 0 || seq == _renderedFrameSeq)
                return;

            var frame = Interlocked.CompareExchange(ref _latestFrame, null, null);
            if (frame == null || frame.Length == 0)
                return;

            try
            {
                using (var ms = new MemoryStream(frame, false))
                {
                    Image image;
                    try
                    {
                        image = Image.FromStream(ms, false, false);
                    }
                    catch (Exception ex)
                    {
                        HandleRenderFailure(seq, frame.Length, "解码", ex);
                        return;
                    }

                    using (image)
                    {
                        try
                        {
                            DrawImage(image);
                        }
                        catch (Exception ex)
                        {
                            HandleRenderFailure(seq, frame.Length, "绘制", ex);
                            return;
                        }
                    }
                }

                Interlocked.Exchange(ref _consecutiveRenderFailures, 0);
                _renderedFrameSeq = seq;
            }
            catch (Exception ex)
            {
                HandleRenderFailure(seq, frame.Length, "解码", ex);
            }
        }

        private void HandleRenderFailure(int sequence, int frameLength, string stage, Exception ex)
        {
            _renderedFrameSeq = sequence;
            var failures = Interlocked.Increment(ref _consecutiveRenderFailures);
            var level = failures >= RenderFailureThreshold ? "错误" : "警告";
            var exceptionType = ex == null ? "未知异常" : ex.GetType().Name;
            var hresult = ex == null ? "<无>" : "0x" + ex.HResult.ToString("X8");
            Logger.TryLogRateLimited(
                "Mjpeg|render|" + _description + "|" + stage + "|" + level,
                LogModules.Preview, level,
                $"HTTP MJPEG帧渲染失败：{JsonHelper.ToLogValue(_description)}，阶段={JsonHelper.ToLogValue(stage)}，" +
                $"帧长度={frameLength}字节，异常类型={JsonHelper.ToLogValue(exceptionType)}，" +
                $"HResult={hresult}，连续失败次数={failures}");

            if (failures < RenderFailureThreshold)
                return;

            long generation;
            lock (_stateLock)
                generation = _streamGeneration;
            SignalStreamFault(generation, "MJPEG帧连续渲染失败");
        }

        private void DrawImage(Image image)
        {
            RECT rect;
            if (!GetClientRect(_videoHwnd, out rect))
                throw new InvalidOperationException("获取预览窗口客户区失败");

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("预览窗口客户区大小无效");

            var hdc = GetDC(_videoHwnd);
            if (hdc == IntPtr.Zero)
                throw new InvalidOperationException("获取预览窗口HDC失败");

            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    ConfigureGraphics(g, InterpolationMode.Bilinear);
                    g.DrawImage(image, new Rectangle(0, 0, width, height));
                }
                if (!_directRenderTarget)
                    ValidateRect(_videoHwnd, IntPtr.Zero);
            }
            finally
            {
                if (ReleaseDC(_videoHwnd, hdc) == 0)
                    Logger.Warn(Logger.FormatModuleMessage(LogModules.Preview, "警告",
                        $"释放预览窗口HDC返回失败：{_description}，HWND={PreviewManager.FormatHwnd(_videoHwnd)}"));
            }
        }

        private static void ConfigureGraphics(Graphics g, InterpolationMode interpolationMode)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = interpolationMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
        }

        private void ApplyFillLayout()
        {
            if (_videoHwnd == IntPtr.Zero || !IsWindow(_videoHwnd)) return;
            if (_currentParentHwnd == IntPtr.Zero || !IsWindow(_currentParentHwnd)) return;
            if (_directRenderTarget) return;

            try
            {
                RECT hostRect;
                if (!GetClientRect(_currentParentHwnd, out hostRect)) return;

                int hostW = hostRect.Right - hostRect.Left;
                int hostH = hostRect.Bottom - hostRect.Top;
                if (hostW <= 0 || hostH <= 0) return;

                if (hostW == _lastHostW && hostH == _lastHostH)
                    return;

                _lastHostW = hostW;
                _lastHostH = hostH;
                SetWindowPos(_videoHwnd, HWND_BOTTOM, 0, 0, hostW, hostH,
                    SWP_NOACTIVATE);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG填充布局应用失败：{_description}，错误={ex.Message}");
            }
        }

        private void ApplyRequestedParentAndVisibility()
        {
            IntPtr requestedParent;
            bool active;
            lock (_stateLock)
            {
                requestedParent = _requestedParentHwnd;
                active = _streamActive;
            }

            if (_videoHwnd == IntPtr.Zero || !IsWindow(_videoHwnd))
                return;

            if (_directRenderTarget)
            {
                // 直绘模式允许在终端切换时更换绘制目标，但不调用 SetParent/ShowWindow。
                if (requestedParent != IntPtr.Zero && IsWindow(requestedParent) &&
                    requestedParent != _currentParentHwnd)
                {
                    _videoHwnd = requestedParent;
                    _currentParentHwnd = requestedParent;
                    _lastHostW = _lastHostH = 0;
                }
                return;
            }

            if (requestedParent != IntPtr.Zero && requestedParent != _currentParentHwnd &&
                IsWindow(requestedParent))
            {
                SetParent(_videoHwnd, requestedParent);
                _currentParentHwnd = requestedParent;
                _lastHostW = _lastHostH = 0;
            }

            ShowWindow(_videoHwnd, active && _visible ? SW_SHOWNOACTIVATE : SW_HIDE);
        }

        private bool TryGetActiveStream(out string url, out long generation)
        {
            lock (_stateLock)
            {
                url = _url;
                generation = _streamGeneration;
                return _streamActive && !string.IsNullOrWhiteSpace(url);
            }
        }

        private bool IsCurrentStream(long generation)
        {
            lock (_stateLock)
                return !_stopRequested && _streamActive && generation == _streamGeneration;
        }

        private bool IsStreamActive()
        {
            lock (_stateLock)
                return _streamActive;
        }

        private void CompletePauseIfIdle()
        {
            TaskCompletionSource<bool> pause = null;
            lock (_stateLock)
            {
                if (!_streamActive)
                    pause = _pauseTcs;
            }
            pause?.TrySetResult(true);
        }

        private static async Task<bool> WaitWithTimeoutAsync(Task<bool> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(Math.Max(1, timeoutMs)))
                .ConfigureAwait(false);
            return completed == task && await task.ConfigureAwait(false);
        }

        private void BeginStop()
        {
            _stopRequested = true;
            lock (_stateLock)
            {
                _streamActive = false;
                _running = false;
                _activationTcs?.TrySetResult(false);
                _activationTcs = null;
                _pauseTcs?.TrySetResult(true);
            }
            Interlocked.Exchange(ref _latestFrame, null);
            try { _cts.Cancel(); } catch { }
            AbortRequest();
            _streamChanged.Set();
        }

        private void AbortRequest()
        {
            try
            {
                lock (_requestLock)
                    _request?.Abort();
            }
            catch { }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] private static extern bool ValidateRect(IntPtr hWnd, IntPtr lpRect);
        [DllImport("user32.dll")] private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName,
            string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);

        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_DISABLED = 0x08000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_CLIPCHILDREN = 0x02000000;
        private const uint WS_EX_NOPARENTNOTIFY = 0x00000004;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
