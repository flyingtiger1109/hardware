using System;
using System.Runtime.InteropServices;

namespace HZCYKJTHardWare.CSharpDemo.Native
{
    internal static class DpiAwareness
    {
        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new IntPtr(-4);

        public static void Enable()
        {
            if (TrySetPerMonitorV2())
            {
                return;
            }

            if (TrySetPerMonitor())
            {
                return;
            }

            TrySetSystemAware();
        }

        private static bool TrySetPerMonitorV2()
        {
            try
            {
                return SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TrySetPerMonitor()
        {
            try
            {
                return SetProcessDpiAwareness(ProcessDpiAwareness.PerMonitorDpiAware) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void TrySetSystemAware()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (Exception)
            {
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private enum ProcessDpiAwareness
        {
            DpiUnaware = 0,
            SystemDpiAware = 1,
            PerMonitorDpiAware = 2
        }
    }
}
