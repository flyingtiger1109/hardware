using System;
using System.Runtime.InteropServices;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Preview
{
    /// <summary>
    /// 本进程无边框覆盖容器窗口。
    /// libVLC 子窗口挂载于此窗口，此窗口按第三方锚点窗口的屏幕客户区定时跟随，
    /// 使车牌视频在视觉上出现在第三方窗口区域，同时完全避免跨进程子窗口操作。
    /// 规格参照 todo.md「同进程覆盖容器修复」：仅锚点失效/不可见/顶层窗体最小化时隐藏，
    /// 不使用 HWND_TOPMOST，并缓存上次位置与显示状态以减少重复的窗口刷新。
    /// </summary>
    internal sealed class ExternalOverlayWindow : IDisposable
    {
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _anchorHwnd = IntPtr.Zero;
        private bool _disposed;

        // 缓存上次可见状态与屏幕矩形，未变化时不重复设置窗口，避免持续刷新。
        private bool _lastVisible;
        private int _lastX;
        private int _lastY;
        private int _lastW;
        private int _lastH;

        public IntPtr Hwnd => _hwnd;

        public bool Create(IntPtr anchorHwnd)
        {
            if (_hwnd != IntPtr.Zero)
                return true;
            if (anchorHwnd == IntPtr.Zero || !IsWindow(anchorHwnd))
                return false;

            _anchorHwnd = anchorHwnd;
            _hwnd = CreateWindowEx(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOPARENTNOTIFY,
                "STATIC", "", WS_POPUP | WS_VISIBLE,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                Logger.Error($"外部覆盖窗口创建失败：锚点={PreviewManager.FormatHwnd(anchorHwnd)}，" +
                             $"锚点有效={IsWindow(anchorHwnd)}，错误={Marshal.GetLastWin32Error()}");
                return false;
            }

            _lastVisible = false;
            _lastX = _lastY = _lastW = _lastH = 0;
            Logger.Info($"外部覆盖窗口已创建：覆盖窗口={PreviewManager.FormatHwnd(_hwnd)}，" +
                        $"锚点={PreviewManager.FormatHwnd(anchorHwnd)}，锚点有效={IsWindow(anchorHwnd)}");
            return true;
        }

        public void SetAnchor(IntPtr anchorHwnd)
        {
            _anchorHwnd = anchorHwnd;
        }

        /// <summary>
        /// 按锚点窗口客户区的屏幕矩形跟随覆盖窗口，并按条件隐藏。
        /// 应在与创建窗口一致的 STA 线程上调用。
        /// </summary>
        public void Follow()
        {
            if (_hwnd == IntPtr.Zero || !IsWindow(_hwnd))
                return;

            if (_anchorHwnd == IntPtr.Zero || !IsWindow(_anchorHwnd))
            {
                Logger.Warn($"外部覆盖窗口隐藏：原因=锚点句柄无效，锚点={PreviewManager.FormatHwnd(_anchorHwnd)}，" +
                            $"锚点有效={_anchorHwnd != IntPtr.Zero && IsWindow(_anchorHwnd)}");
                Hide();
                return;
            }

            var topLevel = GetAncestor(_anchorHwnd, GA_ROOT);
            if (topLevel == IntPtr.Zero)
                topLevel = _anchorHwnd;

            // 锚点不可见，或所属顶层窗体最小化时隐藏覆盖窗口。
            if (!IsWindowVisible(_anchorHwnd) || IsIconic(topLevel))
            {
                Logger.Info($"外部覆盖窗口隐藏：原因=锚点不可见或顶层窗口最小化，锚点={PreviewManager.FormatHwnd(_anchorHwnd)}，" +
                            $"锚点可见={IsWindowVisible(_anchorHwnd)}，顶层窗口={PreviewManager.FormatHwnd(topLevel)}，" +
                            $"顶层最小化={IsIconic(topLevel)}");
                Hide();
                return;
            }

            RECT rect;
            if (!GetWindowRect(_anchorHwnd, out rect))
            {
                Logger.Error($"外部覆盖窗口隐藏：原因=获取锚点矩形失败，锚点={PreviewManager.FormatHwnd(_anchorHwnd)}，" +
                             $"错误={Marshal.GetLastWin32Error()}");
                Hide();
                return;
            }

            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0)
            {
                Logger.Warn($"外部覆盖窗口隐藏：原因=锚点尺寸无效，锚点={PreviewManager.FormatHwnd(_anchorHwnd)}，" +
                            $"矩形=({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");
                Hide();
                return;
            }

            // 位置与显示状态均未改变时不做任何窗口操作，避免持续刷新造成视觉闪动。
            if (_lastVisible && w == _lastW && h == _lastH &&
                rect.Left == _lastX && rect.Top == _lastY)
                return;

            _lastX = rect.Left;
            _lastY = rect.Top;
            _lastW = w;
            _lastH = h;
            _lastVisible = true;

            // 覆盖窗口排在第三方顶层窗口之后（其上方）。若锚点顶层窗口本身是 TopMost
            //（车道程序常置顶防遮挡），覆盖窗口必须同步 TopMost 才能盖在它上面，否则会被车道窗口盖住而无画面。
            var anchorTopMost = IsWindowTopMost(topLevel);
            var insertAfter = anchorTopMost ? HWND_TOPMOST : topLevel;
            Logger.Info($"外部覆盖窗口定位：覆盖窗口={PreviewManager.FormatHwnd(_hwnd)}，锚点={PreviewManager.FormatHwnd(_anchorHwnd)}，" +
                        $"顶层窗口={PreviewManager.FormatHwnd(topLevel)}，顶层TopMost={anchorTopMost}，" +
                        $"位置=({rect.Left},{rect.Top}) 尺寸=({w}x{h})");
            var setOk = SetWindowPos(_hwnd, insertAfter, rect.Left, rect.Top, w, h,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // 布局自检：验证覆盖窗口实际可见性、置顶状态与最终矩形，便于现场定位。
            RECT actualRect;
            var actualOk = GetWindowRect(_hwnd, out actualRect);
            Logger.Info($"外部覆盖窗口定位完成：结果={setOk}，可见={IsWindowVisible(_hwnd)}，" +
                        $"TopMost={IsWindowTopMost(_hwnd)}，" +
                        $"实际矩形={(actualOk ? $"({actualRect.Left},{actualRect.Top})-({actualRect.Right},{actualRect.Bottom})" : "<获取失败>")}");
        }

        private void Hide()
        {
            if (_hwnd == IntPtr.Zero || !IsWindow(_hwnd))
            {
                _lastVisible = false;
                return;
            }

            if (_lastVisible)
                ShowWindow(_hwnd, SW_HIDE);
            _lastVisible = false;
        }

        public void Destroy()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_hwnd != IntPtr.Zero && IsWindow(_hwnd))
                DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        public void Dispose()
        {
            Destroy();
        }

        // ---- Win32 ----

        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName,
            string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

        private static bool IsWindowTopMost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return false;
            return (GetWindowLongW(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_EX_NOPARENTNOTIFY = 0x00000004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_HIDE = 0;
        private const uint GA_ROOT = 2;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
    }
}
