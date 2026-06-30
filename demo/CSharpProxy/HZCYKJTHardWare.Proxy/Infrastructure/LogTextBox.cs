using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public class LogTextBox : RichTextBox
    {
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int SB_VERT = 1;
        private const uint SIF_RANGE = 0x0001;
        private const uint SIF_PAGE = 0x0002;
        private const uint SIF_POS = 0x0004;
        private const uint SIF_TRACKPOS = 0x0010;
        private const uint SIF_ALL = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS;

        private bool _isAtTop = true;
        private bool _isAtBottom = true;
        private bool _suppressScrollDetection;

        public bool AutoScroll { get; set; } = true;
        public bool IsAtTop => _isAtTop;
        public bool IsAtBottom => _isAtBottom;
        public int VerticalScrollPos { get; private set; }

        public bool SuppressScrollDetection
        {
            get => _suppressScrollDetection;
            set => _suppressScrollDetection = value;
        }

        public event EventHandler ScrolledToTop;
        public event EventHandler ScrolledToBottom;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (_suppressScrollDetection) return;

            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_KEYDOWN)
            {
                CheckScrollPosition(true);
            }
        }

        public void RefreshScrollState()
        {
            CheckScrollPosition(false);
        }

        public void ScrollToBottomProgrammatically()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ScrollToBottomProgrammatically));
                return;
            }

            try
            {
                SuppressScrollDetection = true;
                SelectionStart = TextLength;
                SelectionLength = 0;
                ScrollToCaret();
            }
            finally
            {
                SuppressScrollDetection = false;
                RefreshScrollState();
                _isAtBottom = true;
                _isAtTop = TextLength == 0;
            }
        }

        private void CheckScrollPosition(bool raiseTopEvent)
        {
            if (!IsHandleCreated)
                return;

            var info = new SCROLLINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(SCROLLINFO)),
                fMask = SIF_ALL
            };

            if (!GetScrollInfo(Handle, SB_VERT, ref info))
                return;

            int page = info.nPage > int.MaxValue ? int.MaxValue : (int)info.nPage;
            int lastVisiblePos = Math.Max(info.nMin, info.nMax - Math.Max(page - 1, 0));
            int pos = info.nPos;

            VerticalScrollPos = pos;

            bool nowAtBottom = pos >= lastVisiblePos - 1;
            bool nowAtTop = pos <= info.nMin + 1;

            if (nowAtBottom && !_isAtBottom)
            {
                _isAtBottom = true;
                ScrolledToBottom?.Invoke(this, EventArgs.Empty);
            }
            else if (!nowAtBottom && _isAtBottom)
            {
                _isAtBottom = false;
            }

            _isAtTop = nowAtTop;

            if (nowAtTop && raiseTopEvent)
            {
                ScrolledToTop?.Invoke(this, EventArgs.Empty);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi);

        [StructLayout(LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public uint cbSize;
            public uint fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;
        }
    }
}
