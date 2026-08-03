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

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class MjpegPreviewController : IPreviewController
    {
        private const int ReadBufferSize = 8192;
        private const int MaxBufferedBytes = 8 * 1024 * 1024;
        private const int RenderIntervalMs = 33;
        private const int SameUrlMaxFailures = 2;
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
        private readonly PreviewScaleMode _scaleMode;
        private readonly bool _visible;
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
        private int _streamFaulted;
        private bool _faultCallbackIssued;
        private string _streamFaultReason;
        private Action<MjpegPreviewController, string> _streamFaultHandler;
        private HttpWebRequest _request;
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
            int sourceWidth, int sourceHeight, bool swapDimensions,
            PreviewScaleMode scaleMode, bool visible)
        {
            _description = description;
            _requestedParentHwnd = parentHwnd;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _scaleMode = scaleMode;
            _visible = visible;

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

        internal static int CalculateRenderDelayMs(int targetIntervalMs, long elapsedMilliseconds)
        {
            if (targetIntervalMs <= 0 || elapsedMilliseconds >= targetIntervalMs)
                return 0;
            if (elapsedMilliseconds <= 0)
                return targetIntervalMs;
            return targetIntervalMs - (int)elapsedMilliseconds;
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

        internal static async Task<MjpegPreviewController> StartAsyncWithScaleMode(
            string description, string url,
            IntPtr parentHwnd, int sourceWidth, int sourceHeight, bool swapDimensions,
            PreviewScaleMode scaleMode, bool visible, int timeoutMs)
        {
            var controller = new MjpegPreviewController(description, parentHwnd,
                sourceWidth, sourceHeight, swapDimensions, scaleMode, visible);

            controller._thread.Start();
            controller._readerThread.Start();

            var workerStarted = await WaitWithTimeoutAsync(controller._workerStartTcs.Task, timeoutMs)
                .ConfigureAwait(false);
            if (!workerStarted)
            {
                Logger.Warn($"HTTP MJPEG worker start timeout: {description}, timeout={timeoutMs}ms");
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            var ok = await controller.SwitchStreamAsync(url, parentHwnd, timeoutMs).ConfigureAwait(false);
            if (!ok)
            {
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            return controller;
        }

        public static Task<MjpegPreviewController> StartAsync(string description, string url,
            IntPtr parentHwnd, int sourceWidth, int sourceHeight, bool swapDimensions,
            bool visible, int timeoutMs)
        {
            return StartAsyncWithScaleMode(description, url, parentHwnd, sourceWidth, sourceHeight,
                swapDimensions, PreviewScaleMode.Stretch, visible, timeoutMs);
        }

        internal async Task<bool> SwitchStreamAsync(string url, IntPtr parentHwnd, int timeoutMs)
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
                _streamActive = true;
                _running = false;
                _activationTcs = activation;
                Interlocked.Exchange(ref _latestFrame, null);
                _renderedFrameSeq = Interlocked.CompareExchange(ref _latestFrameSeq, 0, 0);
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
                Logger.Debug($"HTTP MJPEG worker已切换媒体: {_description}, generation={generation}, created={CreatedWorkerCount}, render_live={LiveRenderThreadCount}, reader_live={LiveReaderThreadCount}");
                return true;
            }

            await PauseAsync(Math.Min(1000, Math.Max(1, timeoutMs))).ConfigureAwait(false);
            Logger.Warn($"HTTP MJPEG preview start timeout, fallback will be tried: {_description}, timeout={timeoutMs}ms, url={url}");
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

            Logger.Warn($"HTTP MJPEG worker pause timeout: {_description}, timeout={timeoutMs}ms");
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
                Logger.Warn($"HTTP MJPEG worker stop timeout: {_description}, timeout={timeoutMs}ms, render_exited={_renderExitTcs.Task.IsCompleted}, reader_exited={_readerExitTcs.Task.IsCompleted}");
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

        private void ThreadMain()
        {
            var liveCount = Interlocked.Increment(ref _liveRenderThreadCount);
            Logger.Debug($"HTTP MJPEG渲染线程已启动: {_description}, live={liveCount}, created={CreatedWorkerCount}");
            try
            {
                if (!CreateRenderWindow())
                {
                    _workerStartTcs.TrySetResult(false);
                    BeginStop();
                    return;
                }

                _workerStartTcs.TrySetResult(true);

                var renderCycle = new System.Diagnostics.Stopwatch();
                while (!_stopRequested)
                {
                    renderCycle.Restart();
                    Application.DoEvents();
                    ApplyRequestedParentAndVisibility();
                    ApplyFillLayout();
                    RenderLatestFrame();

                    var delayMs = CalculateRenderDelayMs(
                        RenderIntervalMs, renderCycle.ElapsedMilliseconds);
                    if (delayMs > 0)
                        Thread.Sleep(delayMs);
                    else
                        Thread.Yield();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP MJPEG preview thread exception: {_description}, error={ex.Message}", ex);
                _workerStartTcs.TrySetResult(false);
            }
            finally
            {
                BeginStop();
                DestroyRenderWindow();
                _running = false;
                var remaining = Interlocked.Decrement(ref _liveRenderThreadCount);
                _renderExitTcs.TrySetResult(true);
                Logger.Debug($"HTTP MJPEG渲染线程已退出: {_description}, live={remaining}, reader_live={LiveReaderThreadCount}");
            }
        }

        private bool CreateRenderWindow()
        {
            IntPtr parentHwnd;
            lock (_stateLock)
                parentHwnd = _requestedParentHwnd;

            if (parentHwnd == IntPtr.Zero || !IsWindow(parentHwnd))
            {
                Logger.Error($"HTTP MJPEG preview invalid parent HWND: {_description}, hwnd={parentHwnd}");
                return false;
            }

            _currentParentHwnd = parentHwnd;
            // 预览窗口仅用于显示。禁用跨进程子窗口，避免鼠标操作将输入焦点从第三方宿主切换到 Proxy。
            var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_DISABLED;
            if (_visible)
                windowStyle |= WS_VISIBLE;

            _videoHwnd = CreateWindowEx(WS_EX_NOPARENTNOTIFY | WS_EX_NOACTIVATE,
                "STATIC", "", windowStyle,
                0, 0, 1, 1, parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_videoHwnd == IntPtr.Zero)
            {
                Logger.Error($"HTTP MJPEG preview failed to create child window: {_description}");
                return false;
            }

            ApplyFillLayout();
            Logger.Debug($"HTTP MJPEG预览窗口创建: {_description}");
            return true;
        }

        private void DestroyRenderWindow()
        {
            if (_videoHwnd == IntPtr.Zero)
                return;

            try
            {
                if (IsWindow(_videoHwnd))
                    DestroyWindow(_videoHwnd);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG preview destroy window failed: {_description}, error={ex.Message}");
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
            Logger.Debug($"HTTP MJPEG读取线程已启动: {_description}, live={liveCount}, created={CreatedWorkerCount}");
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
                                Logger.Debug($"HTTP MJPEG流已打开: {_description}, generation={generation}");

                                if (stream == null)
                                    throw new IOException("MJPEG response stream is null");

                                var readBuffer = new byte[ReadBufferSize];
                                while (!_stopRequested && !_cts.IsCancellationRequested &&
                                       IsCurrentStream(generation))
                                {
                                    var read = stream.Read(readBuffer, 0, readBuffer.Length);
                                    if (read <= 0)
                                        break;

                                    AppendBytes(buffer, readBuffer, read);
                                    ExtractFrames(buffer, generation);
                                }
                            }

                            if (IsCurrentStream(generation))
                                failureReason = "MJPEG stream ended";
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
                            Logger.Warn($"HTTP MJPEG同URL恢复失败({SameUrlMaxFailures}次): {_description}, error={failureReason}");
                            SignalStreamFault(generation, failureReason);
                            break;
                        }

                        Logger.Warn($"HTTP MJPEG连接断开，{ReconnectDelayMs / 1000}秒后使用同URL重连" +
                                    $"({failureCount}/{SameUrlMaxFailures}): {_description}, error={failureReason}");
                        _streamChanged.WaitOne(ReconnectDelayMs);
                    }
                }
            }
            finally
            {
                CompletePauseIfIdle();
                var remaining = Interlocked.Decrement(ref _liveReaderThreadCount);
                _readerExitTcs.TrySetResult(true);
                Logger.Debug($"HTTP MJPEG读取线程已退出: {_description}, live={remaining}, render_live={LiveRenderThreadCount}");
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
                _streamFaultReason = string.IsNullOrWhiteSpace(reason) ? "MJPEG stream unavailable" : reason;

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
                Logger.Error($"HTTP MJPEG stream fault callback failed: {_description}, error={ex.Message}", ex);
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

        private void AppendBytes(List<byte> buffer, byte[] bytes, int count)
        {
            for (int i = 0; i < count; i++)
                buffer.Add(bytes[i]);

            if (buffer.Count <= MaxBufferedBytes)
                return;

            var soi = FindMarker(buffer, Math.Max(0, buffer.Count - (ReadBufferSize * 4)), 0xD8);
            if (soi > 0)
                buffer.RemoveRange(0, soi);
            else
                buffer.Clear();
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
                var ms = new MemoryStream(frame);
                var image = Image.FromStream(ms, false, false);
                try
                {
                    DrawImage(image);
                }
                finally
                {
                    image.Dispose();
                    ms.Dispose();
                }
                _renderedFrameSeq = seq;
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG render frame failed: {_description}, error={ex.Message}");
                _renderedFrameSeq = seq;
            }
        }

        private void DrawImage(Image image)
        {
            RECT rect;
            if (!GetClientRect(_videoHwnd, out rect))
                return;

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                return;

            var hdc = GetDC(_videoHwnd);
            if (hdc == IntPtr.Zero)
                return;

            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    ConfigureGraphics(g, InterpolationMode.Bilinear);

                    var displayWidth = _swapDimensions ? _sourceHeight : _sourceWidth;
                    var displayHeight = _swapDimensions ? _sourceWidth : _sourceHeight;
                    if (displayWidth <= 0 || displayHeight <= 0)
                    {
                        displayWidth = image.Width;
                        displayHeight = image.Height;
                    }

                    var targetBounds = PreviewLayoutMath.CalculateVideoBounds(
                        new Size(displayWidth, displayHeight),
                        new Size(width, height),
                        _scaleMode);
                    if (!targetBounds.IsEmpty)
                        g.DrawImage(image, targetBounds);
                }
                ValidateRect(_videoHwnd, IntPtr.Zero);
            }
            finally
            {
                ReleaseDC(_videoHwnd, hdc);
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
                Logger.Warn($"HTTP MJPEG apply fill layout failed: {_description}, error={ex.Message}");
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
