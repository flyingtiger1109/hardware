using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public class VlcPreviewPlayer : IDisposable
    {
        private IntPtr _libVlcCoreHandle;
        private IntPtr _libVlcHandle;
        private IntPtr _vlcInstance;
        private IntPtr _mediaPlayer;
        private bool _running;
        private IntPtr _currentParentHwnd;
        private bool _ownsVideoHwnd;
        private bool _directRenderTarget;
        private string _vlcDir;
        private const string RiskySftpPluginRelativePath = @"plugins\access\libsftp_plugin.dll";
        private const uint LoadWithAlteredSearchPath = 0x00000008;
        private const ushort ImageFileMachineI386 = 0x014c;
        private const ushort ImageFileMachineAmd64 = 0x8664;
        private static readonly object RiskyPluginCheckLock = new object();
        private static readonly HashSet<string> RiskyPluginCheckedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Cover 布局使用的源图像尺寸
        private int _sourceWidth;
        private int _sourceHeight;
        private bool _swapDimensions;

        // 布局缓存
        private int _lastHostW, _lastHostH, _lastSrcW, _lastSrcH;

        // libVLC 函数委托
        private delegate IntPtr LibvlcNew(int argc, IntPtr argv);
        private delegate void LibvlcRelease(IntPtr instance);
        private delegate IntPtr LibvlcMediaNewLocation(IntPtr instance, IntPtr mrl);
        private delegate void LibvlcMediaAddOption(IntPtr media, IntPtr option);
        private delegate void LibvlcMediaRelease(IntPtr media);
        private delegate IntPtr LibvlcMediaPlayerNewFromMedia(IntPtr media);
        private delegate void LibvlcMediaPlayerRelease(IntPtr player);
        private delegate void LibvlcMediaPlayerSetHwnd(IntPtr player, IntPtr drawable);
        private delegate int LibvlcMediaPlayerPlay(IntPtr player);
        private delegate void LibvlcMediaPlayerStop(IntPtr player);
        private delegate int LibvlcMediaPlayerGetState(IntPtr player);
        private delegate long LibvlcMediaPlayerGetTime(IntPtr player);
        private delegate int LibvlcVideoTakeSnapshot(IntPtr player, uint num,
            IntPtr path, uint width, uint height);
        private delegate int LibvlcVideoGetSize(IntPtr player, uint num,
            out uint width, out uint height);
        private delegate void LibvlcVideoSetAspectRatio(IntPtr player, IntPtr ratio);
        private delegate void LibvlcVideoSetScale(IntPtr player, float factor);
        private delegate void LibvlcVideoSetInput(IntPtr player, uint enabled);

        private LibvlcNew _fnNew;
        private LibvlcRelease _fnRelease;
        private LibvlcMediaNewLocation _fnMediaNewLocation;
        private LibvlcMediaAddOption _fnMediaAddOption;
        private LibvlcMediaRelease _fnMediaRelease;
        private LibvlcMediaPlayerNewFromMedia _fnPlayerNewFromMedia;
        private LibvlcMediaPlayerRelease _fnPlayerRelease;
        private LibvlcMediaPlayerSetHwnd _fnPlayerSetHwnd;
        private LibvlcMediaPlayerPlay _fnPlayerPlay;
        private LibvlcMediaPlayerStop _fnPlayerStop;
        private LibvlcMediaPlayerGetState _fnPlayerGetState;
        private LibvlcMediaPlayerGetTime _fnPlayerGetTime;
        private LibvlcVideoTakeSnapshot _fnVideoTakeSnapshot;
        private LibvlcVideoGetSize _fnVideoGetSize;
        private LibvlcVideoSetAspectRatio _fnVideoSetAspectRatio;
        private LibvlcVideoSetScale _fnVideoSetScale;
        private LibvlcVideoSetInput _fnVideoSetMouseInput;
        private LibvlcVideoSetInput _fnVideoSetKeyInput;

        public bool IsRunning => _running && _videoHwnd != IntPtr.Zero && IsWindow(_videoHwnd);
        public IntPtr RenderFormHandle => _videoHwnd;
        public int WarmupMs { get; private set; }

        /// <summary>
        /// libVLC 播放器状态（libvlc_state_t）：0=NothingSpecial，3=Playing，5=Stopped，6=Ended，7=Error。
        /// </summary>
        internal int MediaState => _mediaPlayer == IntPtr.Zero || _fnPlayerGetState == null
            ? 0 : _fnPlayerGetState(_mediaPlayer);

        /// <summary>当前播放位置（毫秒）。用于判断 VLC 流是否停滞。</summary>
        internal long MediaTimeMs => _mediaPlayer == IntPtr.Zero || _fnPlayerGetTime == null
            ? 0 : _fnPlayerGetTime(_mediaPlayer);

        /// <summary>
        /// 使用现有 VLC 播放器输出一张原始尺寸快照，不创建新的播放会话。
        /// 必须由所属 VLC 预览线程调用，避免与播放器释放发生并发。
        /// </summary>
        internal bool TryTakeSnapshot(string path, int width = 0, int height = 0)
        {
            if (string.IsNullOrWhiteSpace(path) || _mediaPlayer == IntPtr.Zero ||
                _fnVideoTakeSnapshot == null)
                return false;
            if (width < 0 || height < 0)
                return false;

            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                pathPtr = Marshal.StringToHGlobalAnsi(path);
                return _fnVideoTakeSnapshot(_mediaPlayer, 0, pathPtr,
                    (uint)width, (uint)height) == 0;
            }
            catch (Exception ex)
            {
                Logger.Debug($"VLC快照调用异常: {ex.Message}");
                return false;
            }
            finally
            {
                if (pathPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(pathPtr);
            }
        }

        /// <summary>读取 VLC 当前视频轨道的实际尺寸，读取不到时由调用方回退。</summary>
        internal bool TryGetVideoSize(out int width, out int height)
        {
            width = 0;
            height = 0;
            if (_mediaPlayer == IntPtr.Zero || _fnVideoGetSize == null)
                return false;

            try
            {
                uint nativeWidth;
                uint nativeHeight;
                if (_fnVideoGetSize(_mediaPlayer, 0, out nativeWidth, out nativeHeight) != 0 ||
                    nativeWidth == 0 || nativeHeight == 0 ||
                    nativeWidth > int.MaxValue || nativeHeight > int.MaxValue)
                    return false;

                width = (int)nativeWidth;
                height = (int)nativeHeight;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug($"VLC视频尺寸读取异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 预加载 VLC 库并创建短生命周期实例以预热 VLC 引擎，降低首次播放延迟。
        /// 行为与 Delphi TVlcWarmupThread 保持一致。
        /// </summary>
        public void Warmup()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!LoadVlc())
                {
                    Logger.Warn("VLC预热失败: 无法加载VLC库");
                    return;
                }

                // 创建最小 VLC 实例以触发库初始化
                var args = new List<string>
                {
                    "--no-video-title-show", "--no-xlib", "--quiet", "--no-plugins-cache", "--intf", "dummy"
                };
                var argPtrs = new IntPtr[args.Count];
                for (int i = 0; i < args.Count; i++)
                    argPtrs[i] = Marshal.StringToHGlobalAnsi(args[i]);
                var argvPtr = Marshal.AllocHGlobal(IntPtr.Size * args.Count);
                Marshal.Copy(argPtrs, 0, argvPtr, args.Count);

                var instance = _fnNew(args.Count, argvPtr);

                for (int i = 0; i < args.Count; i++)
                    Marshal.FreeHGlobal(argPtrs[i]);
                Marshal.FreeHGlobal(argvPtr);

                if (instance != IntPtr.Zero)
                {
                    _fnRelease(instance);
                }
                WarmupMs = (int)sw.ElapsedMilliseconds;
                Logger.Info($"VLC预热完成: {WarmupMs}ms");
            }
            catch (Exception ex)
            {
                Logger.Warn($"VLC预热失败: {ex.Message}");
            }
        }

        public bool LoadVlc(string vlcDir = null)
        {
            if (_libVlcHandle != IntPtr.Zero) return true;

            // 优先级 1：输出目录中的本地 vlc 目录，与 Delphi 一致并包含完整插件
            foreach (var directoryName in GetLocalVlcDirectoryNames(Environment.Is64BitProcess))
            {
                var localVlcDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directoryName);
                if (Directory.Exists(localVlcDir) && TryLoadFromDir(localVlcDir)) return true;
            }

            // 优先级 2：尝试已提取的嵌入资源，保持向后兼容
            var extractedDir = VlcResourceExtractor.EnsureExtracted();
            if (!string.IsNullOrEmpty(extractedDir) && System.IO.Directory.Exists(extractedDir))
            {
                if (TryLoadFromDir(extractedDir)) return true;
            }

            // 优先级 3：外部 VLC 安装目录
            var searchDirs = new[]
            {
                vlcDir,
                @"C:\Program Files\VideoLAN\VLC",
                @"C:\Program Files (x86)\VideoLAN\VLC",
                @"D:\VLC",
                @"C:\VLC"
            };

            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) continue;
                if (TryLoadFromDir(dir)) return true;
            }

            Logger.Error("VLC not found");
            return false;
        }

        private bool TryLoadFromDir(string dir)
        {
            var corePath = Path.Combine(dir, "libvlccore.dll");
            var libPath = Path.Combine(dir, "libvlc.dll");

            if (!File.Exists(corePath) || !File.Exists(libPath))
                return false;

            try
            {
                ushort coreMachine = 0;
                ushort libMachine = 0;
                var is64BitProcess = Environment.Is64BitProcess;
                if (!IsPeMachineCompatible(corePath, is64BitProcess, out coreMachine) ||
                    !IsPeMachineCompatible(libPath, is64BitProcess, out libMachine))
                {
                    Logger.Warn(
                        $"Skipping incompatible VLC directory: dir={dir}, " +
                        $"process={(is64BitProcess ? "x64" : "x86")}, " +
                        $"libvlccore={FormatPeMachine(coreMachine)}, libvlc={FormatPeMachine(libMachine)}");
                    return false;
                }

                DisableRiskySftpPlugin(dir);
                _libVlcCoreHandle = LoadLibraryEx(corePath, IntPtr.Zero,
                    LoadWithAlteredSearchPath);
                if (_libVlcCoreHandle == IntPtr.Zero)
                {
                    Logger.Warn($"加载libvlccore.dll失败: path={corePath}, error={Marshal.GetLastWin32Error()}");
                    return false;
                }

                _libVlcHandle = LoadLibraryEx(libPath, IntPtr.Zero,
                    LoadWithAlteredSearchPath);
                if (_libVlcHandle == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    FreeLibrary(_libVlcCoreHandle);
                    _libVlcCoreHandle = IntPtr.Zero;
                    Logger.Warn($"加载libvlc.dll失败: path={libPath}, error={error}");
                    return false;
                }

                _fnNew = GetDelegate<LibvlcNew>("libvlc_new");
                _fnRelease = GetDelegate<LibvlcRelease>("libvlc_release");
                _fnMediaNewLocation = GetDelegate<LibvlcMediaNewLocation>("libvlc_media_new_location");
                _fnMediaAddOption = GetDelegate<LibvlcMediaAddOption>("libvlc_media_add_option");
                _fnMediaRelease = GetDelegate<LibvlcMediaRelease>("libvlc_media_release");
                _fnPlayerNewFromMedia = GetDelegate<LibvlcMediaPlayerNewFromMedia>("libvlc_media_player_new_from_media");
                _fnPlayerRelease = GetDelegate<LibvlcMediaPlayerRelease>("libvlc_media_player_release");
                _fnPlayerSetHwnd = GetDelegate<LibvlcMediaPlayerSetHwnd>("libvlc_media_player_set_hwnd");
                _fnPlayerPlay = GetDelegate<LibvlcMediaPlayerPlay>("libvlc_media_player_play");
                _fnPlayerStop = GetDelegate<LibvlcMediaPlayerStop>("libvlc_media_player_stop");
                _fnPlayerGetState = GetDelegate<LibvlcMediaPlayerGetState>("libvlc_media_player_get_state");
                _fnPlayerGetTime = GetDelegate<LibvlcMediaPlayerGetTime>("libvlc_media_player_get_time");
                _fnVideoTakeSnapshot = GetDelegate<LibvlcVideoTakeSnapshot>("libvlc_video_take_snapshot");
                _fnVideoGetSize = GetDelegate<LibvlcVideoGetSize>("libvlc_video_get_size");
                _fnVideoSetAspectRatio = GetDelegate<LibvlcVideoSetAspectRatio>("libvlc_video_set_aspect_ratio");
                _fnVideoSetScale = GetDelegate<LibvlcVideoSetScale>("libvlc_video_set_scale");
                _fnVideoSetMouseInput = GetDelegate<LibvlcVideoSetInput>("libvlc_video_set_mouse_input");
                _fnVideoSetKeyInput = GetDelegate<LibvlcVideoSetInput>("libvlc_video_set_key_input");

                if (_fnNew == null || _fnPlayerPlay == null)
                {
                    Logger.Warn("libVLC缺少必要导出函数");
                    Unload();
                    return false;
                }

                _vlcDir = dir;
                Logger.Debug($"VLC已加载: {dir}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"加载VLC异常: dir={dir}, error={ex.Message}");
                Unload();
                return false;
            }
        }

        // VLC 渲染子窗口句柄，与 Delphi 的 CreateWindowEx STATIC 窗口一致
        private IntPtr _videoHwnd = IntPtr.Zero;

        public bool Play(string rtspUrl, IntPtr parentHwnd, int networkCachingMs, int liveCachingMs,
            string rtspTransport = "", int sourceWidth = 0, int sourceHeight = 0, bool swapDimensions = false,
            bool visible = true, bool directRenderTarget = false)
        {
            if (_fnNew == null && !LoadVlc()) return false;
            if (parentHwnd == IntPtr.Zero || !IsWindow(parentHwnd)) return false;

            Stop();

            _currentParentHwnd = parentHwnd;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _directRenderTarget = directRenderTarget;
            _lastHostW = _lastHostH = _lastSrcW = _lastSrcH = 0;

            try
            {
                // 1）使用与 Delphi 完全一致的参数创建 VLC 实例
                var args = new List<string>
                {
                    "--no-video-title-show", "--no-xlib", "--quiet", "--no-plugins-cache"
                };

                var pluginsPath = Path.Combine(_vlcDir ?? "", "plugins");
                if (Directory.Exists(pluginsPath))
                    args.Add("--plugin-path=" + pluginsPath);

                var argPtrs = new IntPtr[args.Count];
                for (int i = 0; i < args.Count; i++)
                    argPtrs[i] = Marshal.StringToHGlobalAnsi(args[i]);
                var argvPtr = Marshal.AllocHGlobal(IntPtr.Size * args.Count);
                Marshal.Copy(argPtrs, 0, argvPtr, args.Count);

                var safeUrl = SanitizeUrlForLog(rtspUrl);
                Logger.Debug($"VLC启动步骤：创建实例，url={safeUrl}");
                _vlcInstance = _fnNew(args.Count, argvPtr);

                for (int i = 0; i < args.Count; i++)
                    Marshal.FreeHGlobal(argPtrs[i]);
                Marshal.FreeHGlobal(argvPtr);

                if (_vlcInstance == IntPtr.Zero)
                {
                    Logger.Error("Failed to create VLC instance");
                    CleanupPartial();
                    return false;
                }

                // 2）创建媒体对象并设置选项
                Logger.Debug($"VLC启动步骤：创建媒体，url={safeUrl}");
                var mrlPtr = Marshal.StringToHGlobalAnsi(rtspUrl);
                var media = _fnMediaNewLocation(_vlcInstance, mrlPtr);
                Marshal.FreeHGlobal(mrlPtr);

                if (media == IntPtr.Zero)
                {
                    Logger.Error("Failed to create VLC media");
                    CleanupPartial();
                    return false;
                }

                var isHttp = rtspUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                          || rtspUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                if (isHttp)
                {
                    AddMediaOption(media, $":network-caching={networkCachingMs}");
                    AddMediaOption(media, $":live-caching={liveCachingMs}");
                    AddMediaOption(media, ":drop-late-frames");
                    AddMediaOption(media, ":skip-frames");
                    AddMediaOption(media, ":clock-jitter=0");
                    AddMediaOption(media, ":clock-synchro=0");
                    AddMediaOption(media, ":no-audio");
                }
                else
                {
                    if (string.Equals(rtspTransport, "tcp", StringComparison.OrdinalIgnoreCase))
                        AddMediaOption(media, ":rtsp-tcp");
                    else if (string.Equals(rtspTransport, "udp", StringComparison.OrdinalIgnoreCase))
                        AddMediaOption(media, ":rtsp-udp");
                    AddMediaOption(media, $":network-caching={networkCachingMs}");
                    AddMediaOption(media, $":live-caching={liveCachingMs}");
                    AddMediaOption(media, ":drop-late-frames");
                    AddMediaOption(media, ":skip-frames");
                    AddMediaOption(media, ":clock-jitter=0");
                    AddMediaOption(media, ":clock-synchro=0");
                    AddMediaOption(media, ":no-audio");
                }

                // 3）创建播放器
                Logger.Debug($"VLC启动步骤：创建播放器，url={safeUrl}");
                _mediaPlayer = _fnPlayerNewFromMedia(media);
                _fnMediaRelease(media);

                if (_mediaPlayer == IntPtr.Zero)
                {
                    Logger.Error("Failed to create VLC media player");
                    CleanupPartial();
                    return false;
                }

                // 外部车牌预览将 libVLC 直接绑定到调用方持有的 HWND。
                // 本地或调试会话继续使用 Proxy 持有的子窗口，使绘制生命周期隔离在 Proxy 进程内。
                if (directRenderTarget)
                {
                    Logger.Debug($"VLC启动步骤：直接绑定目标窗口，url={safeUrl}，target={parentHwnd}");
                    _videoHwnd = parentHwnd;
                    _ownsVideoHwnd = false;
                }
                else
                {
                    Logger.Debug($"VLC启动步骤：创建视频窗口，url={safeUrl}，parent={parentHwnd}");
                    var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_DISABLED;
                    if (visible)
                        windowStyle |= WS_VISIBLE;
                    _videoHwnd = CreateWindowEx(WS_EX_NOPARENTNOTIFY | WS_EX_NOACTIVATE,
                        "STATIC", "", windowStyle,
                        0, 0, 1, 1, parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
                    _ownsVideoHwnd = _videoHwnd != IntPtr.Zero;
                    if (_videoHwnd == IntPtr.Zero)
                    {
                        Logger.Error("Failed to create video child window");
                        CleanupPartial();
                        return false;
                    }
                }

                // 5）将 VLC 渲染目标设置为子窗口
                _fnPlayerSetHwnd(_mediaPlayer, _videoHwnd);
                _fnVideoSetMouseInput?.Invoke(_mediaPlayer, 0);
                _fnVideoSetKeyInput?.Invoke(_mediaPlayer, 0);

                // 6）开始播放
                Logger.Debug($"VLC启动步骤：开始播放，url={safeUrl}，videoHwnd={_videoHwnd}");
                if (_fnPlayerPlay(_mediaPlayer) != 0)
                {
                    Logger.Error("VLC play returned error");
                    CleanupPartial();
                    return false;
                }
                _running = true;

                // 7）缩放以填满窗口，与 Delphi 调用 libvlc_video_set_scale(0.0) 一致
                if (_fnVideoSetScale != null)
                    _fnVideoSetScale(_mediaPlayer, 0.0f);

                // 8）播放后应用 Cover 布局，调用顺序与 Delphi 一致
                ApplyCoverLayout();
                Logger.Debug($"VLC播放参数：url={safeUrl}，videoHwnd={_videoHwnd}，parent={parentHwnd}，network_cache={networkCachingMs}ms，live_cache={liveCachingMs}ms，transport={rtspTransport}，visible={visible}，direct={directRenderTarget}");

                Logger.Info($"VLC播放成功: {safeUrl} -> videoHwnd={_videoHwnd}, parent={parentHwnd}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC播放异常: url={SanitizeUrlForLog(rtspUrl)}, 错误={ex.Message}", ex);
                CleanupPartial();
                return false;
            }
        }

        /// <summary>
        /// 清理本次 Play 失败时创建的资源，不处理上次成功播放的资源；后者由 Stop 管理。
        /// </summary>
        private void CleanupPartial()
        {
            if (_mediaPlayer != IntPtr.Zero) { try { _fnPlayerSetHwnd?.Invoke(_mediaPlayer, IntPtr.Zero); } catch { } }
            if (_mediaPlayer != IntPtr.Zero) { try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { } _mediaPlayer = IntPtr.Zero; }
            if (_vlcInstance != IntPtr.Zero) { try { _fnRelease?.Invoke(_vlcInstance); } catch { } _vlcInstance = IntPtr.Zero; }
            if (_ownsVideoHwnd && _videoHwnd != IntPtr.Zero) { DestroyWindow(_videoHwnd); }
            _videoHwnd = IntPtr.Zero;
            _ownsVideoHwnd = false;
            _directRenderTarget = false;
            _running = false;
        }

        /// <summary>
        /// 释放 VLC 实例和视频窗口，但保留已加载的 DLL 以维持预热状态。
        /// 预热后调用 FreeLibrary 会导致后续 libvlc_new 失败。
        /// </summary>
        public void StopKeepDlls()
        {
            if (_mediaPlayer != IntPtr.Zero)
            {
                try { _fnPlayerSetHwnd?.Invoke(_mediaPlayer, IntPtr.Zero); } catch { }
                try { _fnPlayerStop?.Invoke(_mediaPlayer); } catch { }
                try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { }
                _mediaPlayer = IntPtr.Zero;
            }
            if (_vlcInstance != IntPtr.Zero)
            {
                try { _fnRelease?.Invoke(_vlcInstance); } catch { }
                _vlcInstance = IntPtr.Zero;
            }
            if (_ownsVideoHwnd && _videoHwnd != IntPtr.Zero)
            {
                DestroyWindow(_videoHwnd);
            }
            _videoHwnd = IntPtr.Zero;
            _ownsVideoHwnd = false;
            _directRenderTarget = false;
            _running = false;
            _currentParentHwnd = IntPtr.Zero;
        }

        /// <summary>
        /// 将渲染窗口迁移到新的父窗口，并应用 Cover 布局。
        /// </summary>
        public bool SetParentWindow(IntPtr newParentHwnd)
        {
            if (_videoHwnd == IntPtr.Zero || _directRenderTarget) return false;

            try
            {
                _currentParentHwnd = newParentHwnd;
                if (newParentHwnd != IntPtr.Zero)
                {
                    SetParent(_videoHwnd, newParentHwnd);
                    ShowWindow(_videoHwnd, SW_SHOWNOACTIVATE);
                    ApplyCoverLayout();
                }
                else
                {
                    ShowWindow(_videoHwnd, SW_HIDE);
                    SetParent(_videoHwnd, IntPtr.Zero);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"SetParentWindow failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 对视频子窗口应用 Cover 布局，算法与 Delphi ApplyCoverLayout 一致。
        /// 使用 MulDiv 进行精确整数缩放，避免浮点舍入差异。
        /// </summary>
        public void ApplyCoverLayout()
        {
            if (!_running || _mediaPlayer == IntPtr.Zero) return;
            if (_videoHwnd == IntPtr.Zero || !IsWindow(_videoHwnd)) return;
            if (_currentParentHwnd == IntPtr.Zero || !IsWindow(_currentParentHwnd)) return;

            try
            {
                RECT hostRect;
                if (!GetClientRect(_currentParentHwnd, out hostRect)) return;

                int hostW = hostRect.Right - hostRect.Left;
                int hostH = hostRect.Bottom - hostRect.Top;
                if (hostW <= 0 || hostH <= 0) return;

                int srcW = _sourceWidth;
                int srcH = _sourceHeight;
                if ((srcW <= 0 || srcH <= 0) && TryGetVideoSize(out var actualWidth, out var actualHeight))
                {
                    srcW = actualWidth;
                    srcH = actualHeight;
                }
                if (srcW <= 0 || srcH <= 0)
                {
                    srcW = hostW;
                    srcH = hostH;
                }
                if (srcW <= 0 || srcH <= 0) return;

                // 竖向视频源需交换宽高，例如 480x640 相机画面
                int displayW = srcW, displayH = srcH;
                if (_swapDimensions)
                {
                    displayW = srcH;
                    displayH = srcW;
                }

                if (hostW == _lastHostW && hostH == _lastHostH &&
                    displayW == _lastSrcW && displayH == _lastSrcH)
                    return;

                _lastHostW = hostW;
                _lastHostH = hostH;
                _lastSrcW = displayW;
                _lastSrcH = displayH;

                // 1）通知 VLC 精确缩放并填满窗口，与 Delphi 的 libvlc_video_set_scale(0.0) 一致
                if (_fnVideoSetScale != null)
                    _fnVideoSetScale(_mediaPlayer, 0.0f);

                // 2）直接绑定第三方 HWND 时使用宿主客户区比例，确保画面拉伸铺满。
                // 其他会话继续使用原视频源比例和 Cover 布局。
                if (_fnVideoSetAspectRatio != null)
                {
                    var ratioStr = GetAspectRatioForLayout(_directRenderTarget,
                        hostW, hostH, displayW, displayH);
                    var ratioPtr = Marshal.StringToHGlobalAnsi(ratioStr);
                    try
                    {
                        _fnVideoSetAspectRatio(_mediaPlayer, ratioPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ratioPtr);
                    }
                }

                // 直接渲染目标就是第三方窗口本身，只能调整 VLC 宽高比，不能移动该窗口。
                if (_directRenderTarget)
                    return;

                // 3）使用与 Delphi 一致的 MulDiv 算法计算 Cover 布局位置和尺寸
                int videoW, videoH, videoX, videoY;
                if (displayW * hostH > displayH * hostW)
                {
                    // 按高度适配并产生左右黑边，计算方式与 Delphi MulDiv 一致
                    videoH = hostH;
                    videoW = (displayW * hostH) / displayH;
                    videoX = (hostW - videoW) / 2;
                    videoY = 0;
                }
                else
                {
                    // 按宽度适配并产生上下黑边，计算方式与 Delphi MulDiv 一致
                    videoW = hostW;
                    videoH = (displayH * hostW) / displayW;
                    videoX = 0;
                    videoY = (hostH - videoH) / 2;
                }

                SetWindowPos(_videoHwnd, HWND_BOTTOM, videoX, videoY, videoW, videoH,
                    SWP_NOACTIVATE);
            }
            catch (Exception ex)
            {
                Logger.Error($"ApplyCoverLayout failed: {ex.Message}");
            }
        }

        internal static string GetAspectRatioForLayout(bool directRenderTarget,
            int hostW, int hostH, int displayW, int displayH)
        {
            return directRenderTarget
                ? $"{hostW}:{hostH}"
                : $"{displayW}:{displayH}";
        }

        private void AddMediaOption(IntPtr media, string option)
        {
            var optPtr = Marshal.StringToHGlobalAnsi(option);
            _fnMediaAddOption(media, optPtr);
            Marshal.FreeHGlobal(optPtr);
        }

        public void Stop()
        {
            // 按 Delphi 的顺序停止并释放 VLC 播放器
            if (_mediaPlayer != IntPtr.Zero)
            {
                try { _fnPlayerSetHwnd?.Invoke(_mediaPlayer, IntPtr.Zero); } catch { }
                try { _fnPlayerStop?.Invoke(_mediaPlayer); } catch { }
                try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { }
                _mediaPlayer = IntPtr.Zero;
            }
            if (_vlcInstance != IntPtr.Zero)
            {
                try { _fnRelease?.Invoke(_vlcInstance); } catch { }
                _vlcInstance = IntPtr.Zero;
            }

            // 销毁视频子窗口，与 Delphi 的 DestroyWindow 行为一致
            if (_ownsVideoHwnd && _videoHwnd != IntPtr.Zero)
            {
                DestroyWindow(_videoHwnd);
            }
            _videoHwnd = IntPtr.Zero;
            _ownsVideoHwnd = false;
            _directRenderTarget = false;

            _running = false;
            _currentParentHwnd = IntPtr.Zero;
            _lastHostW = _lastHostH = _lastSrcW = _lastSrcH = 0;
        }

        private void Unload()
        {
            Stop();
            Thread.Sleep(100);
            if (_libVlcHandle != IntPtr.Zero) { FreeLibrary(_libVlcHandle); _libVlcHandle = IntPtr.Zero; }
            if (_libVlcCoreHandle != IntPtr.Zero) { FreeLibrary(_libVlcCoreHandle); _libVlcCoreHandle = IntPtr.Zero; }
            _fnNew = null; _fnRelease = null; _fnMediaNewLocation = null; _fnMediaAddOption = null;
            _fnMediaRelease = null; _fnPlayerNewFromMedia = null; _fnPlayerRelease = null;
            _fnPlayerGetState = null; _fnPlayerGetTime = null;
            _fnVideoTakeSnapshot = null; _fnVideoGetSize = null;
            _fnPlayerSetHwnd = null; _fnPlayerPlay = null; _fnPlayerStop = null;
            _fnVideoSetAspectRatio = null; _fnVideoSetScale = null;
            _fnVideoSetMouseInput = null; _fnVideoSetKeyInput = null;
        }

        internal static string SanitizeUrlForLog(string value)
        {
            return Logger.SanitizeUrlForLog(value);
        }

        internal static string[] GetLocalVlcDirectoryNames(bool is64BitProcess)
        {
            return is64BitProcess
                ? new[] { "vlc-x64", "vlc" }
                : new[] { "vlc" };
        }

        internal static bool IsPeMachineCompatible(
            string filePath,
            bool is64BitProcess,
            out ushort machine)
        {
            machine = 0;
            try
            {
                using (var stream = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d)
                        return false;

                    stream.Position = 0x3c;
                    var peOffset = reader.ReadInt32();
                    if (peOffset < 0 || peOffset > stream.Length - 6)
                        return false;

                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550)
                        return false;

                    machine = reader.ReadUInt16();
                    var expected = is64BitProcess
                        ? ImageFileMachineAmd64
                        : ImageFileMachineI386;
                    return machine == expected;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string FormatPeMachine(ushort machine)
        {
            if (machine == ImageFileMachineI386) return "x86(0x014C)";
            if (machine == ImageFileMachineAmd64) return "x64(0x8664)";
            return $"unknown(0x{machine:X4})";
        }

        private static void DisableRiskySftpPlugin(string vlcDir)
        {
            if (string.IsNullOrEmpty(vlcDir))
                return;

            string fullDir;
            try
            {
                fullDir = Path.GetFullPath(vlcDir);
            }
            catch
            {
                fullDir = vlcDir;
            }

            lock (RiskyPluginCheckLock)
            {
                if (!RiskyPluginCheckedDirs.Add(fullDir))
                    return;
            }

            var pluginPath = Path.Combine(fullDir, RiskySftpPluginRelativePath);
            if (!File.Exists(pluginPath))
                return;

            Logger.Warn($"检测到VLC SFTP插件: {pluginPath}。RTSP预览不需要该插件，现场已出现该插件导致的崩溃，正在尝试禁用。");

            var disabledPath = pluginPath + ".disabled";
            if (File.Exists(disabledPath))
                disabledPath = pluginPath + ".disabled." + DateTime.Now.ToString("yyyyMMddHHmmss");

            try
            {
                File.Move(pluginPath, disabledPath);
                Logger.Warn($"已禁用VLC SFTP插件: {disabledPath}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"禁用VLC SFTP插件失败: {ex.Message}。请手动将该文件改名为 libsftp_plugin.dll.disabled 后再启动程序。");
            }
        }

        private T GetDelegate<T>(string procName) where T : class
        {
            var ptr = GetProcAddress(_libVlcHandle, procName);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        public void Dispose() { Unload(); }

        // Win32 API 声明
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName,
            IntPtr hFile, uint dwFlags);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr hModule);
        [DllImport("kernel32.dll")] private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName,
            string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);

        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_HIDE = 0;
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
        private struct RECT { public int Left, Top, Right, Bottom; }
    }
}
