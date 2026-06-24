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

        private bool _isAtBottom = true;
        internal bool _suppressScrollDetection;

        public bool AutoScroll { get; set; } = true;

        public event EventHandler ScrolledToTop;
        public event EventHandler ScrolledToBottom;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (_suppressScrollDetection) return;

            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_KEYDOWN)
            {
                CheckScrollPosition();
            }
        }

        private void CheckScrollPosition()
        {
            int pos = GetScrollPos(Handle, SB_VERT);
            int max = GetScrollLimit(Handle, SB_VERT);

            bool nowAtBottom = (pos >= max - 1);
            bool nowAtTop = (pos <= 0);

            if (nowAtBottom && !_isAtBottom)
            {
                _isAtBottom = true;
                AutoScroll = true;
                ScrolledToBottom?.Invoke(this, EventArgs.Empty);
            }
            else if (!nowAtBottom && _isAtBottom)
            {
                _isAtBottom = false;
                AutoScroll = false;
            }

            if (nowAtTop)
            {
                ScrolledToTop?.Invoke(this, EventArgs.Empty);
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll")]
        private static extern int GetScrollLimit(IntPtr hWnd, int nBar);
    }
}
