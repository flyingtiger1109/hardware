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

        private readonly TaskCompletionSource<bool> _startTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;
        private readonly string _description;
        private readonly string _url;
        private readonly IntPtr _parentHwnd;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;
        private readonly bool _swapDimensions;
        private readonly bool _visible;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _requestLock = new object();
        private readonly object _faultLock = new object();

        private volatile bool _disposed;
        private volatile bool _running;
        private volatile bool _stopRequested;
        private int _streamFaulted;
        private bool _faultCallbackIssued;
        private string _streamFaultReason;
        private Action<MjpegPreviewController, string> _streamFaultHandler;
        private HttpWebRequest _request;
        private Thread _readerThread;
        private IntPtr _videoHwnd = IntPtr.Zero;
        private IntPtr _currentParentHwnd = IntPtr.Zero;
        private byte[] _latestFrame;
        private int _latestFrameSeq;
        private int _renderedFrameSeq;
        private int _lastHostW;
        private int _lastHostH;

        private MjpegPreviewController(string description, string url, IntPtr parentHwnd,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible)
        {
            _description = description;
            _url = url;
            _parentHwnd = parentHwnd;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _visible = visible;

            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "MJPEG Preview Thread-" + description
            };
            _thread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsRunning
        {
            get
            {
                return _running && Volatile.Read(ref _streamFaulted) == 0 &&
                       _videoHwnd != IntPtr.Zero && IsWindow(_videoHwnd);
            }
        }

        internal void SetStreamFaultHandler(Action<MjpegPreviewController, string> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_faultLock)
                _streamFaultHandler = handler;

            // The stream can fail immediately after the first frame and before PreviewManager
            // stores the session. Dispatching here closes that registration race.
            TryDispatchStreamFault();
        }

        public static async Task<MjpegPreviewController> StartAsync(string description, string url,
            IntPtr parentHwnd, int sourceWidth, int sourceHeight, bool swapDimensions,
            bool visible, int timeoutMs)
        {
            var controller = new MjpegPreviewController(description, url, parentHwnd,
                sourceWidth, sourceHeight, swapDimensions, visible);

            controller._thread.Start();

            var completed = await Task.WhenAny(controller._startTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != controller._startTcs.Task)
            {
                Logger.Warn($"HTTP MJPEG preview start timeout, fallback will be tried: {description}, timeout={timeoutMs}ms, url={url}");
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            bool ok;
            try
            {
                ok = await controller._startTcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG preview start exception, fallback will be tried: {description}, error={ex.Message}");
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            if (!ok)
            {
                await controller.DisposeAsync(1000).ConfigureAwait(false);
                return null;
            }

            return controller;
        }

        public async Task DisposeAsync(int timeoutMs)
        {
            if (_disposed)
                return;

            _disposed = true;
            BeginStop();

            if (!_thread.IsAlive)
                return;

            var joined = await Task.Run(() => _thread.Join(timeoutMs)).ConfigureAwait(false);
            if (!joined)
                Logger.Warn($"HTTP MJPEG preview stop timeout: {_description}, timeout={timeoutMs}ms");
        }

        public void Dispose()
        {
            DisposeAsync(1000).GetAwaiter().GetResult();
        }

        private void ThreadMain()
        {
            try
            {
                if (!CreateRenderWindow())
                {
                    _startTcs.TrySetResult(false);
                    return;
                }

                _running = true;
                StartReaderThread();

                while (!_stopRequested)
                {
                    Application.DoEvents();
                    ApplyFillLayout();
                    RenderLatestFrame();
                    Thread.Sleep(RenderIntervalMs);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP MJPEG preview thread exception: {_description}, error={ex.Message}", ex);
                _startTcs.TrySetResult(false);
            }
            finally
            {
                BeginStop();
                JoinReaderThread();
                DestroyRenderWindow();
                _running = false;
                _cts.Dispose();
            }
        }

        private void StartReaderThread()
        {
            _readerThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "MJPEG Reader Thread-" + _description
            };
            _readerThread.Start();
        }

        private void JoinReaderThread()
        {
            try
            {
                if (_readerThread != null && _readerThread.IsAlive)
                    _readerThread.Join(1000);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG reader thread join failed: {_description}, error={ex.Message}");
            }
        }

        private bool CreateRenderWindow()
        {
            if (_parentHwnd == IntPtr.Zero || !IsWindow(_parentHwnd))
            {
                Logger.Error($"HTTP MJPEG preview invalid parent HWND: {_description}, hwnd={_parentHwnd}");
                return false;
            }

            _currentParentHwnd = _parentHwnd;
            // The preview is display-only. Disabling the cross-process child prevents
            // mouse clicks from moving input focus from the third-party host to Proxy.
            var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_DISABLED;
            if (_visible)
                windowStyle |= WS_VISIBLE;

            _videoHwnd = CreateWindowEx(WS_EX_NOPARENTNOTIFY | WS_EX_NOACTIVATE,
                "STATIC", "", windowStyle,
                0, 0, 1, 1, _parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
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
            var buffer = new List<byte>(ReadBufferSize * 4);
            int failureCount = 0;

            while (!_stopRequested && !_cts.IsCancellationRequested)
            {
                string failureReason = null;
                try
                {
                    var request = CreateRequest();
                    lock (_requestLock)
                        _request = request;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    {
                        Logger.Debug($"HTTP MJPEG流已打开: {_description}");

                        if (stream == null)
                            throw new IOException("MJPEG response stream is null");

                        var readBuffer = new byte[ReadBufferSize];
                        while (!_stopRequested && !_cts.IsCancellationRequested)
                        {
                            var read = stream.Read(readBuffer, 0, readBuffer.Length);
                            if (read <= 0)
                                break;

                            AppendBytes(buffer, readBuffer, read);
                            ExtractFrames(buffer);
                        }
                    }

                    if (!_stopRequested)
                        failureReason = "MJPEG stream ended";
                }
                catch (WebException ex)
                {
                    if (_stopRequested || _cts.IsCancellationRequested)
                        return;
                    failureReason = ex.Message;
                }
                catch (Exception ex)
                {
                    if (_stopRequested || _cts.IsCancellationRequested)
                        return;
                    failureReason = ex.Message;
                }
                finally
                {
                    lock (_requestLock)
                        _request = null;
                }

                if (_stopRequested || _cts.IsCancellationRequested)
                    return;

                failureCount++;
                buffer.Clear();
                if (failureCount >= SameUrlMaxFailures)
                {
                    Logger.Warn($"HTTP MJPEG同URL恢复失败({SameUrlMaxFailures}次): {_description}, error={failureReason}");
                    SignalStreamFault(failureReason);
                    return;
                }

                Logger.Warn($"HTTP MJPEG连接断开，{ReconnectDelayMs / 1000}秒后使用同URL重连" +
                            $"({failureCount}/{SameUrlMaxFailures}): {_description}, error={failureReason}");
                if (_cts.Token.WaitHandle.WaitOne(ReconnectDelayMs))
                    return;
            }
        }

        private void SignalStreamFault(string reason)
        {
            if (_disposed || _stopRequested || Interlocked.Exchange(ref _streamFaulted, 1) != 0)
                return;

            lock (_faultLock)
                _streamFaultReason = string.IsNullOrWhiteSpace(reason) ? "MJPEG stream unavailable" : reason;

            _running = false;
            _startTcs.TrySetResult(false);
            _stopRequested = true;
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

        private HttpWebRequest CreateRequest()
        {
            var request = (HttpWebRequest)WebRequest.Create(_url);
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

        private void ExtractFrames(List<byte> buffer)
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
                PublishFrame(frame);
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

        private void PublishFrame(byte[] frame)
        {
            Interlocked.Exchange(ref _latestFrame, frame);
            Interlocked.Increment(ref _latestFrameSeq);
            _startTcs.TrySetResult(true);
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
                    g.DrawImage(image, new Rectangle(0, 0, width, height));
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

        private void BeginStop()
        {
            _stopRequested = true;
            try { _cts.Cancel(); } catch { }
            AbortRequest();
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
