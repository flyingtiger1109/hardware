using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class VlcPreviewController : IDisposable
    {
        private readonly BlockingCollection<Action> _actions = new BlockingCollection<Action>();
        private readonly TaskCompletionSource<bool> _startTcs =
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

        private volatile bool _abandoned;
        private volatile bool _disposed;
        private volatile bool _running;
        private volatile bool _stopRequested;
        private VlcPreviewPlayer _player;

        private VlcPreviewController(string description, string rtspUrl, IntPtr parentHwnd,
            int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible)
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

            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "VLC预览线程-" + description
            };
            _thread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsRunning => _running;

        public static async Task<VlcPreviewController> StartAsync(string description, string rtspUrl,
            IntPtr parentHwnd, int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible, int timeoutMs)
        {
            var controller = new VlcPreviewController(description, rtspUrl, parentHwnd, networkCachingMs,
                liveCachingMs, rtspTransport, sourceWidth, sourceHeight, swapDimensions, visible);

            controller._thread.Start();

            var completed = await Task.WhenAny(controller._startTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != controller._startTcs.Task)
            {
                controller._abandoned = true;
                Logger.Error($"VLC预览启动超时：{description}，timeout={timeoutMs}ms，url={rtspUrl}。本次预览已放弃，终端切换继续完成。");
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

            if (!_startTcs.Task.IsCompleted)
            {
                _abandoned = true;
                return;
            }

            if (!_thread.IsAlive)
                return;

            var stopTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _actions.Add(() =>
                {
                    try
                    {
                        DisposePlayer();
                    }
                    finally
                    {
                        _stopRequested = true;
                        stopTcs.TrySetResult(true);
                    }
                });
            }
            catch (Exception ex)
            {
                _stopRequested = true;
                Logger.Warn($"提交VLC预览停止请求失败：{_description}，错误={ex.Message}");
                return;
            }

            var completed = await Task.WhenAny(stopTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != stopTcs.Task)
            {
                Logger.Warn($"VLC预览停止超时：{_description}，timeout={timeoutMs}ms。后台线程将继续尝试释放资源。");
            }
        }

        public void Dispose()
        {
            DisposeAsync(1000).GetAwaiter().GetResult();
        }

        private void ThreadMain()
        {
            try
            {
                _player = new VlcPreviewPlayer();
                var ok = _player.Play(_rtspUrl, _parentHwnd, _networkCachingMs, _liveCachingMs,
                    _rtspTransport, _sourceWidth, _sourceHeight, _swapDimensions, _visible);

                _running = ok && _player.IsRunning;
                _startTcs.TrySetResult(ok);

                if (!ok || _abandoned)
                    return;

                while (!_stopRequested)
                {
                    if (_actions.TryTake(out var action, 20))
                        action();

                    Application.DoEvents();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC预览线程异常：{_description}，错误={ex.Message}", ex);
                _startTcs.TrySetResult(false);
            }
            finally
            {
                DisposePlayer();
                _running = false;
                _actions.Dispose();
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
