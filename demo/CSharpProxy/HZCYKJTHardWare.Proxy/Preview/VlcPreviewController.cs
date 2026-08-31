using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public sealed class VlcPreviewController : IPreviewController
    {
        internal const int LayoutRefreshIntervalMs = 250;
        internal const int LatestPlateFrameCaptureIntervalMs = 200;
        internal const int LatestPlateFrameMaxBytes = 8 * 1024 * 1024;
        internal const int LatestPlateFrameMaxAgeMs = 3000;
        internal const int LatestFrameFailureNone = 0;
        internal const int LatestFrameFailureNotReady = 1;
        internal const int LatestFrameFailureDataInvalid = 2;
        internal const int LatestFrameFailureTooLarge = 3;
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
        private readonly bool _captureLatestFrame;
        private readonly string _latestFrameTempPath;
        private readonly object _latestFrameLock = new object();

        private volatile bool _abandoned;
        private volatile bool _running;
        private volatile bool _stopRequested;
        private int _disposeStarted;
        private VlcPreviewPlayer _player;
        private Action<VlcPreviewController, string> _streamFaultHandler;
        private string _streamFaultReason;
        private int _streamFaulted;
        private long _lastMediaTimeMs;
        private DateTime _lastMediaUpdateUtc;
        private byte[] _latestJpeg;
        private int _latestFrameWidth;
        private int _latestFrameHeight;
        private long _latestFrameSequence;
        private DateTime _latestFrameCapturedUtc;
        private int _snapshotFailureCount;
        private int _latestFrameFailure;

        private static int _createdThreadCount;
        private static int _liveThreadCount;
        private static int _exitTimeoutCount;

        private VlcPreviewController(string description, string rtspUrl, IntPtr parentHwnd,
            int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible,
            bool directRenderTarget, string requestId, bool captureLatestFrame)
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
            _captureLatestFrame = captureLatestFrame;
            _latestFrameTempPath = captureLatestFrame ? BuildLatestFrameTempPath() : null;

            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "VLC预览线程-" + description
            };
            _thread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsRunning => _running;

        internal bool IsLatestFrameSourceRunning =>
            _running && !_stopRequested && Volatile.Read(ref _streamFaulted) == 0;

        internal int LatestFrameFailure => Volatile.Read(ref _latestFrameFailure);

        internal static int CreatedThreadCount => Volatile.Read(ref _createdThreadCount);
        internal static int LiveThreadCount => Volatile.Read(ref _liveThreadCount);
        internal static int ExitTimeoutCount => Volatile.Read(ref _exitTimeoutCount);

        internal string RequestId => _requestId;

        internal bool TryGetLatestFrame(out LatestPlateFrameSnapshot snapshot)
        {
            lock (_latestFrameLock)
            {
                if (_latestJpeg == null || _latestJpeg.Length == 0)
                {
                    snapshot = null;
                    return false;
                }

                var copy = new byte[_latestJpeg.Length];
                Buffer.BlockCopy(_latestJpeg, 0, copy, 0, _latestJpeg.Length);
                snapshot = new LatestPlateFrameSnapshot(
                    copy, _latestFrameWidth, _latestFrameHeight,
                    _latestFrameSequence, _latestFrameCapturedUtc);
                return true;
            }
        }

        internal void SetStreamFaultHandler(Action<VlcPreviewController, string> handler)
        {
            _streamFaultHandler = handler;
        }

        public static async Task<VlcPreviewController> StartAsync(string description, string rtspUrl,
            IntPtr parentHwnd, int networkCachingMs, int liveCachingMs, string rtspTransport,
            int sourceWidth, int sourceHeight, bool swapDimensions, bool visible, int timeoutMs,
            bool directRenderTarget = false, string requestId = null,
            bool captureLatestFrame = false)
        {
            var controller = new VlcPreviewController(description, rtspUrl, parentHwnd, networkCachingMs,
                liveCachingMs, rtspTransport, sourceWidth, sourceHeight, swapDimensions, visible,
                directRenderTarget, requestId, captureLatestFrame);

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
                _player = new VlcPreviewPlayer();
                var ok = _player.Play(_rtspUrl, _parentHwnd, _networkCachingMs, _liveCachingMs,
                    _rtspTransport, _sourceWidth, _sourceHeight, _swapDimensions, _visible,
                    _directRenderTarget);

                _running = ok && _player.IsRunning;
                _startTcs.TrySetResult(ok);

                if (!ok || _abandoned)
                    return;

                _lastMediaTimeMs = _player.MediaTimeMs;
                _lastMediaUpdateUtc = DateTime.UtcNow;

                var nextLayoutRefreshUtc = DateTime.UtcNow.AddMilliseconds(LayoutRefreshIntervalMs);
                var nextSnapshotUtc = DateTime.UtcNow;
                while (!_stopRequested)
                {
                    Application.DoEvents();
                    var nowUtc = DateTime.UtcNow;
                    if (nowUtc >= nextLayoutRefreshUtc)
                    {
                        _player.ApplyCoverLayout();
                        nextLayoutRefreshUtc = nowUtc.AddMilliseconds(LayoutRefreshIntervalMs);
                    }
                    if (_captureLatestFrame && nowUtc >= nextSnapshotUtc && !_stopRequested)
                    {
                        CaptureLatestFrame();
                        nextSnapshotUtc = DateTime.UtcNow.AddMilliseconds(
                            LatestPlateFrameCaptureIntervalMs);
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
                    ClearLatestFrame();
                    DeleteLatestFrameTempFile();
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

        private void CaptureLatestFrame()
        {
            var player = _player;
            if (player == null || !player.IsRunning ||
                string.IsNullOrWhiteSpace(_latestFrameTempPath))
            {
                SetLatestFrameFailure(LatestFrameFailureNotReady);
                return;
            }

            try
            {
                if (File.Exists(_latestFrameTempPath))
                    File.Delete(_latestFrameTempPath);

                var hasVideoSize = player.TryGetVideoSize(out var videoWidth,
                    out var videoHeight);
                if (!player.TryTakeSnapshot(_latestFrameTempPath,
                    hasVideoSize ? videoWidth : 0,
                    hasVideoSize ? videoHeight : 0))
                {
                    SetLatestFrameFailure(LatestFrameFailureNotReady);
                    RecordSnapshotFailure("libVLC未返回快照");
                    return;
                }

                if (!TryReadSnapshotFile(_latestFrameTempPath, out var jpeg))
                    return;

                if (!JpegFrameValidator.TryGetDimensions(jpeg, out var width, out var height))
                {
                    SetLatestFrameFailure(LatestFrameFailureDataInvalid);
                    RecordSnapshotFailure("快照不是有效JPEG或缺少实际尺寸");
                    return;
                }

                lock (_latestFrameLock)
                {
                    if (_stopRequested)
                        return;
                    _latestJpeg = jpeg;
                    _latestFrameWidth = width;
                    _latestFrameHeight = height;
                    _latestFrameSequence++;
                    _latestFrameCapturedUtc = DateTime.UtcNow;
                }
                SetLatestFrameFailure(LatestFrameFailureNone);

                var previousFailures = Interlocked.Exchange(ref _snapshotFailureCount, 0);
                if (previousFailures > 0)
                    Logger.Info($"VLC车牌最新帧已恢复：{_description}，尺寸={width}x{height}");
            }
            catch (Exception ex)
            {
                SetLatestFrameFailure(LatestFrameFailureNotReady);
                RecordSnapshotFailure(ex.Message);
            }
        }

        private bool TryReadSnapshotFile(string path, out byte[] jpeg)
        {
            jpeg = null;
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                SetLatestFrameFailure(LatestFrameFailureNotReady);
                RecordSnapshotFailure("快照文件为空");
                return false;
            }
            if (fileInfo.Length > LatestPlateFrameMaxBytes || fileInfo.Length > int.MaxValue)
            {
                SetLatestFrameFailure(LatestFrameFailureTooLarge);
                RecordSnapshotFailure("快照文件超过8MB限制");
                return false;
            }

            var buffer = new byte[(int)fileInfo.Length];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                var offset = 0;
                while (offset < buffer.Length)
                {
                    var read = stream.Read(buffer, offset, buffer.Length - offset);
                    if (read <= 0)
                    {
                        SetLatestFrameFailure(LatestFrameFailureNotReady);
                        RecordSnapshotFailure("快照文件读取不完整");
                        return false;
                    }
                    offset += read;
                }
            }

            jpeg = buffer;
            return true;
        }

        private void RecordSnapshotFailure(string reason)
        {
            var count = Interlocked.Increment(ref _snapshotFailureCount);
            if (count == 1 || count % 50 == 0)
                Logger.Warn($"VLC车牌最新帧获取失败：{_description}，次数={count}，原因={reason}");
        }

        private void ClearLatestFrame()
        {
            lock (_latestFrameLock)
            {
                _latestJpeg = null;
                _latestFrameWidth = 0;
                _latestFrameHeight = 0;
                _latestFrameCapturedUtc = default(DateTime);
            }
            SetLatestFrameFailure(LatestFrameFailureNone);
        }

        private void SetLatestFrameFailure(int failure)
        {
            Volatile.Write(ref _latestFrameFailure, failure);
        }

        private void DeleteLatestFrameTempFile()
        {
            if (string.IsNullOrWhiteSpace(_latestFrameTempPath))
                return;
            try
            {
                if (File.Exists(_latestFrameTempPath))
                    File.Delete(_latestFrameTempPath);
            }
            catch (Exception ex)
            {
                Logger.Warn($"VLC车牌最新帧临时文件清理失败：{_latestFrameTempPath}，错误={ex.Message}");
            }
        }

        private static string BuildLatestFrameTempPath()
        {
            try
            {
                var tempDirectory = Path.GetTempPath();
                return Path.Combine(tempDirectory,
                    "HZCYKJTHardWare_PlateFrame_" + Guid.NewGuid().ToString("N") + ".jpg");
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "HZCYKJTHardWare_PlateFrame_" + Guid.NewGuid().ToString("N") + ".jpg");
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
        }

        // libvlc_state_t
        private const int LibvlcPlaying = 3;
        private const int LibvlcEnded = 6;
        private const int LibvlcError = 7;
    }
}
