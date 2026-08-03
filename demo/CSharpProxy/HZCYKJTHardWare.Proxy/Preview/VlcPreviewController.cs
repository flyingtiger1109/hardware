using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class VlcPreviewController : IPreviewController
    {
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
        private readonly PreviewScaleMode _scaleMode;
        private readonly bool _visible;
        private readonly bool _directRenderTarget;

        private volatile bool _abandoned;
        private volatile bool _running;
        private volatile bool _stopRequested;
        private int _disposeStarted;
        private VlcPreviewPlayer _player;

        private static int _createdThreadCount;
        private static int _liveThreadCount;
        private static int _exitTimeoutCount;

        private VlcPreviewController(string description, string rtspUrl, IntPtr parentHwnd,
            int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions,
            PreviewScaleMode scaleMode, bool visible,
            bool directRenderTarget)
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
            _scaleMode = scaleMode;
            _visible = visible;
            _directRenderTarget = directRenderTarget;

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

        public static async Task<VlcPreviewController> StartAsync(string description, string rtspUrl,
            IntPtr parentHwnd, int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible, int timeoutMs,
            bool directRenderTarget = false)
        {
            return await StartAsyncWithScaleMode(description, rtspUrl, parentHwnd,
                networkCachingMs, liveCachingMs, rtspTransport,
                sourceWidth, sourceHeight, swapDimensions, visible, timeoutMs,
                directRenderTarget, PreviewScaleMode.Stretch).ConfigureAwait(false);
        }

        internal static async Task<VlcPreviewController> StartAsyncWithScaleMode(
            string description, string rtspUrl,
            IntPtr parentHwnd, int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible, int timeoutMs,
            bool directRenderTarget, PreviewScaleMode scaleMode)
        {
            var controller = new VlcPreviewController(description, rtspUrl, parentHwnd, networkCachingMs,
                liveCachingMs, rtspTransport, sourceWidth, sourceHeight, swapDimensions, scaleMode, visible,
                directRenderTarget);

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
                Logger.Error($"VLC预览启动超时：{description}，timeout={timeoutMs}ms，url={VlcPreviewPlayer.SanitizeUrlForLog(rtspUrl)}。本次预览已放弃，终端切换继续完成。");
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
                Logger.Warn($"VLC预览线程退出超时：{_description}，timeout={timeoutMs}ms，" +
                    $"live={LiveThreadCount}，exitTimeouts={timeoutCount}。后台线程将继续尝试释放资源。");
            }
        }

        public void Dispose()
        {
            DisposeAsync(1000).GetAwaiter().GetResult();
        }

        private void ThreadMain()
        {
            var liveCount = Interlocked.Increment(ref _liveThreadCount);
            Logger.Debug($"VLC预览线程已启动：{_description}，live={liveCount}，created={CreatedThreadCount}");
            try
            {
                _player = new VlcPreviewPlayer();
                var ok = _player.PlayWithScaleMode(
                    _rtspUrl, _parentHwnd, _networkCachingMs, _liveCachingMs,
                    _rtspTransport, _sourceWidth, _sourceHeight, _swapDimensions, _visible,
                    _directRenderTarget, _scaleMode);

                _running = ok && _player.IsRunning;
                _startTcs.TrySetResult(ok);

                if (!ok || _abandoned)
                    return;

                var nextLayoutRefreshUtc = DateTime.UtcNow;
                while (!_stopRequested)
                {
                    Application.DoEvents();
                    if (DateTime.UtcNow >= nextLayoutRefreshUtc)
                    {
                        _player.ApplyVideoLayout();
                        nextLayoutRefreshUtc = DateTime.UtcNow.AddMilliseconds(250);
                    }
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
                    Logger.Debug($"VLC预览线程已退出：{_description}，live={liveCount}，" +
                        $"created={CreatedThreadCount}，exitTimeouts={ExitTimeoutCount}");
                }
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
        }
    }
}
