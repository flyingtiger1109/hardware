using System;
using System.Diagnostics;
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
        internal const int LatestPlateFrameMaxBytes = 8 * 1024 * 1024;
        // 最新帧只在请求触发时抓拍；超过 1 秒即视为过期，不向第三方返回旧帧。
        internal const int LatestPlateFrameMaxAgeMs = 1000;
        internal const int LatestPlateFrameSnapshotWaitMs = 300;
        internal const int LatestPlateFrameRefreshTimeoutMs = 600;
        // 首次 frame_stale 只允许一次快速重试，总耗时保持在 1.1 秒预算内。
        internal const int LatestPlateFrameRetryDelayMs = 75;
        internal const int LatestPlateFrameRetryBudgetMs = 1100;
        internal const int LatestPlateFrameMaxRetries = 1;
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
        private readonly LatestPlateFrameCache _latestFrameCache =
            new LatestPlateFrameCache();

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
        private int _snapshotFailureCount;
        private string _lastSnapshotFailureCode;
        private int _snapshotRefreshRequested;
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
            return _latestFrameCache.TryGet(out snapshot);
        }

        /// <summary>
        /// 对已过期或尚未产生帧的会话请求一次有界刷新，不创建新的播放器。
        /// </summary>
        internal bool TryRefreshLatestFrame(int timeoutMs,
            out LatestPlateFrameSnapshot snapshot)
        {
            return TryRefreshLatestFrame(timeoutMs, LatestPlateFrameMaxAgeMs,
                out snapshot, out _);
        }

        /// <summary>
        /// 对已过期或尚未产生帧的会话请求一次有界刷新。
        /// 同一会话使用 CompareExchange 合并并发请求，实际 Snapshot 始终只由
        /// VLC 所属线程执行；等待方共享这次刷新产生的序列，不创建新的播放器。
        /// </summary>
        internal bool TryRefreshLatestFrame(int timeoutMs, int maxAgeMs,
            out LatestPlateFrameSnapshot snapshot, out bool refreshed)
        {
            snapshot = null;
            refreshed = false;
            if (!IsLatestFrameSourceRunning)
                return false;

            if (_latestFrameCache.TryGet(out var cached) &&
                IsLatestFrameFresh(cached, maxAgeMs, DateTime.UtcNow))
            {
                snapshot = cached;
                return true;
            }

            long previousSequence = 0;
            _latestFrameCache.TryGetSequence(out previousSequence);

            // 只有第一个请求负责投递刷新；其余并发请求等待同一个新序列。
            Interlocked.CompareExchange(ref _snapshotRefreshRequested, 1, 0);
            var stopwatch = Stopwatch.StartNew();
            var waitMs = Math.Max(1, timeoutMs);
            while (!_stopRequested && stopwatch.ElapsedMilliseconds < waitMs)
            {
                if (_latestFrameCache.TryGetSequence(out var currentSequence) &&
                    currentSequence > previousSequence &&
                    TryGetLatestFrame(out var current))
                {
                    snapshot = current;
                    refreshed = true;
                    return true;
                }

                Thread.Sleep(Math.Min(10,
                    Math.Max(1, waitMs - (int)stopwatch.ElapsedMilliseconds)));
            }

            if (_latestFrameCache.TryGetSequence(out var finalSequence) &&
                finalSequence > previousSequence &&
                TryGetLatestFrame(out snapshot))
            {
                refreshed = true;
                return true;
            }

            return false;
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
                    _directRenderTarget, _captureLatestFrame);

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
                    var nowUtc = DateTime.UtcNow;
                    if (nowUtc >= nextLayoutRefreshUtc)
                    {
                        _player.ApplyCoverLayout();
                        nextLayoutRefreshUtc = nowUtc.AddMilliseconds(LayoutRefreshIntervalMs);
                    }
                    var refreshRequested = Interlocked.Exchange(
                        ref _snapshotRefreshRequested, 0) != 0;
                    if (_captureLatestFrame && !_stopRequested && refreshRequested)
                    {
                        CaptureLatestFrame();
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
                RecordSnapshotFailure("player_not_ready", "VLC播放器尚未就绪");
                return;
            }

            var playerState = -1;
            var snapshotReturnCode = -1;
            var fileBytes = 0L;
            var detectedFormat = "unknown";
            var videoWidth = 0;
            var videoHeight = 0;
            try
            {
                if (File.Exists(_latestFrameTempPath))
                    File.Delete(_latestFrameTempPath);

                playerState = player.MediaState;
                if (playerState != LibvlcPlaying)
                {
                    SetLatestFrameFailure(LatestFrameFailureNotReady);
                    RecordSnapshotFailure("player_not_playing",
                        "VLC视频尚未进入Playing状态", playerState,
                        snapshotReturnCode, detectedFormat, fileBytes,
                        videoWidth, videoHeight);
                    return;
                }

                if (!player.TryGetVideoSize(out videoWidth, out videoHeight))
                {
                    SetLatestFrameFailure(LatestFrameFailureNotReady);
                    RecordSnapshotFailure("video_track_not_ready",
                        "VLC视频轨道尺寸尚未可用", playerState,
                        snapshotReturnCode, detectedFormat, fileBytes,
                        videoWidth, videoHeight);
                    return;
                }

                if (!player.TryTakeSnapshot(_latestFrameTempPath, videoWidth,
                    videoHeight, out snapshotReturnCode))
                {
                    SetLatestFrameFailure(LatestFrameFailureNotReady);
                    RecordSnapshotFailure("snapshot_call_failed",
                        "libVLC未返回快照", playerState, snapshotReturnCode,
                        detectedFormat, fileBytes, videoWidth, videoHeight);
                    return;
                }

                if (!TryReadSnapshotFile(_latestFrameTempPath, out var rawSnapshot,
                    out fileBytes, out var fileFailureReason))
                {
                    SetLatestFrameFailure(fileFailureReason == "snapshot_file_too_large"
                        ? LatestFrameFailureTooLarge : LatestFrameFailureNotReady);
                    RecordSnapshotFailure(fileFailureReason,
                        "快照文件未在限定时间内稳定可读", playerState,
                        snapshotReturnCode, detectedFormat, fileBytes,
                        videoWidth, videoHeight);
                    return;
                }

                if (!SnapshotImageNormalizer.TryNormalizeToJpeg(rawSnapshot,
                    out var jpeg, out detectedFormat, out var width,
                    out var height, out var normalizeFailureReason))
                {
                    SetLatestFrameFailure(LatestFrameFailureDataInvalid);
                    RecordSnapshotFailure(normalizeFailureReason ?? "snapshot_data_invalid",
                        "快照无法解码为有效JPEG", playerState,
                        snapshotReturnCode, detectedFormat, fileBytes,
                        width, height);
                    return;
                }

                if (jpeg.Length > LatestPlateFrameMaxBytes)
                {
                    SetLatestFrameFailure(LatestFrameFailureTooLarge);
                    RecordSnapshotFailure("snapshot_jpeg_too_large",
                        "规范化后的JPEG超过8MB限制", playerState,
                        snapshotReturnCode, detectedFormat, jpeg.Length,
                        width, height);
                    return;
                }

                if (_stopRequested)
                    return;
                _latestFrameCache.Publish(jpeg, width, height, "jpeg", DateTime.UtcNow);
                SetLatestFrameFailure(LatestFrameFailureNone);

                var previousFailures = Interlocked.Exchange(ref _snapshotFailureCount, 0);
                var previousFailureCode = _lastSnapshotFailureCode;
                _lastSnapshotFailureCode = null;
                if (previousFailures > 0)
                {
                    Logger.Info($"VLC车牌最新帧已恢复：{_description}，尺寸={width}x{height}，" +
                                $"DetectedFormat={detectedFormat}，上次故障={previousFailureCode}");
                }
            }
            catch (Exception ex)
            {
                SetLatestFrameFailure(LatestFrameFailureNotReady);
                RecordSnapshotFailure("snapshot_exception", ex.Message, playerState,
                    snapshotReturnCode, detectedFormat, fileBytes,
                    videoWidth, videoHeight);
            }
        }

        private bool TryReadSnapshotFile(string path, out byte[] data,
            out long fileBytes, out string failureReason)
        {
            return SnapshotFileReader.TryReadStable(path, LatestPlateFrameMaxBytes,
                LatestPlateFrameSnapshotWaitMs, () => _stopRequested, out data,
                out fileBytes, out failureReason);
        }

        private void RecordSnapshotFailure(string failureCode, string reason,
            int playerState = -1, int snapshotReturnCode = -1,
            string detectedFormat = "unknown", long fileBytes = 0,
            int width = 0, int height = 0)
        {
            if (!string.Equals(_lastSnapshotFailureCode, failureCode,
                StringComparison.Ordinal))
            {
                _lastSnapshotFailureCode = failureCode;
                Interlocked.Exchange(ref _snapshotFailureCount, 0);
            }

            var count = Interlocked.Increment(ref _snapshotFailureCount);
            var message = BuildSnapshotFailureMessage(failureCode, reason, count,
                playerState, snapshotReturnCode, detectedFormat, fileBytes,
                width, height);
            if (count == 1)
            {
                Logger.Warn(message);
            }
            else
            {
                Logger.TryLogRateLimited(
                    "VlcPlateSnapshot|debug|" + _description + "|" + failureCode,
                    LogModules.Preview, "调试", message);
                if (count % 50 == 0)
                {
                    Logger.TryLogRateLimited(
                        "VlcPlateSnapshot|aggregate|" + _description + "|" + failureCode,
                        LogModules.Preview, "警告", message);
                }
            }
        }

        private string BuildSnapshotFailureMessage(string failureCode, string reason,
            int count, int playerState, int snapshotReturnCode,
            string detectedFormat, long fileBytes, int width, int height)
        {
            return $"VLC车牌最新帧获取失败：Plate={GetPlateCodeForLog()}，" +
                   $"RequestId={_requestId ?? "<无>"}，资源={_description}，" +
                   $"Failure={failureCode}，PlayerState={playerState}，" +
                   $"SnapshotRet={snapshotReturnCode}，DetectedFormat={detectedFormat ?? "unknown"}，" +
                   $"FileBytes={fileBytes}，Width={width}，Height={height}，" +
                   $"LastGoodFrameAgeMs={GetLastGoodFrameAgeMs()}，次数={count}，原因={reason}";
        }

        private long GetLastGoodFrameAgeMs()
        {
            if (!_latestFrameCache.TryGetCapturedUtc(out var capturedUtc))
                return -1;

            var ageMs = (DateTime.UtcNow - capturedUtc).TotalMilliseconds;
            return ageMs < 0 ? 0 : (long)ageMs;
        }

        private string GetPlateCodeForLog()
        {
            if (_description != null && _description.IndexOf("RJ2",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "RJ2";
            if (_description != null && _description.IndexOf("RJ3",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "RJ3";
            if (_description != null && _description.IndexOf("CJ",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "CJ";
            return "unknown";
        }

        private void ClearLatestFrame()
        {
            _latestFrameCache.Clear();
            Interlocked.Exchange(ref _snapshotRefreshRequested, 0);
            _lastSnapshotFailureCode = null;
            SetLatestFrameFailure(LatestFrameFailureNone);
        }

        private void SetLatestFrameFailure(int failure)
        {
            Volatile.Write(ref _latestFrameFailure, failure);
        }

        private static bool IsLatestFrameFresh(LatestPlateFrameSnapshot snapshot,
            int maxAgeMs, DateTime nowUtc)
        {
            if (snapshot == null || snapshot.Jpeg == null || snapshot.Jpeg.Length == 0 ||
                snapshot.Width <= 0 || snapshot.Height <= 0 ||
                !string.Equals(snapshot.Format, "jpeg", StringComparison.OrdinalIgnoreCase))
                return false;

            var ageMs = (nowUtc - snapshot.CapturedUtc).TotalMilliseconds;
            return ageMs >= 0 && ageMs <= Math.Max(0, maxAgeMs);
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
