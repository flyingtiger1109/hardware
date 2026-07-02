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
        private string _vlcDir;
        private const string RiskySftpPluginRelativePath = @"plugins\access\libsftp_plugin.dll";
        private static readonly object RiskyPluginCheckLock = new object();
        private static readonly HashSet<string> RiskyPluginCheckedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Source dimensions for cover layout
        private int _sourceWidth;
        private int _sourceHeight;
        private bool _swapDimensions;

        // Cached layout
        private int _lastHostW, _lastHostH, _lastSrcW, _lastSrcH;

        // Delegates for libvlc functions
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
        private delegate void LibvlcVideoSetAspectRatio(IntPtr player, IntPtr ratio);
        private delegate void LibvlcVideoSetScale(IntPtr player, float factor);

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
        private LibvlcVideoSetAspectRatio _fnVideoSetAspectRatio;
        private LibvlcVideoSetScale _fnVideoSetScale;

        public bool IsRunning => _running && _videoHwnd != IntPtr.Zero && IsWindow(_videoHwnd);
        public IntPtr RenderFormHandle => _videoHwnd;
        public int WarmupMs { get; private set; }

        /// <summary>
        /// Pre-load VLC libraries and create a short-lived instance to warm up the VLC engine.
        /// This significantly reduces first-playback latency (same as Delphi TVlcWarmupThread).
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

                // Create a minimal VLC instance to trigger library initialization
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

            // Priority 1: local vlc directory in output (same as Delphi — full plugins)
            var localVlcDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vlc");
            if (System.IO.Directory.Exists(localVlcDir))
            {
                if (TryLoadFromDir(localVlcDir)) return true;
            }

            // Priority 2: try extracted embedded resources (backward compat)
            var extractedDir = VlcResourceExtractor.EnsureExtracted();
            if (!string.IsNullOrEmpty(extractedDir) && System.IO.Directory.Exists(extractedDir))
            {
                if (TryLoadFromDir(extractedDir)) return true;
            }

            // Priority 3: external VLC installations
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
                DisableRiskySftpPlugin(dir);
                SetDllDirectory(dir);
                _libVlcCoreHandle = LoadLibrary(corePath);
                _libVlcHandle = LoadLibrary(libPath);
                if (_libVlcHandle == IntPtr.Zero) return false;

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
                _fnVideoSetAspectRatio = GetDelegate<LibvlcVideoSetAspectRatio>("libvlc_video_set_aspect_ratio");
                _fnVideoSetScale = GetDelegate<LibvlcVideoSetScale>("libvlc_video_set_scale");

                if (_fnNew == null || _fnPlayerPlay == null) { Unload(); return false; }

                _vlcDir = dir;
                Logger.Debug($"VLC已加载: {dir}");
                return true;
            }
            catch { return false; }
        }

        // Child window handle for VLC rendering (same as Delphi's CreateWindowEx STATIC)
        private IntPtr _videoHwnd = IntPtr.Zero;

        public bool Play(string rtspUrl, IntPtr parentHwnd, int networkCachingMs, int liveCachingMs,
            string rtspTransport = "", int sourceWidth = 0, int sourceHeight = 0, bool swapDimensions = false,
            bool visible = true)
        {
            if (_fnNew == null && !LoadVlc()) return false;
            if (parentHwnd == IntPtr.Zero || !IsWindow(parentHwnd)) return false;

            Stop();

            _currentParentHwnd = parentHwnd;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _swapDimensions = swapDimensions;
            _lastHostW = _lastHostH = _lastSrcW = _lastSrcH = 0;

            try
            {
                // 1) Create VLC instance (exact same args as Delphi)
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

                Logger.Info($"VLC启动步骤：创建实例，url={rtspUrl}");
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

                // 2) Create media with options
                Logger.Info($"VLC启动步骤：创建媒体，url={rtspUrl}");
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

                // 3) Create player
                Logger.Info($"VLC启动步骤：创建播放器，url={rtspUrl}");
                _mediaPlayer = _fnPlayerNewFromMedia(media);
                _fnMediaRelease(media);

                if (_mediaPlayer == IntPtr.Zero)
                {
                    Logger.Error("Failed to create VLC media player");
                    CleanupPartial();
                    return false;
                }

                // 4) Create child window (STATIC) for VLC — same as Delphi CreateWindowEx('STATIC', WS_CHILD)
                Logger.Info($"VLC启动步骤：创建视频窗口，url={rtspUrl}，parent={parentHwnd}");
                // The preview is display-only. Disabling the cross-process child prevents
                // mouse clicks from moving input focus from the third-party host to Proxy.
                var windowStyle = WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_DISABLED;
                if (visible)
                    windowStyle |= WS_VISIBLE;
                _videoHwnd = CreateWindowEx(WS_EX_NOPARENTNOTIFY | WS_EX_NOACTIVATE,
                    "STATIC", "", windowStyle,
                    0, 0, 1, 1, parentHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
                if (_videoHwnd == IntPtr.Zero)
                {
                    Logger.Error("Failed to create video child window");
                    CleanupPartial();
                    return false;
                }

                // 5) Set VLC render target → child window
                _fnPlayerSetHwnd(_mediaPlayer, _videoHwnd);

                // 6) Play
                Logger.Info($"VLC启动步骤：开始播放，url={rtspUrl}，videoHwnd={_videoHwnd}");
                if (_fnPlayerPlay(_mediaPlayer) != 0)
                {
                    Logger.Error("VLC play returned error");
                    CleanupPartial();
                    return false;
                }
                _running = true;

                // 7) Scale to fill window (same as Delphi libvlc_video_set_scale(0.0))
                if (_fnVideoSetScale != null)
                    _fnVideoSetScale(_mediaPlayer, 0.0f);

                // 8) Apply cover layout AFTER play (same order as Delphi)
                ApplyCoverLayout();
                Logger.Info($"VLC播放参数：url={rtspUrl}，videoHwnd={_videoHwnd}，parent={parentHwnd}，network_cache={networkCachingMs}ms，live_cache={liveCachingMs}ms，transport={rtspTransport}，visible={visible}");

                Logger.Info($"VLC播放成功: {rtspUrl} -> videoHwnd={_videoHwnd}, parent={parentHwnd}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"VLC播放异常: url={rtspUrl}, 错误={ex.Message}", ex);
                CleanupPartial();
                return false;
            }
        }

        /// <summary>
        /// Clean up resources created during a failed Play attempt, without touching resources
        /// from a previous successful Play (those are managed by Stop).
        /// </summary>
        private void CleanupPartial()
        {
            if (_mediaPlayer != IntPtr.Zero) { try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { } _mediaPlayer = IntPtr.Zero; }
            if (_vlcInstance != IntPtr.Zero) { try { _fnRelease?.Invoke(_vlcInstance); } catch { } _vlcInstance = IntPtr.Zero; }
            if (_videoHwnd != IntPtr.Zero) { DestroyWindow(_videoHwnd); _videoHwnd = IntPtr.Zero; }
            _running = false;
        }

        /// <summary>
        /// Release VLC instance and video window but keep DLLs loaded (for warmup).
        /// Calling FreeLibrary after warmup causes subsequent libvlc_new to fail.
        /// </summary>
        public void StopKeepDlls()
        {
            if (_mediaPlayer != IntPtr.Zero)
            {
                try { _fnPlayerStop?.Invoke(_mediaPlayer); } catch { }
                try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { }
                _mediaPlayer = IntPtr.Zero;
            }
            if (_vlcInstance != IntPtr.Zero)
            {
                try { _fnRelease?.Invoke(_vlcInstance); } catch { }
                _vlcInstance = IntPtr.Zero;
            }
            if (_videoHwnd != IntPtr.Zero)
            {
                DestroyWindow(_videoHwnd);
                _videoHwnd = IntPtr.Zero;
            }
            _running = false;
            _currentParentHwnd = IntPtr.Zero;
        }

        /// <summary>
        /// Reparent the render window to a new parent and apply cover layout.
        /// </summary>
        public bool SetParentWindow(IntPtr newParentHwnd)
        {
            if (_videoHwnd == IntPtr.Zero) return false;

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
        /// Apply cover-fit layout to child video window (same algorithm as Delphi ApplyCoverLayout).
        /// Uses MulDiv for precise integer scaling — avoids floating-point rounding differences.
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

                int srcW = _sourceWidth > 0 ? _sourceWidth : hostW;
                int srcH = _sourceHeight > 0 ? _sourceHeight : hostH;
                if (srcW <= 0 || srcH <= 0) return;

                // Swap for portrait sources (e.g. camera 480x640)
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

                // 1) Tell VLC to scale to fill window exactly (same as Delphi libvlc_video_set_scale(0.0))
                if (_fnVideoSetScale != null)
                    _fnVideoSetScale(_mediaPlayer, 0.0f);

                // 2) Set aspect ratio (same as Delphi)
                if (_fnVideoSetAspectRatio != null)
                {
                    var ratioStr = $"{displayW}:{displayH}";
                    var ratioPtr = Marshal.StringToHGlobalAnsi(ratioStr);
                    _fnVideoSetAspectRatio(_mediaPlayer, ratioPtr);
                    Marshal.FreeHGlobal(ratioPtr);
                }

                // 3) Calculate cover-fit position/size (same MulDiv algorithm as Delphi)
                int videoW, videoH, videoX, videoY;
                if (displayW * hostH > displayH * hostW)
                {
                    // Fit by height (pillarbox) - same as Delphi MulDiv
                    videoH = hostH;
                    videoW = (displayW * hostH) / displayH;
                    videoX = (hostW - videoW) / 2;
                    videoY = 0;
                }
                else
                {
                    // Fit by width (letterbox) - same as Delphi MulDiv
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

        private void AddMediaOption(IntPtr media, string option)
        {
            var optPtr = Marshal.StringToHGlobalAnsi(option);
            _fnMediaAddOption(media, optPtr);
            Marshal.FreeHGlobal(optPtr);
        }

        public void Stop()
        {
            // Stop and release VLC player (same order as Delphi)
            if (_mediaPlayer != IntPtr.Zero)
            {
                try { _fnPlayerStop?.Invoke(_mediaPlayer); } catch { }
                try { _fnPlayerRelease?.Invoke(_mediaPlayer); } catch { }
                _mediaPlayer = IntPtr.Zero;
            }
            if (_vlcInstance != IntPtr.Zero)
            {
                try { _fnRelease?.Invoke(_vlcInstance); } catch { }
                _vlcInstance = IntPtr.Zero;
            }

            // Destroy child video window (same as Delphi DestroyWindow)
            if (_videoHwnd != IntPtr.Zero)
            {
                DestroyWindow(_videoHwnd);
                _videoHwnd = IntPtr.Zero;
            }

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
            _fnPlayerSetHwnd = null; _fnPlayerPlay = null; _fnPlayerStop = null;
            _fnVideoSetAspectRatio = null; _fnVideoSetScale = null;
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

        // Win32 API
        [DllImport("kernel32.dll")] private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr hModule);
        [DllImport("kernel32.dll")] private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")] private static extern bool SetDllDirectory(string lpPathName);
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
