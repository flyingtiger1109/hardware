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
        private const int RenderIntervalMs = 15;

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

        private volatile bool _disposed;
        private volatile bool _running;
        private volatile bool _stopRequested;
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
            get { return _running && _videoHwnd != IntPtr.Zero && IsWindow(_videoHwnd); }
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
                Logger.Warn($"HTTP MJPEG预览启动超时，将尝试VLC回退：{description}，timeout={timeoutMs}ms，url={url}");
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
                Logger.Warn($"HTTP MJPEG预览启动异常，将尝试VLC回退：{description}，错误={ex.Message}");
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
                Logger.Warn($"HTTP MJPEG预览停止超时：{_description}，timeout={timeoutMs}ms");
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
                Logger.Error($"HTTP MJPEG预览线程异常：{_description}，错误={ex.Message}", ex);
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
                {
                    var joined = _readerThread.Join(1000);
                    if (!joined)
                    {
                        Logger.Warn($"HTTP MJPEG读取线程停止超时：{_description}，线程={_readerThread.Name}，已请求停止={_stopRequested}，已请求取消={_cts.IsCancellationRequested}，url={_url}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"等待HTTP MJPEG读取线程退出失败：{_description}，错误={ex.Message}");
            }
        }

        private bool CreateRenderWindow()
        {
            if (_parentHwnd == IntPtr.Zero || !IsWindow(_parentHwnd))
            {
                Logger.Error($"HTTP MJPEG预览父窗口句柄无效：{_description}，hwnd={_parentHwnd}");
                return false;
            }

            _currentParentHwnd = _parentHwnd;
            var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN;
            if (_visible)
                windowStyle |= WS_VISIBLE;

            _videoHwnd = CreateWindowEx(0, "STATIC", "", windowStyle,
                0, 0, 1, 1, _parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_videoHwnd == IntPtr.Zero)
            {
                Logger.Error($"HTTP MJPEG预览创建子窗口失败：{_description}");
                return false;
            }

            ApplyFillLayout();
            Logger.Info($"HTTP MJPEG预览窗口已创建：{_description}，videoHwnd={_videoHwnd}，parent={_parentHwnd}，source={_sourceWidth}x{_sourceHeight}，交换宽高={_swapDimensions}");
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
                Logger.Warn($"HTTP MJPEG预览销毁窗口失败：{_description}，错误={ex.Message}");
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
            try
            {
                var request = CreateRequest();
                lock (_requestLock)
                    _request = request;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    Logger.Info($"HTTP MJPEG视频流已打开：{_description}，contentType={response.ContentType}，url={_url}");
                    if (stream == null)
                    {
                        _startTcs.TrySetResult(false);
                        return;
                    }

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
                {
                    Logger.Warn($"HTTP MJPEG视频流已结束：{_description}，url={_url}");
                    _startTcs.TrySetResult(false);
                }
            }
            catch (WebException ex)
            {
                if (!_stopRequested)
                {
                    Logger.Warn($"HTTP MJPEG视频流错误：{_description}，status={ex.Status}，错误={ex.Message}");
                    _startTcs.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                if (!_stopRequested)
                {
                    Logger.Warn($"HTTP MJPEG读取线程异常：{_description}，错误={ex.Message}");
                    _startTcs.TrySetResult(false);
                }
            }
            finally
            {
                lock (_requestLock)
                    _request = null;

                if (!_stopRequested)
                    _stopRequested = true;
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
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.AllowReadStreamBuffering = false;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            request.Headers[HttpRequestHeader.Pragma] = "no-cache";
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return request;
        }

        private void AppendBytes(List<byte> buffer, byte[] bytes, int count)
        {
            if (count <= 0)
                return;

            buffer.AddRange(new ArraySegment<byte>(bytes, 0, count));

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
                using (var ms = new MemoryStream(frame))
                using (var image = Image.FromStream(ms, false, false))
                {
                    DrawImage(image);
                }
                _renderedFrameSeq = seq;
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG渲染帧失败：{_description}，错误={ex.Message}");
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
                MoveWindow(_videoHwnd, 0, 0, hostW, hostH, false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HTTP MJPEG应用填充布局失败：{_description}，错误={ex.Message}");
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
            HttpWebRequest request = null;
            try
            {
                lock (_requestLock)
                {
                    request = _request;
                    _request = null;
                }

                request?.Abort();
            }
            catch { }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
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
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_CLIPCHILDREN = 0x02000000;

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
