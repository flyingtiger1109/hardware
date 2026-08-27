using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class VlcPreviewController : IPreviewController
    {
        internal const int LayoutRefreshIntervalMs = 250;
        private const int VlcStallThresholdMs = 5000;

        private readonly TaskCompletionSource<bool> _startTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _exitTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;
        private readonly string _description;
        private readonly string _rtspUrl;
        private readonly IntPtr _parentHwnd;
        private readonly int _networkCachingMs;
        private readonly int _liveCachingMs;
        private readonly string _rtspTransport;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;
        private readonly bool _swapDimensions;
        private readonly bool _visible;
        private readonly bool _directRenderTarget;
        private readonly string _requestId;

        private volatile bool _abandoned;
        private volatile bool _running;
        private volatile bool _stopRequested;
        private int _disposeStarted;
        private VlcPreviewPlayer _player;
        private ExternalOverlayWindow _overlay;
        private Action<VlcPreviewController, string> _streamFaultHandler;
        private string _streamFaultReason;
        private int _streamFaulted;
        private long _lastMediaTimeMs;
        private DateTime _lastMediaUpdateUtc;

        private static int _createdThreadCount;
        private static int _liveThreadCount;
        private static int _exitTimeoutCount;

        private VlcPreviewController(string description, string rtspUrl, IntPtr parentHwnd,
            int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible,
            bool directRenderTarget, string requestId)
        {
            _description = description;
            _rtspUrl = rtspUrl;
            _parentHwnd = parentHwnd;
            _networkCachingMs = networkCachingMs;
            _liveCachingMs = liveCachingMs;
            _rtspTransport = rtspTransport;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _visible = visible;
            _directRenderTarget = directRenderTarget;
            _requestId = requestId;

            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "VLC预览线程-" + description
            };
            _thread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsRunning => _running;

        internal static int CreatedThreadCount => Volatile.Read(ref _createdThreadCount);
        internal static int LiveThreadCount => Volatile.Read(ref _liveThreadCount);
        internal static int ExitTimeoutCount => Volatile.Read(ref _exitTimeoutCount);

        internal string RequestId => _requestId;

        internal void SetStreamFaultHandler(Action<VlcPreviewController, string> handler)
        {
            _streamFaultHandler = handler;
        }

        public static async Task<VlcPreviewController> StartAsync(string description, string rtspUrl,
            IntPtr parentHwnd, int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible, int timeoutMs,
            bool directRenderTarget = false, string requestId = null)
        {
            var controller = new VlcPreviewController(description, rtspUrl, parentHwnd, networkCachingMs,
                liveCachingMs, rtspTransport, sourceWidth, sourceHeight, swapDimensions, visible,
                directRenderTarget, requestId);

            Interlocked.Increment(ref _createdThreadCount);
            try
            {
                controller._thread.Start();
            }
            catch
            {
                Interlocked.Decrement(ref _createdThreadCount);
                throw;
            }

            var completed = await Task.WhenAny(controller._startTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != controller._startTcs.Task)
            {
                controller._abandoned = true;
                Logger.Error($"VLC预览启动超时：{description}，超时={timeoutMs}ms，地址={VlcPreviewPlayer.SanitizeUrlForLog(rtspUrl)}。本次预览已放弃，终端切换继续完成。");
                await controller.DisposeAsync(Math.Min(1000, Math.Max(1, timeoutMs))).ConfigureAwait(false);
                return null;
            }

            bool ok;
            try
            {
                ok = await controller._startTcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC预览启动异常：{description}，错误={ex.Message}", ex);
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
            var firstRequest = Interlocked.CompareExchange(ref _disposeStarted, 1, 0) == 0;
            if (firstRequest)
            {
                _abandoned = true;
                _stopRequested = true;
            }

            if (_exitTcs.Task.IsCompleted)
                return;

            var completed = await Task.WhenAny(_exitTcs.Task,
                Task.Delay(Math.Max(1, timeoutMs))).ConfigureAwait(false);
            if (completed != _exitTcs.Task && firstRequest)
            {
                var timeoutCount = Interlocked.Increment(ref _exitTimeoutCount);
                Logger.Warn($"VLC预览线程退出超时：{_description}，超时={timeoutMs}ms，" +
                    $"存活线程数={LiveThreadCount}，退出超时次数={timeoutCount}。后台线程将继续尝试释放资源。");
            }
        }

        public void Dispose()
        {
            DisposeAsync(1000).GetAwaiter().GetResult();
        }

        private void ThreadMain()
        {
            var liveCount = Interlocked.Increment(ref _liveThreadCount);
            Logger.Debug($"VLC预览线程已启动：{_description}，存活线程数={liveCount}，已创建线程数={CreatedThreadCount}");
            try
            {
                // 外部跨进程预览使用本进程覆盖容器渲染，VLC 只挂到覆盖容器，避免跨进程子窗口操作。
                IntPtr renderHost = _parentHwnd;
                if (_directRenderTarget)
                {
                    _overlay = new ExternalOverlayWindow();
                    if (!_overlay.Create(_parentHwnd))
                    {
                        Logger.Error($"VLC覆盖容器创建失败：{_description}，锚点HWND={PreviewManager.FormatHwnd(_parentHwnd)}");
                        _startTcs.TrySetResult(false);
                        return;
                    }
                    renderHost = _overlay.Hwnd;
                }

                _player = new VlcPreviewPlayer(_description);
                var ok = _player.Play(_rtspUrl, renderHost, _networkCachingMs, _liveCachingMs,
                    _rtspTransport, _sourceWidth, _sourceHeight, _swapDimensions, _visible,
                    directRenderTarget: false);

                _running = ok && _player.IsRunning;
                _startTcs.TrySetResult(ok);

                if (!ok || _abandoned)
                    return;

                _lastMediaTimeMs = _player.MediaTimeMs;
                _lastMediaUpdateUtc = DateTime.UtcNow;

                var nextLayoutRefreshUtc = DateTime.UtcNow.AddMilliseconds(LayoutRefreshIntervalMs);
                while (!_stopRequested)
                {
                    Application.DoEvents();
                    if (_directRenderTarget && _overlay != null)
                        _overlay.Follow();
                    if (DateTime.UtcNow >= nextLayoutRefreshUtc)
                    {
                        _player.ApplyCoverLayout();
                        nextLayoutRefreshUtc = DateTime.UtcNow.AddMilliseconds(LayoutRefreshIntervalMs);
                    }
                    DetectStreamFault();
                    Thread.Sleep(20);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC预览线程异常：{_description}，错误={ex.Message}", ex);
                _startTcs.TrySetResult(false);
            }
            finally
            {
                try
                {
                    DisposePlayer();
                    _running = false;
                }
                finally
                {
                    liveCount = Interlocked.Decrement(ref _liveThreadCount);
                    _exitTcs.TrySetResult(true);
                    Logger.Debug($"VLC预览线程已退出：{_description}，存活线程数={liveCount}，" +
                        $"已创建线程数={CreatedThreadCount}，退出超时次数={ExitTimeoutCount}");
                }
            }
        }

        private void DetectStreamFault()
        {
            var player = _player;
            if (player == null)
                return;

            try
            {
                var state = player.MediaState;
                if (state == LibvlcError || state == LibvlcEnded)
                {
                    SignalStreamFault("VLC状态为错误或已结束");
                    return;
                }

                if (state != LibvlcPlaying)
                    return;

                var mediaTimeMs = player.MediaTimeMs;
                var nowUtc = DateTime.UtcNow;
                if (mediaTimeMs != _lastMediaTimeMs)
                {
                    _lastMediaTimeMs = mediaTimeMs;
                    _lastMediaUpdateUtc = nowUtc;
                }
                else if ((nowUtc - _lastMediaUpdateUtc).TotalMilliseconds >= VlcStallThresholdMs)
                {
                    SignalStreamFault("VLC视频流停滞");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC流故障检测异常：{_description}，错误={ex.Message}", ex);
            }
        }

        private void SignalStreamFault(string reason)
        {
            if (Interlocked.Exchange(ref _streamFaulted, 1) != 0)
                return;

            _streamFaultReason = reason;
            Logger.Warn($"VLC预览流故障：{_description}，原因={reason}");
            _stopRequested = true;

            var handler = _streamFaultHandler;
            if (handler == null)
                return;

            try
            {
                handler(this, string.IsNullOrWhiteSpace(_streamFaultReason) ? "VLC流故障" : _streamFaultReason);
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC流故障回调失败：{_description}，错误={ex.Message}", ex);
            }
        }

        private void DisposePlayer()
        {
            try
            {
                _player?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn($"VLC预览资源释放异常：{_description}，错误={ex.Message}");
            }
            finally
            {
                _player = null;
            }

            try
            {
                _overlay?.Destroy();
            }
            catch (Exception ex)
            {
                Logger.Warn($"VLC覆盖容器释放异常：{_description}，错误={ex.Message}");
            }
            finally
            {
                _overlay = null;
            }
        }

        // libvlc_state_t
        private const int LibvlcPlaying = 3;
        private const int LibvlcEnded = 6;
        private const int LibvlcError = 7;
    }
}
